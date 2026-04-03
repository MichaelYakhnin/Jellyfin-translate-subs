using System.Threading.Channels;

namespace JellyfinSubtitleTranslator.Services;

public interface IFileQueue
{
    ValueTask EnqueueAsync(string filePath, CancellationToken cancellationToken = default);
    IAsyncEnumerable<string> DequeueAsync(CancellationToken cancellationToken = default);
}

public class FileQueue : IFileQueue
{
    private readonly Channel<string> _channel;

    public FileQueue()
    {
        _channel = Channel.CreateBounded<string>(new BoundedChannelOptions(100)
        {
            FullMode = BoundedChannelFullMode.Wait
        });
    }

    public async ValueTask EnqueueAsync(string filePath, CancellationToken cancellationToken = default)
    {
        await _channel.Writer.WriteAsync(filePath, cancellationToken);
    }

    public async IAsyncEnumerable<string> DequeueAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var item in _channel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return item;
        }
    }
}
