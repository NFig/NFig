using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using StackExchange.Redis;

namespace NFig.Redis
{

    public class NFigRedisStore<TSettings, TTier, TDataCenter>
        where TSettings : class, INFigRedisSettings, new()
        where TTier : struct
        where TDataCenter : struct
    {
        public delegate void SettingsUpdateDelegate(Exception ex, TSettings settings, NFigRedisStore<TSettings, TTier, TDataCenter> nfigRedisStore);

        private const string APP_UPDATE_CHANNEL = "NFig-AppUpdate";
        private const string COMMIT_KEY = "$commit";

        private readonly ConnectionMultiplexer _redis;
        private readonly ISubscriber _subscriber;
        private readonly int _dbIndex;
        private readonly SettingsManager<TSettings, TTier, TDataCenter> _manager;

        private readonly object _callbacksLock = new object();
        private readonly Dictionary<string, SettingsUpdateDelegate> _callbacks = new Dictionary<string, SettingsUpdateDelegate>();

        public IReadOnlyDictionary<string, SettingsUpdateDelegate> RegisteredCallbacks { get { return new ReadOnlyDictionary<string, SettingsUpdateDelegate>(_callbacks); } }

        public TTier Tier { get { return _manager.Tier; } }
        public TDataCenter DataCenter { get { return _manager.DataCenter; } }

        public NFigRedisStore(
            string redisConnectionString, 
            int dbIndex, 
            TTier tier, 
            TDataCenter dataCenter, 
            Dictionary<Type, SettingConverterAttribute> additionalDefaultConverters = null
            )
        : this (
            ConnectionMultiplexer.Connect(redisConnectionString),
            dbIndex,
            tier,
            dataCenter,
            additionalDefaultConverters
        )
        {
        }

        // The reason this constructor is private and there is a public static method wrapper is so the calling dll isn't required to reference to SE.Redis.
        private NFigRedisStore(
            ConnectionMultiplexer redisConnection, 
            int dbIndex, 
            TTier tier, 
            TDataCenter dataCenter, 
            Dictionary<Type, SettingConverterAttribute> additionalDefaultConverters = null
            )
        {
            _redis = redisConnection;
            _subscriber = _redis.GetSubscriber();
            _dbIndex = dbIndex;
            _manager = new SettingsManager<TSettings, TTier, TDataCenter>(tier, dataCenter, additionalDefaultConverters);
        }

        public static NFigRedisStore<TSettings, TTier, TDataCenter> FromConnectionMultiplexer(
            ConnectionMultiplexer redisConnection,
            int db,
            TTier tier,
            TDataCenter dataCenter,
            Dictionary<Type, SettingConverterAttribute> additionalDefaultConverters = null
            )
        {
            return new NFigRedisStore<TSettings, TTier, TDataCenter>(redisConnection, db, tier, dataCenter, additionalDefaultConverters);
        }

        public void SubscribeToAppSettings(string appName, SettingsUpdateDelegate callback, bool overrideExisting = false)
        {
            lock (_callbacksLock)
            {
                SettingsUpdateDelegate existing;
                if (_callbacks.TryGetValue(appName, out existing))
                {
                    if (callback == existing)
                        return;

                    if (!overrideExisting && existing != callback)
                        throw new InvalidOperationException("Already subscribed to settings for app: " + appName + ". Only one callback can be subscribed at a time.");
                }

                _callbacks[appName] = callback;

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

        public async Task<TSettings> GetSettingsFromRedisAsync(string appName)
        {
            var data = await GetCurrentDataAsync(appName);

            // create new settings object
            var settings = _manager.GetAppSettings(data.Overrides);
            settings.ApplicationName = appName;
            settings.SettingsCommit = data.Commit;
            return settings;
        }

        public void SetOverride(string appName, string settingName, string value, TTier? tier = null, TDataCenter? dataCenter = null)
        {
            SetOverrideAsync(appName, settingName, value, tier, dataCenter).Wait();
        }

        public async Task SetOverrideAsync(string appName, string settingName, string value, TTier? tier = null, TDataCenter? dataCenter = null)
        {
            var tierVal = tier ?? Tier;
            var dcVal = dataCenter ?? DataCenter;

            var key = GetSettingKey(settingName, tierVal, dcVal);
            var db = GetRedisDb();

            await db.HashSetAsync(appName, new [] { new HashEntry(key, value), new HashEntry(COMMIT_KEY, GetCommit()) });
            await _subscriber.PublishAsync(APP_UPDATE_CHANNEL, appName);
        }

        public void ClearOverride(string appName, string settingName, TTier? tier = null, TDataCenter? dataCenter = null)
        {
            ClearOverrideAsync(appName, settingName, tier, dataCenter).Wait();
        }

        public async Task ClearOverrideAsync(string appName, string settingName, TTier? tier = null, TDataCenter? dataCenter = null)
        {
            var tierVal = tier ?? Tier;
            var dcVal = dataCenter ?? DataCenter;

            var key = GetSettingKey(settingName, tierVal, dcVal);
            var db = GetRedisDb();

            var tran = db.CreateTransaction();
            var delTask = tran.HashDeleteAsync(appName, key);
            var setTask = tran.HashSetAsync(appName, COMMIT_KEY, GetCommit());
            var committed = await tran.ExecuteAsync();
            if (!committed)
                throw new NFigException("Unable to clear override. Redis Transaction failed. " + appName + "." + settingName);

            // not sure if these actually need to be awaited after ExecuteAwait finishes
            await delTask;
            await setTask;

            await _subscriber.PublishAsync(APP_UPDATE_CHANNEL, appName);
        }

        public bool IsCurrent(TSettings settings)
        {
            return IsCurrentAsync(settings).Result;
        }

        public async Task<bool> IsCurrentAsync(TSettings settings)
        {
            var commit = await GetCurrentCommitAsync(settings.ApplicationName);
            return commit == settings.SettingsCommit;
        }

        public string GetCurrentCommit(string appName)
        {
            return GetCurrentCommitAsync(appName).Result;
        }

        public async Task<string> GetCurrentCommitAsync(string appName)
        {
            var db = GetRedisDb();
            return await db.HashGetAsync(appName, COMMIT_KEY);
        }

        public async Task<SettingInfo<TTier, TDataCenter>[]> GetAllSettingInfosAsync(string appName)
        {
            var data = await GetCurrentDataAsync(appName);
            return _manager.GetAllSettingInfos(data.Overrides);
        }

        // ReSharper disable once StaticMemberInGenericType
        private static readonly Regex s_keyRegex = new Regex(@"^:(?<Tier>\d+):(?<DataCenter>\d+);(?<Name>.+)$");
        private async Task<RedisAppData> GetCurrentDataAsync(string appName)
        {
            var tierType = typeof(TTier);
            var dataCenterType = typeof(TDataCenter);

            var data = new RedisAppData();

            // grab the redis hash
            var db = GetRedisDb();
            var hash = await db.HashGetAllAsync(appName);

            var overrides = new List<SettingValue<TTier, TDataCenter>>();
            foreach (var hashEntry in hash)
            {
                string key = hashEntry.Name;
                var match = s_keyRegex.Match(key);
                if (match.Success)
                {
                    overrides.Add(new SettingValue<TTier, TDataCenter>(
                        match.Groups["Name"].Value,
                        hashEntry.Value,
                        (TTier)Enum.ToObject(tierType, int.Parse(match.Groups["Tier"].Value)),
                        (TDataCenter)Enum.ToObject(dataCenterType, int.Parse(match.Groups["DataCenter"].Value))
                    ));
                }
                else if (key == COMMIT_KEY)
                {
                    data.Commit = hashEntry.Value;
                }
            }

            data.Overrides = overrides;
            return data;
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

            callback(ex, settings, this);
        }

        private static string GetSettingKey(string settingName, TTier tier, TDataCenter dataCenter)
        {
            return ":" + Convert.ToUInt32(tier) + ":" + Convert.ToUInt32(dataCenter) + ";" + settingName;
        }

        private static string GetCommit()
        {
            return Guid.NewGuid().ToString();
        }

        private IDatabase GetRedisDb()
        {
            return _redis.GetDatabase(_dbIndex);
        }

        private class RedisAppData
        {
            public string Commit { get; set; }
            public IList<SettingValue<TTier, TDataCenter>> Overrides { get; set; }
        }
    }
}
