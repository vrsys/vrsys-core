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

using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace VRSYS.Meta.Collocation
{
    public class CreateSessionStateHandler : CollocationStateHandler
    {
        #region Properties

        private CreateSessionUi _createSessionUi;

        #endregion
        
        #region Constructor

        public CreateSessionStateHandler(CollocationManager manager) : base(manager)
        {
            State = CollocationState.CreatingSession;
        }

        #endregion

        #region Collocation State Handler Methods

        public override void StartState()
        {
            InitializeCreateSessionUi();
        }

        protected override void EndState()
        {
            Object.Destroy(_createSessionUi.gameObject);
            
            _manager.EnterState<CreateSessionAnchorStateHandler>();
        }

        #endregion

        #region Private Methods

        private void InitializeCreateSessionUi()
        {
            CollocationStateMessage stateMessage = new CollocationStateMessage(State, CollocationStateStatus.Started,
                "Initializing session list ui.");
            _manager.BroadcastState(stateMessage);
            
            GameObject go = GameObject.Instantiate(_manager.CreateSessionUi.gameObject);
            _createSessionUi = go.GetComponent<CreateSessionUi>();
            
            _createSessionUi.Initialize(this);
        }

        #endregion

        #region Public Methods

        public async void CreateSession(string sessionName)
        {
            bool isFirstTry = _retryCount == 0;

            CollocationStateStatus status = isFirstTry ? CollocationStateStatus.Running : CollocationStateStatus.Retry;

            string message = isFirstTry
                ? $"Try creating collocation session {sessionName}."
                : $"Retry to start collocation session {sessionName}. Retry: {_retryCount}";

            CollocationStateMessage stateMessage = new CollocationStateMessage(State, status, message);
            _manager.BroadcastState(stateMessage);

            byte[] advertisementData = Encoding.UTF8.GetBytes(sessionName);
            var startAdvertisementResult = await OVRColocationSession.StartAdvertisementAsync(advertisementData);

            if (startAdvertisementResult.Success)
            {
                stateMessage = new CollocationStateMessage(State, CollocationStateStatus.Success,
                    $"Successfully created collocation session {sessionName} (UUID: {startAdvertisementResult.Value}");
                _manager.BroadcastState(stateMessage);
                
                _manager.SetHostInformation(startAdvertisementResult.Value);
                
                EndState();
            }
            else
            {
                if (_retryCount == _manager.MaxRetries)
                {
                    _manager.SetIsFailed(true);
                    stateMessage = new CollocationStateMessage(State, CollocationStateStatus.Failed,
                        $"Failed to start collocation session {sessionName}");
                    _manager.BroadcastState(stateMessage);
                    
                    return;
                }

                _retryCount++;

                stateMessage = new CollocationStateMessage(State, CollocationStateStatus.Retry,
                    $"Failed to start collocation session {sessionName}. Retry in {_manager.RetryTime} seconds");
                _manager.BroadcastState(stateMessage);

                await Task.Delay((int)(_manager.RetryTime * 1000));
                
                CreateSession(sessionName);
            }
        }

        #endregion
    }
}
