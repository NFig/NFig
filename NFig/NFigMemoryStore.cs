using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NFig.Metadata;

namespace NFig
{
    /// <summary>
    /// An in-memory NFig store. This store is primarily intended for testing and sample apps, but could be used for an app with no persistent backing store.
    /// </summary>
    /// <typeparam name="TTier">The enum type used to represent the deployment tier.</typeparam>
    /// <typeparam name="TDataCenter">The enum type used to represent the data center.</typeparam>
    public class NFigMemoryStore<TTier, TDataCenter> : NFigStore<TTier, TDataCenter>
        where TTier : struct
        where TDataCenter : struct
    {
        const string ROOT_KEY = "$root";

        readonly string _appNamesKey;
        readonly Dictionary<string, Keys> _keysByApp = new Dictionary<string, Keys>();

        NFigMemoryStore(TTier tier, TDataCenter dataCenter, Action<Exception> backgroundExceptionHandler)
            : base(tier, dataCenter, backgroundExceptionHandler)
        {
            _appNamesKey = Keys.GetKeyPrefix(tier) + ":AppNames";
        }

        /// <summary>
        /// Creates an in-memory NFig store. This store is primarily intended for testing and sample apps, but could be used for an app with no persistent
        /// backing store.
        /// </summary>
        /// <param name="tier">The deployment tier of the store.</param>
        /// <param name="dataCenter">The current data center.</param>
        /// <param name="backgroundExceptionHandler">Used to log exceptions which occur on a background thread.</param>
        public NFigMemoryStore<TTier, TDataCenter> Create(TTier tier, TDataCenter dataCenter, Action<Exception> backgroundExceptionHandler)
        {
            var store = new NFigMemoryStore<TTier, TDataCenter>(tier, dataCenter, backgroundExceptionHandler);
            store.RefreshAppNames();
            return store;
        }

        // If this were a real store, there should be an async version of Create, because it needs to load all of the app names from the backing store.

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

        protected override string[] GetAppNames()
        {
            return MockRedis.SetMembers(_appNamesKey);
        }

        protected override Task<string[]> GetAppNamesAsync()
        {
            return Task.FromResult(GetAppNames());
        }

        protected override void RefreshAppMetadata(string appName, bool forceReload)
        {
            // todo: look at history to decide if a refresh is necessary

            var keys = GetKeys(appName);
            string metadataBySettingJson;
            Dictionary<string, string> metadataBySubAppHash;
            using (MockRedis.Multi())
            {
                metadataBySettingJson = MockRedis.Get(keys.MetadataBySetting);
                metadataBySubAppHash = MockRedis.HashGetAll(keys.MetadataBySubApp);
            }

            if (metadataBySettingJson == null)
                throw new NFigException($"Metadata not found for app {appName}");

            var metadataBySetting = BySetting<SettingMetadata>.Deserialize(metadataBySettingJson);

            SubAppMetadata<TTier, TDataCenter> rootMetadata = null;
            var metadataBySubApp = new Dictionary<int, SubAppMetadata<TTier, TDataCenter>>();
            foreach (var kvp in metadataBySubAppHash)
            {
                var meta = JsonConvert.DeserializeObject<SubAppMetadata<TTier, TDataCenter>>(kvp.Value);
                if (kvp.Key == ROOT_KEY)
                {
                    rootMetadata = meta;
                }
                else
                {
                    var subAppId = int.Parse(kvp.Key);
                    metadataBySubApp[subAppId] = meta;
                }
            }

            UpdateAppMetadataCache(appName, metadataBySetting, rootMetadata, metadataBySubApp);
        }

        protected override Task RefreshAppMetadataAsync(string appName, bool forceReload)
        {
            RefreshAppMetadata(appName, forceReload);
            return Task.CompletedTask;
        }

        protected override void RefreshSnapshot(string appName, bool forceReload)
        {
            // todo: look at history to decide if a refresh is necessary
            throw new NotImplementedException();
        }

        protected override Task RefreshSnapshotAsync(string appName, bool forceReload)
        {
            RefreshSnapshot(appName, forceReload);
            return Task.CompletedTask;
        }

        protected override OverridesSnapshot<TTier, TDataCenter> RestoreSnapshot(string appName, OverridesSnapshot<TTier, TDataCenter> snapshot, string user)
        {
            throw new NotImplementedException();
        }

        protected override Task<OverridesSnapshot<TTier, TDataCenter>> RestoreSnapshotAsync(string appName, OverridesSnapshot<TTier, TDataCenter> snapshot, string user)
        {
            return Task.FromResult(RestoreSnapshot(appName, snapshot, user));
        }

        protected override OverridesSnapshot<TTier, TDataCenter> SetOverride(string appName, OverrideValue<TTier, TDataCenter> ov, string user, string commit)
        {
            throw new NotImplementedException();
        }

        protected override Task<OverridesSnapshot<TTier, TDataCenter>> SetOverrideAsync(string appName, OverrideValue<TTier, TDataCenter> ov, string user, string commit)
        {
            return Task.FromResult(SetOverride(appName, ov, user, commit));
        }

        protected override OverridesSnapshot<TTier, TDataCenter> ClearOverride(string appName, string settingName, TDataCenter dataCenter, string user, int? subAppId, string commit)
        {
            throw new NotImplementedException();
        }

        protected override Task<OverridesSnapshot<TTier, TDataCenter>> ClearOverrideAsync(string appName, string settingName, TDataCenter dataCenter, string user, int? subAppId, string commit)
        {
            return Task.FromResult(ClearOverride(appName, settingName, dataCenter, user, subAppId, commit));
        }

        protected override void SetMetadata(string appName, BySetting<SettingMetadata> metadata)
        {
            throw new NotImplementedException();
        }

        protected override Task SetMetadataAsync(string appName, BySetting<SettingMetadata> metadata)
        {
            SetMetadata(appName, metadata);
            return Task.CompletedTask;
        }

        protected override void UpdateSubAppMetadata(string appName, SubAppMetadata<TTier, TDataCenter>[] subAppsMetadata)
        {
            throw new NotImplementedException();
        }

        protected override Task UpdateSubAppMetadataAsync(string appName, SubAppMetadata<TTier, TDataCenter>[] subAppsMetadata)
        {
            UpdateSubAppMetadata(appName, subAppsMetadata);
            return Task.CompletedTask;
        }

#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

        Keys GetKeys(string appName)
        {
            lock (_keysByApp)
            {
                var exists = _keysByApp.TryGetValue(appName, out var keys);

                if (!exists)
                {
                    keys = new Keys(Tier, appName);
                    _keysByApp[appName] = keys;
                }

                return keys;
            }
        }

        class Keys
        {
            public string MetadataBySetting { get; }
            public string MetadataBySubApp { get; }
            public string Overrides { get; }

            public Keys(TTier tier, string appName)
            {
                var prefix = GetKeyPrefix(tier);

                MetadataBySetting = prefix + nameof(MetadataBySetting) + ":" + appName;
                MetadataBySubApp = prefix + nameof(MetadataBySubApp) + ":" + appName;
                Overrides = prefix + nameof(Overrides) + ":" + appName;
            }

            public static string GetKeyPrefix(TTier tier)
            {
                return "NFig3.0:" + Convert.ToInt32(tier) + ":";
            }
        }
    }
}