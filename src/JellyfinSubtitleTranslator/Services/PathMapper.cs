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
    private readonly ILogger<PathMapper> _logger;

    public PathMapper(IOptions<TranslatorOptions> options, ILogger<PathMapper> logger)
    {
        _options = options.Value;
        _logger = logger;
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
                var result = dockerPath.Replace('\\', '/');
                _logger.LogInformation("Mapped Windows path '{WindowsPath}' to Docker path '{DockerPath}'", path, result);
                return result;
            }
        }

        return path;
    }
}
