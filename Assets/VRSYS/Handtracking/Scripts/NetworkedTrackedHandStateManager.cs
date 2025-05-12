using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.Hands;
using VRSYS.Core.Logging;

namespace VRSYS.HandTracking
{
    public class NetworkedTrackedHandStateManager : NetworkBehaviour
    {
        #region Member Variables

        enum Mode
        {
            None,
            Controller,
            TrackedHand
        }
        
        // [Header("Network Properties")]
        private NetworkVariable<Mode> leftHandMode =
            new NetworkVariable<Mode>(writePerm: NetworkVariableWritePermission.Owner);
        private NetworkVariable<Mode> rightHandMode =
            new NetworkVariable<Mode>(writePerm: NetworkVariableWritePermission.Owner);

        [Header("Controller & Hands")]
        public GameObject leftController;
        public GameObject rightController;
        public GameObject leftHand;
        public GameObject rightHand;

        private XRHandSubsystem handSubsystem; // currently used subsystem
        private static readonly List<XRHandSubsystem> handSubsystems = new List<XRHandSubsystem>(); // used to store subsystems temporarily when getting them
        private readonly TrackedDeviceMonitor trackedDeviceMonitor = new TrackedDeviceMonitor();
        private readonly HashSet<int> devicesEverTracked = new HashSet<int>();

        #endregion

        #region MonoBehaviour Callbacks

        private void Start()
        {
            if (IsOwner)
            {
                if (handSubsystem == null)
                {
                    SubsystemManager.GetSubsystems(handSubsystems);
                    if (handSubsystems.Count == 0)
                    {
                        ExtendedLogger.LogWarning(GetType().Name, "No hand subsystem could be found. Are they configured in the project settings?");
                    }
                    else
                    {
                        handSubsystem = handSubsystems[0];
                    }
                }

                if (handSubsystem != null)
                    handSubsystem.trackingAcquired += OnHandTrackingAcquired;

                InputSystem.onDeviceChange += OnDeviceChange;
                trackedDeviceMonitor.trackingAcquired += OnControllerTrackingFirstAcquired;

                UpdateLeftMode();
                UpdateRightMode();
            }
            else
            {
                leftHandMode.OnValueChanged += OnLeftHandModeChanged;
                rightHandMode.OnValueChanged += OnRightHandModeChanged;
            }
        }

        #endregion

        #region Custom Methods
        
        private void OnHandTrackingAcquired(XRHand hand)
        {
            switch (hand.handedness)
            {
                case Handedness.Left:
                    SetLeftMode(Mode.TrackedHand);
                    break;
                case Handedness.Right:
                    SetRightMode(Mode.TrackedHand);
                    break;
            }
        }

        private void SetLeftMode(Mode mode)
        {
            SafeSetActive(leftHand, mode == Mode.TrackedHand);
            SafeSetActive(leftController, mode == Mode.Controller);
            leftHandMode.Value = mode;
        }
        
        private void SetRightMode(Mode mode)
        {
            SafeSetActive(rightHand, mode == Mode.TrackedHand);
            SafeSetActive(rightController, mode == Mode.Controller);
            rightHandMode.Value = mode;
        }
        
        private void SafeSetActive(GameObject gameObject, bool active)
        {
            if (gameObject != null && gameObject.activeSelf != active)
                gameObject.SetActive(active);
        }
        
        private void OnLeftHandModeChanged(Mode previousvalue, Mode newvalue)
        {
            // Same as in Ray Serializer ...
            // SetLeftMode(newvalue);
        }
        
        private void OnRightHandModeChanged(Mode previousvalue, Mode newvalue)
        {
            // Same as in Ray Serializer ...
            // SetRightMode(newvalue);
        }

        private void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            if (!(device is XRController controllerDevice))
                return;

            if (change == InputDeviceChange.Added || change == InputDeviceChange.Reconnected ||
                change == InputDeviceChange.Enabled || change == InputDeviceChange.UsageChanged)
            {
                if (!device.added)
                    return;

                var usages = device.usages;
                if (usages.Contains(CommonUsages.LeftHand))
                {
                    UpdateMode(controllerDevice, SetRightMode);
                }
                else if (usages.Contains(CommonUsages.RightHand))
                {
                    UpdateMode(controllerDevice, SetLeftMode);
                }
            }
            else if (change == InputDeviceChange.Removed || change == InputDeviceChange.Disconnected ||
                     change == InputDeviceChange.Disabled)
            {
                trackedDeviceMonitor.RemoveDevice(controllerDevice);

                var usages = device.usages;
                if (usages.Contains(CommonUsages.LeftHand))
                {
                    Mode mode = handSubsystem != null && handSubsystem.leftHand.isTracked
                        ? Mode.TrackedHand
                        : Mode.None;
                    
                    SetLeftMode(mode);
                }
                else if (usages.Contains(CommonUsages.RightHand))
                {
                    Mode mode = handSubsystem != null && handSubsystem.rightHand.isTracked
                        ? Mode.TrackedHand
                        : Mode.None;
                    
                    SetRightMode(mode);
                }
            }
        }

        private void UpdateMode(XRController controllerDevice, Action<Mode> setModeMethod)
        {
            if (controllerDevice == null)
            {
                setModeMethod(Mode.None);
                return;
            }

            if (devicesEverTracked.Contains(controllerDevice.deviceId))
            {
                setModeMethod(Mode.Controller);
            }
            else if (controllerDevice.isTracked.isPressed)
            {
                devicesEverTracked.Add(controllerDevice.deviceId);
                setModeMethod(Mode.Controller);
            }
            else
            {
                setModeMethod(Mode.None);
                devicesEverTracked.Add(controllerDevice.deviceId);
            }
        }

        private void UpdateLeftMode()
        {
            if (handSubsystem != null && handSubsystem.leftHand.isTracked)
            {
                SetLeftMode(Mode.TrackedHand);
                return;
            }

            var controllerDevice = InputSystem.GetDevice<XRController>(CommonUsages.LeftHand);
            UpdateMode(controllerDevice, SetLeftMode);
        }

        private void UpdateRightMode()
        {
            if (handSubsystem != null && handSubsystem.rightHand.isTracked)
            {
                SetRightMode(Mode.TrackedHand);
                return;
            }

            var controllerDevice = InputSystem.GetDevice<XRController>(CommonUsages.RightHand);
            UpdateMode(controllerDevice, SetRightMode);
        }
        
        private void OnControllerTrackingFirstAcquired(TrackedDevice device)
        {
            if (!(device is XRController))
                return;

            devicesEverTracked.Add(device.deviceId);

            var usages = device.usages;
            if (usages.Contains(CommonUsages.LeftHand))
            {
                if(leftHandMode.Value == Mode.None)
                    SetLeftMode(Mode.Controller);
            }
            else if (usages.Contains(CommonUsages.RightHand))
            {
                if(rightHandMode.Value == Mode.None)
                    SetRightMode(Mode.Controller);
            }
        }

        #endregion
    }
    
        /// <summary>
        /// Helper class to monitor tracked devices from Input System and invoke an event
        /// when the device is tracked. Used in the behavior to keep a GameObject deactivated
        /// until the device becomes tracked, at which point the callback method can activate it.
        /// </summary>
        class TrackedDeviceMonitor
        {
            /// <summary>
            /// Event that is invoked one time when the device is tracked.
            /// </summary>
            /// <seealso cref="AddDevice"/>
            /// <seealso cref="TrackedDevice.isTracked"/>
            public event Action<TrackedDevice> trackingAcquired;

            readonly List<int> m_MonitoredDevices = new List<int>();

            bool m_SubscribedOnAfterUpdate;

            /// <summary>
            /// Add a tracked device to monitor and invoke <see cref="trackingAcquired"/>
            /// one time when the device is tracked. The device is automatically removed
            /// from being monitored when tracking is acquired.
            /// </summary>
            /// <param name="device"></param>
            /// <remarks>
            /// Waits until the next Input System update to read if the device is tracked.
            /// </remarks>
            public void AddDevice(TrackedDevice device)
            {
                // Start subscribing if necessary
                if (!m_MonitoredDevices.Contains(device.deviceId))
                {
                    m_MonitoredDevices.Add(device.deviceId);
                    SubscribeOnAfterUpdate();
                }
            }

            /// <summary>
            /// Stop monitoring the device for its tracked status.
            /// </summary>
            /// <param name="device"></param>
            public void RemoveDevice(TrackedDevice device)
            {
                // Stop subscribing if there are no devices left to monitor
                if (m_MonitoredDevices.Remove(device.deviceId) && m_MonitoredDevices.Count == 0)
                    UnsubscribeOnAfterUpdate();
            }

            /// <summary>
            /// Stop monitoring all devices for their tracked status.
            /// </summary>
            public void ClearAllDevices()
            {
                if (m_MonitoredDevices.Count > 0)
                {
                    m_MonitoredDevices.Clear();
                    UnsubscribeOnAfterUpdate();
                }
            }

            void SubscribeOnAfterUpdate()
            {
                if (!m_SubscribedOnAfterUpdate && m_MonitoredDevices.Count > 0)
                {
                    InputSystem.onAfterUpdate += OnAfterInputUpdate;
                    m_SubscribedOnAfterUpdate = true;
                }
            }

            void UnsubscribeOnAfterUpdate()
            {
                if (m_SubscribedOnAfterUpdate)
                {
                    InputSystem.onAfterUpdate -= OnAfterInputUpdate;
                    m_SubscribedOnAfterUpdate = false;
                }
            }

            void OnAfterInputUpdate()
            {
                for (var index = 0; index < m_MonitoredDevices.Count; ++index)
                {
                    if (!(InputSystem.GetDeviceById(m_MonitoredDevices[index]) is TrackedDevice device))
                        continue;

                    if (!device.isTracked.isPressed)
                        continue;

                    // Stop monitoring and invoke event
                    m_MonitoredDevices.RemoveAt(index);
                    --index;

                    trackingAcquired?.Invoke(device);
                }

                // Once all monitored devices have been tracked, unsubscribe from the Input System callback
                if (m_MonitoredDevices.Count == 0)
                    UnsubscribeOnAfterUpdate();
            }
        }
}
