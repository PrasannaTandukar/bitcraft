using Bitcraft.Domain.Common;

namespace Bitcraft.Domain.Tutoring;

public class ModelCatalogEntry : BaseModel
{
    public string Name { get; set; } = string.Empty;
    public ModelProviderType ProviderType { get; set; }
    public string Endpoint { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public string? ApiKeySecretRef { get; set; }
    public string? SettingsJson { get; set; }
    public bool IsEnabled { get; set; } = true;
}