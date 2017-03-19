namespace NFig
{
    /// <summary>
    /// An override is a value defined at runtime which takes precendence over default values.
    /// </summary>
    public class OverrideValue<TSubApp, TTier, TDataCenter> : ISettingValue<TSubApp, TTier, TDataCenter>
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
        /// The data center that this value applies to. DataCenter=Any means that the value can be applied to any data center.
        /// </summary>
        public TDataCenter DataCenter { get; }

        TTier ISettingValue<TSubApp, TTier, TDataCenter>.Tier => default(TTier);
        bool ISettingValue<TSubApp, TTier, TDataCenter>.IsDefault => false;
        bool ISettingValue<TSubApp, TTier, TDataCenter>.IsOverride => true;

        /// <summary>
        /// Instantiates a new override. Note: overrides always apply to the currently active tier.
        /// </summary>
        public OverrideValue(string name, string value, TSubApp subApp, TDataCenter dataCenter)
        {
            Name = name;
            Value = value;
            SubApp = subApp;
            DataCenter = dataCenter;
        }
    }
}