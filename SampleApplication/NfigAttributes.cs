using Nfig;

namespace SampleApplication
{
    public class DataCenterDefaultValueAttribute : DefaultSettingValueAttribute
    {
        public DataCenterDefaultValueAttribute(DataCenter dataCenter, object defaultValue)
        {
            DataCenter = dataCenter;
            DefaultValue = defaultValue;
        }
    }

    public class TieredDefaultValueAttribute : DefaultSettingValueAttribute
    {
        public TieredDefaultValueAttribute(DeploymentTier tier, object defaultValue)
        {
            Tier = tier;
            DefaultValue = defaultValue;
        }
    }

    public class DataCenterTieredDefaultValueAttribute : DefaultSettingValueAttribute
    {
        public DataCenterTieredDefaultValueAttribute(DataCenter dataCenter, DeploymentTier tier, object defaultValue)
        {
            DataCenter = dataCenter;
            Tier = tier;
            DefaultValue = defaultValue;
        }
    }
}