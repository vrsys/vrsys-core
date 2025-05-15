using Oculus.Avatar2;
using UnityEngine;
using UnityEngine.InputSystem;
using VRSYS.Core.Logging;

public class MetaAvatarScaleHandler : MonoBehaviour
{
    #region Member Variables

    public static MetaAvatarScaleHandler Instance;

    private bool initialized = false;
    private MecanimLegsAnimationController legsAnimationController;

    [Header("Debug")] 
    [SerializeField] private bool enableDebugTrigger = false;
    [SerializeField] private InputAction debugTrigger;
    
    #endregion

    #region MonoBehaviour Callbacks

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(this);
            return;
        }

        Instance = this;

        if(enableDebugTrigger)
            debugTrigger.Enable();
    }

    private void Update()
    {
        if (!initialized)
        {
            Initialize();
            return;
        }
        
        if(debugTrigger.WasReleasedThisFrame())
            TriggerRecalibrate();
    }

    #endregion

    #region Custom Methods

    private void Initialize()
    {
        legsAnimationController = GetComponentInChildren<MecanimLegsAnimationController>();

        initialized = legsAnimationController != null;
    }

    public void TriggerRecalibrate()
    {
        if (!initialized)
        {
            ExtendedLogger.LogWarning(GetType().Name, "Scale handler not done initializing.", this);
            return;
        }

        legsAnimationController.RecalibrateStandingHeight();
    }

    #endregion
}