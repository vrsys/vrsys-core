using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.Hands;
using VRSYS.Core.Logging;

namespace VRSYS.HandTracking
{
    public class LocalTrackedHandStateManager : MonoBehaviour
    {
        #region Member Variables

        enum Mode
        {
            None,
            Controller,
            TrackedHand
        }
        
        // [Header("Network Properties")]
        private Mode leftHandMode;
        private Mode rightHandMode;

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
            leftHandMode = mode;
        }
        
        private void SetRightMode(Mode mode)
        {
            SafeSetActive(rightHand, mode == Mode.TrackedHand);
            SafeSetActive(rightController, mode == Mode.Controller);
            rightHandMode = mode;
        }
        
        private void SafeSetActive(GameObject gameObject, bool active)
        {
            if (gameObject != null && gameObject.activeSelf != active)
                gameObject.SetActive(active);
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
                if(leftHandMode == Mode.None)
                    SetLeftMode(Mode.Controller);
            }
            else if (usages.Contains(CommonUsages.RightHand))
            {
                if(rightHandMode == Mode.None)
                    SetRightMode(Mode.Controller);
            }
        }

        #endregion
    }
}
