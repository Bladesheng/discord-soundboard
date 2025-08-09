using NetCord.Gateway;
using NetCord.Hosting.Gateway;

namespace Bot;

public class VoiceDisconnectHandler(GatewayClient client, SoundService soundService)
    : IVoiceStateUpdateGatewayHandler
{
    public async ValueTask HandleAsync(VoiceState voiceState)
    {
        if (voiceState.UserId != client.Id)
            // Someone else than the bot.
            return;

        if (voiceState.ChannelId != null)
            // It's not a disconnect from voice channel.
            return;

        await soundService.DisposeVoiceClientAsync(voiceState.GuildId);
    }
}