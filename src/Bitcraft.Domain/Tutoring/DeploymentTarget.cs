namespace Bitcraft.Domain.Tutoring;

/// <summary>Target environment a tutor deployment profile applies to.</summary>
public enum DeploymentTarget
{
    /// <summary>Local Docker Compose stack.</summary>
    Local = 0,
    /// <summary>Public cloud deployment target.</summary>
    Server = 1
}
