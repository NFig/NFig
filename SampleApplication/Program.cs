using Nfig;

namespace SampleApplication
{
    class Program
    {
        static void Main(string[] args)
        {
            var manager = new SettingsManager<SampleSettings>();
            var settings = manager.GetAppSettings(DeploymentTier.Local, DataCenter.Local);
        }
    }
}
