namespace Bitcraft.Domain.Modeling;

[Flags]
public enum ModelCapabilities
{
    None = 0,
    ImageInput = 1,
    FunctionCalling = 2,
    StructuredOutput = 4
}
