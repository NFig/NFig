using Newtonsoft.Json;

namespace NFig.Metadata
{
    /// <summary>
    /// Metadata specific to a sub-app. This includes the default values applicable to the sub-app.
    /// </summary>
    public class SubAppMetadata<TTier, TDataCenter>
        where TTier : struct
        where TDataCenter : struct
    {
        /// <summary>
        /// The name of the root application.
        /// </summary>
        public string AppName { get; }
        /// <summary>
        /// The ID of the sub-app, or null if this is the root application.
        /// </summary>
        public int? SubAppId { get; }
        /// <summary>
        /// The name of the sub-app, or null if this is the root application.
        /// </summary>
        public string SubAppName { get; }
        /// <summary>
        /// The default values applicable to each setting. Each setting will have one or more default values. Only defaults applicable to the current tier are
        /// included.
        /// </summary>
        public ListBySetting<DefaultValue<TTier, TDataCenter>> DefaultsBySetting { get; }

        [JsonConstructor]
        internal SubAppMetadata(string appName, int? subAppId, string subAppName, ListBySetting<DefaultValue<TTier, TDataCenter>> defaultsBySetting)
        {
            AppName = appName;
            SubAppId = subAppId;
            SubAppName = subAppName;
            DefaultsBySetting = defaultsBySetting;
        }
    }
}