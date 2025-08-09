using Bot;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NetCord;
using NetCord.Hosting.Gateway;
using NetCord.Hosting.Services;
using NetCord.Hosting.Services.ApplicationCommands;
using NetCord.Hosting.Services.ComponentInteractions;
using NetCord.Services.ComponentInteractions;

var builder = Host.CreateApplicationBuilder(args);

builder.Services
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