# CI-only: installs SteamGate on the Windows runner, kills it, and checks Windows brings it back.
# Never run this on a machine you use: it installs, kills, and removes a system service.
param(
    [Parameter(Mandatory)][string]$PublishDir
)
Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'SteamGate.Common.ps1')

$PublishDir = (Resolve-Path -LiteralPath $PublishDir).Path
$ServiceKey = "HKLM:\SYSTEM\CurrentControlSet\Services\$ServiceName"
$SettingsPath = Join-Path $DataDir 'settings.json'
$LogPath = Join-Path $DataDir 'service.log'
$TallyPath = Join-Path $DataDir 'tally.json'

function Fail {
    param([string]$Message)
    Write-Host "TEST FAILED: $Message"
    exit 1
}

function Get-ServicePid {
    $service = Get-CimInstance -ClassName Win32_Service -Filter "Name='$ServiceName'"
    if ($service -and $service.State -eq 'Running') { return [int]$service.ProcessId }
    return 0
}

# Returns the pid of a running service whose pid is not $NotPid, or fails after $Seconds.
function Wait-ServicePid {
    param([int]$NotPid, [int]$Seconds, [string]$What)
    $deadline = (Get-Date).AddSeconds($Seconds)
    while ((Get-Date) -lt $deadline) {
        $current = Get-ServicePid
        if (($current -ne 0) -and ($current -ne $NotPid)) { return $current }
        Start-Sleep -Seconds 1
    }
    Fail "$What did not happen within $Seconds seconds"
}

function Invoke-Script {
    param([string]$Path)
    $global:LASTEXITCODE = 0
    & $Path
    if ($LASTEXITCODE -ne 0) { Fail "$Path exited with code $LASTEXITCODE" }
}

# The test-only uninstall. The uninstall script refuses removal until allowUninstall has waited out the cooldown, so this ends the service
# first (it would save over the edit), then sets allowUninstall in settings.json itself, then runs the real uninstall script.
# It ends only the service process: the helper, when a group started one, keeps running until the uninstall script ends it.
# The settings.json edit happens whenever the file exists. With no service installed there is nothing to end, and the script just cleans up.
function Invoke-TestUninstall {
    if (Get-Service -Name $ServiceName -ErrorAction SilentlyContinue) {
        Clear-Recovery
        $service = Get-CimInstance -ClassName Win32_Service -Filter "Name='$ServiceName'"
        if ($service -and $service.ProcessId -gt 0) {
            $servicePid = [int]$service.ProcessId
            Stop-Process -Id $servicePid -Force -ErrorAction SilentlyContinue
            $alive = @(Get-Process -Id $servicePid -ErrorAction SilentlyContinue)
            if ($alive.Count -gt 0) { $alive | Wait-Process -Timeout 30 }
        }
        $registered = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
        if ($registered) { $registered.WaitForStatus('Stopped', [TimeSpan]::FromSeconds(30)) }
    }
    if (Test-Path -LiteralPath $SettingsPath) { Set-AllowUninstallInFile }
    Invoke-Script (Join-Path $PublishDir 'Uninstall-SteamGate.ps1')
}

# Sets allowUninstall to true in settings.json by editing the text, so dates and everything else in the file stay as they are.
# A file that does not parse is replaced by a minimal one.
function Set-AllowUninstallInFile {
    $text = [IO.File]::ReadAllText($SettingsPath)
    $valid = $true
    try { $null = $text | ConvertFrom-Json } catch { $valid = $false }
    if (-not $valid -or ($text -notmatch '^\s*\{')) {
        $text = '{"allowUninstall": true}'
    } elseif ($text -match '"allowUninstall"\s*:') {
        $text = $text -replace '"allowUninstall"\s*:\s*[^,}\s]+', '"allowUninstall": true'
    } elseif ($text -match '^\s*\{\s*\}\s*$') {
        $text = '{"allowUninstall": true}'
    } else {
        $text = $text -replace '^\s*\{', '{"allowUninstall": true,'
    }
    [IO.File]::WriteAllText($SettingsPath, $text)
}

function Wait-ForFile {
    param([string]$Path)
    $deadline = (Get-Date).AddSeconds(30)
    while (-not (Test-Path -LiteralPath $Path)) {
        if ((Get-Date) -gt $deadline) { Fail "$Path was not written within 30 seconds" }
        Start-Sleep -Seconds 1
    }
}

function Get-LogLines {
    return @(Get-Content -LiteralPath $LogPath)
}

# Helpers for the Steam game checks. Later issues' check groups reuse them.
$SteamKey = 'HKLM:\SOFTWARE\WOW6432Node\Valve\Steam'
$FakeRootBase = if ($env:RUNNER_TEMP) { $env:RUNNER_TEMP } else { $env:TEMP }
$FakeRoot = Join-Path $FakeRootBase 'FakeSteam'
$FakeGameExe = Join-Path $FakeRoot 'steamapps\common\FakeGame\FakeGame.exe'
# The InstallPath found before New-FakeSteamRoot changed it, or 'absent'. $null means nothing was recorded.
$script:OriginalInstallPath = $null
$script:SteamKeyCreated = $false

# A just-ended program can hold its files for a moment, so retry a few times.
function Remove-FolderWithRetry {
    param([string]$Path)
    for ($attempt = 1; $attempt -le 10; $attempt++) {
        if (-not (Test-Path -LiteralPath $Path)) { return }
        try {
            Remove-Item -LiteralPath $Path -Recurse -Force
            return
        } catch {
            if ($attempt -eq 10) { throw }
            Start-Sleep -Milliseconds 500
        }
    }
}

# Returns the pid of the restarted service after ending the running one.
function Restart-ServiceProcess {
    param([string]$What)
    $old = Get-ServicePid
    if ($old -ne 0) { Stop-Process -Id $old -Force }
    return (Wait-ServicePid -NotPid $old -Seconds 60 -What $What)
}

# Returns $true when a line matching $Pattern appears after the first $After lines of service.log within $Seconds.
function Wait-LogLine {
    param([string]$Pattern, [int]$After, [int]$Seconds)
    $deadline = (Get-Date).AddSeconds($Seconds)
    while ((Get-Date) -lt $deadline) {
        $hits = @(Get-LogLines | Select-Object -Skip $After | Where-Object { $_ -match $Pattern })
        if ($hits.Count -gt 0) { return $true }
        Start-Sleep -Seconds 1
    }
    return $false
}

# Removes any installed copy directly (not through the uninstall script), installs fresh, applies $Settings
# to settings.json as the admin test runner, and restarts the service so it loads them.
function Reset-SteamGate {
    param([hashtable]$Settings = @{})
    if (Get-Service -Name $ServiceName -ErrorAction SilentlyContinue) {
        Clear-Recovery
        Stop-SteamGateProcesses -Prefix 'RESET'
        Invoke-Native 'sc delete' { sc.exe delete $ServiceName }
        $deadline = (Get-Date).AddSeconds(30)
        while (Get-Service -Name $ServiceName -ErrorAction SilentlyContinue) {
            if ((Get-Date) -gt $deadline) { Fail 'the service is still listed 30 seconds after sc delete' }
            Start-Sleep -Seconds 1
        }
    } else {
        Stop-SteamGateProcesses -Prefix 'RESET'
    }
    # The service keeps its boot marker here. A missing key is fine.
    if (Test-Path -LiteralPath $RegistryKey) { Remove-Item -LiteralPath $RegistryKey -Recurse -Force }
    Remove-FolderWithRetry -Path $ProgramDir
    Remove-FolderWithRetry -Path $DataDir
    Invoke-Script (Join-Path $PublishDir 'Install-SteamGate.ps1')
    Wait-ServicePid -NotPid 0 -Seconds 30 -What 'the service reaching Running after the reset install' | Out-Null
    Wait-ForFile $SettingsPath
    $json = Get-Content -Raw -LiteralPath $SettingsPath | ConvertFrom-Json
    foreach ($name in $Settings.Keys) {
        $json | Add-Member -NotePropertyName $name -NotePropertyValue $Settings[$name] -Force
    }
    [IO.File]::WriteAllText($SettingsPath, ($json | ConvertTo-Json -Depth 5))
    Restart-ServiceProcess -What 'the restart after the settings were written' | Out-Null
    Write-Host 'RESET OK'
}

# Ends FakeGame.exe, deletes the fake Steam root, and puts the registry back as New-FakeSteamRoot found it.
function Remove-FakeSteamRoot {
    $running = @(Get-Process -Name 'FakeGame' -ErrorAction SilentlyContinue)
    if ($running.Count -gt 0) {
        $running | Stop-Process -Force -ErrorAction SilentlyContinue
        $running | Wait-Process -Timeout 30 -ErrorAction SilentlyContinue
    }
    Remove-FolderWithRetry -Path $FakeRoot
    if ($null -ne $script:OriginalInstallPath) {
        if ($script:OriginalInstallPath -eq 'absent') {
            Remove-ItemProperty -LiteralPath $SteamKey -Name 'InstallPath' -ErrorAction SilentlyContinue
            if ($script:SteamKeyCreated) { Remove-Item -LiteralPath $SteamKey -Force -ErrorAction SilentlyContinue }
        } else {
            Set-ItemProperty -LiteralPath $SteamKey -Name 'InstallPath' -Value $script:OriginalInstallPath
        }
        $script:OriginalInstallPath = $null
        $script:SteamKeyCreated = $false
    }
}

# Builds a fake Steam root holding FakeGame.exe, points the registry at it, and restarts the service so its first tick reads it.
# Returns the pid of the restarted service.
function New-FakeSteamRoot {
    if (Test-Path -LiteralPath $FakeRoot) { Remove-FakeSteamRoot }
    $script:OriginalInstallPath = 'absent'
    if (Test-Path -LiteralPath $SteamKey) {
        $values = Get-ItemProperty -LiteralPath $SteamKey
        if ($values.PSObject.Properties['InstallPath']) { $script:OriginalInstallPath = [string]$values.InstallPath }
    } else {
        New-Item -Path $SteamKey -Force | Out-Null
        $script:SteamKeyCreated = $true
    }
    New-Item -ItemType Directory -Path (Split-Path -Parent $FakeGameExe) -Force | Out-Null
    $escapedRoot = $FakeRoot.Replace('\', '\\')
    $vdf = @"
"libraryfolders"
{
	"0"
	{
		"path"		"$escapedRoot"
	}
}
"@
    [IO.File]::WriteAllText((Join-Path $FakeRoot 'steamapps\libraryfolders.vdf'), $vdf)
    Copy-Item -LiteralPath (Join-Path $env:SystemRoot 'System32\ping.exe') -Destination $FakeGameExe
    Set-ItemProperty -LiteralPath $SteamKey -Name 'InstallPath' -Value $FakeRoot
    return (Restart-ServiceProcess -What 'the restart after the fake Steam root was set')
}

# Waits until the service with this pid has logged its start and then its first "libraries <count>" line,
# and returns the number of log lines at that moment, so a caller can time what follows.
function Wait-SteamReady {
    param([int]$ServicePid, [int]$Seconds = 60)
    $deadline = (Get-Date).AddSeconds($Seconds)
    while ((Get-Date) -lt $deadline) {
        $lines = @(Get-LogLines)
        $startedAt = -1
        for ($index = 0; $index -lt $lines.Count; $index++) {
            if ($lines[$index] -match "started pid=$ServicePid\s*$") { $startedAt = $index }
        }
        if ($startedAt -ge 0) {
            $libraryLines = @($lines | Select-Object -Skip ($startedAt + 1) | Where-Object { $_ -match 'libraries \d+\s*$' })
            if ($libraryLines.Count -gt 0) { return $lines.Count }
        }
        Start-Sleep -Seconds 1
    }
    Fail "service.log has no ""started pid=$ServicePid"" followed by ""libraries <count>"" within $Seconds seconds"
}

# Returns the night seconds in tally.json, or -1 when the file cannot be read or parsed yet (the service may be replacing it).
function Get-NightSeconds {
    try {
        return [double](Get-Content -Raw -LiteralPath $TallyPath | ConvertFrom-Json).nightSeconds
    } catch {
        return -1
    }
}

# Returns the seconds on the first "tally loaded" line written after the "started pid=<ServicePid>" line, or $null when there is none.
function Get-TallyLoadedSeconds {
    param([int]$ServicePid)
    $lines = @(Get-LogLines)
    $startedAt = -1
    for ($index = 0; $index -lt $lines.Count; $index++) {
        if ($lines[$index] -match "started pid=$ServicePid\s*$") { $startedAt = $index }
    }
    if ($startedAt -lt 0) { return $null }
    foreach ($line in ($lines | Select-Object -Skip ($startedAt + 1))) {
        if ($line -match 'tally loaded night=\d{4}-\d{2}-\d{2} seconds=(\S+)\s*$') {
            return [double]::Parse($Matches[1], [Globalization.CultureInfo]::InvariantCulture)
        }
    }
    return $null
}

function Start-FakeGame {
    return (Start-Process -FilePath $FakeGameExe -ArgumentList '-n', '300', '127.0.0.1' -WindowStyle Hidden -PassThru)
}

# 1. Fresh install.
Invoke-Script (Join-Path $PublishDir 'Install-SteamGate.ps1')
$first = Wait-ServicePid -NotPid 0 -Seconds 30 -What 'the service reaching Running'
$imagePath = (Get-ItemProperty -Path $ServiceKey).ImagePath
if (-not $imagePath.StartsWith('"')) { Fail "ImagePath is not quoted: $imagePath" }
Write-Host 'SERVICE RUNNING'

# 2. Start type.
$qc = (sc.exe qc $ServiceName | Out-String)
if ($qc -notmatch 'AUTO_START') { Fail "start type is not automatic: $qc" }
Write-Host 'START AUTO'

# 3. Restart after the process is ended.
$old = Get-ServicePid
Stop-Process -Id $old -Force
$new = Wait-ServicePid -NotPid $old -Seconds 60 -What 'the restart after the process was ended'
Write-Host "RESTARTED $old -> $new"

# 4. Stop is refused.
$pidBefore = Get-ServicePid
$stop = (sc.exe stop $ServiceName | Out-String)
if ($stop -notmatch '1052') { Fail "sc stop did not report error 1052: $stop" }
Start-Sleep -Seconds 2
if ((Get-Service -Name $ServiceName).Status -ne 'Running') { Fail 'the service is not Running after sc stop' }
if ((Get-ServicePid) -ne $pidBefore) { Fail 'the service pid changed after sc stop' }
Write-Host 'STOP REFUSED'

# 5. Permissions on the data folder and the files the service wrote.
Wait-ForFile $SettingsPath
Wait-ForFile $LogPath
$folderProblem = Test-DataFolderAcl -Path $DataDir
if ($folderProblem) { Fail "the data folder permissions are wrong: $folderProblem" }
foreach ($path in @($SettingsPath, $LogPath)) {
    foreach ($sid in @(Get-ForeignAccessSid -Acl (Get-Acl -LiteralPath $path))) { Fail "$path has an access entry for $sid" }
}
Write-Host 'ACL OK'

# 6. A crash inside the worker (exit code 1) is restarted too.
$mark = (Get-LogLines).Count
New-ItemProperty -Path $ServiceKey -Name 'Environment' -PropertyType MultiString -Value @('STEAMGATE_FAULT_TEST=1') -Force | Out-Null
Stop-Process -Id (Get-ServicePid) -Force
$faultPid = 0
$newPid = 0
$unhandledAt = $null
$deadline = (Get-Date).AddSeconds(120)
while ($newPid -eq 0) {
    if ((Get-Date) -gt $deadline) { Fail 'no restart was logged after the fault' }
    Start-Sleep -Seconds 1
    $lines = Get-LogLines
    $fresh = @($lines | Select-Object -Skip $mark)
    foreach ($line in $fresh) {
        if (($faultPid -eq 0) -and ($line -match 'started pid=(\d+)')) { $faultPid = [int]$Matches[1]; continue }
        if (($faultPid -ne 0) -and ($null -eq $unhandledAt) -and ($line -match "unhandled pid=$faultPid\b")) {
            $unhandledAt = [DateTimeOffset]::Parse(($line -split ' ')[0])
            continue
        }
        if (($null -ne $unhandledAt) -and ($line -match 'started pid=(\d+)') -and ([int]$Matches[1] -ne $faultPid)) {
            $newPid = [int]$Matches[1]
            # The 60 seconds are measured between the two log timestamps, not from when this poll saw the lines.
            $gap = ([DateTimeOffset]::Parse(($line -split ' ')[0]) - $unhandledAt).TotalSeconds
            if ($gap -gt 60) { Fail "the service restarted $gap seconds after unhandled pid=$faultPid, not within 60" }
            break
        }
    }
}
Write-Host "RESTARTED-AFTER-FAULT $faultPid -> $newPid"

# Back to a normal service: drop the fault switch, end whatever runs, and require a stable process.
Remove-ItemProperty -Path $ServiceKey -Name 'Environment'
$running = Wait-ServicePid -NotPid 0 -Seconds 60 -What 'the service running after the fault switch was removed'
Stop-Process -Id $running -Force -ErrorAction SilentlyContinue
$stable = Wait-ServicePid -NotPid $running -Seconds 60 -What 'the restart after the fault switch was removed'
for ($second = 1; $second -le 10; $second++) {
    Start-Sleep -Seconds 1
    if ((Get-ServicePid) -ne $stable) { Fail "pid $stable did not stay Running for 10 seconds" }
}

# 7. Upgrade keeps the settings.
$settings = Get-Content -Raw $SettingsPath | ConvertFrom-Json
$settings.weeknightMinutes = 75
[IO.File]::WriteAllText($SettingsPath, ($settings | ConvertTo-Json -Depth 5))
$killed = Get-ServicePid
Stop-Process -Id $killed -Force
$beforeUpgrade = Wait-ServicePid -NotPid $killed -Seconds 60 -What 'the restart before the upgrade'
Invoke-Script (Join-Path $PublishDir 'Install-SteamGate.ps1')
$afterUpgrade = Wait-ServicePid -NotPid $beforeUpgrade -Seconds 60 -What 'the service running after the upgrade'
$kept = (Get-Content -Raw $SettingsPath | ConvertFrom-Json).weeknightMinutes
if ($kept -ne 75) { Fail "weeknightMinutes is $kept after the upgrade, expected 75" }
Write-Host 'UPGRADED'

# 8. Uninstall.
Invoke-TestUninstall
$gone = $false
for ($second = 0; $second -lt 30; $second++) {
    if (-not (Get-Service -Name $ServiceName -ErrorAction SilentlyContinue)) { $gone = $true; break }
    Start-Sleep -Seconds 1
}
if (-not $gone) { Fail 'the service is still listed 30 seconds after uninstall' }
if (Test-Path -LiteralPath $DataDir) { Fail "$DataDir still exists after uninstall" }
if (Test-Path -LiteralPath $ProgramDir) { Fail "$ProgramDir still exists after uninstall" }
Write-Host 'UNINSTALLED'

# 9. Steam game detection: a counted game is seen, and a not-counted one is not.
try {
    Reset-SteamGate @{}
    $servicePid = New-FakeSteamRoot
    $mark = Wait-SteamReady -ServicePid $servicePid
    $fakeGame = Start-FakeGame
    if (-not (Wait-LogLine -Pattern 'game started FakeGame\s*$' -After $mark -Seconds 20)) { Fail 'service.log has no "game started FakeGame" within 20 seconds' }
    Write-Host 'DETECTED FakeGame'
    Stop-Process -Id $fakeGame.Id -Force
    if (-not (Wait-LogLine -Pattern 'game stopped FakeGame\s*$' -After $mark -Seconds 20)) { Fail 'service.log has no "game stopped FakeGame" within 20 seconds' }
    Write-Host 'STOPPED FakeGame'

    Reset-SteamGate @{ notCounted = @('FakeGame') }
    $servicePid = New-FakeSteamRoot
    $mark = Wait-SteamReady -ServicePid $servicePid
    $fakeGame = Start-FakeGame
    if (Wait-LogLine -Pattern 'game started FakeGame' -After $mark -Seconds 20) { Fail 'FakeGame was counted although it is on the not-counted list' }
    Write-Host 'NOT COUNTED FakeGame'
} finally {
    Remove-FakeSteamRoot
    # The script ends uninstalled, as the first group left it.
    Invoke-TestUninstall
}

# 10. Time accounting: a running game is counted, and the count survives the service being ended.
try {
    # The night would reset in the middle of the test, so wait until just after 04:00.
    $now = Get-Date
    $boundary = $now.Date.AddHours(4)
    if ($boundary -le $now) { $boundary = $boundary.AddDays(1) }
    if (($boundary - $now).TotalMinutes -lt 15) {
        Start-Sleep -Seconds ([int][Math]::Ceiling(($boundary.AddMinutes(1) - $now).TotalSeconds))
    }

    Reset-SteamGate @{}
    $servicePid = New-FakeSteamRoot
    Wait-SteamReady -ServicePid $servicePid | Out-Null
    Start-Process -FilePath $FakeGameExe -ArgumentList '-n', '600', '127.0.0.1' -WindowStyle Hidden | Out-Null

    $counted = -1
    $deadline = (Get-Date).AddSeconds(180)
    while ($counted -lt 60) {
        if ((Get-Date) -gt $deadline) { Fail 'tally.json did not reach 60 night seconds within 180 seconds' }
        Start-Sleep -Seconds 1
        $counted = Get-NightSeconds
    }
    Write-Host "TALLY $counted"

    $restartedPid = Restart-ServiceProcess -What 'the restart after the tally was counted'
    Start-Sleep -Seconds 75
    # A read can collide with the service's save, so retry for up to 10 seconds.
    $keptSeconds = -1
    $readDeadline = (Get-Date).AddSeconds(10)
    while ($true) {
        $keptSeconds = Get-NightSeconds
        if ($keptSeconds -ge ($counted + 30) -or (Get-Date) -gt $readDeadline) { break }
        Start-Sleep -Milliseconds 500
    }
    if ($keptSeconds -lt ($counted + 30)) { Fail "tally.json holds $keptSeconds night seconds after the restart, expected at least $($counted + 30)" }
    $loadedSeconds = Get-TallyLoadedSeconds -ServicePid $restartedPid
    if ($null -eq $loadedSeconds) { Fail "service.log has no ""tally loaded"" line after ""started pid=$restartedPid""" }
    if ($loadedSeconds -lt $counted) { Fail "the restarted service loaded $loadedSeconds night seconds, expected at least $counted" }
    Write-Host "TALLY KEPT $keptSeconds"
} finally {
    Remove-FakeSteamRoot
    # The script ends uninstalled, as the earlier groups left it.
    Invoke-TestUninstall
}

# Helpers for the pipe checks.
$ProbeScript = Join-Path $PSScriptRoot 'PipeProbe.ps1'

# Opens \\.\pipe\SteamGate as the admin test runner, which the pipe allows full control.
function Connect-SteamGatePipe {
    $client = New-Object System.IO.Pipes.NamedPipeClientStream('.', 'SteamGate', [System.IO.Pipes.PipeDirection]::InOut)
    $client.Connect(15000)
    return $client
}

function Send-PipeText {
    param($Client, [string]$Text)
    $bytes = [Text.Encoding]::UTF8.GetBytes($Text)
    $Client.Write($bytes, 0, $bytes.Length)
    $Client.Flush()
}

# Starts PipeProbe.ps1 and returns the process. Pass -Credential to run it as another user.
function Start-PipeProbe {
    param([string]$OutFile, [int]$WaitSeconds, $Credential = $null, [string]$WorkingDirectory = $env:SystemRoot)
    Remove-Item -LiteralPath $OutFile -Force -ErrorAction SilentlyContinue
    $arguments = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "`"$ProbeScript`"", '-OutFile', "`"$OutFile`"", '-WaitSeconds', "$WaitSeconds")
    $parameters = @{ FilePath = 'powershell.exe'; ArgumentList = $arguments; WindowStyle = 'Hidden'; PassThru = $true; WorkingDirectory = $WorkingDirectory }
    if ($null -ne $Credential) {
        $parameters['Credential'] = $Credential
        $parameters['LoadUserProfile'] = $true
    }
    return (Start-Process @parameters)
}

# Waits for the probe's file and returns its text, or fails.
function Wait-ProbeResult {
    param([string]$OutFile, [int]$Seconds, [string]$What)
    $deadline = (Get-Date).AddSeconds($Seconds)
    while ((Get-Date) -lt $deadline) {
        if (Test-Path -LiteralPath $OutFile) {
            $text = [IO.File]::ReadAllText($OutFile).Trim()
            if ($text.Length -gt 0) { return $text }
        }
        Start-Sleep -Milliseconds 500
    }
    Fail "$What did not write $OutFile within $Seconds seconds"
}

function Assert-ProbeConnected {
    param([string]$Text, [string]$What)
    if ($Text -notmatch '^CONNECTED (S-1-5-18|S-1-5-32-544)$') { Fail "$What wrote ""$Text"", expected CONNECTED S-1-5-18 or CONNECTED S-1-5-32-544" }
}

# 11. Enforcement: with no time left, a game started after the limit is closed (polite close, then force close).
try {
    Reset-SteamGate @{ weeknightMinutes = 0; weekendNightMinutes = 0 }
    $servicePid = New-FakeSteamRoot
    $mark = Wait-SteamReady -ServicePid $servicePid -Seconds 30
    $fakeGame = Start-Process -FilePath $FakeGameExe -ArgumentList '-n', '600', '127.0.0.1' -WindowStyle Hidden -PassThru
    $deadline = (Get-Date).AddSeconds(90)
    while (-not $fakeGame.HasExited) {
        if ((Get-Date) -gt $deadline) { Fail 'FakeGame was still running 90 seconds after it started with no time left' }
        Start-Sleep -Seconds 1
    }
    $fresh = @(Get-LogLines | Select-Object -Skip $mark)
    if (@($fresh | Where-Object { $_ -match 'polite close FakeGame\s*$' }).Count -eq 0) { Fail 'service.log has no "polite close FakeGame"' }
    if (@($fresh | Where-Object { $_ -match 'force close FakeGame\s*$' }).Count -gt 0) {
        Write-Host 'FakeGame ended by force close'
    } else {
        Write-Host 'FakeGame exited after the polite close'
    }
    # The helper check runs about every 30 seconds, so allow a little longer for its line.
    $helperLine = $null
    $helperDeadline = (Get-Date).AddSeconds(60)
    while ($null -eq $helperLine) {
        $found = @(Get-LogLines | Select-Object -Skip $mark | Where-Object { $_ -match 'helper started session=\d+\s*$|helper not started: no console user\s*$' })
        if ($found.Count -gt 0) { $helperLine = $found[0] }
        elseif ((Get-Date) -gt $helperDeadline) { Fail 'service.log has neither "helper started session=" nor "helper not started: no console user"' }
        else { Start-Sleep -Seconds 1 }
    }
    if ($helperLine -match 'helper started session=') { Write-Host 'helper started session=' } else { Write-Host 'helper not started: no console user' }
    Write-Host 'ENFORCED FakeGame'
} finally {
    Remove-FakeSteamRoot
    # The script ends uninstalled, as the earlier groups left it.
    Invoke-TestUninstall
}

# 12. The pipe: bad clients cannot stop the service, and a good client is heard.
$clients = @()
try {
    Reset-SteamGate @{}
    $servicePidBefore = Get-ServicePid
    $mark = (Get-LogLines).Count

    $first = Connect-SteamGatePipe
    $clients += $first
    Send-PipeText -Client $first -Text "not json`n"

    $second = Connect-SteamGatePipe
    $clients += $second
    $chunk = New-Object byte[] 65536
    try {
        # The server drops this connection after 16 KB, so the write ends with an error long before 10 MB.
        for ($sent = 0; $sent -lt (10MB); $sent += $chunk.Length) {
            $second.Write($chunk, 0, $chunk.Length)
        }
    } catch [System.IO.IOException] {
        Write-Host 'the server dropped the 10 MB sender'
    }

    $third = Connect-SteamGatePipe
    $clients += $third
    Send-PipeText -Client $third -Text "{`"type`":`"hello`",`"role`":`"status`"}`n"
    if (-not (Wait-LogLine -Pattern "pipe hello role=status pid=$PID\s*$" -After $mark -Seconds 2)) { Fail "service.log has no ""pipe hello role=status pid=$PID"" within 2 seconds" }
    if ((Get-ServicePid) -ne $servicePidBefore) { Fail 'the service process changed after the bad pipe clients' }
    $aliveMark = (Get-LogLines).Count
    if (-not (Wait-LogLine -Pattern 'alive\s*$' -After $aliveMark -Seconds 70)) { Fail 'no new "alive" line within 70 seconds after the pipe checks' }
    if ((Get-ServicePid) -ne $servicePidBefore) { Fail 'the service process changed during the pipe checks' }
    Write-Host 'PIPE OK'
} finally {
    foreach ($client in $clients) { $client.Dispose() }
    Invoke-TestUninstall
}

# 12b and 12c. A standard user's rights are enough: the probe and the real helper both reach the pipe without admin.
$probeUser = 'sgprobe'
$probeDir = Join-Path $FakeRootBase 'SteamGateProbe'
$userHelper = $null
$userCreated = $false
try {
    Reset-SteamGate @{}
    $userPassword = 'Sg1!' + [Guid]::NewGuid().ToString('N')
    New-LocalUser -Name $probeUser -Password (ConvertTo-SecureString $userPassword -AsPlainText -Force) -AccountNeverExpires -PasswordNeverExpires | Out-Null
    $userCreated = $true
    try { Add-LocalGroupMember -Group 'Users' -Member $probeUser -ErrorAction Stop } catch { Write-Host "Users group: $($_.Exception.Message)" }
    $credential = New-Object System.Management.Automation.PSCredential("$env:COMPUTERNAME\$probeUser", (ConvertTo-SecureString $userPassword -AsPlainText -Force))

    Remove-FolderWithRetry -Path $probeDir
    New-Item -ItemType Directory -Path $probeDir -Force | Out-Null
    Invoke-Native 'icacls grant probe folder' { icacls $probeDir /grant "${env:COMPUTERNAME}\${probeUser}:(OI)(CI)M" }
    $userOut = Join-Path $probeDir 'standard-user.txt'
    Start-PipeProbe -OutFile $userOut -WaitSeconds 20 -Credential $credential | Out-Null
    $userResult = Wait-ProbeResult -OutFile $userOut -Seconds 60 -What 'the standard-user probe'
    Assert-ProbeConnected -Text $userResult -What 'the standard-user probe'
    Write-Host 'PIPE USER OK'

    $mark = (Get-LogLines).Count
    $userHelper = Start-Process -FilePath (Join-Path $ProgramDir 'SteamGate.Helper.exe') -Credential $credential -LoadUserProfile -WorkingDirectory $env:SystemRoot -PassThru
    if (-not (Wait-LogLine -Pattern "pipe hello role=helper pid=$($userHelper.Id)\s*$" -After $mark -Seconds 15)) { Fail "service.log has no ""pipe hello role=helper pid=$($userHelper.Id)"" within 15 seconds" }
    Write-Host 'HELPER USER OK'
} finally {
    # Each cleanup step is guarded, so a failing one cannot skip the uninstall at the end.
    if ($null -ne $userHelper) {
        try { Stop-Process -Id $userHelper.Id -Force -ErrorAction SilentlyContinue } catch { Write-Host "cleanup: $($_.Exception.Message)" }
    }
    if ($userCreated) {
        try { Remove-LocalUser -Name $probeUser -ErrorAction SilentlyContinue } catch { Write-Host "cleanup: $($_.Exception.Message)" }
        try {
            Get-CimInstance -ClassName Win32_UserProfile -ErrorAction SilentlyContinue | Where-Object { $_.LocalPath -like "*\$probeUser" } | Remove-CimInstance -ErrorAction SilentlyContinue
        } catch { Write-Host "cleanup: $($_.Exception.Message)" }
    }
    try { Remove-FolderWithRetry -Path $probeDir } catch { Write-Host "cleanup: $($_.Exception.Message)" }
    Invoke-TestUninstall
}

# 13. Another process holds the pipe name: the service keeps running and logs it, then takes the pipe back when it is free.
$squatter = $null
try {
    Reset-SteamGate @{}
    $oldPid = Get-ServicePid
    $mark = (Get-LogLines).Count
    Stop-Process -Id $oldPid -Force
    # Windows restarts the service after 5 seconds; the squatter must hold the name before it comes back.
    $squatDeadline = (Get-Date).AddSeconds(5)
    while ($null -eq $squatter) {
        try {
            $squatter = New-Object System.IO.Pipes.NamedPipeServerStream('SteamGate', [System.IO.Pipes.PipeDirection]::InOut, 1)
        } catch {
            if ((Get-Date) -gt $squatDeadline) { Fail "could not take the pipe name within 5 seconds: $($_.Exception.Message)" }
            Start-Sleep -Milliseconds 100
        }
    }
    $squatPid = Wait-ServicePid -NotPid $oldPid -Seconds 60 -What 'the restart while the pipe name was held'
    for ($second = 1; $second -le 30; $second++) {
        Start-Sleep -Seconds 1
        if ((Get-ServicePid) -ne $squatPid) { Fail "pid $squatPid did not stay Running for 30 seconds while the pipe name was held" }
    }
    if (@(Get-LogLines | Select-Object -Skip $mark | Where-Object { $_ -match 'pipe unavailable' }).Count -eq 0) { Fail 'service.log has no "pipe unavailable" while the pipe name was held' }
    Write-Host 'PIPE SQUAT OK'

    $squatter.Dispose()
    $squatter = $null
    $mark = (Get-LogLines).Count
    $adminOut = Join-Path $FakeRootBase 'admin-probe.txt'
    $adminProbe = Start-PipeProbe -OutFile $adminOut -WaitSeconds 15
    $adminResult = Wait-ProbeResult -OutFile $adminOut -Seconds 15 -What 'the admin probe after the pipe was released'
    Assert-ProbeConnected -Text $adminResult -What 'the admin probe after the pipe was released'
    if (-not (Wait-LogLine -Pattern "pipe hello role=status pid=$($adminProbe.Id)\s*$" -After $mark -Seconds 5)) { Fail "service.log has no ""pipe hello role=status pid=$($adminProbe.Id)"" after the pipe was released" }
    Remove-Item -LiteralPath $adminOut -Force -ErrorAction SilentlyContinue
    Write-Host 'PIPE RECOVERED'
} finally {
    if ($null -ne $squatter) { $squatter.Dispose() }
    Invoke-TestUninstall
}

# 14. A running helper does not stop the uninstall.
$runningHelper = $null
try {
    Reset-SteamGate @{}
    $runningHelper = Start-Process -FilePath (Join-Path $ProgramDir 'SteamGate.Helper.exe') -PassThru
    Start-Sleep -Seconds 3
    Invoke-TestUninstall
    $gone = $false
    for ($second = 0; $second -lt 30; $second++) {
        if (-not (Get-Service -Name $ServiceName -ErrorAction SilentlyContinue)) { $gone = $true; break }
        Start-Sleep -Seconds 1
    }
    if (-not $gone) { Fail 'the service is still listed 30 seconds after uninstall with the helper running' }
    if (Test-Path -LiteralPath $ProgramDir) { Fail "$ProgramDir still exists after uninstall with the helper running" }
    Write-Host 'UNINSTALLED'
} finally {
    if ($null -ne $runningHelper) { Stop-Process -Id $runningHelper.Id -Force -ErrorAction SilentlyContinue }
}

# Helpers for the lock checks.

# Sends one pipe line through PipeProbe.ps1 as the admin test runner and returns the reply it got, parsed.
# The probe hello is "status"; -Line goes right after it and -ReplyFile receives the first reply line.
function Send-PipeRequest {
    param([Parameter(Mandatory)][string]$Json, [Parameter(Mandatory)][string]$RequestId, [int]$Seconds = 25)
    $out = Join-Path $FakeRootBase 'request-probe.txt'
    $replyFile = Join-Path $FakeRootBase 'request-reply.txt'
    Remove-Item -LiteralPath $out, $replyFile -Force -ErrorAction SilentlyContinue
    $quoted = $Json.Replace('"', '\"')
    $arguments = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "`"$ProbeScript`"", '-OutFile', "`"$out`"", '-WaitSeconds', '10', '-HoldSeconds', '0', '-Line', "`"$quoted`"", '-ReplyFile', "`"$replyFile`"")
    Start-Process -FilePath 'powershell.exe' -ArgumentList $arguments -WindowStyle Hidden -WorkingDirectory $env:SystemRoot | Out-Null
    $text = Wait-ProbeResult -OutFile $replyFile -Seconds $Seconds -What "the request probe for $RequestId"
    if ($text -like 'FAILED*') { Fail "the request probe for $RequestId wrote ""$text""" }
    $reply = $text | ConvertFrom-Json
    if ($reply.type -ne 'reply' -or $reply.requestId -ne $RequestId) { Fail "the reply to $RequestId was ""$text""" }
    return $reply
}

# Reads settings.json until $Check returns true for it, or fails after $Seconds. The service replaces the file atomically,
# so a read can collide with a save; such a read is just tried again.
function Wait-Settings {
    param([scriptblock]$Check, [int]$Seconds, [string]$What)
    $deadline = (Get-Date).AddSeconds($Seconds)
    while ($true) {
        try {
            $json = Get-Content -Raw -LiteralPath $SettingsPath | ConvertFrom-Json
            if (& $Check $json) { return $json }
        } catch {
            # Read again.
        }
        if ((Get-Date) -gt $deadline) { Fail "settings.json did not show $What within $Seconds seconds" }
        Start-Sleep -Seconds 1
    }
}

# 15. The lock: a loosening request waits the cooldown, and a tightening one applies at once.
try {
    Reset-SteamGate @{}
    $raise = Send-PipeRequest -RequestId 'r1' -Json '{"type":"request","requestId":"r1","field":"weeknightMinutes","number":120}'
    if (-not $raise.message.StartsWith('Waits 48 h')) { Fail "the reply to the raise was ""$($raise.message)"", expected it to start with ""Waits 48 h""" }
    Wait-Settings -Seconds 70 -What 'weeknightMinutes 90 with one pending entry' -Check {
        param($json)
        return (($json.weeknightMinutes -eq 90) -and (@($json.pending).Count -eq 1) -and ($json.pending[0].number -eq 120))
    } | Out-Null
    Write-Host 'PENDING OK'

    $lower = Send-PipeRequest -RequestId 'r2' -Json '{"type":"request","requestId":"r2","field":"weeknightMinutes","number":60}'
    if ($lower.message -ne 'Applied now.') { Fail "the reply to the tightening was ""$($lower.message)"", expected ""Applied now.""" }
    Wait-Settings -Seconds 70 -What 'weeknightMinutes 60 with the raise still pending' -Check {
        param($json)
        return (($json.weeknightMinutes -eq 60) -and (@($json.pending).Count -eq 1) -and ($json.pending[0].number -eq 120))
    } | Out-Null
    Write-Host 'TIGHTEN OK'
} finally {
    Invoke-TestUninstall
}

# 16. The lock tightens for real: with the limits lowered to 0, a game that starts is closed.
try {
    Reset-SteamGate @{}
    $servicePid = New-FakeSteamRoot
    $mark = Wait-SteamReady -ServicePid $servicePid -Seconds 30
    foreach ($field in @('weeknightMinutes', 'weekendNightMinutes')) {
        $requestId = "z-$field"
        $answer = Send-PipeRequest -RequestId $requestId -Json ('{"type":"request","requestId":"' + $requestId + '","field":"' + $field + '","number":0}')
        if ($answer.message -ne 'Applied now.') { Fail "the reply to $field 0 was ""$($answer.message)"", expected ""Applied now.""" }
    }
    Start-Process -FilePath $FakeGameExe -ArgumentList '-n', '600', '127.0.0.1' -WindowStyle Hidden | Out-Null
    if (-not (Wait-LogLine -Pattern 'polite close FakeGame\s*$' -After $mark -Seconds 90)) { Fail 'service.log has no "polite close FakeGame" within 90 seconds of the limits being lowered to 0' }
    Write-Host 'TIGHTEN ENFORCED'
} finally {
    Remove-FakeSteamRoot
    Invoke-TestUninstall
}

# 17. Uninstall is refused until allowUninstall has waited out the cooldown.
$uninstallScript = Join-Path $PublishDir 'Uninstall-SteamGate.ps1'
$refusalText = 'Removal is not allowed yet. Request it in SteamGate; it applies after the cooldown.'

# Runs the real uninstall script directly and fails unless it exits 1 with the refusal printed and the service still running.
function Assert-UninstallRefused {
    param([string]$What)
    $global:LASTEXITCODE = 0
    $printed = (& $uninstallScript *>&1 | Out-String)
    if ($LASTEXITCODE -ne 1) { Fail "the uninstall $What exited with code $LASTEXITCODE, expected 1" }
    # Console output wraps long lines, so compare with all runs of whitespace folded to one space.
    $flat = ($printed -replace '\s+', ' ')
    if (-not $flat.Contains($refusalText)) { Fail "the uninstall $What printed ""$printed"", expected the refusal" }
    if ((Get-ServicePid) -eq 0) { Fail "the service is not running after the refused uninstall $What" }
    if (-not (Test-Path -LiteralPath $ShortcutPath)) { Fail "the Start Menu shortcut is gone after the refused uninstall $What" }
}

# Replaces settings.json the way a careful admin would: write a temp file, then move it over.
function Set-SettingsFileText {
    param([string]$Text)
    $temp = $SettingsPath + '.edit'
    [IO.File]::WriteAllText($temp, $Text)
    Move-Item -LiteralPath $temp -Destination $SettingsPath -Force
}

try {
    Reset-SteamGate @{}
    Assert-UninstallRefused -What 'with allowUninstall false'
    Write-Host 'UNINSTALL REFUSED'

    $json = Get-Content -Raw -LiteralPath $SettingsPath | ConvertFrom-Json
    $json.PSObject.Properties.Remove('allowUninstall')
    Set-SettingsFileText -Text ($json | ConvertTo-Json -Depth 5)
    Assert-UninstallRefused -What 'with allowUninstall missing'

    $json | Add-Member -NotePropertyName allowUninstall -NotePropertyValue 'true' -Force
    Set-SettingsFileText -Text ($json | ConvertTo-Json -Depth 5)
    Assert-UninstallRefused -What 'with allowUninstall as the text "true"'

    Set-SettingsFileText -Text '{'
    Assert-UninstallRefused -What 'with settings.json unreadable'

    Reset-SteamGate @{ cooldownHours = 0 }
    $allow = Send-PipeRequest -RequestId 'u1' -Seconds 30 -Json '{"type":"request","requestId":"u1","field":"allowUninstall","flag":true}'
    if ($allow.message -ne 'Applied now.') { Fail "the reply to allowUninstall true was ""$($allow.message)"", expected ""Applied now.""" }
    $global:LASTEXITCODE = 0
    & $uninstallScript
    if ($LASTEXITCODE -ne 0) { Fail "the allowed uninstall exited with code $LASTEXITCODE" }
    $gone = $false
    for ($second = 0; $second -lt 30; $second++) {
        if (-not (Get-Service -Name $ServiceName -ErrorAction SilentlyContinue)) { $gone = $true; break }
        Start-Sleep -Seconds 1
    }
    if (-not $gone) { Fail 'the service is still listed 30 seconds after the allowed uninstall' }
    if (Test-Path -LiteralPath $RegistryKey) { Fail "$RegistryKey still exists after the allowed uninstall" }
    if (Test-Path -LiteralPath $DataDir) { Fail "$DataDir still exists after the allowed uninstall" }
    Write-Host 'UNINSTALL UNLOCKED'
} finally {
    if (Get-Service -Name $ServiceName -ErrorAction SilentlyContinue) { Invoke-TestUninstall }
}

# 18. The status screen's state: a client that speaks the screen's pipe protocol (PipeProbe.ps1, hello as "status") gets a state line from the service.
try {
    Reset-SteamGate @{}
    $stateOut = Join-Path $FakeRootBase 'state-probe.txt'
    $stateLineFile = Join-Path $FakeRootBase 'state-line.txt'
    Remove-Item -LiteralPath $stateOut, $stateLineFile -Force -ErrorAction SilentlyContinue
    # The night is the local date minus 4 hours; the limit depends on whether it is a weekend night. Both ends of the wait are checked, so a boundary in between cannot fail the test.
    $weekendNights = @('Friday', 'Saturday', 'Sunday')
    $limitBefore = if ($weekendNights -contains [string](Get-Date).AddHours(-4).DayOfWeek) { 180 } else { 90 }
    $arguments = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "`"$ProbeScript`"", '-OutFile', "`"$stateOut`"", '-WaitSeconds', '10', '-HoldSeconds', '0', '-StateFile', "`"$stateLineFile`"")
    Start-Process -FilePath 'powershell.exe' -ArgumentList $arguments -WindowStyle Hidden -WorkingDirectory $env:SystemRoot | Out-Null
    $stateText = Wait-ProbeResult -OutFile $stateLineFile -Seconds 15 -What 'the state probe'
    if ($stateText -like 'FAILED*') { Fail "the state probe wrote ""$stateText""" }
    $limitAfter = if ($weekendNights -contains [string](Get-Date).AddHours(-4).DayOfWeek) { 180 } else { 90 }
    $state = $stateText | ConvertFrom-Json
    if ($state.type -ne 'state') { Fail "the first line the probe kept had type ""$($state.type)"", expected state" }
    if ($state.settings.weeknightMinutes -ne 90) { Fail "the state has weeknightMinutes $($state.settings.weeknightMinutes), expected 90" }
    if ($state.settings.cooldownHours -ne 48) { Fail "the state has cooldownHours $($state.settings.cooldownHours), expected 48" }
    if (@($state.settings.notCounted).Count -ne 4) { Fail "the state has $(@($state.settings.notCounted).Count) not-counted entries, expected 4" }
    if (@($state.settings.notCounted | Where-Object { $_.displayName -eq 'Wallpaper Engine' }).Count -ne 1) { Fail 'the state has no not-counted entry named Wallpaper Engine' }
    if (($state.tonightLimitMinutes -ne $limitBefore) -and ($state.tonightLimitMinutes -ne $limitAfter)) { Fail "the state has tonightLimitMinutes $($state.tonightLimitMinutes), expected $limitBefore" }
    Write-Host 'STATE OK'
} finally {
    Invoke-TestUninstall
}

# 19. The real status screen program: it says hello, runs one copy per user per session, and closes cleanly.
$statusExe = Join-Path $ProgramDir 'SteamGate.Status.exe'
$statusFirst = $null
$statusSecond = $null
try {
    Reset-SteamGate @{}
    $mark = (Get-LogLines).Count
    # Reading Handle at once keeps the exit code readable after the process ends in Windows PowerShell 5.1.
    $statusFirst = Start-Process -FilePath $statusExe -PassThru
    $null = $statusFirst.Handle
    if (-not (Wait-LogLine -Pattern "pipe hello role=status pid=$($statusFirst.Id)\s*$" -After $mark -Seconds 15)) { Fail "service.log has no ""pipe hello role=status pid=$($statusFirst.Id)"" within 15 seconds" }

    $statusSecond = Start-Process -FilePath $statusExe -PassThru
    $null = $statusSecond.Handle
    if (-not $statusSecond.WaitForExit(5000)) { Fail 'the second copy of the status screen was still running 5 seconds after it started' }
    if ($statusSecond.ExitCode -ne 0) { Fail "the second copy of the status screen exited with code $($statusSecond.ExitCode), expected 0" }
    if ($statusFirst.HasExited) { Fail 'the first copy of the status screen ended when the second one started' }
    Write-Host 'STATUS SINGLE OK'

    $windowDeadline = (Get-Date).AddSeconds(15)
    while ($true) {
        $statusFirst.Refresh()
        if ($statusFirst.MainWindowHandle -ne [IntPtr]::Zero) { break }
        if ((Get-Date) -gt $windowDeadline) { Fail 'the status screen had no main window within 15 seconds' }
        Start-Sleep -Milliseconds 500
    }
    $null = $statusFirst.CloseMainWindow()
    if (-not $statusFirst.WaitForExit(10000)) { Fail 'the status screen was still running 10 seconds after its window was closed' }
    if ($statusFirst.ExitCode -ne 0) { Fail "the status screen exited with code $($statusFirst.ExitCode) after its window was closed, expected 0" }
    Write-Host 'STATUS OK'
} finally {
    foreach ($statusProcess in @($statusFirst, $statusSecond)) {
        if (($null -ne $statusProcess) -and (-not $statusProcess.HasExited)) { Stop-Process -Id $statusProcess.Id -Force -ErrorAction SilentlyContinue }
    }
    Invoke-TestUninstall
}

# 20. The Start Menu shortcut: install makes it, an upgrade puts it right again, and uninstall removes it.
function Get-ShortcutTarget {
    $shell = New-Object -ComObject WScript.Shell
    try {
        return $shell.CreateShortcut($ShortcutPath).TargetPath
    } finally {
        [void][System.Runtime.InteropServices.Marshal]::ReleaseComObject($shell)
    }
}

function Set-ShortcutTarget {
    param([string]$Target)
    $shell = New-Object -ComObject WScript.Shell
    try {
        $link = $shell.CreateShortcut($ShortcutPath)
        $link.TargetPath = $Target
        $link.Save()
    } finally {
        [void][System.Runtime.InteropServices.Marshal]::ReleaseComObject($shell)
    }
}

try {
    Reset-SteamGate @{}
    $expectedTarget = Join-Path $env:ProgramFiles 'SteamGate\SteamGate.Status.exe'
    if (-not (Test-Path -LiteralPath $ShortcutPath)) { Fail "$ShortcutPath does not exist after the install" }
    $target = Get-ShortcutTarget
    if ($target -ne $expectedTarget) { Fail "the shortcut points at ""$target"", expected ""$expectedTarget""" }
    Write-Host 'SHORTCUT OK'

    Set-ShortcutTarget -Target 'C:\Windows\notepad.exe'
    $beforeUpgrade = Get-ServicePid
    Invoke-Script (Join-Path $PublishDir 'Install-SteamGate.ps1')
    Wait-ServicePid -NotPid $beforeUpgrade -Seconds 60 -What 'the service running after the upgrade' | Out-Null
    $target = Get-ShortcutTarget
    if ($target -ne $expectedTarget) { Fail "the shortcut points at ""$target"" after the upgrade, expected ""$expectedTarget""" }

    Invoke-TestUninstall
    if (Test-Path -LiteralPath $ShortcutPath) { Fail "$ShortcutPath still exists after the uninstall" }
    Write-Host 'SHORTCUT REMOVED'
} finally {
    if (Get-Service -Name $ServiceName -ErrorAction SilentlyContinue) { Invoke-TestUninstall }
}
