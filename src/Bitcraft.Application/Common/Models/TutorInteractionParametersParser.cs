using System.Text.Json;

namespace Bitcraft.Application.Common.Models;

public static class TutorInteractionParametersParser
{
    public static bool TryParse(
        string? json,
        out TutorInteractionParameters parameters,
        out Error error)
    {
        parameters = new TutorInteractionParameters(null, null, null);
        error = Error.None;

        if (string.IsNullOrWhiteSpace(json))
        {
            return true;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return Fail("Interaction parameters must be a JSON object.", out error);
            }

            double? temperature = null;
            double? topP = null;
            int? maxTokens = null;

            foreach (var property in document.RootElement.EnumerateObject())
            {
                switch (NormalizeName(property.Name))
                {
                    case "temperature":
                        if (!property.Value.TryGetDouble(out var parsedTemperature) ||
                            double.IsNaN(parsedTemperature) ||
                            double.IsInfinity(parsedTemperature) ||
                            parsedTemperature is < 0d or > 2d)
                        {
                            return Fail("Temperature must be a number between 0.0 and 2.0.", out error);
                        }
                        temperature = parsedTemperature;
                        break;
                    case "topp":
                        if (!property.Value.TryGetDouble(out var parsedTopP) ||
                            double.IsNaN(parsedTopP) ||
                            double.IsInfinity(parsedTopP) ||
                            parsedTopP is < 0d or > 1d)
                        {
                            return Fail("TopP must be a number between 0.0 and 1.0.", out error);
                        }
                        topP = parsedTopP;
                        break;
                    case "maxtokens":
                        if (!property.Value.TryGetInt32(out var parsedMaxTokens) ||
                            parsedMaxTokens is < 1 or > TutorInteractionParameters.MaximumMaxTokens)
                        {
                            return Fail(
                                $"MaxTokens must be between 1 and {TutorInteractionParameters.MaximumMaxTokens}.",
                                out error);
                        }
                        maxTokens = parsedMaxTokens;
                        break;
                }
            }

            parameters = new TutorInteractionParameters(temperature, topP, maxTokens);
            return true;
        }
        catch (JsonException)
        {
            return Fail("Interaction parameters must be valid JSON.", out error);
        }
    }

    private static string NormalizeName(string name) =>
        new(name.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    private static bool Fail(string description, out Error error)
    {
        error = new Error("TutorRuntime.InteractionParametersInvalid", description);
        return false;
    }
}
