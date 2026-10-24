# CI only: connects to \\.\pipe\SteamGate the way the helper and the status screen do, as whoever runs this script.
# It opens the pipe with exactly the rights the pipe grants a standard user (never generic read or write), sends
# {"type":"hello","role":"status"}, reads the pipe's owner, and writes "CONNECTED <owner SID>" to -OutFile. It records the owner
# but does not refuse a pipe over it; the install test checks the owner.
# If it cannot connect it writes "FAILED <reason>" to -OutFile and exits 1. If sending -Line fails, or no reply or state comes, it writes
# "FAILED <reason>" to -ReplyFile or -StateFile (when given), leaves "CONNECTED <owner SID>" in -OutFile, and exits 1.
# -Line is one extra line to send right after the hello (the newline is added). -ReplyFile is where to write the first reply
# line the server sends back, or "FAILED no reply" when none comes within 10 seconds; it skips state lines. -StateFile is where to write
# the first state line the server sends, or "FAILED no state"; a state line can be up to 524288 bytes. Without -ReplyFile and -StateFile it
# does not wait for a line. Without -Line, -ReplyFile, and -StateFile it behaves as it always has. Never run this on a machine you use.
param(
    [Parameter(Mandatory)][string]$OutFile,
    [int]$WaitSeconds = 20,
    [int]$HoldSeconds = 3,
    [string]$Line = '',
    [string]$ReplyFile = '',
    [string]$StateFile = ''
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

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool ReadFile(SafeFileHandle handle, byte[] buffer, int count, out int read, IntPtr overlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool PeekNamedPipe(SafeFileHandle handle, IntPtr buffer, int size, IntPtr bytesRead, out int available, IntPtr bytesLeft);

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

    // Sends one more line (and its newline) on an open connection. False when the write fails.
    public static bool SendLine(SafeFileHandle handle, string line)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(line + "\n");
        int written;
        return WriteFile(handle, bytes, bytes.Length, out written, IntPtr.Zero);
    }

    // Reads one line from the server, polling so a silent server cannot block it. Null when none arrives in time, the pipe closes,
    // or the line is longer than maxBytes (not counting the newline).
    public static string ReadLine(SafeFileHandle handle, int timeoutMs, int maxBytes)
    {
        DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        System.Collections.Generic.List<byte> line = new System.Collections.Generic.List<byte>();
        byte[] one = new byte[1];
        while (DateTime.UtcNow < deadline)
        {
            int available;
            if (!PeekNamedPipe(handle, IntPtr.Zero, 0, IntPtr.Zero, out available, IntPtr.Zero))
            {
                return null;
            }
            if (available == 0)
            {
                System.Threading.Thread.Sleep(50);
                continue;
            }
            int read;
            if (!ReadFile(handle, one, 1, out read, IntPtr.Zero) || read == 0)
            {
                return null;
            }
            if (one[0] == (byte)'\n')
            {
                return Encoding.UTF8.GetString(line.ToArray()).TrimEnd('\r');
            }
            if (line.Count >= maxBytes)
            {
                return null;
            }
            line.Add(one[0]);
        }
        return null;
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
        if ($Line -ne '') {
            if (-not [SteamGateProbe]::SendLine($handle, $Line)) {
                if ($ReplyFile -ne '') { [IO.File]::WriteAllText($ReplyFile, 'FAILED could not send the line') }
                if ($StateFile -ne '') { [IO.File]::WriteAllText($StateFile, 'FAILED could not send the line') }
                $handle.Dispose()
                exit 1
            }
        }
        if (($ReplyFile -ne '') -or ($StateFile -ne '')) {
            # The server sends a state line right after the hello when it already holds one, otherwise on the next tick (within 5 seconds),
            # and again when something changes, so lines are read until the wanted ones have arrived or 10 seconds are up.
            # Every line is read with the state line limit.
            $needReply = ($ReplyFile -ne '')
            $needState = ($StateFile -ne '')
            $readUntil = (Get-Date).AddSeconds(10)
            while ($needReply -or $needState) {
                $left = [int][Math]::Max(0, ($readUntil - (Get-Date)).TotalMilliseconds)
                if ($left -eq 0) { break }
                $received = [SteamGateProbe]::ReadLine($handle, $left, 524288)
                if ($null -eq $received) { break }
                if ($needState -and $received.Contains('"type":"state"')) {
                    [IO.File]::WriteAllText($StateFile, $received)
                    $needState = $false
                } elseif ($needReply -and $received.Contains('"type":"reply"')) {
                    [IO.File]::WriteAllText($ReplyFile, $received)
                    $needReply = $false
                }
            }
            if ($needReply) { [IO.File]::WriteAllText($ReplyFile, 'FAILED no reply') }
            if ($needState) { [IO.File]::WriteAllText($StateFile, 'FAILED no state') }
            if ($needReply -or $needState) {
                $handle.Dispose()
                exit 1
            }
        }
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
