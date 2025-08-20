using Microsoft.Extensions.Logging;
using NetCord.Gateway;
using NetCord.Hosting.Gateway;

namespace Bot.Voice;

public class VoiceStateUpdateHandler(
    GatewayClient client,
    SoundService soundService,
    ILogger<VoiceStateUpdateHandler> logger
) : IVoiceStateUpdateGatewayHandler
{
    public async ValueTask HandleAsync(VoiceState voiceState)
    {
        if (voiceState.UserId == client.Id && voiceState.ChannelId != null)
        {
            logger.LogInformation("Bot joined voice channel {ChannelId}", voiceState.ChannelId);
            return;
        }

        if (voiceState.UserId == client.Id && voiceState.ChannelId == null)
        {
            logger.LogInformation("Bot left voice channel");
            await soundService.DisposeVoiceClientAsync(voiceState.GuildId);
            return;
        }

        if (client.Cache.Guilds.TryGetValue(voiceState.GuildId, out var guild))
        {
            if (!guild.VoiceStates.TryGetValue(client.Id, out var voiceStateBot))
                // Bot is not in a voice channel.
                return;

            var voiceStatesInChannel = guild.VoiceStates
                .Where(vs =>
                    vs.Value.ChannelId == voiceStateBot.ChannelId && vs.Value.UserId != client.Id
                )
                .Select(vs => vs.Value.UserId)
                .ToList();

            // Netcord only provides the cached state of voice states before the update is applied,
            // so you have to apply the update manually.
            if (
                voiceState.ChannelId == voiceStateBot.ChannelId &&
                !voiceStatesInChannel.Contains(voiceState.UserId)
            )
            {
                logger.LogInformation("User {UserId} joined bot's channel.", voiceState.UserId);
                voiceStatesInChannel.Add(voiceState.UserId);
            }
            else if (
                voiceState.ChannelId != voiceStateBot.ChannelId &&
                voiceStatesInChannel.Contains(voiceState.UserId)
            )
            {
                logger.LogInformation("User {UserId} left the bot's channel.", voiceState.UserId);
                voiceStatesInChannel.Remove(voiceState.UserId);
            }


            if (voiceStatesInChannel.Count == 0)
            {
                logger.LogInformation("Bot is alone in voice channel. Disconnecting.");
                await soundService.DisconnectAsync(client, guild.Id);
                return;
            }
        }
    }
}