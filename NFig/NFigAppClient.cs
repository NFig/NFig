using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NFig
{
    /// <summary>
    /// Provides methods for consuming NFig settings within an application.
    /// </summary>
    public class NFigAppClient<TSettings, TTier, TDataCenter>
        where TSettings : class, INFigSettings<int, TTier, TDataCenter>, new() // todo remove TSubApp
        where TTier : struct
        where TDataCenter : struct
    {
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

        internal NFigAppClient(NFigStore<TTier, TDataCenter> store, string appName, TTier tier, TDataCenter dataCenter)
        {
            Store = store;
            AppName = appName;
            Tier = tier;
            DataCenter = dataCenter;
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
            throw new NotImplementedException();
        }

        /// <summary>
        /// Asynchronously returns a hydrated settings object based on the current defaults and overrides.
        /// </summary>
        /// <param name="subAppId">
        /// The ID of a sub-app. This is only applicable in multi-tenancy environments. If not null, this sub-app must have been previously declared via one of
        /// the AddSubApp or AddSubApps methods, otherwise an exception is thrown.
        /// </param>
        public Task<TSettings> GetSettingsAsync(int? subAppId = null)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Returns true if <paramref name="settings"/> is up-to-date.
        /// </summary>
        public bool IsCurrent(TSettings settings)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Asynchronously returns true if <paramref name="settings"/> is up-to-date.
        /// </summary>
        public Task<bool> IsCurrentAsync(TSettings settings)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Declares a sub-app. The sub-app will be added to the store's metadata and included when the callback to <see cref="SubscribeToSubApps"/> is called.
        /// If a sub-app with the same ID already exists, the name is updated to match. If you need to add more than one sub-app at a time, use
        /// <see cref="AddSubApps"/>.
        /// </summary>
        /// <param name="id">Unique ID of the sub-app.</param>
        /// <param name="name">Name of the sub-app.</param>
        public void AddSubApp(int id, string name)
        {
            AddSubApp(new SubAppInfo(id, name));
        }

        /// <summary>
        /// Declares a sub-app. The sub-app will be added to the store's metadata and included when the callback to <see cref="SubscribeToSubApps"/> is called.
        /// If a sub-app with the same ID already exists, the name is updated to match. If you need to add more than one sub-app at a time, use
        /// <see cref="AddSubApps"/>.
        /// </summary>
        public void AddSubApp(SubAppInfo info)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Declares multiple sub-apps at once. Each sub-app will be added to the store's metadata and included when the callback to
        /// <see cref="SubscribeToSubApps"/> is called. If a sub-app with the same ID already exists, the name is updated to match.
        /// </summary>
        /// <param name="subAppInfos"></param>
        public void AddSubApps(IEnumerable<SubAppInfo> subAppInfos)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Gets the name and ID of every sub-app that has been added to this client.
        /// </summary>
        public IEnumerable<SubAppInfo> GetSubApps() // todo: use a concrete type rather than IEnumerable, perhaps convert to property
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Subscribes to settings changes. The callback will be called for the first time before this method returns, and then will be called each time
        /// overrides are updated in the backing store.
        /// </summary>
        public void Subscribe(UpdateDelegate callback)
        {
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
        public bool SettingExists(string settingName)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Returns the declared type of a setting by name. If no setting by that name exists, and exception is thrown.
        /// </summary>
        public Type GetSettingType(string settingName)
        {
            throw new NotImplementedException();
        }

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
    }
}