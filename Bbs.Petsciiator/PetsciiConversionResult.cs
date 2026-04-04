namespace Bbs.Petsciiator;

public sealed record PetsciiConversionResult(
    int Columns,
    int Rows,
    int[] ScreenCodes,
    byte[] ColorRam,
    byte BackgroundColor,
    byte[] RawBytes,
    byte[] BbsBytes);
