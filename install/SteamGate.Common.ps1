# Shared by Install-SteamGate.ps1, Uninstall-SteamGate.ps1, and Test-Install.ps1. Dot-source it; do not run it.
# The installer skips every .ps1 file when it copies into Program Files, so this file is never installed.

$ServiceName = 'SteamGate'
$ProgramDir = Join-Path $env:ProgramFiles 'SteamGate'
$DataDir = Join-Path $env:ProgramData 'SteamGate'
# The service keeps its boot marker under this key (the volatile child Boot); the uninstall script and the test reset remove the whole key.
$RegistryKey = 'HKLM:\SOFTWARE\SteamGate'
$AdminsSid = 'S-1-5-32-544'
$SystemSid = 'S-1-5-18'

function Invoke-Native {
    param([string]$What, [scriptblock]$Command)
    $output = & $Command
    if ($LASTEXITCODE -ne 0) {
        throw "$What failed with exit code ${LASTEXITCODE}: $output"
    }
}

# Throws when the script is not running as an administrator.
function Assert-Administrator {
    $principal = New-Object System.Security.Principal.WindowsPrincipal([System.Security.Principal.WindowsIdentity]::GetCurrent())
    if (-not $principal.IsInRole([System.Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw 'run this from an administrator PowerShell'
    }
}

# Windows must not restart the service while it is being ended for an upgrade or removal.
# This goes through cmd.exe because Windows PowerShell drops the empty actions= "" argument.
function Clear-Recovery {
    $clearLine = 'sc.exe failure {0} reset= 0 actions= ""' -f $ServiceName
    Invoke-Native 'sc failure (clear)' { cmd.exe /c $clearLine }
}

# Ends the service process first and waits for it, so it cannot start a new helper in between. Only then does it end every other
# process running from Program Files (the helper, in every session), and wait for those.
function Stop-SteamGateProcesses {
    param([Parameter(Mandatory)][string]$Prefix)
    $ended = 0
    $service = Get-CimInstance -ClassName Win32_Service -Filter "Name='$ServiceName'"
    if ($service -and $service.ProcessId -gt 0) {
        $servicePid = [int]$service.ProcessId
        Stop-Process -Id $servicePid -Force -ErrorAction SilentlyContinue
        $alive = @(Get-Process -Id $servicePid -ErrorAction SilentlyContinue)
        if ($alive.Count -gt 0) { $alive | Wait-Process -Timeout 30 }
        $ended++
    }
    $registered = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
    if ($registered) { $registered.WaitForStatus('Stopped', [TimeSpan]::FromSeconds(30)) }
    $programPrefix = $ProgramDir + '\'
    $ids = New-Object System.Collections.Generic.List[int]
    foreach ($proc in @(Get-CimInstance -ClassName Win32_Process)) {
        if ($proc.ExecutablePath -and $proc.ExecutablePath.StartsWith($programPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
            $ids.Add([int]$proc.ProcessId)
        }
    }
    foreach ($id in $ids) { Stop-Process -Id $id -Force -ErrorAction SilentlyContinue }
    if ($ids.Count -gt 0) {
        $left = @(Get-Process -Id $ids.ToArray() -ErrorAction SilentlyContinue)
        if ($left.Count -gt 0) { $left | Wait-Process -Timeout 30 }
    }
    $ended += $ids.Count
    Write-Host "${Prefix}: ended $ended running process(es)"
}

# Returns a text problem, or nothing when the folder is admin-only.
function Test-DataFolderAcl {
    param([string]$Path)
    $acl = Get-Acl -LiteralPath $Path
    if (-not $acl.AreAccessRulesProtected) { return 'inheritance is still on' }
    $owner = $acl.GetOwner([System.Security.Principal.SecurityIdentifier]).Value
    if ($owner -ne $AdminsSid) { return "owner is $owner, not $AdminsSid" }
    $extra = @(Get-ForeignAccessSid -Acl $acl)
    if ($extra.Count -gt 0) { return "extra access entry for $($extra[0])" }
    return $null
}

# Returns the identities with an access rule in the ACL other than SYSTEM and Administrators.
function Get-ForeignAccessSid {
    param([Parameter(Mandatory)]$Acl)
    $sids = @($Acl.GetAccessRules($true, $true, [System.Security.Principal.SecurityIdentifier]) | ForEach-Object { $_.IdentityReference.Value })
    return @($sids | Where-Object { ($_ -ne $SystemSid) -and ($_ -ne $AdminsSid) })
}
