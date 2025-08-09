using NetCord.Gateway.Voice;

namespace Bot;

public class VoiceConnection(
    VoiceClient voiceClient,
    ulong channelId,
    Stream outputStream
) : IAsyncDisposable
{
    public VoiceClient VoiceClient { get; } = voiceClient;
    public ulong ChannelId { get; } = channelId;
    public Stream OutputStream { get; } = outputStream;

    public async ValueTask DisposeAsync()
    {
        await OutputStream.DisposeAsync();
        await VoiceClient.CloseAsync();
    }
}