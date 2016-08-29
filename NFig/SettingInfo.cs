using System;
using System.Collections.Generic;
using System.Reflection;

namespace NFig
{
    public class SettingInfo<TSubApp, TTier, TDataCenter>
        where TSubApp : struct
        where TTier : struct
        where TDataCenter : struct
    {
        public string Name { get; }
        public string Description { get; }
        public bool ChangeRequiresRestart { get; }
        public bool IsEncrypted { get; }
        public Type Type { get; }
        public PropertyInfo PropertyInfo { get; }
        public IList<SettingValue<TSubApp, TTier, TDataCenter>> Defaults { get; }
        public IList<SettingValue<TSubApp, TTier, TDataCenter>> Overrides { get; }

        internal SettingInfo(
            string name,
            string description,
            bool changeRequiresRestart,
            bool isEncrypted,
            PropertyInfo propertyInfo,
            IList<SettingValue<TSubApp, TTier, TDataCenter>> defaults,
            IList<SettingValue<TSubApp, TTier, TDataCenter>> overrides
            )
        {
            Name = name;
            Description = description;
            ChangeRequiresRestart = changeRequiresRestart;
            IsEncrypted = isEncrypted;
            Type = propertyInfo.PropertyType;
            PropertyInfo = propertyInfo;
            Defaults = defaults;
            Overrides = overrides;
        } 

        public SettingValue<TSubApp, TTier, TDataCenter> GetActiveValueFor(TSubApp subApp, TTier tier, TDataCenter dataCenter)
        {
            var def = GetDefaultFor(subApp, tier, dataCenter);
            if (!def.AllowsOverrides)
                return def;

            return GetOverrideFor(subApp, tier, dataCenter) ?? def;
        }

        public SettingValue<TSubApp, TTier, TDataCenter> GetDefaultFor(TSubApp subApp, TTier tier, TDataCenter dataCenter)
        {
            SettingValue<TSubApp, TTier, TDataCenter> value;
            Defaults.GetBestValueFor(subApp, tier, dataCenter, out value);
            return value;
        }

        public SettingValue<TSubApp, TTier, TDataCenter> GetOverrideFor(TSubApp subApp, TTier tier, TDataCenter dataCenter)
        {
            SettingValue<TSubApp, TTier, TDataCenter> value;
            Overrides.GetBestValueFor(subApp, tier, dataCenter, out value);
            return value;
        }

        public bool CanSetOverrideFor(TSubApp subApp, TTier tier, TDataCenter dataCenter)
        {
            return GetDefaultFor(subApp, tier, dataCenter).AllowsOverrides;
        }
    }
}
