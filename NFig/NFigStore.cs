using System;
using System.Collections.Generic;
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
        /// Instantiates the base Store class.
        /// </summary>
        /// <param name="tier">The deployment tier which the store exists on.</param>
        protected NFigStore(TTier tier)
        {
            AssertIsValidEnumType(typeof(TTier), nameof(TTier));
            AssertIsValidEnumType(typeof(TDataCenter), nameof(TDataCenter));

            Tier = tier;
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
        /// Gets a client for consuming NFig settings within an application.
        /// </summary>
        /// <typeparam name="TSettings">
        /// The class which represents your settings. It must inherit from <see cref="INFigSettings{TTier,TDataCenter}"/> or
        /// <see cref="NFigSettingsBase{TTier,TDataCenter}"/>.
        /// </typeparam>
        /// <param name="appName">The name of your application. Overrides are keyed off of this name.</param>
        /// <param name="dataCenter">The data center where your application resides.</param>
        public NFigAppClient<TSettings, TTier, TDataCenter> GetAppClient<TSettings>(string appName, TDataCenter dataCenter)
            where TSettings : class, INFigSettings<TTier, TDataCenter>, new()
        {
            throw new NotImplementedException();
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