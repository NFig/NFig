using System;

namespace NFig
{
    /// <summary>
    /// This is the base class for all NFig attributes which specify default values, except for the <see cref="SettingAttribute"/> itself. This attribute is
    /// abstract because you should provide the attributes which make sense for your individual setup. The subApp/tier/dataCenter parameters in inheriting
    /// attributes should be strongly typed (rather than using "object"), and match the generic parameters used for the NFigStoreOld and Settings object.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
    public abstract class DefaultSettingValueAttribute : Attribute
    {
        /// <summary>
        /// The value of the default being applied. This must be either a convertable string, or a literal value whose type matches that of the setting. For
        /// encrypted settings, a default value must always be an encrypted string.
        /// </summary>
        public object DefaultValue { get; protected set; }
        /// <summary>
        /// The sub-app ID which the default is applicable to. If null, the default is not specific to a sub-app. If your application doesn't use sub-apps,
        /// then you should not include this parameter in inheriting attributes.
        /// </summary>
        public int? SubAppId { get; protected set; }
        /// <summary>
        /// The deployment tier (e.g. local/dev/prod) which the default is applicable to. If null or the zero-value, the default is applicable to any tier.
        /// </summary>
        public object Tier { get; protected set; }
        /// <summary>
        /// The data center which the default is applicable to. If null or the zero-value, the default is applicable to any data center.
        /// </summary>
        public object DataCenter { get; protected set; }
        /// <summary>
        /// Specifies whether NFig should accept runtime overrides for this default. Note that this only applies to environments where this particular default
        /// is the active default. For example, if you set an default for Tier=Prod/DataCenter=Any which DOES NOT allow defaults, and another default for
        /// Tier=Prod/DataCenter=East which DOES allow overrides, then you will be able to set overrides in Prod/East, but you won't be able to set overrides
        /// in any other data center.
        /// </summary>
        public bool AllowOverrides { get; protected set; } = true;
    }
}