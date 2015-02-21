using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using StackExchange.Redis;

namespace NFig.Redis
{

    public class NFigRedisStore<TSettings, TTier, TDataCenter>
        where TSettings : class, new()
        where TTier : struct
        where TDataCenter : struct
    {
        public delegate void SettingsUpdateDelegate(Exception ex, string appName, TSettings settings, NFigRedisStore<TSettings, TTier, TDataCenter> nfigRedisStore);

        private const string APP_UPDATE_CHANNEL = "NFig-AppUpdate";

        private readonly ConnectionMultiplexer _redis;
        private readonly ISubscriber _subscriber;
        private readonly int _db;
        private readonly SettingsManager<TSettings, TTier, TDataCenter> _manager;

        private readonly object _callbacksLock = new object();
        private readonly Dictionary<string, SettingsUpdateDelegate> _callbacks = new Dictionary<string, SettingsUpdateDelegate>();

        public IReadOnlyDictionary<string, SettingsUpdateDelegate> RegisteredCallbacks { get { return new ReadOnlyDictionary<string, SettingsUpdateDelegate>(_callbacks); } }

        public TTier Tier { get { return _manager.Tier; } }
        public TDataCenter DataCenter { get { return _manager.DataCenter; } }

        public NFigRedisStore(
            string redisConnectionString, 
            int db, 
            TTier tier, 
            TDataCenter dataCenter, 
            Dictionary<Type, SettingConverterAttribute> additionalDefaultConverters = null
        )
        : this (
            ConnectionMultiplexer.Connect(redisConnectionString),
            db,
            tier,
            dataCenter,
            additionalDefaultConverters
        )
        {
        }

        public NFigRedisStore(
            ConnectionMultiplexer redisConnection, 
            int db, 
            TTier tier, 
            TDataCenter dataCenter, 
            Dictionary<Type, SettingConverterAttribute> additionalDefaultConverters = null
            )
        {
            _redis = redisConnection;
            _subscriber = _redis.GetSubscriber();
            _db = db;
            _manager = new SettingsManager<TSettings, TTier, TDataCenter>(tier, dataCenter, additionalDefaultConverters);
        }

        public void SubscribeToAppSettings(string appName, SettingsUpdateDelegate callback, bool overrideExisting = false)
        {
            lock (_callbacksLock)
            {
                SettingsUpdateDelegate existing;
                if (_callbacks.TryGetValue(appName, out existing))
                {
                    if (!overrideExisting && existing != callback)
                        throw new InvalidOperationException("Already subscribed to settings for app: " + appName + ". Only one callback can be subscribed at a time.");

                    return;
                }

                // set up a redis subscription
                _subscriber.Subscribe(APP_UPDATE_CHANNEL, OnAppUpdate);
                ReloadAndNotifyCallback(appName, callback);
            }
        }

        /// <summary>
        /// Unsubscribes from app settings updates.
        /// Note that there is a potential race condition if you unsibscribe while an update is in progress, the prior callback may still get called.
        /// </summary>
        /// <param name="appName">The name of the app.</param>
        /// <param name="callback">(optional) If null, any callback will be removed. If specified, the current callback will only be removed if it is equal to this param.</param>
        /// <returns>True if a callback was removed, otherwise false.</returns>
        public bool UnsubscribeFromAppSettings(string appName, SettingsUpdateDelegate callback = null)
        {
            lock (_callbacksLock)
            {
                SettingsUpdateDelegate existing;
                if (_callbacks.TryGetValue(appName, out existing))
                {
                    if (callback == null || callback == existing)
                    {
                        _callbacks.Remove(appName);
                        return true;
                    }
                }

                return false;
            }
        }

        public TSettings GetSettingsFromRedis(string appName)
        {
            return GetSettingsFromRedisAsync(appName).Result;
        }

        private static readonly Regex s_keyRegex = new Regex(@"^:(?<Tier>\d+):(?<DataCenter>\d+);(?<Name>.+)$");
        public async Task<TSettings> GetSettingsFromRedisAsync(string appName)
        {
            var tierType = typeof(TTier);
            var dataCenterType = typeof(TDataCenter);

            // grab the redis hash
            var db = _redis.GetDatabase(_db);
            var hash = await db.HashGetAllAsync(appName);
            var overrides = new List<SettingOverride<TTier, TDataCenter>>();
            foreach (var hashEntry in hash)
            {
                string key = hashEntry.Name;
                var match = s_keyRegex.Match(key);
                if (match.Success)
                {
                    overrides.Add(new SettingOverride<TTier, TDataCenter>()
                    {
                        Name = match.Groups["Name"].Value,
                        Value = hashEntry.Value,
                        Tier = (TTier)Enum.ToObject(tierType, int.Parse(match.Groups["Tier"].Value)),
                        DataCenter = (TDataCenter)Enum.ToObject(dataCenterType, int.Parse(match.Groups["DataCenter"].Value))
                    });
                }
            }

            // create new settings object
            return _manager.GetAppSettings(overrides);
        }

        private void OnAppUpdate(RedisChannel channel, RedisValue message)
        {
            if (channel == APP_UPDATE_CHANNEL)
            {
                SettingsUpdateDelegate callback;
                if (_callbacks.TryGetValue(message, out callback))
                {
                    ReloadAndNotifyCallback(message, callback);
                }
            }
        }

        private void ReloadAndNotifyCallback(string appName, SettingsUpdateDelegate callback)
        {
            Exception ex = null;
            TSettings settings = null;
            try
            {
                settings = GetSettingsFromRedis(appName);
            }
            catch(Exception e)
            {
                ex = e;
            }

            callback(ex, appName, settings, this);
        }
    }
}
