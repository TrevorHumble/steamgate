using Microsoft.Extensions.Hosting.WindowsServices;
using SteamGate.Core;
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
builder.Services.AddSingleton<PipeServer>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<PipeServer>());
builder.Services.AddHostedService<GateWorker>();

builder.Build().Run();
