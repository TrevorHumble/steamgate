using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.Extensions.Options;

namespace SteamGate.Service;

/// <summary>The Windows service lifetime, set to refuse Stop and accept Shutdown.</summary>
[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Created by the service container.")]
internal sealed class GateServiceLifetime : WindowsServiceLifetime
{
    public GateServiceLifetime(
        IHostEnvironment environment,
        IHostApplicationLifetime applicationLifetime,
        ILoggerFactory loggerFactory,
        IOptions<HostOptions> optionsAccessor,
        IOptions<WindowsServiceLifetimeOptions> windowsServiceOptionsAccessor)
        : base(environment, applicationLifetime, loggerFactory, optionsAccessor, windowsServiceOptionsAccessor)
    {
        CanStop = false;
        CanShutdown = true;
    }
}
