
namespace Nfig
{
    // todo: these should be types that live in user-land, not library land... need to decide the best way to enable that

    public enum DeploymentTier
    {
        Local,
        Dev,
        Simulation,
        Prod
    }

    public enum DataCenter
    {
        Local,
        NewYork,
        Oregon
    }
}
