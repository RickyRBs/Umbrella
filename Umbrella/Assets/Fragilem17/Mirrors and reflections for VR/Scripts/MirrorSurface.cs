using UnityEngine;
using UnityEngine.XR;
using RenderPipeline = UnityEngine.Rendering.RenderPipelineManager;
using System;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Fragilem17.MirrorsAndPortals
{

    [ExecuteInEditMode]
    public class MirrorSurface : MonoBehaviour
    {

        [Tooltip("The source material, disable and re-enable this component if you make changes to the material")]
        public Material Material;

        [Tooltip("When the camera is further from this distance, the surface stops updating it's texture.")]
        [MinAttribute(0)]
        public float maxRenderingDistance = 5f;

        [Tooltip("The % of maxRenderingDistance over which the mirror starts to darkens.")]
        [Range(0, 1)]
        public float fadeDistance = 0.5f;

        [Tooltip("How much reflection is allowed to blend in the color when you're closer than maxRenderingDistance-fadeDistance.")]
        [Range(0, 1)]
        public float maxBlend = 1f;

        public Color FadeColor = Color.black;


        [Space(10)]

        [Tooltip("When enabled each recursion can be used to darken the reflection, disabled the fadeDistance will be used to darken.")]
        public bool useRecursiveDarkening = true;

        public AnimationCurve recursiveDarkeningCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("Other")]

        public float clippingPlaneOffset = 0.0f;


        public MeshRenderer MyMeshRenderer;

        [Tooltip("when empty, this transform will be used, the Z axis is used as the viewDirection of the Mirror. Add a dedicated transform to specify the forward of the mirror.")]
        public Transform MyForwardTransform;


        [Tooltip("The mirror will make a material instance at runtime, if you want to change the look of the material at runtime you need this reference")]
        public Material MyMaterialInstance { get => _myMaterialInstance; }

        private Material _material;
        private Material _myMaterialInstance;
        private MirrorRenderer _myRenderer;
        private MeshFilter _myMeshFilter;
        private Color _oldFadeColor = Color.black;
        private Material _oldMaterial;
        private bool _wasToFar = false;

        private bool _isSelectedInEditor = false;

        // the worldspace bounds of this portalSurface (precompute?)
        private Vector3[] _portalBounds;
        private Vector3[] _frustumCorners = new Vector3[4];
        private Vector3[] _fustrumBoundsOnPlane = new Vector3[4];
        private Vector3[] _shrinkToVectorArray = new Vector3[4];
        private Plane _plane;


        [Tooltip("These surfaces will not receive a different reflection texture, use it on surfaces with different materials in the same plane as this one to save on rendering")]
        public List<MirrorSurface> ChildSurfaces;

        // a bug in unity would set a gameObject to be MyMeshRenderer.isVisible = false even though it's already enabled in the scene and viewed by a camera
        // the method VisibleFromCamera should ignore MyMeshRenderer.isVisible the first 2 frames cause it's possibly not correct.
        private int _firstFramesOnEnable = 2;

        private void OnEnable()
        {
#if UNITY_EDITOR
            // our surfaces can't be batched! we rely on the mesh to get the real worlds bounds
            // also, you want to hide this mesh as soon as it's out of view.
            // this can't happen when batched with a lot of other meshes.. forcing the recursion to keep rendering.
            var flags = GameObjectUtility.GetStaticEditorFlags(gameObject);
            if ((flags & StaticEditorFlags.BatchingStatic) != 0)
            {
                Debug.LogWarning(MirrorRenderer.Colorize("[MIRRORS] ", MirrorRenderer.DebugColors.Warn, true) + " our MirrorSurface (" + gameObject.name + ") should not be batched! We rely on the individual mesh to get the actual world bounds and you'll want to hide this mesh as soon as it's out of view. This can't happen when batched with a lot of other meshes.. forcing the recursion to keep rendering longer then it should.", gameObject);

                flags &= ~StaticEditorFlags.BatchingStatic;
                GameObjectUtility.SetStaticEditorFlags(gameObject, flags);
            }

#endif

            if (MyForwardTransform == null)
            {
                MyForwardTransform = GetComponent<Transform>();
            }
            if (MyMeshRenderer == null)
            {
                MyMeshRenderer = GetComponent<MeshRenderer>();
            }
            if (_myMeshFilter == null)
            {
                _myMeshFilter = MyMeshRenderer.gameObject.GetComponent<MeshFilter>();
            }

            _wasToFar = false;

            if (!Material && MyMeshRenderer)
            {
                Material = MyMeshRenderer.sharedMaterial;
            }


            if (MyMeshRenderer && Material)
            {
                _oldMaterial = Material;

                if (_isSelectedInEditor)
                {
                    // make sure we're editing the source materials, not the instance
                    Material.SetColor("_FadeColor", FadeColor);
                    MyMeshRenderer.sharedMaterial = Material;
                    _material = Material;
                }
                else
                {
                    _material = new Material(Material);
                    _myMaterialInstance = _material;
                    _material.name += " (for " + gameObject.name + ")";
                    _material.SetColor("_FadeColor", FadeColor);
                    MyMeshRenderer.material = _material;
                }
            }



#if UNITY_EDITOR
            Selection.selectionChanged -= OnSelectionChange;
            Selection.selectionChanged += OnSelectionChange;
#endif

            _firstFramesOnEnable = 2;
        }
        private void OnDisable()
        {
#if UNITY_EDITOR
            Selection.selectionChanged -= OnSelectionChange;
#endif
            if (_material != Material)
            {
                DestroyImmediate(_material, true);
            }
            if (MyMeshRenderer)
            {
                MyMeshRenderer.material = Material;
            }
        }

#if UNITY_EDITOR
        private void OnDestroy()
        {
            _isSelectedInEditor = false;
            Selection.selectionChanged -= OnSelectionChange;
        }
#endif

        internal Bounds GetBounds()
        {
            return MyMeshRenderer.bounds;
        }

        public void UpdatePositionsInMaterial(Vector3 position, Vector3 direction)
        {
            if (_material.HasProperty("_WorldPos"))
            {
                _material.SetVector("_WorldPos", position);
                _material.SetVector("_WorldDir", direction);
            }
        }

        /*
        public bool VisibleInBoundsParent(MirrorSurface parentSurface, Camera reflectionCamera, Camera renderCamera) 
        {
			if (parentSurface == null)
			{
                return true;
			}

            float scale = 1;
            if (_myRenderer)
			{
                scale = _myRenderer.screenScaleFactor;
            }

            Rect myRect = RendererBoundsInScreenSpace(reflectionCamera, _renderer);
            Rect parentRect = RendererBoundsInScreenSpace(reflectionCamera, parentSurface._renderer);

			if (parentRect.width > Screen.width || parentRect.height > Screen.height)
			{
                return true;
			}
            
            //Vector3 pxb = renderCamera.ScreenToWorldPoint(new Vector3(myRect.x, myRect.y, renderCamera.nearClipPlane+0.1f));
            //Vector3 pxt = renderCamera.ScreenToWorldPoint(new Vector3(myRect.x, myRect.y + myRect.height, renderCamera.nearClipPlane + 0.1f));
            //Vector3 pyb = renderCamera.ScreenToWorldPoint(new Vector3(myRect.x + myRect.width, myRect.y, renderCamera.nearClipPlane + 0.1f));
            //Vector3 pyt = renderCamera.ScreenToWorldPoint(new Vector3(myRect.x + myRect.width, myRect.y + myRect.height, renderCamera.nearClipPlane + 0.1f));
   
            //Debug.DrawLine(pxt, pyb, Color.red);
            //Debug.DrawLine(pxt, pxb, Color.red);
            //Debug.DrawLine(pxb, pyt, Color.red);

            //pxb = renderCamera.ScreenToWorldPoint(new Vector3(parentRect.x, parentRect.y, renderCamera.nearClipPlane + 0.1f));
            //pyt = renderCamera.ScreenToWorldPoint(new Vector3(parentRect.x + parentRect.width, parentRect.y + parentRect.height, renderCamera.nearClipPlane + 0.1f));
            //pxt = renderCamera.ScreenToWorldPoint(new Vector3(parentRect.x, parentRect.y + parentRect.height, renderCamera.nearClipPlane + 0.1f));
            //pyb = renderCamera.ScreenToWorldPoint(new Vector3(parentRect.x + parentRect.width, parentRect.y, renderCamera.nearClipPlane+0.1f));

            //Debug.DrawLine(pxt, pyb, Color.green);
            //Debug.DrawLine(pxt, pxb, Color.green);
            //Debug.DrawLine(pxb, pyt, Color.green);
            


            if (myRect.Overlaps(parentRect, true))
            {
                //Debug.Log("Limiting recursion, no overlapping!");
                return true;
            }
            //Debug.Log("Limiting recursion, no overlapping!");
            return false;
        }

        private static Vector3[] screenSpaceCorners;
        static Rect RendererBoundsInScreenSpace(Camera theCamera, Renderer r)
        {
            // This is the space occupied by the object's visuals
            // in WORLD space.
            Bounds bigBounds = r.bounds;
            
            if (screenSpaceCorners == null)
                screenSpaceCorners = new Vector3[8];


            // For each of the 8 corners of our renderer's world space bounding box,
            // convert those corners into screen space.
            screenSpaceCorners[0] = theCamera.WorldToScreenPoint(new Vector3(bigBounds.center.x + bigBounds.extents.x, bigBounds.center.y + bigBounds.extents.y, bigBounds.center.z + bigBounds.extents.z));
            screenSpaceCorners[1] = theCamera.WorldToScreenPoint(new Vector3(bigBounds.center.x + bigBounds.extents.x, bigBounds.center.y + bigBounds.extents.y, bigBounds.center.z - bigBounds.extents.z));
            screenSpaceCorners[2] = theCamera.WorldToScreenPoint(new Vector3(bigBounds.center.x + bigBounds.extents.x, bigBounds.center.y - bigBounds.extents.y, bigBounds.center.z + bigBounds.extents.z));
            screenSpaceCorners[3] = theCamera.WorldToScreenPoint(new Vector3(bigBounds.center.x + bigBounds.extents.x, bigBounds.center.y - bigBounds.extents.y, bigBounds.center.z - bigBounds.extents.z));
            screenSpaceCorners[4] = theCamera.WorldToScreenPoint(new Vector3(bigBounds.center.x - bigBounds.extents.x, bigBounds.center.y + bigBounds.extents.y, bigBounds.center.z + bigBounds.extents.z));
            screenSpaceCorners[5] = theCamera.WorldToScreenPoint(new Vector3(bigBounds.center.x - bigBounds.extents.x, bigBounds.center.y + bigBounds.extents.y, bigBounds.center.z - bigBounds.extents.z));
            screenSpaceCorners[6] = theCamera.WorldToScreenPoint(new Vector3(bigBounds.center.x - bigBounds.extents.x, bigBounds.center.y - bigBounds.extents.y, bigBounds.center.z + bigBounds.extents.z));
            screenSpaceCorners[7] = theCamera.WorldToScreenPoint(new Vector3(bigBounds.center.x - bigBounds.extents.x, bigBounds.center.y - bigBounds.extents.y, bigBounds.center.z - bigBounds.extents.z));

            // Now find the min/max X & Y of these screen space corners.
            float min_x = screenSpaceCorners[0].x;
            float min_y = screenSpaceCorners[0].y;
            float max_x = screenSpaceCorners[0].x;
            float max_y = screenSpaceCorners[0].y;

            for (int i = 1; i < 8; i++)
            {
                if (screenSpaceCorners[i].x < min_x)
                {
                    min_x = screenSpaceCorners[i].x;
                }
                if (screenSpaceCorners[i].y < min_y)
                {
                    min_y = screenSpaceCorners[i].y;
                }
                if (screenSpaceCorners[i].x > max_x)
                {
                    max_x = screenSpaceCorners[i].x;
                }
                if (screenSpaceCorners[i].y > max_y)
                {
                    max_y = screenSpaceCorners[i].y;
                }
            }

            return Rect.MinMaxRect(min_x, min_y, max_x, max_y);
        }
        */

        public bool VisibleFromCamera(Camera renderCamera, bool ignoreDistance = true)
        {
			if (ChildSurfaces.Count > 0)
			{
                foreach (MirrorSurface surface in ChildSurfaces)
                {
                    if (surface && surface.VisibleFromCamera(renderCamera, ignoreDistance))
                    {
                        return true;
                    }
                }
			}


            if (!enabled || !MyMeshRenderer || !_material || !gameObject.activeInHierarchy)
            {
                return false;
            }

            //Debug.Log(gameObject.name + " : " + _firstFramesOnEnable + " : " + MyMeshRenderer.isVisible);
            // don't listen to MyMeshRenderer.isVisible for the first 2 frames, it's not always correct.
            if (_firstFramesOnEnable == 0 && !MyMeshRenderer.isVisible)
            {
                return false;
            }

            if (_firstFramesOnEnable > 0)
            {
                _firstFramesOnEnable--;
            }

            // check the normal of the mirror. if the camera is behind it, return early
            Vector3 forward = -1 * MyForwardTransform.forward; //transform.TransformDirection(Vector3.forward);
            Vector3 toOther = renderCamera.transform.position - MyForwardTransform.position;

            if (Vector3.Dot(forward, toOther) < 0) // if we're behind the mirror 
            {
                if (!_wasToFar)
                {
                    _wasToFar = true;

                    // blend the surface
                    if (_material.HasProperty("_DistanceBlend"))
                    {
                        _material.SetFloat("_DistanceBlend", 0);
                    }
                }

                return false;
            }
            else
            {
                if (_wasToFar)
                {
                    _wasToFar = false;
                }
            }

            if (!ignoreDistance)
            {
                bool toFar = Vector3.Distance(ClosestPoint(renderCamera.transform.position), renderCamera.transform.position) > maxRenderingDistance;
                if (toFar && !_wasToFar)
                {
                    _wasToFar = true;

                    // blend the surface
                    if (_material.HasProperty("_DistanceBlend"))
                    {
                        _material.SetFloat("_DistanceBlend", 0);
                        //Debug.Log(gameObject.name + " blend " + blend + " distance: " + distance);
                    }
                }
                if (!toFar && _wasToFar)
                {
                    _wasToFar = false;
                }

                if (toFar)
                {
                    return false;
                }
            }

            //return true;

            Plane[] planes = GeometryUtility.CalculateFrustumPlanes(renderCamera);
            Bounds extendedBounds = MyMeshRenderer.bounds;

            if (_myRenderer != null)
            {
                extendedBounds.Expand(_myRenderer.ExtendFrustumForCulling);
            }

            bool inBounds = GeometryUtility.TestPlanesAABB(planes, extendedBounds);

            return inBounds;
        }


        public bool ShouldRenderBasedOnDistance(Camera renderCamera)
        {
            if (!enabled)
            {
                return false;
            }

            if (!_material)
            {
                return false;
            }

            bool toFar = Vector3.Distance(ClosestPoint(renderCamera.transform.position), renderCamera.transform.position) > maxRenderingDistance;

            if (toFar && !_wasToFar)
            {
                _wasToFar = true;

                // blend the surface
                if (_material.HasProperty("_DistanceBlend"))
                {
                    _material.SetFloat("_DistanceBlend", 0);
                    //Debug.Log(gameObject.name + " blend " + blend + " distance: " + distance);
                }
            }

            if (!toFar && _wasToFar)
            {
                _wasToFar = false;
            }

            return !toFar;
        }


        public Vector3 ClosestPoint(Vector3 toPos)
        {
            Vector3 p = MyMeshRenderer.bounds.ClosestPoint(toPos);
            return p;
        }

        public void UpdateMaterial(Camera.StereoscopicEye eye = Camera.StereoscopicEye.Left, RenderTexture texture = null, MirrorRenderer myRenderer = null, int depth = 1, float distance = 0, Vector3 camPos = default(Vector3))
        {
            if (MyMeshRenderer && _material)
            {
                //Debug.Log(gameObject.name + " set prop 3");

                _myRenderer = myRenderer;
               //Debug.Log(name + " : " + distance + " : " + depth);
                Material m = _material;

                if (depth >= _myRenderer.recursions + 1)
                {
                    // we need to be fully opaque.. no need to do anything else
                    if (m.HasProperty("_DistanceBlend"))
                    {
                        m.SetFloat("_DistanceBlend", 0);
                        //Debug.Log(gameObject.name + " blend " + blend + " distance: " + distance);
                    }
                    return;
                }

                if (m.HasProperty("_flipY"))
                {
                    //Debug.Log("depth: " + depth);
                    if (_myRenderer.FlipSecondRecursion)
                    {
                        m.SetInt("_flipY", depth == 1 ? 0 : 1);
                    }
                    else
                    {
                        m.SetInt("_flipY", 0);
                    }
                }

                if (m.HasProperty("_ForceEye"))
                {
                    m.SetInt("_ForceEye", eye == Camera.StereoscopicEye.Left ? 0 : 1);
                }

                if (eye == Camera.StereoscopicEye.Left && m.HasProperty("_TexLeft") && texture != null)
                {
                    m.SetTexture("_TexLeft", texture);
                }

                if (eye == Camera.StereoscopicEye.Right && XRSettings.enabled && m.HasProperty("_TexRight") && texture != null)
                {
                    m.SetTexture("_TexRight", texture);
                }


                if (depth != -1)
                {
                    float blend;
                    distance = distance - (maxRenderingDistance - (maxRenderingDistance * fadeDistance));
                    blend = Mathf.Clamp(1 - (distance / (maxRenderingDistance * fadeDistance)), 0, 1) * maxBlend;

                    if (useRecursiveDarkening && depth > 1)
                    {
                        float recusiveDarkening = 1 - (((float)depth - 1) / ((float)myRenderer.recursions));
                        recusiveDarkening = recursiveDarkeningCurve.Evaluate(recusiveDarkening);
                        blend = recusiveDarkening; // Mathf.Min(recusiveDarkening, blend);
                    }

                    if (m.HasProperty("_DistanceBlend"))
                    {
                        m.SetFloat("_DistanceBlend", blend);
                        //Debug.Log(gameObject.name + " blend " + blend + " distance: " + distance);
                    }
                }
            }

            foreach (MirrorSurface surface in ChildSurfaces)
            {
                if (surface)
                {
                    // todo, calc distance for this surface
                    float myDistance = Vector3.Distance(camPos, surface.ClosestPoint(camPos));
                    surface.UpdateMaterial(eye, texture, myRenderer, depth, myDistance);
                }
            }
        }



#if UNITY_EDITOR
        void OnSelectionChange()
        {
            if (gameObject == Selection.activeGameObject)
            {
                _isSelectedInEditor = true;

                // make sure we're editing the source materials, not the instance
                if (Material != null)
                {
                    Material.SetColor("_FadeColor", FadeColor);
                    MyMeshRenderer.sharedMaterial = Material;
                    _material = Material;
                }
            }
            else if (_isSelectedInEditor)
            {
                // i'm no longer selected
                _isSelectedInEditor = false;

                OnDisable();
                OnEnable();

                if (_myRenderer != null)
                {
                    _myRenderer.SurfaceGotDeselectedInEditor();
                }
            }
        }

        public void RefreshMaterialInEditor()
        {
            OnDisable();
            OnEnable();
        }

        private void Update()
        {
            _plane = new Plane(-MyForwardTransform.forward, MyForwardTransform.position);

            //if (_oldFadeColor != (FadeColor.r + FadeColor.g + FadeColor.b))
            if (!FadeColor.Equals(_oldFadeColor))
            {
                if (_material)
                {
                    //Debug.Log(gameObject.name + " set prop 1");
                    _material.SetColor("_FadeColor", FadeColor);
                }
                //_oldFadeColor = (FadeColor.r + FadeColor.g + FadeColor.b);
                _oldFadeColor = FadeColor;
            }

            if (_oldMaterial != Material)
            {
                _material = Material;
                RefreshMaterialInEditor();
            }
        }
#endif

#if !UNITY_EDITOR
        private void Update()
        {
            _plane = new Plane(-MyForwardTransform.forward, MyForwardTransform.position);
        }
#endif


        public void TurnOffForceEye()
        {
            foreach (MirrorSurface surface in ChildSurfaces)
            {
                if (surface)
                {
                    surface.TurnOffForceEye();
                }
            }

            if (_material && _material.HasProperty("_ForceEye"))
            {
                //Debug.Log(gameObject.name + " set prop 2");
                _material.SetInt("_ForceEye", -1);
            }
        }
        public void ForceLeftEye()
        {
            foreach (MirrorSurface surface in ChildSurfaces)
            {
                if (surface)
                {
                    surface.ForceLeftEye();
                }
            }

            if (_material && _material.HasProperty("_ForceEye"))
            {
                //Debug.Log(gameObject.name + " set prop 2");
                _material.SetInt("_ForceEye", 0);
            }
        }

        public Vector3[] ShrinkPointsToBounds(Camera reflectionCamera, float distanceToPlane, Camera.MonoOrStereoscopicEye eye)
        {
            if (_myRenderer == null)
            {
                return null;
            }

            _portalBounds = GetPortalBounds();

            if (ChildSurfaces.Count > 0)
            {
                List<Vector3> points = new List<Vector3>(_portalBounds);
                foreach (MirrorSurface surface in ChildSurfaces)
                {
                    points.AddRange(surface.GetPortalBounds());
                }

                // we've got all points, they're in a plane, look from topRight to bottomLeft direction which one is smallest
                Vector3 bottomLeft = _portalBounds[0];
                Vector3 topRight = _portalBounds[3];

                Vector3 minPoint = points[0];
                Vector3 maxPoint = points[0];
                foreach (Vector3 point in points)
                {
                    minPoint = Vector3.Min(minPoint, point);
                    maxPoint = Vector3.Max(maxPoint, point);
                }

                _portalBounds[0] = minPoint;
                _portalBounds[1] = new Vector3(maxPoint.x, minPoint.y, minPoint.z); ;
                _portalBounds[2] = new Vector3(minPoint.x, maxPoint.y, maxPoint.z);
                _portalBounds[3] = maxPoint;
            }


            if (_myRenderer.showDebuggingInfo && _portalBounds != null && reflectionCamera != null && reflectionCamera.cameraType != CameraType.SceneView)
            {
                for (int x = 0; x < 4; x++)
                {
                    if (reflectionCamera.cameraType != CameraType.SceneView)
                    {
                        MirrorRenderer.DebugWireSphere(_portalBounds[x], Color.cyan, 0.05f);
                    }
                }
            }

            bool allPointsFound = GetFustrumBoundsOnPlane(reflectionCamera, eye, distanceToPlane, ref _fustrumBoundsOnPlane);

            if (_myRenderer.showDebuggingInfo && reflectionCamera.cameraType != CameraType.SceneView)
            {
                for (int x = 0; x < 4; x++)
                {
                    if (eye == Camera.MonoOrStereoscopicEye.Mono && reflectionCamera.cameraType != CameraType.SceneView)
                    {
                        MirrorRenderer.DebugWireSphere(_fustrumBoundsOnPlane[x], Color.blue, 0.05f);
                    }
                }
            }

            for (int x = 0; x < 4; x++)
            {
                _shrinkToVectorArray[x] = _portalBounds[x];

                _fustrumBoundsOnPlane[x] = RotatePointAroundPivot(_fustrumBoundsOnPlane[x], transform.position, Quaternion.Inverse(MyForwardTransform.rotation));
                _portalBounds[x] = RotatePointAroundPivot(_portalBounds[x], transform.position, Quaternion.Inverse(MyForwardTransform.rotation));
            }



            Vector2[] rect1 = new Vector2[4];
            Vector2[] rect2 = new Vector2[4];
            for (int x = 0; x < 4; x++)
            {
                rect1[x] = new Vector2(_fustrumBoundsOnPlane[x].x, _fustrumBoundsOnPlane[x].y);
                rect2[x] = new Vector2(_portalBounds[x].x, _portalBounds[x].y);
            }

            Rect r1 = CreateRectFromPoints(rect1);
            Rect r2 = CreateRectFromPoints(rect2);
            Rect rectOut;

            if (RectIntersects(r1, r2, out rectOut))
            {
                // bottomLeft / bottomRight / topLeft
                _shrinkToVectorArray[0] = new Vector3(rectOut.center.x - (rectOut.width / 2f), rectOut.center.y - (rectOut.height / 2f), _fustrumBoundsOnPlane[0].z);
                _shrinkToVectorArray[1] = new Vector3(rectOut.center.x + (rectOut.width / 2f), rectOut.center.y - (rectOut.height / 2f), _fustrumBoundsOnPlane[0].z);
                _shrinkToVectorArray[2] = new Vector3(rectOut.center.x - (rectOut.width / 2f), rectOut.center.y + (rectOut.height / 2f), _fustrumBoundsOnPlane[0].z);
                _shrinkToVectorArray[3] = new Vector3(rectOut.center.x + (rectOut.width / 2f), rectOut.center.y + (rectOut.height / 2f), _fustrumBoundsOnPlane[0].z);

                for (int x = 0; x < 4; x++)
                {
                    _shrinkToVectorArray[x] = RotatePointAroundPivot(_shrinkToVectorArray[x], transform.position, MyForwardTransform.rotation);
                    _shrinkToVectorArray[x] = _shrinkToVectorArray[x] + ((_shrinkToVectorArray[x] - reflectionCamera.transform.position) * 0.025f);
                    if (_myRenderer.showDebuggingInfo && reflectionCamera.cameraType != CameraType.SceneView)
                    {
                        MirrorRenderer.DebugWireSphere(_shrinkToVectorArray[x], Color.green, 0.1f);
                    }
                }
            }

            return _shrinkToVectorArray;
        }


        public Vector3[] GetPortalBounds() // , float depthDistance, Camera.MonoOrStereoscopicEye eye
        {
            Bounds b = _myMeshFilter.sharedMesh.bounds;

            // worldspace 0,0,0 is now the transform
            Vector3 boundsCenterOffset = Vector3.zero - b.center;
            b.center = MyMeshRenderer.transform.position - boundsCenterOffset;

            Vector3 bottomLeft = b.min; // bottomLeft
            Vector3 topRight = b.max; // topRight
            Vector3 topLeft = new Vector3(b.min.x, b.max.y, b.max.z);
            Vector3 bottomRight = new Vector3(b.max.x, b.min.y, b.min.z);

            float scaleLarger = 1.0f;

            bottomLeft = ScaleAroundPivot(bottomLeft, MyMeshRenderer.transform.position, MyMeshRenderer.transform.lossyScale * scaleLarger);
            topRight = ScaleAroundPivot(topRight, MyMeshRenderer.transform.position, MyMeshRenderer.transform.lossyScale * scaleLarger);
            topLeft = ScaleAroundPivot(topLeft, MyMeshRenderer.transform.position, MyMeshRenderer.transform.lossyScale * scaleLarger);
            bottomRight = ScaleAroundPivot(bottomRight, MyMeshRenderer.transform.position, MyMeshRenderer.transform.lossyScale * scaleLarger);

            bottomLeft = RotatePointAroundPivot(bottomLeft, MyMeshRenderer.transform.position, MyMeshRenderer.transform.rotation);
            topRight = RotatePointAroundPivot(topRight, MyMeshRenderer.transform.position, MyMeshRenderer.transform.rotation);
            topLeft = RotatePointAroundPivot(topLeft, MyMeshRenderer.transform.position, MyMeshRenderer.transform.rotation);
            bottomRight = RotatePointAroundPivot(bottomRight, MyMeshRenderer.transform.position, MyMeshRenderer.transform.rotation);

            /*MirrorRenderer.DebugWireSphere(bottomLeft, Color.cyan, 0.15f);
            MirrorRenderer.DebugWireSphere(topRight, Color.red, 0.17f);
            MirrorRenderer.DebugWireSphere(topLeft, Color.green, 0.19f);
            MirrorRenderer.DebugWireSphere(bottomRight, Color.yellow, 0.21f);
            MirrorRenderer.DebugBounds(b, Color.cyan);*/

            Vector3[] points = new Vector3[4];
            points[0] = bottomLeft;
            points[1] = bottomRight;
            points[2] = topLeft;
            points[3] = topRight;


            return points;
        }


        private bool GetFustrumBoundsOnPlane(Camera reflectionCamera, Camera.MonoOrStereoscopicEye eye, float distanceToPlane, ref Vector3[] positionsOnPlane)
        {
            /*float offset = 0;
			if (distanceToPlane < reflectionCamera.nearClipPlane)
			{
                // move the points forward with the near plane
                offset = reflectionCamera.nearClipPlane - MathF.Max(distanceToPlane, 0);
            }*/

            //reflectionCamera.CalculateFrustumCorners(new Rect(-0f, -0f, 1f, 1f), 1, eye, _frustumCorners);
            float extendFrustum = 0;
            if (_myRenderer != null)
            {
                extendFrustum = _myRenderer.ExtendFrustumForCulling;
            }
            reflectionCamera.CalculateFrustumCorners(new Rect(-extendFrustum, -extendFrustum, 1f + (2 * extendFrustum), 1f + (2 * extendFrustum)), 1, eye, _frustumCorners);


            bool allSucceeded = true;
            for (int i = 0; i < 4; i++)
            {
                _frustumCorners[i] = reflectionCamera.transform.TransformPoint(_frustumCorners[i]);

                if (!ProjectPointOnPlane(reflectionCamera.transform.position, _frustumCorners[i], ref positionsOnPlane[i]))
                {
                    // we're not hitting the plane.. hit far enough, then get the closest to the plane
                    positionsOnPlane[i] = reflectionCamera.transform.position + ((_frustumCorners[i] - reflectionCamera.transform.position) * 25f);
                    positionsOnPlane[i] = _plane.ClosestPointOnPlane(positionsOnPlane[i]);
                    //MirrorRenderer.DebugWireSphere(positionsOnPlane[i], Color.green, 0.05f);
                    allSucceeded = false;

                }

                //MirrorRenderer.DebugWireSphere(frustumCorners[i], Color.magenta, 0.1f);
                //MirrorRenderer.DebugWireSphere(positionsOnPlane[i], Color.green, 0.05f);
            }
            return allSucceeded;
        }

        private bool ProjectPointOnPlane(Vector3 origin, Vector3 target, ref Vector3 posOnPlane)
        {
            Ray r = new Ray(origin, (target - origin));
            float distance = 0;
            if (_plane.Raycast(r, out distance))
            {
                posOnPlane = r.origin + (r.direction.normalized * (distance));
                return true;
            }
            return false;
        }
        public Vector3 RotatePointAroundPivot(Vector3 point, Vector3 pivotPosition, Quaternion rotation)
        {
            return pivotPosition + (rotation * (point - pivotPosition)); // returns new position of the point;
        }
        public Vector3 ScaleAroundPivot(Vector3 target, Vector3 pivot, Vector3 newScale)
        {
            // calc final position post-scale
            Vector3 dir = (target - pivot);
            dir.Scale(newScale);
            return pivot + dir;
        }

        private static Rect CreateRectFromPoints(Vector2[] points)
        {
            float minX = float.MaxValue;
            float minY = float.MaxValue;
            float maxX = float.MinValue;
            float maxY = float.MinValue;

            foreach (Vector2 point in points)
            {
                if (point.x < minX)
                {
                    minX = point.x;
                }
                if (point.y < minY)
                {
                    minY = point.y;
                }
                if (point.x > maxX)
                {
                    maxX = point.x;
                }
                if (point.y > maxY)
                {
                    maxY = point.y;
                }
            }

            return new Rect(minX, minY, maxX - minX, maxY - minY);
        }

        private bool RectIntersects(Rect r1, Rect r2, out Rect area)
        {
            area = new Rect();

            if (r2.Overlaps(r1))
            {
                float x1 = Mathf.Min(r1.xMax, r2.xMax);
                float x2 = Mathf.Max(r1.xMin, r2.xMin);
                float y1 = Mathf.Min(r1.yMax, r2.yMax);
                float y2 = Mathf.Max(r1.yMin, r2.yMin);
                area.x = Mathf.Min(x1, x2);
                area.y = Mathf.Min(y1, y2);
                area.width = Mathf.Max(0.0f, x1 - x2);
                area.height = Mathf.Max(0.0f, y1 - y2);

                return true;
            }

            return false;
        }
    }
}