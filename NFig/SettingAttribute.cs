using System;

namespace NFig
{
    [AttributeUsage(AttributeTargets.Property)]
    public class SettingAttribute : Attribute
    {
        public object DefaultValue { get; }
        public bool IsEncrypted { get; protected set; }

        public SettingAttribute(object defaultValue)
        {
            DefaultValue = defaultValue;
        }
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class EncryptedSettingAttribute : SettingAttribute
    {
        public EncryptedSettingAttribute(object defaultValue) : base(defaultValue)
        {
            IsEncrypted = true;
        }
    }
}
