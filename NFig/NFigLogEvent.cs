using System;

namespace NFig
{
    public enum NFigEventType
    {
        SetOverride = 1,
        ClearOverride = 2,
        RestoreSnapshot = 3,
    }

    public class NFigLogEvent<TDataCenter>
        where TDataCenter : struct
    {
        /// <summary>
        /// The type of event which occurred.
        /// </summary>
        public NFigEventType Type { get; set; }
        /// <summary>
        /// The name of the application this event was applicable to.
        /// </summary>
        public string ApplicationName { get; set; }
        /// <summary>
        /// The commit immediately after this change was applied.
        /// </summary>
        public string Commit { get; set; }
        /// <summary>
        /// The time the event occurred.
        /// </summary>
        public DateTimeOffset Timestamp { get; set; }
        /// <summary>
        /// If Type is SetOverride or ClearOverride, this property will indicate which setting was affected. 
        /// </summary>
        public string SettingName { get; set; }
        /// <summary>
        /// If Type is SetOverride, this property will indicate the new value of the setting, otherwise it will be null.
        /// </summary>
        public string SettingValue { get; set; }
        /// <summary>
        /// If Type is RestoreSnapshot, this property will indicate the orginal commit of the snapshot which was restored.
        /// </summary>
        public string RestoredCommit { get; set; }
        /// <summary>
        /// The data center which was affected by the event. For restores, it should always be the default "Any" data center.
        /// </summary>
        public TDataCenter DataCenter { get; set; }
        /// <summary>
        /// The user who performed the event.
        /// </summary>
        public string User { get; set; }

        public NFigLogEvent() { }

        public NFigLogEvent(
            NFigEventType type,
            string appName,
            string commit,
            DateTimeOffset timestamp,
            string settingName,
            string settingValue,
            string restoredCommit,
            TDataCenter dataCenter,
            string user)
        {
            Type = type;
            ApplicationName = appName;
            Commit = commit;
            Timestamp = timestamp;
            SettingName = settingName;
            SettingValue = settingValue;
            RestoredCommit = restoredCommit;
            DataCenter = dataCenter;
            User = user;
        }

        public NFigLogEvent<TDataCenter> Clone()
        {
            return (NFigLogEvent<TDataCenter>)MemberwiseClone();
        }
    }
}