
namespace NFig.Tests
{
    public enum SubApp
    {
        Global = 0,
        One = 1,
        Two = 2,
        Three = 3,
    }

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

    public class SettingsBase : NFigSettingsBase<SubApp, Tier, DataCenter>
    {
    }

    public class DefaultAttribute : DefaultSettingValueAttribute
    {
        public DefaultAttribute(
            object value,
            SubApp subApp = default(SubApp),
            Tier tier = default(Tier),
            DataCenter dataCenter = default(DataCenter),
            bool allowsOverrides = true)
        {
            DefaultValue = value;
            SubApp = subApp;
            Tier = tier;
            DataCenter = dataCenter;
            AllowOverrides = allowsOverrides;
        }
    }

    public class SubAppAttribute : DefaultSettingValueAttribute
    {
        public SubAppAttribute(SubApp subApp, object defaultValue, bool allowsOverrides = true)
        {
            SubApp = subApp;
            DefaultValue = defaultValue;
            AllowOverrides = allowsOverrides;
        }
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
