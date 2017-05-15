using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using NFig.Converters;

namespace NFig
{
    static class EnumConverters
    {
        class EnumConverter<T> : ISettingConverter<T>
        {
            readonly Func<T, string> _getString;
            readonly Func<string, T> _getValue;

            internal EnumConverter(Func<T, string> getString, Func<string, T> getValue)
            {
                _getString = getString;
                _getValue = getValue;
            }

            public string GetString(T value)
            {
                return _getString(value);
            }

            public T GetValue(string s)
            {
                return _getValue(s);
            }
        }

        static readonly Dictionary<Type, ISettingConverter> s_converters = new Dictionary<Type, ISettingConverter>();
        static readonly object s_lock = new object();

        public static ISettingConverter GetConverter<TEnum>()
        {
            var enumType = typeof(TEnum);

            ISettingConverter converter;
            if (s_converters.TryGetValue(enumType, out converter))
                return converter;

            lock (s_lock)
            {
                if (s_converters.TryGetValue(enumType, out converter))
                    return converter;

                converter = CreateConverter<TEnum>();
                s_converters[enumType] = converter;
                return converter;
            }
        }

        static ISettingConverter<TEnum> CreateConverter<TEnum>()
        {
            var enumType = typeof(TEnum);
            var stringType = typeof(string);
            var underlyingType = enumType.GetEnumUnderlyingType();
            var toString = underlyingType.GetMethod("ToString", new Type[] { });
            var parse = underlyingType.GetMethod("Parse", new[] { stringType });

            var getStringBuilder = new DynamicMethod($"Dynamic:EnumConverter<{enumType.Name}>.getString", stringType, new[] {enumType});
            var il = getStringBuilder.GetILGenerator();

            il.Emit(OpCodes.Ldarga, 0);      // [enumValue addr]
            il.Emit(OpCodes.Call, toString); // [stringValue]
            il.Emit(OpCodes.Ret);

            var getString = (Func<TEnum, string>)getStringBuilder.CreateDelegate(typeof(Func<TEnum, string>));

            var getValueBuilder = new DynamicMethod($"Dynamic:EnumConverter<{enumType.Name}>.getValue", enumType, new[] {stringType});
            il = getValueBuilder.GetILGenerator();

            il.Emit(OpCodes.Ldarg_0);     // [stringValue]
            il.Emit(OpCodes.Call, parse); // [underlyingTypeValue]
            il.Emit(OpCodes.Ret);

            var getValue = (Func<string, TEnum>)getValueBuilder.CreateDelegate(typeof(Func<string, TEnum>));

            return new EnumConverter<TEnum>(getString, getValue);
        }
    }
}