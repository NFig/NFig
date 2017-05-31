using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using NFig.Encryption;

namespace NFig
{
    /// <summary>
    /// Describes a connection to a data-store for NFig overrides and metadata. Store-providers must inherit from this class.
    /// </summary>
    public abstract class NFigStore<TTier, TDataCenter>
        where TTier : struct
        where TDataCenter : struct
    {
        readonly Dictionary<string, AppInternalInfo> _infoByApp = new Dictionary<string, AppInternalInfo>();

        /// <summary>
        /// The deployment tier of the store.
        /// </summary>
        public TTier Tier { get; }
        /// <summary>
        /// The data center of the current app or admin panel.
        /// </summary>
        public TDataCenter DataCenter { get; }

        /// <summary>
        /// Instantiates the base Store class.
        /// </summary>
        /// <param name="tier">The deployment tier which the store exists on.</param>
        /// <param name="dataCenter">The data center of the current app or admin panel.</param>
        protected NFigStore(TTier tier, TDataCenter dataCenter)
        {
            AssertIsValidEnumType(typeof(TTier), nameof(TTier));
            AssertIsValidEnumType(typeof(TDataCenter), nameof(TDataCenter));

            Tier = tier;
            DataCenter = dataCenter;
        }

        /// <summary>
        /// Sets an encryptor for an application. This will be used by both app and admin clients. It MUST be called before
        /// <see cref="GetAppClient{TSettings}"/> if the application uses encrypted settings.
        /// </summary>
        /// <param name="appName">The application name.</param>
        /// <param name="encryptor">The encryptor. If used for app clients, it must support decryption.</param>
        public void SetEncryptor(string appName, ISettingEncryptor encryptor)
        {
            if (appName == null)
                throw new ArgumentNullException(nameof(appName));

            if (encryptor == null)
                throw new ArgumentNullException(nameof(encryptor));

            var info = GetAppInfo(appName);

            lock (_infoByApp)
            {
                // For now, we're not going to allow replacing an encryptor which has already been set. It's unclear why you would ever want to do that.
                // However, I do think we do need to provide a way for a user to tell whether an encryptor has already been set for a particular app.
                // todo: provide a way to check whether an app already has an encryptor set
                if (info.Encryptor != null)
                    throw new NFigException($"Cannot set encryptor for app \"{appName}\". It already has an encryptor.");

                info.Encryptor = encryptor;
            }
        }

        /// <summary>
        /// Gets a client for consuming NFig settings within an application. This method is idempotent and will return the exact same client instance every time
        /// it is called with the same app name.
        /// </summary>
        /// <typeparam name="TSettings">
        /// The class which represents your settings. It must inherit from <see cref="INFigSettings{TTier,TDataCenter}"/> or
        /// <see cref="NFigSettingsBase{TTier,TDataCenter}"/>.
        /// </typeparam>
        /// <param name="appName">The name of your application. Overrides are keyed off of this name.</param>
        public NFigAppClient<TSettings, TTier, TDataCenter> GetAppClient<TSettings>(string appName)
            where TSettings : class, INFigSettings<TTier, TDataCenter>, new()
        {
            var appInfo = GetAppInfo(appName, typeof(TSettings));
            var client = (NFigAppClient<TSettings, TTier, TDataCenter>)appInfo.AppClient;

            if (client == null)
            {
                lock (appInfo)
                {
                    client = new NFigAppClient<TSettings, TTier, TDataCenter>(this, appInfo);
                    appInfo.AppClient = client;
                }
            }

            return client;
        }

        /// <summary>
        /// Gets a client for administering NFig settings for a given application.
        /// </summary>
        /// <param name="appName">The name of the application to administer.</param>
        public NFigAdminClient<TTier, TDataCenter> GetAdminClient(string appName)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Gets the names of all applications connected to this store.
        /// </summary>
        public IEnumerable<string> GetAppNames() // todo: use a concrete type instead of IEnumerable
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Asynchronously gets the names of all applications connected to this store.
        /// </summary>
        public Task<IEnumerable<string>> GetAppNamesAsync() // todo: use a concrete type instead of IEnumerable
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Gets the name and ID of every sub-app that has been added to this application.
        /// </summary>
        protected abstract IEnumerable<SubAppInfo> GetSubApps(string appName); // todo: use a concrete type rather than IEnumerable

        /// <summary>
        /// Gets the name and ID of every sub-app that has been added to this application.
        /// </summary>
        protected abstract Task<IEnumerable<SubAppInfo>> GetSubAppsAsync(string appName); // todo: use a concrete type rather than IEnumerable

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal IEnumerable<SubAppInfo> GetSubAppsInternal(string appName) => GetSubApps(appName);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Task<IEnumerable<SubAppInfo>> GetSubAppsAsyncInternal(string appName) => GetSubAppsAsync(appName);

        /// <summary>
        /// Gets the current snapshot of overrides for the app, including all of its sub-apps.
        /// </summary>
        protected abstract OverridesSnapshot<TTier, TDataCenter> GetSnapshot(string appName);

        /// <summary>
        /// Gets the current snapshot of overrides for the app, including all of its sub-apps.
        /// </summary>
        protected abstract Task<OverridesSnapshot<TTier, TDataCenter>> GetSnapshotAsync(string appName);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal OverridesSnapshot<TTier, TDataCenter> GetSnapshotInternal(string appName) => GetSnapshot(appName);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Task<OverridesSnapshot<TTier, TDataCenter>> GetSnapshotAsyncInternal(string appName) => GetSnapshotAsync(appName);

        /// <summary>
        /// Restores all overrides to a previous state. Returns a snapshot of the new current state (after restoring).
        /// 
        /// Implementations are responsible for logging the restore.
        /// </summary>
        /// <param name="appName">The name of the root application.</param>
        /// <param name="snapshot">The snapshot to restore.</param>
        /// <param name="user">The user performing the restore (for logging purposes).</param>
        /// <returns>A snapshot of the new current state (after restoring).</returns>
        protected abstract OverridesSnapshot<TTier, TDataCenter> RestoreSnapshot(string appName, OverridesSnapshot<TTier, TDataCenter> snapshot, string user);

        /// <summary>
        /// Restores all overrides to a previous state. Returns a snapshot of the new current state (after restoring).
        /// 
        /// Implementations are responsible for logging the restore.
        /// </summary>
        /// <param name="appName">The name of the root application.</param>
        /// <param name="snapshot">The snapshot to restore.</param>
        /// <param name="user">The user performing the restore (for logging purposes).</param>
        protected abstract Task<OverridesSnapshot<TTier, TDataCenter>> RestoreSnapshotAsync(
            string appName,
            OverridesSnapshot<TTier, TDataCenter> snapshot,
            string user);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal OverridesSnapshot<TTier, TDataCenter> RestoreSnapshotInternal(string appName, OverridesSnapshot<TTier, TDataCenter> snapshot, string user)
        {
            return RestoreSnapshot(appName, snapshot, user);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Task<OverridesSnapshot<TTier, TDataCenter>> RestoreSnapshotAsyncInternal(
            string appName,
            OverridesSnapshot<TTier, TDataCenter> snapshot,
            string user)
        {
            return RestoreSnapshotAsync(appName, snapshot, user);
        }

        /// <summary>
        /// Sets an override for the specified setting name and data center combination. If an existing override shares that exact combination, it will be
        /// replaced.
        /// 
        /// Returns a snapshot of the state immediately after the override is applied. If the override is not applied because the current commit didn't match
        /// the commit parameter, the return value must be null.
        /// 
        /// Implementations of this method do not need to perform any validation of the override. It will be validated by the admin client prior to calling.
        /// 
        /// Implementations are responsible for logging the change.
        /// </summary>
        /// <param name="appName">The name of the root application.</param>
        /// <param name="settingName">Name of the setting.</param>
        /// <param name="dataCenter">Data center which the override should be applicable to.</param>
        /// <param name="value">The string-value of the setting. If the setting is an encrypted setting, this value must be pre-encrypted.</param>
        /// <param name="user">The user who is setting the override (used for logging purposes). This can be null if you don't care about logging.</param>
        /// <param name="subAppId">The sub-app which the override should apply to.</param>
        /// <param name="commit">If non-null, the override will only be applied if this is the current commit ID.</param>
        /// <param name="expirationTime">The time when the override should be automatically cleared.</param>
        protected abstract OverridesSnapshot<TTier, TDataCenter> SetOverride(
            string appName,
            string settingName,
            TDataCenter dataCenter,
            string value,
            string user,
            int? subAppId,
            string commit,
            DateTimeOffset? expirationTime);

        /// <summary>
        /// Sets an override for the specified setting name and data center combination. If an existing override shares that exact combination, it will be
        /// replaced.
        /// 
        /// Returns a snapshot of the state immediately after the override is applied. If the override is not applied because the current commit didn't match
        /// the commit parameter, the return value must be null.
        /// 
        /// Implementations of this method do not need to perform any validation of the override. It will be validated by the admin client prior to calling.
        /// 
        /// Implementations are responsible for logging the change.
        /// </summary>
        /// <param name="appName">The name of the root application.</param>
        /// <param name="settingName">Name of the setting.</param>
        /// <param name="dataCenter">Data center which the override should be applicable to.</param>
        /// <param name="value">The string-value of the setting. If the setting is an encrypted setting, this value must be pre-encrypted.</param>
        /// <param name="user">The user who is setting the override (used for logging purposes). This can be null if you don't care about logging.</param>
        /// <param name="subAppId">The sub-app which the override should apply to.</param>
        /// <param name="commit">If non-null, the override will only be applied if this is the current commit ID.</param>
        /// <param name="expirationTime">The time when the override should be automatically cleared.</param>
        protected abstract Task<OverridesSnapshot<TTier, TDataCenter>> SetOverrideAsync(
            string appName,
            string settingName,
            TDataCenter dataCenter,
            string value,
            string user,
            int? subAppId,
            string commit,
            DateTimeOffset? expirationTime);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal OverridesSnapshot<TTier, TDataCenter> SetOverrideInternal(
            string appName,
            string settingName,
            TDataCenter dataCenter,
            string value,
            string user,
            int? subAppId,
            string commit,
            DateTimeOffset? expirationTime)
        {
            return SetOverride(appName, settingName, dataCenter, value, user, subAppId, commit, expirationTime);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Task<OverridesSnapshot<TTier, TDataCenter>> SetOverrideAsyncInternal(
            string appName,
            string settingName,
            TDataCenter dataCenter,
            string value,
            string user,
            int? subAppId,
            string commit,
            DateTimeOffset? expirationTime)
        {
            return SetOverrideAsync(appName, settingName, dataCenter, value, user, subAppId, commit, expirationTime);
        }

        /// <summary>
        /// Clears an override with the specified setting name and data center combination. Even if the override does not exist, this operation may result in a
        /// change of the current commit, depending on the store's implementation.
        /// 
        /// A snapshot of the state immediately after the override is cleared. If the override is not applied, either because it didn't exist, or because the
        /// current commit didn't match the commit parameter, the return value will be null.
        /// 
        /// Implementations are responsible for logging any changes.
        /// </summary>
        /// <param name="appName">Name of the root application.</param>
        /// <param name="settingName">Name of the setting.</param>
        /// <param name="dataCenter">Data center which the override is applied to.</param>
        /// <param name="user">The user who is clearing the override (used for logging purposes). This can be null if you don't care about logging.</param>
        /// <param name="subAppId">The sub-app which the override is applied to.</param>
        /// <param name="commit">(optional) If non-null, the override will only be cleared if this is the current commit ID.</param>
        protected abstract OverridesSnapshot<TTier, TDataCenter> ClearOverride(
            string appName,
            string settingName,
            TDataCenter dataCenter,
            string user,
            int? subAppId,
            string commit);

        /// <summary>
        /// Clears an override with the specified setting name and data center combination. Even if the override does not exist, this operation may result in a
        /// change of the current commit, depending on the store's implementation.
        /// 
        /// A snapshot of the state immediately after the override is cleared. If the override is not applied, either because it didn't exist, or because the
        /// current commit didn't match the commit parameter, the return value will be null.
        /// 
        /// Implementations are responsible for logging any changes.
        /// </summary>
        /// <param name="appName">Name of the root application.</param>
        /// <param name="settingName">Name of the setting.</param>
        /// <param name="dataCenter">Data center which the override is applied to.</param>
        /// <param name="user">The user who is clearing the override (used for logging purposes). This can be null if you don't care about logging.</param>
        /// <param name="subAppId">The sub-app which the override is applied to.</param>
        /// <param name="commit">(optional) If non-null, the override will only be cleared if this is the current commit ID.</param>
        protected abstract Task<OverridesSnapshot<TTier, TDataCenter>> ClearOverrideAsync(
            string appName,
            string settingName,
            TDataCenter dataCenter,
            string user,
            int? subAppId,
            string commit);

        internal OverridesSnapshot<TTier, TDataCenter> ClearOverrideInternal(
            string appName,
            string settingName,
            TDataCenter dataCenter,
            string user,
            int? subAppId,
            string commit)
        {
            return ClearOverride(appName, settingName, dataCenter, user, subAppId, commit);
        }

        internal Task<OverridesSnapshot<TTier, TDataCenter>> ClearOverrideAsyncInternal(
            string appName,
            string settingName,
            TDataCenter dataCenter,
            string user,
            int? subAppId,
            string commit)
        {
            return ClearOverrideAsync(appName, settingName, dataCenter, user, subAppId, commit);
        }

        AppInternalInfo GetAppInfo(string appName, Type settingsType = null)
        {
            if (appName == null)
                throw new ArgumentNullException(nameof(appName));

            lock (_infoByApp)
            {
                AppInternalInfo info;
                if (_infoByApp.TryGetValue(appName, out info))
                {
                    if (settingsType != null) // called from an AppClient
                    {
                        if (info.SettingsType == null)
                        {
                            // This is the first time an app client has been setup for this particular app. We'll just blindly trust that they picked the right TSettings.
                            info.SettingsType = settingsType;
                        }
                        else if (settingsType != info.SettingsType)
                        {
                            // there is a mismatch between 
                            var ex = new NFigException($"The TSettings of app \"{appName}\" does not match the TSettings used when the first NFigAppClient was initialized for the app");
                            ex.Data["OriginalTSettings"] = info.SettingsType.FullName;
                            ex.Data["NewTSettings"] = settingsType.FullName;
                            throw ex;
                        }
                    }

                    return info;
                }

                info = new AppInternalInfo(appName, settingsType);
                _infoByApp[appName] = info;
                return info;
            }
        }

        static void AssertIsValidEnumType(Type type, string name)
        {
            if (!type.IsEnum())
                throw new NFigException(name + " must be an enum type.");

            if (type == typeof(byte)
                || type == typeof(sbyte)
                || type == typeof(short)
                || type == typeof(ushort)
                || type == typeof(int)
                || type == typeof(uint))
            {
                return;
            }

            throw new NFigException($"The backing type for {name} must be a 32-bit, or smaller, integer.");
        }
    }
}