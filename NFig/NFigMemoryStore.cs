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
            string settingsMetadataJson;
            Dictionary<string, string> defaultsBySubAppHash;
            using (MockRedis.Multi())
            {
                settingsMetadataJson = MockRedis.Get(keys.SettingsMetadata);
                defaultsBySubAppHash = MockRedis.HashGetAll(keys.Defaults);
            }

            if (settingsMetadataJson == null)
                throw new NFigException($"Metadata not found for app {appName}");

            var settingsMetadata = BySetting<SettingMetadata>.Deserialize(settingsMetadataJson);

            Defaults<TTier, TDataCenter> rootDefaults = null;
            var defaultsBySubApp = new Dictionary<int, Defaults<TTier, TDataCenter>>();
            foreach (var kvp in defaultsBySubAppHash)
            {
                var defaults = JsonConvert.DeserializeObject<Defaults<TTier, TDataCenter>>(kvp.Value);
                if (kvp.Key == ROOT_KEY)
                {
                    rootDefaults = defaults;
                }
                else
                {
                    var subAppId = int.Parse(kvp.Key);
                    defaultsBySubApp[subAppId] = defaults;
                }
            }

            UpdateAppMetadataCache(appName, settingsMetadata, rootDefaults, defaultsBySubApp);
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

        protected override void SaveDefaults(string appName, Defaults<TTier, TDataCenter>[] defaults)
        {
            throw new NotImplementedException();
        }

        protected override Task SaveDefaultsAsync(string appName, Defaults<TTier, TDataCenter>[] defaults)
        {
            SaveDefaults(appName, defaults);
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
            public string SettingsMetadata { get; }
            public string Defaults { get; }
            public string Overrides { get; }

            public Keys(TTier tier, string appName)
            {
                var prefix = GetKeyPrefix(tier);

                SettingsMetadata = prefix + nameof(SettingsMetadata) + ":" + appName;
                Defaults = prefix + nameof(Defaults) + ":" + appName;
                Overrides = prefix + nameof(Overrides) + ":" + appName;
            }

            public static string GetKeyPrefix(TTier tier)
            {
                return "NFig3.0:" + Convert.ToInt32(tier) + ":";
            }
        }
    }
}