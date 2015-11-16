using System;

namespace NFig
{
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
    public abstract class DefaultSettingValueAttribute : Attribute
    {
        public object DefaultValue { get; protected set; }
        public object DataCenter { get; protected set; }
        public object Tier { get; protected set; }
        public bool AllowOverrides { get; protected set; } = true;
    }
}