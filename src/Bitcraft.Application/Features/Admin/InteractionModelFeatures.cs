using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Application.Common.Models;
using Bitcraft.Application.Common.Security;
using Bitcraft.Domain.Modeling;
using MediatR;

namespace Bitcraft.Application.Features.Admin;

public static class InteractionModelFeatures
{
    private const int MaxNameLength = 200;
    private const int MaxProviderModelNameLength = 200;

    public sealed record CreateInteractionModelCommand(
        string Name,
        Guid RuntimeEngineId,
        string ProviderModelName,
        string? SettingsJson,
        bool IsEnabled,
        bool SupportsImageInput = false,
        bool SupportsFunctionCalling = false,
        bool SupportsStructuredOutput = false) : IRequest<Result<InteractionModel>>;

    public sealed class CreateInteractionModelHandler(
        IModelRepository models,
        IRuntimeEngineRepository engines,
        ICurrentUserContext currentUser)
        : IRequestHandler<CreateInteractionModelCommand, Result<InteractionModel>>
    {
        public async Task<Result<InteractionModel>> Handle(CreateInteractionModelCommand request, CancellationToken cancellationToken)
        {
            if (!currentUser.IsInRole(AppRoles.Admin))
            {
                return Result.Failure<InteractionModel>(new Error("InteractionModel.Forbidden", "Only administrators can manage interaction models."));
            }

            var validation = Validate(request.Name, request.ProviderModelName);
            if (validation is not null)
            {
                return Result.Failure<InteractionModel>(validation);
            }

            var engine = await engines.GetByIdAsync(request.RuntimeEngineId, cancellationToken);
            if (engine is null)
            {
                return Result.Failure<InteractionModel>(new Error("RuntimeEngine.NotFound", $"Runtime engine '{request.RuntimeEngineId}' not found."));
            }

            if (!engine.IsEnabled)
            {
                return Result.Failure<InteractionModel>(new Error("RuntimeEngine.Disabled", "Selected runtime engine is disabled."));
            }

            var trimmedName = request.Name.Trim();
            if (await models.NameExistsAsync(trimmedName, excludeId: null, cancellationToken))
            {
                return Result.Failure<InteractionModel>(new Error("Model.NameConflict", "A model with that name already exists."));
            }

            var model = new InteractionModel
            {
                Name = trimmedName,
                RuntimeEngineId = request.RuntimeEngineId,
                ProviderModelName = request.ProviderModelName.Trim(),
                SettingsJson = string.IsNullOrWhiteSpace(request.SettingsJson) ? null : request.SettingsJson,
                Capabilities = (request.SupportsImageInput ? ModelCapabilities.ImageInput : ModelCapabilities.None) |
                    (request.SupportsFunctionCalling ? ModelCapabilities.FunctionCalling : ModelCapabilities.None) |
                    (request.SupportsStructuredOutput ? ModelCapabilities.StructuredOutput : ModelCapabilities.None),
                IsEnabled = request.IsEnabled
            };

            var created = await models.AddAsync(model, cancellationToken);
            return Result.Success(created);
        }
    }

    public sealed record ListInteractionModelsQuery : IRequest<Result<List<InteractionModel>>>;

    public sealed class ListInteractionModelsHandler(
        IModelRepository models,
        ICurrentUserContext currentUser)
        : IRequestHandler<ListInteractionModelsQuery, Result<List<InteractionModel>>>
    {
        public async Task<Result<List<InteractionModel>>> Handle(ListInteractionModelsQuery request, CancellationToken cancellationToken)
        {
            if (!currentUser.IsInRole(AppRoles.Admin))
            {
                return Result.Failure<List<InteractionModel>>(new Error("InteractionModel.Forbidden", "Only administrators can view interaction models."));
            }

            var list = await models.ListInteractionModelsAsync(cancellationToken);
            return Result.Success(list);
        }
    }

    public sealed record GetInteractionModelQuery(Guid Id) : IRequest<Result<InteractionModel>>;

    public sealed class GetInteractionModelHandler(
        IModelRepository models,
        ICurrentUserContext currentUser)
        : IRequestHandler<GetInteractionModelQuery, Result<InteractionModel>>
    {
        public async Task<Result<InteractionModel>> Handle(GetInteractionModelQuery request, CancellationToken cancellationToken)
        {
            if (!currentUser.IsInRole(AppRoles.Admin))
            {
                return Result.Failure<InteractionModel>(new Error("InteractionModel.Forbidden", "Only administrators can view interaction models."));
            }

            var model = await models.GetInteractionModelByIdAsync(request.Id, cancellationToken);
            if (model is null)
            {
                return Result.Failure<InteractionModel>(new Error("InteractionModel.NotFound", $"Interaction model '{request.Id}' not found."));
            }

            return Result.Success(model);
        }
    }

    public sealed record UpdateInteractionModelCommand(
        Guid Id,
        string Name,
        Guid RuntimeEngineId,
        string ProviderModelName,
        string? SettingsJson,
        bool IsEnabled,
        bool SupportsImageInput = false,
        bool SupportsFunctionCalling = false,
        bool SupportsStructuredOutput = false) : IRequest<Result<InteractionModel>>;

    public sealed class UpdateInteractionModelHandler(
        IModelRepository models,
        IRuntimeEngineRepository engines,
        ICurrentUserContext currentUser)
        : IRequestHandler<UpdateInteractionModelCommand, Result<InteractionModel>>
    {
        public async Task<Result<InteractionModel>> Handle(UpdateInteractionModelCommand request, CancellationToken cancellationToken)
        {
            if (!currentUser.IsInRole(AppRoles.Admin))
            {
                return Result.Failure<InteractionModel>(new Error("InteractionModel.Forbidden", "Only administrators can manage interaction models."));
            }

            var validation = Validate(request.Name, request.ProviderModelName);
            if (validation is not null)
            {
                return Result.Failure<InteractionModel>(validation);
            }

            var existing = await models.GetInteractionModelByIdAsync(request.Id, cancellationToken);
            if (existing is null)
            {
                return Result.Failure<InteractionModel>(new Error("InteractionModel.NotFound", $"Interaction model '{request.Id}' not found."));
            }

            var engine = await engines.GetByIdAsync(request.RuntimeEngineId, cancellationToken);
            if (engine is null)
            {
                return Result.Failure<InteractionModel>(new Error("RuntimeEngine.NotFound", $"Runtime engine '{request.RuntimeEngineId}' not found."));
            }

            if (!engine.IsEnabled)
            {
                return Result.Failure<InteractionModel>(new Error("RuntimeEngine.Disabled", "Selected runtime engine is disabled."));
            }

            var trimmedName = request.Name.Trim();
            if (await models.NameExistsAsync(trimmedName, excludeId: existing.Id, cancellationToken))
            {
                return Result.Failure<InteractionModel>(new Error("Model.NameConflict", "A model with that name already exists."));
            }

            existing.Name = trimmedName;
            existing.RuntimeEngineId = request.RuntimeEngineId;
            existing.ProviderModelName = request.ProviderModelName.Trim();
            existing.SettingsJson = string.IsNullOrWhiteSpace(request.SettingsJson) ? null : request.SettingsJson;
            existing.IsEnabled = request.IsEnabled;
            existing.Capabilities = request.SupportsImageInput
                ? existing.Capabilities | ModelCapabilities.ImageInput
                : existing.Capabilities & ~ModelCapabilities.ImageInput;
            existing.Capabilities = request.SupportsFunctionCalling
                ? existing.Capabilities | ModelCapabilities.FunctionCalling
                : existing.Capabilities & ~ModelCapabilities.FunctionCalling;
            existing.Capabilities = request.SupportsStructuredOutput
                ? existing.Capabilities | ModelCapabilities.StructuredOutput
                : existing.Capabilities & ~ModelCapabilities.StructuredOutput;

            await models.UpdateAsync(existing, cancellationToken);
            return Result.Success(existing);
        }
    }

    public sealed record DeleteInteractionModelCommand(Guid Id) : IRequest<Result>;

    public sealed class DeleteInteractionModelHandler(
        IModelRepository models,
        ICurrentUserContext currentUser)
        : IRequestHandler<DeleteInteractionModelCommand, Result>
    {
        public async Task<Result> Handle(DeleteInteractionModelCommand request, CancellationToken cancellationToken)
        {
            if (!currentUser.IsInRole(AppRoles.Admin))
            {
                return Result.Failure(new Error("InteractionModel.Forbidden", "Only administrators can manage interaction models."));
            }

            var existing = await models.GetInteractionModelByIdAsync(request.Id, cancellationToken);
            if (existing is null)
            {
                return Result.Failure(new Error("InteractionModel.NotFound", $"Interaction model '{request.Id}' not found."));
            }

            await models.DeleteAsync(existing, cancellationToken);
            return Result.Success();
        }
    }

    public sealed record TestImageInputCommand(Guid ModelId, Guid AttachmentId) : IRequest<Result<ImageInputTestResponse>>;
    public sealed record ImageInputTestResponse(string Answer);

    public sealed class TestImageInputHandler(
        IModelRepository models,
        IRuntimeEngineRepository engines,
        IChatAttachmentService attachments,
        IRuntimeModelDiagnostic diagnostic,
        ICurrentUserContext currentUser) : IRequestHandler<TestImageInputCommand, Result<ImageInputTestResponse>>
    {
        public async Task<Result<ImageInputTestResponse>> Handle(TestImageInputCommand request, CancellationToken cancellationToken)
        {
            if (!currentUser.IsInRole(AppRoles.Admin))
                return Result.Failure<ImageInputTestResponse>(new Error("InteractionModel.Forbidden", "Only administrators can test interaction models."));
            var model = await models.GetInteractionModelByIdAsync(request.ModelId, cancellationToken);
            if (model is null)
                return Result.Failure<ImageInputTestResponse>(new Error("InteractionModel.NotFound", "Interaction model was not found."));
            var engine = model.RuntimeEngine ?? await engines.GetByIdAsync(model.RuntimeEngineId, cancellationToken);
            if (!model.IsEnabled || engine?.IsEnabled != true)
                return Result.Failure<ImageInputTestResponse>(new Error("InteractionModel.Disabled", "Enable the model and runtime engine before testing."));
            if (engine.ProviderType is not (RuntimeEngineProviderType.Ollama or RuntimeEngineProviderType.OpenAI or RuntimeEngineProviderType.OpenAICompatible or RuntimeEngineProviderType.vLLM or RuntimeEngineProviderType.LlamaCpp or RuntimeEngineProviderType.Anthropic))
                return Result.Failure<ImageInputTestResponse>(new Error("InteractionModel.ProviderUnsupported", "This runtime provider is not supported for generation."));
            var image = await attachments.ReadAsync(request.AttachmentId, currentUser.UserId, cancellationToken);
            if (image.IsFailure) return Result.Failure<ImageInputTestResponse>(image.Error);
            // A diagnostic of the saved runtime/model. It never infers or updates capabilities.
            var result = await diagnostic.GenerateAsync(new RuntimeModelGenerationDiagnosticRequest(
                engine.Id, engine.ProviderType, engine.Endpoint, engine.ApiKeySecretRef,
                model.Id, model.ProviderModelName, model.Capabilities,
                "Briefly describe the attached image in one sentence.",
                [new(image.Value.Metadata.MimeType, image.Value.Data)], 128), cancellationToken);
            if (result.IsFailure || string.IsNullOrWhiteSpace(result.Value.Answer))
                return Result.Failure<ImageInputTestResponse>(new Error("InteractionModel.ImageTestFailed", "The model could not process the image. Check its image support and runtime configuration."));
            return Result.Success(new ImageInputTestResponse(result.Value.Answer.Trim()));
        }
    }

    private static Error? Validate(string name, string providerModelName)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return new Error("InteractionModel.NameRequired", "Model name is required.");
        }

        if (name.Trim().Length > MaxNameLength)
        {
            return new Error("InteractionModel.NameTooLong", $"Model name cannot exceed {MaxNameLength} characters.");
        }

        if (string.IsNullOrWhiteSpace(providerModelName))
        {
            return new Error("InteractionModel.ProviderModelRequired", "Provider model name is required.");
        }

        if (providerModelName.Trim().Length > MaxProviderModelNameLength)
        {
            return new Error("InteractionModel.ProviderModelTooLong", $"Provider model name cannot exceed {MaxProviderModelNameLength} characters.");
        }

        return null;
    }
}
