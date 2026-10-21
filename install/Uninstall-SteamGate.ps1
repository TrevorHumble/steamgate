# Removes the SteamGate service, its program files, and its settings folder.
# Run from an administrator Windows PowerShell, in the folder that holds this script and SteamGate.Common.ps1.
Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'SteamGate.Common.ps1')

# A just-ended program can hold its files for a moment, so retry a few times.
function Remove-Folder {
    param([string]$Path)
    if (-not (Test-Path -LiteralPath $Path)) {
        Write-Host "UNINSTALL: $Path not present"
        return
    }
    for ($attempt = 1; $attempt -le 10; $attempt++) {
        try {
            Remove-Item -LiteralPath $Path -Recurse -Force
            Write-Host "UNINSTALL: removed $Path"
            return
        } catch {
            if ($attempt -eq 10) { throw }
            Start-Sleep -Milliseconds 500
        }
    }
}

try {
    Assert-Administrator
    if (Get-Service -Name $ServiceName -ErrorAction SilentlyContinue) {
        # Recovery goes first, or Windows would restart the service as it is ended.
        Clear-Recovery
        Write-Host 'UNINSTALL: recovery cleared'
        Stop-SteamGateProcesses -Prefix 'UNINSTALL'
        Invoke-Native 'sc delete' { sc.exe delete $ServiceName }
        Write-Host 'UNINSTALL: service deleted'
    } else {
        Write-Host 'UNINSTALL: service not present'
        Stop-SteamGateProcesses -Prefix 'UNINSTALL'
    }
    Remove-Folder -Path $ProgramDir
    Remove-Folder -Path $DataDir
    Write-Host 'UNINSTALL: done'
} catch {
    Write-Host "UNINSTALL: failed: $($_.Exception.Message)"
    exit 1
}
