using VRSYS.Core.Networking;

namespace VRSYS.Recording
{
    /// <summary>Pickup events whose cause is the Meta avatar stream of the grabbing user.</summary>
    public class MetaAvatarPickupEventSource : PickupEventSource
    {
        protected override bool TryGetUserCause(NetworkUser user, float startTime, out SemanticEventReference cause)
        {
            // The avatar recorder sits on the user's MetaAvatarReplayDataReader.
            foreach (MetaAvatarRecorder recorder in user.GetComponentsInChildren<MetaAvatarRecorder>(true))
            {
                if (recorder.controller != Manager.Controller)
                    continue;
                cause = SemanticEventReference.For(recorder, startTime);
                return true;
            }

            cause = default;
            return false;
        }
    }
}
