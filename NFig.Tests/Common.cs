
namespace NFig.Tests
{
    public enum Tier
    {
        Any = 0,
        Local = 1,
        Dev = 2,
        Prod = 3,
    }

    public enum DataCenter
    {
        Any = 0,
        Local = 1,
        East = 2,
        West = 3,
    }

    public class SettingsBase : INFigSettings<Tier, DataCenter>
    {
        public string ApplicationName { get; set; }
        public string Commit { get; set; }
        public Tier Tier { get; set; }
        public DataCenter DataCenter { get; set; }
    }

    public class DataCenterDefaultAttribute : DefaultSettingValueAttribute
    {
        public DataCenterDefaultAttribute(DataCenter dataCenter, object defaultValue, bool allowOverrides = true)
        {
            DataCenter = dataCenter;
            DefaultValue = defaultValue;
            AllowOverrides = allowOverrides;
        }
    }

    public class TierDefaultAttribute : DefaultSettingValueAttribute
    {
        public TierDefaultAttribute(Tier tier, object defaultValue, bool allowOverrides = true)
        {
            Tier = tier;
            DefaultValue = defaultValue;
            AllowOverrides = allowOverrides;
        }
    }

    public class TierDataCenterDefaultAttribute : DefaultSettingValueAttribute
    {
        public TierDataCenterDefaultAttribute(Tier tier, DataCenter dataCenter, object defaultValue, bool allowOverrides = true)
        {
            Tier = tier;
            DataCenter = dataCenter;
            DefaultValue = defaultValue;
            AllowOverrides = allowOverrides;
        }
    }
}
