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
//   Authors:        Sebastian Muehlhaus
//   Date:           2023
//-----------------------------------------------------------------

using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using VRSYS.Core.Networking;

namespace VRSYS.Core.Navigation
{
    public class TeleportTargetInteractable : XRBaseInteractable
    {
        public LocalTeleportManager localTeleportManager;

        public ITeleportManagerCallbacks teleportManager;

        [Header("Jump Reparent Settings")]

        public bool keepOriginalParentTransform = true;

        public bool selfIsTargetParentTransform = false;

        public Transform targetParentTransform;

        [Header("Static Hit Normal")]

        public bool useStaticHitNormal = true;

        public Vector3 staticHitNormal = Vector3.up;

        public NormalSpace staticNormalSpace = NormalSpace.World;

        private float prevLineBendRatio;
        private bool prevKeepSelectedTargetValid;
        
        public enum NormalSpace
        {
            World,
            Local,
            ParentLocal,
            TargetParentLocal
        }

        private void Start()
        {
            if (selfIsTargetParentTransform)
                targetParentTransform = transform;
            if (localTeleportManager != null)
                teleportManager = localTeleportManager;
        }

        private void Update()
        {
            if (!isHovered || !EnsureTeleportManager())
                return;

            RaycastHit hit;
            if (!teleportManager.GetRayInteractor().TryGetCurrent3DRaycastHit(out hit))
                return;

            hit.normal = useStaticHitNormal ? TransformToNormalSpace() : hit.normal;
            teleportManager.EvaluateRayHit(hit);
        }

        private Vector3 TransformToNormalSpace()
        {
            switch (staticNormalSpace)
            {
                case NormalSpace.Local: return transform.TransformDirection(staticHitNormal);
                case NormalSpace.ParentLocal: return transform.parent.TransformDirection(staticHitNormal);
                case NormalSpace.TargetParentLocal: return targetParentTransform.TransformDirection(staticHitNormal);
                default: return staticHitNormal;
            }
        }

        private bool EnsureTeleportManager()
        {
            if (teleportManager == null)
                teleportManager = NetworkUser.LocalInstance?.GetComponentInChildren<ITeleportManagerCallbacks>();
            return teleportManager != null;
        }

        private bool IsAllowedInteractor(IXRInteractor interactor)
        {
            return EnsureTeleportManager() && ReferenceEquals(interactor, teleportManager.GetRayInteractor());
        }

        protected override void OnHoverEntered(HoverEnterEventArgs args)
        {
            base.OnHoverEntered(args);
            if (!IsAllowedInteractor(args.interactorObject))
            {
                args.manager.HoverCancel(args.interactorObject, args.interactableObject);
                return;
            }

            ApplyRequiredInteractorSettings();
            
            if (!keepOriginalParentTransform)
                teleportManager.EvaluateTargetParentUpdate(targetParentTransform);
        }

        protected override void OnHoverExited(HoverExitEventArgs args)
        {
            base.OnHoverExited(args);
            if (IsAllowedInteractor(args.interactorObject))
            {
                if (!keepOriginalParentTransform)
                    teleportManager.EvaluateTargetParentReset();
                RestoreInteractorSettings();
                
            }
        }

        void ApplyRequiredInteractorSettings()
        {
            var ray = teleportManager.GetRayInteractor();
            prevKeepSelectedTargetValid = ray.keepSelectedTargetValid;
            ray.keepSelectedTargetValid = false;
            var lineVisual = ray.GetComponent<XRInteractorLineVisual>();
            if (lineVisual)
            {
                prevLineBendRatio = lineVisual.lineBendRatio;
                lineVisual.lineBendRatio = 1f;
            }
        }

        void RestoreInteractorSettings()
        {
            var ray = teleportManager.GetRayInteractor();
            ray.keepSelectedTargetValid = prevKeepSelectedTargetValid;
            var lineVisual = ray.GetComponent<XRInteractorLineVisual>();
            if (lineVisual)
                lineVisual.lineBendRatio = prevLineBendRatio;
        }
    }
}
