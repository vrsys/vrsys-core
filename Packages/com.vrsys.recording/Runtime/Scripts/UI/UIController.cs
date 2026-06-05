using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using VRSYS.Core.Networking;
using Vrsys.Scripts.Recording;

namespace VRSYS.Scripts.Recording
{
    [RequireComponent(typeof(RecorderState))]
    [RequireComponent(typeof(TimeInteractor))]
    [RequireComponent(typeof(NetworkController))]
    public class UIController : MonoBehaviour
    {
        public GameObject recordingUICanvas;

        private RecorderState _state;
        private NetworkController _networkController;

        private Vector3 initalUIParentRotation;
        private Matrix4x4 lastDiff;
        private Vector3 targetUIParentRotation;
        private float rotationStartTime = -1.0f;
        private float rotationEndTime = -1.0f;
        private Vector3 initalUIParentPosition;
        private float positionStartTime = -1.0f;
        private float positionEndTime = -1.0f;
        private GameObject _localUser;
        private bool firstUI = true;
        
        private GameObject slider;
        private GameObject timeHandle;
        private GameObject currentTimeHandle;

        private Slider sliderComponent;

        private Text time;
        private Text startTime;
        private Text endTime;
        
        
        private bool _isHmd = false;
        private bool _timeLineCollabActive = false;
        private bool uiActive = false;
        private int sliderWidth;
        private bool drawnOnce;

        private float _uiToggleTime;
        
        public void Start()
        {
            _state = GetComponent<RecorderState>();
            _networkController = GetComponent<NetworkController>();

            _isHmd = true;
            
            slider = Utils.GetChildByName(recordingUICanvas,"TimeSlider");
            startTime =  Utils.GetChildByName(recordingUICanvas, "StartTime").GetComponent<Text>();       
            endTime =  Utils.GetChildByName(recordingUICanvas,"EndTime").GetComponent<Text>();

            sliderComponent = slider.GetComponent<Slider>();
            currentTimeHandle = Utils.GetChildByName(slider, "CurrentTimeHandle");

            sliderComponent.onValueChanged.AddListener(delegate(float val) { SetSliderStatus(val); });
            
            timeHandle = Utils.GetChildByName(slider, "Handle");
            time = Utils.GetChildByName(timeHandle, "Time").GetComponent<Text>();

            slider.SetActive(false);
        }
        
        public void Update()
        {
            if (_state.currentState == State.Idle)
            {
                slider.SetActive(false);

                if(_timeLineCollabActive)
                    RemoveTimeLineCollaborators();
            }
            
            if (_state.currentState == State.Recording)
            {
                slider.SetActive(false);
            }
            
            
            if (_state.currentState == State.Replaying)
            {
                time.text = _state.currentReplayTime.ToString("F1");
                if(_state.currentMinSliderValue > 60.0f)
                    startTime.text = (_state.currentMinSliderValue / 60.0f).ToString("F2");
                else 
                    startTime.text = _state.currentMinSliderValue.ToString("F2");
                if(_state.currentMaxSliderValue > 60.0f)
                    endTime.text = (_state.currentMaxSliderValue / 60.0f).ToString("F2");
                else 
                    endTime.text = _state.currentMaxSliderValue.ToString("F2");
                
                slider.SetActive(true);
                
                //time.text = "";
                //startTime.text = "";
                //endTime.text = "";

                TimeLineCollaborators();

                sliderComponent.minValue = _state.currentMinSliderValue;
                sliderComponent.maxValue = _state.currentMaxSliderValue;
                sliderComponent.value = _state.currentReplayTime;
            }
        }

        public void NavigateToTime(Slider userSliderComponent)
        {
            Debug.LogError("Listener called");

            if(_state.currentState == State.Replaying && 0 <= userSliderComponent.value && userSliderComponent.value <= _state.recordingDuration)
                _state.currentReplayTime = userSliderComponent.value;
            else 
                Debug.LogWarning("Cannot navigate to target ime. Incorrect state or target time.");
        }
        
        private void TimeLineCollaborators()
        {
            _timeLineCollabActive = true;
            
            if(NetworkUser.LocalInstance == null)
                return;
            
            // update slider position for all other users currently present in the replay
            foreach (var key in _networkController._userReplayTimes.Keys.ToList())
            {
                if (key != NetworkUser.LocalInstance.name)
                {
                    // TODO: fix
                    if (true /*PhotonNetwork.CurrentRoom.Players.ContainsKey(key)*/)
                    {
                        float userTime = _networkController._userReplayTimes[key];
                        float userPreviewTime = -1.0f;//_networkController._userPreviewTimes[key];
                        string userName = key;

                        GameObject tSlider = Utils.GetChildByName(recordingUICanvas, "TimeSlider" + key);
                        GameObject tSliderButton = Utils.GetChildByName(recordingUICanvas, "TimeSliderButton" + key);
                        GameObject user = Utils.GetGameObjectBySubstring(userName);

                        // create time slider for user if the slider does not exist yet
                        if (tSlider == null)
                        {
                            tSlider = Instantiate(slider);
                            Slider userSliderComponent = tSlider.GetComponent<Slider>();
                            tSlider.name = "TimeSlider" + key;
                            tSlider.transform.parent = slider.transform.parent;
                            tSlider.transform.localPosition = slider.transform.localPosition;
                            tSlider.transform.localRotation = slider.transform.localRotation;
                            RectTransform rectTransform = tSlider.GetComponent<RectTransform>();
                            RectTransform sliderTransform = slider.GetComponent<RectTransform>();
                            rectTransform.localPosition = sliderTransform.localPosition;
                            rectTransform.localScale = sliderTransform.localScale;
                            rectTransform.localRotation = sliderTransform.localRotation;
                            
                            //float offset = 10.0f;
                            //Vector3 position = rectTransform.localPosition;
                            //rectTransform.localPosition = new Vector3(position.x, position.y + offset, position.z);
                            
                            GameObject background = Utils.GetChildBySubstring(tSlider, "Background");
                            GameObject fill = Utils.GetChildBySubstring(tSlider, "Fill");
                            GameObject startT = Utils.GetChildBySubstring(tSlider, "StartTime");
                            GameObject endT = Utils.GetChildBySubstring(tSlider, "EndTime");
                            GameObject handleArea = Utils.GetChildBySubstring(tSlider, "Handle Slide Area");
                            GameObject timeLines = Utils.GetChildBySubstring(handleArea, "TimeLines");
                            GameObject handle = Utils.GetChildBySubstring(handleArea, "Handle");

                            tSliderButton = new GameObject("TimeSliderButton" + key);
                            tSliderButton.transform.SetParent(tSlider.transform.parent, false);
                            tSliderButton.transform.localPosition = Vector3.zero;
                            Button timeSliderButton = tSliderButton.AddComponent<Button>();
                            Image image = tSliderButton.AddComponent<Image>();
                            image.material.mainTexture = handle.GetComponent<Image>().mainTexture;
                            timeSliderButton.image = image;
                           
                            
                            if (timeSliderButton != null)
                            {
                                timeSliderButton.onClick.AddListener(() => NavigateToTime(userSliderComponent));
                                userSliderComponent.interactable = true;
                                Debug.LogError("Listener set");
                            }

                            ColorBlock colors = new ColorBlock();
                            colors.normalColor = timeSliderButton.image.color;
                            timeSliderButton.colors = colors;
                            
                            background.SetActive(false);
                            fill.SetActive(false);
                            startT.SetActive(false);
                            endT.SetActive(false);
                            timeLines.SetActive(false);
                            Debug.Log("New slider created for user: " + key);
                        }

                        Slider userSlider = tSlider.GetComponent<Slider>();
                        userSlider.minValue = _state.currentMinSliderValue;
                        userSlider.maxValue = _state.currentMaxSliderValue;
                        userSlider.value = userTime;
                        userSlider.interactable = false;
                        userSlider.transition = Selectable.Transition.None;

                        GameObject tHandleArea = Utils.GetChildBySubstring(tSlider, "Handle Slide Area");
                        GameObject tSliderHandle = Utils.GetChildBySubstring(tHandleArea, "Handle");
                        tSliderButton.transform.position = tSliderHandle.transform.position;
                        
                        Utils.GetChildBySubstring(tSlider, "Time").GetComponent<Text>().text = "#" + key;
                        
                        float deltaT = Mathf.Abs(_state.currentReplayTime - userTime);
               
                        if (deltaT >= 0.05f || true)
                            tSlider.SetActive(true);
                        
                        // display time of the user if time difference is greater than a certain threshold
                        if (Mathf.Abs(_state.currentReplayTime - userTime) <= 1.0f)
                            tSlider.SetActive(false);
                    }
                    else
                    {
                        GameObject tSlider = Utils.GetChildByName(recordingUICanvas, "TimeSlider" + key);
                        if(tSlider != null)
                            Destroy(tSlider);
                        GameObject tSliderButton = Utils.GetChildByName(recordingUICanvas, "TimeSliderButton" + key);
                        if(tSliderButton != null)
                            Destroy(tSliderButton);
                    }
                }
            }
        }

        private void SetSliderStatus(float value)
        {
            if (value <= 0.1f)
                value = 0.1f;
            
            if (_state.currentState == State.Replaying)
            {
                currentTimeHandle.SetActive(false);
                _state.currentReplayTime = value;
            }
        }
        
        private void RemoveTimeLineCollaborators()
        {
            // TODO: Fix
            /*
            // update slider position for all other users currently present in the replay
            foreach (var key in _networkController._userReplayTimes.Keys.ToList())
            {
                if (key != PhotonNetwork.LocalPlayer.ActorNumber)
                {
                    GameObject tSlider = Utils.GetChildByName(recordingUICanvas, "TimeSlider" + key);
                    if(tSlider != null)
                        Destroy(tSlider);
                }
            }

            _timeLineCollabActive = false;
            */
        }
    }
}