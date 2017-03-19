namespace NFig
{
    /// <summary>
    /// Represents a default value for an NFig setting. Defaults are defined at compile-time using attributes. They cannot be instantiated by consumers at
    /// runtime.
    /// </summary>
    public class DefaultValue<TSubApp, TTier, TDataCenter> : ISettingValue<TSubApp, TTier, TDataCenter>
        where TSubApp : struct
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
        /// The sub-app that this value applies to. SubApp=Global/0 means that this value can be applied to any sub-app.
        /// </summary>
        public TSubApp SubApp { get; }
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

        bool ISettingValue<TSubApp, TTier, TDataCenter>.IsDefault => true;
        bool ISettingValue<TSubApp, TTier, TDataCenter>.IsOverride => false;

        internal DefaultValue(string name, string value, TSubApp subApp, TTier tier, TDataCenter dataCenter, bool allowsOverrides)
        {
            Name = name;
            Value = value;
            SubApp = subApp;
            Tier = tier;
            DataCenter = dataCenter;
            AllowsOverrides = allowsOverrides;
        }
    }
}