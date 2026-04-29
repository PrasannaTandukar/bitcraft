namespace Bitcraft.Domain.Tutoring;

/// <summary>Target environment a tutor deployment profile applies to.</summary>
public enum DeploymentTarget
{
    /// <summary>Local Docker Compose stack.</summary>
    Local = 0,
    /// <summary>Public cloud (Phase A: configuration only; runtime in later phases).</summary>
    Server = 1
}
