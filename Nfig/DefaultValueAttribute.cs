using System;

namespace Nfig
{
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
    public abstract class DefaultSettingValueAttribute : Attribute
    {
        public object DefaultValue { get; set; }
        public object DataCenter { get; set; }
        public object Tier { get; set; }
    }
}