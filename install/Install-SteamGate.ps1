# Installs SteamGate as a Windows service, or upgrades an installed copy, and makes the Start Menu shortcut that opens SteamGate.Status.exe, the status screen.
# Run from an administrator Windows PowerShell, in the folder that holds this script, SteamGate.Common.ps1, and the published SteamGate.Service.exe,
# SteamGate.Helper.exe, and SteamGate.Status.exe (SteamGate.Service.exe must be next to this script and SteamGate.Status.exe must be in the program folder after the copy; every file next to this script that is not a .ps1 file is copied).
Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'SteamGate.Common.ps1')

$ExeName = 'SteamGate.Service.exe'

function Set-Recovery {
    Invoke-Native 'sc failure' { sc.exe failure $ServiceName reset= 86400 actions= restart/5000/restart/5000/restart/5000 }
    Invoke-Native 'sc failureflag' { sc.exe failureflag $ServiceName 1 }
    Write-Host 'INSTALL: recovery set (restart after 5 seconds, three times, then keep restarting)'
}

function Copy-ProgramFiles {
    if (-not (Test-Path -LiteralPath (Join-Path $PSScriptRoot $ExeName))) {
        throw "$ExeName is not next to this script"
    }
    New-Item -ItemType Directory -Path $ProgramDir -Force | Out-Null
    foreach ($file in @(Get-ChildItem -LiteralPath $PSScriptRoot -File | Where-Object { $_.Extension -ne '.ps1' })) {
        Copy-Item -LiteralPath $file.FullName -Destination $ProgramDir -Force
    }
    Write-Host "INSTALL: program files copied to $ProgramDir"
}

# Makes the Start Menu shortcut that opens the status screen. Any file already at the path is deleted first, so an upgrade also repairs a shortcut that was changed.
function New-StartMenuShortcut {
    $statusExe = Join-Path $ProgramDir 'SteamGate.Status.exe'
    if (-not (Test-Path -LiteralPath $statusExe)) {
        throw "SteamGate.Status.exe is not in $ProgramDir, so the Start Menu shortcut cannot be made; publish it next to this script and run the install again"
    }
    if (Test-Path -LiteralPath $ShortcutPath) { Remove-Item -LiteralPath $ShortcutPath -Force }
    $shell = New-Object -ComObject WScript.Shell
    try {
        $link = $shell.CreateShortcut($ShortcutPath)
        $link.TargetPath = $statusExe
        $link.WorkingDirectory = $ProgramDir
        $link.Save()
    } finally {
        [void][System.Runtime.InteropServices.Marshal]::ReleaseComObject($shell)
    }
    Write-Host 'INSTALL: Start Menu shortcut created'
}

function Start-GateService {
    Start-Service -Name $ServiceName
    (Get-Service -Name $ServiceName).WaitForStatus('Running', [TimeSpan]::FromSeconds(30))
    Write-Host 'INSTALL: service started'
}

function New-DataFolder {
    if (Test-Path -LiteralPath $DataDir) {
        $stamp = Get-Date -Format 'yyyyMMddTHHmmss'
        Rename-Item -LiteralPath $DataDir -NewName "SteamGate.old-$stamp"
        Write-Host "INSTALL: existing $DataDir renamed to SteamGate.old-$stamp"
    }
    # No -Force: this fails if the folder came back after the rename.
    New-Item -ItemType Directory -Path $DataDir | Out-Null
    Invoke-Native 'icacls grant' { icacls $DataDir /inheritance:r /grant:r "*${SystemSid}:(OI)(CI)F" "*${AdminsSid}:(OI)(CI)F" }
    Invoke-Native 'icacls setowner' { icacls $DataDir /setowner "*$AdminsSid" }
    # With the permissions tight no standard user can add to the folder, so this check is conclusive.
    $leftovers = @(Get-ChildItem -LiteralPath $DataDir -Force)
    if ($leftovers.Count -gt 0) { throw "$DataDir is not empty after locking it down" }
    $problem = Test-DataFolderAcl -Path $DataDir
    if ($problem) { throw "$DataDir permissions are wrong: $problem" }
    Write-Host "INSTALL: $DataDir created, admin-only"
}

function Install-Upgrade {
    Write-Host 'INSTALL: SteamGate is already installed, upgrading'
    $failure = $null
    try {
        Clear-Recovery
        Stop-SteamGateProcesses -Prefix 'INSTALL'
        Copy-ProgramFiles
        New-StartMenuShortcut
    } catch {
        $failure = $_.Exception.Message
    }
    # Recovery and the service come back even if the copy failed. The first failure is the one reported.
    foreach ($step in @({ Set-Recovery }, { Start-GateService })) {
        try { & $step } catch { if (-not $failure) { $failure = $_.Exception.Message } }
    }
    if ($failure) { throw "upgrade failed: $failure" }
    Write-Host 'INSTALL: upgrade'
}

function Install-Fresh {
    Copy-ProgramFiles
    New-StartMenuShortcut
    New-DataFolder
    $binaryPath = '"' + (Join-Path $ProgramDir $ExeName) + '"'
    New-Service -Name $ServiceName -DisplayName 'SteamGate' -StartupType Automatic -BinaryPathName $binaryPath `
        -Description 'Counts Steam game time and enforces the limits set in SteamGate.' | Out-Null
    Write-Host 'INSTALL: service registered'
    Set-Recovery
    Start-GateService
    Write-Host 'INSTALL: fresh'
}

try {
    Assert-Administrator
    if (Get-Service -Name $ServiceName -ErrorAction SilentlyContinue) {
        Install-Upgrade
    } else {
        Install-Fresh
    }
} catch {
    Write-Host "INSTALL: failed: $($_.Exception.Message)"
    exit 1
}
