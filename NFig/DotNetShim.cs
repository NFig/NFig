using System;
using System.Reflection;

namespace NFig
{
    static class DotNetShim
    {
        internal static bool IsEnum(this Type type)
        {
            return type.GetTypeInfo().IsEnum;
        }

        internal static bool IsClass(this Type type)
        {
            return type.GetTypeInfo().IsClass;
        }

        internal static bool IsValueType(this Type type)
        {
            return type.GetTypeInfo().IsValueType;
        }

        internal static bool IsPrimitive(this Type type)
        {
            return type.GetTypeInfo().IsPrimitive;
        }

        internal static bool IsPublic(this Type type)
        {
            return type.GetTypeInfo().IsPublic;
        }

        internal static bool IsNestedPublic(this Type type)
        {
            return type.GetTypeInfo().IsNestedPublic;
        }

        internal static bool IsGenericType(this Type type)
        {
            return type.GetTypeInfo().IsGenericType;
        }

        internal static Module Module(this Type type)
        {
            return type.GetTypeInfo().Module;
        }

        internal static Type GetEnumUnderlyingType(this Type enumType)
        {
            // This implementation is an almost verbatim copy of the .NET Framework's implementation of Type.GetEnumUnderlyingType().
            // https://referencesource.microsoft.com/#mscorlib/system/type.cs,1468

            if (!enumType.IsEnum())
                throw new ArgumentException("Type provided must be an Enum.", nameof(enumType));

            var fields = enumType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (fields == null || fields.Length != 1)
                throw new ArgumentException("Invalid enum type.", nameof(enumType));

            return fields[0].FieldType;
        }
    }
}