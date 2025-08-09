using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;


namespace Bot;

public class CommandModule(SoundService soundService)
    : ApplicationCommandModule<ApplicationCommandContext>
{
    [SlashCommand("pong", "Pong!")]
    public static string Pong()
    {
        return "Ping!";
    }


    [SlashCommand("up", "Upload files!")]
    public static string Upload(
        [SlashCommandParameter(Description = "The mp3 file")]
        Attachment file
    )
    {
        // todo
        return "File uploaded";
    }


    [SlashCommand("btn", "Renders all the buttons!")]
    public async Task Button()
    {
        var rows = soundService.AvailableSounds
            // Discord allows only 5 buttons per row.
            .Chunk(5)
            .Select(soundChunk => new ActionRowProperties
            {
                Buttons = soundChunk.Select(sound => new ButtonProperties(
                    $"soundButton:{sound.path}",
                    sound.name,
                    // EmojiProperties.Standard("👋"),
                    ButtonStyle.Primary
                )).ToArray()
            });

        await RespondAsync(
            InteractionCallback.Message(new InteractionMessageProperties
            {
                Flags = MessageFlags.Ephemeral,
                Content = "Buttons!",
                Components = rows
            })
        );
    }
}