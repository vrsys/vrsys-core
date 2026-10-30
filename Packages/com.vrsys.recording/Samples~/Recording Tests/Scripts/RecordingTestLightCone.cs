using UnityEngine;

namespace VRSYS.Recording.Testing
{
    /// <summary>
    /// Makes a spot light's cone visible: a translucent beam whose opening follows the light's outer and inner
    /// spot angles, whose color follows the light color and whose brightness follows the light intensity.
    ///
    /// It only reads the light, every frame and also in edit mode, so it shows whatever drives the light: the live
    /// <see cref="RecordingTestSpotLight"/> animation, or the values a <see cref="RecordingTestSpotLightRecorder"/>
    /// applies during a replay. The beam starts at the light and points along the light's forward axis; place this
    /// component on a child of the light with an identity local pose.
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu("VRSYS/Recording/Testing/Recording Test Light Cone")]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class RecordingTestLightCone : MonoBehaviour
    {
        [Tooltip("The spot light to visualize. Defaults to the Light on the parent.")]
        public Light source;

        [Tooltip("Length of the beam in metres, e.g. the distance from the light to the surface it hits.")]
        public float length = 1f;

        [Tooltip("Light intensity at which the beam reaches full opacity.")]
        public float referenceIntensity = 6f;

        [Tooltip("Opacity of the outer cone at the light, at the reference intensity.")]
        [Range(0f, 1f)]
        public float outerOpacity = 0.28f;

        [Tooltip("Opacity of the inner (full-intensity) cone at the light, at the reference intensity.")]
        [Range(0f, 1f)]
        public float innerOpacity = 0.32f;

        [Tooltip("Share of the opacity left at the end of the beam (0 = fades out completely).")]
        [Range(0f, 1f)]
        public float endFade = 0.25f;

        [Tooltip("Number of sides of each cone.")]
        [Range(8, 96)]
        public int segments = 48;

        private Mesh _mesh;
        private Vector3[] _vertices;
        private Color[] _colors;
        private int _builtSegments;
        private float _lastOuter = -1f, _lastInner = -1f, _lastIntensity = -1f, _lastLength = -1f;
        private Color _lastColor;
        private bool _lastEnabled;

        private void OnEnable()
        {
            if (source == null && transform.parent != null)
                source = transform.parent.GetComponent<Light>();
            Build();
            Refresh(true);
        }

        private void OnDisable()
        {
            if (_mesh != null)
            {
                if (Application.isPlaying) Destroy(_mesh);
                else DestroyImmediate(_mesh);
            }
            _mesh = null;
        }

        // Inspector edits are applied on the next LateUpdate (meshes must not be assigned during OnValidate).
        private void OnValidate()
        {
            _lastOuter = -1f;
        }

        private void LateUpdate()
        {
            if (_builtSegments != segments) Build();
            Refresh(false);
        }

        // Two open cones (outer and inner), each made of an apex vertex per side and a base ring.
        private void Build()
        {
            if (_mesh == null)
            {
                _mesh = new Mesh { name = "Light Cone (generated)", hideFlags = HideFlags.DontSave };
                _mesh.MarkDynamic();
            }
            _mesh.Clear();
            _builtSegments = segments;

            int perCone = segments * 2; // apex copies + base ring
            _vertices = new Vector3[perCone * 2];
            _colors = new Color[perCone * 2];
            var triangles = new int[segments * 3 * 2];
            for (int cone = 0; cone < 2; cone++)
            {
                int v0 = cone * perCone, t0 = cone * segments * 3;
                for (int i = 0; i < segments; i++)
                {
                    triangles[t0 + i * 3] = v0 + i;                                   // apex
                    triangles[t0 + i * 3 + 1] = v0 + segments + i;                    // base i
                    triangles[t0 + i * 3 + 2] = v0 + segments + (i + 1) % segments;   // base i+1
                }
            }
            _mesh.vertices = _vertices;
            _mesh.colors = _colors;
            _mesh.triangles = triangles;
            GetComponent<MeshFilter>().sharedMesh = _mesh;
            _lastOuter = -1f; // force a refresh
        }

        private void Refresh(bool force)
        {
            if (_mesh == null || source == null)
                return;

            float outer = source.spotAngle;
            float inner = Mathf.Min(source.innerSpotAngle, outer);
            float intensity = source.enabled && source.gameObject.activeInHierarchy ? source.intensity : 0f;
            Color color = source.color;
            bool enabled = source.enabled;
            if (!force && outer == _lastOuter && inner == _lastInner && intensity == _lastIntensity &&
                color == _lastColor && length == _lastLength && enabled == _lastEnabled)
                return;
            _lastOuter = outer; _lastInner = inner; _lastIntensity = intensity; _lastColor = color;
            _lastLength = length; _lastEnabled = enabled;

            float strength = referenceIntensity > 0f ? Mathf.Clamp01(intensity / referenceIntensity) : 1f;
            FillCone(0, outer, color, outerOpacity * strength);
            FillCone(1, inner, color, innerOpacity * strength);
            _mesh.vertices = _vertices;
            _mesh.colors = _colors;
            _mesh.RecalculateBounds();
        }

        private void FillCone(int cone, float angle, Color color, float opacity)
        {
            int v0 = cone * segments * 2;
            float radius = Mathf.Tan(Mathf.Clamp(angle, 0f, 170f) * 0.5f * Mathf.Deg2Rad) * length;
            Color atLight = new Color(color.r, color.g, color.b, opacity);
            Color atEnd = new Color(color.r, color.g, color.b, opacity * endFade);
            for (int i = 0; i < segments; i++)
            {
                float b = (float)i / segments * 2f * Mathf.PI;
                _vertices[v0 + i] = Vector3.zero;
                _colors[v0 + i] = atLight;
                _vertices[v0 + segments + i] = new Vector3(Mathf.Cos(b) * radius, Mathf.Sin(b) * radius, length);
                _colors[v0 + segments + i] = atEnd;
            }
        }
    }
}
