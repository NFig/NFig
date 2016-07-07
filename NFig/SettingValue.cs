using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace NFig
{
    [SuppressMessage("ReSharper", "StaticMemberInGenericType", Justification = "Yes, I know it's a static member in a generic type. The fields are also generic.")]
    public class SettingValue<TTier, TDataCenter>
        where TTier : struct
        where TDataCenter : struct
    {
        public string Name { get; }
        public string Value { get; }
        public TTier Tier { get; }
        public TDataCenter DataCenter { get; }
        public bool IsDefault { get; }
        public bool AllowsOverrides { get; }

        public bool IsOverride => !IsDefault;
        public bool HasTier => !Compare.IsDefault(Tier);
        public bool HasDataCenter => !Compare.IsDefault(DataCenter);

        public SettingValue(string name, string value, TDataCenter dataCenter)
            : this(name, value, default(TTier), dataCenter, false, false)
        {
        }

        internal SettingValue(string name, string value, TTier tier, TDataCenter dataCenter, bool isDefault, bool allowsOverrides)
        {
            Name = name;
            Value = value;
            Tier = tier;
            DataCenter = dataCenter;
            IsDefault = isDefault;
            AllowsOverrides = allowsOverrides;
        }

        public bool IsValidFor(TTier tier, TDataCenter dataCenter)
        {
            if (HasTier && !Compare.AreEqual(Tier, tier))
                return false;

            if (HasDataCenter && !Compare.AreEqual(DataCenter, dataCenter))
                return false;

            return true;
        }

        public bool IsMoreSpecificThan(SettingValue<TTier, TDataCenter> value)
        {
            if (value == null)
                return true;

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
            return Compare.AreEqual(Tier, value.Tier) && Compare.AreEqual(DataCenter, value.DataCenter);
        }
    }
}
