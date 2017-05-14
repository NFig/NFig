namespace NFig
{
    /// <summary>
    /// Provides methods for consuming NFig settings within an application.
    /// </summary>
    public class NFigAppClient<TSettings, TTier, TDataCenter>
        where TSettings : class, INFigSettings<int, TTier, TDataCenter>, new() // todo remove TSubApp
        where TTier : struct
        where TDataCenter : struct
    {
        //
    }
}