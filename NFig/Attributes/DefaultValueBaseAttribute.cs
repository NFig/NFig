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
        /// The returned IEnumerable must be a non-null enumeration of <see cref="DefaultValue{TTier,TDataCenter}"/> where TTier and TDataCenter match the
        /// types arguments used when creating the <see cref="NFigStore{TTier,TDataCenter}"/>.
        /// 
        /// All defaults returned must match the setting name provided. You cannot use an attribute to generate default values for a setting other than the one
        /// it was applied to.
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
    }
}