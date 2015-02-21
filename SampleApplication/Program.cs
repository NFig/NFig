using System;
using System.Diagnostics;
using System.Threading;
using NFig;
using NFig.Redis;

namespace SampleApplication
{
    using Override = SettingOverride<DeploymentTier, DataCenter>;
    using Manager = SettingsManager<SampleSettings, DeploymentTier, DataCenter>;
    using NFigRedis = NFigRedisStore<SampleSettings, DeploymentTier, DataCenter>;

    class Program
    {
        static void Main(string[] args)
        {
            var thread = new Thread(KeepAlive);
            thread.Start(0);

            RedisExample();
        }

        public static void BasicOverrides()
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

        public static void RedisExample()
        {
            var nfig = new NFigRedis("localhost:6379", 11, DeploymentTier.Prod, DataCenter.Oregon);
            nfig.SubscribeToAppSettings("Sample", OnSettingsUpdate);
        }

        private static int s_updateInteration = 0;
        public static void OnSettingsUpdate(Exception ex, SampleSettings settings, NFigRedis nfig)
        {
            if (ex != null)
                throw ex;

            Console.WriteLine(settings.ApplicationName + " settings updated. Commit: " + settings.SettingsCommit);
            Console.WriteLine(settings.ConnectionStrings.AdServer);
            Console.WriteLine(nfig.IsCurrent(settings));
            Console.WriteLine();

            s_updateInteration++;

            var tier = DeploymentTier.Prod;
            var dc = DataCenter.Any;

            if (s_updateInteration == 1)
            {
                nfig.SetOverride(settings.ApplicationName, "ConnectionStrings.AdServer", "connection string in redis", tier, dc);
            }
            else if (s_updateInteration == 2)
            {
                nfig.ClearOverride(settings.ApplicationName, "ConnectionStrings.AdServer", tier, dc);
            }
            else
            {
                Environment.Exit(0);
            }
        }

        static void KeepAlive(object data)
        {
            while (true)
            {
                Thread.Sleep(60000);
            }
        }
    }
}
