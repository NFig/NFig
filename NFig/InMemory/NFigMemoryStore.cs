using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NFig.Encryption;
using NFig.Logging;

namespace NFig.InMemory
{
    /// <summary>
    /// An in-memory implementation of NFigStore. Overrides set using this store are not persistent. They will be lost every time the application restarts.
    /// This store is primarily intended for testing purposes, or for getting familiar with NFig in a lightweight way. However, if you don't have any need for
    /// persistent overrides, it could be used in a production environment.
    /// </summary>
    /// <typeparam name="TSettings">
    /// The type where your settings are defined. Must implement <see cref="INFigSettings{TSubApp,TTier,TDataCenter}"/> or inherit from 
    /// <see cref="NFigSettingsBase{TSubApp,TTier,TDataCenter}"/>
    /// </typeparam>
    /// <typeparam name="TSubApp">
    /// The type used to select sub-apps. Must be one of: byte, sbyte, short, ushort, int, uint, or an enum which is backed by one of those types. An enum is
    /// generally preferred, but may be impractical in some cases.
    /// </typeparam>
    /// <typeparam name="TTier">The type used to select the deployment tier. Must be an enum backed by a 32-bit, or smaller, integer.</typeparam>
    /// <typeparam name="TDataCenter">The type used to select the data center. Must be an enum backed by a 32-bit, or smaller, integer.</typeparam>
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
        
        readonly InMemoryAppData _appData = new InMemoryAppData();

        /// <summary>
        /// Initializes a new in-memory NFig store.
        /// </summary>
        /// <param name="globalAppName">The name of the global application. If you are using sub apps, this may represent the name of the umbrella "parent" application.</param>
        /// <param name="tier">Tier the application is running on (cannot be the default "Any" value).</param>
        /// <param name="dataCenter">DataCenter the application is running in (cannot be the default "Any" value).</param>
        /// <param name="logger">The logger which events will be sent to.</param>
        /// <param name="encryptor">
        /// Object which will provide encryption/decryption for encrypted settings. Only required if there are encrypted settings in use.
        /// </param>
        /// <param name="additionalDefaultConverters">
        /// Allows you to specify additional (or replacement) default converters for types. Each key/value pair must be in the form of
        /// (typeof(T), <see cref="ISettingConverter{T}"/>).
        /// </param>
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

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
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
            var data = _appData;

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
            var data = _appData;
            
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
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

        /// <summary>
        /// Returns the most current Commit ID for the application.
        /// </summary>
        public override Task<string> GetCurrentCommitAsync()
        {
            return Task.FromResult(_appData.Commit);
        }

        /// <summary>
        /// Returns the most current Commit ID for the application.
        /// </summary>
        public override string GetCurrentCommit()
        {
            return _appData.Commit;
        }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        protected override Task PushUpdateNotificationAsync()
        {
            PushUpdateNotification();
            return Task.FromResult(0);
        }

        protected override void PushUpdateNotification()
        {
            CheckForUpdatesAndNotifySubscribers();
        }

        protected override Task<OverridesSnapshot<TSubApp, TTier, TDataCenter>> RestoreSnapshotAsyncImpl(OverridesSnapshot<TSubApp, TTier, TDataCenter> snapshot, string user)
        {
            return Task.FromResult(RestoreSnapshot(snapshot, user));
        }

        protected override OverridesSnapshot<TSubApp, TTier, TDataCenter> RestoreSnapshotImpl(OverridesSnapshot<TSubApp, TTier, TDataCenter> snapshot, string user)
        {
            var data = _appData;
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

        protected override Task<OverridesSnapshot<TSubApp, TTier, TDataCenter>> GetSnapshotNoCacheAsync()
        {
            return Task.FromResult(GetSnapshotNoCache());
        }

        protected override OverridesSnapshot<TSubApp, TTier, TDataCenter> GetSnapshotNoCache()
        {
            var data = _appData;
            lock (data)
            {
                return CreateSnapshot(data);
            }
        }

        OverridesSnapshot<TSubApp, TTier, TDataCenter> CreateSnapshot(InMemoryAppData data)
        {
            var overrides = new List<OverrideValue<TSubApp, TTier, TDataCenter>>();
            foreach (var kvp in data.Overrides)
            {
                OverrideValue<TSubApp, TTier, TDataCenter> value;
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
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    }
}