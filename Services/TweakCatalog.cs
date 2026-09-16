using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AdminWorks.Models;
using Microsoft.Win32;

namespace AdminWorks.Services
{
    public static class TweakCatalog
    {
        public static List<TweakItem> GetTweaks(ProfileService profileService)
        {
            var tweaks = new List<TweakItem>();

            #region 1. MAINTENANCE
            tweaks.Add(new TweakItem
            {
                Title = "Deep System Repair",
                Category = "Maintenance",
                CategoryTag = "DISM & SFC",
                Description = "Executes DISM Component Cleanup and System File Checker (SFC) integrity restoration.",
                IconGlyph = "\uE90F",
                Action = async (svc) =>
                {
                    await svc.RunProcessAsync("dism.exe", "/Online /Cleanup-Image /RestoreHealth", "DISM Restore Health");
                    await svc.RunProcessAsync("sfc.exe", "/scannow", "System File Checker");
                }
            });

            tweaks.Add(new TweakItem
            {
                Title = "Clean Component Store",
                Category = "Maintenance",
                CategoryTag = "WinSxS Reduction",
                Description = "Shrinks WinSxS store with /StartComponentCleanup /ResetBase.",
                IconGlyph = "\uE90F",
                Action = async (svc) =>
                {
                    await svc.RunProcessAsync("dism.exe", "/Online /Cleanup-Image /StartComponentCleanup /ResetBase", "WinSxS Store Cleanup");
                }
            });

            tweaks.Add(new TweakItem
            {
                Title = "Reset Windows Update",
                Category = "Maintenance",
                CategoryTag = "Update Repair",
                Description = "Purges stuck SoftwareDistribution & Catroot2 caches and restarts services.",
                IconGlyph = "\uE72C",
                Action = async (svc) =>
                {
                    var script = @"
Stop-Service -Name 'wuauserv', 'bits', 'cryptsvc' -Force -ErrorAction SilentlyContinue
Remove-Item '$env:SystemRoot\SoftwareDistribution\*' -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item '$env:SystemRoot\System32\catroot2\*' -Recurse -Force -ErrorAction SilentlyContinue
Start-Service -Name 'wuauserv', 'bits', 'cryptsvc' -ErrorAction SilentlyContinue
Write-Output 'Windows Update cache cleared and services restarted.'";
                    await svc.RunPowerShellScriptAsync(script, "Reset Windows Update");
                }
            });

            tweaks.Add(new TweakItem
            {
                Title = "Rebuild Icon & Font Cache",
                Category = "Maintenance",
                CategoryTag = "Explorer Repair",
                Description = "Clears corrupted thumbnail, font, and Windows Explorer icon databases.",
                IconGlyph = "\uE8B7",
                Action = async (svc) =>
                {
                    var script = @"
Stop-Process -Name explorer -Force -ErrorAction SilentlyContinue
Remove-Item '$env:LOCALAPPDATA\IconCache.db' -Force -ErrorAction SilentlyContinue
Remove-Item '$env:LOCALAPPDATA\Microsoft\Windows\Explorer\iconcache*' -Force -ErrorAction SilentlyContinue
Remove-Item '$env:LOCALAPPDATA\Microsoft\Windows\Explorer\thumbcache*' -Force -ErrorAction SilentlyContinue
Start-Process explorer.exe
Write-Output 'Icon and thumbnail cache rebuilt.'";
                    await svc.RunPowerShellScriptAsync(script, "Rebuild Icon Cache");
                }
            });

            tweaks.Add(new TweakItem
            {
                Title = "Clear Delivery Optimization",
                Category = "Maintenance",
                CategoryTag = "Disk Reclaim",
                Description = "Purges residual Windows Update peer-to-peer delivery caches to free gigabytes.",
                IconGlyph = "\uEDA2",
                Action = async (svc) =>
                {
                    var script = @"
try {
    if (Get-Command Delete-DeliveryOptimizationCache -ErrorAction SilentlyContinue) {
        Delete-DeliveryOptimizationCache -Force -ErrorAction SilentlyContinue
    } else {
        Stop-Service dosvc -Force -ErrorAction SilentlyContinue
        Remove-Item '$env:SystemRoot\ServiceProfiles\NetworkService\AppData\Local\Microsoft\Windows\DeliveryOptimization\Cache\*' -Recurse -Force -ErrorAction SilentlyContinue
        Start-Service dosvc -ErrorAction SilentlyContinue
    }
    Write-Output 'Delivery Optimization cache purged.'
} catch { Write-Output 'Failed to clear DO cache: ' + $_.Exception.Message }";
                    await svc.RunPowerShellScriptAsync(script, "Clear Delivery Optimization");
                }
            });

            tweaks.Add(new TweakItem
            {
                Title = "Repair WMI Repository",
                Category = "Maintenance",
                CategoryTag = "WMI Fix",
                Description = "Verifies and repairs corrupt Windows Management Instrumentation (WMI) repositories.",
                IconGlyph = "\uE7EF",
                Action = async (svc) =>
                {
                    await svc.RunProcessAsync("winmgmt.exe", "/verifyrepository", "WMI Verify");
                    await svc.RunProcessAsync("winmgmt.exe", "/salvagerepository", "WMI Salvage");
                }
            });

            tweaks.Add(new TweakItem
            {
                Title = "Reset Print Spooler",
                Category = "Maintenance",
                CategoryTag = "Printer Fix",
                Description = "Clears stuck printer queue files and restarts the Print Spooler service.",
                IconGlyph = "\uE90F",
                Action = async (svc) =>
                {
                    var script = @"
Stop-Service Spooler -Force -ErrorAction SilentlyContinue
Remove-Item '$env:SystemRoot\System32\Spool\Printers\*' -Force -ErrorAction SilentlyContinue
Start-Service Spooler -ErrorAction SilentlyContinue
Write-Output 'Print Spooler queue purged and restarted.'";
                    await svc.RunPowerShellScriptAsync(script, "Reset Print Spooler");
                }
            });

            tweaks.Add(new TweakItem
            {
                Title = "Purge Windows Event Logs",
                Category = "Maintenance",
                CategoryTag = "Log Cleaner",
                Description = "Clears all Application, System, Security, and Setup event logs to free space.",
                IconGlyph = "\uE7EF",
                Action = async (svc) =>
                {
                    var script = @"
Get-WinEvent -ListLog * -Force -ErrorAction SilentlyContinue | Where-Object { $_.RecordCount -gt 0 } | ForEach-Object {
    try { [System.Diagnostics.Eventing.Reader.EventLogSession]::GlobalSession.ClearLog($_.LogName) } catch {}
}
Write-Output 'All Windows Event Logs purged.'";
                    await svc.RunPowerShellScriptAsync(script, "Purge Event Logs");
                }
            });

            tweaks.Add(new TweakItem
            {
                Title = "Empty All Recycle Bins",
                Category = "Maintenance",
                CategoryTag = "Disk Reclaim",
                Description = "Purges deleted files in the Recycle Bin across all local and removable volumes.",
                IconGlyph = "\uEDA2",
                Action = async (svc) =>
                {
                    await svc.RunPowerShellScriptAsync("Clear-RecycleBin -Force -ErrorAction SilentlyContinue; Write-Output 'Recycle bins emptied.'", "Empty Recycle Bins");
                }
            });

            tweaks.Add(new TweakItem
            {
                Title = "Rebuild Windows Search Index",
                Category = "Maintenance",
                CategoryTag = "Search Fix",
                Description = "Stops search service, resets index catalog database, and forces rebuild.",
                IconGlyph = "\uE72C",
                Action = async (svc) =>
                {
                    var script = @"
Stop-Service wsearch -Force -ErrorAction SilentlyContinue
Set-ItemProperty -Path 'HKLM:\SOFTWARE\Microsoft\Windows Search' -Name 'SetupCompletedSuccessfully' -Value 0 -ErrorAction SilentlyContinue
Start-Service wsearch -ErrorAction SilentlyContinue
Write-Output 'Windows Search index catalog reset and rebuilding.'";
                    await svc.RunPowerShellScriptAsync(script, "Rebuild Search Index");
                }
            });
            #endregion

            #region 2. PERFORMANCE
            tweaks.Add(new TweakItem
            {
                Title = "Ultimate Power Plan",
                Category = "Performance",
                CategoryTag = "Power Scheme",
                Description = "Unlocks and activates the hidden Windows Ultimate Performance power plan.",
                IconGlyph = "\uE945",
                Action = async (svc) =>
                {
                    var script = @"
$planOut = powercfg -duplicatescheme e9a42b02-d5df-448d-aa00-03f14749eb61 2>$null
if ($planOut -match '([a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12})') {
    powercfg /setactive $matches[1]
} else {
    powercfg /setactive e9a42b02-d5df-448d-aa00-03f14749eb61 2>$null
}
Set-ItemProperty -Path 'HKCU:\Control Panel\Desktop' -Name 'MenuShowDelay' -Value '0'
Write-Output 'Ultimate Performance plan active.'";
                    await svc.RunPowerShellScriptAsync(script, "Ultimate Power Plan");
                }
            });

            tweaks.Add(new TweakItem
            {
                Title = "Visual Responsiveness",
                Category = "Performance",
                CategoryTag = "UI Boost",
                Description = "Disables window animations and menu fading for max FPS without clobbering font smoothing.",
                IconGlyph = "\uE7FC",
                Action = async (svc) =>
                {
                    var script = @"
reg add 'HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects' /v VisualFXSetting /t REG_DWORD /d 2 /f | Out-Null
reg add 'HKCU\Control Panel\Desktop\WindowMetrics' /v MinAnimate /t REG_SZ /d 0 /f | Out-Null
reg add 'HKCU\Control Panel\Desktop' /v MenuShowDelay /t REG_SZ /d 0 /f | Out-Null
reg add 'HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize' /v EnableTransparency /t REG_DWORD /d 0 /f | Out-Null
Write-Output 'Visual latency minimized.'";
                    await svc.RunPowerShellScriptAsync(script, "Visual Responsiveness");
                }
            });

            tweaks.Add(new TweakItem
            {
                Title = "Foreground CPU Boost",
                Category = "Performance",
                CategoryTag = "Thread Priority",
                Description = "Configures Win32PrioritySeparation (38) to prioritize foreground applications.",
                IconGlyph = "\uE950",
                Action = async (svc) =>
                {
                    using var key = Registry.LocalMachine.CreateSubKey(@"SYSTEM\CurrentControlSet\Control\PriorityControl");
                    key.SetValue("Win32PrioritySeparation", 38, RegistryValueKind.DWord);
                    svc.Log("Foreground priority separation optimized (Value: 38).", LogLevel.Success);
                    await Task.CompletedTask;
                }
            });

            tweaks.Add(new TweakItem
            {
                Title = "Kill GameDVR & Capture",
                Category = "Performance",
                CategoryTag = "Gaming Latency",
                Description = "Disables Xbox GameDVR background screen recording to eliminate micro-stuttering.",
                IconGlyph = "\uE7FC",
                IsToggle = true,
                CheckStatus = () =>
                {
                    using var key = Registry.CurrentUser.OpenSubKey(@"System\GameConfigStore");
                    return (int?)(key?.GetValue("GameDVR_Enabled")) == 0;
                },
                EnableAction = async (svc) =>
                {
                    using var key = Registry.CurrentUser.CreateSubKey(@"System\GameConfigStore");
                    key.SetValue("GameDVR_Enabled", 0, RegistryValueKind.DWord);
                    svc.Log("GameDVR recording disabled.", LogLevel.Success);
                    await Task.CompletedTask;
                },
                DisableAction = async (svc) =>
                {
                    using var key = Registry.CurrentUser.CreateSubKey(@"System\GameConfigStore");
                    key.SetValue("GameDVR_Enabled", 1, RegistryValueKind.DWord);
                    svc.Log("GameDVR recording restored.", LogLevel.Warning);
                    await Task.CompletedTask;
                }
            });

            tweaks.Add(new TweakItem
            {
                Title = "Compact OS Compression",
                Category = "Performance",
                CategoryTag = "Storage & Power",
                Description = "Toggles Windows 11 Compact OS system binary compression to save 4-8 GB SSD storage.",
                IconGlyph = "\uEDA2",
                IsToggle = true,
                CheckStatus = () =>
                {
                    try
                    {
                        var psi = new ProcessStartInfo("compact.exe", "/compactos:query")
                        {
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            CreateNoWindow = true
                        };
                        using var proc = Process.Start(psi);
                        var output = proc?.StandardOutput.ReadToEnd() ?? "";
                        proc?.WaitForExit();
                        return output.Contains("is in the compact state");
                    }
                    catch { return false; }
                },
                EnableAction = async (svc) => await svc.RunProcessAsync("compact.exe", "/compactos:always", "Enable Compact OS"),
                DisableAction = async (svc) => await svc.RunProcessAsync("compact.exe", "/compactos:never", "Disable Compact OS")
            });

            tweaks.Add(new TweakItem
            {
                Title = "Disable Hibernation",
                Category = "Performance",
                CategoryTag = "Storage & Power",
                Description = "Runs 'powercfg -h off' to eliminate hiberfil.sys and free gigabytes of drive space.",
                IconGlyph = "\uEDA2",
                IsToggle = true,
                CheckStatus = () =>
                {
                    using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Power");
                    return (int?)(key?.GetValue("HibernateEnabled")) == 0 || !File.Exists(@"C:\hiberfil.sys");
                },
                EnableAction = async (svc) => await svc.RunProcessAsync("powercfg.exe", "-h off", "Disable Hibernation"),
                DisableAction = async (svc) => await svc.RunProcessAsync("powercfg.exe", "-h on", "Enable Hibernation")
            });

            tweaks.Add(new TweakItem
            {
                Title = "Disable USB Suspend",
                Category = "Performance",
                CategoryTag = "Hardware Latency",
                Description = "Disables USB Selective Suspend to prevent disconnects on peripherals.",
                IconGlyph = "\uE7F8",
                Action = async (svc) =>
                {
                    await svc.RunProcessAsync("powercfg.exe", "/SETACVALUEINDEX SCHEME_CURRENT 2a84c312-a001-40c3-b31f-1393d254d070 48e6b7a6-50f2-4389-a784-1779c7b048db 0", "Disable USB Suspend (AC)");
                    await svc.RunProcessAsync("powercfg.exe", "/SETDCVALUEINDEX SCHEME_CURRENT 2a84c312-a001-40c3-b31f-1393d254d070 48e6b7a6-50f2-4389-a784-1779c7b048db 0", "Disable USB Suspend (DC)");
                    await svc.RunProcessAsync("powercfg.exe", "/setactive SCHEME_CURRENT", "Apply Power Scheme");
                }
            });

            tweaks.Add(new TweakItem
            {
                Title = "Auto HDR for Gaming",
                Category = "Performance",
                CategoryTag = "DirectX Gaming",
                Description = "Toggles system-wide Auto HDR for DirectX 11 and 12 games on compatible displays.",
                IconGlyph = "\uE7F4",
                IsToggle = true,
                CheckStatus = () =>
                {
                    using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Direct3D");
                    return (int?)(key?.GetValue("EnableAutoHDR")) == 1;
                },
                EnableAction = async (svc) =>
                {
                    using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Direct3D");
                    key.SetValue("EnableAutoHDR", 1, RegistryValueKind.DWord);
                    svc.Log("Auto HDR enabled.", LogLevel.Success);
                    await Task.CompletedTask;
                },
                DisableAction = async (svc) =>
                {
                    using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Direct3D");
                    key.SetValue("EnableAutoHDR", 0, RegistryValueKind.DWord);
                    svc.Log("Auto HDR disabled.", LogLevel.Warning);
                    await Task.CompletedTask;
                }
            });

            tweaks.Add(new TweakItem
            {
                Title = "Windowed Game Latency",
                Category = "Performance",
                CategoryTag = "Gaming Boost",
                Description = "Upgrades presentation model for windowed games in Windows 11 to minimize latency & enable VRR.",
                IconGlyph = "\uE7FC",
                IsToggle = true,
                CheckStatus = () =>
                {
                    using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\DirectX\UserGpuPreferences");
                    var val = key?.GetValue("DirectXUserGlobalSettings") as string ?? "";
                    return val.Contains("SwapEffectUpgradeEnable=1");
                },
                EnableAction = async (svc) =>
                {
                    using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\DirectX\UserGpuPreferences");
                    key.SetValue("DirectXUserGlobalSettings", "SwapEffectUpgradeEnable=1;", RegistryValueKind.String);
                    svc.Log("Windowed Game Optimizations enabled.", LogLevel.Success);
                    await Task.CompletedTask;
                },
                DisableAction = async (svc) =>
                {
                    try { Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\DirectX\UserGpuPreferences", true)?.DeleteValue("DirectXUserGlobalSettings"); } catch { }
                    svc.Log("Windowed Game Optimizations disabled.", LogLevel.Warning);
                    await Task.CompletedTask;
                }
            });

            tweaks.Add(new TweakItem
            {
                Title = "Core Isolation (HVCI) Audit",
                Category = "Performance",
                CategoryTag = "Security & VBS",
                Description = "Audits Virtualization-Based Security (VBS) and Hypervisor-Enforced Code Integrity (Memory Integrity).",
                IconGlyph = "\uEA18",
                Action = async (svc) =>
                {
                    var script = @"
$dg = Get-CimInstance -ClassName Win32_DeviceGuard -Namespace root\Microsoft\Windows\DeviceGuard -ErrorAction SilentlyContinue | Select-Object -First 1
if ($dg) {
    $vbsStatus = switch ($dg.VirtualizationBasedSecurityStatus) { 0 { 'Disabled' } 1 { 'Configured' } 2 { 'Running' } Default { 'Unknown' } }
    Write-Output ""VBS Security Status: $vbsStatus""
    $hvci = (Get-ItemProperty 'HKLM:\SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity' -ErrorAction SilentlyContinue).Enabled
    Write-Output ""Memory Integrity (HVCI): $(if ($hvci -eq 1) { 'Enabled' } else { 'Disabled' })""
}";
                    await svc.RunPowerShellScriptAsync(script, "Core Isolation Audit");
                }
            });
            #endregion

            #region 3. NETWORK & DNS
            tweaks.Add(new TweakItem
            {
                Title = "Reset Network Stack",
                Category = "Network & DNS",
                CategoryTag = "Network Repair",
                Description = "Performs full TCP/IP reset, Winsock catalog repair, and DNS cache flush.",
                IconGlyph = "\uE774",
                Action = async (svc) =>
                {
                    await svc.RunProcessAsync("ipconfig.exe", "/flushdns", "Flush DNS");
                    await svc.RunProcessAsync("netsh.exe", "int ip reset", "Reset IP Stack");
                    await svc.RunProcessAsync("netsh.exe", "winsock reset", "Reset Winsock");
                }
            });

            tweaks.Add(new TweakItem
            {
                Title = "Cloudflare DNS (DoH)",
                Category = "Network & DNS",
                CategoryTag = "DNS Switcher",
                Description = "Sets primary & secondary DNS to Cloudflare (1.1.1.1) with encrypted DNS-over-HTTPS.",
                IconGlyph = "\uE774",
                Action = async (svc) =>
                {
                    var script = @"
Get-NetAdapter | Where-Object { $_.Status -eq 'Up' } | ForEach-Object {
    Set-DnsClientServerAddress -InterfaceAlias $_.Name -ServerAddresses ('1.1.1.1', '1.0.0.1')
    try {
        if (Get-Command Set-DnsClientDohServerAddress -ErrorAction SilentlyContinue) {
            Set-DnsClientDohServerAddress -ServerAddress '1.1.1.1' -DohTemplate 'https://cloudflare-dns.com/dns-query' -AllowFallbackToUdp $false -AutoUpgrade $true -ErrorAction SilentlyContinue
        }
    } catch {}
    Write-Output ""Set Cloudflare DNS with DoH on: $($_.Name)""
}";
                    await svc.RunPowerShellScriptAsync(script, "Configure Cloudflare DoH");
                }
            });

            tweaks.Add(new TweakItem
            {
                Title = "Google DNS (DoH)",
                Category = "Network & DNS",
                CategoryTag = "DNS Switcher",
                Description = "Sets primary & secondary DNS to Google (8.8.8.8) with encrypted DNS-over-HTTPS.",
                IconGlyph = "\uE774",
                Action = async (svc) =>
                {
                    var script = @"
Get-NetAdapter | Where-Object { $_.Status -eq 'Up' } | ForEach-Object {
    Set-DnsClientServerAddress -InterfaceAlias $_.Name -ServerAddresses ('8.8.8.8', '8.8.4.4')
    try {
        if (Get-Command Set-DnsClientDohServerAddress -ErrorAction SilentlyContinue) {
            Set-DnsClientDohServerAddress -ServerAddress '8.8.8.8' -DohTemplate 'https://dns.google/dns-query' -AllowFallbackToUdp $false -AutoUpgrade $true -ErrorAction SilentlyContinue
        }
    } catch {}
    Write-Output ""Set Google DNS with DoH on: $($_.Name)""
}";
                    await svc.RunPowerShellScriptAsync(script, "Configure Google DoH");
                }
            });

            tweaks.Add(new TweakItem
            {
                Title = "Restore Automatic DNS",
                Category = "Network & DNS",
                CategoryTag = "DNS Reset",
                Description = "Reverts all active network interfaces to obtain DNS dynamically via DHCP.",
                IconGlyph = "\uE72C",
                Action = async (svc) =>
                {
                    var script = @"
Get-NetAdapter | Where-Object { $_.Status -eq 'Up' } | ForEach-Object {
    Set-DnsClientServerAddress -InterfaceAlias $_.Name -ResetServerAddresses
    Write-Output ""Restored DHCP DNS on: $($_.Name)""
}";
                    await svc.RunPowerShellScriptAsync(script, "Restore DHCP DNS");
                }
            });

            tweaks.Add(new TweakItem
            {
                Title = "DNS Benchmark Test",
                Category = "Network & DNS",
                CategoryTag = "Diagnostics",
                Description = "Pings Cloudflare, Google, Quad9, and OpenDNS to find lowest latency provider.",
                IconGlyph = "\uE945",
                Action = async (svc) =>
                {
                    var script = @"
@(@{ Name='Cloudflare'; IP='1.1.1.1' }, @{ Name='Google'; IP='8.8.8.8' }, @{ Name='Quad9'; IP='9.9.9.9' }, @{ Name='OpenDNS'; IP='208.67.222.222' }) | ForEach-Object {
    $test = Test-Connection -ComputerName $_.IP -Count 3 -ErrorAction SilentlyContinue
    if ($test) {
        $avg = [math]::Round(($test | Measure-Object -Property ResponseTime -Average).Average, 1)
        Write-Output ""$($_.Name) ($($_.IP)): Avg Latency = $avg ms""
    } else { Write-Output ""$($_.Name) ($($_.IP)): 100% Packet Loss"" }
}";
                    await svc.RunPowerShellScriptAsync(script, "DNS Benchmark");
                }
            });

            tweaks.Add(new TweakItem
            {
                Title = "Reveal Wi-Fi Passwords",
                Category = "Network & DNS",
                CategoryTag = "Security & Keys",
                Description = "Audits and displays all saved Wi-Fi profiles along with cleartext passwords.",
                IconGlyph = "\uE72E",
                Action = async (svc) =>
                {
                    var script = @"
$profiles = netsh wlan show profiles | Select-String 'All User Profile' | ForEach-Object { ($_ -split ':')[-1].Trim() }
foreach ($prof in $profiles) {
    $pass = netsh wlan show profile name=""$prof"" key=clear | Select-String 'Key Content' | ForEach-Object { ($_ -split ':')[-1].Trim() }
    if ($pass) { Write-Output ""SSID: '$prof' ==> Password: '$pass'"" }
    else { Write-Output ""SSID: '$prof' ==> [Open / No Key]"" }
}";
                    await svc.RunPowerShellScriptAsync(script, "Audit Wi-Fi Keys");
                }
            });

            tweaks.Add(new TweakItem
            {
                Title = "Remote Desktop (RDP)",
                Category = "Network & DNS",
                CategoryTag = "Remote Admin",
                Description = "Toggles Windows Terminal Server RDP listener and firewall exception rule.",
                IconGlyph = "\uEA18",
                IsToggle = true,
                CheckStatus = () =>
                {
                    using var key = Registry.LocalMachine.OpenSubKey(@"System\CurrentControlSet\Control\Terminal Server");
                    return (int?)(key?.GetValue("fDenyTSConnections")) == 0;
                },
                EnableAction = async (svc) =>
                {
                    using (var key = Registry.LocalMachine.CreateSubKey(@"System\CurrentControlSet\Control\Terminal Server"))
                    {
                        key.SetValue("fDenyTSConnections", 0, RegistryValueKind.DWord);
                    }
                    await svc.RunPowerShellScriptAsync("Enable-NetFirewallRule -DisplayGroup 'Remote Desktop' -ErrorAction SilentlyContinue; Enable-NetFirewallRule -Name 'RemoteDesktop*' -ErrorAction SilentlyContinue", "Enable RDP Firewall");
                    svc.Log("Remote Desktop (RDP) enabled.", LogLevel.Success);
                },
                DisableAction = async (svc) =>
                {
                    using (var key = Registry.LocalMachine.CreateSubKey(@"System\CurrentControlSet\Control\Terminal Server"))
                    {
                        key.SetValue("fDenyTSConnections", 1, RegistryValueKind.DWord);
                    }
                    await svc.RunPowerShellScriptAsync("Disable-NetFirewallRule -DisplayGroup 'Remote Desktop' -ErrorAction SilentlyContinue; Disable-NetFirewallRule -Name 'RemoteDesktop*' -ErrorAction SilentlyContinue", "Disable RDP Firewall");
                    svc.Log("Remote Desktop (RDP) disabled.", LogLevel.Warning);
                }
            });
            
            tweaks.Add(new TweakItem
            {
                Title = "Scan LAN Subnet Devices",
                Category = "Network & DNS",
                CategoryTag = "Network Discovery",
                Description = "Sweeps the local subnet and lists active IP and MAC addresses.",
                IconGlyph = "\uE7F8",
                Action = async (svc) =>
                {
                    var script = @"
arp -a | Select-String 'dynamic' | ForEach-Object {
    $parts = $_.Line.Trim() -split '\s+'
    if ($parts.Count -ge 2) { Write-Output ""Active Host: IP $($parts[0]) | MAC $($parts[1])"" }
}";
                    await svc.RunPowerShellScriptAsync(script, "Scan LAN Devices");
                }
            });

            tweaks.Add(new TweakItem
            {
                Title = "Port & Process Listeners",
                Category = "Network & DNS",
                CategoryTag = "Network Security",
                Description = "Scans active listening TCP ports and maps them to host application executables.",
                IconGlyph = "\uE7EF",
                Action = async (svc) =>
                {
                    var script = @"
$procMap = @{}
Get-Process -ErrorAction SilentlyContinue | ForEach-Object { $procMap[$_.Id] = $_.Name }
Get-NetTCPConnection -State Listen -ErrorAction SilentlyContinue | ForEach-Object {
    $pName = if ($procMap.ContainsKey($_.OwningProcess)) { $procMap[$_.OwningProcess] } else { 'System/Unknown' }
    Write-Output ""Port $($_.LocalPort) ($($_.LocalAddress)) -> $pName (PID $($_.OwningProcess))""
}";
                    await svc.RunPowerShellScriptAsync(script, "Port & Process Listeners");
                }
            });

            tweaks.Add(new TweakItem
            {
                Title = "Public IP & Geo-Location",
                Category = "Network & DNS",
                CategoryTag = "WAN Diagnostics",
                Description = "Queries external routing APIs to retrieve WAN IP, ISP, ASN, and city location.",
                IconGlyph = "\uE774",
                Action = async (svc) =>
                {
                    var script = @"
try {
    $info = Invoke-RestMethod -Uri 'https://ipinfo.io/json' -TimeoutSec 4
    Write-Output ""Public IP: $($info.ip) | ISP: $($info.org)""
    Write-Output ""Location: $($info.city), $($info.region), $($info.country)""
} catch { Write-Output 'Failed to reach IP resolution service.' }";
                    await svc.RunPowerShellScriptAsync(script, "Public IP & Geo-Location");
                }
            });

            #endregion

            #region 4. PRIVACY & BLOAT
            tweaks.Add(new TweakItem
            {
                Title = "Universal OEM Debloat",
                Category = "Privacy & Bloat",
                CategoryTag = "App Purge",
                Description = "Removes consumer bloatware (TikTok, CandyCrush, McAfee, Netflix, DevHome, etc.).",
                IconGlyph = "\uEB49",
                Action = async (svc) =>
                {
                    var script = @"
$Apps = @('*TikTok*', '*Instagram*', '*Facebook*', '*LinkedIn*', '*Twitter*', '*Spotify*', '*Netflix*', '*CandyCrush*', '*DevHome*', '*Clipchamp*')
foreach ($app in $Apps) {
    Get-AppxPackage -Name $app -AllUsers -ErrorAction SilentlyContinue | Remove-AppxPackage -AllUsers -ErrorAction SilentlyContinue
    Get-AppxProvisionedPackage -Online -ErrorAction SilentlyContinue | Where-Object { $_.DisplayName -like $app } | Remove-AppxProvisionedPackage -Online -ErrorAction SilentlyContinue
    Write-Output ""Purged: $app""
}";
                    await svc.RunPowerShellScriptAsync(script, "Universal Debloat");
                }
            });

            tweaks.Add(new TweakItem
            {
                Title = "Disable Copilot & Web Search",
                Category = "Privacy & Bloat",
                CategoryTag = "Search & AI",
                Description = "Toggles Windows Copilot, Taskbar Widgets, and Start Menu Bing Web search.",
                IconGlyph = "\uE721",
                IsToggle = true,
                CheckStatus = () =>
                {
                    using var key = Registry.CurrentUser.OpenSubKey(@"Software\Policies\Microsoft\Windows\WindowsCopilot");
                    return (int?)(key?.GetValue("TurnOffWindowsCopilot")) == 1;
                },
                EnableAction = async (svc) =>
                {
                    using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Policies\Microsoft\Windows\WindowsCopilot"))
                        key.SetValue("TurnOffWindowsCopilot", 1, RegistryValueKind.DWord);
                    using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced"))
                        key.SetValue("TaskbarDa", 0, RegistryValueKind.DWord);
                    using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Policies\Microsoft\Windows\Explorer"))
                        key.SetValue("DisableSearchBoxSuggestions", 1, RegistryValueKind.DWord);
                    await svc.RunPowerShellScriptAsync("Stop-Process -Name explorer -Force; Start-Sleep -Milliseconds 600; Start-Process explorer.exe", "Restart Explorer");
                    svc.Log("Copilot and Web search disabled.", LogLevel.Success);
                },
                DisableAction = async (svc) =>
                {
                    try { Registry.CurrentUser.DeleteSubKeyTree(@"Software\Policies\Microsoft\Windows\WindowsCopilot"); } catch { }
                    using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced"))
                        key.SetValue("TaskbarDa", 1, RegistryValueKind.DWord);
                    try { Registry.CurrentUser.CreateSubKey(@"Software\Policies\Microsoft\Windows\Explorer").DeleteValue("DisableSearchBoxSuggestions", false); } catch { }
                    await svc.RunPowerShellScriptAsync("Stop-Process -Name explorer -Force; Start-Sleep -Milliseconds 600; Start-Process explorer.exe", "Restart Explorer");
                    svc.Log("Copilot and Web search restored.", LogLevel.Warning);
                }
            });

            tweaks.Add(new TweakItem
            {
                Title = "Kill Telemetry & DiagTrack",
                Category = "Privacy & Bloat",
                CategoryTag = "Privacy",
                Description = "Toggles Connected User Experiences (DiagTrack), dmwappushservice, and telemetry.",
                IconGlyph = "\uEA18",
                IsToggle = true,
                CheckStatus = () =>
                {
                    using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\DiagTrack");
                    return (int?)(key?.GetValue("Start")) == 4;
                },
                EnableAction = async (svc) =>
                {
                    var script = @"
Stop-Service 'DiagTrack', 'dmwappushservice' -Force -ErrorAction SilentlyContinue
Set-Service 'DiagTrack', 'dmwappushservice' -StartupType Disabled -ErrorAction SilentlyContinue
reg add 'HKLM\SOFTWARE\Policies\Microsoft\Windows\DataCollection' /v AllowTelemetry /t REG_DWORD /d 0 /f | Out-Null
Write-Output 'Telemetry services disabled.'";
                    await svc.RunPowerShellScriptAsync(script, "Disable Telemetry");
                },
                DisableAction = async (svc) =>
                {
                    var script = @"
Set-Service 'DiagTrack', 'dmwappushservice' -StartupType Automatic -ErrorAction SilentlyContinue
Start-Service 'DiagTrack' -ErrorAction SilentlyContinue
reg delete 'HKLM\SOFTWARE\Policies\Microsoft\Windows\DataCollection' /v AllowTelemetry /f 2>$null | Out-Null
Write-Output 'Telemetry services enabled.'";
                    await svc.RunPowerShellScriptAsync(script, "Enable Telemetry");
                }
            });
            
            tweaks.Add(new TweakItem
            {
                Title = "Disable Recall & AI Tracking",
                Category = "Privacy & Bloat",
                CategoryTag = "Privacy",
                Description = "Disables Windows 11 Recall AI screen snapshots and background image analysis.",
                IconGlyph = "\uE72E",
                Action = async (svc) =>
                {
                    var script = @"
if (Get-WindowsOptionalFeature -Online -FeatureName 'Recall' -ErrorAction SilentlyContinue) {
    Disable-WindowsOptionalFeature -Online -FeatureName 'Recall' -Remove -NoRestart -ErrorAction SilentlyContinue | Out-Null
    Write-Output 'Windows Recall feature uninstalled.'
}
reg add 'HKLM\SOFTWARE\Policies\Microsoft\Windows\WindowsAI' /v DisableAIDataAnalysis /t REG_DWORD /d 1 /f | Out-Null
Write-Output 'Recall and AI data analysis policies disabled.'";
                    await svc.RunPowerShellScriptAsync(script, "Disable Recall & AI");
                }
            });

            tweaks.Add(new TweakItem
            {
                Title = "Disable AI Data Analysis",
                Category = "Privacy & Bloat",
                CategoryTag = "Windows 11 AI",
                Description = "Toggles system-wide model training, telemetry feedback, and diagnostic AI analysis policies.",
                IconGlyph = "\uEA18",
                IsToggle = true,
                CheckStatus = () =>
                {
                    using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows\WindowsAI");
                    return (int?)(key?.GetValue("DisableAIDataAnalysis")) == 1;
                },
                EnableAction = async (svc) =>
                {
                    using var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Windows\WindowsAI");
                    key.SetValue("DisableAIDataAnalysis", 1, RegistryValueKind.DWord);
                    svc.Log("AI Data Analysis disabled.", LogLevel.Success);
                    await Task.CompletedTask;
                },
                DisableAction = async (svc) =>
                {
                    try { Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows\WindowsAI", true)?.DeleteValue("DisableAIDataAnalysis"); } catch { }
                    svc.Log("AI Data Analysis restored.", LogLevel.Warning);
                    await Task.CompletedTask;
                }
            });

            tweaks.Add(new TweakItem
            {
                Title = "Block Telemetry in Hosts",
                Category = "Privacy & Bloat",
                CategoryTag = "Security",
                Description = "Appends known telemetry, diagnostic, and ad endpoints to hosts file (with backup).",
                IconGlyph = "\uEA18",
                Action = async (svc) =>
                {
                    var script = @"
$hosts = ""$env:SystemRoot\System32\drivers\etc\hosts""
Copy-Item $hosts ""$hosts.bak"" -Force
$domains = @('telemetry.microsoft.com', 'v10.events.data.microsoft.com', 'browser.events.data.msn.com', 'watson.telemetry.microsoft.com')
foreach ($d in $domains) {
    if (-not (Select-String -Path $hosts -Pattern $d -SimpleMatch)) {
        ""0.0.0.0 $d"" | Out-File -FilePath $hosts -Append -Encoding ASCII
        Write-Output ""Blocked host: $d""
    }
}
Write-Output 'Hosts telemetry filters updated (Backup saved as hosts.bak).' ";
                    await svc.RunPowerShellScriptAsync(script, "Block Telemetry in Hosts");
                }
            });

            tweaks.Add(new TweakItem
            {
                Title = "Lock Screen Spotlight & Ads",
                Category = "Privacy & Bloat",
                CategoryTag = "UI Cleanup",
                Description = "Toggles dynamic promotional suggestions, lockscreen tips, and feedback notifications.",
                IconGlyph = "\uE72E",
                IsToggle = true,
                CheckStatus = () =>
                {
                    using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager");
                    return (int?)(key?.GetValue("SubscribedContent-338388Enabled")) == 0;
                },
                EnableAction = async (svc) =>
                {
                    using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager");
                    key.SetValue("SubscribedContent-338388Enabled", 0, RegistryValueKind.DWord);
                    svc.Log("Lock screen promotional suggestions disabled.", LogLevel.Success);
                    await Task.CompletedTask;
                },
                DisableAction = async (svc) =>
                {
                    using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager");
                    key.SetValue("SubscribedContent-338388Enabled", 1, RegistryValueKind.DWord);
                    svc.Log("Lock screen suggestions enabled.", LogLevel.Warning);
                    await Task.CompletedTask;
                }
            });

            tweaks.Add(new TweakItem
            {
                Title = "Activity History & Timeline",
                Category = "Privacy & Bloat",
                CategoryTag = "Privacy",
                Description = "Toggles local Windows application activity tracking and cloud telemetry sync.",
                IconGlyph = "\uE7EF",
                IsToggle = true,
                CheckStatus = () =>
                {
                    using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows\System");
                    return (int?)(key?.GetValue("EnableActivityFeed")) == 0;
                },
                EnableAction = async (svc) =>
                {
                    using var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Windows\System");
                    key.SetValue("EnableActivityFeed", 0, RegistryValueKind.DWord);
                    svc.Log("Activity History disabled.", LogLevel.Success);
                    await Task.CompletedTask;
                },
                DisableAction = async (svc) =>
                {
                    using var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Windows\System");
                    key.SetValue("EnableActivityFeed", 1, RegistryValueKind.DWord);
                    svc.Log("Activity History restored.", LogLevel.Warning);
                    await Task.CompletedTask;
                }
            });

            #endregion

            #region 5. SHELL & EXPLORER
            tweaks.Add(new TweakItem
            {
                Title = "Classic Context Menu",
                Category = "Shell & Explorer",
                CategoryTag = "Context Menu",
                Description = "Restores the Windows 10 classic full context menu without needing 'Show more options'.",
                IconGlyph = "\uE8B7",
                IsToggle = true,
                CheckStatus = () =>
                {
                    using var key = Registry.CurrentUser.OpenSubKey(@"Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32");
                    return key != null;
                },
                EnableAction = async (svc) =>
                {
                    using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32"))
                    {
                        key.SetValue("", "");
                    }
                    await svc.RunPowerShellScriptAsync("Stop-Process -Name explorer -Force; Start-Sleep -Milliseconds 600; Start-Process explorer.exe", "Restart Explorer");
                    svc.Log("Classic Context Menu enabled.", LogLevel.Success);
                },
                DisableAction = async (svc) =>
                {
                    try { Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}"); } catch { }
                    await svc.RunPowerShellScriptAsync("Stop-Process -Name explorer -Force; Start-Sleep -Milliseconds 600; Start-Process explorer.exe", "Restart Explorer");
                    svc.Log("Modern Windows 11 context menu restored.", LogLevel.Warning);
                }
            });

            tweaks.Add(new TweakItem
            {
                Title = "Add 'Take Ownership'",
                Category = "Shell & Explorer",
                CategoryTag = "Context Menu",
                Description = "Adds 'Take Ownership' option on file and folder right-click context menus.",
                IconGlyph = "\uE7EF",
                IsToggle = true,
                CheckStatus = () =>
                {
                    using var key = Registry.ClassesRoot.OpenSubKey(@"*\shell\TakeOwnership");
                    return key != null;
                },
                EnableAction = async (svc) =>
                {
                    var script = @"
foreach ($p in @('HKCR:\*\shell\TakeOwnership', 'HKCR:\Directory\shell\TakeOwnership')) {
    New-Item -Path $p -Force | Out-Null
    Set-ItemProperty -Path $p -Name '(Default)' -Value 'Take Ownership'
    Set-ItemProperty -Path $p -Name 'HasLUAShield' -Value ''
    New-Item -Path ""$p\command"" -Force | Out-Null
}
Set-ItemProperty -Path 'HKCR:\*\shell\TakeOwnership\command' -Name '(Default)' -Value 'cmd.exe /c takeown /f ""%1"" && icacls ""%1"" /grant administrators:F'
Set-ItemProperty -Path 'HKCR:\Directory\shell\TakeOwnership\command' -Name '(Default)' -Value 'cmd.exe /c takeown /f ""%1"" /r /d y && icacls ""%1"" /grant administrators:F /t'
Write-Output 'Take Ownership shortcut added.'";
                    await svc.RunPowerShellScriptAsync(script, "Add Take Ownership");
                },
                DisableAction = async (svc) =>
                {
                    var script = @"Remove-Item 'HKCR:\*\shell\TakeOwnership', 'HKCR:\Directory\shell\TakeOwnership' -Recurse -Force -ErrorAction SilentlyContinue; Write-Output 'Take Ownership shortcut removed.'";
                    await svc.RunPowerShellScriptAsync(script, "Remove Take Ownership");
                }
            });

            tweaks.Add(new TweakItem
            {
                Title = "File Explorer Pro Mode",
                Category = "Shell & Explorer",
                CategoryTag = "File System",
                Description = "Shows file extensions (.exe, .txt) and unhides hidden system items.",
                IconGlyph = "\uE8B7",
                IsToggle = true,
                CheckStatus = () =>
                {
                    using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced");
                    return (int?)(key?.GetValue("HideFileExt")) == 0;
                },
                EnableAction = async (svc) =>
                {
                    using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced");
                    key.SetValue("HideFileExt", 0, RegistryValueKind.DWord);
                    key.SetValue("Hidden", 1, RegistryValueKind.DWord);
                    await svc.RunPowerShellScriptAsync("Stop-Process -Name explorer -Force; Start-Sleep -Milliseconds 600; Start-Process explorer.exe", "Restart Explorer");
                    svc.Log("File extensions and hidden files shown.", LogLevel.Success);
                },
                DisableAction = async (svc) =>
                {
                    using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced");
                    key.SetValue("HideFileExt", 1, RegistryValueKind.DWord);
                    key.SetValue("Hidden", 2, RegistryValueKind.DWord);
                    await svc.RunPowerShellScriptAsync("Stop-Process -Name explorer -Force; Start-Sleep -Milliseconds 600; Start-Process explorer.exe", "Restart Explorer");
                    svc.Log("File Explorer restored to default hidden view.", LogLevel.Warning);
                }
            });

            tweaks.Add(new TweakItem
            {
                Title = "Taskbar Align Left",
                Category = "Shell & Explorer",
                CategoryTag = "Taskbar Layout",
                Description = "Toggles Windows 11 taskbar icons between standard Center alignment and classic Left alignment.",
                IconGlyph = "\uE8B7",
                IsToggle = true,
                CheckStatus = () =>
                {
                    using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced");
                    return (int?)(key?.GetValue("TaskbarAl")) == 0;
                },
                EnableAction = async (svc) =>
                {
                    using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced");
                    key.SetValue("TaskbarAl", 0, RegistryValueKind.DWord);
                    await svc.RunPowerShellScriptAsync("Stop-Process -Name explorer -Force; Start-Sleep -Milliseconds 600; Start-Process explorer.exe", "Restart Explorer");
                    svc.Log("Taskbar aligned to left.", LogLevel.Success);
                },
                DisableAction = async (svc) =>
                {
                    using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced");
                    key.SetValue("TaskbarAl", 1, RegistryValueKind.DWord);
                    await svc.RunPowerShellScriptAsync("Stop-Process -Name explorer -Force; Start-Sleep -Milliseconds 600; Start-Process explorer.exe", "Restart Explorer");
                    svc.Log("Taskbar aligned to center.", LogLevel.Warning);
                }
            });
            
            tweaks.Add(new TweakItem
            {
                Title = "Add 'PowerShell Admin Here'",
                Category = "Shell & Explorer",
                CategoryTag = "Context Menu",
                Description = "Adds an 'Open PowerShell as Administrator' shortcut on background folder clicks.",
                IconGlyph = "\uE7EF",
                IsToggle = true,
                CheckStatus = () =>
                {
                    using var key = Registry.ClassesRoot.OpenSubKey(@"Directory\Background\shell\OpenElevatedPS");
                    return key != null;
                },
                EnableAction = async (svc) =>
                {
                    var script = @"
$regPath = 'HKCR:\Directory\Background\shell\OpenElevatedPS'
New-Item -Path $regPath -Force | Out-Null
Set-ItemProperty -Path $regPath -Name '(Default)' -Value 'Open PowerShell As Admin Here'
Set-ItemProperty -Path $regPath -Name 'Icon' -Value 'powershell.exe'
New-Item -Path ""$regPath\command"" -Force | Out-Null
Set-ItemProperty -Path ""$regPath\command"" -Name '(Default)' -Value 'powershell.exe -Command ""Start-Process powershell -Verb RunAs -WorkingDirectory ''%V''""'
Write-Output ""'Open PowerShell As Admin Here' added.""";
                    await svc.RunPowerShellScriptAsync(script, "Add PowerShell Admin Shortcut");
                },
                DisableAction = async (svc) =>
                {
                    var script = @"Remove-Item 'HKCR:\Directory\Background\shell\OpenElevatedPS' -Recurse -Force -ErrorAction SilentlyContinue; Write-Output 'Shortcut removed.'";
                    await svc.RunPowerShellScriptAsync(script, "Remove PowerShell Admin Shortcut");
                }
            });

            tweaks.Add(new TweakItem
            {
                Title = "Explorer Compact View",
                Category = "Shell & Explorer",
                CategoryTag = "File Explorer",
                Description = "Toggles dense compact folder row spacing in Windows 11 File Explorer.",
                IconGlyph = "\uE8B7",
                IsToggle = true,
                CheckStatus = () =>
                {
                    using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced");
                    return (int?)(key?.GetValue("UseCompactMode")) == 1;
                },
                EnableAction = async (svc) =>
                {
                    using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced");
                    key.SetValue("UseCompactMode", 1, RegistryValueKind.DWord);
                    await svc.RunPowerShellScriptAsync("Stop-Process -Name explorer -Force; Start-Sleep -Milliseconds 600; Start-Process explorer.exe", "Restart Explorer");
                    svc.Log("Compact mode enabled in File Explorer.", LogLevel.Success);
                },
                DisableAction = async (svc) =>
                {
                    using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced");
                    key.SetValue("UseCompactMode", 0, RegistryValueKind.DWord);
                    await svc.RunPowerShellScriptAsync("Stop-Process -Name explorer -Force; Start-Sleep -Milliseconds 600; Start-Process explorer.exe", "Restart Explorer");
                    svc.Log("Compact mode disabled.", LogLevel.Warning);
                }
            });

            tweaks.Add(new TweakItem
            {
                Title = "Taskbar Never Combine",
                Category = "Shell & Explorer",
                CategoryTag = "Taskbar Behavior",
                Description = "Shows individual window labels on the taskbar without combining identical application icons.",
                IconGlyph = "\uE8B7",
                IsToggle = true,
                CheckStatus = () =>
                {
                    using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced");
                    return (int?)(key?.GetValue("TaskbarGlomLevel")) == 2;
                },
                EnableAction = async (svc) =>
                {
                    using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced");
                    key.SetValue("TaskbarGlomLevel", 2, RegistryValueKind.DWord);
                    await svc.RunPowerShellScriptAsync("Stop-Process -Name explorer -Force; Start-Sleep -Milliseconds 600; Start-Process explorer.exe", "Restart Explorer");
                    svc.Log("Taskbar never combine enabled.", LogLevel.Success);
                },
                DisableAction = async (svc) =>
                {
                    using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced");
                    key.SetValue("TaskbarGlomLevel", 0, RegistryValueKind.DWord);
                    await svc.RunPowerShellScriptAsync("Stop-Process -Name explorer -Force; Start-Sleep -Milliseconds 600; Start-Process explorer.exe", "Restart Explorer");
                    svc.Log("Taskbar combine restored to default.", LogLevel.Warning);
                }
            });

            tweaks.Add(new TweakItem
            {
                Title = "Hide Start Recommendations",
                Category = "Shell & Explorer",
                CategoryTag = "Start Menu",
                Description = "Hides recommended recent files, newly installed app suggestions, and tips in the Start Menu.",
                IconGlyph = "\uE8B7",
                IsToggle = true,
                CheckStatus = () =>
                {
                    using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows\Explorer");
                    return (int?)(key?.GetValue("HideRecommendedSection")) == 1;
                },
                EnableAction = async (svc) =>
                {
                    using var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Windows\Explorer");
                    key.SetValue("HideRecommendedSection", 1, RegistryValueKind.DWord);
                    await svc.RunPowerShellScriptAsync("Stop-Process -Name explorer -Force; Start-Sleep -Milliseconds 600; Start-Process explorer.exe", "Restart Explorer");
                    svc.Log("Start Menu recommendations hidden.", LogLevel.Success);
                },
                DisableAction = async (svc) =>
                {
                    try { Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows\Explorer", true)?.DeleteValue("HideRecommendedSection"); } catch { }
                    await svc.RunPowerShellScriptAsync("Stop-Process -Name explorer -Force; Start-Sleep -Milliseconds 600; Start-Process explorer.exe", "Restart Explorer");
                    svc.Log("Start Menu recommendations restored.", LogLevel.Warning);
                }
            });

            #endregion

            #region 6. HARDWARE AUDIT
            tweaks.Add(new TweakItem
            {
                Title = "SMART Disk Health",
                Category = "Hardware",
                CategoryTag = "Storage Health",
                Description = "Audits physical drives, media type (NVMe/SSD/HDD), and SMART health statuses.",
                IconGlyph = "\uEDA2",
                Action = async (svc) =>
                {
                    var script = @"
Get-PhysicalDisk -ErrorAction SilentlyContinue | ForEach-Object {
    Write-Output ""Disk #$($_.DeviceId) ($($_.FriendlyName)): Health=$($_.HealthStatus) | MediaType=$($_.MediaType)""
}";
                    await svc.RunPowerShellScriptAsync(script, "SMART Health Audit");
                }
            });

            tweaks.Add(new TweakItem
            {
                Title = "RAM Bank & Slot Audit",
                Category = "Hardware",
                CategoryTag = "Memory Specs",
                Description = "Inspects physical RAM slots, module capacities, clock speeds, and manufacturers.",
                IconGlyph = "\uE7B8",
                Action = async (svc) =>
                {
                    var script = @"
$sticks = Get-CimInstance Win32_PhysicalMemory
$tot = [math]::Round(($sticks | Measure-Object -Property Capacity -Sum).Sum / 1GB, 2)
Write-Output ""Total Installed Memory: $tot GB across $($sticks.Count) slots:""
foreach ($s in $sticks) {
    $gb = [math]::Round($s.Capacity / 1GB, 2)
    Write-Output ""Slot $($s.BankLabel): $gb GB @ $($s.Speed) MHz ($($s.Manufacturer))""
}";
                    await svc.RunPowerShellScriptAsync(script, "RAM Specs Audit");
                }
            });

            tweaks.Add(new TweakItem
            {
                Title = "Battery Health Report",
                Category = "Hardware",
                CategoryTag = "Power Report",
                Description = "Generates a detailed HTML battery capacity and degradation report on Desktop.",
                IconGlyph = "\uE945",
                Action = async (svc) =>
                {
                    var desk = ProfileService.GetDesktopPath();
                    var p = Path.Combine(desk, "BatteryReport.html");
                    await svc.RunProcessAsync("powercfg.exe", $"/batteryreport /output \"{p}\"", "Battery Report");
                    svc.Log($"Battery report generated at: {p}", LogLevel.Success);
                }
            });

            tweaks.Add(new TweakItem
            {
                Title = "GPU & Display Audit",
                Category = "Hardware",
                CategoryTag = "Graphics Specs",
                Description = "Inspects installed GPU adapters, driver versions, and display resolutions.",
                IconGlyph = "\uE7F8",
                Action = async (svc) =>
                {
                    var script = @"
Get-CimInstance Win32_VideoController | ForEach-Object {
    Write-Output ""GPU: $($_.Name) - Driver: $($_.DriverVersion) - Res: $($_.CurrentHorizontalResolution)x$($_.CurrentVerticalResolution) @ $($_.CurrentRefreshRate)Hz""
}";
                    await svc.RunPowerShellScriptAsync(script, "GPU Specs Audit");
                }
            });

            tweaks.Add(new TweakItem
            {
                Title = "Motherboard & BIOS Audit",
                Category = "Hardware",
                CategoryTag = "Firmware Specs",
                Description = "Retrieves baseboard manufacturer, model, BIOS/UEFI version, and Secure Boot status.",
                IconGlyph = "\uE7EF",
                Action = async (svc) =>
                {
                    var script = @"
$bb = Get-CimInstance Win32_BaseBoard; $bios = Get-CimInstance Win32_BIOS
$sb = try { (Confirm-SecureBootUEFI) } catch { 'Unsupported/Legacy' }
Write-Output ""Motherboard: $($bb.Manufacturer) $($bb.Product)""
Write-Output ""BIOS: $($bios.SMBIOSBIOSVersion)""
Write-Output ""Secure Boot: $sb""";
                    await svc.RunPowerShellScriptAsync(script, "BIOS Audit");
                }
            });

            tweaks.Add(new TweakItem
            {
                Title = "TPM 2.0 & Platform Security",
                Category = "Hardware",
                CategoryTag = "Security Hardware",
                Description = "Audits Trusted Platform Module (TPM 2.0) chip presence and Secure Boot status.",
                IconGlyph = "\uEA18",
                Action = async (svc) =>
                {
                    var script = @"
$tpm = Get-Tpm -ErrorAction SilentlyContinue
if ($tpm) {
    Write-Output ""TPM Present: $($tpm.TpmPresent) | Ready: $($tpm.TpmReady) | Enabled: $($tpm.TpmEnabled)""
}
$sb = try { Confirm-SecureBootUEFI } catch { 'Not Supported' }
Write-Output ""UEFI Secure Boot Status: $sb""";
                    await svc.RunPowerShellScriptAsync(script, "TPM Security Audit");
                }
            });

            tweaks.Add(new TweakItem
            {
                Title = "DirectStorage BypassIO Audit",
                Category = "Hardware",
                CategoryTag = "Storage Architecture",
                Description = "Inspects Windows 11 BypassIO status on System Drive (C:) for ultra-fast NVMe game loading.",
                IconGlyph = "\uEDA2",
                Action = async (svc) =>
                {
                    await svc.RunProcessAsync("fsutil.exe", "bypassIo state C:", "BypassIO State");
                }
            });
            
            tweaks.Add(new TweakItem
            {
                Title = "CPU Virtualization Audit",
                Category = "Hardware",
                CategoryTag = "CPU Topology",
                Description = "Checks hardware virtualization flags (VT-x / AMD-V), Hyper-V status, and core topology.",
                IconGlyph = "\uE950",
                Action = async (svc) =>
                {
                    var script = @"
$proc = Get-CimInstance Win32_Processor | Select-Object -First 1
Write-Output ""Processor: $($proc.Name)""
Write-Output ""Cores: $($proc.NumberOfCores) | Threads: $($proc.NumberOfLogicalProcessors) | Max Clock: $($proc.MaxClockSpeed) MHz""
Write-Output ""Firmware Virtualization Enabled: $($proc.VirtualizationFirmwareEnabled)""";
                    await svc.RunPowerShellScriptAsync(script, "CPU Virtualization Audit");
                }
            });

            tweaks.Add(new TweakItem
            {
                Title = "Disk Sector & Partition Audit",
                Category = "Hardware",
                CategoryTag = "Drive Specs",
                Description = "Audits physical sector sizes (4Kn vs 512e) and partition tables per disk.",
                IconGlyph = "\uEDA2",
                Action = async (svc) =>
                {
                    var script = @"
Get-Disk | ForEach-Object {
    Write-Output ""Disk #$($_.Number): $($_.FriendlyName) | Style: $($_.PartitionStyle) | SectorSize: $($_.PhysicalSectorSize)B""
}";
                    await svc.RunPowerShellScriptAsync(script, "Storage Geometry Audit");
                }
            });

            #endregion

            #region 7. SOFTWARE HUB
            tweaks.Add(new TweakItem
            {
                Title = "Install / Repair Winget",
                Category = "Software Hub",
                CategoryTag = "Package Manager",
                Description = "Downloads and forces installation of the latest Microsoft App Installer (Winget).",
                IconGlyph = "\uEB49",
                Action = async (svc) =>
                {
                    var script = @"
$file = ""$env:TEMP\winget.msixbundle""
Invoke-WebRequest -Uri 'https://github.com/microsoft/winget-cli/releases/latest/download/Microsoft.DesktopAppInstaller_8wekyb3d8bbwe.msixbundle' -OutFile $file -UseBasicParsing
Add-AppxPackage -Path $file -ForceUpdateFromAnyVersion
Write-Output 'Winget successfully installed/repaired.'";
                    await svc.RunPowerShellScriptAsync(script, "Install Winget");
                }
            });

            tweaks.Add(new TweakItem
            {
                Title = "Winget Upgrade All Apps",
                Category = "Software Hub",
                CategoryTag = "Package Manager",
                Description = "Runs 'winget upgrade --all' to synchronize all installed software packages.",
                IconGlyph = "\uE72C",
                Action = async (svc) =>
                {
                    await svc.RunProcessAsync("winget.exe", "upgrade --all --include-unknown --accept-package-agreements --accept-source-agreements --disable-interactivity", "Winget Upgrade All");
                }
            });

            var apps = new[]
            {
                ("WinToys", "9P8LTPGCBZXD", "msstore", "Optimization"),
                ("VLC Media Player", "VideoLAN.VLC", null, "Media Player"),
                ("Sumatra PDF", "SumatraPDF.SumatraPDF", null, "Productivity"),
                ("PowerToys", "Microsoft.PowerToys", null, "Essential Tools"),
                ("7-Zip", "7zip.7zip", null, "Compression Utility"),
                ("Sysinternals Suite", "Microsoft.SysinternalsSuite", null, "SysAdmin Tools")
            };

            foreach (var (appName, appId, source, cat) in apps)
            {
                var srcArg = source != null ? $"--source {source}" : "";
                tweaks.Add(new TweakItem
                {
                    Title = $"Install {appName}",
                    Category = "Software Hub",
                    CategoryTag = cat,
                    Description = $"Installs {appName} silently via Winget package manager.",
                    IconGlyph = "\uEB49",
                    Action = async (svc) =>
                    {
                        await svc.RunProcessAsync("winget.exe", $"install {appId} --silent --accept-package-agreements --accept-source-agreements --disable-interactivity {srcArg}".Trim(), $"Install {appName}");
                    }
                });
            }
            
            tweaks.Add(new TweakItem
            {
                Title = "Install Developer Bundle",
                Category = "Software Hub",
                CategoryTag = "Winget Bundle",
                Description = "Installs Git, VS Code, Windows Terminal, and PowerShell 7 in one batch.",
                IconGlyph = "\uE7EF",
                Action = async (svc) =>
                {
                    var script = @"
@('Git.Git', 'Microsoft.VisualStudioCode', 'Microsoft.WindowsTerminal', 'Microsoft.PowerShell') | ForEach-Object {
    Write-Output ""Installing: $_...""
    winget install $_ --silent --accept-package-agreements --accept-source-agreements --disable-interactivity
}";
                    await svc.RunPowerShellScriptAsync(script, "Install Developer Bundle");
                }
            });

            tweaks.Add(new TweakItem
            {
                Title = "Install SysAdmin Bundle",
                Category = "Software Hub",
                CategoryTag = "Winget Bundle",
                Description = "Installs Wireshark, Nmap, PuTTY, and System Informer in one batch.",
                IconGlyph = "\uEA18",
                Action = async (svc) =>
                {
                    var script = @"
@('WiresharkFoundation.Wireshark', 'Insecure.Nmap', 'PuTTY.PuTTY', 'Winsiderss.SystemInformer') | ForEach-Object {
    Write-Output ""Installing: $_...""
    winget install $_ --silent --accept-package-agreements --accept-source-agreements --disable-interactivity
}";
                    await svc.RunPowerShellScriptAsync(script, "Install SysAdmin Bundle");
                }
            });

            tweaks.Add(new TweakItem
            {
                Title = "Install / Update WSL 2",
                Category = "Software Hub",
                CategoryTag = "Linux Subsystem",
                Description = "Installs or updates the Windows Subsystem for Linux (WSL 2) kernel package directly.",
                IconGlyph = "\uE7EF",
                Action = async (svc) =>
                {
                    await svc.RunProcessAsync("wsl.exe", "--update", "Update WSL 2 Kernel");
                    await svc.RunProcessAsync("wsl.exe", "--status", "WSL 2 Status Check");
                }
            });

            #endregion

            #region 8. ADMIN UTILITIES
            tweaks.Add(new TweakItem
            {
                Title = "Create System Restore Point",
                Category = "Admin Utilities",
                CategoryTag = "Safety Checkpoint",
                Description = "Generates a fresh Windows System Restore point named 'AdminWorks_Checkpoint'.",
                IconGlyph = "\uEA18",
                Action = async (svc) =>
                {
                    var script = @"
Enable-ComputerRestore -Drive 'C:\' -ErrorAction SilentlyContinue
reg add 'HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore' /v SystemRestorePointCreationFrequency /t REG_DWORD /d 0 /f 2>$null | Out-Null
Checkpoint-Computer -Description 'AdminWorks_Checkpoint' -RestorePointType 'MODIFY_SETTINGS' -ErrorAction Stop
Write-Output 'System Restore Point created successfully.'";
                    await svc.RunPowerShellScriptAsync(script, "Create Restore Point");
                }
            });

            tweaks.Add(new TweakItem
            {
                Title = "Create GodMode Shortcut",
                Category = "Admin Utilities",
                CategoryTag = "Master Control",
                Description = "Creates a master GodMode folder on Desktop linking to 200+ system applets.",
                IconGlyph = "\uE7EF",
                Action = async (svc) =>
                {
                    var desk = ProfileService.GetDesktopPath();
                    var p = Path.Combine(desk, "GodMode.{ED7BA470-8E54-465E-825C-99712043E01C}");
                    if (!Directory.Exists(p))
                    {
                        Directory.CreateDirectory(p);
                        svc.Log("GodMode shortcut created on Desktop.", LogLevel.Success);
                    }
                    else
                    {
                        svc.Log("GodMode shortcut already exists.", LogLevel.Warning);
                    }
                    await Task.CompletedTask;
                }
            });

            var launchers = new[]
            {
                ("Windows Update Settings", "ms-settings:windowsupdate", "Quick Launcher", "\uE72C"),
                ("Installed Apps Settings", "ms-settings:appsfeatures", "Quick Launcher", "\uEB49"),
                ("Advanced Network Settings", "ms-settings:network-advancedsettings", "Quick Launcher", "\uE774"),
                ("Privacy & Security Settings", "ms-settings:privacy", "Quick Launcher", "\uE72E"),
                ("Network Connections (NCPA)", "ncpa.cpl", "Quick Launcher", "\uE774"),
                ("Device Manager", "devmgmt.msc", "Quick Launcher", "\uE7F8"),
                ("Services Console", "services.msc", "Quick Launcher", "\uE7EF"),
                ("Event Viewer", "eventvwr.msc", "Quick Launcher", "\uE7EF"),
                ("Advanced Firewall", "wf.msc", "Quick Launcher", "\uEA18")
            };

            foreach (var (title, target, tag, glyph) in launchers)
            {
                tweaks.Add(new TweakItem
                {
                    Title = title,
                    Category = "Admin Utilities",
                    CategoryTag = tag,
                    Description = $"Direct launcher for {title}.",
                    IconGlyph = glyph,
                    Action = async (svc) =>
                    {
                        Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
                        svc.Log($"{title} launched.", LogLevel.Success);
                        await Task.CompletedTask;
                    }
                });
            }

            tweaks.Add(new TweakItem
            {
                Title = "Defender Quick Scan",
                Category = "Admin Utilities",
                CategoryTag = "Antivirus",
                Description = "Updates signature definitions and triggers a Microsoft Defender quick scan.",
                IconGlyph = "\uEA18",
                Action = async (svc) =>
                {
                    await svc.RunPowerShellScriptAsync("Update-MpSignature -ErrorAction SilentlyContinue; Start-MpScan -ScanType QuickScan; Write-Output 'Defender quick scan initiated.'", "Defender Quick Scan");
                }
            });

            tweaks.Add(new TweakItem
            {
                Title = "Export Tweak Profile",
                Category = "Admin Utilities",
                CategoryTag = "Profile Manager",
                Description = "Exports all current toggle tweak configurations to a JSON profile on Desktop.",
                IconGlyph = "\uE7EF",
                Action = async (svc) =>
                {
                    var path = await profileService.ExportProfileAsync(tweaks);
                    svc.Log($"Tweak profile successfully exported to: {path}", LogLevel.Success);
                }
            });
            #endregion

            
            tweaks.Add(new TweakItem
            {
                Title = "Audit Local Administrators",
                Category = "Admin Utilities",
                CategoryTag = "Security Audit",
                Description = "Lists all members of the local Administrators group for unauthorized access.",
                IconGlyph = "\uE72E",
                Action = async (svc) =>
                {
                    var script = @"
Get-LocalGroupMember -Group 'Administrators' | ForEach-Object {
    Write-Output ""Admin Member: $($_.Name) ($($_.PrincipalSource))""
}";
                    await svc.RunPowerShellScriptAsync(script, "Audit Local Admins");
                }
            });

            tweaks.Add(new TweakItem
            {
                Title = "Audit Active SMB Shares",
                Category = "Admin Utilities",
                CategoryTag = "Security Audit",
                Description = "Audits all active network shared folders, admin shares, and paths.",
                IconGlyph = "\uE774",
                Action = async (svc) =>
                {
                    var script = @"
Get-SmbShare | ForEach-Object {
    Write-Output ""Share '$($_.Name)' -> Path: $($_.Path) [Type: $($_.ShareType)]""
}";
                    await svc.RunPowerShellScriptAsync(script, "Audit SMB Shares");
                }
            });

            tweaks.Add(new TweakItem
            {
                Title = "Windows License Audit",
                Category = "Admin Utilities",
                CategoryTag = "License Status",
                Description = "Checks Windows digital licensing, product keys, and activation status.",
                IconGlyph = "\uEA18",
                Action = async (svc) =>
                {
                    var script = @"
$lic = Get-CimInstance SoftwareLicensingProduct -Filter ""ApplicationId = '55c92734-d682-4d71-983e-d6ec3f16059f' and PartialProductKey IS NOT NULL"" -ErrorAction SilentlyContinue | Select-Object -First 1
if ($lic) {
    $st = switch ($lic.LicenseStatus) { 1 { 'Licensed' } 2 { 'OOB Grace' } 3 { 'OOT Grace' } 4 { 'Non-Genuine' } 5 { 'Notification' } Default { 'Unknown' } }
    Write-Output ""Product: $($lic.Name)""
    Write-Output ""License Status: $st (Channel: $($lic.Description))""
} else { Write-Output 'Unable to retrieve licensing details.' }";
                    await svc.RunPowerShellScriptAsync(script, "Windows License Audit");
                }
            });

            tweaks.Add(new TweakItem
            {
                Title = "Import Tweak Profile",
                Category = "Admin Utilities",
                CategoryTag = "Profile Manager",
                Description = "Imports latest AdminWorks_Profile_*.json from Desktop and synchronizes toggle states.",
                IconGlyph = "\uE72C",
                Action = async (svc) =>
                {
                    var desk = ProfileService.GetDesktopPath();
                    var files = Directory.GetFiles(desk, "AdminWorks_Profile_*.json").OrderByDescending(File.GetLastWriteTime).ToList();
                    if (files.Count == 0)
                    {
                        svc.Log("No AdminWorks_Profile_*.json files found on Desktop.", LogLevel.Warning);
                        return;
                    }
                    var latest = files[0];
                    svc.Log($"Importing profile: {Path.GetFileName(latest)}...", LogLevel.Exec);
                    var dict = await profileService.LoadProfileAsync(latest);
                    if (dict != null)
                    {
                        foreach (var (title, desiredActive) in dict)
                        {
                            var targetTweak = tweaks.FirstOrDefault(t => t.Title.Equals(title, StringComparison.OrdinalIgnoreCase) && t.IsToggle);
                            if (targetTweak != null && targetTweak.IsActive != desiredActive)
                            {
                                await svc.ExecuteTweakAsync(targetTweak, desiredActive);
                            }
                        }
                        svc.Log("Profile import complete.", LogLevel.Success);
                    }
                }
            });

            return tweaks;
        }
    }
}
