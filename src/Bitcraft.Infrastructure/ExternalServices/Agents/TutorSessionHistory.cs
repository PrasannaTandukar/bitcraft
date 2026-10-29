using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
namespace Bitcraft.Infrastructure.ExternalServices.Agents;

/// <summary>State lives in AgentSession.StateBag and is serialized into PostgreSQL, never in this factory.
/// Only the model projection is bounded; durable messages and session history are retained.
/// A future compactor can replace Bound without changing conversation storage.</summary>
internal static class TutorSessionHistory
{
    public static ChatHistoryProvider Create() => new InMemoryChatHistoryProvider(new()
    {
        StateKey = "bitcraft.tutor.history.v1",
        // Older conversations were stored as jsonb, which reorders $type metadata.
        // Provider state is deserialized lazily, using these options rather than
        // the options passed to agent.DeserializeSessionAsync.
        JsonSerializerOptions = new JsonSerializerOptions(AIJsonUtilities.DefaultOptions)
        {
            AllowOutOfOrderMetadataProperties = true
        },
        StorageInputRequestMessageFilter = messages => messages.Where(message =>
            message.GetAgentRequestMessageSourceType() == AgentRequestMessageSourceType.External),
        StorageInputResponseMessageFilter = CanonicalResponses,
        ProvideOutputMessageFilter = Bound
    });

    private static IEnumerable<ChatMessage> CanonicalResponses(IEnumerable<ChatMessage> messages) =>
        messages.Select(message => new ChatMessage(message.Role,
            message.Contents.Where(content => content is TextContent or DataContent or FunctionCallContent or FunctionResultContent).ToList())
        {
            MessageId = message.MessageId,
            AuthorName = message.AuthorName,
            CreatedAt = message.CreatedAt
        });

    private static IEnumerable<ChatMessage> Bound(IEnumerable<ChatMessage> messages)
    {
        var result = new List<ChatMessage>();
        var characters = 0;
        var images = 0;
        foreach (var message in messages.Reverse())
        {
            characters += message.Text.Length;
            images += message.Contents.OfType<DataContent>().Count();
            if (result.Count >= 20 || characters > 30_000 || images > 16) break;
            result.Add(message);
        }
        result.Reverse();
        // Do not start a truncated window with an orphan assistant answer.
        while (result.Count > 0 && result[0].Role != ChatRole.User) result.RemoveAt(0);
        return result;
    }
}
