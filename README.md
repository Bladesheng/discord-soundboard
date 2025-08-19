# Discord Soundboard Bot

Discord bot that plays custom sounds in voice channels.
You can upload and fully manage the sounds through the bot's commands.

## Installation

- Get your
  own [Discord token](https://netcord.dev/guides/getting-started/making-a-bot.html?tabs=bare-bones#retrieving-your-discord-bot-token)


- Run migrations:

```bash
dotnet ef database update
```

- Copy `secrets.example.json` to your `secrets.json` secret manager and provide your own Discord
  token or pass the token through env:

```bash
Discord__Token="xxx" dotnet run
 ```

- Run the bot:

```bash
dotnet run
```