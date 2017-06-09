using System;

namespace NFig
{
    /// <summary>
    /// An override is a value defined at runtime which takes precendence over default values.
    /// </summary>
    public class OverrideValue<TTier, TDataCenter> : ISettingValue<TTier, TDataCenter>, IEquatable<OverrideValue<TTier, TDataCenter>>
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
        /// The data center that this value applies to. DataCenter=Any means that the value can be applied to any data center.
        /// </summary>
        public TDataCenter DataCenter { get; }
        /// <summary>
        /// Indicated when the override is set to automatically expire, if applicable.
        /// </summary>
        public DateTimeOffset? ExpirationTime { get; }

        TTier ISettingValue<TTier, TDataCenter>.Tier => default(TTier);
        /// <summary>
        /// True if the value is an override (not a default).
        /// </summary>
        public bool IsOverride => true;

        // This constructor is used for deserialization. Make sure it includes all properties which need to be set.
        /// <summary>
        /// Instantiates a new override. Note: overrides always apply to the currently active tier.
        /// </summary>
        public OverrideValue(string name, string value, int? subAppId, TDataCenter dataCenter, DateTimeOffset? expirationtime)
        {
            Name = name;
            Value = value;
            SubAppId = subAppId;
            DataCenter = dataCenter;
            ExpirationTime = expirationtime;
        }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

        public static bool operator ==(OverrideValue<TTier, TDataCenter> a, OverrideValue<TTier, TDataCenter> b)
        {
            if (a == null)
                return b == null;

            return a.Equals(b);
        }

        public static bool operator !=(OverrideValue<TTier, TDataCenter> a, OverrideValue<TTier, TDataCenter> b)
        {
            if (a == null)
                return b != null;

            return !a.Equals(b);
        }

        public bool Equals(OverrideValue<TTier, TDataCenter> other)
        {
            if (ReferenceEquals(null, other))
                return false;

            if (ReferenceEquals(this, other))
                return true;

            return Name == other.Name
                   && Value == other.Value
                   && SubAppId == other.SubAppId
                   && Compare.AreEqual(DataCenter, other.DataCenter)
                   && ExpirationTime == other.ExpirationTime;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
                return false;

            if (ReferenceEquals(this, obj))
                return true;

            if (obj.GetType() != GetType())
                return false;

            return Equals((OverrideValue<TTier, TDataCenter>)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (Name != null ? Name.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Value != null ? Value.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ SubAppId.GetHashCode();
                hashCode = (hashCode * 397) ^ DataCenter.GetHashCode();
                hashCode = (hashCode * 397) ^ ExpirationTime.GetHashCode();
                return hashCode;
            }
        }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    }
}