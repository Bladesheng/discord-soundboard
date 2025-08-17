using System.Collections.Concurrent;
using System.Diagnostics;
using Bot.Utils;
using NetCord.Gateway;
using NetCord.Gateway.Voice;
using NetCord.Logging;

namespace Bot.Voice;

public class SoundService
{
    private readonly ConcurrentDictionary<ulong, VoiceConnection> _voiceConnections = new();

    private readonly ConcurrentDictionary<ulong, AsyncLock> _guildLocks = new();


    public async Task PlaySoundAsync(
        GatewayClient client,
        Guild guild,
        ulong userId,
        string filePath
    )
    {
        using var l =
            await _guildLocks.GetOrAdd(guild.Id, _ => new AsyncLock()).AcquireAsync();

        // Check if user is in voice channel.
        if (!guild.VoiceStates.TryGetValue(userId, out var voiceState))
            return;

        var channelId = voiceState.ChannelId.GetValueOrDefault();
        if (channelId == 0)
            return;

        await JoinChannel(client, guild.Id, channelId);

        if (!_voiceConnections.TryGetValue(guild.Id, out var voiceConnection))
            throw new InvalidOperationException("Failed to get voice connection for the server.");

        await PlayAudioFileAsync(voiceConnection, filePath);
    }


    private async Task JoinChannel(
        GatewayClient client,
        ulong guildId,
        ulong channelId
    )
    {
        var voiceConnection = _voiceConnections.GetValueOrDefault(guildId);
        if (voiceConnection?.ChannelId == channelId)
            // Bot is already connected to the voice channel.
            return;


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

        // Create a stream that sends voice to Discord.
        var outStream = voiceClient.CreateOutputStream();

        // We create this stream to automatically convert the PCM data returned by FFmpeg to Opus data.
        // The Opus data is then written to 'outStream' that sends the data to Discord.
        OpusEncodeStream opusEncodeStream = new(
            outStream,
            PcmFormat.Short,
            VoiceChannels.Stereo,
            OpusApplication.Audio
        );

        _voiceConnections[guildId] = new VoiceConnection(
            voiceClient,
            channelId,
            opusEncodeStream
        );
    }


    private async Task PlayAudioFileAsync(VoiceConnection voiceConnection, string filePath)
    {
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

        using var ffmpeg = Process.Start(startInfo);
        if (ffmpeg == null)
            throw new InvalidOperationException("Failed to start FFmpeg process");

        await ffmpeg.StandardOutput.BaseStream.CopyToAsync(voiceConnection.OutputStream);

        await ffmpeg.WaitForExitAsync();

        Console.WriteLine("FFmpeg done");

        await voiceConnection.OutputStream.FlushAsync();

        Console.WriteLine("Flushed");
    }


    public async Task DisposeVoiceClientAsync(ulong guildId)
    {
        if (_voiceConnections.TryRemove(guildId, out var voiceConnection))
            await voiceConnection.DisposeAsync();

        _guildLocks.TryRemove(guildId, out _);
    }
}