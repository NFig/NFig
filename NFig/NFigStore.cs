using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NFig
{
    /// <summary>
    /// Describes a connection to a data-store for NFig overrides and metadata. Store-providers must inherit from this class.
    /// </summary>
    public abstract class NFigStore<TTier, TDataCenter>
        where TTier : struct
        where TDataCenter : struct
    {
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
            Tier = tier;
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
    }
}