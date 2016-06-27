using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NFig
{
    public class NFigMemoryStore<TSettings, TTier, TDataCenter> : NFigStore<TSettings, TTier, TDataCenter>
        where TSettings : class, INFigSettings<TTier, TDataCenter>, new()
        where TTier : struct
        where TDataCenter : struct
    {
        private class InMemoryAppData
        {
            public string Commit { get; set; }
            public Dictionary<string, string> Overrides { get; set; } = new Dictionary<string, string>();
        }

        private readonly object _lock = new object();
        private readonly Dictionary<string, InMemoryAppData> _dataByApp = new Dictionary<string, InMemoryAppData>();

        public NFigMemoryStore(Dictionary<Type, object> additionalDefaultConverters = null) : base(additionalDefaultConverters, pollingInterval: 0)
        {
        }

        protected override Task SetOverrideAsyncImpl(string appName, string settingName, string value, TDataCenter dataCenter, string user)
        {
            SetOverrideImpl(appName, settingName, value, dataCenter, user);
            return Task.FromResult(0);
        }

        protected override void SetOverrideImpl(string appName, string settingName, string value, TDataCenter dataCenter, string user)
        {
            AssertValidStringForSetting(settingName, value, dataCenter);

            var key = GetOverrideKey(settingName, dataCenter);
            var data = GetInMemoryAppData(appName);

            lock (data)
            {
                data.Overrides[key] = value;
                data.Commit = NewCommit();
            }
        }

        protected override Task ClearOverrideAsyncImpl(string appName, string settingName, TDataCenter dataCenter, string user)
        {
            ClearOverrideImpl(appName, settingName, dataCenter, user);
            return Task.FromResult(0);
        }

        protected override void ClearOverrideImpl(string appName, string settingName, TDataCenter dataCenter, string user)
        {
            var key = GetOverrideKey(settingName, dataCenter);
            var data = GetInMemoryAppData(appName);
            
            lock (data)
            {
                data.Overrides.Remove(key);
                data.Commit = NewCommit();
            }
        }

        public override Task<string> GetCurrentCommitAsync(string appName)
        {
            var data = GetInMemoryAppData(appName);
            return Task.FromResult(data.Commit);
        }

        public override string GetCurrentCommit(string appName)
        {
            return GetInMemoryAppData(appName).Commit;
        }

        protected override Task PushUpdateNotificationAsync(string appName)
        {
            PushUpdateNotification(appName);
            return Task.FromResult(0);
        }

        protected override void PushUpdateNotification(string appName)
        {
            TriggerUpdate(appName);
        }

        protected override Task<AppSnapshot<TTier, TDataCenter>> GetAppDataNoCacheAsync(string appName)
        {
            return Task.FromResult(GetAppDataNoCache(appName));
        }

        protected override AppSnapshot<TTier, TDataCenter> GetAppDataNoCache(string appName)
        {
            var data = GetInMemoryAppData(appName);
            var appData = new AppSnapshot<TTier, TDataCenter>();
            lock (data)
            {
                appData.ApplicationName = appName;
                appData.Commit = data.Commit;

                var overrides = new List<SettingValue<TTier, TDataCenter>>();
                foreach (var kvp in data.Overrides)
                {
                    SettingValue<TTier, TDataCenter> value;
                    if (TryGetValueFromOverride(kvp.Key, kvp.Value, out value))
                        overrides.Add(value);
                }

                appData.Overrides = overrides;
            }

            return appData;
        }

        protected override Task DeleteOrphanedOverridesAsync(AppSnapshot<TTier, TDataCenter> snapshot)
        {
            // there's never going to be orphaned overrides for an in-memory store
            return Task.FromResult(0);
        }

        protected override void DeleteOrphanedOverrides(AppSnapshot<TTier, TDataCenter> snapshot)
        {
            // there's never going to be orphaned overrides for an in-memory store
        }

        private InMemoryAppData GetInMemoryAppData(string appName)
        {
            lock (_lock)
            {
                InMemoryAppData data;
                if (_dataByApp.TryGetValue(appName, out data))
                {
                    return data;
                }

                data = new InMemoryAppData();
                _dataByApp[appName] = data;

                return data;
            }
        }
    }
}