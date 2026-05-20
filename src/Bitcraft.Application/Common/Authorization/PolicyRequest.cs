namespace Bitcraft.Application.Common.Authorization;

/// <summary>
/// Attribute-based access control (ABAC) request. Carries the subject, the
/// resource, the requested action verb, and ambient environment attributes.
/// Attribute dictionaries are intentionally typed as <see cref="object"/>?
/// values to admit primitive types (string, Guid, bool, int) and small
/// structured payloads without coupling the contract to a serializer.
/// </summary>
/// <param name="SubjectId">Stable identifier of the acting principal (typically <see cref="Guid"/>); null for anonymous.</param>
/// <param name="SubjectAttributes">Subject claims such as roles, tenant scopes, or membership signals.</param>
/// <param name="ResourceType">Logical resource kind (e.g. "Tutor", "Document", "Knowledge").</param>
/// <param name="ResourceId">Identifier of the resource instance, or null for collection-level actions.</param>
/// <param name="ResourceAttributes">Resource attributes such as owner, visibility, sensitivity, parent ids.</param>
/// <param name="Action">Action verb being attempted (e.g. "read", "create", "delete", "publish").</param>
/// <param name="EnvironmentAttributes">Ambient context such as request time, source IP, or runtime flags.</param>
public sealed record PolicyRequest(
    Guid? SubjectId,
    IReadOnlyDictionary<string, object?> SubjectAttributes,
    string ResourceType,
    string? ResourceId,
    IReadOnlyDictionary<string, object?> ResourceAttributes,
    string Action,
    IReadOnlyDictionary<string, object?> EnvironmentAttributes);
