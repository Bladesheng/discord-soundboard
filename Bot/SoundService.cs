using System.Collections.Concurrent;
using System.Diagnostics;
using NetCord.Gateway;
using NetCord.Gateway.Voice;
using NetCord.Logging;

namespace Bot;

public class SoundService
{
    private readonly ConcurrentDictionary<ulong, (ulong channelId, VoiceClient voiceClient)>
        _voiceClients = new();

    private readonly ConcurrentDictionary<ulong, SemaphoreSlim> _guildSemaphores = new();

    private readonly List<(string name, string path)> _sounds =
    [
        ("neslo nic", "sounds/neslo.mp3"),
        ("bombastic", "sounds/bombastic.mp3"),
        ("dejavu", "sounds/dejavu.mp3"),
        ("faking healer", "sounds/healer.mp3"),
        ("neboli", "sounds/neboli.mp3"),
        ("stfu", "sounds/stfu.mp3")
    ];


    public IReadOnlyList<(string name, string path)> AvailableSounds => _sounds.AsReadOnly();

    public async Task PlaySoundAsync(GatewayClient client, Guild guild, ulong userId,
        string trackPath)
    {
        // Check if user is in voice channel.

        if (!guild.VoiceStates.TryGetValue(userId, out var voiceState))
            return;

        var channelId = voiceState.ChannelId.GetValueOrDefault();
        if (channelId == 0)
            return;

        await JoinChannel(client, guild.Id, channelId);

        var voiceClient = GetVoiceClient(guild.Id);
        if (voiceClient == null)
            throw new InvalidOperationException("Failed to get voice client for the server.");

        await PlayAudioFileAsync(voiceClient, trackPath);
    }


    private async Task JoinChannel(
        GatewayClient client,
        ulong guildId,
        ulong channelId
    )
    {
        // Joining channel is not thread safe - use per-guild lock.
        var semaphore = _guildSemaphores.GetOrAdd(guildId, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync();
        try
        {
            var voiceClientInfo = _voiceClients.GetValueOrDefault(guildId);

            // Check if the bot is already connected to the voice channel.
            if (voiceClientInfo.channelId != channelId)
            {
                var voiceClient = await client.JoinVoiceChannelAsync(
                    guildId,
                    channelId,
                    new VoiceClientConfiguration
                    {
                        Logger = new ConsoleLogger()
                    }
                );
                await voiceClient.StartAsync();

                // Enter speaking state, to be able to send voice.
                await voiceClient.EnterSpeakingStateAsync(
                    new SpeakingProperties(SpeakingFlags.Microphone)
                );

                _voiceClients[guildId] = (channelId, voiceClient);
            }
        }
        finally
        {
            semaphore.Release();
        }
    }

    private VoiceClient? GetVoiceClient(ulong guildId)
    {
        if (!_voiceClients.TryGetValue(guildId, out var voiceClientInfo))
            return null;

        return voiceClientInfo.voiceClient;
    }

    private async Task PlayAudioFileAsync(VoiceClient voiceClient, string filePath)
    {
        // Create a stream that sends voice to Discord.
        var outStream = voiceClient.CreateOutputStream();

        // We create this stream to automatically convert the PCM data returned by FFmpeg to Opus data.
        // The Opus data is then written to 'outStream' that sends the data to Discord.
        OpusEncodeStream stream = new(outStream,
            PcmFormat.Short,
            VoiceChannels.Stereo,
            OpusApplication.Audio
        );

        ProcessStartInfo startInfo = new("ffmpeg")
        {
            RedirectStandardOutput = true
        };
        var arguments = startInfo.ArgumentList;

        // Specify the input.
        arguments.Add("-i");
        arguments.Add(filePath);

        // Show warnings.
        arguments.Add("-loglevel");
        arguments.Add("-24");

        // Set the number of audio channels to 2 (stereo).
        arguments.Add("-ac");
        arguments.Add("2");

        // Set the output format to 16-bit signed little-endian.
        arguments.Add("-f");
        arguments.Add("s16le");

        // Set the audio sampling rate to 48 kHz.
        arguments.Add("-ar");
        arguments.Add("48000");

        // Direct the output to stdout.
        arguments.Add("pipe:1");

        Console.WriteLine("Starting ffmpeg");

        var ffmpeg = Process.Start(startInfo);
        if (ffmpeg == null)
            throw new InvalidOperationException("Failed to start FFmpeg process");

        await ffmpeg.StandardOutput.BaseStream.CopyToAsync(stream);

        await ffmpeg.WaitForExitAsync();

        Console.WriteLine("FFmpeg done");

        await stream.FlushAsync();

        await stream.DisposeAsync();
        ffmpeg.Dispose();
        await outStream.DisposeAsync();
    }

    public async Task DisposeVoiceClientAsync(ulong guildId)
    {
        // todo do this in semaphore maybe

        if (_voiceClients.TryRemove(guildId, out var voiceClientInfo))
            voiceClientInfo.voiceClient.Dispose();

        _guildSemaphores.TryRemove(guildId, out _);
    }
}