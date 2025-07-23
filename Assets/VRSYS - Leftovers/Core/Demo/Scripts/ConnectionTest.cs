using Unity.Netcode;
using VRSYS.Core.Logging;

public class ConnectionTest : NetworkBehaviour
{
    public override void OnNetworkSpawn()
    {
        ExtendedLogger.LogInfo(GetType().Name, "OnNetworkSpawn Called!", this);
        
        if(IsServer)
            ExtendedLogger.LogInfo(GetType().Name, "Is Server!", this);
        
        if(IsHost)
            ExtendedLogger.LogInfo(GetType().Name, "Is Host", this);
        
        if(IsClient)
            ExtendedLogger.LogInfo(GetType().Name, "Is Client", this);
    }
}
