using UnityEngine;

namespace VRSYS.Recording
{
    /// <summary>
    /// Added by <see cref="ContactEventSource"/> to recorded Rigidbodies for a recording. Reports contacts in which
    /// another body runs into this one while it is at rest.
    /// </summary>
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    public class SemanticContactRelay : MonoBehaviour
    {
        private ContactEventSource _source;
        private Rigidbody _body;
        private bool _wasResting;

        internal void Init(ContactEventSource source, Rigidbody body)
        {
            _source = source;
            _body = body;
        }

        private void FixedUpdate()
        {
            // State before this physics step; OnCollisionEnter comes after it.
            _wasResting = _body != null && !_body.isKinematic &&
                          _body.linearVelocity.magnitude < _source.RestSpeed;
        }

        private void OnCollisionEnter(Collision collision)
        {
            // Another body (not a static collider) ran into this one while it was at rest.
            Rigidbody mover = collision.rigidbody;
            bool hitByOtherBody = mover != null && mover != _body;
            if (hitByOtherBody && _wasResting && _source != null)
                _source.OnContact(mover, _body);
        }
    }
}
