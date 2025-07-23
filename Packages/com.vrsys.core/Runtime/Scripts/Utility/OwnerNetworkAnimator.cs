using Unity.Netcode.Components;

namespace VRSYS.Core.Utility
{
    public class OwnerNetworkAnimator : NetworkAnimator
    {
        protected override bool OnIsServerAuthoritative()
        {
            return false;
        }
    }
}
