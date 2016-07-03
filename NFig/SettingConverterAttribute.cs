using System;
using System.Linq;

namespace NFig
{
    public class SettingConverterAttribute : Attribute
    {
        public object Converter { get; private set; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="converterType">The type must implement SettingConverter&lt;T&gt; where T is the property type of the setting.</param>
        public SettingConverterAttribute(Type converterType)
        {
            // make sure type implements SettingsConverter<>
            var genericType = typeof(ISettingConverter<>);
            if (!converterType.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == genericType))
            {
                throw new InvalidOperationException("Cannot use type " + converterType.Name + " as a setting converter. It does not implement SettingConverter<T>.");
            }

            Converter = Activator.CreateInstance(converterType);
        }
    }
}