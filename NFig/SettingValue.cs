using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NFig
{
    public class SettingValue<TSubApp, TTier, TDataCenter>
        where TSubApp : struct
        where TTier : struct
        where TDataCenter : struct
    {
        public string Name { get; }
        public string Value { get; }
        public TSubApp SubApp { get; }
        public TTier Tier { get; }
        public TDataCenter DataCenter { get; }
        public bool IsDefault { get; }
        public bool AllowsOverrides { get; }

        public bool IsOverride => !IsDefault;
        public bool HasSubApp => !Compare.IsDefault(SubApp);
        public bool HasTier => !Compare.IsDefault(Tier);
        public bool HasDataCenter => !Compare.IsDefault(DataCenter);

        public SettingValue(string name, string value, TSubApp subApp, TDataCenter dataCenter)
            : this(name, value, subApp, default(TTier), dataCenter, false, false)
        {
        }

        internal SettingValue(string name, string value, TSubApp subApp, TTier tier, TDataCenter dataCenter, bool isDefault, bool allowsOverrides)
        {
            Name = name;
            Value = value;
            SubApp = subApp;
            Tier = tier;
            DataCenter = dataCenter;
            IsDefault = isDefault;
            AllowsOverrides = allowsOverrides;
        }

        public bool IsValidFor(TSubApp subApp, TTier tier, TDataCenter dataCenter)
        {
            if (HasSubApp && !Compare.AreEqual(SubApp, subApp))
                return false;

            if (HasTier && !Compare.AreEqual(Tier, tier))
                return false;

            if (HasDataCenter && !Compare.AreEqual(DataCenter, dataCenter))
                return false;

            return true;
        }

        public bool IsMoreSpecificThan([CanBeNull] SettingValue<TSubApp, TTier, TDataCenter> value)
        {
            if (value == null)
                return true;

            // an override is always more specific than a default
            if (IsOverride != value.IsOverride)
                return IsOverride;

            // sub app has precedence over tier
            if (HasSubApp != value.HasSubApp)
                return HasSubApp;

            // tier has precedence over data center
            if (HasTier != value.HasTier)
                return HasTier;

            if (HasDataCenter != value.HasDataCenter)
                return HasDataCenter;

            return false;
        }
        
        public bool HasSameSubAppTierDataCenter([NotNull] SettingValue<TSubApp, TTier, TDataCenter> value)
        {
            return Compare.AreEqual(SubApp, value.SubApp) && Compare.AreEqual(Tier, value.Tier) && Compare.AreEqual(DataCenter, value.DataCenter);
        }
    }

    static class SettingValueExtensions
    {
        internal static int GetBestValueFor<TSubApp, TTier, TDataCenter>(
            this IList<SettingValue<TSubApp, TTier, TDataCenter>> values,
            TSubApp subApp,
            TTier tier,
            TDataCenter dataCenter,
            [CanBeNull] out SettingValue<TSubApp, TTier, TDataCenter> bestValue)
            where TSubApp : struct
            where TTier : struct
            where TDataCenter : struct
        {
            bestValue = null;
            var bestIndex = -1;
            for (var i = 0; i < values.Count; i++)
            {
                var val = values[i];
                if (val.IsValidFor(subApp, tier, dataCenter) && val.IsMoreSpecificThan(bestValue))
                {
                    bestIndex = i;
                    bestValue = val;
                }
            }

            return bestIndex;
        }
    }
}
