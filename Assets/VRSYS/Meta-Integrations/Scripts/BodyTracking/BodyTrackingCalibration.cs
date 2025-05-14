using System;
using UnityEngine;
using UnityEngine.InputSystem;
using VRSYS.Core.Logging;

namespace VRSYS.Meta.Bodytracking
{
    public class BodyTrackingCalibration : MonoBehaviour
    {
        #region Member Variables

        [Header("Debug")] 
        public InputAction debugRecalibrationAction;

        #endregion

        #region MonoBehaviour Callbacks

        private void Awake()
        {
            debugRecalibrationAction.Enable();
        }

        private void Update()
        {
            if(debugRecalibrationAction.WasPressedThisFrame())
                Recalibrate();
        }

        #endregion
        
        #region Custom Methods

        public void Recalibrate()
        {
            OVRBody.ResetBodyTrackingCalibration();
            
            ExtendedLogger.LogError(GetType().Name, "Raclibrated.", this);
        }

        #endregion
    }
}

