using UnityEngine;
using VRSYS.Core.Networking;

namespace VRSYS.Core.Utility
{
    public class FollowUserHead : MonoBehaviour, INetworkUserCallbacks
    {
        #region Properties
        
        [Header("Movement Configuration")]
        [Tooltip("Horizontal distance to user head, transform moves to after reset.")] [SerializeField] private float _horizontalTargetDistance = .5f;
        [Tooltip("Vertical distance to user head, transform moves to after reset.")] [SerializeField] private float _verticalTargetDistance = 0f;
        [SerializeField] private float _resetDistance = 2f;
        [SerializeField] private float _resetAngle = 90f;
        [SerializeField] private float _resetMovementSpeed = 10f; // m/s
        [SerializeField] private float _resetRotationSpeed = 90f; // degree/s
        
        private Transform _userHead;
        private bool _isResetting = false;
        bool _positionResetDone = false;
        bool _rotationResetDone = false;

        private Vector3 _targetPos
        {
            get
            {
                Vector3 vec = _userHead.position;
                vec.y += _verticalTargetDistance;
                vec.z += _horizontalTargetDistance;

                return vec;
            }
        }

        private Vector3 _targetForward
        {
            get
            {
                Vector3 vec = _userHead.forward;
                vec.y = 0;

                return vec;
            }
        }

        #endregion
        
        #region INetwrokUserCallbacks

        public void OnLocalNetworkUserSetup()
        {
            _userHead = NetworkUser.LocalInstance.head;
        }

        public void OnRemoteNetworkUserSetup(NetworkUser user)
        {
            // ... 
        }

        #endregion

        #region MonoBehaviour Methods

        private void Update()
        {
            if(_userHead == null)
                return;

            if (!_isResetting)
            {
                CheckPosition();
            }
            else
            {
                ResetPosition();
            }
        }

        #endregion

        #region Custom Methods

        private void CheckPosition()
        {
            float distanceToTargetPos = Vector3.Distance(transform.position, _targetPos);
            float angleToHead = Vector3.Angle(transform.forward, _targetForward);

            _isResetting = distanceToTargetPos >= _resetDistance || angleToHead >= _resetAngle;
        }

        private void ResetPosition()
        {
            if (!_positionResetDone)
            {
                Vector3 moveDirection = _targetPos - transform.position;
                float distance = _resetMovementSpeed * Time.deltaTime;

                if (distance >= Vector3.Distance(transform.position, _targetPos))
                {
                    transform.position = _targetPos;
                    _positionResetDone = true;
                }
                else
                {
                    transform.position += moveDirection.normalized * distance;
                }
            }

            if (_rotationResetDone)
            {
                float rotationAngle = _resetRotationSpeed * Time.deltaTime;

                if (rotationAngle >= Vector3.Angle(transform.forward, _targetForward))
                {
                    transform.rotation = Quaternion.LookRotation(_targetForward);
                    _rotationResetDone = true;
                }
                else
                {
                    transform.Rotate(Vector3.up, rotationAngle);
                }
            }

            if (_positionResetDone && _rotationResetDone)
            {
                _isResetting = false;
                _positionResetDone = false;
                _rotationResetDone = false;
            }
                
        }

        #endregion
    }
}
