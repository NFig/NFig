namespace NFig
{
    /// <summary>
    /// The interface which must be implemented by all top-level settings classes. Typically, you should use <see cref="NFigSettingsBase{TSubApp,TTier,TDataCenter}"/>
    /// instead of implementing this interface manually. However, if your settings class needs to inherit from a different base class, then you may provide
    /// your own interface implementation.
    /// </summary>
    public interface INFigSettings<TSubApp, TTier, TDataCenter>
        where TSubApp : struct
        where TTier : struct
        where TDataCenter : struct
    {
        /// <summary>
        /// The name of the top-level application which these settings were loaded for. If you are using sub apps, this may represent the name of the umbrella
        /// "parent" application.
        /// </summary>
        string GlobalAppName { get; }
        /// <summary>A unique identifier for the these settings which changes anytime an override is set or cleared.</summary>
        string Commit { get; }
        /// <summary>The sub app for which these settings were loaded.</summary>
        TSubApp SubApp { get; }
        /// <summary>The tier on which these settings were loaded.</summary>
        TTier Tier { get; }
        /// <summary>The data center in which these settings were loaded.</summary>
        TDataCenter DataCenter { get; }

        /// <summary>
        /// This method should not be called, except by NFig itself. As a best practice, classes which implement INFigSettings should use an explicit
        /// implementation for this method so that it is not a public method on the inheriting class.
        /// </summary>
        void SetBasicInformation(string globalAppName, string commit, TSubApp subApp, TTier tier, TDataCenter dataCenter);
    }
}