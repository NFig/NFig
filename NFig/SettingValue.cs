using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace NFig
{
    [SuppressMessage("ReSharper", "StaticMemberInGenericType", Justification = "Yes, I know it's a static member in a generic type. The fields are also generic.")]
    public class SettingValue<TTier, TDataCenter>
        where TTier : struct
        where TDataCenter : struct
    {
        private static readonly EqualityComparer<TTier> s_tierComparer = EqualityComparer<TTier>.Default;
        private static readonly EqualityComparer<TDataCenter> s_dataCenterComparer = EqualityComparer<TDataCenter>.Default;

        public string Name { get; }
        public string Value { get; }
        public TTier Tier { get; }
        public TDataCenter DataCenter { get; }
        public bool IsDefault { get; }

        public bool IsOverride => !IsDefault;
        public bool HasTier => !s_tierComparer.Equals(Tier, default(TTier));
        public bool HasDataCenter => !s_dataCenterComparer.Equals(DataCenter, default(TDataCenter));

        public SettingValue(string name, string value, TTier tier, TDataCenter dataCenter)
            : this(name, value, tier, dataCenter, false)
        {
        }

        internal SettingValue(string name, string value, TTier tier, TDataCenter dataCenter, bool isDefault)
        {
            Name = name;
            Value = value;
            Tier = tier;
            DataCenter = dataCenter;
            IsDefault = isDefault;
        }

        public bool IsValidFor(TTier tier, TDataCenter dataCenter)
        {
            if (HasTier && !s_tierComparer.Equals(Tier, tier))
                return false;

            if (HasDataCenter && !s_dataCenterComparer.Equals(DataCenter, dataCenter))
                return false;

            return true;
        }

        public bool IsMoreSpecificThan(SettingValue<TTier, TDataCenter> value)
        {
            // an override is always more specific than a default
            if (IsOverride && value.IsDefault)
                return true;

            // tier is considered more important than dc, so this check is first
            if (HasTier != value.HasTier)
                return HasTier;

            if (HasDataCenter != value.HasDataCenter)
                return HasDataCenter;

            return false;
        }
        
        public bool HasSameTierAndDataCenter(SettingValue<TTier, TDataCenter> value)
        {
            return s_tierComparer.Equals(Tier, value.Tier) && s_dataCenterComparer.Equals(DataCenter, value.DataCenter);
        }
    }
}
