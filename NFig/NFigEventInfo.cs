using System;

namespace NFig
{
    public enum NFigEventType
    {
        SetOverride = 1,
        ClearOverride = 2,
        RestoreSnapshot = 3,
    }

    public class NFigEventInfo<TDataCenter>
        where TDataCenter : struct
    {
        /// <summary>
        /// The type of event which occurred.
        /// </summary>
        public NFigEventType Type { get; set; }
        /// <summary>
        /// The time the event occurred.
        /// </summary>
        public DateTimeOffset Timestamp { get; set; }
        /// <summary>
        /// If the type of event is SetOverride or ClearOverride, this property will indicate which setting was affected. 
        /// </summary>
        public string SettingName { get; set; }
        /// <summary>
        /// The data center which was affected by the event. For restores, it should always be the default "Any" data center.
        /// </summary>
        public TDataCenter DataCenter { get; set; }
        /// <summary>
        /// The user who performed the event.
        /// </summary>
        public string User { get; set; }
        /// <summary>
        /// If the type of event is RestoreSnapshot, this property will indicate the orginal commit of the snapshot which was restored.
        /// </summary>
        public string RestoredCommit { get; set; }
    }
}