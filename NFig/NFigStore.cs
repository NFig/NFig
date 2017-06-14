using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection.Emit;
using System.Threading.Tasks;
using JetBrains.Annotations;
using NFig.Encryption;
using NFig.Metadata;

namespace NFig
{
    /// <summary>
    /// Describes a connection to a data-store for NFig overrides and metadata. Store-providers must inherit from this class.
    /// </summary>
    public abstract class NFigStore<TTier, TDataCenter>
        where TTier : struct
        where TDataCenter : struct
    {
        readonly Dictionary<string, AppInternalInfo<TTier, TDataCenter>> _infoByApp = new Dictionary<string, AppInternalInfo<TTier, TDataCenter>>();

        /// <summary>
        /// The Commit value which should be used when no overrides have ever been set for the application.
        /// </summary>
        public const string INITIAL_COMMIT = "00000000-0000-0000-0000-000000000000";

        /// <summary>
        /// Casts a tier to its integer value.
        /// </summary>
        protected Func<TTier, int> TierToInt { get; }
        /// <summary>
        /// Casts a data center to its integer value.
        /// </summary>
        protected Func<TDataCenter, int> DataCenterToInt { get; }
        /// <summary>
        /// Casts an integer as a tier.
        /// </summary>
        protected Func<int, TTier> IntToTier { get; }
        /// <summary>
        /// Casts an integer as a data center.
        /// </summary>
        protected Func<int, TDataCenter> IntToDataCenter { get; }
        /// <summary>
        /// Used to log exceptions which occur on a background thread.
        /// </summary>
        protected Action<Exception> BackgroundExceptionHandler { get; }

        /// <summary>
        /// Info about the enum value which represents the current tier.
        /// </summary>
        internal EnumValue CurrentTierValue { get; }
        /// <summary>
        /// Info about the enum value which represents the current data center.
        /// </summary>
        internal EnumValue CurrentDataCenterValue { get; }

        /// <summary>
        /// The deployment tier of the store.
        /// </summary>
        public TTier Tier { get; }
        /// <summary>
        /// The data center of the current app or admin panel.
        /// </summary>
        public TDataCenter DataCenter { get; }
        /// <summary>
        /// Metadata about the <typeparamref name="TDataCenter"/> type.
        /// </summary>
        public EnumMetadata DataCenterMetadata { get; }

        /// <summary>
        /// The names of all applications connected to this store.
        /// </summary>
        public string[] AppNames { get; private set; }

        /// <summary>
        /// Instantiates the base Store class.
        /// </summary>
        /// <param name="tier">The deployment tier which the store exists on.</param>
        /// <param name="dataCenter">The data center of the current app or admin panel.</param>
        /// <param name="backgroundExceptionHandler">Used to log exceptions which occur on a background thread.</param>
        protected NFigStore(TTier tier, TDataCenter dataCenter, Action<Exception> backgroundExceptionHandler)
        {
            AssertIsValidEnumType(typeof(TTier), nameof(TTier));
            AssertIsValidEnumType(typeof(TDataCenter), nameof(TDataCenter));

            TierToInt = CreateEnumToIntConverter<TTier>();
            DataCenterToInt = CreateEnumToIntConverter<TDataCenter>();
            IntToTier = CreateIntToEnumConverter<TTier>();
            IntToDataCenter = CreateIntToEnumConverter<TDataCenter>();

            BackgroundExceptionHandler = backgroundExceptionHandler;

            CurrentTierValue = new EnumValue(Convert.ToInt64(tier), tier.ToString());
            CurrentDataCenterValue = new EnumValue(Convert.ToInt64(dataCenter), dataCenter.ToString());

            Tier = tier;
            DataCenter = dataCenter;
            DataCenterMetadata = EnumMetadata.Create(typeof(TDataCenter), tier);
        }

        /// <summary>
        /// Forces a refresh of the list of app names. This typically isn't necessary because the store should auto-refresh when an app is added.
        /// 
        /// Returns the new list of app names.
        /// </summary>
        public string[] RefreshAppNames()
        {
            var appNames = GetAppNames();
            AppNames = appNames;
            return appNames;
        }

        /// <summary>
        /// Forces a refresh of the list of app names. This typically isn't necessary because the store should auto-refresh when an app is added.
        /// 
        /// Returns the new list of app names.
        /// </summary>
        public async Task<string[]> RefreshAppNamesAsync()
        {
            var appNames = await GetAppNamesAsync();
            AppNames = appNames;
            return appNames;
        }

        /// <summary>
        /// Returns an updated list of app names which are known by the backing store.
        /// </summary>
        protected abstract string[] GetAppNames();

        /// <summary>
        /// Returns an updated list of app names which are known by the backing store.
        /// </summary>
        protected abstract Task<string[]> GetAppNamesAsync();

        /// <summary>
        /// Checks the backing store for changes to the app's metadata, including sub-apps, and calls <see cref="UpdateAppMetadataCache"/> as necessary.
        /// </summary>
        /// <param name="appName">The name of the root app.</param>
        /// <param name="forceReload">If true, all metadata for the app should be reloaded, even if no change was detected.</param>
        protected abstract void RefreshAppMetadata(string appName, bool forceReload);

        /// <summary>
        /// Checks the backing store for changes to the app's metadata, including sub-apps, and calls <see cref="UpdateAppMetadataCache"/> as necessary.
        /// </summary>
        /// <param name="appName">The name of the root app.</param>
        /// <param name="forceReload">If true, all metadata for the app will be reloaded, even if no change was detected.</param>
        protected abstract Task RefreshAppMetadataAsync(string appName, bool forceReload);

        internal void RefreshAppMetadataInternal(string appName, bool forceReload) => RefreshAppMetadata(appName, forceReload);
        internal Task RefreshAppMetadataAsyncInternal(string appName, bool forceReload) => RefreshAppMetadataAsync(appName, forceReload);

        /// <summary>
        /// Updates the metadata cache for the application.
        /// </summary>
        protected void UpdateAppMetadataCache(
            [NotNull] string appName,
            [NotNull] BySetting<SettingMetadata> settingsMetadata,
            [CanBeNull] Defaults<TTier, TDataCenter> rootDefaults,
            [NotNull] Dictionary<int, Defaults<TTier, TDataCenter>> subAppDefaults)
        {
            if (appName == null)
                throw new ArgumentNullException(nameof(appName));

            if (settingsMetadata == null)
                throw new ArgumentNullException(nameof(settingsMetadata));

            if (subAppDefaults == null)
                throw new ArgumentNullException(nameof(subAppDefaults));

            var subApps = new SubApp[subAppDefaults.Count];
            var i = 0;
            foreach (var kvp in subAppDefaults)
            {
                subApps[i] = new SubApp(kvp.Key, kvp.Value.SubAppName);
                i++;
            }

            var appMetadata = new AppMetadata(appName, CurrentTierValue, CurrentDataCenterValue, DataCenterMetadata, settingsMetadata, subApps);

            lock (_infoByApp)
            {
                var info = _infoByApp[appName];
                info.AppMetadata = appMetadata;
                info.RootDefaults = rootDefaults;
                info.SubAppDefaults = subAppDefaults;
                // todo: notify clients
            }
        }

        /// <summary>
        /// Checks the backing store for changes to the app's overrides, including sub-apps, and calls <see cref="UpdateSnapshotCache"/> as necessary.
        /// </summary>
        /// <param name="appName">The name of the root app.</param>
        /// <param name="forceReload">If true, the snapshot must be reloaded, even if no change was detected.</param>
        protected abstract void RefreshSnapshot(string appName, bool forceReload);

        /// <summary>
        /// Checks the backing store for changes to the app's overrides, including sub-apps, and calls <see cref="UpdateSnapshotCache"/> as necessary.
        /// </summary>
        /// <param name="appName">The name of the root app.</param>
        /// <param name="forceReload">If true, the snapshot must be reloaded, even if no change was detected.</param>
        protected abstract Task RefreshSnapshotAsync(string appName, bool forceReload);

        internal void RefreshSnapshotInternal(string appName, bool forceReload) => RefreshSnapshot(appName, forceReload);
        internal Task RefreshSnapshotAsyncInternal(string appName, bool forceReload) => RefreshSnapshotAsync(appName, forceReload);

        /// <summary>
        /// Updates the cached snapshot for the app.
        /// </summary>
        protected void UpdateSnapshotCache(OverridesSnapshot<TTier, TDataCenter> snapshot)
        {
            if (snapshot == null)
                throw new NFigException("Cannot set a null snapshot");

            lock (_infoByApp)
            {
                _infoByApp[snapshot.AppName].Snapshot = snapshot;
                // todo: notify clients
            }
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

            lock (_infoByApp)
            {
                var info = SetupAppInfo(appName);

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
            var appInfo = SetupAppInfo(appName, typeof(TSettings));
            var client = (NFigAppClient<TSettings, TTier, TDataCenter>)appInfo.AppClient;

            if (client == null)
            {
                RefreshSnapshotInternal(appName, true);

                lock (appInfo)
                {
                    client = new NFigAppClient<TSettings, TTier, TDataCenter>(this, appInfo);
                    appInfo.AppClient = client;
                }
            }

            return client;
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
        public async Task<NFigAppClient<TSettings, TTier, TDataCenter>> GetAppClientAsync<TSettings>(string appName)
            where TSettings : class, INFigSettings<TTier, TDataCenter>, new()
        {
            var appInfo = SetupAppInfo(appName, typeof(TSettings));
            var client = (NFigAppClient<TSettings, TTier, TDataCenter>)appInfo.AppClient;

            if (client == null)
            {
                await RefreshSnapshotAsyncInternal(appName, true);

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
            var appInfo = SetupAppInfo(appName);
            var client = (NFigAdminClient<TTier, TDataCenter>)appInfo.AdminClient;

            if (client == null)
            {
                RefreshSnapshotInternal(appName, true);
                RefreshAppMetadataInternal(appName, true);

                lock (appInfo)
                {
                    client = new NFigAdminClient<TTier, TDataCenter>(this, appInfo);
                    appInfo.AdminClient = client;
                }
            }

            return client;
        }

        /// <summary>
        /// Gets a client for administering NFig settings for a given application.
        /// </summary>
        /// <param name="appName">The name of the application to administer.</param>
        public async Task<NFigAdminClient<TTier, TDataCenter>> GetAdminClientAsync(string appName)
        {
            var appInfo = SetupAppInfo(appName);
            var client = (NFigAdminClient<TTier, TDataCenter>)appInfo.AdminClient;

            if (client == null)
            {
                await RefreshSnapshotAsyncInternal(appName, true);
                await RefreshAppMetadataAsyncInternal(appName, true);

                lock (appInfo)
                {
                    client = new NFigAdminClient<TTier, TDataCenter>(this, appInfo);
                    appInfo.AdminClient = client;
                }
            }

            return client;
        }

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

        internal OverridesSnapshot<TTier, TDataCenter> RestoreSnapshotInternal(string appName, OverridesSnapshot<TTier, TDataCenter> snapshot, string user)
        {
            return RestoreSnapshot(appName, snapshot, user);
        }

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
        /// <param name="ov">The override to set.</param>
        /// <param name="user">The user who is setting the override (used for logging purposes). This can be null if you don't care about logging.</param>
        /// <param name="commit">If non-null, the override will only be applied if this is the current commit ID.</param>
        protected abstract OverridesSnapshot<TTier, TDataCenter> SetOverride(string appName, OverrideValue<TTier, TDataCenter> ov, string user, string commit);

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
        /// <param name="ov">The override to set.</param>
        /// <param name="user">The user who is setting the override (used for logging purposes). This can be null if you don't care about logging.</param>
        /// <param name="commit">If non-null, the override will only be applied if this is the current commit ID.</param>
        protected abstract Task<OverridesSnapshot<TTier, TDataCenter>> SetOverrideAsync(
            string appName,
            OverrideValue<TTier, TDataCenter> ov,
            string user,
            string commit);

        internal OverridesSnapshot<TTier, TDataCenter> SetOverrideInternal(
            string appName,
            OverrideValue<TTier, TDataCenter> ov,
            string user,
            string commit)
        {
            return SetOverride(appName, ov, user, commit);
        }

        internal Task<OverridesSnapshot<TTier, TDataCenter>> SetOverrideAsyncInternal(
            string appName,
            OverrideValue<TTier, TDataCenter> ov,
            string user,
            string commit)
        {
            return SetOverrideAsync(appName, ov, user, commit);
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

        /// <summary>
        /// Sends metadata to the backing store.
        /// </summary>
        protected abstract void SetMetadata(string appName, BySetting<SettingMetadata> metadata);

        /// <summary>
        /// Sends metadata to the backing store.
        /// </summary>
        protected abstract Task SetMetadataAsync(string appName, BySetting<SettingMetadata> metadata);

        internal void SetMetadataInternal(string appName, BySetting<SettingMetadata> metadata) => SetMetadata(appName, metadata);
        internal Task SetMetadataAsyncInternal(string appName, BySetting<SettingMetadata> metadata) => SetMetadataAsync(appName, metadata);

        /// <summary>
        /// Sends default values to the backing store.
        /// </summary>
        protected abstract void SaveDefaults(string appName, Defaults<TTier, TDataCenter>[] defaults);

        /// <summary>
        /// Sends default values to the backing store.
        /// </summary>
        protected abstract Task SaveDefaultsAsync(string appName, Defaults<TTier, TDataCenter>[] defaults);

        internal void SaveDefaultsInternal(string appName, params Defaults<TTier, TDataCenter>[] defaults)
        {
            SaveDefaults(appName, defaults);
        }

        internal Task SaveDefaultsAsyncInternal(string appName, params Defaults<TTier, TDataCenter>[] defaults)
        {
            return SaveDefaultsAsync(appName, defaults);
        }

        /// <summary>
        /// Returns a unique value which can be used as a snapshot commit.
        /// </summary>
        protected string NewCommit()
        {
            return Guid.NewGuid().ToString();
        }

        /// <summary>
        /// Serializes an override into a key/value pair useful for saving into a backing store.
        /// </summary>
        protected void SerializeOverride(OverrideValue<TTier, TDataCenter> ov, out string key, out string value)
        {
            // Key format: DataCenter,SubAppId;SettingName
            // Value format: ExpirationTime;Value

            key = CreateOverrideKey(ov.Name, ov.SubAppId, ov.DataCenter);
            value = (ov.ExpirationTime?.ToString("O") ?? "") + ";" + ov.Value;
        }

        /// <summary>
        /// Generates a hash key for an override.
        /// </summary>
        protected string CreateOverrideKey(string settingName, int? subAppId, TDataCenter dataCenter)
        {
            return DataCenterToInt(dataCenter) + "," + (subAppId?.ToString() ?? "") + ";" + settingName;
        }

        /// <summary>
        /// Parses a key/value pair into an override value.
        /// </summary>
        protected OverrideValue<TTier, TDataCenter> ParseOverride(string key, string value)
        {
            string settingName = null;
            int? subAppId = null;
            var dataCenter = default(TDataCenter);

            var ki = 0;
            var dataCenterInt = ParseInt(key, ref ki);
            if (dataCenterInt == null || ki >= key.Length || key[ki] != ',')
                goto BAD_OVERRIDE;

            dataCenter = IntToDataCenter(dataCenterInt.Value);

            subAppId = ParseInt(key, ref ki);
            if (ki + 1 >= key.Length || key[ki] != ';')
                goto BAD_OVERRIDE;

            settingName = key.Substring(ki + 1);
            if (settingName.Length == 0)
                goto BAD_OVERRIDE;

            var vi = value.IndexOf(';');
            if (vi == -1)
                goto BAD_OVERRIDE;

            DateTimeOffset? expirationTime = null;
            if (vi > 0)
            {
                var timeStr = value.Substring(0, vi);
                if (DateTimeOffset.TryParseExact(timeStr, "O", null, DateTimeStyles.None, out var exp))
                    expirationTime = exp;
                else
                    goto BAD_OVERRIDE;
            }

            var realValue = vi + 1 == value.Length ? "" : value.Substring(vi + 1);
            return new OverrideValue<TTier, TDataCenter>(settingName, realValue, subAppId, dataCenter, expirationTime);

            BAD_OVERRIDE:
            var ex = new InvalidOverrideValueException("Unable to parse saved override", settingName, value, subAppId, dataCenter.ToString());
            ex.Data["Key"] = key;
            throw ex;
        }

        static int? ParseInt(string s, ref int index)
        {
            if (index >= s.Length)
                return null;

            var c = s[index];
            if (c < '0' || c > '9')
                return null;

            var value = c - '0';
            index++;
            while (index < s.Length && c >= '0' && c <= '9')
            {
                var digit = c - '0';
                value = value * 10 + digit;
                index++;
            }

            return value;
        }

        AppInternalInfo<TTier, TDataCenter> SetupAppInfo(string appName, Type settingsType = null)
        {
            if (appName == null)
                throw new ArgumentNullException(nameof(appName));

            lock (_infoByApp)
            {
                AppInternalInfo<TTier, TDataCenter> info;
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
                            var ex = new NFigException($"The TSettings of app \"{appName}\" does not match the TSettings used when the first NFigAppClient was initialized for the app");
                            ex.Data["OriginalTSettings"] = info.SettingsType.FullName;
                            ex.Data["NewTSettings"] = settingsType.FullName;
                            throw ex;
                        }
                    }

                    return info;
                }

                info = new AppInternalInfo<TTier, TDataCenter>(appName, settingsType);
                _infoByApp[appName] = info;
                return info;
            }
        }

        static void AssertIsValidEnumType(Type type, string name)
        {
            if (!type.IsEnum())
                throw new NFigException(name + " must be an enum type.");

            var backingType = Enum.GetUnderlyingType(type);

            if (backingType == typeof(byte)
                || backingType == typeof(sbyte)
                || backingType == typeof(short)
                || backingType == typeof(ushort)
                || backingType == typeof(int)
                || backingType == typeof(uint))
            {
                return;
            }

            throw new NFigException($"The backing type for {name} must be a 32-bit, or smaller, integer.");
        }

        static Func<int, T> CreateIntToEnumConverter<T>()
        {
            var tType = typeof(T);
            var dm = new DynamicMethod($"Dynamic:IntToEnumConverter<{tType.Name}>", tType, new[] { typeof(int) });
            var il = dm.GetILGenerator();

            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ret);

            return (Func<int, T>)dm.CreateDelegate(typeof(Func<int, T>));
        }

        static Func<T, int> CreateEnumToIntConverter<T>()
        {
            var tType = typeof(T);
            var dm = new DynamicMethod($"Dynamic:EnumToIntConverter<{tType.Name}>", typeof(int), new[] { tType });
            var il = dm.GetILGenerator();

            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ret);

            return (Func<T, int>)dm.CreateDelegate(typeof(Func<T, int>));
        }
    }
}