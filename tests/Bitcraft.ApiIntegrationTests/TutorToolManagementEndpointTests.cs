using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bitcraft.Api.Contracts.Tutoring;
using Bitcraft.Application.Common.Security;
using Bitcraft.Domain.Modeling;
using Bitcraft.Domain.Tutoring;
using Bitcraft.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bitcraft.ApiIntegrationTests;

public sealed partial class TutorRuntimeEndpointTests
{
    [Fact]
    public async Task ToolCrudCatalogAndAuthorizationAreTutorScoped()
    {
        using var client = factory.CreateClient();
        var owner = await AuthorizeAsRoleAsync(client, AppRoles.Editor);
        var tutor = await SeedActiveTutorAsync(owner);
        var otherTutor = await SeedActiveTutorAsync(owner);
        var root = $"/api/tutors/{tutor.TutorId}/tools";
        Assert.Empty((await client.GetFromJsonAsync<ToolDefinitionResponse[]>(root))!);
        var catalog = (await client.GetFromJsonAsync<TutorToolCatalogResponse[]>($"{root}/catalog"))!;
        var calculator = Assert.Single(catalog);
        Assert.Equal("builtin.calculator.v1", calculator.ImplementationKey);
        Assert.True(calculator.SupportsAutomaticExecution);
        var request = new RegisterToolDefinitionRequest("Calculator", "Calculates", ImplementationKey: calculator.ImplementationKey,
            ExecutionPolicy: TutorToolExecutionPolicy.ReadOnlyAutomatic);
        using var created = await client.PostAsJsonAsync(root, request);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var tool = (await created.Content.ReadFromJsonAsync<ToolDefinitionResponse>())!;
        Assert.Equal(calculator.JsonSchema, tool.JsonSchema);
        Assert.Equal(tool.Id, (await client.GetFromJsonAsync<ToolDefinitionResponse>(created.Headers.Location))!.Id);
        Assert.Single((await client.GetFromJsonAsync<ToolDefinitionResponse[]>(root))!);
        var update = new UpdateToolDefinitionRequest("Arithmetic", "Edited", TutorToolKind.BuiltIn,
            calculator.ImplementationKey, TutorToolExecutionPolicy.Disabled);
        using var updated = await client.PutAsJsonAsync($"{root}/{tool.Id}", update);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal(TutorToolExecutionPolicy.Disabled, (await updated.Content.ReadFromJsonAsync<ToolDefinitionResponse>())!.ExecutionPolicy);
        using var crossGet = await client.GetAsync($"/api/tutors/{otherTutor.TutorId}/tools/{tool.Id}");
        using var crossPut = await client.PutAsJsonAsync($"/api/tutors/{otherTutor.TutorId}/tools/{tool.Id}", update);
        using var crossDelete = await client.DeleteAsync($"/api/tutors/{otherTutor.TutorId}/tools/{tool.Id}");
        Assert.Equal(HttpStatusCode.NotFound, crossGet.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, crossPut.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, crossDelete.StatusCode);
        using var outsider = factory.CreateClient();
        await AuthorizeAsRoleAsync(outsider, AppRoles.Editor);
        foreach (var path in new[] { root, $"{root}/catalog", $"{root}/{tool.Id}" })
            Assert.Equal(HttpStatusCode.Forbidden, (await outsider.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await outsider.PostAsJsonAsync(root, request with { Name = "Other" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await outsider.PutAsJsonAsync($"{root}/{tool.Id}", update)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await outsider.DeleteAsync($"{root}/{tool.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"{root}/{tool.Id}")).StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<ToolDefinitionResponse[]>(root))!);
    }

    [Theory]
    [InlineData(TutorToolExecutionPolicy.Disabled, HttpStatusCode.OK)]
    [InlineData(TutorToolExecutionPolicy.RequiresApproval, HttpStatusCode.BadRequest)]
    public async Task DisabledToolsPermitTextOnlyPublicationAndApprovalToolsAreBlocked(TutorToolExecutionPolicy policy, HttpStatusCode expected)
    {
        using var client = factory.CreateClient();
        var tutor = await SeedActiveTutorAsync(await AuthorizeAsRoleAsync(client, AppRoles.Editor));
        var root = $"/api/tutors/{tutor.TutorId}";
        var original = (await client.GetFromJsonAsync<TutorRevisionResponse[]>($"{root}/revisions"))!.First();
        var tool = (await (await client.PostAsJsonAsync($"{root}/tools", new RegisterToolDefinitionRequest("Calculator", null,
            ImplementationKey: "builtin.calculator.v1", ExecutionPolicy: policy))).Content.ReadFromJsonAsync<ToolDefinitionResponse>())!;
        var revision = (await (await client.PostAsync($"{root}/revisions", null)).Content.ReadFromJsonAsync<TutorRevisionResponse>())!;
        var configuration = new UpdateDraftRevisionRequest(original.PromptVersionId, original.InteractionModelId,
            null, null, DeploymentTarget.Local, null, [tool.Id]);
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"{root}/revisions/{revision.Id}", configuration)).StatusCode);
        // Removing and re-adding a saved link exercises real repository relationship persistence.
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"{root}/revisions/{revision.Id}", configuration with { ToolDefinitionIds = [] })).StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<TutorRevisionResponse>($"{root}/revisions/{revision.Id}"))!.ToolDefinitionIds);
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"{root}/revisions/{revision.Id}", configuration)).StatusCode);
        Assert.Equal(expected, (await client.PostAsync($"{root}/revisions/{revision.Id}/publish", null)).StatusCode);
    }

    [Theory]
    [InlineData("unknown.v1", 0, 1)]
    [InlineData("builtin.calculator.v1", 99, 1)]
    [InlineData("builtin.calculator.v1", 1, 1)]
    [InlineData("builtin.calculator.v1", 2, 1)]
    [InlineData("builtin.calculator.v1", 0, 99)]
    public async Task ToolManagementRejectsUnsupportedConfiguration(string key, int kind, int policy)
    {
        using var client = factory.CreateClient();
        var tutor = await SeedActiveTutorAsync(await AuthorizeAsRoleAsync(client, AppRoles.Editor));
        var root = $"/api/tutors/{tutor.TutorId}/tools";
        using var response = await client.PostAsJsonAsync(root, new RegisterToolDefinitionRequest("Calculator", null,
            Kind: (TutorToolKind)kind, ImplementationKey: key, ExecutionPolicy: (TutorToolExecutionPolicy)policy));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var created = await client.PostAsJsonAsync(root, new RegisterToolDefinitionRequest("Calculator", null,
            ImplementationKey: "builtin.calculator.v1", ExecutionPolicy: TutorToolExecutionPolicy.ReadOnlyAutomatic));
        var tool = (await created.Content.ReadFromJsonAsync<ToolDefinitionResponse>())!;
        using var update = await client.PutAsJsonAsync($"{root}/{tool.Id}", new UpdateToolDefinitionRequest("Calculator", null,
            (TutorToolKind)kind, key, (TutorToolExecutionPolicy)policy));
        Assert.Equal(HttpStatusCode.BadRequest, update.StatusCode);
    }

    [Fact]
    public async Task RevisionSelectionValidatesCapabilityAndCapturesImmutableTools()
    {
        using var client = factory.CreateClient();
        var owner = await AuthorizeAsRoleAsync(client, AppRoles.Editor);
        var tutor = await SeedActiveTutorAsync(owner);
        var root = $"/api/tutors/{tutor.TutorId}";
        var existing = (await client.GetFromJsonAsync<TutorRevisionResponse[]>($"{root}/revisions"))!.First();
        var tool = (await (await client.PostAsJsonAsync($"{root}/tools", new RegisterToolDefinitionRequest("Calculator", null,
            ImplementationKey: "builtin.calculator.v1", ExecutionPolicy: TutorToolExecutionPolicy.ReadOnlyAutomatic)))
            .Content.ReadFromJsonAsync<ToolDefinitionResponse>())!;
        var draft = (await (await client.PostAsync($"{root}/revisions", null)).Content.ReadFromJsonAsync<TutorRevisionResponse>())!;
        var revisionRoot = $"{root}/revisions/{draft.Id}";
        var configuration = new UpdateDraftRevisionRequest(existing.PromptVersionId, existing.InteractionModelId, null,
            null, DeploymentTarget.Local, null, [tool.Id]);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync(revisionRoot,
            configuration with { ToolDefinitionIds = [Guid.NewGuid()] })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync(revisionRoot, configuration)).StatusCode);
        Assert.Equal(tool.Id, Assert.Single((await client.GetFromJsonAsync<TutorRevisionResponse>(revisionRoot))!.ToolDefinitionIds));
        Assert.Equal(HttpStatusCode.BadRequest, (await client.DeleteAsync($"{root}/tools/{tool.Id}")).StatusCode);
        var publish = await client.PostAsync($"{revisionRoot}/publish", null);
        Assert.Equal(HttpStatusCode.BadRequest, publish.StatusCode);
        Assert.Contains("function calling", await publish.Content.ReadAsStringAsync());
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var model = await db.Set<InteractionModel>().SingleAsync(model => model.Id == existing.InteractionModelId);
            model.Capabilities |= ModelCapabilities.FunctionCalling;
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"{revisionRoot}/publish", null)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"{root}/tools/{tool.Id}",
            new UpdateToolDefinitionRequest("Changed", null, tool.Kind, tool.ImplementationKey, TutorToolExecutionPolicy.Disabled))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync(revisionRoot, configuration)).StatusCode);
        var activated = await client.PostAsync($"{revisionRoot}/activate", null);
        Assert.Equal(HttpStatusCode.OK, activated.StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var snapshot = await db.Set<TutorConfigurationSnapshot>().SingleAsync(snapshot => snapshot.TutorRevisionId == draft.Id);
            using var json = JsonDocument.Parse(snapshot.ToolsJson!);
            var captured = Assert.Single(json.RootElement.EnumerateArray());
            Assert.Equal(tool.JsonSchema, captured.GetProperty("JsonSchema").GetString());
            Assert.Equal(tool.ImplementationKey, captured.GetProperty("ImplementationKey").GetString());
            Assert.Equal((int)tool.ExecutionPolicy, captured.GetProperty("ExecutionPolicy").GetInt32());
        }
        Assert.Equal(HttpStatusCode.BadRequest, (await client.DeleteAsync($"{root}/tools/{tool.Id}")).StatusCode);
    }
}
