using System;
using System.Collections.Generic;
using JetBrains.Annotations;

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
        static ISettingConverter Get(Type settingType)
        {
            return s_defaultConverters.TryGetValue(settingType, out var converter) ? converter : null;
        }
    }
}