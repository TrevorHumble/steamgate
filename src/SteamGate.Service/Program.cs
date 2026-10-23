using Microsoft.Extensions.Hosting.WindowsServices;
using System.Threading.Channels;
using SteamGate.Core;
using SteamGate.Core.Rules;
using SteamGate.Service;

var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory,
});

builder.Services.AddWindowsService(options => options.ServiceName = "SteamGate");

if (WindowsServiceHelpers.IsWindowsService())
{
    // Registered after AddWindowsService, so this lifetime replaces the stock one.
    builder.Services.AddSingleton<IHostLifetime, GateServiceLifetime>();
}

// One log instance, so two writers never append to and rotate service.log at once.
builder.Services.AddSingleton(new GateLog(SteamGatePaths.DataFolder));
builder.Services.AddSingleton<SnapshotHolder>();

// Requests from the pipe to the worker: one bounded channel (a full channel answers "Busy, try again." at once), and one log limiter for request lines.
builder.Services.AddSingleton(Channel.CreateBounded<SettingsCommand>(new BoundedChannelOptions(32)
{
    FullMode = BoundedChannelFullMode.Wait,
    SingleReader = true,
}));
builder.Services.AddSingleton(provider => new RateLimitedLog(provider.GetRequiredService<GateLog>(), "request log suppressed"));
builder.Services.AddSingleton<PipeServer>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<PipeServer>());
builder.Services.AddHostedService<GateWorker>();

builder.Build().Run();
