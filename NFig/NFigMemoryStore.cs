using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using JetBrains.Annotations;
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

        readonly Dictionary<string, App> _apps = new Dictionary<string, App>();

        /// <summary>
        /// Creates an in-memory NFig store. This store is primarily intended for testing and sample apps, but could be used for an app with no persistent
        /// backing store.
        /// </summary>
        /// <param name="tier">The deployment tier of the store.</param>
        /// <param name="dataCenter">The current data center.</param>
        /// <param name="backgroundExceptionHandler">Used to log exceptions which occur on a background thread.</param>
        public NFigMemoryStore(TTier tier, TDataCenter dataCenter, Action<Exception> backgroundExceptionHandler)
            : base(tier, dataCenter, backgroundExceptionHandler)
        {
        }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

        protected override IEnumerable<SubApp> GetSubApps(string appName)
        {
            var app = GetApp(appName);
            lock (app)
            {
                var subApps = new SubApp[app.SubApps.Count];
                var i = 0;
                foreach (var kvp in app.SubApps)
                {
                    subApps[i] = new SubApp(kvp.Key, kvp.Value);
                    i++;
                }

                return subApps;
            }
        }

        protected override Task<IEnumerable<SubApp>> GetSubAppsAsync(string appName)
        {
            return Task.FromResult(GetSubApps(appName));
        }

        protected override BySetting<SettingMetadata> GetSettingsMetadata(string appName)
        {
            var app = GetApp(appName);
            return app.Metadata == null ? null : BySetting<SettingMetadata>.Deserialize(app.Metadata);
        }

        protected override Task<BySetting<SettingMetadata>> GetSettingsMetadataAsync(string appName)
        {
            return Task.FromResult(GetSettingsMetadata(appName));
        }

        protected override ListBySetting<DefaultValue<TTier, TDataCenter>> GetDefaults(string appName, int? subAppId)
        {
            var app = GetApp(appName);
            lock (app)
            {
                string json;
                var key = subAppId.HasValue ? subAppId.Value.ToString() : ROOT_KEY;
                if (!app.Defaults.TryGetValue(key, out json))
                    return null;

                return ListBySetting<DefaultValue<TTier, TDataCenter>>.Deserialize(json);
            }
        }

        protected override Task<ListBySetting<DefaultValue<TTier, TDataCenter>>> GetDefaultsAsync(string appName, int? subAppId)
        {
            return Task.FromResult(GetDefaults(appName, subAppId));
        }

        protected override OverridesSnapshot<TTier, TDataCenter> GetSnapshot(string appName)
        {
            var app = GetApp(appName);
            lock (app)
            {
                if (app.SnapshotCache != null && app.SnapshotCache.Commit == app.Commit)
                    return app.SnapshotCache;

                var overrides = new OverrideValue<TTier, TDataCenter>[app.Overrides.Count];
                var i = 0;
                foreach (var kvp in app.Overrides)
                {
                    overrides[i] = ParseOverride(kvp.Key, kvp.Value);
                    i++;
                }

                var overridesDictionary = new ListBySetting<OverrideValue<TTier, TDataCenter>>(overrides);
                var snapshot = new OverridesSnapshot<TTier, TDataCenter>(appName, app.Commit, overridesDictionary);
                app.SnapshotCache = snapshot;
                return snapshot;
            }
        }

        protected override Task<OverridesSnapshot<TTier, TDataCenter>> GetSnapshotAsync(string appName)
        {
            return Task.FromResult(GetSnapshot(appName));
        }

        protected override OverridesSnapshot<TTier, TDataCenter> RestoreSnapshot(string appName, OverridesSnapshot<TTier, TDataCenter> snapshot, string user)
        {
            var app = GetApp(snapshot.AppName);
            lock (app)
            {
                app.Overrides.Clear();

                if (snapshot.Overrides != null)
                {
                    foreach (var ov in snapshot.Overrides.GetAllValues())
                    {
                        SerializeOverride(ov, out var key, out var value);
                        app.Overrides[key] = value;
                    }
                }

                app.Commit = snapshot.Commit;

                // todo: log

                return GetSnapshot(app.AppName);
            }
        }

        protected override Task<OverridesSnapshot<TTier, TDataCenter>> RestoreSnapshotAsync(string appName, OverridesSnapshot<TTier, TDataCenter> snapshot, string user)
        {
            return Task.FromResult(RestoreSnapshot(appName, snapshot, user));
        }

        protected override string GetCurrentCommit(string appName)
        {
            return GetApp(appName).Commit;
        }

        protected override Task<string> GetCurrentCommitAsync(string appName)
        {
            return Task.FromResult(GetCurrentCommit(appName));
        }

        protected override OverridesSnapshot<TTier, TDataCenter> SetOverride(string appName, OverrideValue<TTier, TDataCenter> ov, string user, string commit)
        {
            var app = GetApp(appName);
            lock (app)
            {
                if (commit != null && app.Commit != commit)
                    return null;

                SerializeOverride(ov, out var key, out var value);
                app.Overrides[key] = value;
                app.Commit = NewCommit();

                // todo: log

                return GetSnapshot(appName);
            }
        }

        protected override Task<OverridesSnapshot<TTier, TDataCenter>> SetOverrideAsync(
            string appName,
            OverrideValue<TTier, TDataCenter> ov,
            string user,
            string commit)
        {
            return Task.FromResult(SetOverride(appName, ov, user, commit));
        }

        protected override OverridesSnapshot<TTier, TDataCenter> ClearOverride(string appName, string settingName, TDataCenter dataCenter, string user, int? subAppId, string commit)
        {
            var app = GetApp(appName);
            lock (app)
            {
                if (commit != null && app.Commit != commit)
                    return null;

                var key = CreateOverrideKey(settingName, subAppId, dataCenter);
                var removed = app.Overrides.Remove(key);

                if (removed)
                {
                    app.Commit = NewCommit();

                    // todo: log
                }

                return GetSnapshot(appName);
            }
        }

        protected override Task<OverridesSnapshot<TTier, TDataCenter>> ClearOverrideAsync(string appName, string settingName, TDataCenter dataCenter, string user, int? subAppId, string commit)
        {
            return Task.FromResult(ClearOverride(appName, settingName, dataCenter, user, subAppId, commit));
        }

        protected override void SetMetadata(string appName, BySetting<SettingMetadata> metadata)
        {
            var app = GetApp(appName);
            app.Metadata = JsonConvert.SerializeObject(metadata);
        }

        protected override void UpdateSubApps(string appName, SubAppMetadata<TTier, TDataCenter>[] subAppsMetadata)
        {
            var app = GetApp(appName);
            lock (app)
            {
                foreach (var subApp in subAppsMetadata)
                {
                    var key = subApp.SubAppId?.ToString() ?? ROOT_KEY;
                    var json = JsonConvert.SerializeObject(subApp.DefaultsBySetting);

                    app.Defaults[key] = json;

                    if (subApp.SubAppId.HasValue)
                        app.SubApps[subApp.SubAppId.Value] = subApp.SubAppName;
                }

            }
        }

#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

        App GetApp(string appName)
        {
            lock (_apps)
            {
                App app;
                if (_apps.TryGetValue(appName, out app))
                    return app;

                app = new App(appName);
                _apps[appName] = app;

                return app;
            }
        }

        // This class is essentially designed to mimic how NFig.Redis is intended to work. We could design an in-memory store which was a bit more effecient,
        // but it wouldn't be as good of a test platform. The most common use cases for this memory store are: testing, sample apps, and apps where the settings
        // don't change. None of those need anything more effecient.
        class App
        {
            public string AppName { get; }
            public string Commit { get; set; }
            public string Metadata { get; set; }
            public Dictionary<int, string> SubApps { get; } = new Dictionary<int, string>();
            public Dictionary<string, string> Defaults { get; } = new Dictionary<string, string>();
            public Dictionary<string, string> Overrides { get; } = new Dictionary<string, string>();

            public OverridesSnapshot<TTier, TDataCenter> SnapshotCache { get; set; }

            public App(string appName)
            {
                AppName = appName;
                Commit = INITIAL_COMMIT;
            }
        }
    }
}