using System.Text.Json;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Application.Common.Models;
using Bitcraft.Domain.Modeling;
using Bitcraft.Domain.Tutoring;
using Microsoft.Extensions.Logging;

namespace Bitcraft.Application.Features.Tutoring;

/// <summary>
/// Canonical execution path for interactive tutor chat and automatic benchmark output.
/// Activated snapshot values are the immutable runtime source of truth; live entities are
/// loaded only to validate ownership, lifecycle, enabled state, and provider connectivity.
/// </summary>
public sealed class TutorRuntime(
    ITutorRepository tutors,
    ITutorMembershipReader membership,
    IModelRepository models,
    IRuntimeEngineRepository runtimeEngines,
    ITutorAgentExecutor agentExecutor,
    ILogger<TutorRuntime> logger,
    IChatAttachmentService attachments,
    ChatAttachmentLimits attachmentLimits,
    ITutorConversationRepository? conversations = null) : ITutorRuntime
{
    internal const int DefaultTopK = 5;
    internal const int MaximumTopK = 10;
    internal const int MaximumQuestionCharacters = 20_000;

    public Task<Result<TutorRuntimeResult>> ExecuteAsync(
        TutorRuntimeRequest request,
        CancellationToken cancellationToken = default) =>
        ExecuteCoreAsync(request, null, cancellationToken);

    public async IAsyncEnumerable<TutorRuntimeStreamEvent> ExecuteStreamingAsync(
        TutorRuntimeRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var channel = Channel.CreateBounded<TutorRuntimeStreamEvent>(new BoundedChannelOptions(32)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        });
        long sequence = 0;
        ValueTask PublishAsync(TutorRuntimeStreamEvent value, CancellationToken token) =>
            channel.Writer.WriteAsync(value with { Sequence = Interlocked.Increment(ref sequence) }, token);

        var execution = ExecuteCoreAsync(request, PublishAsync, cancellationToken);
        _ = CompleteStreamAsync(execution, channel.Writer, PublishAsync, cancellationToken);
        await foreach (var value in channel.Reader.ReadAllAsync(cancellationToken)) yield return value;
    }

    private static async Task CompleteStreamAsync(Task<Result<TutorRuntimeResult>> execution,
        ChannelWriter<TutorRuntimeStreamEvent> writer,
        Func<TutorRuntimeStreamEvent, CancellationToken, ValueTask> publish,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await execution;
            if (result.IsSuccess)
                await publish(new TutorRuntimeStreamEvent(0,
                    TutorRuntimeStreamEventKind.MessageCompleted,
                    result.Value.ConversationId, Result: result.Value), cancellationToken);
            else
                await publish(new TutorRuntimeStreamEvent(0, TutorRuntimeStreamEventKind.Error,
                    Error: result.Error), cancellationToken);
            writer.TryComplete();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            writer.TryComplete();
        }
        catch (Exception exception)
        {
            writer.TryComplete(exception);
        }
    }

    private async Task<Result<TutorRuntimeResult>> ExecuteCoreAsync(
        TutorRuntimeRequest request,
        Func<TutorRuntimeStreamEvent, CancellationToken, ValueTask>? observer,
        CancellationToken cancellationToken)
    {
        var inputValidation = ValidateRequest(request);
        if (inputValidation.IsFailure)
        {
            return Result.Failure<TutorRuntimeResult>(inputValidation.Error);
        }

        var tutor = await tutors.GetByIdAsync(request.TutorId, cancellationToken);
        if (tutor is null)
        {
            return Failure("TutorRuntime.TutorNotFound", "Tutor was not found.");
        }

        // An opaque not-found prevents tutor-id probing by authenticated users who are not members.
        var role = await membership.GetRoleAsync(tutor.Id, request.UserId, cancellationToken);
        if (role is null)
        {
            return Failure("TutorRuntime.TutorNotFound", "Tutor was not found.");
        }

        TutorConversation? conversation = null;
        if (request.ConversationId.HasValue)
        {
            if (request.ExecutionMode != TutorRuntimeExecutionMode.Interactive || conversations is null)
                return Failure("TutorConversation.NotFound", "Conversation was not found.");
            conversation = await conversations.GetAsync(request.ConversationId.Value, request.UserId, tutor.Id, cancellationToken);
            if (conversation is null ||
                (request.TutorRevisionId.HasValue && request.TutorRevisionId != conversation.TutorRevisionId) ||
                (request.TutorConfigurationSnapshotId.HasValue && request.TutorConfigurationSnapshotId != conversation.TutorConfigurationSnapshotId))
                return Failure("TutorConversation.NotFound", "Conversation was not found.");
            request = request with { TutorRevisionId = conversation.TutorRevisionId,
                TutorConfigurationSnapshotId = conversation.TutorConfigurationSnapshotId };
        }

        var revisionResult = await ResolveRevisionAsync(tutor, request, cancellationToken);
        if (revisionResult.IsFailure)
        {
            return Result.Failure<TutorRuntimeResult>(revisionResult.Error);
        }
        var revision = revisionResult.Value;

        var snapshotResult = await ResolveSnapshotAsync(tutor.Id, revision.Id, request.TutorConfigurationSnapshotId, cancellationToken);
        if (snapshotResult.IsFailure)
        {
            return Result.Failure<TutorRuntimeResult>(snapshotResult.Error);
        }
        var snapshot = snapshotResult.Value;
        var isNewConversation = request.PersistConversation && conversation is null;
        if (isNewConversation)
            conversation = TutorConversation.Create(request.UserId, tutor.Id, revision.Id, snapshot.Id);
        if (observer is not null)
            await observer(new TutorRuntimeStreamEvent(0, TutorRuntimeStreamEventKind.ConversationStarted,
                conversation?.Id), cancellationToken);

        // Session images are checked against current ownership/expiry before replay.
        foreach (var id in conversation?.Messages.SelectMany(message => message.AttachmentIds).Distinct() ?? [])
        {
            var storedImage = await attachments.ReadAsync(id, request.UserId, cancellationToken);
            if (storedImage.IsFailure) return Result.Failure<TutorRuntimeResult>(storedImage.Error);
            if (!snapshot.ModelCapabilities.HasFlag(ModelCapabilities.ImageInput))
                return Failure("TutorRuntime.ImageInputNotSupported", "This tutor's model does not support image input.");
        }
        var imageIds = (request.AttachmentIds ?? []).Distinct().ToList();
        if (imageIds.Count > 0 && !snapshot.ModelCapabilities.HasFlag(ModelCapabilities.ImageInput))
            return Failure("TutorRuntime.ImageInputNotSupported", "This tutor's current model does not support image input.");
        if (imageIds.Count > attachmentLimits.MaxImagesPerRequest)
            return Failure("ChatAttachment.TooManyImages", $"Conversation history and this message may include at most {attachmentLimits.MaxImagesPerRequest} distinct images. Start a new chat or remove images.");

        var images = new Dictionary<Guid, LlmImageInput>();
        foreach (var id in imageIds)
        {
            var attachment = await attachments.ReadAsync(id, request.UserId, cancellationToken);
            if (attachment.IsFailure) return Result.Failure<TutorRuntimeResult>(attachment.Error);
            images.Add(id, new LlmImageInput(attachment.Value.Metadata.MimeType, attachment.Value.Data));
        }
        var labelIds = (conversation?.Messages.OrderBy(message => message.Sequence).SelectMany(message => message.AttachmentIds) ?? [])
            .Concat(imageIds).Distinct().ToList();
        var labels = labelIds.Select((id, index) => (id, label: $"Image {index + 1}"))
            .ToDictionary(value => value.id, value => value.label);
        var question = string.IsNullOrWhiteSpace(request.UserQuestion)
            ? "Please analyze the attached image(s)."
            : request.UserQuestion.Trim();

        var promptResult = await ResolvePromptAsync(tutor.Id, revision, snapshot, cancellationToken);
        if (promptResult.IsFailure)
        {
            return Result.Failure<TutorRuntimeResult>(promptResult.Error);
        }

        var interactionResult = await ResolveInteractionModelAsync(revision, snapshot, cancellationToken);
        if (interactionResult.IsFailure)
        {
            return Result.Failure<TutorRuntimeResult>(interactionResult.Error);
        }
        var interaction = interactionResult.Value;

        if (!TutorInteractionParametersParser.TryParse(snapshot.ModelSettingsJson, out var parameters, out var parameterError))
        {
            return Result.Failure<TutorRuntimeResult>(parameterError);
        }

        var toolsResult = ResolveTools(snapshot.ToolsJson, tutor.Id, snapshot.ModelCapabilities);
        if (toolsResult.IsFailure) return Result.Failure<TutorRuntimeResult>(toolsResult.Error);

        var agentRequest = new TutorAgentRequest(
                tutor.Id,
                revision.Id,
                snapshot.Id,
                promptResult.Value,
                new TutorAgentModelConfiguration(
                    interaction.Engine.Id,
                    interaction.Engine.ProviderType,
                    interaction.Engine.Endpoint,
                    interaction.Engine.ApiKeySecretRef,
                    interaction.Model.Id,
                    interaction.ProviderModelName,
                    snapshot.ModelCapabilities),
                LabelAttachments(question, request.AttachmentIds, labels),
                Images: SelectImages(request.AttachmentIds, images),
                Knowledge: new TutorKnowledgeConfiguration(
                    request.UseKnowledge, request.UserQuestion, revision.QueryEmbeddingModelId,
                    snapshot.EffectiveDocumentIdsJson, snapshot.EffectiveKnowledgeBindingJson, request.TopK),
                Memory: new TutorMemoryConfiguration(request.UseMemory, request.SaveToMemory,
                    request.UserId, tutor.Id, revision.Id, snapshot.Id, conversation?.Id,
                    request.UserQuestion, revision.QueryEmbeddingModelId, request.TopK),
                Tools: toolsResult.Value,
                Temperature: parameters.Temperature,
                TopP: parameters.TopP,
                MaxOutputTokens: parameters.MaxTokens,
                AgentSessionJson: conversation?.AgentSessionJson,
                PersistSession: request.PersistConversation || conversation is not null,
                ConversationId: conversation?.Id);
        var generated = observer is null
            ? await agentExecutor.ExecuteAsync(agentRequest, cancellationToken)
            : await agentExecutor.ExecuteStreamingAsync(agentRequest, (progress, token) =>
                observer(ToRuntimeStreamEvent(progress, conversation?.Id), token), cancellationToken);

        if (generated.IsFailure)
        {
            logger.LogWarning(
                "Tutor generation failed. TutorId={TutorId}, RevisionId={RevisionId}, Model={Model}, ErrorCode={ErrorCode}",
                tutor.Id,
                revision.Id,
                interaction.ProviderModelName,
                generated.Error.Code);
            return Result.Failure<TutorRuntimeResult>(ToSafeAgentError(generated.Error, interaction.Engine.ProviderType));
        }

        if (string.IsNullOrWhiteSpace(generated.Value.Answer))
        {
            return Failure("TutorRuntime.GenerationFailed", "The tutor could not generate an answer.");
        }

        TutorMessageProvenanceResponse? provenance = null;
        if (request.PersistConversation || conversation is not null)
        {
            // Generation may finish just as the caller disconnects. Do not complete a
            // durable turn with evidence if cancellation was already requested.
            cancellationToken.ThrowIfCancellationRequested();
            if (conversations is null || generated.Value.AgentSessionJson is null)
                return Failure("TutorRuntime.GenerationFailed", "The conversation could not be saved.");
            if (conversation is null)
                return Failure("TutorRuntime.GenerationFailed", "The conversation could not be saved.");
            var captured = TutorMessageProvenanceCapture.Capture(agentRequest, generated.Value);
            conversation.CompleteTurn(generated.Value.AgentSessionJson, request.UserQuestion?.Trim() ?? "", request.AttachmentIds,
                generated.Value.Answer.Trim(), generated.Value.ProviderModelName, generated.Value.ProviderType.ToString(), captured);
            if (isNewConversation) await conversations.AddAsync(conversation, cancellationToken);
            else if (!await conversations.SaveAsync(conversation, cancellationToken))
                return Failure("TutorConversation.Conflict", "This conversation changed during the request. Reload it before sending again.");
            provenance = TutorMessageProvenancePresentation.Map(captured, revision.VersionNumber);
        }
        var retrieval = generated.Value.Knowledge ?? TutorKnowledgeResult.Empty;
        var memory = generated.Value.Memory ?? TutorMemoryResult.Empty;
        return Result.Success(new TutorRuntimeResult(
            tutor.Id,
            tutor.Name,
            revision.Id,
            revision.VersionNumber,
            snapshot.Id,
            generated.Value.Answer.Trim(),
            new TutorRuntimeModelMetadata(
                interaction.Model.Id,
                interaction.Model.Name,
                interaction.ProviderModelName,
                interaction.Engine.Id,
                interaction.Engine.Name,
                snapshot.ModelCapabilities.HasFlag(ModelCapabilities.ImageInput),
                snapshot.ModelCapabilities.HasFlag(ModelCapabilities.FunctionCalling),
                snapshot.ModelCapabilities.HasFlag(ModelCapabilities.StructuredOutput)),
            retrieval.EmbeddingModel,
            retrieval.KnowledgeUsed,
            retrieval.Sources,
            new TutorRuntimeUsageMetadata(generated.Value.Usage.GenerationLatencyMs,
                generated.Value.Usage.InputTokenCount, generated.Value.Usage.OutputTokenCount,
                generated.Value.Usage.TotalTokenCount),
            retrieval.ReferenceMaterial, conversation?.Id, memory.MemoryUsed, memory.MemorySaved,
            memory.Sources, generated.Value.ToolActivities) { Provenance = provenance });
    }

    private static TutorRuntimeStreamEvent ToRuntimeStreamEvent(TutorAgentStreamEvent value,
        Guid? conversationId) => value.Kind switch
    {
        TutorAgentStreamEventKind.MessageDelta => new(0, TutorRuntimeStreamEventKind.MessageDelta,
            conversationId, Delta: value.Delta),
        TutorAgentStreamEventKind.SourceRetrieved => new(0, TutorRuntimeStreamEventKind.SourceRetrieved,
            conversationId, Source: value.Source),
        TutorAgentStreamEventKind.ToolStarted => new(0, TutorRuntimeStreamEventKind.ToolStarted,
            conversationId, ToolActivity: value.ToolActivity),
        TutorAgentStreamEventKind.ToolCompleted => new(0, TutorRuntimeStreamEventKind.ToolCompleted,
            conversationId, ToolActivity: value.ToolActivity),
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    private async Task<Result<TutorRevision>> ResolveRevisionAsync(
        Tutor tutor,
        TutorRuntimeRequest request,
        CancellationToken cancellationToken)
    {
        Guid revisionId;
        if (request.ExecutionMode == TutorRuntimeExecutionMode.Interactive)
        {
            if (tutor.Status != TutorStatus.Active)
            {
                return Result.Failure<TutorRevision>(new Error(
                    "TutorRuntime.InvalidRevision",
                    "This tutor is not active and cannot be used for chat."));
            }
            if (!request.ConversationId.HasValue && request.TutorRevisionId.HasValue && request.TutorRevisionId != tutor.ActiveRevisionId)
            {
                return Result.Failure<TutorRevision>(new Error(
                    "TutorRuntime.InvalidRevision",
                    "Interactive chat can only execute the tutor's active revision."));
            }

            if (!tutor.ActiveRevisionId.HasValue)
            {
                return Result.Failure<TutorRevision>(new Error(
                    "TutorRuntime.NoActiveRevision",
                    "This tutor is not ready for chat yet. Activate a configured revision first."));
            }
            revisionId = request.ConversationId.HasValue ? request.TutorRevisionId!.Value : tutor.ActiveRevisionId.Value;
        }
        else
        {
            revisionId = request.TutorRevisionId ?? tutor.ActiveRevisionId ?? Guid.Empty;
            if (revisionId == Guid.Empty)
            {
                return Result.Failure<TutorRevision>(new Error(
                    "TutorRuntime.NoActiveRevision",
                    "The tutor has no revision available for execution."));
            }
        }

        var revision = await tutors.GetRevisionByIdAsync(revisionId, cancellationToken);
        if (revision is null || revision.TutorId != tutor.Id)
        {
            return Result.Failure<TutorRevision>(new Error(
                "TutorRuntime.InvalidRevision",
                "The configured tutor revision is invalid."));
        }

        var allowedStatus = request.ExecutionMode == TutorRuntimeExecutionMode.Interactive
            ? request.ConversationId.HasValue
                ? revision.Status is RevisionStatus.Active or RevisionStatus.Published or RevisionStatus.Archived
                : revision.Status == RevisionStatus.Active && tutor.ActiveRevisionId == revision.Id
            : revision.Status is RevisionStatus.Active or RevisionStatus.Published;
        if (!allowedStatus)
        {
            return Result.Failure<TutorRevision>(new Error(
                "TutorRuntime.InvalidRevision",
                "The selected tutor revision cannot be executed."));
        }

        return Result.Success(revision);
    }

    private async Task<Result<TutorConfigurationSnapshot>> ResolveSnapshotAsync(
        Guid tutorId,
        Guid revisionId,
        Guid? requestedSnapshotId,
        CancellationToken cancellationToken)
    {
        TutorConfigurationSnapshot? snapshot;
        if (requestedSnapshotId.HasValue)
        {
            snapshot = await tutors.GetSnapshotByIdAsync(requestedSnapshotId.Value, cancellationToken);
        }
        else
        {
            var snapshots = await tutors.ListSnapshotsByTutorAsync(tutorId, cancellationToken);
            snapshot = snapshots
                .Where(value => value.TutorRevisionId == revisionId)
                .OrderByDescending(value => value.CapturedUtc)
                .FirstOrDefault();
        }

        if (snapshot is null || snapshot.TutorId != tutorId || snapshot.TutorRevisionId != revisionId)
        {
            return Result.Failure<TutorConfigurationSnapshot>(new Error(
                "TutorRuntime.SnapshotMissing",
                "The active tutor configuration snapshot is missing. Activate the revision again before using it."));
        }

        return Result.Success(snapshot);
    }

    private async Task<Result<string>> ResolvePromptAsync(
        Guid tutorId,
        TutorRevision revision,
        TutorConfigurationSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        if (!revision.PromptVersionId.HasValue || snapshot.PromptVersionId != revision.PromptVersionId)
        {
            return Result.Failure<string>(new Error(
                "TutorRuntime.PromptNotConfigured",
                "This tutor does not have a valid prompt configured."));
        }

        var prompt = await tutors.GetPromptVersionByIdAsync(revision.PromptVersionId.Value, cancellationToken);
        if (prompt is null || prompt.TutorId != tutorId || string.IsNullOrWhiteSpace(snapshot.PromptContent))
        {
            return Result.Failure<string>(new Error(
                "TutorRuntime.PromptNotConfigured",
                "This tutor does not have a valid prompt configured."));
        }

        // The snapshot content is authoritative even if the live prompt row is later changed.
        return Result.Success(snapshot.PromptContent);
    }

    private async Task<Result<ResolvedInteractionModel>> ResolveInteractionModelAsync(
        TutorRevision revision,
        TutorConfigurationSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        if (!revision.InteractionModelId.HasValue || snapshot.InteractionModelId != revision.InteractionModelId)
        {
            return Result.Failure<ResolvedInteractionModel>(new Error(
                "TutorRuntime.InteractionModelNotConfigured",
                "This tutor does not have an interaction model configured."));
        }

        var model = await models.GetInteractionModelByIdAsync(revision.InteractionModelId.Value, cancellationToken);
        if (model is null || string.IsNullOrWhiteSpace(model.ProviderModelName) || string.IsNullOrWhiteSpace(snapshot.ModelName))
        {
            return Result.Failure<ResolvedInteractionModel>(new Error(
                "TutorRuntime.InteractionModelNotConfigured",
                "This tutor does not have an interaction model configured."));
        }
        if (!model.IsEnabled)
        {
            return Result.Failure<ResolvedInteractionModel>(new Error(
                "TutorRuntime.InteractionModelDisabled",
                "This tutor's interaction model is disabled."));
        }

        var engine = model.RuntimeEngine ??
            await runtimeEngines.GetByIdAsync(model.RuntimeEngineId, cancellationToken);
        if (engine is null || !engine.IsEnabled)
        {
            return Result.Failure<ResolvedInteractionModel>(new Error(
                "TutorRuntime.InteractionModelDisabled",
                "This tutor's interaction runtime is unavailable."));
        }

        var isSupportedProvider = engine.ProviderType is
            RuntimeEngineProviderType.Ollama or
            RuntimeEngineProviderType.OpenAI or
            RuntimeEngineProviderType.OpenAICompatible or
            RuntimeEngineProviderType.vLLM or
            RuntimeEngineProviderType.LlamaCpp or
            RuntimeEngineProviderType.Anthropic;

        if (!isSupportedProvider)
        {
            return Result.Failure<ResolvedInteractionModel>(new Error(
                "TutorRuntime.InteractionModelNotConfigured",
                "The selected interaction runtime provider is not supported."));
        }

        return Result.Success(new ResolvedInteractionModel(model, engine, snapshot.ModelName.Trim()));
    }

    private Result ValidateRequest(TutorRuntimeRequest request)
    {
        if (request.UserId == Guid.Empty)
        {
            return Result.Failure(new Error("TutorRuntime.TutorNotFound", "Tutor was not found."));
        }
        if (string.IsNullOrWhiteSpace(request.UserQuestion) && (request.AttachmentIds?.Count ?? 0) == 0)
        {
            return Result.Failure(new Error("TutorRuntime.QuestionRequired", "Enter a question or attach an image."));
        }
        if ((request.UserQuestion?.Trim().Length ?? 0) > MaximumQuestionCharacters)
        {
            return Result.Failure(new Error("TutorRuntime.QuestionTooLong", $"Question cannot exceed {MaximumQuestionCharacters} characters."));
        }
        if (request.TopK is < 1 or > MaximumTopK)
        {
            return Result.Failure(new Error("TutorRuntime.TopKInvalid", $"TopK must be between 1 and {MaximumTopK}."));
        }
        if (!Enum.IsDefined(request.ExecutionMode))
        {
            return Result.Failure(new Error("TutorRuntime.ExecutionModeInvalid", "Tutor execution mode is invalid."));
        }

        if (request.ExecutionMode == TutorRuntimeExecutionMode.Benchmark && (request.PersistConversation || request.ConversationId.HasValue))
            return Result.Failure(new Error("TutorRuntime.ExecutionModeInvalid", "Benchmark execution does not use interactive conversations."));

        if ((request.AttachmentIds?.Count ?? 0) > attachmentLimits.MaxImagesPerMessage)
            return Result.Failure(new Error("ChatAttachment.TooManyImages", $"Each message may include at most {attachmentLimits.MaxImagesPerMessage} images."));
        if ((request.AttachmentIds ?? []).Any(id => id == Guid.Empty))
            return Result.Failure(new Error("ChatAttachment.NotFound", "This image is unavailable. Please attach it again."));
        return Result.Success();
    }

    private static IReadOnlyList<LlmImageInput>? SelectImages(
        IReadOnlyList<Guid>? ids, IReadOnlyDictionary<Guid, LlmImageInput> images) =>
        ids is { Count: > 0 } ? ids.Distinct().Select(id => images[id]).ToList() : null;

    private static Result<IReadOnlyList<TutorAgentToolConfiguration>> ResolveTools(
        string? toolsJson, Guid tutorId, ModelCapabilities capabilities)
    {
        if (string.IsNullOrWhiteSpace(toolsJson))
            return Result.Success<IReadOnlyList<TutorAgentToolConfiguration>>([]);
        try
        {
            var captured = JsonSerializer.Deserialize<List<CapturedTool>>(toolsJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (captured is null) return ToolFailure("ToolConfigurationInvalid", "The snapshot tool configuration is invalid.");
            var tools = new List<TutorAgentToolConfiguration>();
            foreach (var item in captured)
            {
                var id = item.ToolDefinitionId ?? item.Id;
                var policy = item.ExecutionPolicy ?? TutorToolExecutionPolicy.Disabled;
                if (id is null || id == Guid.Empty || item.TutorId != tutorId ||
                    string.IsNullOrWhiteSpace(item.Name) || string.IsNullOrWhiteSpace(item.JsonSchema) ||
                    string.IsNullOrWhiteSpace(item.ImplementationKey) || !item.Kind.HasValue ||
                    !Enum.IsDefined(item.Kind.Value) ||
                    !Enum.IsDefined(policy))
                    return ToolFailure("ToolConfigurationInvalid", "The snapshot tool configuration is invalid.");
                tools.Add(new TutorAgentToolConfiguration(id.Value, tutorId, item.Name.Trim(),
                    item.Description?.Trim(), item.JsonSchema, item.Kind!.Value,
                    item.ImplementationKey.Trim(), policy));
            }
            if (tools.GroupBy(tool => tool.Name, StringComparer.Ordinal).Any(group => group.Count() > 1))
                return ToolFailure("ToolConfigurationInvalid", "The snapshot contains duplicate tool names.");
            if (tools.Any(tool => tool.ExecutionPolicy != TutorToolExecutionPolicy.Disabled) &&
                !capabilities.HasFlag(ModelCapabilities.FunctionCalling))
                return ToolFailure("FunctionCallingNotSupported",
                    "This tutor's model is not configured for function calling.");
            return Result.Success<IReadOnlyList<TutorAgentToolConfiguration>>(tools);
        }
        catch (JsonException)
        {
            return ToolFailure("ToolConfigurationInvalid", "The snapshot tool configuration is invalid.");
        }
    }

    private static Result<IReadOnlyList<TutorAgentToolConfiguration>> ToolFailure(string code, string description) =>
        Result.Failure<IReadOnlyList<TutorAgentToolConfiguration>>(new Error($"TutorRuntime.{code}", description));

    private static string LabelAttachments(string? content, IReadOnlyList<Guid>? ids, IReadOnlyDictionary<Guid, string> labels)
    {
        var text = content?.Trim() ?? string.Empty;
        return ids is not { Count: > 0 }
            ? text
            : $"attachments: [{string.Join(", ", ids.Distinct().Select(id => labels[id]))}]\n{text}";
    }

    private static Error ToSafeAgentError(Error error, RuntimeEngineProviderType provider) => error.Code switch
    {
        "TutorRuntime.KnowledgeConfigurationInvalid" =>
            new Error("TutorRuntime.KnowledgeConfigurationInvalid", "Tutor knowledge does not match the activated configuration."),
        "TutorRuntime.EmbeddingModelDisabled" =>
            new Error("TutorRuntime.EmbeddingModelDisabled", "Tutor knowledge could not be searched because its embedding model or runtime is unavailable."),
        "TutorRuntime.EmbeddingUnavailable" =>
            new Error("TutorRuntime.EmbeddingUnavailable", "Tutor knowledge could not be searched because its embedding model is unavailable."),
        "TutorRuntime.KnowledgeSearchFailed" =>
            new Error("TutorRuntime.KnowledgeSearchFailed", "Tutor knowledge could not be searched right now."),
        "TutorAgent.SessionRestoreFailed" => new Error("TutorRuntime.SessionRestoreFailed", "The saved conversation could not be restored."),
        "TutorAgent.SessionSerializationFailed" => new Error("TutorRuntime.SessionSerializationFailed", "The updated conversation could not be saved."),
        "TutorRuntime.MemoryConfigurationInvalid" => new Error("TutorRuntime.MemoryConfigurationInvalid", "Tutor memory is not configured for this tutor."),
        "TutorRuntime.MemoryEmbeddingUnavailable" => new Error("TutorRuntime.MemoryEmbeddingUnavailable", "Tutor memory is unavailable right now."),
        "TutorRuntime.MemorySearchFailed" => new Error("TutorRuntime.MemorySearchFailed", "Tutor memory could not be searched right now."),
        "TutorRuntime.MemorySaveFailed" => new Error("TutorRuntime.MemorySaveFailed", "Tutor memory could not be saved right now."),
        "TutorAgent.ToolConfigurationInvalid" or "TutorAgent.ToolSchemaInvalid" or
        "TutorAgent.ToolUnavailable" or "TutorAgent.ToolKindUnavailable" or
        "TutorAgent.ToolTooManyTools" =>
            new Error("TutorRuntime.ToolConfigurationInvalid", "The activated tutor tool configuration is unavailable."),
        "TutorAgent.FunctionCallingNotSupported" =>
            new Error("TutorRuntime.FunctionCallingNotSupported", "The selected interaction model does not support function calling."),
        "TutorAgent.ToolRequestRejected" =>
            new Error("TutorRuntime.ToolRequestRejected", "The provider rejected tool calling. Choose a model that supports function calling and check its runtime configuration."),
        "TutorAgent.ToolApprovalRequired" =>
            new Error("TutorRuntime.ToolApprovalRequired", "This tool call requires human approval and was not executed."),
        "TutorAgent.ImageRejected" =>
            new Error("TutorRuntime.ImageRejected", "The tutor's provider rejected the image request. Try fewer or smaller images, or ask an administrator to check the model's image support."),
        "TutorAgent.ImageInputNotSupported" =>
            new Error("TutorRuntime.ImageInputNotSupported", "This tutor's current model does not support image input."),
        "TutorAgent.Timeout" =>
            new Error("TutorRuntime.Timeout", "The tutor generation timed out."),
        "TutorAgent.RateLimited" =>
            new Error("TutorRuntime.RateLimited", "The tutor provider is temporarily rate limited."),
        "TutorAgent.ContextTooLarge" =>
            new Error("TutorRuntime.ContextTooLarge", "The tutor request exceeds the model's context limit."),
        "TutorAgent.Unavailable" when provider == RuntimeEngineProviderType.Ollama =>
            new Error("Ollama.Unavailable", "The configured Ollama runtime is unavailable."),
        "TutorAgent.Unavailable" =>
            new Error("TutorRuntime.ProviderUnavailable", "The configured tutor provider is unavailable."),
        "TutorAgent.EndpointInvalid" or "TutorAgent.ModelRequired" or "TutorAgent.TimeoutInvalid" or
        "TutorAgent.SecretNotFound" or "TutorAgent.UnsupportedProvider" =>
            new Error("TutorRuntime.ModelConfigurationInvalid", "The tutor model configuration is unavailable."),
        "TutorAgent.ModelNotFound" when provider == RuntimeEngineProviderType.Ollama =>
            new Error("Ollama.ModelNotFound", "The tutor's configured model is not installed."),
        "TutorAgent.ModelNotFound" =>
            new Error("TutorRuntime.ModelConfigurationInvalid", "The tutor's configured model was not found."),
        "TutorAgent.InvalidResponse" when provider == RuntimeEngineProviderType.Ollama =>
            new Error("Ollama.InvalidResponse", "The configured Ollama runtime returned an invalid response."),
        "TutorAgent.GenerationFailed" when provider == RuntimeEngineProviderType.Ollama =>
            new Error("Ollama.RequestFailed", "The configured Ollama runtime could not complete the request."),
        _ => new Error("TutorRuntime.GenerationFailed", "The tutor could not generate an answer."),
    };

    private static Result<TutorRuntimeResult> Failure(string code, string description) =>
        Result.Failure<TutorRuntimeResult>(new Error(code, description));

    private sealed record ResolvedInteractionModel(
        InteractionModel Model,
        RuntimeEngine Engine,
        string ProviderModelName);

    private sealed record CapturedTool(Guid? ToolDefinitionId, Guid? Id, Guid? TutorId,
        string? Name, string? Description, string? JsonSchema, TutorToolKind? Kind,
        string? ImplementationKey, TutorToolExecutionPolicy? ExecutionPolicy);

}
