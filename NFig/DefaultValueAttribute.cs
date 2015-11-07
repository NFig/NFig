using System;

namespace NFig
{
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
    public abstract class DefaultSettingValueAttribute : Attribute
    {
        public object DefaultValue { get; set; }
        public object DataCenter { get; set; }
        public object Tier { get; set; }
        public bool AllowOverrides { get; set; } = true;
    }
}