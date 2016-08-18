using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NFig.Encryption;
using NFig.Logging;

namespace NFig.InMemory
{
    public class NFigMemoryStore<TSettings, TTier, TDataCenter> : NFigStore<TSettings, TTier, TDataCenter>
        where TSettings : class, INFigSettings<TTier, TDataCenter>, new()
        where TTier : struct
        where TDataCenter : struct
    {
        class InMemoryAppData
        {
            public string Commit { get; set; } = INITIAL_COMMIT;
            public byte[] LastEvent { get; set; }
            public Dictionary<string, string> Overrides { get; } = new Dictionary<string, string>();
        }

        readonly object _lock = new object();
        readonly Dictionary<string, InMemoryAppData> _dataByApp = new Dictionary<string, InMemoryAppData>();

        public NFigMemoryStore(
            TTier tier,
            TDataCenter dataCenter,
            SettingsLogger<TTier, TDataCenter> logger = null,
            ISettingEncryptor encryptor = null,
            Dictionary<Type, object> additionalDefaultConverters = null)
            : base(tier, dataCenter, logger, encryptor, additionalDefaultConverters, pollingInterval: 0)
        {
        }

        protected override Task<AppSnapshot<TTier, TDataCenter>> SetOverrideAsyncImpl(
            string appName,
            string settingName,
            string value,
            TDataCenter dataCenter,
            string user,
            string commit)
        {
            var snapshot = SetOverrideImpl(appName, settingName, value, dataCenter, user, commit);
            return Task.FromResult(snapshot);
        }

        protected override AppSnapshot<TTier, TDataCenter> SetOverrideImpl(
            string appName,
            string settingName,
            string value,
            TDataCenter dataCenter,
            string user,
            string commit)
        {
            AssertValidStringForSetting(settingName, value, dataCenter);

            var key = GetOverrideKey(settingName, dataCenter);
            var data = GetInMemoryAppData(appName);

            lock (data)
            {
                if (commit != null && commit != data.Commit)
                    return null;

                data.Overrides[key] = value;
                data.Commit = NewCommit();

                var log = new NFigLogEvent<TDataCenter>(
                    type: NFigLogEventType.SetOverride,
                    appName: appName,
                    commit: data.Commit,
                    timestamp: DateTime.UtcNow,
                    settingName: settingName,
                    settingValue: value,
                    restoredCommit: null,
                    dataCenter: dataCenter,
                    user: user);

                data.LastEvent = log.BinarySerialize();

                return CreateSnapshot(appName, data);
            }
        }

        protected override Task<AppSnapshot<TTier, TDataCenter>> ClearOverrideAsyncImpl(
            string appName,
            string settingName,
            TDataCenter dataCenter,
            string user,
            string commit)
        {
            var snapshot = ClearOverrideImpl(appName, settingName, dataCenter, user, commit);
            return Task.FromResult(snapshot);
        }

        protected override AppSnapshot<TTier, TDataCenter> ClearOverrideImpl(
            string appName,
            string settingName,
            TDataCenter dataCenter,
            string user,
            string commit)
        {
            var key = GetOverrideKey(settingName, dataCenter);
            var data = GetInMemoryAppData(appName);
            
            lock (data)
            {
                if (commit != null && commit != data.Commit)
                    return null;

                if (!data.Overrides.ContainsKey(key))
                    return null;

                data.Overrides.Remove(key);
                data.Commit = NewCommit();

                var log = new NFigLogEvent<TDataCenter>(
                    type: NFigLogEventType.ClearOverride,
                    appName: appName,
                    commit: data.Commit,
                    timestamp: DateTime.UtcNow,
                    settingName: settingName,
                    settingValue: null,
                    restoredCommit: null,
                    dataCenter: dataCenter,
                    user: user);

                data.LastEvent = log.BinarySerialize();

                return CreateSnapshot(appName, data);
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
            TriggerReload(appName);
        }

        protected override Task<AppSnapshot<TTier, TDataCenter>> RestoreSnapshotAsyncImpl(AppSnapshot<TTier, TDataCenter> snapshot, string user)
        {
            return Task.FromResult(RestoreSnapshot(snapshot, user));
        }

        protected override AppSnapshot<TTier, TDataCenter> RestoreSnapshotImpl(AppSnapshot<TTier, TDataCenter> snapshot, string user)
        {
            var data = GetInMemoryAppData(snapshot.ApplicationName);
            lock (data)
            {
                data.Commit = NewCommit();

                var log = new NFigLogEvent<TDataCenter>(
                    type: NFigLogEventType.RestoreSnapshot,
                    appName: snapshot.ApplicationName,
                    commit: data.Commit,
                    timestamp: DateTime.UtcNow,
                    settingName: null,
                    settingValue: null,
                    restoredCommit: snapshot.Commit,
                    dataCenter: default(TDataCenter),
                    user: user);

                data.LastEvent = log.BinarySerialize();

                data.Overrides.Clear();
                foreach (var o in snapshot.Overrides)
                {
                    var key = GetOverrideKey(o.Name, o.DataCenter);
                    data.Overrides.Add(key, o.Value);
                }

                return CreateSnapshot(snapshot.ApplicationName, data);
            }
        }

        protected override Task<AppSnapshot<TTier, TDataCenter>> GetAppSnapshotNoCacheAsync(string appName)
        {
            return Task.FromResult(GetAppSnapshotNoCache(appName));
        }

        protected override AppSnapshot<TTier, TDataCenter> GetAppSnapshotNoCache(string appName)
        {
            var data = GetInMemoryAppData(appName);
            lock (data)
            {
                return CreateSnapshot(appName, data);
            }
        }

        static AppSnapshot<TTier, TDataCenter> CreateSnapshot(string appName, InMemoryAppData data)
        {
            var snapshot = new AppSnapshot<TTier, TDataCenter>();

            snapshot.ApplicationName = appName;
            snapshot.Commit = data.Commit;

            var overrides = new List<SettingValue<TTier, TDataCenter>>();
            foreach (var kvp in data.Overrides)
            {
                SettingValue<TTier, TDataCenter> value;
                if (TryGetValueFromOverride(kvp.Key, kvp.Value, out value))
                    overrides.Add(value);
            }

            snapshot.Overrides = overrides;

            snapshot.LastEvent = NFigLogEvent<TDataCenter>.BinaryDeserialize(data.LastEvent);

            return snapshot;
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

        InMemoryAppData GetInMemoryAppData(string appName)
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