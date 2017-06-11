using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NFig.Metadata;

namespace NFig
{
    /// <summary>
    /// Provides methods for consuming NFig settings within an application.
    /// </summary>
    public class NFigAppClient<TSettings, TTier, TDataCenter>
        where TSettings : class, INFigSettings<TTier, TDataCenter>, new()
        where TTier : struct
        where TDataCenter : struct
    {
        bool _isRootRegistered = false;

        /// <summary>
        /// The method signature of callback functions passed to <see cref="Subscribe"/>.
        /// </summary>
        /// <param name="ex">An Exception object if there was a problem getting or applying overrides. This parameter will be null in most cases</param>
        /// <param name="settings">A hydrated settings object which represents the current active setting values.</param>
        /// <param name="client">A reference to the app client which generated the settings object.</param>
        public delegate void UpdateDelegate(
            Exception ex,
            TSettings settings,
            NFigAppClient<TSettings, TTier, TDataCenter> client);

        /// <summary>
        /// The method signature for of callbacks passed to <see cref="SubscribeToSubApps"/>.
        /// </summary>
        /// <param name="ex">An Exception object if there was a problem getting or applying overrides. This parameter will be null in most cases</param>
        /// <param name="settingsBySubAppId">A dictionary of hydrated settings object by sub-app.</param>
        /// <param name="client">A reference to the app client which generated the settings object.</param>
        public delegate void SubAppsUpdateDelegate(
            Exception ex,
            Dictionary<int, TSettings> settingsBySubAppId,
            NFigAppClient<TSettings, TTier, TDataCenter> client);

        readonly SettingsFactory<TSettings, TTier, TDataCenter> _factory;

        /// <summary>
        /// The backing store for NFig overrides and metadata.
        /// </summary>
        public NFigStore<TTier, TDataCenter> Store { get; }
        /// <summary>
        /// The name of your application.
        /// </summary>
        public string AppName { get; }
        /// <summary>
        /// The deployment tier of your application.
        /// </summary>
        public TTier Tier { get; }
        /// <summary>
        /// The data center where your application resides.
        /// </summary>
        public TDataCenter DataCenter { get; }

        /// <summary>
        /// Initializes the app client.
        /// </summary>
        internal NFigAppClient(NFigStore<TTier, TDataCenter> store, AppInternalInfo appInfo)
        {
            Store = store;
            AppName = appInfo.AppName;
            Tier = store.Tier;
            DataCenter = store.DataCenter;

            _factory = new SettingsFactory<TSettings, TTier, TDataCenter>(appInfo, Tier, DataCenter);
        }

        /// <summary>
        /// Returns a hydrated settings object based on the current defaults and overrides.
        /// </summary>
        /// <param name="subAppId">
        /// The ID of a sub-app. This is only applicable in multi-tenancy environments. If not null, this sub-app must have been previously declared via one of
        /// the AddSubApp or AddSubApps methods, otherwise an exception is thrown.
        /// </param>
        public TSettings GetSettings(int? subAppId = null)
        {
            RegisterRootApp();
            var snapshot = Store.GetSnapshotInternal(AppName);

            var ex = _factory.TryGetSettings(subAppId, snapshot, out var settings);
            if (ex != null)
                throw ex;

            return settings;
        }

        /// <summary>
        /// Asynchronously returns a hydrated settings object based on the current defaults and overrides.
        /// </summary>
        /// <param name="subAppId">
        /// The ID of a sub-app. This is only applicable in multi-tenancy environments. If not null, this sub-app must have been previously declared via one of
        /// the AddSubApp or AddSubApps methods, otherwise an exception is thrown.
        /// </param>
        public async Task<TSettings> GetSettingsAsync(int? subAppId = null)
        {
            var snapshot = await Store.GetSnapshotAsyncInternal(AppName);

            var ex = _factory.TryGetSettings(subAppId, snapshot, out var settings);
            if (ex != null)
                throw ex;

            return settings;
        }

        /// <summary>
        /// Returns true if <paramref name="settings"/> is up-to-date.
        /// </summary>
        public bool IsCurrent(TSettings settings)
        {
            if (settings.AppName != AppName)
            {
                var ex = new InvalidOperationException("AppName does not match between NFigAppClient and settings object");
                ex.Data["settings.AppName"] = settings.AppName;
                ex.Data["NFigAppClient.AppName"] = AppName;
                throw ex;
            }

            return settings.Commit == GetCurrentCommit();
        }

        /// <summary>
        /// Asynchronously returns true if <paramref name="settings"/> is up-to-date.
        /// </summary>
        public async Task<bool> IsCurrentAsync(TSettings settings)
        {
            if (settings.AppName != AppName)
            {
                var ex = new InvalidOperationException("AppName does not match between NFigAppClient and settings object");
                ex.Data["settings.AppName"] = settings.AppName;
                ex.Data["NFigAppClient.AppName"] = AppName;
                throw ex;
            }

            var currentCommit = await GetCurrentCommitAsync();
            return settings.Commit == currentCommit;
        }

        /// <summary>
        /// Returns the current snapshot commit for the app.
        /// </summary>
        public string GetCurrentCommit() => Store.GetCurrentCommitInternal(AppName);

        /// <summary>
        /// Returns the current snapshot commit for the app.
        /// </summary>
        public Task<string> GetCurrentCommitAsync() => Store.GetCurrentCommitAsyncInternal(AppName);

        /// <summary>
        /// Registers multiple sub-apps at once. Each sub-app will be added to the store's metadata and included when the callback to
        /// <see cref="SubscribeToSubApps"/> is called. If a sub-app with the same ID already exists, and the names DO NOT match, an exception is thrown.
        /// Registering a sub-app multiple times with the same ID and name has no effect.
        /// 
        /// Sub-app names are not required to be unique, but it is best practice for every unique sub-app to have a unique name.
        /// </summary>
        public void RegisterSubApps(params SubApp[] subApps)
        {
            var metas = new SubAppMetadata<TTier, TDataCenter>[subApps.Length];
            for (var i = 0; i < subApps.Length; i++)
            {
                // todo: we should probably make this parallel
                ref var subApp = ref subApps[i];
                var defaults = _factory.RegisterSubApp(subApp.Id, subApp.Name);
                metas[i] = new SubAppMetadata<TTier, TDataCenter>(AppName, subApp.Id, subApp.Name, defaults);
            }

            Store.UpdateSubAppsInternal(AppName, metas);
        }

        /// <summary>
        /// Gets the name and ID of every sub-app that has been registered on this client. This may not include every sub-app which has been registered by other
        /// clients. If you want to know the list of every sub-app, use <see cref="NFigAdminClient{TTier,TDataCenter}.GetSubApps"/>.
        /// </summary>
        public SubApp[] GetRegisteredSubApps() => _factory.GetRegisteredSubApps();

        /// <summary>
        /// Subscribes to settings changes. The callback will be called for the first time before this method returns, and then will be called each time
        /// overrides are updated in the backing store.
        /// </summary>
        public void Subscribe(UpdateDelegate callback)
        {
            RegisterRootApp();
            throw new NotImplementedException();
        }

        /// <summary>
        /// Subscribes to sub-app settings. Only sub-apps which have been declared via AddSubApp or AddSubApps will be sent to the callback. The callback will
        /// be called for the first time before this method returns, and then will be called anytime overrides are updated in the backing store or new sub-apps
        /// are declared. Each time it is called, a new settings object is provided for all sub-apps, even if some sub-apps have not changed.
        /// </summary>
        /// <param name="callback"></param>
        public void SubscribeToSubApps(SubAppsUpdateDelegate callback)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Returns true if a setting by that name exists.
        /// </summary>
        public bool SettingExists(string settingName) => _factory.SettingExists(settingName);

        /// <summary>
        /// Returns the declared type of a setting by name. If no setting by that name exists, and exception is thrown.
        /// </summary>
        public Type GetSettingType(string settingName) => _factory.GetSettingType(settingName);

        /// <summary>
        /// Extracts the value of a setting by name from an existing TSettings object. If no setting by that name exists, and exception is thrown.
        /// </summary>
        public object GetSettingValue(TSettings settings, string settingName)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Extracts the value of a setting by name from an existing TSettings object. If not setting by that name exists, or if <typeparamref name="TValue"/>
        /// doesn't match the setting's declared type, an exception is thrown.
        /// </summary>
        public TValue GetSettingValue<TValue>(TSettings settings, string settingName)
        {
            throw new NotImplementedException();
        }

        void RegisterRootApp()
        {
            if (_isRootRegistered)
                return;

            _isRootRegistered = true;
            var defaults = _factory.RegisterRootApp();
            var meta = new SubAppMetadata<TTier, TDataCenter>(AppName, null, null, defaults);
            Store.UpdateSubAppsInternal(AppName, meta);
        }
    }
}