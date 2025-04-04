using UnityEngine;
using System.Collections.Generic;
using UnityEngine.XR;
using RenderPipeline = UnityEngine.Rendering.RenderPipelineManager;
using System;
using UnityEngine.Rendering.Universal;
using UnityEngine.Events;
using UnityEngine.Rendering;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Fragilem17.MirrorsAndPortals
{
    [ExecuteInEditMode]
    public class MirrorRenderer : MonoBehaviour
    {
        public static List<MirrorRenderer> mirrorRendererInstances;

        [Tooltip("The source material, disable and re-enable this component if you make changes to the material")]
        public List<MirrorSurface> mirrorSurfaces;

        [Tooltip("How many times can this surface reflect back onto the recursiveSurface.\nFrom 1 till the texturememory runs out.")]
        [MinAttribute(1)]
        public int recursions = 1;

        [Space(10)] // 10 pixels of spacing here.

        [Header("Other")]

        [Tooltip("The layer mask of the reflection camera")]
        public LayerMask RenderTheseLayers = -1;

        [Tooltip("Off is fast")]
        public CameraOverrideOption OpaqueTextureMode = CameraOverrideOption.Off;
        [Tooltip("Off is fast")]
        public CameraOverrideOption DepthTextureMode = CameraOverrideOption.Off;

        public bool RenderShadows = false;
        [Tooltip("As most PP is on top of your entire screen, it makes no sense to render PP in the reflection, and then again on the whole screen, so keep it off unless you know why you're turning it on.")]
        public bool RenderPostProcessing = false;


        [Space(10)] // 10 pixels of spacing here.

        [Tooltip("The custom skybox the reflection will use, when none is used, the skybox from the MainCamera is used or the skybox from lighting settings.")]
        public Material CustomSkybox;

        [Space(10)] // 10 pixels of spacing here.

        [Tooltip("When checked, the reflection will be renderer with the cameraClearFlaggOverride, otherwise the main cameras clearFlag will be used.")]
        public bool useCameraClearFlagOverride = false;
        public CameraClearFlags cameraClearFlagOverride = CameraClearFlags.Color;
        public Color32 cameraClearColorOverride;

#if UNITY_2022_3_OR_NEWER
        [Tooltip("When checked, the reflection will be calculated using 'SubmitRenderRequest' instead of 'RenderSingleCamera', this allows UI to be correctly reflected at the expense of a runtime error that appears complaining about recursive rendering not supported, this error can be safely ignored.")]
        public bool UseSubmitRenderRequest = true;
#endif


        [Space(10)]
        [Header("Quality Settings")]
        public Vector2 textureSize = Vector2.one * 128f;

        public bool useScreenScaleFactor = true;

        [Range(0.01f, 1f)]
        public float screenScaleFactor = 0.5f;

        [Tooltip("Default and ARGB32 are generally good\nDefault HDR and ARGBHalf get rid of banding but some visual artifacts in recursions. ARGB64 is too heavy with no advantages to use it.")]
        public RenderTextureFormat _renderTextureFormat = RenderTextureFormat.Default;

        public AA antiAliasing = AA.Low;

        public bool disablePixelLights = true;

        [Tooltip("In VR this should probably always be 0")]
        [MinAttribute(0)]
        public int framesNeededToUpdate = 0;

        [Tooltip("If things start flickering, turn it off! Make sure to bake your occlusions for this to take effect")]
        public bool UseOcclusionCulling = true;

        [Tooltip("Unity returns the wrong FrustumCorners (they do not match up with the actual visible space in all headsets the same) This value extends the position of the corners to stop stuff from being culled to early.")]
        public float ExtendFrustumForCulling = 0.01f;

        [Tooltip("The index of the renderer in the URP Asset you want to use to render the mirror.")]
        public int RendererIndex = 0;

        [Space(10)]
        [Header("Events")]
        public UnityEvent onStartRendering;
        public UnityEvent onFinishedRendering;

        private List<PooledTexture> _pooledTextures = new List<PooledTexture>();

        private static Dictionary<Camera, Camera> _reflectionCameras = new Dictionary<Camera, Camera>();
        private static Dictionary<Camera, UniversalAdditionalCameraData> _UacPerCameras = new Dictionary<Camera, UniversalAdditionalCameraData>();
        private static Dictionary<Camera, Skybox> _SkyboxPerCameras = new Dictionary<Camera, Skybox>();


        private static InputDevice _centerEye;
        private static float _IPD;
        private static Vector3 _leftEyePosition;
        private static Vector3 _rightEyePosition;

        private static Camera _reflectionCamera;

        private int _frameCounter = 0;

        private RenderTextureFormat _oldRenderTextureFormat = RenderTextureFormat.DefaultHDR;
        private AA _oldAntiAliasing = AA.Low;
        private int _oldTextureSize = 0;
        private bool _oldUseScreenScaleFactor = true;
        private float _oldScreenScaleFactor = 0.5f;

        private bool _isMultipass = true;
        private bool _UacAllowXRRendering = true;

        private readonly List<CameraMatrices> _cameraMatricesInOrder = new List<CameraMatrices>();

        private static MirrorRenderer _master;
        private UniversalAdditionalCameraData _uacRenderCam;
        private UniversalAdditionalCameraData _uacReflectionCam;
        private Skybox _skyboxRenderCam;
        private Skybox _skyboxReflectionCam;
        private Material _mySkyboxMaterial;
        private Camera _currentRenderCamera;
        private Rect _defaultViewRect = new Rect(0, 0, 1, 1);



        [Space(10)]
        [Header("Beta Features (read tooltip!)")]

        [Tooltip("When enabled the reflection will be rendered in the sceneview as well as the game view. If you can't turn on this checkbox, check out the warning on top of the component.")]
        public bool RenderSceneCamera = true;

        [Tooltip("When using Vulkan on Quest the second recusion would be flipped due to a bug on Unity's side, if you encounter this, check the box to flip it back.")]
        public bool FlipSecondRecursion = false;



        [Tooltip("When checked, the reflection will stop rendering but the materials will still update their position and blending")]
        public bool disableRenderingWhileStillUpdatingMaterials = false;

        [Space(10)]
        [Header("Debugging")]

        public bool showDebuggingInfo = false;


#if UNITY_EDITOR_OSX
        [Tooltip("When checked, in Unity for MacOSX, the console will be spammed with a message each time a mirror renders, this is a workarround to a Unity Bug that instantly crashes the editor. (disable at your own peril)")]
        public bool enableMacOSXTemporaryLogsToAvoidCrashingTheEditor = true;
#endif
        //[Tooltip("If you have multiple sceneViews in the editor open, you can select where the mirrors should render, and where you want to see debug info")]
        //public int SceneViewIndex = 0;



        public enum AA
        {
            None = 1,
            Low = 2,
            Medium = 4,
            High = 8
        }
        public enum DebugColors
        {
            Info,
            Warn,
            Error
        }


        private void OnEnable()
        {
            if (mirrorRendererInstances == null)
            {
                mirrorRendererInstances = new List<MirrorRenderer>();
            }
            mirrorRendererInstances.Add(this);

            RenderPipeline.beginCameraRendering += UpdateCamera;
        }

        private void LateUpdate()
        {
            if (XRSettings.enabled)
            {
                _isMultipass = XRSettings.stereoRenderingMode == XRSettings.StereoRenderingMode.MultiPass;
                if (_isMultipass && _reflectionCameras.Count > 0)
                {
                    foreach (MirrorSurface ms in mirrorSurfaces)
                    {
                        ms.UpdatePositionsInMaterial(Camera.main.transform.position, Camera.main.transform.right);
                    }
                }
            }
        }

        private void UpdateCamera(UnityEngine.Rendering.ScriptableRenderContext src, Camera renderCamera)
        {
            bool renderNeeded = false;
#if UNITY_EDITOR
            // only render the first sceneView (so we can see debug info in a second sceneView)
            //int index = Mathf.Clamp(SceneViewIndex, 0, SceneView.sceneViews.Count - 1);
            //SceneView view = SceneView.sceneViews[index] as SceneView;
            renderNeeded = renderCamera.CompareTag("MainCamera") || renderCamera.tag == "SpectatorCamera" || (renderCamera.cameraType == CameraType.SceneView && renderCamera.name.IndexOf("Preview Camera") == -1); //  && view.camera == renderCamera
#else
            renderNeeded = renderCamera.CompareTag("MainCamera") || renderCamera.tag == "SpectatorCamera";
#endif


            if (!renderNeeded)
            {
                if (_master == this) { _master = null; }
                return;
            }


            if (!enabled || !renderCamera)
            {
                if (_master == this) { _master = null; }
                return;
            }

            if (mirrorSurfaces == null || mirrorSurfaces.Count == 0)
            {
                if (_master == this) { _master = null; }
                return;
            }

            if (_frameCounter > 0)
            {
                _frameCounter--;
                return;
            }
            _frameCounter = framesNeededToUpdate;



            // check the distance
            renderNeeded = false;
            for (int i = 0; i < mirrorSurfaces.Count; i++)
            {
                MirrorSurface ms = mirrorSurfaces[i];
                if (ms)
                {
                    renderNeeded = mirrorSurfaces[i].VisibleFromCamera(renderCamera, false) || renderNeeded;
                }
            }

            if (!renderNeeded)
            {
                //Debug.Log("stop rendering exit " + name);
                if (_master == this) { _master = null; }
                return;
            }


            _currentRenderCamera = renderCamera;


            if (disableRenderingWhileStillUpdatingMaterials && _cameraMatricesInOrder != null)
            {
                for (int i = 0; i < mirrorSurfaces.Count; i++)
                {
                    MirrorSurface ms = mirrorSurfaces[i];

                    float myDistance = Vector3.Distance(renderCamera.transform.position, ms.ClosestPoint(renderCamera.transform.position));
                    ms.UpdateMaterial(Camera.StereoscopicEye.Left, null, this, 1, myDistance, renderCamera.transform.position);
                }
                if (_master == this) { _master = null; }
                return;
            }

            if (!_master) { _master = this; }


            CreateMirrorCameras(renderCamera, out _reflectionCamera);

            _reflectionCamera.CopyFrom(renderCamera);
            _reflectionCamera.rect = _defaultViewRect;
            _reflectionCamera.cullingMask = RenderTheseLayers.value;


#if UNITY_2020_3_OR_NEWER
            GetUACData(renderCamera, out _uacRenderCam);
            GetUACData(_reflectionCamera, out _uacReflectionCam);
            if (_uacRenderCam != null)
            {
                if (renderCamera.cameraType != CameraType.SceneView && _uacRenderCam.renderType == CameraRenderType.Overlay)
                {
                    Debug.LogWarning(Colorize("[MIRRORS] ", DebugColors.Warn, true) + renderCamera.name + " is an overlay camera, reflections for overlay cameras are not supported, either remove the MainCamera or SpectatorCamera tag from this cam or make this it a Base Camera. There might be a workarround for your specific situation. Send me an email!");
                    return;
                }

                _UacAllowXRRendering = _uacRenderCam.allowXRRendering;

                _uacReflectionCam.requiresColorOption = OpaqueTextureMode;
                _uacReflectionCam.requiresDepthOption = DepthTextureMode;
                _uacReflectionCam.renderPostProcessing = RenderPostProcessing;
                _uacReflectionCam.renderShadows = RenderShadows;
                _uacReflectionCam.SetRenderer(RendererIndex);
            }
            else
            {
                _UacAllowXRRendering = true;
            }
#endif



            if (CustomSkybox != null)
            {
                _mySkyboxMaterial = CustomSkybox;
            }
            else
            {
                GetCustomSkybox(renderCamera, out _skyboxRenderCam);
                if (_skyboxRenderCam != null)
                {
                    _mySkyboxMaterial = _skyboxRenderCam.material;
                }
                else
                {
                    _mySkyboxMaterial = RenderSettings.skybox;
                }
            }

            GetCustomSkybox(_reflectionCamera, out _skyboxReflectionCam);
            if (_skyboxReflectionCam != null)
            {
                _skyboxReflectionCam.material = _mySkyboxMaterial;
            }



            if (XRSettings.enabled && (_master == this) && _UacAllowXRRendering)
            {
                // get the IPD
                _centerEye = InputDevices.GetDeviceAtXRNode(XRNode.CenterEye);
                _centerEye.TryGetFeatureValue(CommonUsages.leftEyePosition, out _leftEyePosition);
                _centerEye.TryGetFeatureValue(CommonUsages.rightEyePosition, out _rightEyePosition);

                _IPD = Vector3.Distance(_leftEyePosition, _rightEyePosition) * renderCamera.transform.lossyScale.x;
            }

            if (renderCamera.cameraType != CameraType.SceneView && XRSettings.enabled && _UacAllowXRRendering)
            {
                Vector3 originalPos = renderCamera.transform.position;
                renderCamera.transform.position -= (renderCamera.transform.right * _IPD / 2f);
                _reflectionCamera.transform.SetPositionAndRotation(renderCamera.transform.position, renderCamera.transform.rotation);
                _reflectionCamera.worldToCameraMatrix = renderCamera.worldToCameraMatrix;
                renderCamera.transform.position = originalPos;

                _reflectionCamera.projectionMatrix = renderCamera.GetStereoProjectionMatrix(Camera.StereoscopicEye.Left);
            }
            else
            {
                _reflectionCamera.transform.SetPositionAndRotation(renderCamera.transform.position, renderCamera.transform.rotation);
                _reflectionCamera.worldToCameraMatrix = renderCamera.worldToCameraMatrix;
                _reflectionCamera.projectionMatrix = renderCamera.projectionMatrix;
            }


            _cameraMatricesInOrder.Clear();

            onStartRendering.Invoke();


            //Debug.Log("START SEARCH !! Mirror Cameras on Root: " + name + " : " + renderCamera.name + " : " + mirrorSurfaces.Count);
            RecusiveFindMirrorsInOrder(renderCamera, _cameraMatricesInOrder, 1, Camera.StereoscopicEye.Left);

            RenderMirrorCamera(src, _reflectionCamera, _cameraMatricesInOrder, Camera.StereoscopicEye.Left);


            if (renderCamera.cameraType != CameraType.SceneView && XRSettings.enabled && _UacAllowXRRendering)
            {
                Vector3 originalPos = renderCamera.transform.position;
                renderCamera.transform.position += (renderCamera.transform.right * _IPD / 2f);
                _reflectionCamera.transform.SetPositionAndRotation(renderCamera.transform.position, renderCamera.transform.rotation);
                _reflectionCamera.worldToCameraMatrix = renderCamera.worldToCameraMatrix;
                renderCamera.transform.position = originalPos;

                _reflectionCamera.projectionMatrix = renderCamera.GetStereoProjectionMatrix(Camera.StereoscopicEye.Right);

                _cameraMatricesInOrder.Clear();
                RecusiveFindMirrorsInOrder(renderCamera, _cameraMatricesInOrder, 1, Camera.StereoscopicEye.Right);

                RenderMirrorCamera(src, _reflectionCamera, _cameraMatricesInOrder, Camera.StereoscopicEye.Right);

                foreach (MirrorSurface ms in mirrorSurfaces)
                {
                    if (ms != null)
                    {
                        ms.TurnOffForceEye();
                    }
                }
            }
            else
            {
                if (_isMultipass)
                {
                    foreach (MirrorSurface ms in mirrorSurfaces)
                    {
                        if (ms != null)
                        {
                            ms.ForceLeftEye();
                        }
                    }
                }
            }

            onFinishedRendering.Invoke();
        }

        private void RecusiveFindMirrorsInOrder(Camera renderCamera, List<CameraMatrices> cameraMatricesInOrder, int depth, Camera.StereoscopicEye eye,
            MirrorSurface parentSurface = null,
            MirrorSurface parentsParentSurface = null,
            MirrorSurface parentsParentsParentSurface = null,
            MirrorSurface parentsParentsParentsParentSurface = null)
        {
            // look one deeper to know which deepest mirrors to turn dark
            if (depth > recursions + 1)
            {
                return;
            }

            Vector3 eyePosition = _reflectionCamera.transform.position;
            Quaternion eyeRotation = _reflectionCamera.transform.rotation;
            Matrix4x4 worldToCameraMatrix = _reflectionCamera.worldToCameraMatrix;
            Matrix4x4 projectionMatrix = _reflectionCamera.projectionMatrix;
            if (renderCamera.cameraType != CameraType.SceneView && XRSettings.enabled && _UacAllowXRRendering)
            {
                projectionMatrix = _reflectionCamera.GetStereoProjectionMatrix(eye);
            }

            //Vector3 planeIntersection = Vector3.zero;
            //Debug.DrawLine(_reflectionCamera.transform.position, _reflectionCamera.transform.position + _reflectionCamera.transform.forward, Color.red, 0, false);

            Vector3 mirrorPos;
            Vector3 mirrorNormal;
            float myDistance;

            foreach (MirrorSurface reflectionMs in mirrorSurfaces)
            {
                //Debug.Log("Should I Add? " + reflectionMs.name);
                if (reflectionMs != null && reflectionMs != parentSurface && reflectionMs.VisibleFromCamera(_reflectionCamera, true)
                    ) // && (depth != 2 || (depth == 2 && reflectionMs.VisibleInBoundsParent(parentSurface, _reflectionCamera, renderCamera)))
                {

                    mirrorPos = reflectionMs.MyForwardTransform.position;
                    mirrorNormal = reflectionMs.MyForwardTransform.forward * -1;

                    if (showDebuggingInfo && renderCamera.cameraType != CameraType.SceneView)
                    {
                        Ray ray = _reflectionCamera.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height / 2f));
                        Debug.DrawRay(ray.origin, ray.direction, Color.red, 0f, false);
                    }

                    // todo: calculate closest point in the bounds of the mirror renderer surface instead of the mirror center
                    myDistance = Vector3.Distance(eyePosition, reflectionMs.ClosestPoint(eyePosition));

                    // if we're using useRecursiveDarkening, we might be rendering deeper then the maxDistance allows
                    if (myDistance <= reflectionMs.maxRenderingDistance || reflectionMs.useRecursiveDarkening)
                    {
                        // Render reflection
                        // Reflect camera around reflection plane
                        float d = (-Vector3.Dot(mirrorNormal, mirrorPos) - reflectionMs.clippingPlaneOffset);
                        Vector4 reflectionPlane = new Vector4(mirrorNormal.x, mirrorNormal.y, mirrorNormal.z, d);

                        Matrix4x4 reflection = Matrix4x4.zero;
                        CalculateReflectionMatrix(ref reflection, reflectionPlane);

                        // no need to update the transforms, the matrix contains where to render, but it helps with debugging
                        Vector3 newEyePos = reflection.MultiplyPoint(eyePosition);
                        _reflectionCamera.transform.position = newEyePos;

                        Vector3 newForward = Vector3.Reflect(_reflectionCamera.transform.forward, mirrorNormal);
                        _reflectionCamera.transform.rotation = Quaternion.LookRotation(newForward);

                        Matrix4x4 newWorldToCameraMatrix = _reflectionCamera.worldToCameraMatrix * reflection;
                        _reflectionCamera.worldToCameraMatrix = newWorldToCameraMatrix;

                        //offset a bit so we can see it next to the red one
                        if (showDebuggingInfo && renderCamera.cameraType != CameraType.SceneView)
                        {
                            Debug.DrawRay(_reflectionCamera.transform.position + (Vector3.one * 0.01f), _reflectionCamera.transform.forward, Color.cyan, 0, false);
                        }

                        Vector4 clipPlane = CameraSpacePlane(newWorldToCameraMatrix, mirrorPos, mirrorNormal, 1.0f, reflectionMs.clippingPlaneOffset);

                        Matrix4x4 newProjectionMatrix;
                        if (renderCamera.cameraType != CameraType.SceneView && XRSettings.enabled && _UacAllowXRRendering)
                        {
                            newProjectionMatrix = _reflectionCamera.GetStereoProjectionMatrix(eye);
                        }
                        else
                        {
                            newProjectionMatrix = _reflectionCamera.projectionMatrix;
                        }

                        Matrix4x4 newCullingMatrix = newProjectionMatrix * newWorldToCameraMatrix;

                        if (UseOcclusionCulling)
                        {
                            if (!renderCamera.orthographic)
                            {
                                Camera.MonoOrStereoscopicEye mEye = Camera.MonoOrStereoscopicEye.Mono;
                                if (XRSettings.enabled && _UacAllowXRRendering)
                                {
                                    mEye = (Camera.MonoOrStereoscopicEye)eye;
                                }

                                Vector3[] occlusionBounds = reflectionMs.ShrinkPointsToBounds(_reflectionCamera, myDistance, mEye);

                                if (occlusionBounds != null)
                                {
                                    // bottomLeft / bottomRight / topLeft / topRight
                                    Vector3 bottomLeft = occlusionBounds[0];
                                    Vector3 bottomRight = occlusionBounds[1];
                                    Vector3 topLeft = occlusionBounds[2];

                                    // move backward eye posz
                                    Vector3 backwardEye = newEyePos + (_reflectionCamera.transform.forward * renderCamera.nearClipPlane);
                                    newCullingMatrix = OffAxisProjectionMatrix(renderCamera.nearClipPlane, renderCamera.farClipPlane, bottomLeft, bottomRight, topLeft, backwardEye);
                                }
                            }
                        }


                        MakeProjectionMatrixOblique(ref newProjectionMatrix, clipPlane);
                        _reflectionCamera.projectionMatrix = newProjectionMatrix;

                        if (UseOcclusionCulling && renderCamera.orthographic)
                        {
                            newCullingMatrix = newProjectionMatrix * newWorldToCameraMatrix;
                        }

                        //Debug.Log("Search Mirror Cameras seen by " + reflectionMs.name + " : " + depth);
                        RecusiveFindMirrorsInOrder(renderCamera, cameraMatricesInOrder, depth + 1, eye, reflectionMs, parentSurface, parentsParentSurface, parentsParentsParentSurface);

                        // we might have moved the reflection camera in a previous iteration
                        // reset it for the next 
                        _reflectionCamera.transform.position = eyePosition;
                        _reflectionCamera.transform.rotation = eyeRotation;
                        _reflectionCamera.worldToCameraMatrix = worldToCameraMatrix;

                        _reflectionCamera.projectionMatrix = projectionMatrix;

                        if (showDebuggingInfo && renderCamera.cameraType != CameraType.SceneView)
                        {
                            MirrorRenderer.DebugWireSphere(newEyePos, Color.red, 0.1f, 0, false);
                        }


                        //Debug.Log("Found Mirror: " + reflectionMs.name + " depth: " + depth + " parent: " + parentSurface?.name);
                        //Debug.Log(newProjectionMatrix);
                        cameraMatricesInOrder.Add(new CameraMatrices(renderCamera, newProjectionMatrix, newWorldToCameraMatrix, newCullingMatrix, reflectionMs, depth % 2 != 0, newEyePos, newForward, depth, myDistance, parentSurface, parentsParentSurface, parentsParentsParentSurface, parentsParentsParentsParentSurface));
                    }
                    else
                    {
                        //Debug.Log("to far: " + reflectionMs.name + " : " + depth + " : " + myDistance + " : " + reflectionMs.maxRenderingDistance);
                        cameraMatricesInOrder.Add(new CameraMatrices(renderCamera, Matrix4x4.identity, Matrix4x4.identity, Matrix4x4.identity, reflectionMs, true, Vector3.zero, Vector3.zero, recursions + 1, myDistance, parentSurface, parentsParentSurface, parentsParentsParentSurface, parentsParentsParentsParentSurface));
                    }
                }
            }

            //Debug.Log("stop search: " + depth + " : " + parentSurface?.name);
        }

        private void RenderMirrorCamera(UnityEngine.Rendering.ScriptableRenderContext src, Camera reflectionCamera, List<CameraMatrices> cameraMatricesInOrder, Camera.StereoscopicEye eye)
        {
            //Debug.Log("RenderMirrorCamera");

            // Optionally disable pixel lights for reflection
            int oldPixelLightCount = QualitySettings.pixelLightCount;
            if (disablePixelLights)
            {
                QualitySettings.pixelLightCount = 0;
            }

            PooledTexture _ptex = null;
            // position and render the camera
            CameraMatrices matrices = null;

            if (useCameraClearFlagOverride)
            {
                reflectionCamera.clearFlags = cameraClearFlagOverride;
                reflectionCamera.backgroundColor = cameraClearColorOverride;
            }

            for (int i = 0; i < cameraMatricesInOrder.Count; i++)
            {
                matrices = cameraMatricesInOrder[i];

                if (matrices.depth >= recursions + 1)
                {
                    //Debug.Log(" render surface lite: " + matrices.mirrorSurface.name + " de: " + matrices.depth + " pa: " + matrices.parentMirrorSurface?.name + " di: " + matrices.distance);
                    // make it completely blended, no need to render these
                    matrices.mirrorSurface.UpdateMaterial(eye, null, this, matrices.depth, Mathf.Infinity, _currentRenderCamera.transform.position);
                }
                else
                {
                    GetFreeTexture(out _ptex, eye);
                    _ptex.matrices = matrices;

                    //Debug.Log("DO render depth: " + matrices.depth + " render op: " + matrices.mirrorSurface.name + " VOOR parent: " + matrices.parentMirrorSurface?.name + " using tex: " + _ptex.texture.name + " parentsParent: "+ matrices.parentsParentMirrorSurface);

                    _ptex.liteLock = true;

                    if (matrices.parentMirrorSurface == null)
                    {
                        _pooledTextures.ForEach(pTex => {
                            pTex.liteLock = false;
                        });
                        _ptex.fullLock = true;
                    }

                    matrices.mirrorSurface.UpdateMaterial(eye, _ptex.texture, this, matrices.depth, matrices.distance, _currentRenderCamera.transform.position);

                    reflectionCamera.targetTexture = _ptex.texture;

                    reflectionCamera.transform.position = matrices.camPos;
                    reflectionCamera.worldToCameraMatrix = matrices.worldToCameraMatrix;
                    reflectionCamera.projectionMatrix = matrices.projectionMatrix;

                    if (matrices.even)
                    {
                        GL.invertCulling = true;
                    }

                    //Debug.Log(" render surface heav: " + matrices.mirrorSurface.name + " de : " + matrices.depth + " pa: " + matrices.parentMirrorSurface?.name + " di: " + matrices.distance + " tex: " + _ptex.texture.name);
                    reflectionCamera.useOcclusionCulling = UseOcclusionCulling;
                    reflectionCamera.cullingMatrix = matrices.cullingMatrix;

#if UNITY_EDITOR_OSX
                if(enableMacOSXTemporaryLogsToAvoidCrashingTheEditor){
                    Debug.Log(" a bug in Unity for MacOSX causes the editor to crash if this message is not here. Terribly sorry about this");
                }
#endif
                    //UniversalRenderPipeline.SingleCameraRequest req = new UniversalRenderPipeline.SingleCameraRequest();
                    //UniversalRenderPipeline.SubmitRenderRequest(reflectionCamera, src); // with UniversalRenderer.SingleCameraRequest as RequestData type

                    float renderScale = UniversalRenderPipeline.asset.renderScale;
                    UniversalRenderPipeline.asset.renderScale = 1f;

#if UNITY_2022_3_OR_NEWER

                    if (UseSubmitRenderRequest && _currentRenderCamera.cameraType != CameraType.SceneView)
                    {
                        UniversalRenderPipeline.SingleCameraRequest req = new UniversalRenderPipeline.SingleCameraRequest();
                        req.destination = reflectionCamera.targetTexture;
                        reflectionCamera.SubmitRenderRequest(req);
                    }
                    else
                    {
                        if (_currentRenderCamera.cameraType != CameraType.SceneView || (_currentRenderCamera.cameraType == CameraType.SceneView && RenderSceneCamera))
                        {
#pragma warning disable CS0618 // Type or member is obsolete
                            UniversalRenderPipeline.RenderSingleCamera(src, reflectionCamera);
#pragma warning restore CS0618 // Type or member is obsolete
                        }
                    }

#else
                    if (_currentRenderCamera.cameraType != CameraType.SceneView || (_currentRenderCamera.cameraType == CameraType.SceneView && RenderSceneCamera))
                    {
                        UniversalRenderPipeline.RenderSingleCamera(src, reflectionCamera);
                    }
#endif

                    UniversalRenderPipeline.asset.renderScale = renderScale;

                    if (matrices.even)
                    {
                        GL.invertCulling = false;
                    }

                    // reset the material to the one with the lowest depth
                    List<CameraMatrices> li = cameraMatricesInOrder.FindAll(x => x.depth == matrices.depth
                        && x.depth == matrices.depth
                        && x.parentMirrorSurface == matrices.parentMirrorSurface
                        && x.parentsParentMirrorSurface == matrices.parentsParentMirrorSurface
                        && x.parentsParentsParentMirrorSurface == matrices.parentsParentsParentMirrorSurface
                        && x.parentsParentsParentsParentMirrorSurface == matrices.parentsParentsParentsParentMirrorSurface);
                    //Debug.Log("how many?" + li.Count);

                    if (li.Count > 0)
                    {
                        foreach (CameraMatrices cm in li)
                        {
                            if (cm != matrices)
                            {
                                PooledTexture p = _pooledTextures.Find(ptex => ptex.matrices.mirrorSurface == cm.mirrorSurface
                                    && ptex.matrices.parentMirrorSurface == cm.parentMirrorSurface
                                    && ptex.matrices.parentsParentMirrorSurface == cm.parentsParentMirrorSurface
                                    && ptex.matrices.parentsParentsParentMirrorSurface == cm.parentsParentsParentMirrorSurface
                                    && ptex.matrices.parentsParentsParentsParentMirrorSurface == cm.parentsParentsParentsParentMirrorSurface
                                    && ptex.matrices.depth == cm.depth && ptex.eye == eye);
                                if (p != null)
                                {
                                    cm.mirrorSurface.UpdateMaterial(eye, p.texture, this, cm.depth, cm.distance, _currentRenderCamera.transform.position);
                                }
                            }
                        }
                    }


                    // turn on occlusionCulling even though there is an issue with the cullingMatrix for mirrors
                    // the RecusiveFindMirrorsInOrder will use VisibleFromCamera and that can early exit 
                    reflectionCamera.useOcclusionCulling = true;
                }
            }


            // break the textureLocks
            _pooledTextures.ForEach(pTex => {
                pTex.liteLock = false;
                pTex.fullLock = false;
            });

            // Restore pixel light count
            if (disablePixelLights)
            {
                QualitySettings.pixelLightCount = oldPixelLightCount;
            }
        }

        private void GetFreeTexture(out PooledTexture textureOut, Camera.StereoscopicEye eye = Camera.StereoscopicEye.Left)
        {
            PooledTexture tex = _pooledTextures.Find(tex2 => !tex2.fullLock && !tex2.liteLock && tex2.eye == eye);
            if (tex == null)
            {
                tex = new PooledTexture();
                tex.eye = eye;
                _pooledTextures.Add(tex);

                // create the texture
                //Debug.Log("creating new pooledTexture: " + _pooledTextures.Count);

                if (useScreenScaleFactor && screenScaleFactor > 0)
                {
                    float scale = screenScaleFactor; // * (1f / depth);
                    textureSize = new Vector2(Screen.width * scale, Screen.height * scale);
                }

                RenderTextureDescriptor desc = new RenderTextureDescriptor((int)textureSize.x, (int)textureSize.y, _renderTextureFormat, 1);
                desc.useMipMap = false;
                //desc.autoGenerateMips = true;

                desc.msaaSamples = (int)antiAliasing;

                tex.texture = RenderTexture.GetTemporary(desc);
                tex.texture.wrapMode = TextureWrapMode.Mirror;
                tex.texture.name = "_Tex" + gameObject.name + "_" + _pooledTextures.Count;
                tex.texture.hideFlags = HideFlags.DontSave;
                //tex.texture.stencilFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.A2B10G10R10_UNormPack32;
                //tex.texture.depthStencilFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.A2B10G10R10_UNormPack32;
            }

            textureOut = tex;
        }

        private void Update()
        {
            if (_oldTextureSize != ((int)textureSize.x + (int)textureSize.y)
                || _oldScreenScaleFactor != screenScaleFactor
                || _oldAntiAliasing != antiAliasing
                || _oldRenderTextureFormat != _renderTextureFormat
                || _oldUseScreenScaleFactor != useScreenScaleFactor)
            {
                _oldUseScreenScaleFactor = useScreenScaleFactor;
                _oldAntiAliasing = antiAliasing;
                _oldRenderTextureFormat = _renderTextureFormat;
                _oldScreenScaleFactor = screenScaleFactor;
                _oldTextureSize = ((int)textureSize.x + (int)textureSize.y);

                foreach (PooledTexture tex in _pooledTextures)
                {
                    DestroyImmediate(((RenderTexture)tex.texture));
                }
                _pooledTextures.Clear();
            }

            if (recursions > 8)
            {
                recursions = 8;
            }

        }

        private void GetUACData(Camera renderCamera, out UniversalAdditionalCameraData uac)
        {
            UniversalAdditionalCameraData uacOut;

            if (!_UacPerCameras.TryGetValue(renderCamera, out uacOut))
            {
                uacOut = renderCamera.GetComponent<UniversalAdditionalCameraData>();
                _UacPerCameras.Add(renderCamera, uacOut);
            }
            uac = uacOut;
        }

        private void GetCustomSkybox(Camera renderCamera, out Skybox skybox)
        {
            Skybox skyboxOut;

            if (!_SkyboxPerCameras.TryGetValue(renderCamera, out skyboxOut))
            {
                //Debug.Log("Slow!!", gameObject);
                skyboxOut = renderCamera.GetComponent<Skybox>();
                _SkyboxPerCameras.Add(renderCamera, skyboxOut);
            }
            skybox = skyboxOut;
        }


        private void CreateMirrorCameras(Camera renderCamera, out Camera reflectionCamera)
        {
            reflectionCamera = null;

            // Camera for reflection
            Camera reflectionCam;
            _reflectionCameras.TryGetValue(renderCamera, out reflectionCam);

            if (reflectionCam == null)
            {
                //Debug.Log("new reflection camera for " + renderCamera.name);
                GameObject go = new GameObject("Reflection Camera for " + renderCamera.name + " (autoGen, not saved in scene)", typeof(Camera), typeof(Skybox));
                reflectionCamera = go.GetComponent<Camera>();
                reflectionCamera.useOcclusionCulling = true;
                reflectionCamera.enabled = false;
                reflectionCamera.transform.position = transform.position;
                reflectionCamera.transform.rotation = transform.rotation;
                //reflectionCamera.allowHDR = false;
                //reflectionCamera.gameObject.AddComponent<FlareLayer>();

                Material mySkyboxMaterial = CustomSkybox;
                if (!mySkyboxMaterial)
                {
                    Skybox mySky = renderCamera.GetComponent<Skybox>();
                    if (mySky)
                    {
                        mySkyboxMaterial = mySky.material;
                    }
                    else
                    {
                        mySkyboxMaterial = RenderSettings.skybox;
                    }
                }
                go.GetComponent<Skybox>().material = mySkyboxMaterial;

                if (useCameraClearFlagOverride)
                {
                    reflectionCamera.clearFlags = cameraClearFlagOverride;
                    reflectionCamera.backgroundColor = cameraClearColorOverride;
                }


                //reflectionCamera.clearFlags = CameraClearFlags.Nothing;
                //reflectionCamera.depthTextureMode = DepthTextureMode.None;

                GetUACData(renderCamera, out _uacRenderCam);
                //_uacRenderCam = renderCamera.GetComponent<UniversalAdditionalCameraData>();
                if (_uacRenderCam != null)
                {
                    _uacReflectionCam = reflectionCamera.gameObject.AddComponent<UniversalAdditionalCameraData>();
                    _uacReflectionCam.requiresColorOption = OpaqueTextureMode;
                    _uacReflectionCam.requiresDepthOption = DepthTextureMode;
                    _uacReflectionCam.renderPostProcessing = RenderPostProcessing;
                    _uacReflectionCam.allowXRRendering = false;
                    _uacReflectionCam.renderType = CameraRenderType.Base;
                    _uacReflectionCam.renderShadows = RenderShadows;

#if UNITY_2020_3_OR_NEWER
                    _uacReflectionCam.allowXRRendering = _uacRenderCam.allowXRRendering;
#endif
                }

                //go.hideFlags = HideFlags.DontSave;
                go.hideFlags = HideFlags.HideAndDontSave;


                if (_reflectionCameras.ContainsKey(renderCamera))
                {
                    _reflectionCameras[renderCamera] = reflectionCamera;
                }
                else
                {
                    _reflectionCameras.Add(renderCamera, reflectionCamera);
                }
            }
            else
            {
                reflectionCamera = reflectionCam;
            }
        }


        private void OnDestroy()
        {
            if (mirrorRendererInstances != null)
            {
                mirrorRendererInstances.Remove(this);
            }
            OnDisable();
        }

        // Cleanup all the objects we possibly have created
        void OnDisable()
        {
            //Debug.Log("OnDisable");
            RenderPipeline.beginCameraRendering -= UpdateCamera;

            if (_master == this)
            {
                _master = null;
            }

            if (!Application.isPlaying)
            {
                foreach (var kvp in _reflectionCameras)
                {
                    DestroyImmediate(((Camera)kvp.Value).gameObject);
                }
                foreach (var pTex in _pooledTextures)
                {
                    DestroyImmediate(((RenderTexture)pTex.texture));
                }
            }
            else
            {
                // destroy all reflectionCameras, if needed they'll be recreated
                foreach (var kvp in _reflectionCameras)
                {
                    Destroy(((Camera)kvp.Value).gameObject);
                }
                foreach (var pTex in _pooledTextures)
                {
                    Destroy(((RenderTexture)pTex.texture));
                }
            }

            _reflectionCameras.Clear();
            _pooledTextures.Clear();

            _UacPerCameras.Clear();
            _uacReflectionCam = null;
            _uacRenderCam = null;

            _SkyboxPerCameras.Clear();
            _skyboxReflectionCam = null;
            _skyboxRenderCam = null;
        }


#if UNITY_EDITOR
        public void SurfaceGotDeselectedInEditor()
        {
            // notify the other surfaces as the material might have changed, to update their materials
            if (mirrorSurfaces == null)
            {
                return;
            }

            foreach (MirrorSurface ms in mirrorSurfaces)
            {
                if (ms)
                {
                    ms.RefreshMaterialInEditor();
                }
            }
        }
#endif


        // Extended sign: returns -1, 0 or 1 based on sign of a
        private static float sgn(float a)
        {
            if (a > 0.0f) return 1.0f;
            if (a < 0.0f) return -1.0f;
            return 0.0f;
        }

        // Given position/normal of the plane, calculates plane in camera space.
        private Vector4 CameraSpacePlane(Matrix4x4 worldToCameraMatrix, Vector3 pos, Vector3 normal, float sideSign, float clippingPlaneOffset)
        {
            Vector3 offsetPos = pos + normal * clippingPlaneOffset;
            Vector3 cpos = worldToCameraMatrix.MultiplyPoint(offsetPos);
            Vector3 cnormal = worldToCameraMatrix.MultiplyVector(normal).normalized * sideSign;
            return new Vector4(cnormal.x, cnormal.y, cnormal.z, -Vector3.Dot(cpos, cnormal));
        }

        // Calculates reflection matrix around the given plane
        private static void CalculateReflectionMatrix(ref Matrix4x4 reflectionMat, Vector4 plane)
        {
            reflectionMat.m00 = (1F - 2F * plane[0] * plane[0]);
            reflectionMat.m01 = (-2F * plane[0] * plane[1]);
            reflectionMat.m02 = (-2F * plane[0] * plane[2]);
            reflectionMat.m03 = (-2F * plane[3] * plane[0]);

            reflectionMat.m10 = (-2F * plane[1] * plane[0]);
            reflectionMat.m11 = (1F - 2F * plane[1] * plane[1]);
            reflectionMat.m12 = (-2F * plane[1] * plane[2]);
            reflectionMat.m13 = (-2F * plane[3] * plane[1]);

            reflectionMat.m20 = (-2F * plane[2] * plane[0]);
            reflectionMat.m21 = (-2F * plane[2] * plane[1]);
            reflectionMat.m22 = (1F - 2F * plane[2] * plane[2]);
            reflectionMat.m23 = (-2F * plane[3] * plane[2]);

            reflectionMat.m30 = 0F;
            reflectionMat.m31 = 0F;
            reflectionMat.m32 = 0F;
            reflectionMat.m33 = 1F;
        }

        // taken from http://www.terathon.com/code/oblique.html
        private static void MakeProjectionMatrixOblique(ref Matrix4x4 matrix, Vector4 clipPlane)
        {
            /*
            Vector4 q;

            // Calculate the clip-space corner point opposite the clipping plane
            // as (sgn(clipPlane.x), sgn(clipPlane.y), 1, 1) and
            // transform it into camera space by multiplying it
            // by the inverse of the projection matrix

            q.x = (sgn(clipPlane.x) + matrix[8]) / matrix[0];
            q.y = (sgn(clipPlane.y) + matrix[9]) / matrix[5];
            q.z = -1.0F;
            q.w = (1.0F + matrix[10]) / matrix[14];

            // Calculate the scaled plane vector
            Vector4 c = clipPlane * (2.0F / Vector3.Dot(clipPlane, q));

            // Replace the third row of the projection matrix
            matrix[2] = c.x;
            matrix[6] = c.y;
            matrix[10] = c.z + 1.0F;
            matrix[14] = c.w;
            */

            Vector4 q = matrix.inverse * new Vector4(
                sgn(clipPlane.x),
                sgn(clipPlane.y),
                1.0f,
                1.0f
            );
            Vector4 c = clipPlane * (2.0F / (Vector4.Dot(clipPlane, q)));
            // third row = clip plane - fourth row
            matrix[2] = c.x - matrix[3];
            matrix[6] = c.y - matrix[7];
            matrix[10] = c.z - matrix[11];
            matrix[14] = c.w - matrix[15];
        }

        private static Matrix4x4 OffAxisProjectionMatrix(float near, float far, Vector3 pa, Vector3 pb, Vector3 pc, Vector3 pe)
        {
            Vector3 va; // from pe to pa
            Vector3 vb; // from pe to pb
            Vector3 vc; // from pe to pc
            Vector3 vr; // right axis of screen
            Vector3 vu; // up axis of screen
            Vector3 vn; // normal vector of screen

            float l; // distance to left screen edge
            float r; // distance to right screen edge
            float b; // distance to bottom screen edge
            float t; // distance to top screen edge
            float d; // distance from eye to screen 

            vr = pb - pa;
            vu = pc - pa;
            va = pa - pe;
            vb = pb - pe;
            vc = pc - pe;

            // are we looking at the backface of the plane object?
            if (Vector3.Dot(-Vector3.Cross(va, vc), vb) < 0.0)
            {
                // mirror points along the z axis (most users 
                // probably expect the x axis to stay fixed)
                vu = -vu;
                pa = pc;
                pb = pa + vr;
                pc = pa + vu;
                va = pa - pe;
                vb = pb - pe;
                vc = pc - pe;
            }

            vr.Normalize();
            vu.Normalize();
            vn = -Vector3.Cross(vr, vu);
            // we need the minus sign because Unity 
            // uses a left-handed coordinate system
            vn.Normalize();

            d = -Vector3.Dot(va, vn);

            // Set near clip plane
            near = d; // + _clippingDistance;

            l = Vector3.Dot(vr, va) * near / d;
            r = Vector3.Dot(vr, vb) * near / d;
            b = Vector3.Dot(vu, va) * near / d;
            t = Vector3.Dot(vu, vc) * near / d;

            Matrix4x4 p = new Matrix4x4(); // projection matrix 
            p[0, 0] = 2.0f * near / (r - l);
            p[0, 1] = 0.0f;
            p[0, 2] = (r + l) / (r - l);
            p[0, 3] = 0.0f;

            p[1, 0] = 0.0f;
            p[1, 1] = 2.0f * near / (t - b);
            p[1, 2] = (t + b) / (t - b);
            p[1, 3] = 0.0f;

            p[2, 0] = 0.0f;
            p[2, 1] = 0.0f;
            p[2, 2] = (far + near) / (near - far);
            p[2, 3] = 2.0f * far * near / (near - far);

            p[3, 0] = 0.0f;
            p[3, 1] = 0.0f;
            p[3, 2] = -1.0f;
            p[3, 3] = 0.0f;

            Matrix4x4 rm = new Matrix4x4(); // rotation matrix;
            rm[0, 0] = vr.x;
            rm[0, 1] = vr.y;
            rm[0, 2] = vr.z;
            rm[0, 3] = 0.0f;

            rm[1, 0] = vu.x;
            rm[1, 1] = vu.y;
            rm[1, 2] = vu.z;
            rm[1, 3] = 0.0f;

            rm[2, 0] = vn.x;
            rm[2, 1] = vn.y;
            rm[2, 2] = vn.z;
            rm[2, 3] = 0.0f;

            rm[3, 0] = 0.0f;
            rm[3, 1] = 0.0f;
            rm[3, 2] = 0.0f;
            rm[3, 3] = 1.0f;

            Matrix4x4 tm = new Matrix4x4(); // translation matrix;
            tm[0, 0] = 1.0f;
            tm[0, 1] = 0.0f;
            tm[0, 2] = 0.0f;
            tm[0, 3] = -pe.x;

            tm[1, 0] = 0.0f;
            tm[1, 1] = 1.0f;
            tm[1, 2] = 0.0f;
            tm[1, 3] = -pe.y;

            tm[2, 0] = 0.0f;
            tm[2, 1] = 0.0f;
            tm[2, 2] = 1.0f;
            tm[2, 3] = -pe.z;

            tm[3, 0] = 0.0f;
            tm[3, 1] = 0.0f;
            tm[3, 2] = 0.0f;
            tm[3, 3] = 1.0f;

            Matrix4x4 worldToCameraMatrix = rm * tm;
            return p * worldToCameraMatrix;
        }

        public static void DebugWireSphere(Vector3 position, Color color, float radius = 1.0f, float duration = 0, bool depthTest = true)
        {
            float angle = 10.0f;

            Vector3 x = new Vector3(position.x, position.y + radius * Mathf.Sin(0), position.z + radius * Mathf.Cos(0));
            Vector3 y = new Vector3(position.x + radius * Mathf.Cos(0), position.y, position.z + radius * Mathf.Sin(0));
            Vector3 z = new Vector3(position.x + radius * Mathf.Cos(0), position.y + radius * Mathf.Sin(0), position.z);

            Vector3 new_x;
            Vector3 new_y;
            Vector3 new_z;

            for (int i = 1; i < 37; i++)
            {

                new_x = new Vector3(position.x, position.y + radius * Mathf.Sin(angle * i * Mathf.Deg2Rad), position.z + radius * Mathf.Cos(angle * i * Mathf.Deg2Rad));
                new_y = new Vector3(position.x + radius * Mathf.Cos(angle * i * Mathf.Deg2Rad), position.y, position.z + radius * Mathf.Sin(angle * i * Mathf.Deg2Rad));
                new_z = new Vector3(position.x + radius * Mathf.Cos(angle * i * Mathf.Deg2Rad), position.y + radius * Mathf.Sin(angle * i * Mathf.Deg2Rad), position.z);

                Debug.DrawLine(x, new_x, color, duration, depthTest);
                Debug.DrawLine(y, new_y, color, duration, depthTest);
                Debug.DrawLine(z, new_z, color, duration, depthTest);

                x = new_x;
                y = new_y;
                z = new_z;
            }
        }

        public static string Colorize(string text, DebugColors color, bool bold = false)
        {
            string c = "00BC0E";
            if (color == DebugColors.Error)
            {
                c = "BE0000";
            }
            else if (color == DebugColors.Warn)
            {
                c = "FFB900";
            }

            return "<color=#" + c + ">" + (bold ? "<b>" : "") + text + (bold ? "</b>" : "") + "</color>";
        }
    }



    public class PooledTexture
    {
        public bool liteLock;
        public bool fullLock;
        public CameraMatrices matrices;
        //public MirrorSurface mirrorSurface;
        //public MirrorSurface parentSurface;
        //public MirrorSurface parentsParentSurface;
        public RenderTexture texture;
        //public int depth;
        public Camera.StereoscopicEye eye = Camera.StereoscopicEye.Left;

        public PooledTexture()
        {
        }
    }

    public class CameraMatrices
    {
        public Camera sourceCam;
        public Matrix4x4 projectionMatrix;
        public Matrix4x4 worldToCameraMatrix;
        public Matrix4x4 cullingMatrix;
        public MirrorSurface mirrorSurface;
        public MirrorSurface parentMirrorSurface;
        public MirrorSurface parentsParentMirrorSurface;
        public MirrorSurface parentsParentsParentMirrorSurface;
        public MirrorSurface parentsParentsParentsParentMirrorSurface;
        public bool even;
        public Vector3 camPos;
        public Vector3 camForward;
        public int depth;
        public float distance;

        public CameraMatrices(Camera sourceCam_, Matrix4x4 projectionMatrix, Matrix4x4 worldToCameraMatrix, Matrix4x4 cullingMatrix, MirrorSurface mirrorSurface, bool even,
            Vector3 camPos, Vector3 camForward, int depth, float distance,
            MirrorSurface parentMirrorSurface,
            MirrorSurface parentsParentMirrorSurface,
            MirrorSurface parentsParentsParentMirrorSurface,
            MirrorSurface parentsParentsParentsParentMirrorSurface)
        {
            this.sourceCam = sourceCam_;
            this.projectionMatrix = projectionMatrix;
            this.worldToCameraMatrix = worldToCameraMatrix;
            this.mirrorSurface = mirrorSurface;
            this.even = even;
            this.camPos = camPos;
            this.camForward = camForward;
            this.depth = depth;
            this.distance = distance;
            this.parentMirrorSurface = parentMirrorSurface;
            this.parentsParentMirrorSurface = parentsParentMirrorSurface;
            this.parentsParentsParentMirrorSurface = parentsParentsParentMirrorSurface;
            this.parentsParentsParentsParentMirrorSurface = parentsParentsParentsParentMirrorSurface;
            this.cullingMatrix = cullingMatrix;
        }
    }
}