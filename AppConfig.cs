namespace FikaServerSetupWizard;

public class AppConfig
{
    public string SptDir    { get; set; } = "";
    public string EftDir    { get; set; } = "";
    public string ApiKey    { get; set; } = "";

    public string EftMethod { get; set; } = "BSG";

    public string TempDir   { get; set; } = Path.Combine(Path.GetTempPath(), "FikaSetup");
    public bool   Busy      { get; set; } = false;

    public AppConfig()
    {
        Directory.CreateDirectory(TempDir);
    }
}