using System;

namespace Nfig
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
