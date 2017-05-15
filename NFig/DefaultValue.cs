namespace NFig
{
    /// <summary>
    /// Represents a default value for an NFig setting. Defaults are defined at compile-time using attributes. They cannot be instantiated by consumers at
    /// runtime.
    /// </summary>
    public class DefaultValue<TTier, TDataCenter> : ISettingValue<TTier, TDataCenter>
        where TTier : struct
        where TDataCenter : struct
    {
        /// <summary>
        /// The name of the setting which this value applies to.
        /// </summary>
        public string Name { get; }
        /// <summary>
        /// A string-representation of the value. If the setting is encrypted, then this property will be the encrypted string.
        /// </summary>
        public string Value { get; }
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

        bool ISettingValue<TTier, TDataCenter>.IsDefault => true;
        bool ISettingValue<TTier, TDataCenter>.IsOverride => false;

        internal DefaultValue(string name, string value, int? subAppId, TTier tier, TDataCenter dataCenter, bool allowsOverrides)
        {
            Name = name;
            Value = value;
            SubAppId = subAppId;
            Tier = tier;
            DataCenter = dataCenter;
            AllowsOverrides = allowsOverrides;
        }
    }
}