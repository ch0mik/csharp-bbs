namespace Bbs.Tenants.Content;

internal static class InlinePetsciiFeatureFlags
{
    public static bool IsCommodoreNewsEnabled()
    {
        var global = ReadBool("BBS_INLINE_PETSCII_IMAGES", defaultValue: true);
        return ReadBool("BBS_COMMODORENEWS_INLINE_PETSCII_IMAGES", defaultValue: global);
    }

    public static bool IsWikipediaEnabled()
    {
        var global = ReadBool("BBS_INLINE_PETSCII_IMAGES", defaultValue: true);
        return ReadBool("BBS_WIKI_INLINE_PETSCII_IMAGES", defaultValue: global);
    }

    public static bool IsWordpressEnabled()
    {
        var global = ReadBool("BBS_INLINE_PETSCII_IMAGES", defaultValue: true);
        return ReadBool("BBS_WORDPRESS_INLINE_PETSCII_IMAGES", defaultValue: global);
    }

    private static bool ReadBool(string name, bool defaultValue)
    {
        var raw = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return defaultValue;
        }

        return raw.Trim().ToLowerInvariant() switch
        {
            "1" or "true" or "yes" or "on" => true,
            "0" or "false" or "no" or "off" => false,
            _ => defaultValue
        };
    }
}
