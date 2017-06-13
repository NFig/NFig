using System;
using System.Collections.Generic;
using System.Threading;

namespace NFig
{
    static class MockRedis
    {
        public delegate void SubscribeCallback(string channel, string message);

        static readonly Dictionary<string, Value> s_database = new Dictionary<string, Value>();

        class MultiCommand : IDisposable
        {
            public void Dispose()
            {
                Monitor.Exit(s_database);
            }
        }

        public static IDisposable Multi()
        {
            Monitor.Enter(s_database);
            return new MultiCommand();
        }

        /* ================================================================================================================================================== *
         * Pub/Sub
         * ================================================================================================================================================== */

        public static void Subscribe(string channel, SubscribeCallback callback)
        {
            throw new NotImplementedException();
        }

        public static void Publish(string channel, string message)
        {
            throw new NotImplementedException();
        }

        /* ================================================================================================================================================== *
         * Strings
         * ================================================================================================================================================== */

        public static bool Set(string key, string value)
        {
            lock (s_database)
            {
                s_database[key] = new Value(DataType.String, value);
                return true;
            }
        }

        public static string Get(string key)
        {
            lock (s_database)
            {
                if (s_database.TryGetValue(key, out var valueObj))
                {
                    if (valueObj.Type != DataType.String)
                        throw new InvalidOperationException($"The value stored at {key} was not a string");

                    return (string)valueObj.Data;
                }

                return null;
            }
        }

        /* ================================================================================================================================================== *
         * Sets
         * ================================================================================================================================================== */

        public static bool SetAdd(string key, string value)
        {
            lock (s_database)
            {
                HashSet<string> data;
                if (s_database.TryGetValue(key, out var valueObj))
                {
                    if (valueObj.Type != DataType.Set)
                        throw new InvalidOperationException($"Key {key} is not a Set");

                    data = (HashSet<string>)valueObj.Data;
                }
                else
                {
                    data = new HashSet<string>();
                    s_database[key] = new Value(DataType.Set, data);
                }

                return data.Add(value);
            }
        }

        public static string[] SetMembers(string key)
        {
            lock (s_database)
            {
                if (s_database.TryGetValue(key, out var valueObj))
                {
                    if (valueObj.Type != DataType.Set)
                        throw new InvalidOperationException($"Key {key} is not a Set");

                    var data = (HashSet<string>)valueObj.Data;

                    var result = new string[data.Count];
                    var i = 0;
                    foreach (var val in data)
                    {
                        result[i] = val;
                        i++;
                    }

                    return result;
                }

                return Array.Empty<string>();
            }
        }

        /* ================================================================================================================================================== *
         * Sorted Sets
         * ================================================================================================================================================== */

        //

        /* ================================================================================================================================================== *
         * Lists
         * ================================================================================================================================================== */

        //

        /* ================================================================================================================================================== *
         * Hashes
         * ================================================================================================================================================== */

        public static void HashSet(string key, string hashKey, string value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            lock (s_database)
            {
                Dictionary<string, string> hash;
                if (s_database.TryGetValue(key, out var valueObj))
                {
                    if (valueObj.Type != DataType.Hash)
                        throw new InvalidOperationException($"Key {key} is not a Hash");

                    hash = (Dictionary<string, string>)valueObj.Data;
                }
                else
                {
                    hash = new Dictionary<string, string>();
                    s_database[key] = new Value(DataType.Hash, hash);
                }

                hash[hashKey] = value;
            }
        }

        public static bool HashDelete(string key, string hashKey)
        {
            if (hashKey == null)
                throw new ArgumentNullException(nameof(hashKey));

            lock (s_database)
            {
                if (s_database.TryGetValue(key, out var valueObj))
                {
                    if (valueObj.Type != DataType.Hash)
                        throw new InvalidOperationException($"Key {key} is not a Hash");

                    var hash = (Dictionary<string, string>)valueObj.Data;
                    return hash.Remove(hashKey);
                }

                return false;
            }
        }

        public static Dictionary<string, string> HashGetAll(string key)
        {
            lock (s_database)
            {
                if (s_database.TryGetValue(key, out var valueObj))
                {
                    if (valueObj.Type != DataType.Hash)
                        throw new InvalidOperationException($"Key {key} is not a Hash");

                    var hash = (Dictionary<string, string>)valueObj.Data;
                    return new Dictionary<string, string>(hash);
                }

                return new Dictionary<string, string>();
            }
        }

        /* ================================================================================================================================================== *
         * Data
         * ================================================================================================================================================== */

        enum DataType
        {
            Unknown = 0,
            String,
            Set,
            SortedSet,
            List,
            Hash,
        }

        class Value
        {
            public DataType Type { get; }
            public object Data { get; }

            public Value(DataType type, object data)
            {
                Type = type;
                Data = data;
            }
        }
    }
}