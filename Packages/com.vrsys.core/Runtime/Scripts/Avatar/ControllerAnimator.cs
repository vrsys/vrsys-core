// VRSYS plugin of Virtual Reality and Visualization Group (Bauhaus-University Weimar)
//  _    ______  _______  _______
// | |  / / __ \/ ___/\ \/ / ___/
// | | / / /_/ /\__ \  \  /\__ \ 
// | |/ / _, _/___/ /  / /___/ / 
// |___/_/ |_|/____/  /_//____/  
//
//  __                            __                       __   __   __    ___ .  . ___
// |__)  /\  |  | |__|  /\  |  | /__`    |  | |\ | | \  / |__  |__) /__` |  |   /\   |  
// |__) /~~\ \__/ |  | /~~\ \__/ .__/    \__/ | \| |  \/  |___ |  \ .__/ |  |  /~~\  |  
//
//       ___               __                                                           
// |  | |__  |  |\/|  /\  |__)                                                          
// |/\| |___ |  |  | /~~\ |  \                                                                                                                                                                                     
//
// Copyright (c) 2023 Virtual Reality and Visualization Group
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:

// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.

// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.
//-----------------------------------------------------------------
//   Authors:        Tony Jan Zoeppig
//   Date:           2026
//-----------------------------------------------------------------

using Unity.Netcode;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using VRSYS.Core.Logging;

namespace VRSYS.Core.Avatar
{
    public class ControllerAnimator : MonoBehaviour
    {
        #region Struct

        public struct ControllerValueData : INetworkSerializable
        {
            public Vector2 stickValue;
            public float triggerValue;
            public float gripValue;

            public ControllerValueData(Vector2 stickValue, float triggerValue, float gripValue)
            {
                this.stickValue = stickValue;
                this.triggerValue = triggerValue;
                this.gripValue = gripValue;
            }

            public void SetData(Vector2 stickValue, float triggerValue, float gripValue)
            {
                this.stickValue = stickValue;
                this.triggerValue = triggerValue;
                this.gripValue = gripValue;
            }

            public void SetData(ControllerValueData data)
            {
                stickValue = data.stickValue;
                triggerValue = data.triggerValue;
                gripValue = data.gripValue;
            }

            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                if (serializer.IsReader)
                {
                    var reader = serializer.GetFastBufferReader();
                    reader.ReadValueSafe(out stickValue);
                    reader.ReadValueSafe(out triggerValue);
                    reader.ReadValueSafe(out gripValue);
                }
                else
                {
                    var writer = serializer.GetFastBufferWriter();
                    writer.WriteValueSafe(stickValue);
                    writer.WriteValueSafe(triggerValue);
                    writer.WriteValueSafe(gripValue);
                }
            }
        }

        #endregion
        
        #region Properties

        [Header("Thumbstick")] 
        [SerializeField] private Transform _thumbstuickTransform;
        [SerializeField] private Vector2 _stickRotationRange = new Vector2(30f, 30f);
        [SerializeField] private XRInputValueReader<Vector2> _stickInput;
        
        [Header("Trigger")]
        [SerializeField] private Transform _triggerTransform;
        [SerializeField] private Vector2 _triggerXAxisRotationRange = new Vector2(0f, -15f);
        [SerializeField] private XRInputValueReader<float> _triggerInput;
        
        [Header("Grip")]
        [SerializeField] private Transform _gripTransform;
        [SerializeField] private Vector2 _gripRightRange = new Vector2(-0.0125f, -0.011f);
        [SerializeField] private XRInputValueReader<float> _gripInput;

        private ControllerValueData _controllerValueData;
        public ControllerValueData controllerValueData => _controllerValueData;

        public bool readControllerData { get; private set; } = true;

        #endregion

        #region MonoBehaviour Methods

        private void Awake()
        {
            _controllerValueData = new ControllerValueData(Vector2.zero, 0f, 0f);
        }

        private void OnEnable()
        {
            if (_thumbstuickTransform == null || _triggerTransform == null || _gripTransform == null)
            {
                enabled = false;
                ExtendedLogger.LogWarning(GetType().Name, "Transform references missing.", this);
                return;
            }

            if (readControllerData)
            {
                EnableInputReader();
            }
        }

        private void OnDisable()
        {
            DisableInputReader();
        }

        private void Update()
        {
            if (readControllerData)
                ReadControllerInput();

            ApplyControllerInput();
        }

        #endregion

        #region Public Methods

        public void EnableReadControllerData()
        {
            readControllerData = true;
            
            if(enabled)
                EnableInputReader();
        }

        public void DisableReadControllerData()
        {
            readControllerData = false;
            
            DisableInputReader();
        }

        public void SetControllerValues(ControllerValueData data)
        {
            _controllerValueData.SetData(data);
        }

        #endregion

        #region Private Methods

        private void EnableInputReader()
        {
            _stickInput.EnableDirectActionIfModeUsed();
            _triggerInput.EnableDirectActionIfModeUsed();
            _gripInput.EnableDirectActionIfModeUsed();
        }

        private void DisableInputReader()
        {
            _stickInput.DisableDirectActionIfModeUsed();
            _triggerInput.DisableDirectActionIfModeUsed();
            _gripInput.DisableDirectActionIfModeUsed();
        }

        private void ReadControllerInput()
        {
            _controllerValueData.SetData(_stickInput.ReadValue(), _triggerInput.ReadValue(), _gripInput.ReadValue());
        }

        private void ApplyControllerInput()
        {
            _thumbstuickTransform.localRotation = Quaternion.Euler(
                -_controllerValueData.stickValue.y * _stickRotationRange.x, 
                0f,
                _controllerValueData.stickValue.x * _stickRotationRange.y);

            _triggerTransform.localRotation = Quaternion.Euler(
                Mathf.Lerp(_triggerXAxisRotationRange.x, _triggerXAxisRotationRange.y, _controllerValueData.triggerValue), 
                0f, 
                0f);

            Vector3 currentGripPos = _gripTransform.localPosition;
            _gripTransform.localPosition =
                new Vector3(Mathf.Lerp(_gripRightRange.x, _gripRightRange.y, _controllerValueData.gripValue),
                    currentGripPos.y, 
                    currentGripPos.z);
        }

        #endregion


    }
}
