namespace NFig
{
    /// <summary>
    /// Describes a connection to a data-store for NFig overrides and metadata. Store-providers must inherit from this class.
    /// </summary>
    public abstract class NFigStore<TTier, TDataCenter>
        where TTier : struct
        where TDataCenter : struct
    {
        //
    }
}