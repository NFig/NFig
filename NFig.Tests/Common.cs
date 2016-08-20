
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

    public class SettingsBase : NFigSettingsBase<Tier, DataCenter>
    {
    }

    public class DataCenterAttribute : DefaultSettingValueAttribute
    {
        public DataCenterAttribute(DataCenter dataCenter, object defaultValue, bool allowOverrides = true)
        {
            DataCenter = dataCenter;
            DefaultValue = defaultValue;
            AllowOverrides = allowOverrides;
        }
    }

    public class TierAttribute : DefaultSettingValueAttribute
    {
        public TierAttribute(Tier tier, object defaultValue, bool allowOverrides = true)
        {
            Tier = tier;
            DefaultValue = defaultValue;
            AllowOverrides = allowOverrides;
        }
    }

    public class TierDataCenterAttribute : DefaultSettingValueAttribute
    {
        public TierDataCenterAttribute(Tier tier, DataCenter dataCenter, object defaultValue, bool allowOverrides = true)
        {
            Tier = tier;
            DataCenter = dataCenter;
            DefaultValue = defaultValue;
            AllowOverrides = allowOverrides;
        }
    }
}
