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
//   Date:           2023
//-----------------------------------------------------------------

using Unity.Netcode;
using UnityEngine;

namespace VRSYS.Core.Interaction.Samples
{
    public class NetworkedAudioSourceController : NetworkBehaviour
    {
        #region Member Variables

        private bool initialized = false;
        
        public AudioSource audioSource;
        //private bool isPlaying = false;

        private NetworkVariable<bool> isPlaying = new NetworkVariable<bool>(false,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private NetworkVariable<float> volume = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private NetworkVariable<float> playTime = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        
        public JoystickSerializer joystick;
        
        [Range(0, 10)] public float translationVelocity = 3.0f;

        #endregion

        #region Mono & NetworkBehaviour Callbacks

        private void Awake()
        {
            Initialize();
        }

        public override void OnNetworkSpawn()
        {
            isPlaying.OnValueChanged += OnIsPlayingChanged;
            volume.OnValueChanged += OnVolumeChanged;

            if (!IsServer)
            {
                if(isPlaying.Value)
                    audioSource.Play();
                else
                    audioSource.Pause();
                
                audioSource.volume = volume.Value;
                
                audioSource.time = playTime.Value;
            }
        }

        private void Update()
        {
            if (!initialized)
            {
                Initialize();
                return;
            }

            if (IsServer)
            {
                playTime.Value = audioSource.time;
                MapInput(CalcTransInput());
            }
        }

        #endregion

        #region Custom Methods

        private void Initialize()
        {
            if (audioSource is not null)
                initialized = true;
            else
            {
                audioSource = GetComponent<AudioSource>();
                initialized = audioSource is not null;
            }

        }

        public void ToggleAudio()
        {
            ToggleAudioServerRpc();
        }
        
        public void AdjustVolume(float volume)
        {
            AdjustVolumeServerRpc(volume);
        }

        private void MapInput(Vector3 transInput)
        {
            transform.Translate(transInput);
        }

        private Vector3 CalcTransInput()
        {
            Vector3 joystickInput = new Vector3(joystick.joystickValue.Value.x, 0f, joystick.joystickValue.Value.y);
            return joystickInput * (translationVelocity * Time.deltaTime);
        }
        
        private void OnIsPlayingChanged(bool previousvalue, bool newvalue)
        {
            if (initialized)
            {
                if(isPlaying.Value)
                    audioSource.Play();
                else
                    audioSource.Stop();
            }
        }
        
        private void OnVolumeChanged(float previousvalue, float newvalue)
        {
            if(initialized)
                audioSource.volume = volume.Value;
        }

        #endregion

        #region RPCs

        [ServerRpc(RequireOwnership = false)]
        public void ToggleAudioServerRpc(ServerRpcParams rpcParams = default)
        {
            if (initialized)
            {
                isPlaying.Value = !isPlaying.Value;
                if (isPlaying.Value)
                    audioSource.Play();
                else
                    audioSource.Stop();
            }
        }
        
        [ServerRpc(RequireOwnership = false)]
        public void AdjustVolumeServerRpc(float volume, ServerRpcParams rpcParams = default)
        {
            if (initialized)
            {
                this.volume.Value = volume;
            }
        }

        #endregion
    }
}
