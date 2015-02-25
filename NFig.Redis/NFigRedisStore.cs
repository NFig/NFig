using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using StackExchange.Redis;

namespace NFig.Redis
{
    public class NFigRedisStore<TSettings, TTier, TDataCenter>
        where TSettings : class, INFigRedisSettings<TTier, TDataCenter>, new()
        where TTier : struct
        where TDataCenter : struct
    {
        public delegate void SettingsUpdateDelegate(Exception ex, TSettings settings, NFigRedisStore<TSettings, TTier, TDataCenter> nfigRedisStore);

        private class TierDataCenterCallback
        {
            public TTier Tier { get; }
            public TDataCenter DataCenter { get; }
            public SettingsUpdateDelegate Callback { get; }

            public TierDataCenterCallback(TTier tier, TDataCenter dataCenter, SettingsUpdateDelegate callback)
            {
                Tier = tier;
                DataCenter = dataCenter;
                Callback = callback;
            }
        }

        private const string APP_UPDATE_CHANNEL = "NFig-AppUpdate";
        private const string COMMIT_KEY = "$commit";

        private readonly ConnectionMultiplexer _redis;
        private readonly ISubscriber _subscriber;
        private readonly int _dbIndex;

        private readonly object _callbacksLock = new object();
        private readonly Dictionary<string, List<TierDataCenterCallback>> _callbacksByApp = new Dictionary<string, List<TierDataCenterCallback>>();

        private readonly object _dataCacheLock = new object();
        private readonly Dictionary<string, RedisAppData> _dataCache = new Dictionary<string, RedisAppData>();

        private readonly object _infoCacheLock = new object();
        private readonly Dictionary<string, SettingInfoData> _infoCache = new Dictionary<string, SettingInfoData>();

        public SettingsManager<TSettings, TTier, TDataCenter> Manager { get; }

        public NFigRedisStore(
            string redisConnectionString, 
            int dbIndex,
            Dictionary<Type, SettingConverterAttribute> additionalDefaultConverters = null
            )
        : this (
            ConnectionMultiplexer.Connect(redisConnectionString),
            dbIndex,
            additionalDefaultConverters
        )
        {
        }

        // The reason this constructor is private and there is a public static method wrapper is so the calling dll isn't required to reference to SE.Redis.
        private NFigRedisStore(
            ConnectionMultiplexer redisConnection, 
            int dbIndex,
            Dictionary<Type, SettingConverterAttribute> additionalDefaultConverters = null
            )
        {
            _redis = redisConnection;
            _subscriber = _redis.GetSubscriber();
            _dbIndex = dbIndex;
            Manager = new SettingsManager<TSettings, TTier, TDataCenter>(additionalDefaultConverters);
        }

        public static NFigRedisStore<TSettings, TTier, TDataCenter> FromConnectionMultiplexer(
            ConnectionMultiplexer redisConnection,
            int db,
            Dictionary<Type, SettingConverterAttribute> additionalDefaultConverters = null
            )
        {
            return new NFigRedisStore<TSettings, TTier, TDataCenter>(redisConnection, db, additionalDefaultConverters);
        }

        public void SubscribeToAppSettings(string appName, TTier tier, TDataCenter dataCenter, SettingsUpdateDelegate callback, bool overrideExisting = false)
        {
            lock (_callbacksLock)
            {
                var info = new TierDataCenterCallback(tier, dataCenter, callback);
                List<TierDataCenterCallback> callbackList;
                if (_callbacksByApp.TryGetValue(appName, out callbackList))
                {
                    var existing = callbackList.FirstOrDefault(tdc => tdc.Tier.Equals(tier) && tdc.DataCenter.Equals(dataCenter));

                    if (existing != null)
                    {
                        if (callback == existing.Callback)
                            return;
                    }
                }
                else
                {
                    _callbacksByApp[appName] = new List<TierDataCenterCallback>();

                    // set up a redis subscription
                    _subscriber.Subscribe(APP_UPDATE_CHANNEL, OnAppUpdate);
                }

                _callbacksByApp[appName].Add(info);
            }
        }

        /// <summary>
        /// Unsubscribes from app settings updates.
        /// Note that there is a potential race condition if you unsibscribe while an update is in progress, the prior callback may still get called.
        /// </summary>
        /// <param name="appName">The name of the app.</param>
        /// <param name="tier"></param>
        /// <param name="dataCenter"></param>
        /// <param name="callback">(optional) If null, any callback will be removed. If specified, the current callback will only be removed if it is equal to this param.</param>
        /// <returns>True if a callback was removed, otherwise false.</returns>
        public bool UnsubscribeFromAppSettings(string appName, TTier? tier = null, TDataCenter? dataCenter = null, SettingsUpdateDelegate callback = null)
        {
            lock (_callbacksLock)
            {
                var removedAny = false;
                List<TierDataCenterCallback> callbackList;
                if (_callbacksByApp.TryGetValue(appName, out callbackList))
                {
                    for (var i = callbackList.Count - 1; i >= 0; i--)
                    {
                        var c = callbackList[i];

                        if ((tier == null || c.Tier.Equals(tier.Value)) && (dataCenter == null || c.DataCenter.Equals(dataCenter.Value)) && (callback == null || c.Callback == callback))
                        {
                            callbackList.RemoveAt(i);
                            removedAny = true;
                        }
                    }
                }

                return removedAny;
            }
        }

        public TSettings GetApplicationSettings(string appName, TTier tier, TDataCenter dataCenter)
        {
            return Task.Run(async () => { return await GetApplicationSettingsAsync(appName, tier, dataCenter); }).Result;
        }

        public async Task<TSettings> GetApplicationSettingsAsync(string appName, TTier tier, TDataCenter dataCenter)
        {
            var data = await GetCurrentDataAsync(appName).ConfigureAwait(false);
            return GetSettingsObjectFromData(data, tier, dataCenter);
        }

        public void SetOverride(string appName, string settingName, string value, TTier tier, TDataCenter dataCenter)
        {
            Task.Run(async () => { await SetOverrideAsync(appName, settingName, value, tier, dataCenter); }).Wait();
        }

        public async Task SetOverrideAsync(string appName, string settingName, string value, TTier tier, TDataCenter dataCenter)
        {
            // make sure this is even valid input before saving it to Redis
            if (!Manager.IsValidStringForSetting(settingName, value))
                throw new SettingConversionException("\"" + value + "\" is not a valid value for setting \"" + settingName + "\"");

            var key = GetSettingKey(settingName, tier, dataCenter);
            var db = GetRedisDb();

            await db.HashSetAsync(appName, new [] { new HashEntry(key, value), new HashEntry(COMMIT_KEY, GetCommit()) }).ConfigureAwait(false);
            await _subscriber.PublishAsync(APP_UPDATE_CHANNEL, appName).ConfigureAwait(false);
        }

        public void ClearOverride(string appName, string settingName, TTier tier, TDataCenter dataCenter)
        {
            Task.Run(async () => { await ClearOverrideAsync(appName, settingName, tier, dataCenter); }).Wait();
        }

        public async Task ClearOverrideAsync(string appName, string settingName, TTier tier, TDataCenter dataCenter)
        {
            var key = GetSettingKey(settingName, tier, dataCenter);
            var db = GetRedisDb();

            var tran = db.CreateTransaction();
            var delTask = tran.HashDeleteAsync(appName, key);
            var setTask = tran.HashSetAsync(appName, COMMIT_KEY, GetCommit());
            var committed = await tran.ExecuteAsync().ConfigureAwait(false);
            if (!committed)
                throw new NFigException("Unable to clear override. Redis Transaction failed. " + appName + "." + settingName);

            // not sure if these actually need to be awaited after ExecuteAwait finishes
            await delTask.ConfigureAwait(false);
            await setTask.ConfigureAwait(false);

            await _subscriber.PublishAsync(APP_UPDATE_CHANNEL, appName).ConfigureAwait(false);
        }

        public bool IsCurrent(TSettings settings)
        {
            return Task.Run(async () => { return await IsCurrentAsync(settings); }).Result;
        }

        public async Task<bool> IsCurrentAsync(TSettings settings)
        {
            var commit = await GetCurrentCommitAsync(settings.ApplicationName).ConfigureAwait(false);
            return commit == settings.SettingsCommit;
        }

        public string GetCurrentCommit(string appName)
        {
            return Task.Run(async () => { return await GetCurrentCommitAsync(appName); }).Result;
        }

        public async Task<string> GetCurrentCommitAsync(string appName)
        {
            var db = GetRedisDb();
            return await db.HashGetAsync(appName, COMMIT_KEY).ConfigureAwait(false);
        }

        public bool SettingExists(string settingName)
        {
            return Manager.SettingExists(settingName);
        }

        public SettingInfo<TTier, TDataCenter>[] GetAllSettingInfos(string appName)
        {
            return Task.Run(async () => { return await GetAllSettingInfosAsync(appName); }).Result;
        }

        public async Task<SettingInfo<TTier, TDataCenter>[]> GetAllSettingInfosAsync(string appName)
        {
            var data = await GetCurrentDataAsync(appName).ConfigureAwait(false);
            return Manager.GetAllSettingInfos(data.Overrides);
        }

        public SettingInfo<TTier, TDataCenter> GetSettingInfo(string appName, string settingName)
        {
            return Task.Run(async () => { return await GetSettingInfoAsync(appName, settingName); }).Result;
        }

        public async Task<SettingInfo<TTier, TDataCenter>> GetSettingInfoAsync(string appName, string settingName)
        {
            // todo: should probably call GetAllSettingInfosAsync and have it perform caching rather than redoing work and reproducing logic in this method
            SettingInfoData data;
            // ReSharper disable once InconsistentlySynchronizedField
            if (_infoCache.TryGetValue(appName, out data))
            {
                // check if cached info is valid
                var commit = await GetCurrentCommitAsync(appName).ConfigureAwait(false);
                if (data.Commit == commit)
                    return data.InfoBySetting[settingName];
            }

            data = new SettingInfoData();
            var redisData = await GetCurrentDataAsync(appName).ConfigureAwait(false);
            data.InfoBySetting = Manager.GetAllSettingInfos(redisData.Overrides).ToDictionary(s => s.Name);

            lock (_infoCacheLock)
            {
                _infoCache[appName] = data;
            }

            return data.InfoBySetting[settingName];
        }

        public bool IsValidStringForSetting(string settingName, string str)
        {
            return Manager.IsValidStringForSetting(settingName, str);
        }

        // ReSharper disable once StaticMemberInGenericType
        private static readonly Regex s_keyRegex = new Regex(@"^:(?<Tier>\d+):(?<DataCenter>\d+);(?<Name>.+)$");
        private async Task<RedisAppData> GetCurrentDataAsync(string appName)
        {
            RedisAppData data;

            // check cache first
            // ReSharper disable once InconsistentlySynchronizedField
            if (_dataCache.TryGetValue(appName, out data))
            {
                var commit = await GetCurrentCommitAsync(appName).ConfigureAwait(false);
                if (data.Commit == commit)
                    return data;
            }

            var tierType = typeof(TTier);
            var dataCenterType = typeof(TDataCenter);

            data = new RedisAppData();
            data.ApplicationName = appName;

            // grab the redis hash
            var db = GetRedisDb();
            var hash = await db.HashGetAllAsync(appName).ConfigureAwait(false);

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

            lock (_dataCacheLock)
            {
                _dataCache[appName] = data;
            }

            return data;
        }

        private TSettings GetSettingsObjectFromData(RedisAppData data, TTier tier, TDataCenter dataCenter)
        {
            // create new settings object
            var settings = Manager.GetAppSettings(tier, dataCenter, data.Overrides);
            settings.ApplicationName = data.ApplicationName;
            settings.SettingsCommit = data.Commit;
            return settings;
        }

        private void OnAppUpdate(RedisChannel channel, RedisValue message)
        {
            if (channel == APP_UPDATE_CHANNEL)
            {
                List<TierDataCenterCallback> callbacks;
                if (_callbacksByApp.TryGetValue(message, out callbacks))
                {
                    ReloadAndNotifyCallback(message, callbacks);
                }
            }
        }

        private void ReloadAndNotifyCallback(string appName, List<TierDataCenterCallback> callbacks)
        {
            if (callbacks == null || callbacks.Count == 0)
                return;

            Exception ex = null;
            RedisAppData data = null;
            try
            {
                data = Task.Run(async () => { return await GetCurrentDataAsync(appName); }).Result;
            }
            catch(Exception e)
            {
                ex = e;
            }

            foreach (var c in callbacks)
            {
                if (c.Callback == null)
                    continue;

                TSettings settings = null;
                Exception inner = null;
                try
                {
                    if (ex == null)
                        settings = GetSettingsObjectFromData(data, c.Tier, c.DataCenter);
                }
                catch (Exception e)
                {
                    inner = e;
                }

                c.Callback(ex ?? inner, settings, this);
            }

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
            public string ApplicationName { get; set; }
            public string Commit { get; set; }
            public IList<SettingValue<TTier, TDataCenter>> Overrides { get; set; }
        }

        private class SettingInfoData
        {
            public string Commit { get; set; }
            public Dictionary<string, SettingInfo<TTier, TDataCenter>> InfoBySetting { get; set; }
        }
    }
}
