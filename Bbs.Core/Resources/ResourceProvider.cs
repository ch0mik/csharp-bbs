using System.Reflection;

namespace Bbs.Core.Resources;

public interface IResourceProvider
{
    byte[] ReadBinary(string resourcePath);

    bool TryReadBinary(string resourcePath, out byte[] data);

    IEnumerable<string> List(string prefix);
}

public sealed class ResourceProvider : IResourceProvider
{
    private static readonly StringComparer PathComparer = StringComparer.OrdinalIgnoreCase;

    private readonly IReadOnlyList<Assembly> _assemblies;

    public ResourceProvider(params Assembly[] assemblies)
    {
        var source = assemblies is { Length: > 0 }
            ? assemblies.Where(a => a is not null).Distinct().ToList()
            : AppDomain.CurrentDomain.GetAssemblies().Where(a => !a.IsDynamic).Distinct().ToList();

        _assemblies = source;
    }

    public byte[] ReadBinary(string resourcePath)
    {
        if (TryReadBinary(resourcePath, out var data))
        {
            return data;
        }

        throw new FileNotFoundException($"Resource not found: {resourcePath}");
    }

    public bool TryReadBinary(string resourcePath, out byte[] data)
    {
        data = Array.Empty<byte>();
        if (string.IsNullOrWhiteSpace(resourcePath))
        {
            return false;
        }

        var normalized = NormalizePath(resourcePath);

        foreach (var assembly in _assemblies)
        {
            var names = assembly.GetManifestResourceNames();
            var exact = names.FirstOrDefault(n => PathComparer.Equals(n, normalized));
            var suffix = exact ?? names.FirstOrDefault(n => n.EndsWith('.' + normalized, StringComparison.OrdinalIgnoreCase));
            if (suffix is null)
            {
                continue;
            }

            using var stream = assembly.GetManifestResourceStream(suffix);
            if (stream is null)
            {
                continue;
            }

            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            data = ms.ToArray();
            return true;
        }

        if (File.Exists(resourcePath))
        {
            data = File.ReadAllBytes(resourcePath);
            return true;
        }

        return false;
    }

    public IEnumerable<string> List(string prefix)
    {
        var normalized = NormalizePath(prefix ?? string.Empty);

        return _assemblies
            .SelectMany(a => a.GetManifestResourceNames())
            .Where(n => n.Contains(normalized, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string NormalizePath(string path)
    {
        return path.Trim().Replace('\\', '.').Replace('/', '.');
    }
}
