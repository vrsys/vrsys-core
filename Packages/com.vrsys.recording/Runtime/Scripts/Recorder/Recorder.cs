using System.Runtime.InteropServices;
using UnityEngine;
using VRSYS.Core.Logging;
using Vrsys.Scripts.Recording;

namespace VRSYS.Scripts.Recording
{
    public abstract class Recorder : MonoBehaviour
    {
        [DllImport("RecordingPlugin")]
        protected static extern int GetOriginalID(int recorder_id, string object_name, int object_name_length, int object_uuid);
        
        [DllImport("RecordingPlugin")]
        protected static extern int GetOriginalID2(int recorder_id, int object_uuid);

        [DllImport("RecordingPlugin")]
        protected static extern int GetNewID(int recorder_id, int object_uuid);

        public RecorderController controller;
        
        public RecorderController Controller
        {
            get => controller;

            set
            {
                controller = value; 
                RegisterRecorder();
            }
        }
        
        public bool verbose = false;
        public bool registered = false;
        protected int id = 99999;
        protected int recorderId = 99999;
        public volatile bool inRerecordingMode = false;
        public bool preventAutoRegister = false;

        public int Id => id;

        protected float _lastReplayTime;
        
        public virtual void Start()
        {
            if(id == 99999)
                id = Utils.GetObjectName(gameObject, controller != null ? controller.replayRoot : null).GetHashCode();

            if (!preventAutoRegister && controller != null && !registered)
                RegisterRecorder();
        }

        public virtual void OnRecordingStart()
        {
            
        }

        public virtual void OnRecordingEnd()
        {
            
        }

        public virtual void OnReplayStart()
        {
            
        }
        
        public virtual void OnReplayEnd()
        {

        }

        public virtual void OnDestroy()
        {
            DeregisterRecorder();
        }

        public virtual bool Record(float recordTime)
        {
            return false;
        }

        public virtual bool Replay(float replayTime)
        {
            return false;
        }

        public virtual bool Preview(float previewTime)
        {
            return false;
        }

        public virtual void BeginRerecordCapture()
        {
            inRerecordingMode = true;
        }

        public virtual void EndRerecordCapture()
        {
            inRerecordingMode = false;
        }

        public virtual void TickRerecordCapture(float currentReplayTime)
        {
        }

        public void RegisterRecorder()
        {
            if (id == 99999)
            {
                ExtendedLogger.LogError(GetType().Name, "Error! Id not correctly set!", this);
            }

            controller.RegisterRecorder(id, this);
            recorderId = controller.RecorderID;
            registered = true;
        }

        public void DeregisterRecorder()
        {
            if(controller != null)
                controller.DeregisterRecorder(id, this);
            recorderId = 99999;
            registered = false;
        }
        
        public virtual void Update()
        {
            if (!preventAutoRegister && !registered && controller != null)
                RegisterRecorder();
        }

        public void MarkAsPreviewRecorder()
        {
            registered = false;
            preventAutoRegister = true;
        }

        public void MarkAsPortalRecorder()
        {
            registered = false;
            preventAutoRegister = true;
        }
        
        public void SetId(int customId)
        {
            id = customId;
        }
    }
}