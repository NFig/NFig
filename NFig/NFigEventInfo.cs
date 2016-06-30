using System;

namespace NFig
{
    public enum NFigEventType
    {
        OverrideSet = 1,
        OverrideCleared = 2,
        SnapshotRestored = 3,
    }

    public class NFigEventInfo<TDataCenter>
        where TDataCenter : struct
    {
        public NFigEventType Type { get; set; }
        public DateTimeOffset Timestamp { get; set; }
        public string SettingName { get; set; }
        public TDataCenter DataCenter { get; set; }
        public string User { get; set; }
    }
}