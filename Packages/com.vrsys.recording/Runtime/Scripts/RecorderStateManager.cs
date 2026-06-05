using System.Collections.Generic;
using UnityEngine;
using VRSYS.Scripts.Recording;

namespace VRSYS.Recording.Scripts
{
    public class RecorderStateManager : MonoBehaviour
    {
        private static RecorderStateManager _instance;

        public static RecorderStateManager Instance {
            get
            {
                if (_instance == null)
                    _instance = FindAnyObjectByType<RecorderStateManager>();

                return _instance;
            }
            set
            {
                _instance = value;
            }
        }

        private List<RecorderState> _recorderStates;

        public void Awake()
        {
            _recorderStates = new List<RecorderState>();
        }

        public void RegisterRecorderState(RecorderState state)
        {
            if (IsIDPresent(state))
            {
                for (int i = 0; i < 100; ++i)
                {
                    state.recorderID = i;
                    if(!IsIDPresent(state))
                        break;
                }
            }
            
            _recorderStates.Add(state);
        }

        private bool IsIDPresent(RecorderState state)
        {
            bool idAlreadyPresent = false;
            foreach (var s in _recorderStates)
                if (s.recorderID == state.recorderID)
                    idAlreadyPresent = true;
            return idAlreadyPresent;
        }
        
        public void DeRegisterRecorderState(RecorderState state)
        {
            _recorderStates.Remove(state);
        }
    }
}