using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace FikaServerSetupWizard;

static class Installer
{
    // HTTP
    static readonly HttpClient Http =
        new(new HttpClientHandler { AllowAutoRedirect = true })
        { Timeout = TimeSpan.FromMinutes(15) };

    static Installer()
    {
        Http.DefaultRequestHeaders.TryAddWithoutValidation(
            "User-Agent", "FikaServerSetupWizard/1.0");
    }


    //  PATH MODEL  (SPT 4.1+)
    //
    //  <gameRoot>\                    EscapeFromTarkov.exe, winhttp.dll
    //    ├─ BepInEx\plugins\Fika\     client plugin  (Fika.Core.dll)
    //    └─ SPT_Runtime\              SPT.Server.exe, SPT_Data\, user\mods\
    //
    //  Config.SptDir always points at the folder holding SPT.Server.exe – that is
    //  SPT_Runtime on 4.1+, and the install root on 4.0 and earlier. Fika archives
    //  ship paths relative to <gameRoot> and prefix server content with the runtime
    //  folder name, so both roots are needed when extracting.
    static readonly string[] RuntimeFolderNames = { "SPT_Runtime", "SPT" };

    static string GameRoot(string? sptDir)
    {
        if (string.IsNullOrEmpty(sptDir)) return "";
        var dir    = sptDir.TrimEnd(Path.DirectorySeparatorChar,
                                    Path.AltDirectorySeparatorChar);
        var parent = Path.GetDirectoryName(dir);

        // Probe the parent for client files instead of matching on the folder
        // name – the runtime folder can be renamed, and a 4.0-era install named
        // "SPT" sits at the root rather than one level down.
        if (!string.IsNullOrEmpty(parent) && Directory.Exists(parent)
            && (File.Exists(Path.Combine(parent, "EscapeFromTarkov.exe"))
             || Directory.Exists(Path.Combine(parent, "BepInEx"))))
            return parent;

        return dir;
    }

    // Accepts either the game root or the runtime folder and returns whichever
    // one actually holds SPT.Server.exe.
    public static string? ResolveSptDir(string? candidate)
    {
        if (string.IsNullOrEmpty(candidate)) return null;
        if (SptExists(candidate)) return candidate;

        foreach (var rt in RuntimeFolderNames)
        {
            var p = Path.Combine(candidate, rt);
            if (SptExists(p)) return p;
        }
        return null;
    }

    // Extracts a Fika release archive, re-rooting "SPT_Runtime\…" / "SPT\…"
    // entries onto the detected runtime folder and everything else (BepInEx,
    // FikaHeadlessManager.exe) onto the game root.
    static void ExtractFikaArchive(OperationContext ctx, string zipPath)
    {
        var runtimeDir = ctx.Config.SptDir;
        var gameRoot   = GameRoot(runtimeDir);

        using var zip = ZipFile.OpenRead(zipPath);
        foreach (var entry in zip.Entries)
        {
            var rel  = entry.FullName.Replace('/', '\\');
            var root = gameRoot;

            foreach (var rt in RuntimeFolderNames)
            {
                if (rel.StartsWith(rt + "\\", StringComparison.OrdinalIgnoreCase))
                {
                    rel  = rel[(rt.Length + 1)..];
                    root = runtimeDir;
                    break;
                }
            }

            if (rel.Length == 0) continue;

            var rootFull = Path.GetFullPath(root)
                .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var dest     = Path.GetFullPath(Path.Combine(root, rel));

            if (!dest.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
            {
                ctx.Log($"  Skipped entry outside target: {entry.FullName}", "W");
                continue;
            }

            if (entry.Name.Length == 0)          // directory entry
            {
                Directory.CreateDirectory(dest);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            entry.ExtractToFile(dest, overwrite: true);
        }
    }


    //  CHECK ALL
    public static void OpCheckAll(OperationContext ctx)
    {
        ctx.Log("Running system checks …", "S");
        CheckSteam(ctx);
        CheckEFT(ctx);
        CheckSPT(ctx);
        CheckFika(ctx);
        CheckHeadless(ctx);
        CheckDockerAndWsl(ctx);
        CheckFirewall(ctx);
        CheckWebApp(ctx);
        ctx.Log("=== CHECK COMPLETE ===", "O");
    }

    
    //  STEAM CHECK
    static void CheckSteam(OperationContext ctx)
    {
        ctx.NotifyStatus("Steam", 1, "Checking Steam …");
        var path  = SteamInstallPath();
        bool found = !string.IsNullOrEmpty(path);
        ctx.NotifyStatus("Steam", found ? 2 : 4,
            found ? $"Steam found: {path}" : "Steam not found.");
        ctx.SetBadge("Steam", found ? 2 : 4);
    }

    
    //  EFT CHECK
    static void CheckEFT(OperationContext ctx)
    {
        ctx.NotifyStatus("EFT", 1, "Checking EFT …");
        if (!EftExists(ctx.Config.EftDir))
        {
            var found = FindEftPath(ctx);
            if (found != null) ctx.UpdateEftDir(found);
        }
        bool ok = EftExists(ctx.Config.EftDir);
        ctx.NotifyStatus("EFT", ok ? 2 : 4,
            ok  ? $"EFT found: {ctx.Config.EftDir}"
                : "EFT not found.");
        ctx.SetBadge("EFT", ok ? 2 : 4);
    }

    
    //  WAIT FOR EFT
    static bool WaitForEFT(OperationContext ctx)
    {
        string method  = ctx.Config.EftMethod ?? "BSG";
        string titleK  = "eft_wait_title";
        string msgKey  = method == "Steam"
            ? "eft_wait_msg_steam"
            : "eft_wait_msg_bsg";

        ctx.Log("──────────────────────────────────────────────", "S");
        ctx.Log(Translations.T("eft_wait_log"), "S");
        ctx.Log("──────────────────────────────────────────────", "S");

        // BLOCKING:
        ctx.ShowBlockingOkDialog(
            Translations.T(titleK),
            Translations.T(msgKey));

        // Verify after OK
        ctx.Log("Verifying EFT after user confirmation …", "S");
        ctx.NotifyStatus("EFT", 1, "Verifying EFT …");

        // Quick check first
        if (!EftExists(ctx.Config.EftDir))
        {
            ctx.Log("Quick check failed – running full scan …", "S");
            var found = FindEftPath(ctx);
            if (found != null) ctx.UpdateEftDir(found);
        }

        bool ok = EftExists(ctx.Config.EftDir);

        if (ok)
        {
            ctx.Log($"[OK]  EFT verified: {ctx.Config.EftDir}", "O");
            ctx.NotifyStatus("EFT", 2, $"EFT verified: {ctx.Config.EftDir}");
        }
        else
        {
            ctx.Log(
                "[!!]  EFT still not detected after confirmation.\r\n" +
                "      Please set the path manually in Settings.", "W");
            ctx.NotifyStatus("EFT", 4,
                "EFT not found – set path in Settings.");
        }

        return ok;
    }

    
    //  WSL2 HELPERS
    static (bool installed, bool isV2) CheckWsl(OperationContext ctx)
    {
        ctx.Log(Translations.T("wsl_checking"), "S");

        // wsl --status
        try
        {
            var pi = new ProcessStartInfo("wsl.exe", "--status")
            {
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false,
                CreateNoWindow         = true,
                StandardOutputEncoding = Encoding.Unicode,
                StandardErrorEncoding  = Encoding.Unicode,
            };

            using var proc  = Process.Start(pi);
            string stdout   = proc?.StandardOutput.ReadToEnd() ?? "";
            string stderr   = proc?.StandardError.ReadToEnd()  ?? "";
            proc?.WaitForExit();

            string combined = stdout + stderr;

            ctx.Log($"WSL --status output: {combined.Trim()
                .Replace("\r\n", " | ").Replace("\n", " | ")}", "S");

            if (string.IsNullOrWhiteSpace(combined)
                && proc?.ExitCode != 0)
            {
                ctx.Log("WSL not installed or not available.", "W");
                return (false, false);
            }

            // Detect
            bool isV2 = IsWslVersion2(combined);
            ctx.Log(isV2
                ? Translations.T("wsl_found") + " (v2)"
                : "WSL found but version unclear – assuming v1.", "S");
            return (true, isV2);
        }
        catch (Exception ex)
        {
            ctx.Log($"wsl --status error: {ex.Message}", "W");
        }

        // Fallback: wsl
        try
        {
            var pi = new ProcessStartInfo("wsl.exe", "--list --verbose")
            {
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false,
                CreateNoWindow         = true,
                StandardOutputEncoding = Encoding.Unicode,
                StandardErrorEncoding  = Encoding.Unicode,
            };

            using var proc = Process.Start(pi);
            string output  = proc?.StandardOutput.ReadToEnd() ?? "";
            output        += proc?.StandardError.ReadToEnd()  ?? "";
            proc?.WaitForExit();

            bool hasDistro = !string.IsNullOrWhiteSpace(output)
                && !output.Contains("no installed distribution",
                    StringComparison.OrdinalIgnoreCase)
                && !output.Contains("keine installierte",
                    StringComparison.OrdinalIgnoreCase);

            bool isV2 = IsWslVersion2(output);
            ctx.Log(hasDistro
                ? $"WSL distros found (v2={isV2})."
                : "No WSL distributions found.", "S");
            return (hasDistro, isV2);
        }
        catch (Exception ex)
        {
            ctx.Log($"wsl --list error: {ex.Message}", "W");
        }

        return (false, false);
    }

    static bool IsWslVersion2(string output)
    {
        return Regex.IsMatch(output,
            @"(Default\s+Version|Standardversion)\s*:\s*2",
            RegexOptions.IgnoreCase)
        || Regex.IsMatch(output, @"\s2\s", RegexOptions.Multiline);
    }

    static bool InstallWsl2(OperationContext ctx)
    {
        ctx.Log(Translations.T("wsl_installing"), "S");
        ctx.Log("[!] UAC elevation prompt may appear.", "W");

        try
        {
            // Runs  wsl --install
            var pi = new ProcessStartInfo
            {
                FileName        = "powershell.exe",
                Arguments       =
                    "-NoProfile -ExecutionPolicy Bypass " +
                    "-Command \"" +
                    "Write-Host 'Installing WSL2 – please wait …'; " +
                    "wsl --install; " +
                    "Write-Host ''; " +
                    "Write-Host 'Done. Close this window to continue.' -ForegroundColor Green; " +
                    "Pause\"",
                Verb            = "runas",         // request UAC elevation
                UseShellExecute = true,            // required for Verb=runas
                CreateNoWindow  = false,
                WindowStyle     = ProcessWindowStyle.Normal,
            };

            var proc = Process.Start(pi);
            ctx.Log("WSL2 PowerShell installer running – waiting …", "S");
            proc?.WaitForExit();

            int code = proc?.ExitCode ?? -1;
            ctx.Log($"WSL2 installer exited with code {code}.", "S");

            if (code == 0 || code == 1)
            {
                if (code == 1)
                    ctx.Log(Translations.T("wsl_reboot"), "W");
                ctx.Log("[OK]  WSL2 installation command completed.", "O");
                return true;
            }

            ctx.Log($"WSL2 installer returned exit code {code}.", "W");
            return false;
        }
        catch (Exception ex)
        {
            ctx.Log($"{Translations.T("wsl_error")}: {ex.Message}", "E");
            return false;
        }
    }

    
    //  DOCKER + WSL2  CHECK
    static void CheckDockerAndWsl(OperationContext ctx)
    {
        ctx.NotifyStatus("Docker", 1, "Checking Docker + WSL2 …");

        // WSL2 ─────────────────────────────────────────────────
        var (wslInst, wslV2) = CheckWsl(ctx);
        if (!wslInst)
            ctx.Log(Translations.T("wsl_not_found"), "W");
        else if (!wslV2)
            ctx.Log("WSL found but not v2 – Docker may not work correctly.", "W");

        // Docker CLI
        bool dockerRunning = false;
        try
        {
            var pi = new ProcessStartInfo("docker", "--version")
            {
                RedirectStandardOutput = true,
                UseShellExecute        = false,
                CreateNoWindow         = true,
            };
            using var proc = Process.Start(pi);
            string ver     = proc?.StandardOutput.ReadLine() ?? "";
            proc?.WaitForExit();
            dockerRunning  = proc?.ExitCode == 0
                          && !string.IsNullOrWhiteSpace(ver);
            if (dockerRunning)
                ctx.Log($"Docker CLI: {ver.Trim()}", "O");
        }
        catch { }

        // Docker Desktop installed
        bool dockerInstalled = dockerRunning || DockerDesktopInstalled();

        // Combined status
        int state;
        string msg;

        if (dockerRunning && wslV2)
        {
            state = 2;
            msg   = "Docker running, WSL2 OK.";
        }
        else if (dockerInstalled && !wslInst)
        {
            state = 4;
            msg   = "Docker installed but WSL2 missing.";
        }
        else if (dockerInstalled && !dockerRunning)
        {
            state = 4;
            msg   = "Docker installed but not running – start Docker Desktop.";
        }
        else if (!dockerInstalled && wslInst)
        {
            state = 4;
            msg   = "WSL2 OK – Docker Desktop not installed.";
        }
        else
        {
            state = 3;
            msg   = "Docker and WSL2 not installed.";
        }

        ctx.NotifyStatus("Docker", state, msg);
        ctx.SetBadge("Docker", state);
    }

    static bool DockerDesktopInstalled()
    {
        // Registry check
        foreach (var rp in new[]
        {
            @"SOFTWARE\Docker Inc.\Docker Desktop",
            @"SOFTWARE\WOW6432Node\Docker Inc.\Docker Desktop"
        })
        {
            foreach (var hive in new[] { Registry.LocalMachine, Registry.CurrentUser })
            {
                try
                {
                    if (hive.OpenSubKey(rp) != null) return true;
                }
                catch { }
            }
        }

        // Filesystem check
        foreach (var exe in new[]
        {
            @"C:\Program Files\Docker\Docker\Docker Desktop.exe",
            @"C:\Program Files\Docker\Docker Desktop.exe",
            Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                @"Docker\Docker Desktop.exe"),
        })
        {
            if (File.Exists(exe)) return true;
        }
        return false;
    }

    
    //  OP DOCKER
    
    public static void OpDocker(OperationContext ctx)
    {
        ctx.NotifyStatus("Docker", 1, "Setting up Docker + WSL2 …");
        ctx.Log(Translations.T("docker_wsl_warn"), "S");

        // Step 1: WSL2
        var (wslInst, wslV2) = CheckWsl(ctx);

        if (!wslInst || !wslV2)
        {
            ctx.Log(Translations.T("wsl_not_found"), "W");
            bool success = InstallWsl2(ctx);

            if (success)
            {
                // Re-check after install
                (wslInst, wslV2) = CheckWsl(ctx);
                if (wslInst && wslV2)
                    ctx.Log(Translations.T("wsl_found"), "O");
                else
                    ctx.Log(
                        "WSL2 may need a restart to fully activate.\r\n" +
                        "Continuing with Docker Desktop installation …", "W");
            }
            else
            {
                ctx.Log(
                    "WSL2 installation failed or was cancelled.\r\n" +
                    "Docker Desktop requires WSL2 – proceeding anyway.", "W");
            }
        }
        else
        {
            ctx.Log(Translations.T("wsl_found"), "O");
        }

        // Step 2: Docker Desktop
        if (DockerDesktopInstalled())
        {
            ctx.Log("Docker Desktop already installed – skipping download.", "S");
            ctx.NotifyStatus("Docker", 2, "Docker Desktop present.");
            return;
        }

        try
        {
            const string url =
                "https://desktop.docker.com/win/main/amd64/" +
                "Docker%20Desktop%20Installer.exe";
            var dest = Path.Combine(
                Path.GetTempPath(), "DockerDesktopInstaller.exe");

            ctx.Log("Downloading Docker Desktop installer …", "S");
            DownloadFile(ctx, url, dest);

            ctx.Log("Launching Docker Desktop installer …", "S");
            var proc = Process.Start(new ProcessStartInfo(dest,
                "install --quiet --backend=wsl-2")
            {
                UseShellExecute = true,
                Verb            = "runas",
            });
            proc?.WaitForExit();

            bool ok = proc?.ExitCode == 0;
            ctx.NotifyStatus("Docker", ok ? 2 : 4,
                ok  ? "Docker Desktop installed."
                    : $"Docker installer exited with {proc?.ExitCode}.");
            ctx.Log(ok
                ? "[OK]  Docker Desktop installed."
                : $"[!!]  Docker installer returned {proc?.ExitCode}.", ok ? "O" : "W");
        }
        catch (Exception ex)
        {
            ctx.NotifyStatus("Docker", 3, $"Error: {ex.Message}");
            ctx.Log(ex.Message, "E");
        }
    }

    
    //  OP ALL  –  complete sequence
    public static void OpAll(OperationContext ctx)
    {
        ctx.Log("=== FIKA-Server Installation gestartet ===", "S");
        ctx.Log($"    EFT-Methode: {ctx.Config.EftMethod}", "S");
        ctx.Log("─────────────────────────────────────────────", "S");

        // 1. Steam ──────────────────────────────────────────────
        ctx.Log("Schritt 1/8 – STEAM", "S");
        OpSteam(ctx);

        // 2. EFT ────────────────────────────────────────────────
        ctx.Log("Schritt 2/8 – EFT", "S");
        if (EftExists(ctx.Config.EftDir))
        {
            ctx.Log($"EFT bereits vorhanden: {ctx.Config.EftDir}", "O");
            ctx.NotifyStatus("EFT", 2, $"EFT found: {ctx.Config.EftDir}");
        }
        else
        {
            // Launch installer/launcher
            try
            {
                if (ctx.Config.EftMethod == "Steam")
                    OpEFTSteam(ctx);
                else
                    OpEFTBSG(ctx);
            }
            catch (Exception ex)
            {
                ctx.Log(
                    $"EFT launch error: {ex.Message} – continuing to wait …", "W");
            }

            WaitForEFT(ctx);
        }

        // 3. SPT
        ctx.Log("Schritt 3/8 – SPT SERVER", "S");
        OpSPT(ctx);

        // 4. Fika
        ctx.Log("Schritt 4/8 – FIKA", "S");
        OpFika(ctx);

        // 5. Headless
        ctx.Log("Schritt 5/8 – HEADLESS CLIENT", "S");
        OpHeadless(ctx);

        // 6. Docker + WSL2
        ctx.Log("Schritt 6/8 – DOCKER + WSL2", "S");
        OpDocker(ctx);

        // 7. Firewall
        ctx.Log("Schritt 7/8 – FIREWALL", "S");
        OpFirewall(ctx);

        // 8. WebApp
        ctx.Log("Schritt 8/8 – FIKAWEBAPP", "S");
        OpWebApp(ctx);

        ctx.Log("─────────────────────────────────────────────", "S");
        ctx.Log("=== Installation vollständig ===", "O");

        // Final status check
        OpCheckAll(ctx);
    }

    
    //  OPERATIONS
    
    public static void OpSteam(OperationContext ctx)
    {
        ctx.NotifyStatus("Steam", 1, "Downloading Steam …");
        try
        {
            const string url =
                "https://cdn.akamai.steamstatic.com/client/installer/SteamSetup.exe";
            var dest = Path.Combine(Path.GetTempPath(), "SteamSetup.exe");
            DownloadFile(ctx, url, dest);
            Process.Start(new ProcessStartInfo(dest)
                { UseShellExecute = true })?.WaitForExit();
            ctx.NotifyStatus("Steam", 2, "Steam setup complete.");
            ctx.Log("[OK]  Steam installation finished.", "O");
        }
        catch (Exception ex)
        {
            ctx.NotifyStatus("Steam", 3, $"Error: {ex.Message}");
            ctx.Log(ex.Message, "E");
        }
    }

    public static void OpEFTBSG(OperationContext ctx)
    {
        ctx.NotifyStatus("EFT", 1, "Downloading BSG Launcher …");
        const string url =
            "https://launcher.escapefromtarkov.com/launcher/download";
        var dest = Path.Combine(Path.GetTempPath(), "BSGLauncher.exe");
        ctx.Log("Downloading BSG Launcher …", "S");
        DownloadFile(ctx, url, dest);
        ctx.Log("Launching BSG Launcher installer …", "S");
        Process.Start(new ProcessStartInfo(dest)
            { UseShellExecute = true })?.WaitForExit();
        ctx.Log("BSG Launcher installer closed – continuing to wait for EFT.", "O");
        ctx.NotifyStatus("EFT", 1, "BSG Launcher ready – awaiting EFT …");
    }

    public static void OpEFTSteam(OperationContext ctx)
    {
        ctx.NotifyStatus("EFT", 1, "Starting EFT Steam download …");
        ctx.Log("Triggering Steam install for EFT (App-ID 3932890) …", "S");
        Process.Start(new ProcessStartInfo("steam://install/3932890")
            { UseShellExecute = true });
        ctx.Log("Steam download triggered – awaiting EFT.", "O");
        ctx.NotifyStatus("EFT", 1, "Steam is downloading EFT …");
    }

    public static void OpSPT(OperationContext ctx)
    {
        ctx.NotifyStatus("SPT", 1, "Downloading SPT Installer …");
        try
        {
            // SPT 4.x needs both runtimes present before the server will start.
            EnsureDotnetRuntimes(ctx);

            // Official installer, hosted by the SP-Tushonka project. The
            // "latest/download" form always resolves to the newest release.
            const string url =
                "https://github.com/SP-Tushonka/installer/releases/latest/" +
                "download/SPTInstaller.exe";
            var dest = Path.Combine(Path.GetTempPath(), "SPTInstaller.exe");
            ctx.Log("Downloading SPT Installer …", "S");
            DownloadFile(ctx, url, dest);
            ctx.Log("Launching SPT Installer – follow on-screen steps.", "S");

            var proc = Process.Start(new ProcessStartInfo(dest)
                { UseShellExecute = true });

            ctx.Log("Waiting for SPT Installer to close …", "S");
            proc?.WaitForExit();
            ctx.Log("SPT Installer closed. Scanning for SPT …", "O");

            var found = ResolveSptDir(ctx.Config.SptDir) ?? FindSptPath(ctx);
            if (found != null) ctx.UpdateSptDir(found);

            bool ok = SptExists(ctx.Config.SptDir);
            ctx.NotifyStatus("SPT", ok ? 2 : 4,
                ok  ? $"SPT found: {ctx.Config.SptDir}"
                    : "SPT not found – set path in Settings.");
        }
        catch (Exception ex)
        {
            ctx.NotifyStatus("SPT", 3, $"Error: {ex.Message}");
            ctx.Log(ex.Message, "E");
        }
    }

    public static void OpFika(OperationContext ctx)
    {
        ctx.NotifyStatus("Fika", 1, "Installing Fika …");
        try
        {
            if (!ValidateSptDir(ctx, "Fika")) return;
            bool anyOk = false;

            ctx.Log($"Runtime folder: {ctx.Config.SptDir}", "S");
            ctx.Log($"Game root:      {GameRoot(ctx.Config.SptDir)}", "S");

            foreach (var (repo, zipName) in new[]
            {
                ("Fika-Server-CSharp", "Fika.Server.zip"),
                ("Fika-Plugin",        "Fika.Plugin.zip"),
            })
            {
                ctx.Log($"Downloading {repo} …", "S");
                var asset = GetLatestGitHubAsset(ctx,
                    $"https://api.github.com/repos/project-fika/{repo}/releases/latest",
                    ".zip");
                if (asset == null) continue;

                var tmp = Path.Combine(Path.GetTempPath(), zipName);
                DownloadFile(ctx, asset, tmp);
                ExtractFikaArchive(ctx, tmp);
                File.Delete(tmp);
                ctx.Log($"[OK]  {repo} installed.", "O");
                anyOk = true;
            }

            // Only available once the server has run once and written fika.jsonc.
            if (anyOk && string.IsNullOrWhiteSpace(ctx.Config.ApiKey))
            {
                var key = TryReadFikaApiKey(ctx);
                if (key != null) ctx.UpdateApiKey(key);
            }

            ctx.NotifyStatus("Fika", anyOk ? 2 : 3,
                anyOk ? "Fika installed." : "No Fika assets found.");
        }
        catch (Exception ex)
        {
            ctx.NotifyStatus("Fika", 3, $"Error: {ex.Message}");
            ctx.Log(ex.Message, "E");
        }
    }

    public static void OpHeadless(OperationContext ctx)
    {
        ctx.NotifyStatus("Headless", 1, "Installing Headless …");
        try
        {
            if (!ValidateSptDir(ctx, "Headless")) return;

            foreach (var (repo, zipName) in new[]
            {
                ("Fika-Headless",         "Fika.Headless.zip"),
                ("Fika-Headless-Manager", "Fika.Headless.Manager.zip"),
            })
            {
                ctx.Log($"Downloading {repo} …", "S");
                var asset = GetLatestGitHubAsset(ctx,
                    $"https://api.github.com/repos/project-fika/{repo}/releases/latest",
                    ".zip");
                if (asset == null) continue;

                var tmp = Path.Combine(Path.GetTempPath(), zipName);
                DownloadFile(ctx, asset, tmp);
                ExtractFikaArchive(ctx, tmp);
                File.Delete(tmp);
                ctx.Log($"[OK]  {repo} installed.", "O");
            }

            ctx.NotifyStatus("Headless", 2, "Headless installed.");
        }
        catch (Exception ex)
        {
            ctx.NotifyStatus("Headless", 3, $"Error: {ex.Message}");
            ctx.Log(ex.Message, "E");
        }
    }

    public static void OpFirewall(OperationContext ctx)
    {
        ctx.NotifyStatus("Firewall", 1, "Configuring Firewall …");
        ctx.Log("Adding FIKA firewall rules (Admin required) …", "S");

        var rules = new[]
        {
            ("6969","TCP"), ("6969","UDP"),
            ("25565","UDP"), ("8080","TCP"), ("5000","TCP")
        };

        bool allOk = true;
        foreach (var (port, proto) in rules)
        {
            bool ok = AddFirewallRule(ctx, port, proto);
            ctx.FWPort(port, proto, ok ? "v" : "x");
            if (!ok) allOk = false;
        }

        ctx.NotifyStatus("Firewall", allOk ? 2 : 4,
            allOk ? "All firewall rules applied."
                  : "Some rules failed – run as Administrator.");
    }

    //  FikaWebApp ships as a Docker image – the old Fika-Web SPT mod repository
    //  no longer exists. Host port 8080 maps to the container's port 5000.
    const string WebAppImage     = "lacyway/fikawebapp:latest";
    const string WebAppContainer = "fikawebapp";

    // Reaching the host from inside the container needs Docker Desktop's
    // host alias – "localhost" would resolve to the container itself.
    const string WebAppBaseUrl   = "https://host.docker.internal:6969";

    public static void OpWebApp(OperationContext ctx)
    {
        ctx.NotifyStatus("WebApp", 1, "Installing FikaWebApp …");
        try
        {
            if (string.IsNullOrWhiteSpace(ctx.Config.ApiKey))
            {
                var key = TryReadFikaApiKey(ctx);
                if (key != null) ctx.UpdateApiKey(key);
            }

            if (string.IsNullOrWhiteSpace(ctx.Config.ApiKey))
            {
                ctx.NotifyStatus("WebApp", 3,
                    "API key empty – enter it on the WebApp page.");
                ctx.Log("API key required for WebApp. Start the SPT server " +
                        "once so Fika writes it to fika.jsonc.", "W");
                return;
            }

            if (!DockerRunning())
            {
                ctx.NotifyStatus("WebApp", 3, "Docker is not running.");
                ctx.Log("FikaWebApp runs as a Docker container – start " +
                        "Docker Desktop and try again.", "W");
                return;
            }

            var dataDir = SptExists(ctx.Config.SptDir)
                ? Path.Combine(GameRoot(ctx.Config.SptDir), "webappdata")
                : Path.Combine(Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                    "FikaWebApp", "data");
            Directory.CreateDirectory(dataDir);

            ctx.Log($"Pulling {WebAppImage} …", "S");
            if (!RunDocker(ctx, $"pull {WebAppImage}", 10))
            {
                ctx.NotifyStatus("WebApp", 3, "Could not pull the image.");
                return;
            }

            // Replace any previous container so a changed API key takes effect.
            RunDocker(ctx, $"rm -f {WebAppContainer}", 1, quiet: true);

            ctx.Log("Starting FikaWebApp container …", "S");
            var args =
                $"run -d --name {WebAppContainer} --restart unless-stopped " +
                $"-p 8080:5000 " +
                $"-e PORT=5000 " +
                $"-e API_KEY={ctx.Config.ApiKey} " +
                $"-e BASE_URL={WebAppBaseUrl} " +
                $"-v \"{dataDir}:/app/data\" " +
                $"{WebAppImage} --quiet-logs";

            if (!RunDocker(ctx, args, 3))
            {
                ctx.NotifyStatus("WebApp", 3, "Container failed to start.");
                return;
            }

            ctx.Log($"Fika server URL: {WebAppBaseUrl}", "S");
            ctx.Log($"Data folder:     {dataDir}", "S");
            ctx.NotifyStatus("WebApp", 2, "FikaWebApp running on port 8080.");
            ctx.Log("[OK]  FikaWebApp running – http://localhost:8080", "O");
            ctx.Log("      Default login: admin / Admin123!  – change it now.", "W");
        }
        catch (Exception ex)
        {
            ctx.NotifyStatus("WebApp", 3, $"Error: {ex.Message}");
            ctx.Log(ex.Message, "E");
        }
    }

    static bool DockerRunning()
    {
        try
        {
            // --format keeps the output to a single line; "docker info" in full
            // can outgrow the pipe buffer and stall.
            var pi = new ProcessStartInfo("docker", "info --format {{.ServerVersion}}")
            {
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false,
                CreateNoWindow         = true,
            };
            using var proc = Process.Start(pi);
            if (proc == null) return false;

            var stdout = proc.StandardOutput.ReadToEndAsync();
            var stderr = proc.StandardError.ReadToEndAsync();
            if (!proc.WaitForExit(20_000)) { try { proc.Kill(true); } catch { } return false; }
            stdout.Wait(2_000); stderr.Wait(2_000);

            return proc.ExitCode == 0;
        }
        catch { return false; }
    }

    static bool RunDocker(OperationContext ctx, string args,
        int timeoutMinutes, bool quiet = false)
    {
        try
        {
            var pi = new ProcessStartInfo("docker", args)
            {
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false,
                CreateNoWindow         = true,
            };
            using var proc = Process.Start(pi);
            if (proc == null) return false;

            // Drain both pipes concurrently – "docker pull" emits enough
            // progress output to fill the buffer and deadlock otherwise.
            var stdout = proc.StandardOutput.ReadToEndAsync();
            var stderr = proc.StandardError.ReadToEndAsync();

            if (!proc.WaitForExit(timeoutMinutes * 60_000))
            {
                try { proc.Kill(true); } catch { }
                if (!quiet)
                    ctx.Log($"  docker {args.Split(' ')[0]}: timed out after " +
                            $"{timeoutMinutes} min.", "W");
                return false;
            }

            stdout.Wait(2_000);
            stderr.Wait(2_000);

            if (proc.ExitCode == 0) return true;

            if (!quiet)
            {
                var msg = stderr.Status == TaskStatus.RanToCompletion
                    ? stderr.Result.Trim() : "";
                ctx.Log($"  docker {args.Split(' ')[0]} failed " +
                        $"({proc.ExitCode}): {msg}", "W");
            }
            return false;
        }
        catch (Exception ex)
        {
            if (!quiet) ctx.Log($"  docker error: {ex.Message}", "E");
            return false;
        }
    }

    
    //  SPT CHECK + FIND
    static void CheckSPT(OperationContext ctx)
    {
        ctx.NotifyStatus("SPT", 1, "Checking SPT …");
        if (!SptExists(ctx.Config.SptDir))
        {
            // A path saved before 4.1 points at the game root – the server moved
            // into SPT_Runtime, so try that before falling back to a full scan.
            var found = ResolveSptDir(ctx.Config.SptDir) ?? FindSptPath(ctx);
            if (found != null) ctx.UpdateSptDir(found);
        }
        bool ok = SptExists(ctx.Config.SptDir);
        ctx.NotifyStatus("SPT", ok ? 2 : 4,
            ok  ? $"SPT found: {ctx.Config.SptDir}"
                : "SPT not found – set path in Settings.");
        ctx.SetBadge("SPT", ok ? 2 : 4);
    }

    static bool SptExists(string? dir)
        => !string.IsNullOrEmpty(dir)
        && File.Exists(Path.Combine(dir, "SPT.Server.exe"));

    static string? FindSptPath(OperationContext ctx)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Add(string p) { if (!string.IsNullOrEmpty(p)) seen.Add(p); }

        foreach (var drv in FixedDrives())
            foreach (var n in new[]
            {
                "SPT","spt","SPT 4.1","SPT4","Tushonka","SPTushonka",
                "FIKA","fika","FikaServer","fika-server",
                @"Games\SPT", @"Games\fika", @"Spiele\SPT", @"Gaming\SPT"
            })
                Add(Path.Combine(drv, n));

        foreach (var lib in GetSteamLibraries())
            foreach (var n in new[]
                { "SPT","spt","SPT 4.1","Tushonka","fika","FIKA","FikaServer" })
            {
                Add(Path.Combine(lib, n));
                Add(Path.Combine(lib, "steamapps", "common", n));
            }

        // Each candidate may be the game root or the runtime folder itself.
        foreach (var c in seen)
        {
            try
            {
                var hit = ResolveSptDir(c);
                if (hit != null) return hit;
            }
            catch { }
        }

        foreach (var drv in FixedDrives())
        {
            ctx.Log($"Full-scan {drv} for SPT.Server.exe …", "S");
            var dir = FindFileOnDrive(ctx, drv, "SPT.Server.exe", 90_000);
            if (dir != null) return dir;
        }
        return null;
    }

    
    //  FIKA CHECK
    static void CheckFika(OperationContext ctx)
    {
        ctx.NotifyStatus("Fika", 1, "Checking Fika …");
        bool serverFound = false, pluginFound = false;

        // Server mod sits next to SPT.Server.exe, the client plugin one level up
        // at the game root (BepInEx is a sibling of SPT_Runtime on 4.1+).
        var mods = Path.Combine(ctx.Config.SptDir ?? "", "user", "mods");
        if (Directory.Exists(mods))
            serverFound = Directory.GetDirectories(mods)
                .Any(d => Path.GetFileName(d).StartsWith(
                    "fika-server", StringComparison.OrdinalIgnoreCase));

        var plugins = Path.Combine(GameRoot(ctx.Config.SptDir), "BepInEx", "plugins");
        if (Directory.Exists(plugins))
            pluginFound = Directory.GetFiles(plugins, "Fika.Core*.dll",
                SearchOption.AllDirectories).Length > 0;

        bool ok = serverFound || pluginFound;
        ctx.NotifyStatus("Fika", ok ? 2 : 4,
            ok  ? $"Fika – Server: {serverFound}, Plugin: {pluginFound}"
                : "Fika not installed.");
        ctx.SetBadge("Fika", ok ? 2 : 4);
    }

    
    //  HEADLESS CHECK
    static void CheckHeadless(OperationContext ctx)
    {
        ctx.NotifyStatus("Headless", 1, "Checking Headless …");
        bool found = false;

        var root = GameRoot(ctx.Config.SptDir);
        var p    = Path.Combine(root, "BepInEx", "plugins");
        if (Directory.Exists(p))
            found = Directory.GetFiles(p, "Fika.Headless*",
                SearchOption.AllDirectories).Length > 0;

        found |= File.Exists(Path.Combine(root, "FikaHeadlessManager.exe"));

        ctx.NotifyStatus("Headless", found ? 2 : 4,
            found ? "Headless found." : "Headless not installed.");
        ctx.SetBadge("Headless", found ? 2 : 4);
    }

    
    //  FIREWALL CHECK
    static void CheckFirewall(OperationContext ctx)
    {
        ctx.NotifyStatus("Firewall", 1, "Checking firewall …");
        var ports = new[]
        {
            ("6969","TCP"),("6969","UDP"),
            ("25565","UDP"),("8080","TCP"),("5000","TCP")
        };
        bool all = true;
        foreach (var (p, proto) in ports)
        {
            bool ok = FirewallRuleExists(p, proto);
            ctx.FWPort(p, proto, ok ? "v" : "x");
            if (!ok) all = false;
        }
        ctx.NotifyStatus("Firewall", all ? 2 : 4,
            all ? "All ports OK." : "Some ports not open.");
        ctx.SetBadge("Firewall", all ? 2 : 4);
    }

    
    //  WEBAPP CHECK
    static void CheckWebApp(OperationContext ctx)
    {
        ctx.NotifyStatus("WebApp", 1, "Checking FikaWebApp …");

        string state = "";
        try
        {
            var pi = new ProcessStartInfo("docker",
                $"ps -a --filter name=^/{WebAppContainer}$ --format {{{{.State}}}}")
            {
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false,
                CreateNoWindow         = true,
            };
            using var proc = Process.Start(pi);
            if (proc != null)
            {
                var stdout = proc.StandardOutput.ReadToEndAsync();
                var stderr = proc.StandardError.ReadToEndAsync();
                if (proc.WaitForExit(20_000) && stdout.Wait(2_000))
                    state = stdout.Result.Trim();
                else
                    try { proc.Kill(true); } catch { }
                stderr.Wait(2_000);
            }
        }
        catch { }

        bool running = state.Equals("running", StringComparison.OrdinalIgnoreCase);
        bool present = !string.IsNullOrEmpty(state);

        ctx.NotifyStatus("WebApp", running ? 2 : 4,
            running ? "FikaWebApp container running."
            : present ? $"FikaWebApp container present but {state}."
                      : "WebApp: container not found");
        ctx.SetBadge("WebApp", running ? 2 : 4);
    }

    
    //  EFT HELPERS
    static bool EftExists(string? dir)
        => !string.IsNullOrEmpty(dir)
        && (File.Exists(Path.Combine(dir, "EscapeFromTarkov.exe"))
         || File.Exists(Path.Combine(dir, "build",
                "EscapeFromTarkov.exe")));

    static string? FindEftPath(OperationContext ctx)
    {
        // 1) BSG registry
        foreach (var rp in new[]
        {
            @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion" +
            @"\Uninstall\EscapeFromTarkov",
            @"SOFTWARE\Microsoft\Windows\CurrentVersion" +
            @"\Uninstall\EscapeFromTarkov"
        })
        {
            foreach (var hive in new[]
                { Registry.LocalMachine, Registry.CurrentUser })
            {
                try
                {
                    using var k = hive.OpenSubKey(rp);
                    if (k?.GetValue("InstallLocation") is string loc
                        && EftExists(loc)) return loc;
                }
                catch { }
            }
        }

        // 2) Steam libraries
        foreach (var lib in GetSteamLibraries())
        {
            var eft = Path.Combine(lib, "steamapps",
                "common", "Escape from Tarkov");
            if (EftExists(eft)) return eft;
        }

        // 3) Common drive paths
        foreach (var drv in FixedDrives())
            foreach (var sub in new[]
            {
                @"Battlestate Games\EFT (live)",
                @"Battlestate Games\EFT",
                @"Games\EFT", @"Games\EFT (live)",
                @"Spiele\EFT", "EFT", "EFT (live)",
                "Escape from Tarkov"
            })
            {
                var p = Path.Combine(drv, sub);
                if (EftExists(p)) return p;
            }

        // 4) Full-drive scan fallback
        foreach (var drv in FixedDrives())
        {
            ctx.Log($"EFT full-scan on {drv} …", "S");
            var dir = FindFileOnDrive(ctx, drv,
                "EscapeFromTarkov.exe", 60_000);
            if (dir == null) continue;
            if (string.Equals(Path.GetFileName(dir), "build",
                    StringComparison.OrdinalIgnoreCase))
                dir = Path.GetDirectoryName(dir) ?? dir;
            return dir;
        }
        return null;
    }

    
    //  STEAM HELPERS
    static string? SteamInstallPath()
    {
        foreach (var rp in new[]
        {
            @"SOFTWARE\WOW6432Node\Valve\Steam",
            @"SOFTWARE\Valve\Steam"
        })
        {
            try
            {
                using var k = Registry.LocalMachine.OpenSubKey(rp);
                if (k?.GetValue("InstallPath") is string p
                    && Directory.Exists(p)) return p;
            }
            catch { }
        }
        try
        {
            using var k = Registry.CurrentUser
                .OpenSubKey(@"SOFTWARE\Valve\Steam");
            if (k?.GetValue("SteamPath") is string p
                && Directory.Exists(p))
                return p.Replace("/", "\\");
        }
        catch { }
        foreach (var p in new[]
        {
            @"C:\Program Files (x86)\Steam",
            @"C:\Program Files\Steam",
        })
        {
            if (Directory.Exists(p)
                && File.Exists(Path.Combine(p, "steam.exe")))
                return p;
        }
        return null;
    }

    static List<string> GetSteamLibraries()
    {
        var libs = new List<string>();
        void Add(string p)
        {
            if (!string.IsNullOrEmpty(p) && !libs.Contains(p)
                && Directory.Exists(p)) libs.Add(p);
        }
        var base_ = SteamInstallPath();
        if (!string.IsNullOrEmpty(base_))
        {
            Add(base_);
            var vdf = Path.Combine(base_, "steamapps", "libraryfolders.vdf");
            if (File.Exists(vdf))
            {
                try
                {
                    foreach (Match m in Regex.Matches(
                        File.ReadAllText(vdf),
                        @"""path""\s+""([^""]+)"""))
                        Add(m.Groups[1].Value.Replace("\\\\", "\\"));
                }
                catch { }
            }
        }
        foreach (var drv in FixedDrives())
            foreach (var sub in new[]
                { "SteamLibrary", "Steam", "SteamGames" })
                Add(Path.Combine(drv, sub));
        return libs;
    }

    
    //  GENERIC HELPERS
    
    static IEnumerable<string> FixedDrives()
        => DriveInfo.GetDrives()
            .Where(d => d.IsReady && d.DriveType == DriveType.Fixed)
            .Select(d => d.RootDirectory.FullName);

    static bool ValidateSptDir(OperationContext ctx, string id)
    {
        if (SptExists(ctx.Config.SptDir)) return true;
        ctx.NotifyStatus(id, 3,
            "SPT path not set – configure in Settings first.");
        ctx.Log($"[{id}] Set a valid SPT directory in Settings.", "W");
        return false;
    }

    static void DownloadFile(OperationContext ctx, string url, string dest)
    {
        ctx.Log($"  GET {url}", "S");
        using var response = Http
            .GetAsync(url, HttpCompletionOption.ResponseHeadersRead)
            .GetAwaiter().GetResult();
        response.EnsureSuccessStatusCode();
        using var src = response.Content
            .ReadAsStreamAsync().GetAwaiter().GetResult();
        using var fs  = File.Create(dest);
        src.CopyTo(fs);
        ctx.Log($"  → {dest}", "O");
    }

    static string? GetLatestGitHubAsset(OperationContext ctx,
        string apiUrl, string ext)
    {
        try
        {
            var json  = Http.GetStringAsync(apiUrl).GetAwaiter().GetResult();
            int start = 0;
            while ((start = json.IndexOf(
                "\"browser_download_url\"", start)) >= 0)
            {
                int q1 = json.IndexOf('"', start + 23) + 1;
                int q2 = json.IndexOf('"', q1);
                var c  = json[q1..q2];
                if (c.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                {
                    ctx.Log($"  Asset: {c}", "S");
                    return c;
                }
                start = q2 + 1;
            }
            ctx.Log($"  No '{ext}' asset found at {apiUrl}.", "W");
        }
        catch (Exception ex)
        {
            ctx.Log($"  GitHub API error: {ex.Message}", "E");
        }
        return null;
    }

    //  .NET RUNTIMES  –  required by the SPT 4.x C# server and launcher
    static readonly (string Name, string Probe, string Url)[] RequiredRuntimes =
    {
        ("ASP.NET Core Runtime 10", "Microsoft.AspNetCore.App 10.",
         "https://aka.ms/dotnet/10.0/aspnetcore-runtime-win-x64.exe"),
        (".NET Desktop Runtime 10", "Microsoft.WindowsDesktop.App 10.",
         "https://aka.ms/dotnet/10.0/windowsdesktop-runtime-win-x64.exe"),
    };

    static string InstalledRuntimes()
    {
        try
        {
            var pi = new ProcessStartInfo("dotnet", "--list-runtimes")
            {
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false,
                CreateNoWindow         = true,
            };
            using var proc = Process.Start(pi);
            string output  = proc?.StandardOutput.ReadToEnd() ?? "";
            proc?.WaitForExit();
            return output;
        }
        catch { return ""; }
    }

    static void EnsureDotnetRuntimes(OperationContext ctx)
    {
        ctx.Log("Checking .NET runtimes required by SPT 4.x …", "S");
        var installed = InstalledRuntimes();

        foreach (var (name, probe, url) in RequiredRuntimes)
        {
            if (installed.Contains(probe, StringComparison.OrdinalIgnoreCase))
            {
                ctx.Log($"  {name}: present.", "O");
                continue;
            }

            ctx.Log($"  {name}: missing – installing …", "W");
            try
            {
                var dest = Path.Combine(Path.GetTempPath(),
                    Path.GetFileName(new Uri(url).LocalPath));
                DownloadFile(ctx, url, dest);

                var proc = Process.Start(new ProcessStartInfo(dest,
                    "/install /quiet /norestart")
                {
                    UseShellExecute = true,
                    Verb            = "runas",
                });
                proc?.WaitForExit();

                ctx.Log(proc?.ExitCode == 0
                    ? $"  [OK]  {name} installed."
                    : $"  [!!]  {name} installer returned {proc?.ExitCode}.",
                    proc?.ExitCode == 0 ? "O" : "W");
            }
            catch (Exception ex)
            {
                ctx.Log($"  {name} install failed: {ex.Message}", "E");
                ctx.Log($"  Install it manually from: {url}", "W");
            }
        }
    }


    //  FIKA API KEY  –  generated into fika.jsonc on the server's first start
    static string? TryReadFikaApiKey(OperationContext ctx)
    {
        if (!SptExists(ctx.Config.SptDir)) return null;

        var cfg = Path.Combine(ctx.Config.SptDir, "user", "mods",
            "fika-server", "assets", "configs", "fika.jsonc");
        if (!File.Exists(cfg))
        {
            ctx.Log("fika.jsonc not found – start the SPT server once to " +
                    "generate the API key.", "W");
            return null;
        }

        try
        {
            var m = Regex.Match(File.ReadAllText(cfg),
                @"""apiKey""\s*:\s*""([^""]+)""");
            if (!m.Success)
            {
                ctx.Log("fika.jsonc found but no apiKey set yet.", "W");
                return null;
            }
            ctx.Log("API key read from fika.jsonc.", "O");
            return m.Groups[1].Value;
        }
        catch (Exception ex)
        {
            ctx.Log($"Could not read fika.jsonc: {ex.Message}", "W");
            return null;
        }
    }

    static bool AddFirewallRule(OperationContext ctx,
        string port, string proto)
    {
        var name = $"FIKA-{port}-{proto}";
        var args =
            $"advfirewall firewall add rule name=\"{name}\" " +
            $"dir=in action=allow protocol={proto} localport={port}";
        try
        {
            var pi = new ProcessStartInfo("netsh", args)
            {
                UseShellExecute        = false,
                CreateNoWindow         = true,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
            };
            using var proc = Process.Start(pi);
            proc?.WaitForExit();
            bool ok = proc?.ExitCode == 0;
            ctx.Log($"  Firewall {port}/{proto}: " +
                $"{(ok ? "added" : $"failed ({proc?.ExitCode})")}",
                ok ? "O" : "W");
            return ok;
        }
        catch (Exception ex)
        {
            ctx.Log($"  Firewall {port}/{proto}: {ex.Message}", "E");
            return false;
        }
    }

    static bool FirewallRuleExists(string port, string proto)
    {
        try
        {
            var pi = new ProcessStartInfo("netsh",
                $"advfirewall firewall show rule name=\"FIKA-{port}-{proto}\"")
            {
                UseShellExecute        = false,
                CreateNoWindow         = true,
                RedirectStandardOutput = true,
            };
            using var proc = Process.Start(pi);
            string output  = proc?.StandardOutput.ReadToEnd() ?? "";
            proc?.WaitForExit();
            return output.Contains("Rule Name:",
                StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    static string? FindFileOnDrive(OperationContext ctx,
        string drivePath, string fileName, int timeoutMs)
    {
        try
        {
            var pi = new ProcessStartInfo("cmd.exe",
                $"/c dir /s /b \"{Path.Combine(drivePath, fileName)}\" 2>nul")
            {
                RedirectStandardOutput = true,
                UseShellExecute        = false,
                CreateNoWindow         = true,
            };
            using var proc = Process.Start(pi);
            if (proc == null) return null;

            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                if (proc.StandardOutput.EndOfStream) break;
                var line = proc.StandardOutput.ReadLine();
                if (!string.IsNullOrWhiteSpace(line))
                {
                    if (!proc.HasExited)
                        try { proc.Kill(); } catch { }
                    proc.WaitForExit(2000);
                    return Path.GetDirectoryName(line.Trim());
                }
                if (proc.HasExited) break;
                System.Threading.Thread.Sleep(10);
            }
            if (!proc.HasExited)
                try { proc.Kill(); } catch { }
            proc.WaitForExit(2000);
        }
        catch (Exception ex)
        {
            ctx.Log($"FindFile({fileName}): {ex.Message}", "W");
        }
        return null;
    }
}