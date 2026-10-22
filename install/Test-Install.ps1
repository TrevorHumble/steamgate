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
    param([int]$ServicePid)
    $deadline = (Get-Date).AddSeconds(60)
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
    Fail "service.log has no ""started pid=$ServicePid"" followed by ""libraries <count>"" within 60 seconds"
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
Invoke-Script (Join-Path $PublishDir 'Uninstall-SteamGate.ps1')
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
    Invoke-Script (Join-Path $PublishDir 'Uninstall-SteamGate.ps1')
}
