namespace NFig.Metadata
{
    /// <summary>
    /// Describes basic information about a sub-app.
    /// </summary>
    public struct SubApp
    {
        /// <summary>
        /// The ID of the sub-app.
        /// </summary>
        public int Id { get; }
        /// <summary>
        /// The name of the sub-app.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Instantiates a sub-app info.
        /// </summary>
        /// <param name="id">The ID of the sub-app.</param>
        /// <param name="name">The name of the sub-app.</param>
        public SubApp(int id, string name)
        {
            Id = id;
            Name = name;
        }
    }
}