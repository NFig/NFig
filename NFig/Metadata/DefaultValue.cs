using System;
using Newtonsoft.Json;
using NFig.Infrastructure;

namespace NFig.Metadata
{
    /// <summary>
    /// Represents a default value for an NFig setting. Defaults are defined at compile-time using attributes. They cannot be instantiated by consumers at
    /// runtime.
    /// </summary>
    public class DefaultValue<TTier, TDataCenter> : ISettingValue<TTier, TDataCenter>, IEquatable<DefaultValue<TTier, TDataCenter>>
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
        /// The ID of the sub-app that this value applies to. Null means that the default is applicable to the top-level application, as well as all sub-apps.
        /// </summary>
        public int? SubAppId { get; }
        /// <summary>
        /// The tier that this value applies to. Tier=Any means that the value can be applied to any tier.
        /// </summary>
        public TTier Tier { get; }
        /// <summary>
        /// The data center that this value applies to. DataCenter=Any means that the value can be applied to any data center.
        /// </summary>
        public TDataCenter DataCenter { get; }
        /// <summary>
        /// Indicates whether overrides are allowed when this default value is active.
        /// </summary>
        public bool AllowsOverrides { get; }
        /// <summary>
        /// True if the value is an override (not a default).
        /// </summary>
        public bool IsOverride => false;

        // This constructor is used for deserialization. Make sure it includes all properties which need to be set.
        [JsonConstructor]
        internal DefaultValue(string name, string value, int? subAppId, TTier tier, TDataCenter dataCenter, bool allowsOverrides)
        {
            Name = name;
            Value = value;
            SubAppId = subAppId;
            Tier = tier;
            DataCenter = dataCenter;
            AllowsOverrides = allowsOverrides;
        }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

        public static bool operator ==(DefaultValue<TTier, TDataCenter> a, DefaultValue<TTier, TDataCenter> b)
        {
            return Object.ReferenceEquals(a, null) ? Object.ReferenceEquals(b, null) : a.Equals(b);
        }

        public static bool operator !=(DefaultValue<TTier, TDataCenter> a, DefaultValue<TTier, TDataCenter> b)
        {
            return !(a == b);
        }

        public bool Equals(DefaultValue<TTier, TDataCenter> other)
        {
            if (Object.ReferenceEquals(null, other))
                return false;

            if (Object.ReferenceEquals(this, other))
                return true;

            return Name == other.Name
                && Value == other.Value
                && SubAppId == other.SubAppId
                && Compare.AreEqual(Tier, other.Tier)
                && Compare.AreEqual(DataCenter, other.DataCenter)
                && AllowsOverrides == other.AllowsOverrides;
        }

        public override bool Equals(object obj)
        {
            if (Object.ReferenceEquals(null, obj))
                return false;

            if (Object.ReferenceEquals(this, obj))
                return true;

            if (obj.GetType() != this.GetType()) return false;
            return Equals((DefaultValue<TTier, TDataCenter>)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (Name != null ? Name.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Value != null ? Value.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ SubAppId.GetHashCode();
                hashCode = (hashCode * 397) ^ Tier.GetHashCode();
                hashCode = (hashCode * 397) ^ DataCenter.GetHashCode();
                hashCode = (hashCode * 397) ^ AllowsOverrides.GetHashCode();
                return hashCode;
            }
        }

#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    }
}