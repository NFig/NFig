﻿using System;
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

        public override Task SetOverrideAsync(string appName, string settingName, string value, TTier tier, TDataCenter dataCenter)
        {
            SetOverride(appName, settingName, value, tier, dataCenter);
            return Task.FromResult(0);
        }

        public override void SetOverride(string appName, string settingName, string value, TTier tier, TDataCenter dataCenter)
        {
            AssertValidStringForSetting(settingName, value, tier, dataCenter);

            var key = GetOverrideKey(settingName, tier, dataCenter);
            var data = GetInMemoryAppData(appName);

            lock (data)
            {
                data.Overrides[key] = value;
                data.Commit = NewCommit();
            }

            TriggerUpdate(appName);
        }

        public override Task ClearOverrideAsync(string appName, string settingName, TTier tier, TDataCenter dataCenter)
        {
            ClearOverride(appName, settingName, tier, dataCenter);
            return Task.FromResult(0);
        }

        public override void ClearOverride(string appName, string settingName, TTier tier, TDataCenter dataCenter)
        {
            var key = GetOverrideKey(settingName, tier, dataCenter);
            var data = GetInMemoryAppData(appName);
            
            lock (data)
            {
                data.Overrides.Remove(key);
                data.Commit = NewCommit();
            }

            TriggerUpdate(appName);
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

        protected override Task<AppData> GetAppDataNoCacheAsync(string appName)
        {
            return Task.FromResult(GetAppDataNoCache(appName));
        }

        protected override AppData GetAppDataNoCache(string appName)
        {
            var data = GetInMemoryAppData(appName);
            var appData = new AppData();
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

        protected override Task DeleteOrphanedOverridesAsync(AppData data)
        {
            // there's never going to be orphaned overrides for an in-memory store
            return Task.FromResult(0);
        }

        protected override void DeleteOrphanedOverrides(AppData data)
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