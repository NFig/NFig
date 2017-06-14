using Newtonsoft.Json;

namespace NFig
{
    /// <summary>
    /// Provides the blessed methods for serializing/deserializing NFig models.
    /// </summary>
    public static class NFigJson
    {
        /// <summary>
        /// Serializes an NFig model to JSON.
        /// </summary>
        public static string Serialize(object obj)
        {
            return JsonConvert.SerializeObject(obj);
        }

        /// <summary>
        /// Deserializes an NFig model from JSON.
        /// </summary>
        public static T Deserialize<T>(string json)
        {
            return JsonConvert.DeserializeObject<T>(json);
        }
    }
}