using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using NFig.Infrastructure;

namespace NFig.Converters
{
    static class DefaultConverters
    {
        static readonly Dictionary<Type, ISettingConverter> s_defaultConverters = new Dictionary<Type, ISettingConverter>
        {
            [typeof(bool)] = new BooleanSettingConverter(),
            [typeof(byte)] = new ByteSettingConverter(),
            [typeof(short)] = new ShortSettingConverter(),
            [typeof(ushort)] = new UShortSettingConverter(),
            [typeof(int)] = new IntSettingConverter(),
            [typeof(uint)] = new UIntSettingConverter(),
            [typeof(long)] = new LongSettingConverter(),
            [typeof(ulong)] = new ULongSettingConverter(),
            [typeof(float)] = new FloatSettingConverter(),
            [typeof(double)] = new DoubleSettingConverter(),
            [typeof(string)] = new StringSettingConverter(),
            [typeof(char)] = new CharSettingConverter(),
            [typeof(decimal)] = new DecimalSettingConverter(),
        };

        [CanBeNull]
        internal static ISettingConverter Get(Type settingType)
        {
            lock (s_defaultConverters)
            {
                ISettingConverter converter;
                if (s_defaultConverters.TryGetValue(settingType, out converter))
                    return converter;

                if (settingType.IsEnum())
                {
                    converter = EnumConverters.GetConverter(settingType);
                    s_defaultConverters[settingType] = converter;
                    return converter;
                }

                return null;
            }
        }
    }
}