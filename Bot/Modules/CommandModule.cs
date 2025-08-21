using Bot.Data;
using Bot.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace Bot.Modules;

public class CommandModule(SoundboardDbContext dbContext, ILogger<CommandModule> logger)
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

        // Discord allows only 80 characters inside a button.
        if (name.Length > 80)
        {
            await ModifyResponseAsync(msg =>
                msg.Content =
                    $"❌ Error: Sound name is too long: `{name.Length}` characters. Maximum length `80` characters."
            );
            return;
        }

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

        if (await dbContext.Sounds.CountAsync() > 100)
        {
            await ModifyResponseAsync(msg =>
                msg.Content = "❌ Error: Maximum number of sounds reached (100)"
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


        string? filePath = null;
        try
        {
            using HttpClient httpClient = new();
            using var response = await httpClient.GetAsync(sound.Url);
            response.EnsureSuccessStatusCode();

            var soundsDirectory = Path.Combine("/var", "lib", "DiscordSoundboard", "sounds");
            Directory.CreateDirectory(soundsDirectory);
            filePath = Path.Combine(soundsDirectory, $"{Guid.NewGuid()}_{sound.FileName}");
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
            if (File.Exists(filePath))
                File.Delete(filePath);

            logger.LogError(ex, "Failed to save sound file {name}", name);

            await ModifyResponseAsync(msg =>
                msg.Content = "❌ Error: Failed to save the sound file. Please try again later."
            );
            return;
        }

        logger.LogInformation("User {Username} uploaded new sound {name}.",
            Context.User.Username,
            name
        );

        await ModifyResponseAsync(msg =>
            msg.Content = "✅ File uploaded successfully. Type `/sound` to see it."
        );
    }


    [SlashCommand("rename", "Renames the sound effect.")]
    public async Task Rename(
        [SlashCommandParameter(
            Description = "Name of existing sound effect to be renamed",
            AutocompleteProviderType = typeof(SoundNameAutocomplete)
        )]
        string oldName,
        [SlashCommandParameter(
            Description = "New name of the sound effect"
        )]
        string newName
    )
    {
        // Discord allows only 80 characters inside a button.
        if (newName.Length > 80)
        {
            await RespondEphemeralAsync(
                $"❌ Error: Sound name is too long: `{newName.Length}` characters. Maximum length `80` characters."
            );
            return;
        }

        var sound = await dbContext.Sounds
            .Where(s => s.Name == oldName)
            .FirstOrDefaultAsync();

        if (sound == null)
        {
            await RespondEphemeralAsync($"❌ Error: Sound with name `{oldName}` not found.");
            return;
        }

        var soundWithNewName = await dbContext.Sounds
            .Where(s => s.Name == newName)
            .FirstOrDefaultAsync();

        if (soundWithNewName != null)
        {
            await RespondEphemeralAsync($"❌ Error: Sound with name `{newName}` already exists.");
            return;
        }


        sound.Name = newName;

        try
        {
            await dbContext.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to update sound name {oldName} to {newName}",
                oldName,
                newName
            );
            await RespondEphemeralAsync(
                "❌ Error: Failed to rename the sound. Sound with the new name may already exist."
            );
            return;
        }

        logger.LogInformation("User {Username} renamed sound {oldName} to {newName}.",
            Context.User.Username,
            oldName,
            newName
        );

        await RespondEphemeralAsync($"✅ Sound `{oldName}` renamed to `{newName}` successfully.");
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
            await RespondEphemeralAsync($"❌ Error: Sound with name `{name}` not found.");
            return;
        }

        try
        {
            if (File.Exists(sound.FilePath))
                File.Delete(sound.FilePath);

            dbContext.Sounds.Remove(sound);
            await dbContext.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete file {name}.", name);
            await RespondEphemeralAsync("❌ Failed to delete sound. Please try again later.");
            return;
        }


        logger.LogInformation("User {Username} deleted sound {name}.", Context.User.Username, name);

        await RespondEphemeralAsync($"✅ Sound `{name}` deleted successfully.");
    }


    [SlashCommand("sound", "Displays all soundboard buttons.")]
    public async Task Sound()
    {
        logger.LogInformation("Displaying all soundboard buttons for user {user}.",
            Context.User.Username
        );

        await RespondAsync(
            InteractionCallback.DeferredMessage(MessageFlags.Ephemeral)
        );

        // Discord allows sending up to 5 rows (components) per message,
        // and each row can have up to 5 buttons.
        var messagesRows = (await dbContext.Sounds.ToListAsync())
            .Chunk(5)
            .Select(soundsChunk => new ActionRowProperties
            {
                Buttons = soundsChunk.Select(sound => new ButtonProperties(
                    $"soundButton:{sound.Id}",
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


    [SlashCommand("download", "Download all the sound effects.")]
    public async Task Download()
    {
        logger.LogInformation("User {user} requested all sound effects.", Context.User.Username);

        await RespondAsync(
            InteractionCallback.DeferredMessage(MessageFlags.Ephemeral)
        );

        var messagesAttachments = (await dbContext.Sounds.ToListAsync())
            .Select(sound => new AttachmentProperties(
                Path.GetFileName(sound.FilePath),
                File.OpenRead(sound.FilePath))
            )
            // Discord allows sending up to 10 files per message.
            .Chunk(10);

        foreach (var messageAttachments in messagesAttachments)
            await Context.Interaction.SendFollowupMessageAsync(new InteractionMessageProperties
            {
                Flags = MessageFlags.Ephemeral,
                Attachments = messageAttachments
            });
    }


    private async Task RespondEphemeralAsync(string content)
    {
        await RespondAsync(InteractionCallback.Message(new InteractionMessageProperties
        {
            Flags = MessageFlags.Ephemeral,
            Content = content
        }));
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