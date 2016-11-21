using System;

namespace NFig
{
    /// <summary>
    /// Used to mark a property as a setting.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class SettingAttribute : Attribute
    {
        /// <summary>
        /// The default value (SubApp=Global, Tier=Any, DataCenter=Any) which will be used anywhere that a more specific default is not found.
        /// </summary>
        public object DefaultValue { get; }
        /// <summary>
        /// Indicates whether the setting is encrypted.
        /// </summary>
        public bool IsEncrypted { get; protected set; }

        /// <summary>
        /// Marks a property as an NFig Setting. For an encrypted setting, use <see cref="EncryptedSettingAttribute"/> instead.
        /// </summary>
        /// <param name="defaultValue">
        /// The default value (SubApp=Global, Tier=Any, DataCenter=Any) which will be used anywhere that a more specific default is not found.
        /// </param>
        public SettingAttribute(object defaultValue)
        {
            DefaultValue = defaultValue;
        }
    }

    /// <summary>
    /// Used to mark a property as an encrypted setting.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class EncryptedSettingAttribute : SettingAttribute
    {
        /// <summary>
        /// Marks a property as an encrypted NFig Setting. For an unencrypted setting, use <see cref="SettingAttribute"/> instead. Encrypted settings do not
        /// allow a (SubApp=Global, Tier=Any, DataCenter=Any) default, other than the default value for the setting's type (default(T)).
        /// </summary>
        public EncryptedSettingAttribute() : base(null)
        {
            IsEncrypted = true;
        }
    }
}
