using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bitcraft.Application.Common.Models;
using Microsoft.Extensions.AI;

namespace Bitcraft.Infrastructure.ExternalServices.Agents;

/// <summary>
/// MEAI adapter for the stable Anthropic Messages HTTP API. Keeps roles, system instructions and
/// inline images separate without a prerelease framework dependency. Tools and streaming are deferred.
/// </summary>
internal sealed class AnthropicCompatibilityChatClient(
    HttpClient httpClient,
    TutorAgentModelConfiguration model,
    string? resolvedApiKey) : IChatClient
{
    public async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages,
        ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (options?.Tools is { Count: > 0 })
            throw new NotSupportedException("The Anthropic compatibility transport does not support tools.");

        var turns = new List<object>();
        foreach (var message in messages)
        {
            if (message.Role != ChatRole.User && message.Role != ChatRole.Assistant)
                throw new NotSupportedException("Anthropic conversation messages must have a user or assistant role.");
            var blocks = new List<object>();
            foreach (var content in message.Contents)
            {
                switch (content)
                {
                    case TextContent text when !string.IsNullOrEmpty(text.Text):
                        blocks.Add(new { type = "text", text = text.Text });
                        break;
                    case TextContent:
                        break;
                    case DataContent data when message.Role == ChatRole.User && data.HasTopLevelMediaType("image"):
                        blocks.Add(new { type = "image", source = new
                        {
                            type = "base64", media_type = data.MediaType,
                            data = Convert.ToBase64String(data.Data.Span)
                        } });
                        break;
                    default:
                        throw new NotSupportedException("The Anthropic compatibility transport only supports text and inline user images.");
                }
            }
            turns.Add(new { role = message.Role.Value, content = blocks });
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, MessagesEndpoint(model.Endpoint));
        if (!string.IsNullOrWhiteSpace(resolvedApiKey)) request.Headers.Add("x-api-key", resolvedApiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");
        request.Content = JsonContent.Create(new
        {
            model = model.ProviderModelName,
            system = options?.Instructions,
            messages = turns,
            max_tokens = options?.MaxOutputTokens ?? 4096,
            temperature = options?.Temperature,
            top_p = options?.TopP,
            stream = false
        }, options: new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<MessagesResponse>(cancellationToken: cancellationToken)
            ?? throw new JsonException("The provider returned no response.");
        return new ChatResponse(new ChatMessage(ChatRole.Assistant,
            string.Concat((payload.Content ?? []).Where(block => block.Type == "text").Select(block => block.Text))))
        {
            ModelId = payload.Model ?? model.ProviderModelName,
            Usage = payload.Usage is { } usage ? new UsageDetails
            {
                InputTokenCount = usage.InputTokens,
                OutputTokenCount = usage.OutputTokens,
                TotalTokenCount = usage.InputTokens + usage.OutputTokens
            } : null
        };
    }

    private static Uri MessagesEndpoint(string endpoint)
    {
        var raw = endpoint.TrimEnd('/');
        if (raw.EndsWith("/v1/messages", StringComparison.OrdinalIgnoreCase)) return new Uri(raw);
        return new Uri(raw + (raw.EndsWith("/v1", StringComparison.OrdinalIgnoreCase) ? "/messages" : "/v1/messages"));
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages,
        ChatOptions? options = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("The Anthropic compatibility transport does not support streaming.");

    public object? GetService(Type serviceType, object? serviceKey = null) =>
        serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;

    public void Dispose() { } // The factory's owning wrapper disposes the supplied HttpClient.

    private sealed record MessagesResponse(string? Model, IReadOnlyList<ContentBlock>? Content, UsagePayload? Usage);
    private sealed record ContentBlock(string Type, string? Text);
    private sealed record UsagePayload(
        [property: JsonPropertyName("input_tokens")] long? InputTokens,
        [property: JsonPropertyName("output_tokens")] long? OutputTokens);
}
