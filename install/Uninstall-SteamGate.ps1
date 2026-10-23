# Removes the SteamGate service, its program files, its settings folder, and the registry key HKLM:\SOFTWARE\SteamGate.
# With the service installed it refuses until settings.json says allowUninstall is true, which happens after a request to allow
# removal has waited out the cooldown (or an admin sets it by hand). There is no switch to skip that check. Without the service it cleans up whatever is left.
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

# True only when the last saved settings.json holds allowUninstall as the JSON value true. A missing folder or file, a file that
# cannot be read or parsed, a missing property, and a value of any other kind (the text "true", for example) all say no.
function Test-RemovalAllowed {
    try {
        $settings = Get-Content -Raw -LiteralPath (Join-Path $DataDir 'settings.json') | ConvertFrom-Json
        $property = $settings.PSObject.Properties['allowUninstall']
        return (($null -ne $property) -and ($property.Value -is [bool]) -and ($property.Value -eq $true))
    } catch {
        return $false
    }
}

try {
    Assert-Administrator
    if (Get-Service -Name $ServiceName -ErrorAction SilentlyContinue) {
        if (-not (Test-RemovalAllowed)) {
            Write-Host 'Removal is not allowed yet. Request it in SteamGate; it applies after the cooldown (until its status screen ships, an admin sets allowUninstall to true in settings.json and runs the uninstall right away).'
            exit 1
        }
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
    # A missing key is fine.
    if (Test-Path -LiteralPath $RegistryKey) {
        Remove-Item -LiteralPath $RegistryKey -Recurse -Force
        Write-Host "UNINSTALL: removed $RegistryKey"
    }
    Remove-Folder -Path $ProgramDir
    Remove-Folder -Path $DataDir
    Write-Host 'UNINSTALL: done'
} catch {
    Write-Host "UNINSTALL: failed: $($_.Exception.Message)"
    exit 1
}
