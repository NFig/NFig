using System;

namespace Nfig
{
    [AttributeUsage(AttributeTargets.Property)]
    public class SettingAttribute : Attribute
    {
        public object DefaultValue { get; set; }
        public bool IsRequired { get; set; }

        public SettingAttribute(object defaultValue)
        {
            DefaultValue = defaultValue;
        }
    }

    public class RequiredSettingAttribute : SettingAttribute
    {
        public RequiredSettingAttribute() : base(null)
        {
            IsRequired = true;
        }
    }
}
