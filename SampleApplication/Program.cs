using Nfig;

namespace SampleApplication
{
    class Program
    {
        static void Main(string[] args)
        {
            var manager = new SettingsManager<SampleSettings, DeploymentTier, DataCenter>(DeploymentTier.Prod, DataCenter.Local);
            var settings = manager.GetAppSettings();
        }
    }
}
