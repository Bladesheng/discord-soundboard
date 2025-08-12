using Bot;
using Bot.Data;
using Bot.Voice;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NetCord;
using NetCord.Hosting.Gateway;
using NetCord.Hosting.Services;
using NetCord.Hosting.Services.ApplicationCommands;
using NetCord.Hosting.Services.ComponentInteractions;
using NetCord.Services.ComponentInteractions;

var builder = Host.CreateApplicationBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DbPath") ??
                       throw new InvalidOperationException("`DbPath` connection string not found");

builder.Services
    .AddDbContext<SoundboardDbContext>(options =>
        options.UseSqlite(connectionString)
    )
    .AddDiscordGateway()
    .AddGatewayHandlers(typeof(Program).Assembly)
    .AddComponentInteractions<ButtonInteraction, ButtonInteractionContext>()
    .AddApplicationCommands()
    .AddSingleton<SoundService>();


var host = builder.Build();

host.AddComponentInteraction<ButtonInteractionContext>("ping", () => "Pong!");

host.AddModules(typeof(Program).Assembly);
host.UseGatewayHandlers();

await host.RunAsync();