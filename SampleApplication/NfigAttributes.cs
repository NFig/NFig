


using NFig;

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
        public TieredDefaultValueAttribute(Tier tier, object defaultValue)
        {
            Tier = tier;
            DefaultValue = defaultValue;
        }
    }

    public class TieredDataCenterDefaultValueAttribute : DefaultSettingValueAttribute
    {
        public TieredDataCenterDefaultValueAttribute(Tier tier, DataCenter dataCenter, object defaultValue)
        {
            Tier = tier;
            DataCenter = dataCenter;
            DefaultValue = defaultValue;
        }
    }
}