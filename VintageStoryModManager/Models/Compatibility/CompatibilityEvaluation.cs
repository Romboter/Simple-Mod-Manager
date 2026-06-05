namespace VintageStoryModManager;

internal readonly struct CompatibilityEvaluation
{
    private CompatibilityEvaluation(
        bool isCompatible,
        bool isUnknown,
        string? message)
    {
        IsCompatible = isCompatible;
        IsUnknown = isUnknown;
        Message = message;
    }

    public bool IsCompatible { get; }

    public bool IsUnknown { get; }

    public string? Message { get; }

    public static CompatibilityEvaluation Compatible { get; } =
        new(true, false, null);

    public static CompatibilityEvaluation Incompatible(string message)
    {
        return new CompatibilityEvaluation(false, false, message);
    }

    public static CompatibilityEvaluation Unknown(string message)
    {
        return new CompatibilityEvaluation(false, true, message);
    }
}
