namespace FikaServerSetupWizard;

static class Translations
{
    public static string Lang = "DE";

    private static readonly Dictionary<string, Dictionary<string, string>> Tr = new()
    {
        ["sb_ready"]        = new() { ["DE"] = "  BEREIT  //  STATUS WIRD AUTOMATISCH GEPRUEFT",       ["EN"] = "  READY  //  STATUS IS BEING CHECKED AUTOMATICALLY" },
        ["s_install"]       = new() { ["DE"] = "INSTALLATION",                                          ["EN"] = "INSTALLATION" },
        ["s_all"]           = new() { ["DE"] = ">> ALLE INSTALLIEREN",                                  ["EN"] = ">> INSTALL ALL" },
        ["s_check"]         = new() { ["DE"] = ">> STATUS PRUEFEN",                                     ["EN"] = ">> CHECK STATUS" },
        ["s_settings"]      = new() { ["DE"] = "EINSTELLUNGEN",                                         ["EN"] = "SETTINGS" },
        ["lang_label"]      = new() { ["DE"] = "SPRACHE",                                               ["EN"] = "LANGUAGE" },
        ["n_Steam"]         = new() { ["DE"] = "STEAM",                                                 ["EN"] = "STEAM" },
        ["n_EFT"]           = new() { ["DE"] = "ESCAPE FROM TARKOV",                                    ["EN"] = "ESCAPE FROM TARKOV" },
        ["n_SPT"]           = new() { ["DE"] = "SPT SERVER",                                            ["EN"] = "SPT SERVER" },
        ["n_Fika"]          = new() { ["DE"] = "FIKA",                                                  ["EN"] = "FIKA" },
        ["n_Headless"]      = new() { ["DE"] = "HEADLESS CLIENT",                                       ["EN"] = "HEADLESS CLIENT" },
        ["n_Docker"]        = new() { ["DE"] = "DOCKER + WSL2",                                         ["EN"] = "DOCKER + WSL2" },
        ["n_Firewall"]      = new() { ["DE"] = "FIREWALL",                                              ["EN"] = "FIREWALL" },
        ["n_WebApp"]        = new() { ["DE"] = "FIKAWEBAPP",                                            ["EN"] = "FIKAWEBAPP" },
        ["btn_install"]     = new() { ["DE"] = "INSTALLIEREN",                                          ["EN"] = "INSTALL" },
        ["btn_browse"]      = new() { ["DE"] = "DURCHSUCHEN",                                           ["EN"] = "BROWSE" },
        ["btn_cancel"]      = new() { ["DE"] = "ABBRECHEN",                                             ["EN"] = "CANCEL" },
        ["btn_save"]        = new() { ["DE"] = "SPEICHERN",                                             ["EN"] = "SAVE" },
        ["btn_recheck"]     = new() { ["DE"] = "STATUS NEU PRUEFEN",                                    ["EN"] = "RECHECK STATUS" },
        ["btn_clear"]       = new() { ["DE"] = "LEEREN",                                                ["EN"] = "CLEAR" },
        ["btn_ports"]       = new() { ["DE"] = "PORTS FREISCHALTEN",                                    ["EN"] = "OPEN PORTS" },
        ["btn_wa"]          = new() { ["DE"] = "WEBAPP STARTEN",                                        ["EN"] = "START WEBAPP" },
        ["btn_vsteam"]      = new() { ["DE"] = "VIA STEAM",                                             ["EN"] = "VIA STEAM" },
        // ── Neue Button-Texte ───────────────────────────────────────
        ["btn_chk_steam"]   = new() { ["DE"] = "STEAM PRÜFEN",                                         ["EN"] = "CHECK STEAM" },
        ["btn_inst_steam"]  = new() { ["DE"] = "STEAM INSTALLIEREN",                                   ["EN"] = "INSTALL STEAM" },
        ["btn_chk_spt"]     = new() { ["DE"] = "SPT PRÜFEN",                                           ["EN"] = "CHECK SPT" },
        ["btn_inst_spt"]    = new() { ["DE"] = "SPT INSTALLIEREN",                                     ["EN"] = "INSTALL SPT" },
        ["btn_inst_fika"]   = new() { ["DE"] = "FIKA INSTALLIEREN",                                    ["EN"] = "INSTALL FIKA" },
        ["btn_inst_hl"]     = new() { ["DE"] = "HEADLESS EINRICHTEN",                                  ["EN"] = "SETUP HEADLESS" },
        ["btn_chk_docker"]  = new() { ["DE"] = "DOCKER PRÜFEN",                                        ["EN"] = "CHECK DOCKER" },
        ["btn_inst_docker"] = new() { ["DE"] = "DOCKER INSTALLIEREN",                                  ["EN"] = "INSTALL DOCKER" },
        ["btn_wsl2"]        = new() { ["DE"] = "WSL2 AKTIVIEREN",                                      ["EN"] = "ENABLE WSL2" },
        ["btn_inst_wa"]     = new() { ["DE"] = "WEBAPP INSTALLIEREN",                                  ["EN"] = "INSTALL WEBAPP" },
        ["btn_open_wa"]     = new() { ["DE"] = "WEBAPP ÖFFNEN",                                        ["EN"] = "OPEN WEBAPP" },
        // ── Headers / Descriptions ──────────────────────────────────
        ["h_sub"]           = new() { ["DE"] = "Automatische Erkennung und Installation aller Serverkomponenten.", ["EN"] = "Automatic detection and installation of all server components." },
        ["h_steps"]         = new() { ["DE"] = "INSTALLATIONSSCHRITTE",                                 ["EN"] = "INSTALLATION STEPS" },
        ["h_all"]           = new() { ["DE"] = ">> ALLE INSTALLIEREN",                                  ["EN"] = ">> INSTALL ALL" },
        ["sec_action"]      = new() { ["DE"] = "AKTION",                                                ["EN"] = "ACTION" },
        ["st_desc"]         = new() { ["DE"] = "Lädt den offiziellen Steam Installer herunter.",        ["EN"] = "Downloads the official Steam installer." },
        ["e_sec"]           = new() { ["DE"] = "INSTALLATIONSQUELLE WAEHLEN",                           ["EN"] = "CHOOSE INSTALLATION SOURCE" },
        ["e_note"]          = new() { ["DE"] = "Steam: ...steamapps\\common\\Escape from Tarkov  /  BSG: beliebiger Pfad", ["EN"] = "Steam: ...steamapps\\common\\Escape from Tarkov  /  BSG: any path" },
        ["e_bsg_t"]         = new() { ["DE"] = "BSG LAUNCHER",                                          ["EN"] = "BSG LAUNCHER" },
        ["e_bsg_s"]         = new() { ["DE"] = "Gekauft auf escapefromtarkov.com",                      ["EN"] = "Purchased at escapefromtarkov.com" },
        ["e_st_t"]          = new() { ["DE"] = "STEAM  (App-ID 3932890)",                               ["EN"] = "STEAM  (App-ID 3932890)" },
        ["e_st_s"]          = new() { ["DE"] = "Gekauft im Steam-Store",                                ["EN"] = "Purchased in the Steam Store" },
        ["spt_desc"]        = new() { ["DE"] = "Offizieller SPT Installer. Alle Laufwerke werden automatisch durchsucht.", ["EN"] = "Official SPT Installer. All drives are searched automatically." },
        ["spt_note"]        = new() { ["DE"] = "(!) Falls SPT nicht erkannt wird: Pfad in Einstellungen manuell setzen.", ["EN"] = "(!) If SPT is not detected: manually set the path in Settings." },
        ["fika_desc"]       = new() { ["DE"] = "Fika-Plugin (BepInEx\\plugins\\) + Server-Mod (user\\mods\\fika-server\\)", ["EN"] = "Fika Plugin (BepInEx\\plugins\\) + Server Mod (user\\mods\\fika-server\\)" },
        ["fika_note"]       = new() { ["DE"] = "(!) SPT Server wird einmalig 60s gestartet um API Key zu generieren.", ["EN"] = "(!) SPT Server is started once for 60s to generate the API Key." },
        ["hl_desc"]         = new() { ["DE"] = "Fika.Headless Plugin (BepInEx\\plugins\\) + FikaHeadlessManager.exe",  ["EN"] = "Fika.Headless Plugin (BepInEx\\plugins\\) + FikaHeadlessManager.exe" },
        ["dk_desc"]         = new() { ["DE"] = "Aktiviert WSL2-Features, Kernel-Update und installiert Docker Desktop.", ["EN"] = "Enables WSL2 features, kernel update and installs Docker Desktop." },
        ["dk_note"]         = new() { ["DE"] = "(!) Nach der Installation ist ein Neustart erforderlich.",              ["EN"] = "(!) A restart is required after installation." },
        ["fw_sec"]          = new() { ["DE"] = "PORT-KONFIGURATION",                                    ["EN"] = "PORT CONFIGURATION" },
        ["fw_p1"]           = new() { ["DE"] = "SPT Server API",                                        ["EN"] = "SPT Server API" },
        ["fw_p2"]           = new() { ["DE"] = "SPT Server UDP",                                        ["EN"] = "SPT Server UDP" },
        ["fw_p3"]           = new() { ["DE"] = "Fika Peer-to-Peer",                                     ["EN"] = "Fika Peer-to-Peer" },
        ["fw_p4"]           = new() { ["DE"] = "FikaWebApp HTTP",                                       ["EN"] = "FikaWebApp HTTP" },
        ["fw_p5"]           = new() { ["DE"] = "Container intern",                                      ["EN"] = "Container internal" },
        ["wa_sec"]          = new() { ["DE"] = "KONFIGURATION",                                         ["EN"] = "CONFIGURATION" },
        ["wa_desc"]         = new() { ["DE"] = "Docker Container -> http://localhost:8080",              ["EN"] = "Docker Container -> http://localhost:8080" },
        ["wa_api"]          = new() { ["DE"] = "API KEY  (wird automatisch aus fika.jsonc gelesen)",    ["EN"] = "API KEY  (read automatically from fika.jsonc)" },
        ["wa_note"]         = new() { ["DE"] = "(!) Standard-Login: admin / Admin123  ---  SOFORT AENDERN", ["EN"] = "(!) Default login: admin / Admin123  ---  CHANGE IMMEDIATELY" },
        ["set_title"]       = new() { ["DE"] = "EINSTELLUNGEN",                                         ["EN"] = "SETTINGS" },
        ["set_spt_h"]       = new() { ["DE"] = "SPT-PFAD",                                              ["EN"] = "SPT PATH" },
        ["set_spt_d"]       = new() { ["DE"] = "Muss SPT.Server.exe enthalten.",                        ["EN"] = "Must contain SPT.Server.exe." },
        ["set_eft_h"]       = new() { ["DE"] = "EFT-PFAD  (OPTIONAL)",                                  ["EN"] = "EFT PATH  (OPTIONAL)" },
        ["set_eft_d"]       = new() { ["DE"] = "Steam: ...steamapps\\common\\Escape from Tarkov",       ["EN"] = "Steam: ...steamapps\\common\\Escape from Tarkov" },
        ["set_api_h"]       = new() { ["DE"] = "API KEY",                                               ["EN"] = "API KEY" },
        ["log_hdr"]         = new() { ["DE"] = "  AUSGABE  //  SYSTEMLOG",                              ["EN"] = "  OUTPUT  //  SYSTEM LOG" },
        ["log_cancel"]      = new() { ["DE"] = "Komplett-Installation abgebrochen.",                    ["EN"] = "Full installation cancelled." },
        ["log_method"]      = new() { ["DE"] = "EFT-Methode gewählt:",                                  ["EN"] = "EFT method selected:" },
        ["log_started"]     = new() { ["DE"] = "FIKA-SERVER SETUP UTILITY v1.0 GESTARTET",              ["EN"] = "FIKA-SERVER SETUP UTILITY v1.0 STARTED" },
        ["log_autocheck"]   = new() { ["DE"] = "Starte automatische Status-Prüfung...",                 ["EN"] = "Starting automatic status check..." },
        ["dlg_hdr"]         = new() { ["DE"] = "EFT INSTALLATIONSMETHODE WAEHLEN",                      ["EN"] = "CHOOSE EFT INSTALLATION METHOD" },
        ["dlg_sub"]         = new() { ["DE"] = "Welche Methode soll bei >> ALLE INSTALLIEREN verwendet werden?", ["EN"] = "Which method should be used for >> INSTALL ALL?" },
        ["fbr_spt"]         = new() { ["DE"] = "SPT-Ordner wählen (Ordner mit SPT.Server.exe)",         ["EN"] = "Choose SPT folder (folder containing SPT.Server.exe)" },
        ["fbr_eft"]         = new() { ["DE"] = "EFT-Ordner wählen",                                     ["EN"] = "Choose EFT folder" },
    };

    public static string T(string key)
    {
        if (Tr.TryGetValue(key, out var dict))
            if (dict.TryGetValue(Lang, out var val))
                return val;
        return key;
    }
}