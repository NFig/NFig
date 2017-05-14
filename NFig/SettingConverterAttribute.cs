using System;
using System.Linq;
using JetBrains.Annotations;

namespace NFig
{
    /// <summary>
    /// Used to mark individual settings as using a specific converter.
    /// </summary>
    public class SettingConverterAttribute : Attribute
    {
        /// <summary>
        /// The converter object. It will implement <see cref="ISettingConverter{TValue}"/> where TValue is the property type of the setting.
        /// </summary>
        public ISettingConverter Converter { get; }

        /// <summary>
        /// Explicitly assigns a converter to a specific setting. If you want a converter to automatically apply to any setting of a particular type, pass the
        /// converter as part of the "additionalDefaultConverters" argument to the NFigStoreOld you're using.
        /// </summary>
        /// <param name="converterType">The type must implement <see cref="ISettingConverter{TValue}"/> where TValue is the property type of the setting.</param>
        public SettingConverterAttribute(Type converterType)
        {
            ValidateConverterType(converterType);
            Converter = (ISettingConverter)Activator.CreateInstance(converterType);
        }

        /// <summary>
        /// Explicitly assigns a converter to a specific setting. If you want a converter to automatically apply to any setting of a particular type, pass the
        /// converter as part of the "additionalDefaultConverters" argument to the NFigStoreOld you're using.
        /// </summary>
        /// <param name="converter">
        /// An instance of <see cref="ISettingConverter"/>. The concrete type must implement  <see cref="ISettingConverter{TValue}"/>
        /// where TValue is the property type of the setting.
        /// </param>
        protected SettingConverterAttribute([NotNull] ISettingConverter converter)
        {
            if (converter == null)
            {
                throw new ArgumentNullException(nameof(converter));
            }

            ValidateConverterType(converter.GetType());

            Converter = converter;
        }

        static void ValidateConverterType(Type converterType)
        {
            // make sure type implements SettingsConverter<>
            var genericType = typeof(ISettingConverter<>);
            if (!converterType.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == genericType))
            {
                throw new InvalidOperationException($"Cannot use type {converterType.Name} as a setting converter. It does not implement ISettingConverter<T>.");
            }
        }
    }
}