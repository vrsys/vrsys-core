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
//   Authors:        Tony Zoeppig
//   Date:           2025
//-----------------------------------------------------------------

using UnityEngine;

namespace VRSYS.Meta.Collocation
{
    [CreateAssetMenu(menuName = "VRSYS/Meta/Scriptable Objects/Collocation Settings")]
    public class CollocationSettings : ScriptableObject
    {
        #region Player Pref Keys

        private const string DISCOVERY_TIME_KEY = "DiscoveryTime";
        private const string RETRY_TIME_KEY = "RetryTime";
        private const string MAX_RETRIES_KEY = "MaxRetries";
        private const string USE_LOCAL_ANCHOR_KEY = "UseLocalAnchor";
        private const string TRY_LOAD_LOCAL_ANCHOR_KEY = "TryLoadLocalAnchor";
        private const string USE_DEFAULT_SESSION_ANCHOR_KEY = "UseDefaultSessionAnchor";

        private const string DEFAULT_SESSION_ANCHOR_WORLD_POS_X = "DefaultSessionAnchorWorldPosX";
        private const string DEFAULT_SESSION_ANCHOR_WORLD_POS_Y = "DefaultSessionAnchorWorldPosY";
        private const string DEFAULT_SESSION_ANCHOR_WORLD_POS_Z = "DefaultSessionAnchorWorldPosZ";

        private const string AUTO_START_SESSION_KEY = "AutoStartCollocationSession";
        private const string DEFAULT_SESSION_NAME_KEY = "DefaultCollocationSessionName";

        private const string TRUE_KEY = "True";
        private const string FALSE_KEY = "False";

        #endregion
        
        #region Properties

        [SerializeField, Tooltip("If true, values get stored in PlayerPrefs when changed and loaded from PlayerPrefs when accessed.")]
        private bool _usePlayerPrefs = false;

        [SerializeField, Tooltip("Time in seconds defining how long existing collocation sessions are searched.")] 
        private float _discoveryTime = 10f;
        public float DiscoverTime
        {
            get => GetFloatValue(DISCOVERY_TIME_KEY, _discoveryTime);
            set
            {
                _discoveryTime = value;
                
                if(_usePlayerPrefs)
                    PlayerPrefs.SetFloat(DISCOVERY_TIME_KEY, _discoveryTime);
            }
        }

        [SerializeField, Tooltip("Time in seconds defining how long system waits before retrying failed action.")]
        private float _retryTime = 1f;
        public float RetryTime
        {
            get => GetFloatValue(RETRY_TIME_KEY, _retryTime);
            set
            {
                _retryTime = value;
                
                if(_usePlayerPrefs)
                    PlayerPrefs.SetFloat(RETRY_TIME_KEY, _retryTime);
            }
        }

        [SerializeField, Tooltip("Defines how often failed actions are retried, before process stops.")]
        private int _maxRetries = 5;
        public int MaxRetries
        {
            get => GetIntValue(MAX_RETRIES_KEY, _maxRetries);
            set
            {
                _maxRetries = value;
                
                if(_usePlayerPrefs)
                    PlayerPrefs.SetInt(MAX_RETRIES_KEY, _maxRetries);
            }
        }

        [SerializeField, Tooltip("If true, local anchor is used to create collocation session.")]
        private bool _useLocalAnchor = false;
        public bool UseLocalAnchor
        {
            get => GetBoolValue(USE_LOCAL_ANCHOR_KEY, _useLocalAnchor);
            set
            {
                _useLocalAnchor = value;
                
                if(_usePlayerPrefs)
                    PlayerPrefs.SetString(USE_LOCAL_ANCHOR_KEY, BoolToString(_useLocalAnchor));
            }
        }

        [SerializeField, Tooltip("If true, tries to load previous anchor automatically.")]
        private bool _tryLoadLocalAnchor = false;
        public bool TryLoadLocalAnchor
        {
            get => GetBoolValue(TRY_LOAD_LOCAL_ANCHOR_KEY, _tryLoadLocalAnchor);
            set
            {
                _tryLoadLocalAnchor = value;
                
                if(_usePlayerPrefs)
                    PlayerPrefs.SetString(TRY_LOAD_LOCAL_ANCHOR_KEY, BoolToString(_tryLoadLocalAnchor));
            }
        }

        [SerializeField, Tooltip("If true, session anchor is always created at DefaultSessionAnchorWorldPosition.")]
        private bool _useDefaultSessionAnchor = false;
        public bool UserDefaultSessionAnchor
        {
            get => GetBoolValue(USE_DEFAULT_SESSION_ANCHOR_KEY, _useDefaultSessionAnchor);
            set
            {
                _useDefaultSessionAnchor = value;
                
                if(_usePlayerPrefs)
                    PlayerPrefs.SetString(USE_DEFAULT_SESSION_ANCHOR_KEY, BoolToString(_useDefaultSessionAnchor));
            }
        }
        
        [SerializeField, Tooltip("World position at which default anchor is created.")]
        private Vector3 _defaultSessionAnchorWorldPos = Vector3.zero;

        public Vector3 DefaultSessionAnchorWorldPos
        {
            get => GetVector3Value(
                DEFAULT_SESSION_ANCHOR_WORLD_POS_X, 
                DEFAULT_SESSION_ANCHOR_WORLD_POS_Y,
                DEFAULT_SESSION_ANCHOR_WORLD_POS_Z, 
                _defaultSessionAnchorWorldPos);
            set
            {
                _defaultSessionAnchorWorldPos = value;
                
                if(_usePlayerPrefs)
                    SetVector3Value(
                        DEFAULT_SESSION_ANCHOR_WORLD_POS_X, 
                        DEFAULT_SESSION_ANCHOR_WORLD_POS_Y,
                        DEFAULT_SESSION_ANCHOR_WORLD_POS_Z, 
                        _defaultSessionAnchorWorldPos);
            }
        }

        [SerializeField, Tooltip("If true, a session with the given default name is auto started/joined. " +
                                 "Session creation steps are skipped.")]
        private bool _autoStartSession = false;
        public bool AutoStartSession
        {
            get => GetBoolValue(AUTO_START_SESSION_KEY, _autoStartSession);
            set
            {
                _autoStartSession = value;
                
                if(_usePlayerPrefs)
                    PlayerPrefs.SetString(AUTO_START_SESSION_KEY, BoolToString(_autoStartSession));
            }
        }

        [SerializeField, Tooltip("Session name used, if collocation session is auto started/joined.")]
        private string _defaultSessionName = "VRSYS-Collocation";
        public string DefaultSessionName
        {
            get => GetStringValue(DEFAULT_SESSION_NAME_KEY, _defaultSessionName);
            set
            {
                _defaultSessionName = value;
                
                if(_usePlayerPrefs)
                    PlayerPrefs.SetString(DEFAULT_SESSION_NAME_KEY, _defaultSessionName);
            }
        }

        #endregion

        #region Private Methods

        private float GetFloatValue(string playerPrefKey, float currentValue)
        {
            if (!_usePlayerPrefs)
                return currentValue;
            
            float value = PlayerPrefs.GetFloat(playerPrefKey);

            if (value == 0.0f)
            {
                value = currentValue;
                PlayerPrefs.SetFloat(playerPrefKey, value);
            }

            return value;
        }

        private int GetIntValue(string playerPrefKey, int currentValue)
        {
            if (!_usePlayerPrefs)
                return currentValue;
            
            int value = PlayerPrefs.GetInt(playerPrefKey);

            if (value == 0)
            {
                value = currentValue;
                PlayerPrefs.SetInt(playerPrefKey, value);
            }

            return value;
        }

        private string GetStringValue(string playerPrefKey, string currentValue)
        {
            if (!_usePlayerPrefs)
                return currentValue;
            
            string value = PlayerPrefs.GetString(playerPrefKey);

            if (string.IsNullOrEmpty(value))
            {
                value = currentValue;
                PlayerPrefs.SetString(playerPrefKey, value);
            }

            return value;
        }

        private bool GetBoolValue(string playerPrefKey, bool currentValue)
        {
            if (!_usePlayerPrefs)
                return currentValue;
            
            bool value;
            
            string s = GetStringValue(playerPrefKey, "");

            if (string.IsNullOrEmpty(s))
            {
                value = currentValue;
            }
            else
            {
                value = s == TRUE_KEY;
            }

            return value;
        }

        private string BoolToString(bool value)
        {
            return value ? TRUE_KEY : FALSE_KEY;
        }

        private Vector3 GetVector3Value(string xKey, string yKey, string zKey, Vector3 currentValue)
        {
            if (!_usePlayerPrefs)
                return currentValue;
            
            Vector3 value = new Vector3(
                PlayerPrefs.GetFloat(xKey),
                PlayerPrefs.GetFloat(yKey),
                PlayerPrefs.GetFloat(zKey)
                );

            if (value == Vector3.zero)
            {
                value = currentValue;
                SetVector3Value(xKey, yKey, zKey, value);
            }

            return value;
        }

        private void SetVector3Value(string xKey, string yKey, string zKey, Vector3 value)
        {
            PlayerPrefs.SetFloat(xKey, value.x);
            PlayerPrefs.SetFloat(yKey, value.y);
            PlayerPrefs.SetFloat(zKey, value.z);
        }

        #endregion
    }
}
