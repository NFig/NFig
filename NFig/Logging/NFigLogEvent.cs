using System;
using System.IO;

namespace NFig.Logging
{
    public enum NFigLogEventType : byte
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
        public NFigLogEventType Type { get; set; }
        /// <summary>
        /// The top-level name of the application this event was applicable to.
        /// </summary>
        public string GlobalAppName { get; set; }
        /// <summary>
        /// The commit immediately after this change was applied.
        /// </summary>
        public string Commit { get; set; }
        /// <summary>
        /// The time the event occurred.
        /// </summary>
        public DateTime Timestamp { get; set; }
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
            NFigLogEventType type,
            string globalAppName,
            string commit,
            DateTime timestamp,
            string settingName,
            string settingValue,
            string restoredCommit,
            TDataCenter dataCenter,
            string user)
        {
            Type = type;
            GlobalAppName = globalAppName;
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

        public byte[] BinarySerialize()
        {
            using (var s = new MemoryStream())
            using (var w = new BinaryWriter(s))
            {
                w.Write((byte)1); // version - might be useful in the future
                w.Write((byte)Type);
                w.WriteNullableString(GlobalAppName);
                w.WriteNullableString(Commit);
                w.Write(Timestamp.ToBinary());
                w.WriteNullableString(SettingName);
                w.WriteNullableString(SettingValue);
                w.WriteNullableString(RestoredCommit);
                w.Write(Convert.ToUInt32(DataCenter));
                w.WriteNullableString(User);

                return s.ToArray();
            }
        }

        public static NFigLogEvent<TDataCenter> BinaryDeserialize(byte[] data)
        {
            if (data == null || data.Length == 0)
                return null;

            using (var s = new MemoryStream(data))
            using (var r = new BinaryReader(s))
            {
                var version = r.ReadByte();

                var log = new NFigLogEvent<TDataCenter>();

                log.Type = (NFigLogEventType)r.ReadByte();
                log.GlobalAppName = r.ReadNullableString();
                log.Commit = r.ReadNullableString();
                log.Timestamp = DateTime.FromBinary(r.ReadInt64());
                log.SettingName = r.ReadNullableString();
                log.SettingValue = r.ReadNullableString();
                log.RestoredCommit = r.ReadNullableString();
                log.DataCenter = (TDataCenter)Enum.ToObject(typeof(TDataCenter), r.ReadUInt32());
                log.User = r.ReadNullableString();

                return log;
            }
        }
    }
}