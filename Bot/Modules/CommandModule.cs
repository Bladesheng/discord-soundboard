using Bot.Data;
using Bot.Models;
using Microsoft.EntityFrameworkCore;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace Bot.Modules;

public class CommandModule(SoundboardDbContext dbContext)
    : ApplicationCommandModule<ApplicationCommandContext>
{
    [SlashCommand("new", "Creates a new sound effect.")]
    public async Task Upload(
        [SlashCommandParameter(Description = "Name of the sound effect")]
        string name,
        [SlashCommandParameter(Description = ".mp3 file (max 1 MB)")]
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

        if (await dbContext.Sounds.CountAsync() > 25)
        {
            await ModifyResponseAsync(msg =>
                msg.Content = "❌ Error: Maximum number of sounds reached (25)"
            );
            return;
        }

        var nameExists = await dbContext.Sounds.AnyAsync(s => s.Name == name);
        if (nameExists)
        {
            await ModifyResponseAsync(msg =>
                msg.Content = $"❌ Error: Sound with the name `{name}` already exists."
            );
            return;
        }


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

            dbContext.Sounds.Add(new Sound
            {
                Name = name,
                FilePath = filePath,
                FileSizeBytes = sound.Size
            });
            await dbContext.SaveChangesAsync();
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


    [SlashCommand("delete", "Deletes the sound effect.")]
    public async Task Delete(
        [SlashCommandParameter(
            Description = "Name of the sound effect",
            AutocompleteProviderType = typeof(SoundNameAutocomplete)
        )]
        string name
    )
    {
        var sound = await dbContext.Sounds
            .Where(s => s.Name == name)
            .FirstOrDefaultAsync();

        if (sound == null)
        {
            await RespondEphemeralAsync(new InteractionMessageProperties
            {
                Content = $"❌ Error: Sound with name `{name}` not found."
            });
            return;
        }

        if (File.Exists(sound.FilePath))
            File.Delete(sound.FilePath);

        dbContext.Sounds.Remove(sound);
        await dbContext.SaveChangesAsync();

        await RespondEphemeralAsync(new InteractionMessageProperties
        {
            Content = $"✅ Sound `{name}` deleted successfully."
        });
    }


    [SlashCommand("sound", "Displays soundboard buttons.")]
    public async Task Button()
    {
        await RespondAsync(
            InteractionCallback.DeferredMessage(MessageFlags.Ephemeral)
        );

        // Discord allows sending up to 5 rows (components) per message,
        // and each row can have up to 5 buttons.
        var messagesRows = (await dbContext.Sounds.ToListAsync())
            .Chunk(5)
            .Select(soundChunk => new ActionRowProperties
            {
                Buttons = soundChunk.Select(sound => new ButtonProperties(
                    $"soundButton:{sound.FilePath}",
                    sound.Name,
                    // EmojiProperties.Standard("👋"),
                    ButtonStyle.Primary
                )).ToArray()
            })
            .Chunk(5);

        foreach (var messageRows in messagesRows)
            await Context.Interaction.SendFollowupMessageAsync(new InteractionMessageProperties
            {
                Flags = MessageFlags.Ephemeral,
                Components = messageRows
            });
    }


    private async Task RespondEphemeralAsync(InteractionMessageProperties messageProperties)
    {
        messageProperties.Flags = MessageFlags.Ephemeral;
        await RespondAsync(InteractionCallback.Message(messageProperties));
    }
}

internal class SoundNameAutocomplete(SoundboardDbContext dbContext)
    : IAutocompleteProvider<AutocompleteInteractionContext>
{
    public async ValueTask<IEnumerable<ApplicationCommandOptionChoiceProperties>?> GetChoicesAsync(
        ApplicationCommandInteractionDataOption option,
        AutocompleteInteractionContext context
    )
    {
        var searchQuery = option.Value ?? string.Empty;

        if (string.IsNullOrWhiteSpace(searchQuery))
            return await dbContext.Sounds
                // Discord limits choices to 25.
                .Take(25)
                .Select(s => new ApplicationCommandOptionChoiceProperties(s.Name, s.Name))
                .ToListAsync();

        var fuzzyPattern = "%" + string.Join("%", searchQuery.ToCharArray()) + "%";

        return await dbContext.Sounds
            .Where(s => EF.Functions.Like(s.Name.ToLower(), fuzzyPattern))
            .Take(25)
            .Select(s => new ApplicationCommandOptionChoiceProperties(s.Name, s.Name))
            .ToListAsync();
    }
}