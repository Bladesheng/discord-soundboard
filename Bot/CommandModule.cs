using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;


namespace Bot;

public class CommandModule(SoundService soundService)
    : ApplicationCommandModule<ApplicationCommandContext>
{
    [SlashCommand("new", "Creates a new sound effect.")]
    public async Task Upload(
        [SlashCommandParameter(Description = "Name of the sound effect")]
        string name,
        [SlashCommandParameter(Description = ".mp3 file")]
        Attachment sound
    )
    {
        await RespondAsync(
            InteractionCallback.DeferredMessage(MessageFlags.Ephemeral)
        );

        if (sound.Size > 1_000_000)
        {
            await ModifyResponseAsync(msg =>
                msg.Content =
                    $"❌ Error: File is too large: `{sound.Size / 1_000:F1} kB`. Maximum size is `1000 kB`."
            );
            return;
        }

        var allowedContentTypes = new[]
        {
            "audio/mpeg",
            "audio/wav",
            "audio/ogg",
            "audio/webm",
            "audio/flac",
            "audio/x-wav"
        };
        if (!allowedContentTypes.Contains(sound.ContentType))
        {
            await ModifyResponseAsync(msg =>
                msg.Content =
                    $"❌ Error: Unsupported file type: `{sound.ContentType}`. Supported formats: `MP3`, `WAV`, `OGG`, `WebM`, `FLAC`."
            );
            return;
        }

        // todo check if name already exists
        // todo check max number of sounds

        try
        {
            using HttpClient httpClient = new();
            using var response = await httpClient.GetAsync(sound.Url);
            response.EnsureSuccessStatusCode();

            var soundsDirectory = Path.Combine("/var", "lib", "DiscordSoundboard", "sounds");
            Directory.CreateDirectory(soundsDirectory);
            var filePath = Path.Combine(soundsDirectory, sound.FileName);
            await using FileStream fileStream = new(filePath, FileMode.Create);
            await response.Content.CopyToAsync(fileStream);

            // todo save to db
            Console.WriteLine(name);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to download sound file: {ex}");

            await ModifyResponseAsync(msg =>
                msg.Content = "❌ Error: Failed to download the sound file. Please try again later."
            );
            return;
        }

        await ModifyResponseAsync(msg =>
            msg.Content = "✅ File uploaded successfully. Type `/sound` to see it."
        );
    }


    [SlashCommand("sound", "Displays soundboard buttons.")]
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

        await RespondEphemeralAsync(new InteractionMessageProperties
        {
            Content = "Soundboard:",
            Components = rows
        });
    }


    private async Task RespondEphemeralAsync(InteractionMessageProperties messageProperties)
    {
        messageProperties.Flags = MessageFlags.Ephemeral;
        await RespondAsync(InteractionCallback.Message(messageProperties));
    }
}