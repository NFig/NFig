using System;
using System.IO;

namespace NFig.Logging
{
    /// <summary>
    /// An enumeration of the types of events which can be logged by a <see cref="SettingsLogger{TSubApp,TTier,TDataCenter}"/>.
    /// </summary>
    public enum NFigLogEventType : byte
    {
        /// <summary>An override was set.</summary>
        SetOverride = 1,
        /// <summary>An override was cleared.</summary>
        ClearOverride = 2,
        /// <summary>Overrides were restored to a previous state.</summary>
        RestoreSnapshot = 3,
    }

    /// <summary>
    /// Represents a loggable NFig event.
    /// </summary>
    /// <typeparam name="TDataCenter"></typeparam>
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

        /// <summary>
        /// This method is primarily intended to support deserialization strategies. Manual construction should typically be performed with the alternate
        /// constructor.
        /// </summary>
        public NFigLogEvent() { }

        /// <summary>
        /// Initializes a new log event.
        /// </summary>
        /// <param name="type">The type of event.</param>
        /// <param name="globalAppName">The "Global" application name.</param>
        /// <param name="commit">The resulting commit ID (after the event has occurred).</param>
        /// <param name="timestamp">Time that the event occurred</param>
        /// <param name="settingName">The name of the setting which the event is applicable to. Use null for restore events.</param>
        /// <param name="settingValue">The new value of a setting. Use null if a no setting is being set.</param>
        /// <param name="restoredCommit">The commit ID being restored (only applicable to restore events).</param>
        /// <param name="dataCenter">The data center which was affected by the event. For restores, it should always be the default "Any" data center.</param>
        /// <param name="user"></param>
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

        /// <summary>
        /// Creates a memberwise-clone of this event.
        /// </summary>
        public NFigLogEvent<TDataCenter> Clone()
        {
            return (NFigLogEvent<TDataCenter>)MemberwiseClone();
        }

        /// <summary>
        /// Produces a binary-encoded representation of the event, which may be useful for storing.
        /// </summary>
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

        /// <summary>
        /// Deserializes binary blobs encoded by <see cref="BinarySerialize"/>.
        /// </summary>
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