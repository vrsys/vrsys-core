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
//   Authors:        Tony Zoeppig, Karoline Brehm
//   Date:           2025
//-----------------------------------------------------------------

using UnityEngine;
using UnityEngine.Events;

namespace VRSYS.Meta.Collocation
{
    public class RestartCollocationStateHandler : CollocationStateHandler
    {
        #region Properties

        private UnityAction _endAction;

        #endregion
        
        #region Constructor

        public RestartCollocationStateHandler(CollocationManager manager, UnityAction endFunction) : base(manager)
        {
            State = CollocationState.RestartCollocation;
            _endAction = endFunction;
        }

        #endregion

        #region Collocation State Handler Methods

        public override void StartState()
        {
            ResetCollocationManager();
        }

        protected override void EndState()
        {
            _manager.BroadcastState(new CollocationStateMessage(State, CollocationStateStatus.Success,
                "Successful reset. Restarting collocation process..."));
            
            _endAction.Invoke();
        }

        #endregion

        #region Private Methods

        private void ResetCollocationManager()
        {
            _manager.BroadcastState(new CollocationStateMessage(State, CollocationStateStatus.Started,
                "Resetting current collocation to restart collocation process.."));
            
            if (_manager.IsSessionHost)
            {
                OVRColocationSession.StopAdvertisementAsync();
                _manager.ResetHostInformation();
            }

            _manager.ResetSessionData();

            GameObject.Destroy(_manager.CurrentAnchor.gameObject);
            
            _manager.SetIsSuccessfullyCollocated(false);
            _manager.SetIsFailed(false);
            
            EndState();
        }

        #endregion
    }
}
