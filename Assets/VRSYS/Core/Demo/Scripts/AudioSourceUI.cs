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

using System;
using TMPro;
using UnityEngine;

namespace VRSYS.Core.Interaction.Samples
{
    public class AudioSourceUI : MonoBehaviour
    {
        #region Member Variables

        [SerializeField] private AudioSource audioSource;
        private bool isPlaying = false;
        private float volume = 0f;
        
        public TextMeshPro playStateText;
        public TextMeshPro volumeText;
        
        public Color onColor = Color.cyan;
        public Color offColor = Color.gray;

        #endregion

        #region MonoBehaviour Callbacks

        private void Start()
        {
            if(audioSource == null)
                audioSource = GetComponent<AudioSource>();
            
            UpdatePlayState();

            UpdateVolume();
        }

        private void Update()
        {
            if(isPlaying != audioSource.isPlaying)
                UpdatePlayState();

            if(volume != audioSource.volume)
                UpdateVolume();
        }

        #endregion

        #region Custom Methods

        private void UpdatePlayState()
        {
            isPlaying = audioSource.isPlaying;
            playStateText.text = isPlaying ? "ON" : "OFF";
            playStateText.color = isPlaying ? onColor : offColor;
        }

        private void UpdateVolume()
        {
            volume = audioSource.volume;
            volumeText.text = Math.Round(volume * 100, 2).ToString();
        }

        #endregion
    }
}
