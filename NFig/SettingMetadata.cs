using System;
using Newtonsoft.Json;

namespace NFig
{
    /// <summary>
    /// Describes all information about an individual setting, except for its values.
    /// </summary>
    public class SettingMetadata : IBySettingItem, IEquatable<SettingMetadata>
    {
        /// <summary>
        /// The name of the setting. Dots in the name represent nesting levels.
        /// </summary>
        public string Name { get; }
        /// <summary>
        /// User-provided description of the setting.
        /// </summary>
        public string Description { get; }
        /// <summary>
        /// The full name of the setting's type.
        /// </summary>
        public string TypeName { get; }
        /// <summary>
        /// If true, all default and override values are encrypted.
        /// </summary>
        public bool IsEncrypted { get; }
        /// <summary>
        /// True if the setting type is an enum. Use integer values when setting overrides for enum settings (e.g. to assign the value MyEnum.Two, use "2", not
        /// "Two").
        /// </summary>
        public bool IsEnum { get; }
        /// <summary>
        /// The fully-qualified type name of the setting's converter. All built-in converters begin with "NFig.Converters."
        /// </summary>
        public string ConverterTypeName { get; }
        /// <summary>
        /// True if this setting uses the built-in default converter for the setting's type.
        /// </summary>
        public bool IsDefaultConverter { get; }
        /// <summary>
        /// True if the setting was marked with the [ChangeRequiresRestart] attribute. This indicates that any edits to this setting likely won't have any
        /// affect until the application is restarted.
        /// </summary>
        public bool ChangeRequiresRestart { get; }

        [JsonConstructor]
        internal SettingMetadata(
            string name,
            string description,
            string typeName,
            bool isEncrypted,
            bool isEnum,
            string converterTypeName,
            bool isDefaultConverter,
            bool changeRequiresRestart)
        {
            Name = name;
            Description = description;
            TypeName = typeName;
            IsEncrypted = isEncrypted;
            IsEnum = isEnum;
            ConverterTypeName = converterTypeName;
            IsDefaultConverter = isDefaultConverter;
            ChangeRequiresRestart = changeRequiresRestart;
        }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

        public static bool operator ==(SettingMetadata a, SettingMetadata b)
        {
            return ReferenceEquals(a, null) ? ReferenceEquals(b, null) : a.Equals(b);
        }

        public static bool operator !=(SettingMetadata a, SettingMetadata b)
        {
            return !(a == b);
        }

        public bool Equals(SettingMetadata other)
        {
            if (ReferenceEquals(null, other))
                return false;

            if (ReferenceEquals(this, other))
                return true;

            return Name == other.Name
                && Description == other.Description
                && TypeName == other.TypeName
                && IsEncrypted == other.IsEncrypted
                && IsEnum == other.IsEnum
                && ConverterTypeName == other.ConverterTypeName
                && IsDefaultConverter == other.IsDefaultConverter
                && ChangeRequiresRestart == other.ChangeRequiresRestart;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
                return false;

            if (ReferenceEquals(this, obj))
                return true;

            if (obj.GetType() != GetType())
                return false;

            return Equals((SettingMetadata)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (Name != null ? Name.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Description != null ? Description.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (TypeName != null ? TypeName.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ IsEncrypted.GetHashCode();
                hashCode = (hashCode * 397) ^ IsEnum.GetHashCode();
                hashCode = (hashCode * 397) ^ (ConverterTypeName != null ? ConverterTypeName.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ IsDefaultConverter.GetHashCode();
                hashCode = (hashCode * 397) ^ ChangeRequiresRestart.GetHashCode();
                return hashCode;
            }
        }

#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    }
}