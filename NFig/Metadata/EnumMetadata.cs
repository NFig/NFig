using System;
using System.Reflection;
using Newtonsoft.Json;

namespace NFig.Metadata
{
    /// <summary>
    /// Metadata about an enum type which may be useful in an admin panel.
    /// </summary>
    public class EnumMetadata
    {
        /// <summary>
        /// The fully qualified type name of the enum.
        /// </summary>
        public string Name { get; }
        /// <summary>
        /// True if the enum is marked with the [Flags] attribute.
        /// </summary>
        public bool IsFlags { get; }
        /// <summary>
        /// The values of the enum.
        /// </summary>
        public EnumValue[] Values { get; }

        [JsonConstructor]
        EnumMetadata(string name, bool isFlags, EnumValue[] values)
        {
            Name = name;
            IsFlags = isFlags;
            Values = values;
        }

        internal static EnumMetadata Create<TTier>(Type enumType, TTier tier) where TTier : struct
        {
            var fields = enumType.GetFields();
            var values = new EnumValue[fields.Length];
            var count = 0;
            foreach (var fi in fields)
            {
                if (!fi.IsStatic || !fi.IsLiteral)
                    continue;

                var attr = fi.GetCustomAttribute<OnlyVisibleOnTierAttribute>();
                if (attr != null && !attr.ContainsTier(tier))
                    continue;

                var value = fi.GetValue(null);
                var longValue = Convert.ToInt64(value);
                var name = value.ToString();

                values[count] = new EnumValue(longValue, name);
                count++;
            }

            var finalValues = new EnumValue[count];
            Array.Copy(values, finalValues, count);

            var isFlags = enumType.GetTypeInfo().GetCustomAttribute<FlagsAttribute>() != null;

            return new EnumMetadata(enumType.FullName, isFlags, finalValues);
        }
    }
}