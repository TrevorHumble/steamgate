# CI only: connects to \\.\pipe\SteamGate the way the helper and the later status screen do, as whoever runs this script.
# It opens the pipe with exactly the rights the pipe grants a standard user (never generic read or write), sends
# {"type":"hello","role":"status"}, reads the pipe's owner, and writes "CONNECTED <owner SID>" to -OutFile. It records the owner
# but does not refuse a pipe over it; the install test checks the owner.
# On failure it writes "FAILED <reason>" to -OutFile and exits 1. Never run this on a machine you use.
param(
    [Parameter(Mandatory)][string]$OutFile,
    [int]$WaitSeconds = 20,
    [int]$HoldSeconds = 3
)
Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

# Windows PowerShell compiles this with C# 5, so it uses no newer syntax.
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using Microsoft.Win32.SafeHandles;

public static class SteamGateProbe
{
    // FILE_READ_DATA | FILE_WRITE_DATA | FILE_READ_EA | FILE_READ_ATTRIBUTES | READ_CONTROL | SYNCHRONIZE
    private const uint Rights = 0x0001 | 0x0002 | 0x0008 | 0x0080 | 0x00020000 | 0x00100000;

    // SECURITY_SQOS_PRESENT | SECURITY_IDENTIFICATION: the server may learn who we are but may not act as us.
    private const uint SecurityIdentification = 0x00100000 | 0x00010000;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(string name, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool WriteFile(SafeFileHandle handle, byte[] buffer, int count, out int written, IntPtr overlapped);

    [DllImport("advapi32.dll")]
    private static extern uint GetSecurityInfo(SafeFileHandle handle, int type, uint info, out IntPtr owner, out IntPtr group, out IntPtr dacl, out IntPtr sacl, out IntPtr descriptor);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr memory);

    // Opens the pipe, says hello, and returns the owner SID. The handle stays open until Close, so the server sees a connected client.
    public static SafeFileHandle Open(out string ownerSid, out int error)
    {
        ownerSid = null;
        error = 0;
        SafeFileHandle handle = CreateFile(@"\\.\pipe\SteamGate", Rights, 0, IntPtr.Zero, 3, SecurityIdentification, IntPtr.Zero);
        if (handle.IsInvalid)
        {
            error = Marshal.GetLastWin32Error();
            return null;
        }
        IntPtr owner, group, dacl, sacl, descriptor;
        uint status = GetSecurityInfo(handle, 6, 1, out owner, out group, out dacl, out sacl, out descriptor);
        if (status != 0)
        {
            error = (int)status;
            handle.Dispose();
            return null;
        }
        try { ownerSid = new SecurityIdentifier(owner).Value; }
        finally { LocalFree(descriptor); }
        byte[] hello = Encoding.UTF8.GetBytes("{\"type\":\"hello\",\"role\":\"status\"}\n");
        int written;
        if (!WriteFile(handle, hello, hello.Length, out written, IntPtr.Zero))
        {
            error = Marshal.GetLastWin32Error();
            handle.Dispose();
            return null;
        }
        return handle;
    }
}
'@

$deadline = (Get-Date).AddSeconds($WaitSeconds)
$lastError = 0
while ($true) {
    $sid = $null
    $openError = 0
    $handle = [SteamGateProbe]::Open([ref]$sid, [ref]$openError)
    if ($null -ne $handle) {
        [IO.File]::WriteAllText($OutFile, "CONNECTED $sid")
        Start-Sleep -Seconds $HoldSeconds
        $handle.Dispose()
        exit 0
    }
    $lastError = $openError
    if ((Get-Date) -gt $deadline) { break }
    Start-Sleep -Milliseconds 250
}
[IO.File]::WriteAllText($OutFile, "FAILED win32 error $lastError")
exit 1
