using System;

namespace Nfig
{
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
    public abstract class DefaultSettingValueAttribute : Attribute
    {
        public object DefaultValue { get; set; }
        public DataCenter? DataCenter { get; set; }
        public DeploymentTier? DeploymentTier { get; set; }
    }

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
            DeploymentTier = tier;
            DefaultValue = defaultValue;
        }
    }

    public class DataCenterTieredDefaultValueAttribute : DefaultSettingValueAttribute
    {
        public DataCenterTieredDefaultValueAttribute(DataCenter dataCenter, DeploymentTier tier, object defaultValue)
        {
            DataCenter = dataCenter;
            DeploymentTier = tier;
            DefaultValue = defaultValue;
        }
    }
}