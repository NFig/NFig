using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace NFig
{
    internal static class EnumConverters
    {
        private static readonly Dictionary<Type, ISettingConverter> _converters = new Dictionary<Type, ISettingConverter>();
        private static readonly object _lock = new object();
        private static readonly ModuleBuilder _moduleBuilder;

        static EnumConverters()
        {
            var name = "NFigEnumConverters_" + Guid.NewGuid();
            var asmName = new AssemblyName(name);
            var asmBuilder = AssemblyBuilder.DefineDynamicAssembly(asmName, AssemblyBuilderAccess.Run);
            _moduleBuilder = asmBuilder.DefineDynamicModule("Main");
        }

        public static ISettingConverter GetConverterFor(Type enumType)
        {
            if (_converters.TryGetValue(enumType, out var converter))
                return converter;

            lock (_lock)
            {
                if (_converters.TryGetValue(enumType, out converter))
                    return converter;

                converter = CreateConverter(enumType);
                _converters[enumType] = converter;
                return converter;
            }
        }

        private static ISettingConverter CreateConverter(Type enumType)
        {
            var ifaceType = typeof(ISettingConverter<>).MakeGenericType(enumType);
            var stringType = typeof(string);
            var underlyingType = enumType.GetEnumUnderlyingType();
            var toString = underlyingType.GetMethod("ToString", new Type[] { });
            var parse = underlyingType.GetMethod("Parse", new[] { stringType });

            var typeBuilder = _moduleBuilder.DefineType($"{enumType.FullName}_SettingConverter_{Guid.NewGuid()}",
                                TypeAttributes.Public |
                                TypeAttributes.Class |
                                TypeAttributes.AutoClass |
                                TypeAttributes.AnsiClass |
                                TypeAttributes.BeforeFieldInit |
                                TypeAttributes.AutoLayout,
                                null,
                                new[] { ifaceType });

            var getString = typeBuilder.DefineMethod("GetString", MethodAttributes.Public | MethodAttributes.Virtual, stringType, new[] { enumType });
            var il = getString.GetILGenerator();

            il.Emit(OpCodes.Ldarga, 1);      // [enumValue addr]
            il.Emit(OpCodes.Call, toString); // [stringValue]
            il.Emit(OpCodes.Ret);

            var getValue = typeBuilder.DefineMethod("GetValue", MethodAttributes.Public | MethodAttributes.Virtual, enumType, new[] { stringType });
            il = getValue.GetILGenerator();

            il.Emit(OpCodes.Ldarg_1);     // [stringValue]
            il.Emit(OpCodes.Call, parse); // [underlyingTypeValue]
            il.Emit(OpCodes.Ret);

            var converterType = typeBuilder.CreateType();
            return (ISettingConverter)Activator.CreateInstance(converterType);
        }
    }
}