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
//   Authors:        Ephraim Schott, Tony Jan Zoeppig, Sebastian Muehlhaus 
//   Date:           2023
//-----------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;

namespace VRSYS.Core.Interaction
{
    [RequireComponent(typeof(InteractableNetworkState))]
    public class TwoHandedInteractable : UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable
    {
        #region Member Variables

        private InteractableNetworkState networkState;
        private bool isLocallyGrabbed;
        
        private Dictionary<int, Matrix4x4> interactorOffsets;
        private Dictionary<int, Matrix4x4> contactPointOffsets;
        
        private float contactDistance = -1f;
        private bool firstLoop = true;
        private Vector4 firstInteractorUp;
        private Vector4 secondInteractorUp;

        private int lastframe_first_pointer_axis_index;
        private int lastframe_second_pointer_axis_index;
        private float first_pointer_offset_angle = 0f;
        private float second_pointer_offset_angle = 0f;
        private Vector3 last_frame_average;
        private Matrix4x4 contactOffsetMat;
        private bool contactOffsetMatInitialized = false;
        
        public float maximumScale = -1;
        public float minimumScale = -1;

        [Header("Two Handed Interactable Properties")]
        public bool manipulationActivated = true;
        public bool usePhysics = true;
        private Vector3 lastFramePosition;
        private Vector3 throwForce;

        [Header("Input Actions")]
        public InputActionProperty depthAdjustmentAction;

        #endregion

        #region MonoBehaviour Callbacks

        protected override void Awake()
        {
            networkState = GetComponent<InteractableNetworkState>();
            selectMode = UnityEngine.XR.Interaction.Toolkit.Interactables.InteractableSelectMode.Multiple;

            interactorOffsets = new Dictionary<int, Matrix4x4>();
            contactPointOffsets = new Dictionary<int, Matrix4x4>();
            
            lastFramePosition = transform.position;
        }

        private void Update()
        {
            if (interactorOffsets.Count == 1 && manipulationActivated && isLocallyGrabbed)
            {
                EvaluateSingleContactUpdate();
            }
            else if (interactorOffsets.Count == 2 && manipulationActivated && isLocallyGrabbed)
            {
                EvaluateDualContactUpdate();
            }
            
            if (usePhysics && isLocallyGrabbed)
            {
                throwForce = (transform.position - lastFramePosition) / Time.deltaTime;
                lastFramePosition = transform.position;
            }
        }

        #endregion

        #region XRBaseInteractable Callbacks

        protected override void OnSelectEntered(SelectEnterEventArgs args)
        {
            if (!networkState.isGrabbed.Value || isLocallyGrabbed)
            {
                isLocallyGrabbed = true;
                networkState.UpdateIsGrabbed(true);
                
                base.OnSelectEntered(args);

                if (usePhysics)
                {
                    GetComponent<Rigidbody>().useGravity = false;
                    GetComponent<Rigidbody>().isKinematic = true;
                }
                
                UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInteractor interactor = (UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInteractor)args.interactorObject;
                
                Matrix4x4 interactorOffset = CalculateInteractorOffset(interactor);
                interactorOffsets.Add(interactor.GetInstanceID(), interactorOffset);
                
                Matrix4x4 contactPointOffset = CalculateContactPointOffset(interactor);
                contactPointOffsets.Add(interactor.GetInstanceID(), contactPointOffset);
            }
        }
        
        protected override void OnSelectExited(SelectExitEventArgs args)
        {
            if (isLocallyGrabbed)
            {
                firstLoop = true;
                contactDistance = -1f;
                contactOffsetMatInitialized = false;
                contactOffsetMat = Matrix4x4.identity;
                last_frame_average = Vector3.zero;
                first_pointer_offset_angle = 0f;
                second_pointer_offset_angle = 0f;
                lastframe_first_pointer_axis_index = -1;
                lastframe_second_pointer_axis_index = -1;
                
                base.OnSelectExited(args);
                UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInteractor interactor = (UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInteractor)args.interactorObject;
                
                if (interactorOffsets.ContainsKey(interactor.GetInstanceID()))
                {
                    interactorOffsets.Remove(interactor.GetInstanceID());
                }
                
                if (contactPointOffsets.ContainsKey(interactor.GetInstanceID()))
                {
                    contactPointOffsets.Remove(interactor.GetInstanceID());
                }

                if (interactorsSelecting.Count == 0)
                {
                    networkState.UpdateIsGrabbed(false);
                    isLocallyGrabbed = false;

                    if (usePhysics)
                    {
                        GetComponent<Rigidbody>().useGravity = true;
                        GetComponent<Rigidbody>().isKinematic = false;
                        
                        GetComponent<Rigidbody>().AddForce(throwForce, ForceMode.VelocityChange);
                    }
                }
                
                UpdateInteractorOffset();
            }
        }

        #endregion

        #region Update Methods

        private void EvaluateSingleContactUpdate() 
        {
            UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInteractor interactor = (UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInteractor)interactorsSelecting[0];

            Matrix4x4 interactorMat = GetWorldMatrix(interactor.attachTransform.gameObject);
            Matrix4x4 mat = interactorMat * interactorOffsets[interactor.GetInstanceID()];

            SetTransformByMatrix(this.gameObject, mat);

            if(depthAdjustmentAction != null) 
            {
                float depthInput = depthAdjustmentAction.action.ReadValue<Vector2>().y;
                if(Mathf.Abs(depthInput) > 0.1) {
                    var depthAdjustment = (depthInput*Time.deltaTime) * interactor.transform.forward;
                    transform.position += depthAdjustment;
                    interactorOffsets[interactor.GetInstanceID()] = CalculateInteractorOffset(interactor);
                    contactPointOffsets[interactor.GetInstanceID()] = CalculateContactPointOffset(interactor);
                }
            }
        }
        
        private void EvaluateDualContactUpdate() 
        {
            UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInteractor firstInteractor = (UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInteractor)interactorsSelecting[0];
            UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInteractor secondInteractor = (UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInteractor)interactorsSelecting[1];

            Matrix4x4 firstInteractorMat = GetWorldMatrix(firstInteractor.attachTransform.gameObject);
            Matrix4x4 firstContactPoint = firstInteractorMat * contactPointOffsets[firstInteractor.GetInstanceID()];

            Matrix4x4 secondInteractorMat = GetWorldMatrix(secondInteractor.attachTransform.gameObject);
            Matrix4x4 secondContactPoint = secondInteractorMat * contactPointOffsets[secondInteractor.GetInstanceID()];

            if (contactDistance == -1)
            {
                contactDistance = (firstContactPoint.GetColumn(3) - secondContactPoint.GetColumn(3)).magnitude;
            }

            Matrix4x4 mat = CalculateCenterMatrix(firstContactPoint, secondContactPoint);
            Matrix4x4 scale = CalculateUniformScaling(firstContactPoint, secondContactPoint);

            if (!contactOffsetMatInitialized)
            {
                contactOffsetMat = Matrix4x4.Inverse(mat * scale) * GetWorldMatrix(this.gameObject);
                contactOffsetMatInitialized = true;
            }

            var newObjectMat = mat * scale * contactOffsetMat;
            
            // ensure scale constraints are met
            if(minimumScale > 0 || maximumScale > 0)
            {
                float factor = -1;
                float s = newObjectMat.lossyScale.x;
                if(minimumScale > 0 && s < minimumScale)
                    factor = minimumScale / s;
                else if(maximumScale > 0 && s > maximumScale)
                    factor = maximumScale / s;
                
                if(factor > 0)
                {
                    scale = Matrix4x4.TRS(new Vector3(), Quaternion.identity, new Vector3(s*factor, s*factor, s*factor));
                    contactOffsetMat = Matrix4x4.Inverse(mat * scale) * GetWorldMatrix(this.gameObject);
                    newObjectMat = mat * scale * contactOffsetMat;
                }
            }

            SetTransformByMatrix(this.gameObject, newObjectMat);
        }
        
        private void UpdateInteractorOffset()
        {
            if (interactorsSelecting.Count == 0)
            {
                interactorOffsets = new Dictionary<int, Matrix4x4>();
                return;
            }
            foreach (var interactorInterface in interactorsSelecting)
            {
                UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInteractor interactor = (UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInteractor)interactorInterface;
                interactorOffsets[interactor.GetInstanceID()] = CalculateInteractorOffset((UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInteractor)interactorInterface);
            }
        }

        #endregion

        #region Calculation Methods

        private Matrix4x4 CalculateInteractorOffset(UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInteractor interactor)
        {
            Matrix4x4 interactorMat = Matrix4x4.TRS(interactor.attachTransform.position, interactor.attachTransform.rotation, interactor.attachTransform.lossyScale);
            Matrix4x4 interactableMat = Matrix4x4.TRS(transform.position, transform.rotation, transform.lossyScale);
            return Matrix4x4.Inverse(interactorMat) * interactableMat;
        }
        
        private Matrix4x4 CalculateContactPointOffset(UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInteractor interactor)
        {
            if (interactor is UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor)
            {
                RaycastHit contactPoint;
                UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor rayInteractor = (UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor)interactor;
                rayInteractor.TryGetCurrent3DRaycastHit(out contactPoint);

                Matrix4x4 interactorMat = Matrix4x4.TRS(interactor.attachTransform.position, interactor.attachTransform.rotation, interactor.attachTransform.lossyScale);
                Matrix4x4 hitPointMat = Matrix4x4.TRS(contactPoint.point, Quaternion.identity, Vector3.one);

                return Matrix4x4.Inverse(interactorMat) * hitPointMat;
            }
            else
            {
                return Matrix4x4.identity;
            }
        }
        
        private Matrix4x4 CalculateCenterMatrix(Matrix4x4 firstMat, Matrix4x4 secondMat)
        {
            Vector3 pos1 = firstMat.GetColumn(3);
            Vector3 pos2 = secondMat.GetColumn(3);

            Vector3 contactConnectionVec = pos2 - pos1;

            // calculate the midpoint of the connection vector
            Vector3 midpoint = pos1 + contactConnectionVec * 0.5f;

            //# rotation and translation
            Matrix4x4 transformationMatrix = CalculateTransformation(firstMat, secondMat);

            return Matrix4x4.TRS(midpoint, Quaternion.identity, Vector3.one) * transformationMatrix;
        }
        
        private Matrix4x4 CalculateUniformScaling(Matrix4x4 firstMat, Matrix4x4 secondMat)
        {
            Vector3 _pos1 = firstMat.GetColumn(3);
            Vector3 _pos2 = secondMat.GetColumn(3);

            Vector3 connectionVec = _pos2 - _pos1;

            float _rel_scale = connectionVec.magnitude / contactDistance;

            /*if (maximumScale > 0)
                _rel_scale = Mathf.Min(_rel_scale, maximumScale);
            if (minimumScale > 0)
                _rel_scale = Mathf.Max(_rel_scale, minimumScale);*/

            return Matrix4x4.TRS(new Vector3(), Quaternion.identity, new Vector3(_rel_scale, _rel_scale, _rel_scale));
        }
        
        private Matrix4x4 CalculateTransformation(Matrix4x4 firstMat, Matrix4x4 secondMat)
        {
            Vector3 pos1 = firstMat.GetColumn(3);
            Vector3 pos2 = secondMat.GetColumn(3);
            
            Vector3 contactConnectionVec = pos2 - pos1;
            //Debug.Log("Test 0 :\n" + firstMat);

            // MAT 
            // x y z t
            // x y z t
            // x y z t
            // 0 0 0 1

            // Get 
            Vector3[] firstInteractorVecs = new Vector3[3];
            firstInteractorVecs[0] = new Vector3(firstMat[0, 1], firstMat[1, 1], firstMat[2, 1]);//.normalized;
            firstInteractorVecs[1] = new Vector3(firstMat[0, 0], firstMat[1, 0], firstMat[2, 0]);//.normalized;
            firstInteractorVecs[2] = new Vector3(firstMat[0, 2], firstMat[1, 2], firstMat[2, 2]);//.normalized;

            Vector3[] secondInteractorVecs = new Vector3[3];
            secondInteractorVecs[0] = new Vector3(secondMat[0, 1], secondMat[1, 1], secondMat[2, 1]).normalized;
            secondInteractorVecs[1] = new Vector3(secondMat[0, 0], secondMat[1, 0], secondMat[2, 0]).normalized;
            secondInteractorVecs[2] = new Vector3(secondMat[0, 2], secondMat[1, 2], secondMat[2, 2]).normalized;

            Vector3[] _first_pointer_connection = new Vector3[3];
            for (int i = 0; i < firstInteractorVecs.Length; i++)
            { _first_pointer_connection[i] = Vector3.Cross(firstInteractorVecs[i].normalized, contactConnectionVec.normalized); }

            Vector3[] _second_pointer_connection = new Vector3[3];
            for (int i = 0; i < secondInteractorVecs.Length; i++)
            { _second_pointer_connection[i] = Vector3.Cross(secondInteractorVecs[i].normalized, contactConnectionVec.normalized); }

            float[] _dot_first_pointer_connection = new float[3];
            for (int i = 0; i < firstInteractorVecs.Length; i++)
            { _dot_first_pointer_connection[i] = Vector3.Dot(firstInteractorVecs[i].normalized, contactConnectionVec.normalized); }

            float[] _dot_second_pointer_connection = new float[3];
            for (int i = 0; i < secondInteractorVecs.Length; i++)
            { _dot_second_pointer_connection[i] = Vector3.Dot(secondInteractorVecs[i].normalized, contactConnectionVec.normalized); }

            // pointer up vectors in reference to the connection vector
            List<float> _abs_dot_first_pointer_connection = new List<float>();
            for (int i = 0; i < _dot_first_pointer_connection.Length; i++)
            { _abs_dot_first_pointer_connection.Add(Mathf.Abs(_dot_first_pointer_connection[i])); }

            List<float> _abs_dot_second_pointer_connection = new List<float>();
            for (int i = 0; i < _dot_second_pointer_connection.Length; i++)
            { _abs_dot_second_pointer_connection.Add(Mathf.Abs(_dot_second_pointer_connection[i])); }

            float firstMinVal = _abs_dot_first_pointer_connection.Min();
            int _first_pointer_axis_index = _abs_dot_first_pointer_connection.IndexOf(firstMinVal);

            float secondMinVal = _abs_dot_second_pointer_connection.Min();
            int _second_pointer_axis_index = _abs_dot_second_pointer_connection.IndexOf(secondMinVal);

            Vector4 _first_contact_up;
            Vector4 _second_contact_up;
            if (firstLoop)
            {
                firstInteractorUp = _first_pointer_connection[_first_pointer_axis_index];
                secondInteractorUp = _second_pointer_connection[_second_pointer_axis_index];
                firstLoop = false;
            }
            else
            {
                _first_contact_up = _first_pointer_connection[_first_pointer_axis_index];
                Quaternion first_t = Quaternion.AngleAxis(first_pointer_offset_angle, contactConnectionVec);
                if (lastframe_first_pointer_axis_index == _first_pointer_axis_index)
                {
                    firstInteractorUp = first_t * _first_contact_up;
                }
                else
                {
                    Vector3 _tmp_first_vec = new Vector3(_first_pointer_connection[lastframe_first_pointer_axis_index].x,
                                                         _first_pointer_connection[lastframe_first_pointer_axis_index].y,
                                                         _first_pointer_connection[lastframe_first_pointer_axis_index].z);
                    Vector3 _first_offset_angle_connection_vec = first_t * _tmp_first_vec;
                    //Quaternion _first_contact_up_offset_rot = MathUtilities.GetRotationBetweenVectors(_first_contact_up, _first_offset_angle_connection_vec, flip: true);
                    Quaternion _first_contact_up_offset_rot = Quaternion.Inverse(Quaternion.FromToRotation(_first_contact_up, _first_offset_angle_connection_vec));
                    _first_contact_up_offset_rot.ToAngleAxis(out float _tmp_angle, out Vector3 _tmp_axis);
                    Vector3 _first_contact_up_offset_rot_axis = _tmp_axis;

                    first_pointer_offset_angle = _tmp_angle * Mathf.Round(Vector3.Dot(_first_contact_up_offset_rot_axis.normalized, contactConnectionVec.normalized)) * -1f;
                    Quaternion new_t = Quaternion.AngleAxis(first_pointer_offset_angle, contactConnectionVec);
                    firstInteractorUp = new_t * _first_contact_up;
                }

                _second_contact_up = _second_pointer_connection[_second_pointer_axis_index];
                Quaternion second_t = Quaternion.AngleAxis(second_pointer_offset_angle, contactConnectionVec);
                if (lastframe_second_pointer_axis_index == _second_pointer_axis_index)
                {
                    
                    
                    secondInteractorUp = second_t * _second_contact_up;
                }
                else
                {
                 
                    Vector3 _tmp_second_vec = new Vector3(_second_pointer_connection[lastframe_second_pointer_axis_index].x,
                                                          _second_pointer_connection[lastframe_second_pointer_axis_index].y,
                                                          _second_pointer_connection[lastframe_second_pointer_axis_index].z);
                    Vector3 _second_offset_angle_connection_vec = second_t * _tmp_second_vec;
                    //Quaternion _second_contact_up_offset_rot =  MathUtilities.GetRotationBetweenVectors(_second_contact_up, _second_offset_angle_connection_vec, flip: true);
                    //Quaternion _second_contact_up_offset_rot = Quaternion.Inverse(Quaternion.FromToRotation(_second_contact_up, _second_offset_angle_connection_vec));
                    Quaternion _second_contact_up_offset_rot = Quaternion.Inverse(Quaternion.FromToRotation(_second_contact_up, _second_offset_angle_connection_vec));

                    _second_contact_up_offset_rot.ToAngleAxis(out float _tmp_angle, out Vector3 _tmp_axis);
                    Vector3 _second_contact_up_offset_rot_axis = _tmp_axis;

                    second_pointer_offset_angle = _tmp_angle * Mathf.Round(Vector3.Dot(_second_contact_up_offset_rot_axis.normalized, contactConnectionVec.normalized)) * -1f;
                    Quaternion new_t = Quaternion.AngleAxis(second_pointer_offset_angle, contactConnectionVec);
                    secondInteractorUp = new_t * _second_contact_up;
                }
            }
            lastframe_first_pointer_axis_index = _first_pointer_axis_index;
            lastframe_second_pointer_axis_index = _second_pointer_axis_index;

            Vector3 _average_up;
            if (Vector3.Dot(firstInteractorUp.normalized, secondInteractorUp.normalized) < 0)
            {
                _second_contact_up = secondInteractorUp * -1;
                _average_up = (firstInteractorUp.normalized + _second_contact_up.normalized); // normalization needed?
                _average_up = Vector3.Cross(_average_up.normalized, contactConnectionVec); // Check maybe if right value needs to be normalized
            }
            else
            {
                _average_up = (firstInteractorUp.normalized + secondInteractorUp.normalized);
                Vector4 weirdVec = new Vector4(_average_up.x, _average_up.y, _average_up.z, 0f).normalized;
                _average_up = new Vector3(weirdVec.x, weirdVec.y, weirdVec.z);
            }

            if (last_frame_average != Vector3.zero && Vector3.Dot(_average_up.normalized, last_frame_average.normalized) < 0)
            {
                _average_up *= -1f;
            }

            last_frame_average = _average_up;

            //Matrix4x4 lookat_average = Matrix4x4.LookAt(pos2, pos1, _average_up);
            var resultQ =  Quaternion.LookRotation(contactConnectionVec, _average_up);
            //Matrix4x4 lookat_average = MathUtilities.GetLookAtMatrix(contactConnectionVec, _average_up);
            Matrix4x4 resultMat = Matrix4x4.TRS(Vector3.zero, resultQ, Vector3.one);

            return resultMat;
        }

        #endregion

        #region Utility Methods

        private Matrix4x4 GetWorldMatrix(GameObject go)
        {
            return Matrix4x4.TRS(go.transform.position, go.transform.rotation, go.transform.lossyScale);
        } 
        
        private void SetTransformByMatrix(GameObject go, Matrix4x4 mat)
        {
            go.transform.position = mat.GetColumn(3);
            go.transform.rotation = mat.rotation;
            go.transform.localScale = Vector3.one;
            go.transform.localScale = new Vector3(mat.lossyScale.x / go.transform.lossyScale.x, mat.lossyScale.y / go.transform.lossyScale.y, mat.lossyScale.z / go.transform.lossyScale.z);
        }

        #endregion
    }
}