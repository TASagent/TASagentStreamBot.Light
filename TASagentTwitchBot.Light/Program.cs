using System.Net;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;

using TASagentTwitchBot.Core.Extensions;
using TASagentTwitchBot.Core.Web;
using TASagentTwitchBot.Plugin.ControllerSpy.Web;

//Initialize DataManagement
BGC.IO.DataManagement.Initialize("TASagentBotLight");

//
// Define and register services
//

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.WebHost
    .UseKestrel()
    .UseUrls("http://0.0.0.0:5000");

IMvcBuilder mvcBuilder = builder.Services.GetMvcBuilder();

//Register ControllerSpy for inclusion
mvcBuilder.AddControllerSpyControllerAssembly();

//Register Core Controllers (with potential exclusions) 
mvcBuilder.RegisterControllersWithoutFeatures("TTS", "Midi", "Scripting");

//Add SignalR for Hubs
builder.Services.AddSignalR();

//Core Agnostic Systems
builder.Services
    .AddSingleton<TASagentTwitchBot.Core.Config.BotConfiguration>(TASagentTwitchBot.Core.Config.BotConfiguration.GetConfig())
    .AddSingleton<TASagentTwitchBot.Core.ICommunication, TASagentTwitchBot.Core.CommunicationHandler>()
    .AddSingleton<TASagentTwitchBot.Core.View.IConsoleOutput, TASagentTwitchBot.Core.View.BasicView>()
    .AddSingleton<TASagentTwitchBot.Core.ErrorHandler>()
    .AddSingleton<TASagentTwitchBot.Core.ApplicationManagement>()
    .AddSingleton<TASagentTwitchBot.Core.IMessageAccumulator, TASagentTwitchBot.Core.MessageAccumulator>();

//Custom Configurator
builder.Services
    .AddSingleton<TASagentTwitchBot.Core.IConfigurator, TASagentTwitchBot.Light.AudioConfigurator>();

//Core Audio System
builder.Services
    .AddSingleton<TASagentTwitchBot.Core.Audio.IAudioPlayer, TASagentTwitchBot.Core.Audio.AudioPlayer>()
    .AddSingleton<TASagentTwitchBot.Core.Audio.IMicrophoneHandler, TASagentTwitchBot.Core.Audio.MicrophoneHandler>()
    .AddSingleton<TASagentTwitchBot.Core.Audio.ISoundEffectSystem, TASagentTwitchBot.Core.Audio.SoundEffectSystem>();

//Core Audio Effects System
builder.Services
    .AddSingleton<TASagentTwitchBot.Core.Audio.Effects.IAudioEffectSystem, TASagentTwitchBot.Core.Audio.Effects.AudioEffectSystem>()
    .AddSingleton<TASagentTwitchBot.Core.Audio.Effects.IAudioEffectProvider, TASagentTwitchBot.Core.Audio.Effects.ChorusEffectProvider>()
    .AddSingleton<TASagentTwitchBot.Core.Audio.Effects.IAudioEffectProvider, TASagentTwitchBot.Core.Audio.Effects.EchoEffectProvider>()
    .AddSingleton<TASagentTwitchBot.Core.Audio.Effects.IAudioEffectProvider, TASagentTwitchBot.Core.Audio.Effects.FrequencyModulationEffectProvider>()
    .AddSingleton<TASagentTwitchBot.Core.Audio.Effects.IAudioEffectProvider, TASagentTwitchBot.Core.Audio.Effects.FrequencyShiftEffectProvider>()
    .AddSingleton<TASagentTwitchBot.Core.Audio.Effects.IAudioEffectProvider, TASagentTwitchBot.Core.Audio.Effects.NoiseVocoderEffectProvider>()
    .AddSingleton<TASagentTwitchBot.Core.Audio.Effects.IAudioEffectProvider, TASagentTwitchBot.Core.Audio.Effects.PitchShiftEffectProvider>()
    .AddSingleton<TASagentTwitchBot.Core.Audio.Effects.IAudioEffectProvider, TASagentTwitchBot.Core.Audio.Effects.ReverbEffectProvider>();

//Core Timer System
builder.Services.AddSingleton<TASagentTwitchBot.Core.Timer.ITimerManager, TASagentTwitchBot.Core.Timer.TimerManager>();

//Controller Overlay
builder.Services.RegisterControllerSpyServices();


//Routing
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
});


//
// Finished defining services
// Construct application
//

using WebApplication app = builder.Build();

//Handle forwarding properly
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseRouting();
app.UseAuthorization();
app.UseDefaultFiles();

//Custom Web Assets
app.UseStaticFiles();

//Core Web Assets
app.UseCoreLibraryContent("TASagentTwitchBot.Core");

//Controllerspy Assets
app.UseCoreLibraryContent("TASagentTwitchBot.Plugin.ControllerSpy");

//Authentication Middleware
app.UseMiddleware<TASagentTwitchBot.Core.Web.Middleware.AuthCheckerMiddleware>();

//Map all Core Non-excluded controllers
app.MapControllers();

//Core Control Page Hub
app.MapHub<TASagentTwitchBot.Core.Web.Hubs.MonitorHub>("/Hubs/Monitor");

//Core Timer Hub
app.MapHub<TASagentTwitchBot.Core.Web.Hubs.TimerHub>("/Hubs/Timer");

//ControllerSpy Endpoints
app.RegisterControllerSpyEndpoints();


await app.StartAsync();


//
// Construct and run Configurator
//

TASagentTwitchBot.Core.ICommunication communication = app.Services.GetRequiredService<TASagentTwitchBot.Core.ICommunication>();
TASagentTwitchBot.Core.IConfigurator configurator = app.Services.GetRequiredService<TASagentTwitchBot.Core.IConfigurator>();

app.Services.GetRequiredService<TASagentTwitchBot.Core.View.IConsoleOutput>();

bool configurationSuccessful = await configurator.VerifyConfigured();

if (!configurationSuccessful)
{
    communication.SendErrorMessage($"Configuration unsuccessful. Aborting.");

    await app.StopAsync();
    await Task.Delay(15_000);
    return;
}

//
// Construct required components and run
//
communication.SendDebugMessage("*** Starting Up ***");

TASagentTwitchBot.Core.ErrorHandler errorHandler = app.Services.GetRequiredService<TASagentTwitchBot.Core.ErrorHandler>();
TASagentTwitchBot.Core.ApplicationManagement applicationManagement = app.Services.GetRequiredService<TASagentTwitchBot.Core.ApplicationManagement>();

app.Services.GetRequiredService<TASagentTwitchBot.Core.Audio.IMicrophoneHandler>();
app.Services.GetRequiredService<TASagentTwitchBot.Core.IMessageAccumulator>();


//
// Wait for signal to end application
//

try
{
    await applicationManagement.WaitForEndAsync();
}
catch (Exception ex)
{
    errorHandler.LogSystemException(ex);
}

//
// Stop webhost
//

await app.StopAsync();