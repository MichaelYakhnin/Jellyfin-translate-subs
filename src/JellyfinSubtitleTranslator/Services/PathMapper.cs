using JellyfinSubtitleTranslator.Configuration;
using Microsoft.Extensions.Options;

namespace JellyfinSubtitleTranslator.Services;

public interface IPathMapper
{
    string MapToDockerPath(string windowsPath);
}

public class PathMapper : IPathMapper
{
    private readonly TranslatorOptions _options;

    public PathMapper(IOptions<TranslatorOptions> options)
    {
        _options = options.Value;
    }

    public string MapToDockerPath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return path;


        foreach (var mapping in _options.PathMappings)
        {
            if (path.StartsWith(mapping.WindowsPath, StringComparison.OrdinalIgnoreCase))
            {
                var relativePath = path[mapping.WindowsPath.Length..].TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var dockerPath = Path.Combine(mapping.DockerPath, relativePath);
                return dockerPath.Replace(Path.DirectorySeparatorChar, '/');
            }
        }

        return path;
    }
}
