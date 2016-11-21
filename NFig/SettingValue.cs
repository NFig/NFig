using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NFig
{
    /// <summary>
    /// Represents the value of a setting. It is useful for describing both defaults and overrides.
    /// </summary>
    public class SettingValue<TSubApp, TTier, TDataCenter>
        where TSubApp : struct
        where TTier : struct
        where TDataCenter : struct
    {
        /// <summary>
        /// The name of the setting which this value applies to.
        /// </summary>
        public string Name { get; }
        /// <summary>
        /// A string-representation of the value. If the setting is encrypted, then this property will be the encrypted string.
        /// </summary>
        public string Value { get; }
        /// <summary>
        /// The sub-app that this value applies to. SubApp=Global/0 means that this value can be applied to any sub-app.
        /// </summary>
        public TSubApp SubApp { get; }
        /// <summary>
        /// The tier that this value applies to. Tier=Any means that the value can be applied to any tier.
        /// </summary>
        public TTier Tier { get; }
        /// <summary>
        /// The data center that this value applies to. DataCenter=Any means that the value can be applied to any data center.
        /// </summary>
        public TDataCenter DataCenter { get; }
        /// <summary>
        /// True if the value is a default (not an override).
        /// </summary>
        public bool IsDefault { get; }
        /// <summary>
        /// Indicates whether overrides are allowed when this value is the current default (not applicable when IsDefault=true).
        /// </summary>
        public bool AllowsOverrides { get; }

        /// <summary>
        /// True if the value is an override (not a default).
        /// </summary>
        public bool IsOverride => !IsDefault;
        /// <summary>
        /// True if <see cref="SubApp"/> is not the "Global"/0 sub-app.
        /// </summary>
        public bool HasSubApp => !Compare.IsDefault(SubApp);
        /// <summary>
        /// True if <see cref="Tier"/> is not the "Any" tier.
        /// </summary>
        public bool HasTier => !Compare.IsDefault(Tier);
        /// <summary>
        /// True if <see cref="DataCenter"/> is not the "Any" data center.
        /// </summary>
        public bool HasDataCenter => !Compare.IsDefault(DataCenter);

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

        /// <summary>
        /// Initializes a new <see cref="SettingValue{TSubApp,TTier,TDataCenter}"/> which represents an override value.
        /// </summary>
        /// <param name="name">The name of the setting which the override applies to.</param>
        /// <param name="value">A string-representation of the override value. If the setting is encrypted, then this must be an encrypted string.</param>
        /// <param name="subApp">The sub-app which the override applies to.</param>
        /// <param name="dataCenter">The data center which the override applies to.</param>
        /// <returns></returns>
        public static SettingValue<TSubApp, TTier, TDataCenter> CreateOverrideValue(string name, string value, TSubApp subApp, TDataCenter dataCenter)
        {
            return new SettingValue<TSubApp, TTier, TDataCenter>(name, value, subApp, default(TTier), dataCenter, false, false);
        }

        /// <summary>
        /// Returns true if this value can be applied to the specified sub-app, tier, and data center.
        /// </summary>
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

        /// <summary>
        /// Returns true if this value is considered more specific (greater precedence) than the <paramref name="value"/> parameter.
        /// </summary>
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
        
        /// <summary>
        /// Returns true if this value, and the <paramref name="value"/> parameter share the same sub-app, tier, and data center.
        /// </summary>
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
