namespace NFig
{
    /// <summary>
    /// This class is used by <see cref="DefaultValueBaseAttribute"/> to transmit default values to NFig.
    /// </summary>
    public class DefaultCreationInfo<TTier, TDataCenter>
        where TTier : struct
        where TDataCenter : struct
    {
        /// <summary>
        /// The actual value, or string representation, of the default.
        /// </summary>
        public object Value { get; }
        /// <summary>
        /// The ID of the sub-app that this value applies to. Null means that the default is applicable to the top-level application, as well as all sub-apps.
        /// </summary>
        public int? SubAppId { get; }
        /// <summary>
        /// The tier that this value applies to. Tier=Any means that the value can be applied to any tier.
        /// </summary>
        public TTier Tier { get; }
        /// <summary>
        /// The data center that this value applies to. DataCenter=Any means that the value can be applied to any data center.
        /// </summary>
        public TDataCenter DataCenter { get; }
        /// <summary>
        /// Indicates whether overrides are allowed when this default value is active.
        /// </summary>
        public bool AllowsOverrides { get; }

        internal DefaultCreationInfo(object value, int? subAppId, TTier tier, TDataCenter dataCenter, bool allowsOverrides)
        {
            Value = value;
            SubAppId = subAppId;
            Tier = tier;
            DataCenter = dataCenter;
            AllowsOverrides = allowsOverrides;
        }
    }
}