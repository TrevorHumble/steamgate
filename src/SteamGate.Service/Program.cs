using Microsoft.Extensions.Hosting.WindowsServices;
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

builder.Services.AddHostedService<GateWorker>();

builder.Build().Run();
