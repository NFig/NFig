using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace NFig
{
    public abstract class NFigStore<TSettings, TTier, TDataCenter>
        where TSettings : class, INFigSettings<TTier, TDataCenter>, new()
        where TTier : struct
        where TDataCenter : struct
    {
        public delegate void SettingsUpdateDelegate(Exception ex, TSettings settings, NFigStore<TSettings, TTier, TDataCenter> store);

        private class TierDataCenterCallback
        {
            public TTier Tier { get; }
            public TDataCenter DataCenter { get; }
            public SettingsUpdateDelegate Callback { get; }
            public string LastNotifiedCommit { get; set; } = "NONE";

            public TierDataCenterCallback(TTier tier, TDataCenter dataCenter, SettingsUpdateDelegate callback)
            {
                Tier = tier;
                DataCenter = dataCenter;
                Callback = callback;
            }
        }

        private readonly SettingsFactory<TSettings, TTier, TDataCenter> _factory;

        private readonly object _callbacksLock = new object();
        private readonly Dictionary<string, TierDataCenterCallback[]> _callbacksByApp = new Dictionary<string, TierDataCenterCallback[]>();

        private readonly object _dataCacheLock = new object();
        private readonly Dictionary<string, AppSnapshot<TTier, TDataCenter>> _dataCache = new Dictionary<string, AppSnapshot<TTier, TDataCenter>>();

        private Timer _pollingTimer;

        public int PollingInterval { get; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="additionalDefaultConverters">
        /// Allows you to specify additional (or replacement) default converters for types. Each key/value pair must be in the form of (typeof(T), ISettingConverter&lt;T&gt;).
        /// </param>
        /// <param name="pollingInterval">The interval, in seconds, to poll for override changes. Use 0 to disable polling.</param>
        protected NFigStore(Dictionary<Type, object> additionalDefaultConverters = null, int pollingInterval = 60)
        {
            PollingInterval = pollingInterval;
            _factory = new SettingsFactory<TSettings, TTier, TDataCenter>(additionalDefaultConverters);
            
            if (pollingInterval > 0)
                _pollingTimer = new Timer(PollForChanges, null, pollingInterval * 1000, pollingInterval * 1000);
        }

// === Public Non-Virtual Methods ===

        /// <summary>
        /// Sets an override for the specified application, setting name, tier, and data center combination. If an existing override shares that exact
        /// combination, it will be replaced.
        /// </summary>
        public async Task SetOverrideAsync(string appName, string settingName, string value, TDataCenter dataCenter, string user)
        {
            await SetOverrideAsyncImpl(appName, settingName, value, dataCenter, user);

            try
            {
                await PushUpdateNotificationAsync(appName);
            }
            finally
            {
                // log
            }
        }

        /// <summary>
        /// Synchronous version of SetOverrideAsync. You should use the async version if possible because it may have a lower risk of deadlocking in some circumstances.
        /// </summary>
        public void SetOverride(string appName, string settingName, string value, TDataCenter dataCenter, string user)
        {
            SetOverrideImpl(appName, settingName, value, dataCenter, user);

            try
            {
                PushUpdateNotification(appName);
            }
            finally
            {
                // log
            }
        }

        /// <summary>
        /// Clears an override with the specified application, setting name, tier, and data center combination. Even if the override does not exist, this
        /// operation may result in a change of the current commit, depending on the store's implementation.
        /// </summary>
        public async Task ClearOverrideAsync(string appName, string settingName, TDataCenter dataCenter, string user)
        {
            await ClearOverrideAsyncImpl(appName, settingName, dataCenter, user);

            try
            {
                await PushUpdateNotificationAsync(appName);
            }
            finally
            {
                // log
            }
        }

        /// <summary>
        /// Synchronous version of ClearOverrideAsync. You should use the async version if possible because it may have a lower risk of deadlocking in some circumstances.
        /// </summary>
        public void ClearOverride(string appName, string settingName, TDataCenter dataCenter, string user)
        {
            ClearOverrideImpl(appName, settingName, dataCenter, user);

            try
            {
                PushUpdateNotification(appName);
            }
            finally
            {
                // log
            }
        }

        /// <summary>
        /// Gets a hydrated TSettings object with the correct values for the specified application, tier, and data center combination.
        /// </summary>
        public async Task<TSettings> GetAppSettingsAsync(string appName, TTier tier, TDataCenter dataCenter)
        {
            var data = await GetAppDataAsync(appName).ConfigureAwait(false);

            TSettings settings;
            var ex = GetSettingsObjectFromData(data, tier, dataCenter, out settings);
            if (ex != null)
                throw ex;

            return settings;
        }

        /// <summary>
        /// Synchronous version of GetAppSettingsAsync.
        /// </summary>
        public virtual TSettings GetAppSettings(string appName, TTier tier, TDataCenter dataCenter)
        {
            var data = GetAppData(appName);

            TSettings settings;
            var ex = GetSettingsObjectFromData(data, tier, dataCenter, out settings);
            if (ex != null)
                throw ex;

            return settings;
        }

        /// <summary>
        /// Returns a SettingInfo object for each setting. The SettingInfo contains meta data about the setting, as well as lists of the defaults values and current overrides.
        /// </summary>
        public async Task<SettingInfo<TTier, TDataCenter>[]> GetAllSettingInfosAsync(string appName)
        {
            var data = await GetAppDataAsync(appName).ConfigureAwait(false);
            return _factory.GetAllSettingInfos(data.Overrides);
        }

        /// <summary>
        /// Synchronous version of GetAllSettingInfosAsync.
        /// </summary>
        public SettingInfo<TTier, TDataCenter>[] GetAllSettingInfos(string appName)
        {
            var data = GetAppData(appName);
            return _factory.GetAllSettingInfos(data.Overrides);
        }

        /// <summary>
        /// Returns true if the commit ID on the settings object matches the most current commit ID.
        /// </summary>
        public async Task<bool> IsCurrentAsync(TSettings settings)
        {
            var commit = await GetCurrentCommitAsync(settings.ApplicationName).ConfigureAwait(false);
            return commit == settings.Commit;
        }

        /// <summary>
        /// Synchronous version of IsCurrentAsync.
        /// </summary>
        public bool IsCurrent(TSettings settings)
        {
            var commit = GetCurrentCommit(settings.ApplicationName);
            return commit == settings.Commit;
        }

        /// <summary>
        /// Returns true if a setting by that name exists. For nested settings, use dot notation (i.e. "Some.Setting").
        /// </summary>
        public bool SettingExists(string settingName)
        {
            return _factory.SettingExists(settingName);
        }

        /// <summary>
        /// Returns the property type 
        /// </summary>
        public Type GetSettingType(string settingName)
        {
            return _factory.GetSettingType(settingName);
        }

        /// <summary>
        /// Extracts the value of a setting by name from an existing TSettings object.
        /// </summary>
        public object GetSettingValue(TSettings obj, string settingName)
        {
            return _factory.GetSettingValue(obj, settingName);
        }

        /// <summary>
        /// Extracts the value of a setting by name from an existing TSettings object.
        /// </summary>
        public TValue GetSettingValue<TValue>(TSettings obj, string settingName)
        {
            return _factory.GetSettingValue<TValue>(obj, settingName);
        }

        /// <summary>
        /// Returns true if the given string can be converted into a valid value 
        /// </summary>
        public bool IsValidStringForSetting(string settingName, string value)
        {
            return _factory.IsValidStringForSetting(settingName, value);
        }

// === Virtual Methods ===

        /// <summary>
        /// Returns the most current Commit ID for a given application. The commit may be null if there are no overrides set.
        /// </summary>
        public abstract Task<string> GetCurrentCommitAsync(string appName);

        /// <summary>
        /// Synchronous version of GetCurrentCommitAsync. You should use the async version if possible because it has a lower risk of deadlocking in some circumstances.
        /// </summary>
        public virtual string GetCurrentCommit(string appName)
        {
            return Task.Run(async () => await GetCurrentCommitAsync(appName)).Result;
        }

        protected abstract Task SetOverrideAsyncImpl(string appName, string settingName, string value, TDataCenter dataCenter, string user);

        protected virtual void SetOverrideImpl(string appName, string settingName, string value, TDataCenter dataCenter, string user)
        {
            Task.Run(async () => { await SetOverrideAsyncImpl(appName, settingName, value, dataCenter, user); }).Wait();
        }

        protected abstract Task ClearOverrideAsyncImpl(string appName, string settingName, TDataCenter dataCenter, string user);
        
        protected virtual void ClearOverrideImpl(string appName, string settingName, TDataCenter dataCenter, string user)
        {
            Task.Run(async () => { await ClearOverrideAsyncImpl(appName, settingName, dataCenter, user); }).Wait();
        }

        protected abstract Task PushUpdateNotificationAsync(string appName);

        protected virtual void PushUpdateNotification(string appName)
        {
            Task.Run(async () => { await PushUpdateNotificationAsync(appName); }).Wait();
        }

        // log

        // publish

        // backup

        // restore

        protected abstract Task<AppSnapshot<TTier, TDataCenter>> GetAppDataNoCacheAsync(string appName);

        protected virtual AppSnapshot<TTier, TDataCenter> GetAppDataNoCache(string appName)
        {
            return Task.Run(async () => await GetAppDataNoCacheAsync(appName)).Result;
        }

        protected abstract Task DeleteOrphanedOverridesAsync(AppSnapshot<TTier, TDataCenter> snapshot);

        protected virtual void DeleteOrphanedOverrides(AppSnapshot<TTier, TDataCenter> snapshot)
        {
            Task.Run(async () => await DeleteOrphanedOverridesAsync(snapshot)).Wait();
        }

        protected virtual void OnSubscribe(string appName, TTier tier, TDataCenter dataCenter, SettingsUpdateDelegate callback)
        {
        }

// === Subscriptions ===

        public void SubscribeToAppSettings(string appName, TTier tier, TDataCenter dataCenter, SettingsUpdateDelegate callback)
        {
            TierDataCenterCallback[] callbacks;
            lock (_callbacksLock)
            {
                var info = new TierDataCenterCallback(tier, dataCenter, callback);
                if (_callbacksByApp.TryGetValue(appName, out callbacks))
                {
                    foreach (var c in callbacks)
                    {
                        if (c.Tier.Equals(tier) && c.DataCenter.Equals(dataCenter) && c.Callback == callback)
                            return; // callback already exists, no need to add it again
                    }

                    var oldCallbacks = callbacks;
                    callbacks = new TierDataCenterCallback[oldCallbacks.Length + 1];
                    Array.Copy(oldCallbacks, callbacks, oldCallbacks.Length);
                    callbacks[oldCallbacks.Length] = info;

                    _callbacksByApp[appName] = callbacks;
                }
                else
                {
                    callbacks = new[] { info };
                    _callbacksByApp[appName] = callbacks;
                }
            }

            OnSubscribe(appName, tier, dataCenter, callback);
            ReloadAndNotifyCallback(appName, callbacks);
        }

        private void ReloadAndNotifyCallback(string appName, TierDataCenterCallback[] callbacks)
        {
            if (callbacks == null || callbacks.Length == 0)
                return;

            Exception ex = null;
            AppSnapshot<TTier, TDataCenter> snapshot = null;
            try
            {
                snapshot = GetAppData(appName);
            }
            catch (Exception e)
            {
                ex = e;
            }

            foreach (var c in callbacks)
            {
                if (c.Callback == null)
                    continue;

                if (snapshot != null && snapshot.Commit == c.LastNotifiedCommit)
                    continue;

                TSettings settings = null;
                Exception inner = null;
                if (ex == null)
                {
                    try
                    {
                        ex = GetSettingsObjectFromData(snapshot, c.Tier, c.DataCenter, out settings);
                        c.LastNotifiedCommit = snapshot.Commit;
                    }
                    catch (Exception e)
                    {
                        inner = e;
                    }
                }

                c.Callback(ex ?? inner, settings, this);
            }
        }

        /// <summary>
        /// Unsubscribes from app settings updates.
        /// Note that there is a potential race condition if you unsibscribe while an update is in progress, the prior callback may still get called.
        /// </summary>
        /// <param name="appName">The name of the app.</param>
        /// <param name="tier"></param>
        /// <param name="dataCenter"></param>
        /// <param name="callback">(optional) If null, any callback will be removed. If specified, a current callback will only be removed if it is equal to this param.</param>
        /// <returns>The number of callbacks removed.</returns>
        public int UnsubscribeFromAppSettings(string appName, TTier? tier = null, TDataCenter? dataCenter = null, SettingsUpdateDelegate callback = null)
        {
            lock (_callbacksLock)
            {
                var removedCount = 0;
                TierDataCenterCallback[] callbacks;
                if (_callbacksByApp.TryGetValue(appName, out callbacks))
                {
                    var callbackList = new List<TierDataCenterCallback>(callbacks);
                    for (var i = callbackList.Count - 1; i >= 0; i--)
                    {
                        var c = callbackList[i];

                        if ((tier == null || c.Tier.Equals(tier.Value)) && (dataCenter == null || c.DataCenter.Equals(dataCenter.Value)) && (callback == null || c.Callback == callback))
                        {
                            callbackList.RemoveAt(i);
                            removedCount++;
                        }
                    }

                    if (removedCount > 0)
                        _callbacksByApp[appName] = callbackList.ToArray();
                }

                return removedCount;
            }
        }

        /// <summary>
        /// Causes NFig to reload settings for the app, and notifies all registered callbacks. However,
        /// if the settings haven't changed since the last time a callback was called, it will be skipped.
        /// </summary>
        protected void TriggerReload(string appName)
        {
            ReloadAndNotifyCallback(appName, GetCallbacks(appName));
        }

        private void PollForChanges(object _)
        {
            List<string> appNames;
            lock (_callbacksLock)
            {
                appNames = new List<string>(_callbacksByApp.Count);
                foreach (var name in _callbacksByApp.Keys)
                {
                    appNames.Add(name);
                }
            }

            foreach (var name in appNames)
            {
                var commit = GetCurrentCommit(name);

                var notify = true;
                lock (_dataCacheLock)
                {
                    AppSnapshot<TTier, TDataCenter> snapshot;
                    if (_dataCache.TryGetValue(name, out snapshot))
                    {
                        notify = snapshot.Commit != commit;
                    }
                }

                if (notify)
                {
                    TriggerReload(name);
                }
            }
        }

        private TierDataCenterCallback[] GetCallbacks(string appName)
        {
            lock (_callbacksLock)
            {
                TierDataCenterCallback[] callbacks;
                if (_callbacksByApp.TryGetValue(appName, out callbacks))
                    return callbacks;

                return new TierDataCenterCallback[0];
            }
        }

// === Data Cache ===

        private async Task<AppSnapshot<TTier, TDataCenter>> GetAppDataAsync(string appName)
        {
            AppSnapshot<TTier, TDataCenter> snapshot;

            // check cache first
            bool cacheExisted;
            lock (_dataCacheLock)
            {
                cacheExisted = _dataCache.TryGetValue(appName, out snapshot);
            }

            if (cacheExisted)
            {
                var commit = await GetCurrentCommitAsync(appName).ConfigureAwait(false);
                if (snapshot.Commit == commit)
                    return snapshot;
            }

            snapshot = await GetAppDataNoCacheAsync(appName);

            lock (_dataCacheLock)
            {
                _dataCache[appName] = snapshot;
            }

            if (!cacheExisted)
                await DeleteOrphanedOverridesAsync(snapshot);

            return snapshot;
        }

        private AppSnapshot<TTier, TDataCenter> GetAppData(string appName)
        {
            AppSnapshot<TTier, TDataCenter> snapshot;

            // check cache first
            bool cacheExisted;
            lock (_dataCacheLock)
            {
                cacheExisted = _dataCache.TryGetValue(appName, out snapshot);
            }

            if (cacheExisted)
            {
                var commit = GetCurrentCommit(appName);
                if (snapshot.Commit == commit)
                    return snapshot;
            }

            snapshot = GetAppDataNoCache(appName);

            lock (_dataCacheLock)
            {
                _dataCache[appName] = snapshot;
            }

            if (!cacheExisted)
                DeleteOrphanedOverrides(snapshot);

            return snapshot;
        }

// === Helpers ===

        protected static string NewCommit()
        {
            return Guid.NewGuid().ToString();
        }

        protected static string GetOverrideKey(string settingName, TDataCenter dataCenter)
        {
            return ":0:" + Convert.ToUInt32(dataCenter) + ";" + settingName;
        }
        
        private static readonly Regex s_keyRegex = new Regex(@"^:\d+:(?<DataCenter>\d+);(?<Name>.+)$");
        protected static bool TryGetValueFromOverride(string key, string stringValue, out SettingValue<TTier, TDataCenter> value)
        {
            var match = s_keyRegex.Match(key);
            if (match.Success)
            {
                value = new SettingValue<TTier, TDataCenter>(
                    match.Groups["Name"].Value,
                    stringValue,
                    (TDataCenter)Enum.ToObject(typeof(TDataCenter), int.Parse(match.Groups["DataCenter"].Value)));

                return true;
            }

            value = null;
            return false;
        }

        protected void AssertValidStringForSetting(string settingName, string value, TDataCenter dataCenter)
        {
            if (!IsValidStringForSetting(settingName, value))
            {
                throw new InvalidSettingValueException(
                    "\"" + value + "\" is not a valid value for setting \"" + settingName + "\"",
                    settingName,
                    value,
                    false,
                    dataCenter.ToString());
            }
        }

        private InvalidSettingOverridesException GetSettingsObjectFromData(AppSnapshot<TTier, TDataCenter> snapshot, TTier tier, TDataCenter dataCenter, out TSettings settings)
        {
            // create new settings object
            var ex = _factory.TryGetAppSettings(out settings, tier, dataCenter, snapshot.Overrides);
            settings.ApplicationName = snapshot.ApplicationName;
            settings.Commit = snapshot.Commit;

            return ex;
        }
    }
}