using System;

namespace NFig
{
    /// <summary>
    /// Describes all information about an individual setting, except for its values.
    /// </summary>
    public class SettingMetadata : IBySettingDictionaryItem
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
        /// The runtime type of the setting's values.
        /// </summary>
        public Type Type { get; }

        /// <summary>
        /// The full name of the setting's type.
        /// </summary>
        public string TypeName => Type.FullName;

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
        /// True if the setting was marked with the [ChangeRequiresRestart] attribute. This indicates that any edits to this setting likely won't have any
        /// affect until the application is restarted.
        /// </summary>
        public bool ChangeRequiresRestart { get; }

        internal SettingMetadata(string name, string description, Type type, bool isEncrypted, bool changeRequiresRestart)
        {
            Name = name;
            Description = description;
            Type = type;
            IsEncrypted = isEncrypted;
            IsEnum = type.IsEnum;
            ChangeRequiresRestart = changeRequiresRestart;
        }
    }
}