using System;
using JetBrains.Annotations;

namespace NFig
{
    /// <summary>
    /// Used to declare custom converters.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class|AttributeTargets.Property, AllowMultiple = true)]
    public class SettingConverterAttribute : Attribute
    {
        /// <summary>
        /// The type which the converter will be applied to.
        /// </summary>
        public Type SettingType { get; }
        /// <summary>
        /// The converter object. It will implement <see cref="ISettingConverter{TValue}"/> where TValue is the property type of the setting.
        /// </summary>
        public ISettingConverter Converter { get; }

        /// <summary>
        /// If placed on an individual setting, this attribute declares a custom converter for that setting. If placed on a class, it declares a default
        /// converter which applies to all child settings of that class.
        /// </summary>
        /// <param name="converterType">The type must implement <see cref="ISettingConverter{TValue}"/> where TValue is the property type of the setting.</param>
        public SettingConverterAttribute(Type converterType)
        {
            SettingType = GetSettingType(converterType);
            Converter = (ISettingConverter)Activator.CreateInstance(converterType);
        }

        /// <summary>
        /// This protected constructor is intended to be used for more complicated converters which require special initialization. Define your own child
        /// attribute to perform the initialization and call this constructor.
        /// </summary>
        /// <param name="converter">
        /// An instance of <see cref="ISettingConverter"/>. The concrete type must implement  <see cref="ISettingConverter{TValue}"/>
        /// where TValue is the property type of the setting.
        /// </param>
        protected SettingConverterAttribute([NotNull] ISettingConverter converter)
        {
            if (converter == null)
                throw new ArgumentNullException(nameof(converter));

            SettingType = GetSettingType(converter.GetType());
            Converter = converter;
        }

        static Type GetSettingType(Type converterType)
        {
            // make sure type implements SettingsConverter<>
            var genericType = typeof(ISettingConverter<>);

            foreach (var iface in converterType.GetInterfaces())
            {
                if (iface.IsGenericType && iface.GetGenericTypeDefinition() == genericType)
                {
                    return iface.GenericTypeArguments[0];
                }
            }

            throw new InvalidOperationException($"Cannot use type {converterType.Name} as a setting converter. It does not implement ISettingConverter<T>.");
        }
    }
}