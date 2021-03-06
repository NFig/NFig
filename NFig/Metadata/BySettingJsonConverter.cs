﻿using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Newtonsoft.Json;
using NFig;
using NFig.Infrastructure;

namespace NFig.Metadata
{
    /// <summary>
    /// Used to JSON serialize and deserialize <see cref="BySetting{TValue}"/> and <see cref="ListBySetting{TValue}"/> objects.
    /// </summary>
    public class BySettingJsonConverter : JsonConverter
    {
        readonly Dictionary<Type, ReflectionCache> _cacheByObjectType = new Dictionary<Type, ReflectionCache>();
        ReflectionCache _cache;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
                return;
            }

            var cache = GetReflectionCache(value.GetType());
            cache.Write(writer, serializer, value);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
            {
                reader.Read();
                return null;
            }

            var cache = GetReflectionCache(objectType);
            return cache.Read(reader, serializer);
        }

        public override bool CanConvert(Type objectType)
        {
            return GetMode(objectType) != Mode.Unknown;
        }

#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

        static void WriteBySetting<T>(JsonWriter w, JsonSerializer serializer, BySetting<T> value) where T : IBySettingItem
        {
            w.WriteStartObject();

            foreach (var kvp in value)
            {
                w.WritePropertyName(kvp.Key);
                serializer.Serialize(w, kvp.Value);
            }

            w.WriteEndObject();
        }

        static void WriteListBySetting<T>(JsonWriter w, JsonSerializer serializer, ListBySetting<T> value) where T : IBySettingItem
        {
            w.WriteStartObject();

            foreach (var kvp in value)
            {
                w.WritePropertyName(kvp.Key);
                w.WriteStartArray();
                foreach (var item in kvp.Value)
                {
                    serializer.Serialize(w, item);
                }
                w.WriteEndArray();
            }

            w.WriteEndObject();
        }

        static BySetting<T> ReadBySetting<T>(JsonReader r, JsonSerializer serializer) where T : IBySettingItem
        {
            if (r.TokenType != JsonToken.StartObject)
                throw new JsonSerializationException($"Expected '{{' but got token type {r.TokenType}");

            r.Read(); // consume {

            var items = new List<T>();
            while (r.TokenType == JsonToken.PropertyName)
            {
                r.Read(); // consume property name
                var item = serializer.Deserialize<T>(r);
                items.Add(item);

                if (r.TokenType != JsonToken.EndObject)
                    throw new JsonSerializationException($"Expected '}}' but got token type {r.TokenType}");

                r.Read(); // consume } from the end of the item we just deserialized
            }

            if (r.TokenType != JsonToken.EndObject)
                throw new JsonSerializationException($"Expected '}}' but got token type {r.TokenType}");

            // leave the } unconsumed

            return new BySetting<T>(items);
        }

        static ListBySetting<T> ReadListBySetting<T>(JsonReader r, JsonSerializer serializer) where T : IBySettingItem
        {
            if (r.TokenType != JsonToken.StartObject)
                throw new JsonSerializationException($"Expected '{{' but got token type {r.TokenType}");

            r.Read(); // consume {

            var items = new List<T>();
            while (r.TokenType == JsonToken.PropertyName)
            {
                r.Read(); // consume property name

                if (r.TokenType != JsonToken.StartArray)
                    throw new JsonSerializationException($"Expected '[' but got token type {r.TokenType}");

                r.Read(); // consume [

                while (r.TokenType == JsonToken.StartObject)
                {
                    var item = serializer.Deserialize<T>(r);
                    items.Add(item);

                    if (r.TokenType != JsonToken.EndObject)
                        throw new JsonSerializationException($"Expected '}}' but got token type {r.TokenType}");

                    r.Read(); // consume } from the end of the item we just deserialized
                }

                if (r.TokenType != JsonToken.EndArray)
                    throw new JsonSerializationException($"Expected '[' but got token type {r.TokenType}");

                r.Read(); // consume ]
            }

            if (r.TokenType != JsonToken.EndObject)
                throw new JsonSerializationException($"Expected '}}' but got token type {r.TokenType}");

            // leave the } unconsumed

            return new ListBySetting<T>(items);
        }

        ReflectionCache GetReflectionCache(Type objectType)
        {
            var cache = _cache;

            if (cache != null && cache.ObjectType == objectType)
                return cache;

            lock (_cacheByObjectType)
            {
                if (!_cacheByObjectType.TryGetValue(objectType, out cache))
                {
                    cache = new ReflectionCache(objectType);
                    _cacheByObjectType[objectType] = cache;
                }

                _cache = cache;
                return cache;
            }
        }

        static Mode GetMode(Type objectType)
        {
            var genericType = objectType.GetGenericTypeDefinition();

            if (genericType == typeof(BySetting<>))
                return Mode.BySetting;

            if (genericType == typeof(ListBySetting<>))
                return Mode.ListBySetting;

            return Mode.Unknown;
        }

        enum Mode
        {
            Unknown,
            BySetting,
            ListBySetting,
        }

        class ReflectionCache
        {
            public Type ObjectType { get; }
            public Action<JsonWriter, JsonSerializer, object> Write { get; }
            public Func<JsonReader, JsonSerializer, object> Read { get; }

            public ReflectionCache(Type objectType)
            {
                var mode = GetMode(objectType);
                if (mode == Mode.Unknown)
                    throw new InvalidOperationException($"{nameof(BySettingJsonConverter)} cannot be used to serialize/deserialize {objectType.Name}");

                ObjectType = objectType;

                var tValue = objectType.GenericTypeArguments[0];
                var module = GetType().Module();

                Write = CreateWriteDelegate(mode, objectType, tValue, module);
                Read = CreateReadDelegate(mode, tValue, module);
            }

            static Action<JsonWriter, JsonSerializer, object> CreateWriteDelegate(Mode mode, Type objectType, Type tValue, Module module)
            {
                var methodName = mode == Mode.BySetting ? nameof(WriteBySetting) : nameof(WriteListBySetting);
                var methodInfo = GetGenericMethod(methodName, tValue);

                var dmName = $"Invoke_{methodName}<{tValue.FullName}>";
                var dm = new DynamicMethod(dmName, typeof(void), new[] { typeof(JsonWriter), typeof(JsonSerializer), typeof(object) }, module, true);
                var il = dm.GetILGenerator();

                il.Emit(OpCodes.Ldarg_0);               // [writer]
                il.Emit(OpCodes.Ldarg_1);               // [writer] [serializer]
                il.Emit(OpCodes.Ldarg_2);               // [writer] [serializer] [object value]
                il.Emit(OpCodes.Castclass, objectType); // [writer] [serializer] [value]
                il.Emit(OpCodes.Call, (MethodInfo)methodInfo);      // empty
                il.Emit(OpCodes.Ret);

                return (Action<JsonWriter, JsonSerializer, object>)dm.CreateDelegate(typeof(Action<JsonWriter, JsonSerializer, object>));
            }

            static Func<JsonReader, JsonSerializer, object> CreateReadDelegate(Mode mode, Type tValue, Module module)
            {
                var methodName = mode == Mode.BySetting ? nameof(ReadBySetting) : nameof(ReadListBySetting);
                var methodInfo = GetGenericMethod(methodName, tValue);

                var dmName = $"Invoke_{methodName}<{tValue.FullName}>";
                var dm = new DynamicMethod(dmName, typeof(object), new[] { typeof(JsonReader), typeof(JsonSerializer) }, module, true);
                var il = dm.GetILGenerator();

                il.Emit(OpCodes.Ldarg_0);               // [reader]
                il.Emit(OpCodes.Ldarg_1);               // [reader] [serializer]
                il.Emit(OpCodes.Call, (MethodInfo)methodInfo);      // [value]
                il.Emit(OpCodes.Ret);

                return (Func<JsonReader, JsonSerializer, object>)dm.CreateDelegate(typeof(Func<JsonReader, JsonSerializer, object>));
            }

            static MethodInfo GetGenericMethod(string name, Type tValue)
            {
                var mi = typeof(BySettingJsonConverter).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic);
                return mi.MakeGenericMethod(tValue);
            }
        }
    }
}