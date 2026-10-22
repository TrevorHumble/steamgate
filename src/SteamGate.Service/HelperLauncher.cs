using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using SteamGate.Core;

namespace SteamGate.Service;

/// <summary>Starts SteamGate.Helper.exe in the session of the user signed in at the console. The service runs as SYSTEM in session 0 and cannot draw on that user's screen itself.</summary>
internal sealed class HelperLauncher
{
    private const uint CreateUnicodeEnvironment = 0x00000400;
    private const uint CreateNoWindow = 0x08000000;

    private readonly GateLog _log;

    public HelperLauncher(GateLog log)
    {
        _log = log;
    }

    /// <summary>Starts the helper for the console user and logs what happened. Never throws for a Windows failure.</summary>
    public void Start()
    {
        if (ConsoleSession.ActiveId() is not { } session)
        {
            _log.Write("helper not started: no console user");
            return;
        }

        if (!File.Exists(SteamGatePaths.HelperPath))
        {
            _log.Write("helper not started: " + SteamGatePaths.HelperPath + " is missing");
            return;
        }

        if (!WTSQueryUserToken(session, out var token))
        {
            // No user is signed in on that session (the sign-in screen).
            _log.Write("helper not started: no console user");
            return;
        }

        using (token)
        {
            LaunchIn(session, token);
        }
    }

    private void LaunchIn(uint session, SafeAccessTokenHandle token)
    {
        var tokenHandle = token.DangerousGetHandle();
        var environment = IntPtr.Zero;
        try
        {
            if (!CreateEnvironmentBlock(out environment, tokenHandle, false))
            {
                _log.Write(FormattableString.Invariant($"helper not started: environment error {Marshal.GetLastWin32Error()}"));
                return;
            }

            var startup = new StartupInfo { cb = Marshal.SizeOf<StartupInfo>(), lpDesktop = "winsta0\\default" };
            if (!CreateProcessAsUser(
                    tokenHandle,
                    SteamGatePaths.HelperPath,
                    null,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    false,
                    CreateUnicodeEnvironment | CreateNoWindow,
                    environment,
                    Path.GetDirectoryName(SteamGatePaths.HelperPath),
                    ref startup,
                    out var info))
            {
                _log.Write(FormattableString.Invariant($"helper not started: launch error {Marshal.GetLastWin32Error()}"));
                return;
            }

            CloseHandle(info.hProcess);
            CloseHandle(info.hThread);
            _log.Write(FormattableString.Invariant($"helper started session={session}"));
        }
        finally
        {
            if (environment != IntPtr.Zero)
            {
                DestroyEnvironmentBlock(environment);
            }
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct StartupInfo
    {
        public int cb;
        public string? lpReserved;
        public string? lpDesktop;
        public string? lpTitle;
        public int dwX;
        public int dwY;
        public int dwXSize;
        public int dwYSize;
        public int dwXCountChars;
        public int dwYCountChars;
        public int dwFillAttribute;
        public int dwFlags;
        public short wShowWindow;
        public short cbReserved2;
        public IntPtr lpReserved2;
        public IntPtr hStdInput;
        public IntPtr hStdOutput;
        public IntPtr hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessInformation
    {
        public IntPtr hProcess;
        public IntPtr hThread;
        public int dwProcessId;
        public int dwThreadId;
    }

    [DllImport("wtsapi32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool WTSQueryUserToken(uint sessionId, out SafeAccessTokenHandle token);

    [DllImport("userenv.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateEnvironmentBlock(out IntPtr environment, IntPtr token, [MarshalAs(UnmanagedType.Bool)] bool inherit);

    [DllImport("userenv.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyEnvironmentBlock(IntPtr environment);

    [DllImport("advapi32.dll", EntryPoint = "CreateProcessAsUserW", CharSet = CharSet.Unicode, SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateProcessAsUser(
        IntPtr token,
        string? applicationName,
        string? commandLine,
        IntPtr processAttributes,
        IntPtr threadAttributes,
        [MarshalAs(UnmanagedType.Bool)] bool inheritHandles,
        uint creationFlags,
        IntPtr environment,
        string? currentDirectory,
        ref StartupInfo startupInfo,
        out ProcessInformation processInformation);

    [DllImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);
}
