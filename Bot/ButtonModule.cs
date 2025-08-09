using NetCord;
using NetCord.Rest;
using NetCord.Services.ComponentInteractions;

namespace Bot;

public class ButtonModule(SoundService soundService)
    : ComponentInteractionModule<ButtonInteractionContext>
{
    [ComponentInteraction("soundButton")]
    public async Task Button(string filePath)
    {
        // Check if user is in a voice channel
        if (Context.Guild == null || !Context.Guild.VoiceStates.TryGetValue(Context.User.Id, out _))
        {
            await RespondAsync(
                InteractionCallback.Message(new InteractionMessageProperties
                {
                    Content = "You need to be in a voice channel to play sounds!",
                    Flags = MessageFlags.Ephemeral
                }));
            return;
        }

        await RespondAsync(InteractionCallback.DeferredModifyMessage);

        await soundService.PlaySoundAsync(Context.Client, Context.Guild, Context.User.Id, filePath);

        Console.WriteLine("button click done");
    }
}