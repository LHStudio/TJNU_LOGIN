param(
    [string]$HostAddress = "115.24.187.161",
    [string]$Url = "http://115.24.187.161/",
    [switch]$Remove
)

$ErrorActionPreference = "Stop"
$settingsPath = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Internet Settings"

if (-not (Test-Path $settingsPath)) {
    throw "Could not find the current user's Windows Internet proxy settings."
}

$property = Get-ItemProperty -Path $settingsPath -Name ProxyOverride -ErrorAction SilentlyContinue
$existing = if ($null -eq $property.ProxyOverride) { @() } else {
    ([string]$property.ProxyOverride).Split(';') |
        ForEach-Object { $_.Trim() } |
        Where-Object { $_ -ne "" }
}

$hostPattern = "^(\*\.)?" + [regex]::Escape($HostAddress) + "(:\d+)?$"
$updated = @($existing | Where-Object { $_ -notmatch $hostPattern })

if (-not $Remove) {
    $updated += $HostAddress
}

$value = ($updated -join ';')
Set-ItemProperty -Path $settingsPath -Name ProxyOverride -Value $value

# Notify WinINET applications that the proxy configuration changed.
if (-not ([System.Management.Automation.PSTypeName]'ProxySettingsRefresh').Type) {
    Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class ProxySettingsRefresh {
    [DllImport("wininet.dll", SetLastError = true)]
    public static extern bool InternetSetOption(IntPtr hInternet, int dwOption, IntPtr lpBuffer, int dwBufferLength);
}
"@
}
[void][ProxySettingsRefresh]::InternetSetOption([IntPtr]::Zero, 39, [IntPtr]::Zero, 0) # SETTINGS_CHANGED
[void][ProxySettingsRefresh]::InternetSetOption([IntPtr]::Zero, 37, [IntPtr]::Zero, 0) # REFRESH

if ($Remove) {
    Write-Host "Removed $HostAddress from the proxy bypass list."
    exit 0
}

$chromeCandidates = @(
    "$env:ProgramFiles\Google\Chrome\Application\chrome.exe",
    "$env:ProgramFiles(x86)\Google\Chrome\Application\chrome.exe",
    "$env:LocalAppData\Google\Chrome\Application\chrome.exe"
) | Where-Object { $_ -and (Test-Path -LiteralPath $_) }
$chromeCandidates = @($chromeCandidates)

if (-not $chromeCandidates) {
    throw "Google Chrome was not found."
}

$chrome = (Get-Item -LiteralPath ([string]$chromeCandidates[0])).FullName
Start-Process -FilePath $chrome -WorkingDirectory (Split-Path -Parent $chrome) -ArgumentList @("--new-window", $Url)

Write-Host "Added $HostAddress to the proxy bypass list and opened $Url in Chrome."
Write-Host "To remove it: powershell -ExecutionPolicy Bypass -File `"$PSCommandPath`" -Remove"
