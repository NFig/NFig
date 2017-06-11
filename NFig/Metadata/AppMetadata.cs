namespace NFig.Metadata
{
    /// <summary>
    /// Metadata about an NFig application. This provides most of the information that an admin panel needs for an app. However, it does not include default
    /// values. For those, see <see cref="SubAppMetadata{TTier,TDataCenter}"/>.
    /// </summary>
    public class AppMetadata
    {
        /// <summary>
        /// The name of the application.
        /// </summary>
        public string AppName { get; }
        /// <summary>
        /// Information about the current tier.
        /// </summary>
        public EnumValue CurrentTier { get; }
        /// <summary>
        /// Information about the current data center.
        /// </summary>
        public EnumValue CurrentDataCenter { get; }
        /// <summary>
        /// Information about the available data centers.
        /// </summary>
        public EnumMetadata DataCenterMetadata { get; }
        /// <summary>
        /// Basic metadata about each setting.
        /// </summary>
        public BySetting<SettingMetadata> MetadataBySetting { get; }
        /// <summary>
        /// A list of sub-apps which are registered for this application.
        /// </summary>
        public SubApp[] SubApps { get; }

        internal AppMetadata(
            string appName,
            EnumValue currentTier,
            EnumValue currentDataCenter,
            EnumMetadata dataCenterMetadata,
            BySetting<SettingMetadata> metadataBySetting,
            SubApp[] subApps)
        {
            AppName = appName;
            CurrentTier = currentTier;
            CurrentDataCenter = currentDataCenter;
            DataCenterMetadata = dataCenterMetadata;
            MetadataBySetting = metadataBySetting;
            SubApps = subApps;
        }
    }
}