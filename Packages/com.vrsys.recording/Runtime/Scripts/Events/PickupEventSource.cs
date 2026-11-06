using System.Collections.Generic;
using UnityEngine;
using VRSYS.Core.Interaction;
using VRSYS.Core.Logging;
using VRSYS.Core.Networking;

namespace VRSYS.Recording
{
    /// <summary>
    /// Registers a pickup event when a user grabs a networked interactable during a recording. The cause is the stream
    /// that represents the user (found by a subclass, e.g. the Meta avatar), the effects are the transform streams of
    /// the object. Grabs come from <see cref="InteractableNetworkState"/> on the server, so the interactors need no
    /// component.
    /// </summary>
    public abstract class PickupEventSource : SemanticEventSource
    {
        [SerializeField] private string _eventName = "PickUp";

        [Tooltip("Seconds the event starts before the grab arrived on the server. The server records a client's " +
                 "streams when they arrive, so they are as late as the grab; the margin only makes sure the event " +
                 "starts before the first recorded movement of the object.")]
        [SerializeField] private float _startMargin = 0.1f;

        private void OnEnable()
        {
            InteractableNetworkState.ServerGrabChanged += OnServerGrabChanged;
        }

        private void OnDisable()
        {
            InteractableNetworkState.ServerGrabChanged -= OnServerGrabChanged;
        }

        /// <summary>The stream that represents <paramref name="user"/>, from <paramref name="startTime"/> on.</summary>
        protected abstract bool TryGetUserCause(NetworkUser user, float startTime, out SemanticEventReference cause);

        private void OnServerGrabChanged(InteractableNetworkState interactable, ulong clientId, bool grabbed)
        {
            // Only grabs, not releases, while this server records.
            bool isRecordedGrab = grabbed && Manager != null && Manager.IsRecordingOnServer;
            if (!isRecordedGrab)
                return;

            NetworkUser user = FindUser(clientId);
            if (user == null)
            {
                RecordingLogger.LogWarning(GetType().Name, "No user found for client " + clientId + " who grabbed " +
                                                           interactable.name + ".", this);
                return;
            }

            float startTime = Mathf.Max(0.0f, Manager.RecordingTime - _startMargin);
            if (!TryGetUserCause(user, startTime, out SemanticEventReference cause))
            {
                RecordingLogger.LogWarning(GetType().Name, "No recorded stream for " + user.name + "; the pickup of " +
                                                           interactable.name + " is not registered.", this);
                return;
            }

            List<SemanticEventReference> effects = new List<SemanticEventReference>();
            Manager.CollectReferences(interactable.gameObject, startTime, false, effects);
            if (effects.Count == 0)
            {
                if (RecordingLogger.IsEnabled(LogLevel.Debug))
                    RecordingLogger.LogDebug(GetType().Name, interactable.name + " is not recorded.", this);
                return;
            }

            Manager.RegisterEvent(_eventName, user.userName.Value.ToString() + " picked up " + interactable.name,
                cause, effects);
        }

        // Users are spawned with the ownership of their client.
        private static NetworkUser FindUser(ulong clientId)
        {
            foreach (NetworkUser user in FindObjectsByType<NetworkUser>(FindObjectsSortMode.None))
                if (user.OwnerClientId == clientId)
                    return user;
            return null;
        }
    }
}
