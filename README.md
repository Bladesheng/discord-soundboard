# Discord Soundboard Bot

Discord bot for playing custom sounds in voice channels.

<img width="889" height="268" alt="image" src="https://github.com/user-attachments/assets/427e6e45-a307-4703-a2ba-0326ecf15059" />

You can upload and fully manage the sounds through the bot's commands:

<img width="237" height="285" alt="image" src="https://github.com/user-attachments/assets/519108f8-3859-4f47-8542-3baf382f0a02" />


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
