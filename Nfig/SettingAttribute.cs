using System;

namespace NFig
{
    [AttributeUsage(AttributeTargets.Property)]
    public class SettingAttribute : Attribute
    {
        public object DefaultValue { get; set; }

        public SettingAttribute(object defaultValue)
        {
            DefaultValue = defaultValue;
        }
    }
}
