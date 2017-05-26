using System;
using System.Collections;

namespace NFig
{
    /// <summary>
    /// This is the base class for attributes which define default values for a setting. You should provide attributes which make sense for your setup. For
    /// example, if your setup doesn't use sub-apps, then there's no reason to create an attribute which allows you to set a sub-app-specific default. Your
    /// attributes should use concrete-typed arguments which match your TTier and TDataCenter types, as appropriate.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
    public abstract class DefaultValueBaseAttribute : Attribute
    {
        /// <summary>
        /// This method is called to get default values out of the attribute. A single attribute can generate zero or more default values. This will be called
        /// once when an NFigAppClient initializes. <paramref name="subAppId"/> and <paramref name="subAppName"/> will be null for this first call. It will be
        /// called again each time a sub-app is declared.
        /// 
        /// Use the protected method <see cref="CreateDefault{TTier,TDataCenter}"/> to generate the items in the IEnumerable.
        /// 
        /// The returned IEnumerable must be a non-null enumeration of <see cref="DefaultCreationInfo{TTier,TDataCenter}"/> where TTier and TDataCenter match
        /// the types arguments used when creating the <see cref="NFigStore{TTier,TDataCenter}"/>. The values in the enumeration must not be null.
        /// 
        /// Some returned default values may be ignored by NFig. They will be ignored if:
        /// 
        /// - The DefaultValue.SubAppId does not exactly match the subAppId this method was called with; or
        /// - The DefaultValue.Tier is not equal to the "Any" tier (default TTier) and not equal to the tier argument the method was called with.
        /// 
        /// Since this method will be called n+1 times, where n is the number of sub-apps declared, these "ignore" rules ensure that the same default is not
        /// registered more than once.
        /// 
        /// </summary>
        /// <param name="appName">The name of the top-level application.</param>
        /// <param name="settingName">The name of the setting which the attribute was applied to.</param>
        /// <param name="settingType">The data type of the setting.</param>
        /// <param name="tier">The tier to get defaults for. This will be of type TTier.</param>
        /// <param name="subAppId">The sub-app to get defaults for, or null if getting defaults for the top-level app.</param>
        /// <param name="subAppName">The name of the sub-app to get defaults for, or null for the top-level app.</param>
        public abstract IEnumerable GetDefaults(string appName, string settingName, Type settingType, object tier, int? subAppId, string subAppName);

        /// <summary>
        /// Creates an object which tells NFig how to create a default value.
        /// </summary>
        /// <typeparam name="TTier">The Tier type used when creating the <see cref="NFigStore{TTier,TDataCenter}"/>.</typeparam>
        /// <typeparam name="TDataCenter">The DataCenter type used when creating the <see cref="NFigStore{TTier,TDataCenter}"/>.</typeparam>
        /// <param name="value">
        /// The actual value, or string representation, of the default.
        /// </param>
        /// <param name="subAppId">
        /// The ID of the sub-app that this value applies to. Null means that the default is applicable to the top-level application, as well as all sub-apps.
        /// </param>
        /// <param name="tier">
        /// The tier that this value applies to. Tier=Any means that the value can be applied to any tier.
        /// </param>
        /// <param name="dataCenter">
        /// The data center that this value applies to. DataCenter=Any means that the value can be applied to any data center.
        /// </param>
        /// <param name="allowsOverrides">
        /// Indicates whether overrides are allowed when this default value is active.
        /// </param>
        protected DefaultCreationInfo<TTier, TDataCenter> CreateDefault<TTier, TDataCenter>(
            object value,
            int? subAppId,
            TTier tier,
            TDataCenter dataCenter,
            bool allowsOverrides)
            where TTier : struct
            where TDataCenter : struct
        {
            return new DefaultCreationInfo<TTier, TDataCenter>(value, subAppId, tier, dataCenter, allowsOverrides);
        }
    }
}