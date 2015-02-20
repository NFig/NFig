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
                new Override { Name = "Creatives.NearYouThreshold", Value = "50", Tier = 0, DataCenter = DataCenter.Local },
                new Override { Name = "ChatBots.MalfunctioningEddieEnabled", Value = "true" },
                new Override { Name = "ConnectionStrings.AdServer", Value = "LOCAL!!!", Tier = DeploymentTier.Local },
                new Override { Name = "ConnectionStrings.AdServer", Value = "DEV!!!", Tier = DeploymentTier.Dev },
                new Override { Name = "ConnectionStrings.AdServer", Value = "PROD!!!", Tier = DeploymentTier.Prod },
                new Override { Name = "ConnectionStrings.AdServer", Value = "PROD-OREGON!!!", Tier = DeploymentTier.Prod, DataCenter = DataCenter.Oregon },
            };

            var manager = new Manager(DeploymentTier.Prod, DataCenter.Oregon);
            var settings = manager.GetAppSettings(overrides);
        }
    }
}
