using NFig;

namespace SampleApplication
{
    using Override = SettingOverride<DeploymentTier, DataCenter>;
    using Manager = SettingsManager<SampleSettings, DeploymentTier, DataCenter>;

    class Program
    {
        static void Main(string[] args)
        {
            // create some test overrides
            var overrides = new []
            {
                new Override { Name = "Creatives.NearYouThreshold", Value = "50", Tier = null, DataCenter = DataCenter.Local },
                new Override { Name = "ChatBots.MalfunctioningEddieEnabled", Value = "true", Tier = null, DataCenter = null },
                new Override { Name = "ConnectionStrings.AdServer", Value = "LOCAL!!!", Tier = DeploymentTier.Local, DataCenter = null },
                new Override { Name = "ConnectionStrings.AdServer", Value = "DEV!!!", Tier = DeploymentTier.Dev, DataCenter = null },
                new Override { Name = "ConnectionStrings.AdServer", Value = "PROD!!!", Tier = DeploymentTier.Prod, DataCenter = null },
                new Override { Name = "ConnectionStrings.AdServer", Value = "PROD-OREGON!!!", Tier = DeploymentTier.Prod, DataCenter = DataCenter.Oregon },
            };

            var manager = new Manager(DeploymentTier.Prod, DataCenter.Oregon);
            var settings = manager.GetAppSettings(overrides);
        }
    }
}
