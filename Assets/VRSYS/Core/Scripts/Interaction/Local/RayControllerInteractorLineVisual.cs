using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(RayControllerInteractor))]
[RequireComponent(typeof(LineRenderer))]
public class RayControllerInteractorLineVisual : MonoBehaviour
{

    private RayControllerInteractor interactor;

    [SerializeField]
    float lineOriginOffset = 0f;

    [SerializeField]
    float lineLength = 10f;

    [SerializeField][Range(0.0001f, 0.05f)]
    float lineWidth = 0.005f;

    [SerializeField]
    AnimationCurve widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);

    [SerializeField]
    Color unhitColor = Color.white;    
    
    [SerializeField]
    Color validColor = Color.green;

    [SerializeField]
    Color invalidColor = Color.red;

    [SerializeField]
    Color activeColor = Color.yellow;

    public float LineOriginOffset { get => lineOriginOffset; set => lineOriginOffset = value; }
    public float LineLength { get => lineLength; set => lineLength = value; }
    public float LineWidth { get => lineWidth; set => lineWidth = value; }
    public AnimationCurve WidthCurve { get => widthCurve; set => widthCurve = value; }

    public Color UnhitColor { get => unhitColor; set => unhitColor = value; }
    public Color ValidColor { get => validColor; set => validColor = value; }
    public Color InvalidColor { get => invalidColor; set => invalidColor = value; }
    public Color ActiveColor { get => activeColor; set => activeColor = value; }

    LineRenderer lineRenderer;



    private void Awake()
    {

        Setup();
    }

    // Start is called before the first frame update
    void Start()
    {
        UpdateLineGeometry();
    }

    // Update is called once per frame
    void Update()
    {
        //UpdateLineGeometry();
    }

    #region Custom Methods
    private void UpdateLineGeometry()
    {
        lineRenderer.widthMultiplier = LineWidth;
        lineRenderer.widthCurve = WidthCurve;

    }

    private void Setup()
    {
        interactor = GetComponent<RayControllerInteractor>();
        LineOriginOffset = interactor.rayOriginOffset;
        LineLength = interactor.rayLength;

        lineRenderer = GetComponent<LineRenderer>();
        lineRenderer.useWorldSpace = false;
        lineRenderer.SetPosition(0, transform.localPosition + transform.forward * LineOriginOffset);
        lineRenderer.SetPosition(1, transform.localPosition + transform.forward * LineLength);


    }
    #endregion
}
