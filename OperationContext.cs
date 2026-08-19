namespace FikaServerSetupWizard
{
    public class OperationContext
    {
        public AppConfig Config { get; set; } = new();

        // Logging
        public Action<string, string>            Log          { get; set; } = (_, _) => { };

        // Badge+log combined
        public Action<string, int, string>       NotifyStatus { get; set; } = (_, _, _) => { };

        // Only badge
        public Action<string, int>               SetBadge     { get; set; } = (_, _) => { };

        // Firewall port status
        public Action<string, string, string>    FWPort       { get; set; } = (_, _, _) => { };

        // Config updates
        public Action<string>                    UpdateApiKey { get; set; } = _ => { };
        public Action<string>                    UpdateSptDir { get; set; } = _ => { };
        public Action<string>                    UpdateEftDir { get; set; } = _ => { };

        // Config getters
        public Func<string>                      GetApiKey    { get; set; } = () => "";

        // BLOCKING DIALOG
        public Func<string, string, bool>        ShowBlockingOkDialog { get; set; }
            = (_, _) => true;
    }
}