using System;

namespace NFig
{
    public enum NFigEventType
    {
        OverrideSet = 1,
        OverrideCleared = 2,
    }

    public class NFigEventInfo<TTier, TDataCenter>
        where TTier : struct
        where TDataCenter : struct
    {
        public NFigEventType Type { get; }
        public DateTimeOffset Timestamp { get; }
        public string SettingName { get; }
        public TDataCenter DataCenter { get; }
        public string User { get; }

        internal NFigEventInfo(
            NFigEventType type,
            DateTimeOffset timestamp,
            string settingName,
            TDataCenter dataCenter,
            string user)
        {
            Type = type;
            Timestamp = timestamp;
            SettingName = settingName;
            DataCenter = dataCenter;
            User = user;
        }
    }
}