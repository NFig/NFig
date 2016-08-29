namespace NFig
{
    /// <summary>
    /// A minimal implementatino of <see cref="INFigSettings{TSubApp,TTier,TDataCenter}"/> which should typically be used as the base class for settings classes.
    /// </summary>
    public class NFigSettingsBase<TSubApp, TTier, TDataCenter> : INFigSettings<TSubApp, TTier, TDataCenter>
        where TSubApp : struct
        where TTier : struct
        where TDataCenter : struct
    {
        /// <summary>The name of the top-level application which these settings were loaded for.</summary>
        public string ApplicationName { get; private set; }
        /// <summary>A unique identifier for the these settings which changes anytime an override is set or cleared.</summary>
        public string Commit { get; private set; }
        /// <summary>The sub app for which these settings were loaded.</summary>
        public TSubApp SubApp { get; private set; }
        /// <summary>The tier on which these settings were loaded.</summary>
        public TTier Tier { get; private set; }
        /// <summary>The data center in which these settings were loaded.</summary>
        public TDataCenter DataCenter { get; private set; }

        void INFigSettings<TSubApp, TTier, TDataCenter>.SetBasicInformation(string appName, string commit, TSubApp subApp, TTier tier, TDataCenter dataCenter)
        {
            ApplicationName = appName;
            Commit = commit;
            SubApp = subApp;
            Tier = tier;
            DataCenter = dataCenter;
        }
    }
}