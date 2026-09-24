using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Application.Common.Models;
using Bitcraft.Domain.Tutoring;
using MediatR;

namespace Bitcraft.Application.Features.Tutoring;

public static class TutorToolManagementFeatures
{
    public sealed record ListToolsQuery(Guid TutorId) : IRequest<Result<IReadOnlyList<ToolDefinition>>>;
    public sealed record UpdateToolCommand(Guid TutorId, Guid ToolId, string Name, string? Description,
        TutorToolKind Kind, string ImplementationKey, TutorToolExecutionPolicy ExecutionPolicy) : IRequest<Result<ToolDefinition>>;
    public sealed record DeleteToolCommand(Guid TutorId, Guid ToolId) : IRequest<Result>;

    internal static async Task<Error?> AuthorizeAsync(Guid tutorId, ITutorRepository repository,
        ITutorMembershipReader membership, ICurrentUserContext user, CancellationToken ct)
    {
        if (await repository.GetByIdAsync(tutorId, ct) is null)
            return new Error("Tutor.NotFound", "Tutor not found.");
        return await membership.GetRoleAsync(tutorId, user.UserId, ct) is ResourceRole.Owner or ResourceRole.Editor
            ? null : new Error("Tutor.Forbidden", "Editor or Owner membership required.");
    }

    public sealed class ListToolsHandler(ITutorRepository repository, ITutorMembershipReader membership,
        ICurrentUserContext user) : IRequestHandler<ListToolsQuery, Result<IReadOnlyList<ToolDefinition>>>
    {
        public async Task<Result<IReadOnlyList<ToolDefinition>>> Handle(ListToolsQuery request, CancellationToken ct)
        {
            if (await AuthorizeAsync(request.TutorId, repository, membership, user, ct) is { } error)
                return Result.Failure<IReadOnlyList<ToolDefinition>>(error);
            return Result.Success<IReadOnlyList<ToolDefinition>>(await repository.ListToolDefinitionsAsync(request.TutorId, ct));
        }
    }

    public sealed class UpdateToolHandler(ITutorRepository repository, ITutorMembershipReader membership,
        ICurrentUserContext user, ITutorToolCatalog catalog) : IRequestHandler<UpdateToolCommand, Result<ToolDefinition>>
    {
        public async Task<Result<ToolDefinition>> Handle(UpdateToolCommand request, CancellationToken ct)
        {
            if (await AuthorizeAsync(request.TutorId, repository, membership, user, ct) is { } error)
                return Result.Failure<ToolDefinition>(error);
            var tools = await repository.ListToolDefinitionsAsync(request.TutorId, ct);
            var tool = tools.FirstOrDefault(tool => tool.Id == request.ToolId);
            if (tool is null) return Result.Failure<ToolDefinition>(new Error("Tool.NotFound", "Tool not found."));
            var revisions = await repository.ListRevisionsByTutorAsync(request.TutorId, ct);
            if (revisions.Any(revision => revision.Status != RevisionStatus.Draft && revision.RevisionTools.Any(link => link.ToolDefinitionId == tool.Id)))
                return Result.Failure<ToolDefinition>(new Error("Tool.InUse", "This tool is used by an immutable revision. Add a new tool for a new draft."));
            var implementation = catalog.Entries.FirstOrDefault(entry => entry.ImplementationKey == request.ImplementationKey && entry.Kind == request.Kind);
            if (implementation is null || !Enum.IsDefined(request.ExecutionPolicy))
                return Result.Failure<ToolDefinition>(new Error("Tool.ConfigurationInvalid", "Select a registered implementation and valid execution policy."));
            if (request.ExecutionPolicy == TutorToolExecutionPolicy.ReadOnlyAutomatic && !implementation.SupportsAutomaticExecution)
                return Result.Failure<ToolDefinition>(new Error("Tool.ApprovalRequired", "This tool cannot execute automatically."));
            if (request.Description?.Length > 2000)
                return Result.Failure<ToolDefinition>(new Error("Tool.DescriptionInvalid", "Description must be at most 2000 characters."));
            var name = request.Name?.Trim() ?? "";
            if (!System.Text.RegularExpressions.Regex.IsMatch(name, "^[A-Za-z_][A-Za-z0-9_-]{0,63}$"))
                return Result.Failure<ToolDefinition>(new Error("Tool.NameInvalid", "Use 1–64 letters, digits, underscores or hyphens, starting with a letter or underscore."));
            if (tools.Any(other => other.Id != tool.Id && other.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                return Result.Failure<ToolDefinition>(new Error("Tool.NameDuplicate", "A tool with this name already exists for this tutor."));
            tool.Name = name;
            tool.Description = request.Description?.Trim();
            tool.Kind = implementation.Kind;
            tool.ImplementationKey = implementation.ImplementationKey;
            tool.JsonSchema = implementation.JsonSchema;
            tool.ExecutionPolicy = request.ExecutionPolicy;
            tool.UpdateMetadata(user.UserId);
            await repository.UpdateToolDefinitionAsync(tool, ct);
            return Result.Success(tool);
        }
    }

    public sealed class DeleteToolHandler(ITutorRepository repository, ITutorMembershipReader membership,
        ICurrentUserContext user) : IRequestHandler<DeleteToolCommand, Result>
    {
        public async Task<Result> Handle(DeleteToolCommand request, CancellationToken ct)
        {
            if (await AuthorizeAsync(request.TutorId, repository, membership, user, ct) is { } error)
                return Result.Failure(error);
            var tool = (await repository.ListToolDefinitionsAsync(request.TutorId, ct)).FirstOrDefault(tool => tool.Id == request.ToolId);
            if (tool is null) return Result.Failure(new Error("Tool.NotFound", "Tool not found."));
            if ((await repository.ListRevisionsByTutorAsync(request.TutorId, ct)).Any(revision => revision.RevisionTools.Any(link => link.ToolDefinitionId == tool.Id)))
                return Result.Failure(new Error("Tool.InUse", "This tool is referenced by a revision. Remove it from drafts first; tools in immutable revisions cannot be deleted."));
            await repository.RemoveToolDefinitionAsync(tool, ct);
            return Result.Success();
        }
    }
}
