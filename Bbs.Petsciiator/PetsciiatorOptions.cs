namespace Bbs.Petsciiator;

public enum PetsciiResizeMode
{
    Crop,
    Pad,
    Stretch
}

public sealed record PetsciiatorOptions
{
    public static PetsciiatorOptions Default { get; } = new();

    public int TargetWidth { get; init; } = 320;

    public int TargetHeight { get; init; } = 200;

    public PetsciiResizeMode ResizeMode { get; init; } = PetsciiResizeMode.Stretch;

    public bool PreferLightForeground { get; init; }

    public bool BbsCompatibleOutput { get; init; } = true;

    public int BbsColumns { get; init; } = 39;

    public float PreContrastPercent { get; init; } = 0f;

    public int PreColorCount { get; init; } = 0;

    public bool PreDither { get; init; } = false;
}
