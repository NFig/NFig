using System;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace NFig
{
    /// <summary>
    /// The common inferface for defaults and overrides. However, typically you'll want to use one of the concrete types
    /// <see cref="DefaultValue{TTier,TDataCenter}"/> or <see cref="OverrideValue{TTier,TDataCenter}"/>.
    /// </summary>
    public interface ISettingValue<TTier, TDataCenter> : IBySettingDictionaryItem
        where TTier : struct
        where TDataCenter : struct
    {
        /// <summary>
        /// A string-representation of the value. If the setting is encrypted, then this property will be the encrypted string.
        /// </summary>
        string Value { get; }
        /// <summary>
        /// The ID of the sub-app that this value applies to. Null means that the default is applicable to the top-level application, as well as all sub-apps.
        /// </summary>
        int? SubAppId { get; }
        /// <summary>
        /// The tier that this value applies to. Tier=Any means that the value can be applied to any tier.
        /// </summary>
        TTier Tier { get; }
        /// <summary>
        /// The data center that this value applies to. DataCenter=Any means that the value can be applied to any data center.
        /// </summary>
        TDataCenter DataCenter { get; }
        /// <summary>
        /// True if the value is a default (not an override).
        /// </summary>
        bool IsDefault { get; }
        /// <summary>
        /// True if the value is an override (not a default).
        /// </summary>
        bool IsOverride { get; }
    }

    /// <summary>
    /// Helper methods for setting values which are applicable to both defaults and overrides.
    /// </summary>
    public static class SettingValueExtensions // todo: rename
    {
        /// <summary>
        /// True if SubApp is not the "Global"/0 sub-app.
        /// </summary>
        public static bool HasSubApp<TTier, TDataCenter>(this ISettingValue<TTier, TDataCenter> value)
            where TTier : struct
            where TDataCenter : struct
        {
            return value.SubAppId.HasValue;
        }

        /// <summary>
        /// True if Tier is not the "Any" tier.
        /// </summary>
        public static bool HasTier<TTier, TDataCenter>(this ISettingValue<TTier, TDataCenter> value)
            where TTier : struct
            where TDataCenter : struct
        {
            return !Compare.IsDefault(value.Tier);
        }

        /// <summary>
        /// True if DataCenter is not the "Any" data center.
        /// </summary>
        public static bool HasDataCenter<TTier, TDataCenter>(this ISettingValue<TTier, TDataCenter> value)
            where TTier : struct
            where TDataCenter : struct
        {
            return !Compare.IsDefault(value.DataCenter);
        }

        /// <summary>
        /// Returns true if this value can be applied to the specified sub-app, tier, and data center.
        /// </summary>
        public static bool IsValidFor<TTier, TDataCenter>(this ISettingValue<TTier, TDataCenter> value, int? subAppId, TTier tier, TDataCenter dataCenter)
            where TTier : struct
            where TDataCenter : struct
        {
            if (value.HasSubApp() && subAppId != value.SubAppId)
                return false;

            if (value.HasTier() && !Compare.AreEqual(value.Tier, tier))
                return false;

            if (value.HasDataCenter() && !Compare.AreEqual(value.DataCenter, dataCenter))
                return false;

            return true;
        }

        /// <summary>
        /// Returns true if <paramref name="a"/> (this) is considered more specific (greater precedence) than <paramref name="b"/>.
        /// </summary>
        public static bool IsMoreSpecificThan<TTier, TDataCenter>(
            [CanBeNull] this ISettingValue<TTier, TDataCenter> a,
            [CanBeNull] ISettingValue<TTier, TDataCenter> b)
            where TTier : struct
            where TDataCenter : struct
        {
            if (a == null)
                return false;

            if (b == null)
                return true;

            // an override is always more specific than a default
            if (a.IsOverride != b.IsOverride)
                return a.IsOverride;

            // sub app has precedence over tier
            if (a.HasSubApp() != b.HasSubApp())
                return a.HasSubApp();

            // tier has precedence over data center
            if (a.HasTier() != b.HasTier())
                return a.HasTier();

            if (a.HasDataCenter() != b.HasDataCenter())
                return a.HasDataCenter();

            return false;
        }

        /// <summary>
        /// Returns true if the two ISettingValues share the same sub-app, tier, and data center.
        /// </summary>
        public static bool HasSameSubAppTierDataCenter<TTier, TDataCenter>(
            [NotNull] this ISettingValue<TTier, TDataCenter> a,
            [NotNull] ISettingValue<TTier, TDataCenter> b)
            where TTier : struct
            where TDataCenter : struct
        {
            return a.SubAppId == b.SubAppId && Compare.AreEqual(a.Tier, b.Tier) && Compare.AreEqual(a.DataCenter, b.DataCenter);
        }

        internal static int GetBestValueFor<TSettingValue, TTier, TDataCenter>(
            this IList<TSettingValue> values,
            int? subAppId,
            TTier tier,
            TDataCenter dataCenter,
            [CanBeNull] out ISettingValue<TTier, TDataCenter> bestValue)
            where TSettingValue : ISettingValue<TTier, TDataCenter>
            where TTier : struct
            where TDataCenter : struct
        {
            bestValue = null;
            var bestIndex = -1;
            for (var i = 0; i < values.Count; i++)
            {
                var val = values[i];
                if (val.IsValidFor(subAppId, tier, dataCenter) && val.IsMoreSpecificThan(bestValue))
                {
                    bestIndex = i;
                    bestValue = val;
                }
            }

            return bestIndex;
        }
    }
}