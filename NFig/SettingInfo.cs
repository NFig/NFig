using System;
using System.Collections.Generic;
using System.Reflection;

namespace NFig
{
    public class SettingInfo<TTier, TDataCenter>
        where TTier : struct
        where TDataCenter : struct
    {
        public string Name { get; }
        public string Description { get; }
        public bool ChangeRequiresRestart { get; }
        public Type Type { get; }
        public PropertyInfo PropertyInfo { get; }
        public IList<SettingValue<TTier, TDataCenter>> Defaults { get; }
        public IList<SettingValue<TTier, TDataCenter>> Overrides { get; }

        internal SettingInfo(
            string name,
            string description,
            bool changeRequiresRestart,
            PropertyInfo propertyInfo,
            IList<SettingValue<TTier, TDataCenter>> defaults,
            IList<SettingValue<TTier, TDataCenter>> overrides
            )
        {
            Name = name;
            Description = description;
            ChangeRequiresRestart = changeRequiresRestart;
            Type = propertyInfo.PropertyType;
            PropertyInfo = propertyInfo;
            Defaults = defaults;
            Overrides = overrides;
        } 

        public SettingValue<TTier, TDataCenter> GetActiveValueFor(TTier tier, TDataCenter dataCenter)
        {
            var def = GetDefaultFor(tier, dataCenter);
            if (!def.AllowsOverrides)
                return def;

            return GetOverrideFor(tier, dataCenter) ?? def;
        }

        public SettingValue<TTier, TDataCenter> GetDefaultFor(TTier tier, TDataCenter dataCenter)
        {
            return GetBestValueFor(Defaults, tier, dataCenter);
        }

        public SettingValue<TTier, TDataCenter> GetOverrideFor(TTier tier, TDataCenter dataCenter)
        {
            return GetBestValueFor(Overrides, tier, dataCenter);
        }

        public bool CanSetOverrideFor(TTier tier, TDataCenter dataCenter)
        {
            return GetDefaultFor(tier, dataCenter).AllowsOverrides;
        }

        internal static SettingValue<TTier, TDataCenter> GetBestValueFor(IList<SettingValue<TTier, TDataCenter>> values, TTier tier, TDataCenter dataCenter)
        {
            SettingValue<TTier, TDataCenter> best = null;
            foreach (var val in values)
            {
                if (val.IsValidFor(tier, dataCenter) && val.IsMoreSpecificThan(best))
                    best = val;
            }

            return best;
        }
    }
}
