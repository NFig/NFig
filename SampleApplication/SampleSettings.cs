using System;
using System.ComponentModel;
using NFig;

namespace SampleApplication
{
    public enum DeploymentTier
    {
        Local,
        Dev,
        Simulation,
        Prod
    }
    public enum DataCenter
    {
        Local,
        NewYork,
        Oregon
    }

    public class SampleSettings
    {
        public CreativesSettings Creatives { get; set; }
        public ChatBotsSettings ChatBots { get; set; }
        public ConnectionStringsSettings ConnectionStrings { get; set; }
        public HaProxyHeadersSettings HaProxyHeaders { get; set; }
        public BosunSettings Bosun { get; set; }
        public AnalyticsSettings Analytics { get; set; }

        public class CreativesSettings : SettingsGroup
        {
            [Setting(30)]
            [Description("How close (in miles) a job has to be considered near the user. Used to determine whether we can show the 'Jobs Near You' creative.")]
            public int NearYouThreshold { get; set; }
            [Setting("-")]
            [Description("The separator used in legacy analytic strings.")]
            public string ImpressionSeparator { get; set; }
        }

        public class ChatBotsSettings : SettingsGroup
        {
            [Setting(false)]
            [TieredDefaultValue(DeploymentTier.Prod, true)]
            [Description("Enables the Malfunctioning Eddie chat bot.")]
            public bool MalfunctioningEddieEnabled { get; set; }
        }

        public class ConnectionStringsSettings : SettingsGroup
        {
            [Setting(null)]
            [TieredDefaultValue(DeploymentTier.Local, "local connection string")]
            [Description("SQL Connection string to the Calculon db.")]
            public string AdServer { get; set; }
            [Setting(null)]
            [TieredDefaultValue(DeploymentTier.Local, "local connection string")]
            [Description("SQL Connection string to the Calculon.Metrics db.")]
            public string Metrics { get; set; }
        }

        public class HaProxyHeadersSettings : SettingsGroup
        {
            [Setting(true)]
            [Description("Enables X-* headers indended for logging in HAProxyLogs.")]
            public bool Enabled { get; set; }
        }

        public class BosunSettings : SettingsGroup
        {
            [Setting(false)]
            [DataCenterDefaultValue(DataCenter.NewYork, true)]
            [Description("Enables reporting to Bosun.")]
            public bool Enabled { get; set; }

            [Setting(null)]
            [DataCenterTieredDefaultValue(DataCenter.NewYork, DeploymentTier.Dev, "http://ny-devbosun01:8070/api/put")]
            [DataCenterTieredDefaultValue(DataCenter.NewYork, DeploymentTier.Prod, "http://bosun:80/api/put")]
            public string ApiUrl { get; set; }

            [Setting(15)]
            public int Interval { get; set; }
        }

        public class AnalyticsSettings : SettingsGroup
        {
            [Setting("Analytics")]
            public string ProdTableName { get; set; }

            [Setting(true)]
            public bool LegacyEnabled { get; set; }
        }
    }
}
