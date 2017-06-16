using System;
using System.Collections;
using NFig.Infrastructure;

namespace NFig.Tests.Common
{
    public class DefaultAttribute : DefaultValueBaseAttribute
    {
        readonly DefaultCreationInfo<Tier, DataCenter> _creationInfo;

        public DefaultAttribute(
            object value,
            int? subAppId = null,
            Tier tier = default(Tier),
            DataCenter dataCenter = default(DataCenter),
            bool allowsOverrides = true)
        {
            _creationInfo = CreateDefault(value, subAppId, tier, dataCenter, allowsOverrides);
        }

        public override IEnumerable GetDefaults(string appName, string settingName, Type settingType, object tier, int? subAppId, string subAppName)
        {
            yield return _creationInfo;
        }
    }

    public class SubAppAttribute : DefaultAttribute
    {
        public SubAppAttribute(int subAppId, object value, bool allowsOverrides = true) : base(value, subAppId, allowsOverrides: allowsOverrides)
        {
        }
    }

    public class TierAttribute : DefaultAttribute
    {
        public TierAttribute(Tier tier, object value, bool allowsOverrides = true) : base(value, tier: tier, allowsOverrides: allowsOverrides)
        {
        }
    }

    public class DataCenterAttribute : DefaultAttribute
    {
        public DataCenterAttribute(DataCenter dataCenter, object value, bool allowsOverrides = true)
            : base(value, dataCenter: dataCenter, allowsOverrides: allowsOverrides)
        {
        }
    }

    public class TierDataCenterAttribute : DefaultAttribute
    {
        public TierDataCenterAttribute(Tier tier, DataCenter dataCenter, object value, bool allowsOverrides = true)
            : base(value, tier: tier, dataCenter: dataCenter, allowsOverrides: allowsOverrides)
        {
        }
    }

    public class NamedSubAppTierAttribute : DefaultValueBaseAttribute
    {
        public string SubAppName { get; }
        public Tier Tier { get; }
        public object Value { get; }
        public bool AllowsOverrides { get; }

        public NamedSubAppTierAttribute(string subAppName, Tier tier, object value, bool allowsOverrides = true)
        {
            SubAppName = subAppName;
            Tier = tier;
            Value = value;
            AllowsOverrides = allowsOverrides;
        }

        public override IEnumerable GetDefaults(string appName, string settingName, Type settingType, object tier, int? subAppId, string subAppName)
        {
            if (subAppName == SubAppName)
            {
                yield return CreateDefault(Value, subAppId, Tier, DataCenter.Any, AllowsOverrides);
            }
        }
    }

    public class NamedSubAppAttribute : NamedSubAppTierAttribute
    {
        public NamedSubAppAttribute(string subAppName, object value, bool allowsOverrides = true) : base(subAppName, Tier.Any, value, allowsOverrides)
        {
        }
    }
}