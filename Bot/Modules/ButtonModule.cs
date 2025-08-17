using Bot.Data;
using Bot.Voice;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ComponentInteractions;

namespace Bot.Modules;

public class ButtonModule(SoundService soundService, SoundboardDbContext dbContext)
    : ComponentInteractionModule<ButtonInteractionContext>
{
    [ComponentInteraction("soundButton")]
    public async Task Button(int soundId)
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

        var sound = await dbContext.Sounds.FindAsync(soundId);
        if (sound == null)
            throw new ArgumentException($"Sound with ID `{soundId}` not found.");

        await soundService.PlaySoundAsync(
            Context.Client,
            Context.Guild,
            Context.User.Id,
            sound.FilePath
        );

        Console.WriteLine("button click done");
    }
}