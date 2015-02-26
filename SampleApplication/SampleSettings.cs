using System;
using System.ComponentModel;
using NFig;
using NFig.Redis;
// ReSharper disable UnusedAutoPropertyAccessor.Local

namespace SampleApplication
{
    public enum Tier
    {
        Any = 0,
        Local = 1,
        Dev,
        Simulation,
        Prod
    }
    public enum DataCenter
    {
        Any = 0,
        Local = 1,
        NewYork,
        Oregon
    }

    public class SampleSettings : INFigRedisSettings<Tier, DataCenter>
    {
        public string ApplicationName { get; set; }
        public string SettingsCommit { get; set; }
        public Tier Tier { get; set; }
        public DataCenter DataCenter { get; set; }

        [SettingsGroup]
        public CreativesSettings Creatives { get; private set; }
        
        public class CreativesSettings
        {
            [Setting(30)]
            [Description("How close (in miles) a job has to be considered near the user. Used to determine whether we can show the 'Jobs Near You' creative.")]
            public int NearYouThreshold { get; private set; }

            [Setting("-")]
            [Description("The separator used in legacy analytic strings.")]
            public string ImpressionSeparator { get; private set; }
        }

        [SettingsGroup]
        public ChatBotsSettings ChatBots { get; private set; }

        public class ChatBotsSettings
        {
            [Setting(false)]
            [TieredDefaultValue(Tier.Prod, true)]
            [Description("Enables the Malfunctioning Eddie chat bot.")]
            public bool MalfunctioningEddieEnabled { get; private set; }
        }

        [SettingsGroup]
        public ConnectionStringsSettings ConnectionStrings { get; private set; }

        public class ConnectionStringsSettings
        {
            [Setting(null)]
            [TieredDefaultValue(Tier.Local, "local connection string")]
            [Description("SQL Connection string to the Calculon db.")]
            public string AdServer { get; private set; }

            [Setting(null)]
            [TieredDefaultValue(Tier.Local, "local connection string")]
            [Description("SQL Connection string to the Calculon.Metrics db.")]
            public string Metrics { get; private set; }
        }

        [SettingsGroup]
        public HaProxyHeadersSettings HaProxyHeaders { get; private set; }

        public class HaProxyHeadersSettings
        {
            [Setting("true")]
            [Description("Enables X-* headers indended for logging in HAProxyLogs.")]
            public bool Enabled { get; private set; }
        }

        [SettingsGroup]
        public BosunSettings Bosun { get; private set; }

        public class BosunSettings
        {
            [Setting(false)]
            [DataCenterDefaultValue(DataCenter.NewYork, true)]
            [Description("Enables reporting to Bosun.")]
            public bool Enabled { get; private set; }

            [Setting(null)]
            [TieredDataCenterDefaultValue(Tier.Dev, DataCenter.NewYork, "http://ny-devbosun01:8070/api/put")]
            [TieredDataCenterDefaultValue(Tier.Prod, DataCenter.NewYork, "http://bosun:80/api/put")]
            public string ApiUrl { get; private set; }

            [Setting(15)]
            public int Interval { get; private set; }
        }

        [SettingsGroup]
        public AnalyticsSettings Analytics { get; private set; }

        public class AnalyticsSettings
        {
            [Setting("Analytics")]
            public string ProdTableName { get; private set; }

            [Setting(true)]
            public bool LegacyEnabled { get; private set; }
        }
    }
}
