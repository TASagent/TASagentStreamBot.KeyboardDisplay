using Microsoft.AspNetCore.HttpOverrides;

using TASagentTwitchBot.Core.Extensions;
using TASagentTwitchBot.Core.Web;
using TASagentTwitchBot.Plugin.Audio.Midi;

//Initialize DataManagement
string oldPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "TASagentBotKezBoard");
string newPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "TASagentBotKeyboardDisplay");

if (Directory.Exists(oldPath))
{
    Directory.Move(oldPath, newPath);
}


BGC.IO.DataManagement.Initialize("TASagentBotKeyboardDisplay");

//
// Define and register services
//

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.WebHost
    .UseKestrel()
    .UseUrls("http://0.0.0.0:5000");

IMvcBuilder mvcBuilder = builder.Services.GetMvcBuilder();

//Register Core Controllers (with potential exclusions) 
mvcBuilder.RegisterControllersWithoutFeatures("Overlay", "Notifications", "Database", "TTS");

//Add SignalR for Hubs
builder.Services.AddSignalR();

TASagentTwitchBot.Core.Config.BotConfiguration defaultConfig = new TASagentTwitchBot.Core.Config.BotConfiguration();
defaultConfig.MicConfiguration.Enabled = false;

//Core Agnostic Systems
builder.Services
    .AddTASSingleton(TASagentTwitchBot.Core.Config.BotConfiguration.GetConfig(defaultConfig))
    .AddTASSingleton<TASagentTwitchBot.Core.CommunicationHandler>()
    .AddTASSingleton<TASagentTwitchBot.Core.ErrorHandler>()
    .AddTASSingleton<TASagentTwitchBot.Core.ApplicationManagement>()
    .AddTASSingleton<TASagentTwitchBot.Core.MessageAccumulator>();

//Custom Core Systems
builder.Services
    .AddTASSingleton<TASagentStreamBot.KeyboardDisplay.KeyboardView>();

//Custom Configurator
builder.Services
    .AddTASSingleton<TASagentStreamBot.KeyboardDisplay.Configurator>();

//Core Audio System
builder.Services
    .AddTASSingleton<TASagentTwitchBot.Core.Audio.NAudioDeviceManager>()
    .AddTASSingleton<TASagentTwitchBot.Core.Audio.NAudioPlayer>()
    .AddTASSingleton<TASagentTwitchBot.Core.Audio.NAudioMicrophoneHandler>()
    .AddTASSingleton<TASagentTwitchBot.Core.Audio.SoundEffectSystem>();

//Core Audio Effects System
builder.Services
    .AddTASSingleton<TASagentTwitchBot.Core.Audio.Effects.AudioEffectSystem>()
    .AddTASSingleton<TASagentTwitchBot.Core.Audio.Effects.ChorusEffectProvider>()
    .AddTASSingleton<TASagentTwitchBot.Core.Audio.Effects.EchoEffectProvider>()
    .AddTASSingleton<TASagentTwitchBot.Core.Audio.Effects.FrequencyModulationEffectProvider>()
    .AddTASSingleton<TASagentTwitchBot.Core.Audio.Effects.FrequencyShiftEffectProvider>()
    .AddTASSingleton<TASagentTwitchBot.Core.Audio.Effects.NoiseVocoderEffectProvider>()
    .AddTASSingleton<TASagentTwitchBot.Core.Audio.Effects.PitchShiftEffectProvider>()
    .AddTASSingleton<TASagentTwitchBot.Core.Audio.Effects.ReverbEffectProvider>();

//Core Midi System
builder.Services.RegisterMidiServices();

//Custom Midi Components
builder.Services
    .AddTASSingleton(TASagentStreamBot.KeyboardDisplay.MidiBindingConfig.GetConfig())
    .AddTASSingleton<TASagentStreamBot.KeyboardDisplay.ControllerMidiBinding>();

//XInput Services
builder.Services
    .AddTASSingleton<TASagentTwitchBot.Plugin.XInput.ButtonPressDispatcher>();

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

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseRouting();
app.UseAuthorization();
app.UseDefaultFiles();

//Use custom files
app.UseDocumentsOverrideContent();

//Custom Web Assets
app.UseStaticFiles();

//Core Web Assets
app.UseCoreLibraryContent("TASagentTwitchBot.Core");

//Authentication Middleware
app.UseMiddleware<TASagentTwitchBot.Core.Web.Middleware.AuthCheckerMiddleware>();

//Map all Core Non-excluded controllers
app.MapControllers();

//Core Control Page Hub
app.MapHub<TASagentTwitchBot.Core.Web.Hubs.MonitorHub>("/Hubs/Monitor");
app.MapHub<TASagentStreamBot.KeyboardDisplay.Web.Hubs.PianoDisplayHub>("/Hubs/PianoDisplay");

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
    communication.SendErrorMessage($"Configuration unsuccessful.  Aborting.");

    await app.StopAsync();
    await Task.Delay(15_000);
    return;
}

//
// Construct required components and run
//
TASagentTwitchBot.Core.ErrorHandler errorHandler = app.Services.GetRequiredService<TASagentTwitchBot.Core.ErrorHandler>();
TASagentTwitchBot.Core.ApplicationManagement applicationManagement = app.Services.GetRequiredService<TASagentTwitchBot.Core.ApplicationManagement>();

foreach (TASagentTwitchBot.Core.IStartupListener startupListener in app.Services.GetServices<TASagentTwitchBot.Core.IStartupListener>())
{
    startupListener.NotifyStartup();
}


TASagentStreamBot.KeyboardDisplay.ControllerMidiBinding midiBinding =
    app.Services.GetRequiredService<TASagentStreamBot.KeyboardDisplay.ControllerMidiBinding>();

IMidiDeviceManager midiDeviceManager = app.Services.GetRequiredService<IMidiDeviceManager>();
MidiKeyboardHandler midiKeyboardHandler = app.Services.GetRequiredService<MidiKeyboardHandler>();

midiKeyboardHandler.BindToCustomBinding(midiBinding);

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
