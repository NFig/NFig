using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NFig.Encryption;
using NFig.Logging;

namespace NFig.InMemory
{
    public class NFigMemoryStore<TSettings, TSubApp, TTier, TDataCenter> : NFigStore<TSettings, TSubApp, TTier, TDataCenter>
        where TSettings : class, INFigSettings<TSubApp, TTier, TDataCenter>, new()
        where TSubApp : struct
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
            string globalAppName,
            TTier tier,
            TDataCenter dataCenter,
            SettingsLogger<TSubApp, TTier, TDataCenter> logger = null,
            ISettingEncryptor encryptor = null,
            Dictionary<Type, object> additionalDefaultConverters = null)
            : base(globalAppName, tier, dataCenter, logger, encryptor, additionalDefaultConverters, pollingInterval: 0)
        {
        }

        protected override Task<OverridesSnapshot<TSubApp, TTier, TDataCenter>> SetOverrideAsyncImpl(
            string settingName,
            string value,
            TDataCenter dataCenter,
            string user,
            TSubApp subApp,
            string commit)
        {
            var snapshot = SetOverrideImpl(settingName, value, dataCenter, user, subApp, commit);
            return Task.FromResult(snapshot);
        }

        protected override OverridesSnapshot<TSubApp, TTier, TDataCenter> SetOverrideImpl(
            string settingName,
            string value,
            TDataCenter dataCenter,
            string user,
            TSubApp subApp,
            string commit)
        {
            var key = GetOverrideKey(settingName, subApp, dataCenter);
            var data = GetInMemoryAppData();

            lock (data)
            {
                if (commit != null && commit != data.Commit)
                    return null;

                data.Overrides[key] = value;
                data.Commit = NewCommit();

                var log = new NFigLogEvent<TDataCenter>(
                    type: NFigLogEventType.SetOverride,
                    globalAppName: GlobalAppName,
                    commit: data.Commit,
                    timestamp: DateTime.UtcNow,
                    settingName: settingName,
                    settingValue: value,
                    restoredCommit: null,
                    dataCenter: dataCenter,
                    user: user);

                data.LastEvent = log.BinarySerialize();

                return CreateSnapshot(data);
            }
        }

        protected override Task<OverridesSnapshot<TSubApp, TTier, TDataCenter>> ClearOverrideAsyncImpl(
            string settingName,
            TDataCenter dataCenter,
            string user,
            TSubApp subApp,
            string commit)
        {
            var snapshot = ClearOverrideImpl(settingName, dataCenter, user, subApp, commit);
            return Task.FromResult(snapshot);
        }

        protected override OverridesSnapshot<TSubApp, TTier, TDataCenter> ClearOverrideImpl(
            string settingName,
            TDataCenter dataCenter,
            string user,
            TSubApp subApp,
            string commit)
        {
            var key = GetOverrideKey(settingName, subApp, dataCenter);
            var data = GetInMemoryAppData();
            
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
                    globalAppName: GlobalAppName,
                    commit: data.Commit,
                    timestamp: DateTime.UtcNow,
                    settingName: settingName,
                    settingValue: null,
                    restoredCommit: null,
                    dataCenter: dataCenter,
                    user: user);

                data.LastEvent = log.BinarySerialize();

                return CreateSnapshot(data);
            }
        }

        public override Task<string> GetCurrentCommitAsync()
        {
            var data = GetInMemoryAppData();
            return Task.FromResult(data.Commit);
        }

        public override string GetCurrentCommit()
        {
            return GetInMemoryAppData().Commit;
        }

        protected override Task PushUpdateNotificationAsync()
        {
            PushUpdateNotification();
            return Task.FromResult(0);
        }

        protected override void PushUpdateNotification()
        {
            CheckForUpdatesAndNotifyCallbacks();
        }

        protected override Task<OverridesSnapshot<TSubApp, TTier, TDataCenter>> RestoreSnapshotAsyncImpl(OverridesSnapshot<TSubApp, TTier, TDataCenter> snapshot, string user)
        {
            return Task.FromResult(RestoreSnapshot(snapshot, user));
        }

        protected override OverridesSnapshot<TSubApp, TTier, TDataCenter> RestoreSnapshotImpl(OverridesSnapshot<TSubApp, TTier, TDataCenter> snapshot, string user)
        {
            var data = GetInMemoryAppData();
            lock (data)
            {
                data.Commit = NewCommit();

                var log = new NFigLogEvent<TDataCenter>(
                    type: NFigLogEventType.RestoreSnapshot,
                    globalAppName: snapshot.GlobalAppName,
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
                    var key = GetOverrideKey(o.Name, o.SubApp, o.DataCenter);
                    data.Overrides.Add(key, o.Value);
                }

                return CreateSnapshot(data);
            }
        }

        protected override Task<OverridesSnapshot<TSubApp, TTier, TDataCenter>> GetAppSnapshotNoCacheAsync()
        {
            return Task.FromResult(GetAppSnapshotNoCache());
        }

        protected override OverridesSnapshot<TSubApp, TTier, TDataCenter> GetAppSnapshotNoCache()
        {
            var data = GetInMemoryAppData();
            lock (data)
            {
                return CreateSnapshot(data);
            }
        }

        OverridesSnapshot<TSubApp, TTier, TDataCenter> CreateSnapshot(InMemoryAppData data)
        {
            var overrides = new List<SettingValue<TSubApp, TTier, TDataCenter>>();
            foreach (var kvp in data.Overrides)
            {
                SettingValue<TSubApp, TTier, TDataCenter> value;
                if (TryGetValueFromOverride(kvp.Key, kvp.Value, out value))
                    overrides.Add(value);
            }

            var lastEvent = NFigLogEvent<TDataCenter>.BinaryDeserialize(data.LastEvent);

            return new OverridesSnapshot<TSubApp, TTier, TDataCenter>(GlobalAppName, data.Commit, overrides, lastEvent);
        }

        protected override Task DeleteOrphanedOverridesAsync(OverridesSnapshot<TSubApp, TTier, TDataCenter> snapshot)
        {
            // there's never going to be orphaned overrides for an in-memory store
            return Task.FromResult(0);
        }

        protected override void DeleteOrphanedOverrides(OverridesSnapshot<TSubApp, TTier, TDataCenter> snapshot)
        {
            // there's never going to be orphaned overrides for an in-memory store
        }

        InMemoryAppData GetInMemoryAppData()
        {
            lock (_lock)
            {
                InMemoryAppData data;
                if (_dataByApp.TryGetValue(GlobalAppName, out data))
                {
                    return data;
                }

                data = new InMemoryAppData();
                _dataByApp[GlobalAppName] = data;

                return data;
            }
        }
    }
}