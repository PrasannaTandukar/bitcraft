namespace Bitcraft.Api.Contracts;

/// <summary>Standard API error payload returned for validation, authorization, and missing-resource failures.</summary>
public sealed record ApiError(string Code, string Description);
