using System.ClientModel;
using System.Net;
using System.Text.Json;
using Bitcraft.Application.Common.Models;

namespace Bitcraft.Infrastructure.ExternalServices.Agents;

/// <summary>Maps framework and provider failures without exposing upstream bodies or exception messages.</summary>
internal static class TutorAgentErrorMapper
{
    public static Error Map(Exception exception, string stage, bool hasImageInput, bool hasTools = false)
    {
        if (stage == "RestoreSession") return Failure("SessionRestoreFailed", "The saved conversation could not be restored.");
        if (stage == "SerializeSession") return Failure("SessionSerializationFailed", "The updated conversation could not be saved.");
        if (exception is TutorKnowledgeException knowledgeFailure) return knowledgeFailure.Error;
        if (exception is TutorMemoryException memoryFailure) return memoryFailure.Error;
        if (exception is OperationCanceledException) return Failure("Timeout", "The configured provider did not respond within the generation timeout.");
        if (hasTools && (exception is ClientResultException { Status: 400 or 415 or 422 } ||
            exception is HttpRequestException { StatusCode: HttpStatusCode.BadRequest or HttpStatusCode.UnsupportedMediaType or HttpStatusCode.UnprocessableEntity } ||
            exception is NotSupportedException))
            return Failure("ToolRequestRejected", "The provider rejected tool calling. Choose a model that supports function calling and check its runtime configuration.");
        if (exception is ClientResultException sdkFailure)
        {
            if (sdkFailure.Status == 429) return Failure("RateLimited", "The configured provider is temporarily rate limited.");
            if (sdkFailure.Status == 413) return Failure("ContextTooLarge", "The tutor request exceeds the configured provider context limit.");
            if (sdkFailure.Status == 404) return Failure("ModelNotFound", "The configured model was not found on the runtime.");
            return hasImageInput && sdkFailure.Status is 400 or 415 or 422
                ? Failure("ImageRejected", "The configured provider rejected the image request.")
                : Failure("GenerationFailed", "The configured provider could not complete the request.");
        }
        if (exception is HttpRequestException httpFailure)
        {
            if (httpFailure.StatusCode == HttpStatusCode.TooManyRequests)
                return Failure("RateLimited", "The configured provider is temporarily rate limited.");
            if (httpFailure.StatusCode == HttpStatusCode.RequestEntityTooLarge)
                return Failure("ContextTooLarge", "The tutor request exceeds the configured provider context limit.");
            if (hasImageInput && httpFailure.StatusCode is HttpStatusCode.BadRequest
                or HttpStatusCode.UnsupportedMediaType or HttpStatusCode.UnprocessableEntity)
                return Failure("ImageRejected", "The configured provider rejected the image request.");
            if (httpFailure.StatusCode == HttpStatusCode.NotFound)
                return Failure("ModelNotFound", "The configured model was not found on the runtime.");
            return httpFailure.StatusCode.HasValue
                ? Failure("GenerationFailed", "The configured provider could not complete the request.")
                : Failure("Unavailable", "The configured runtime is unavailable.");
        }
        if (exception is JsonException && stage == "ExecuteAgent")
            return Failure("InvalidResponse", "The configured provider returned an invalid response.");
        if (exception is NotSupportedException)
            return Failure("CapabilityNotSupported", "The configured provider does not support the requested content or options.");
        return Failure("GenerationFailed", "The tutor could not complete the request.");
    }

    private static Error Failure(string code, string description) => new($"TutorAgent.{code}", description);
}
