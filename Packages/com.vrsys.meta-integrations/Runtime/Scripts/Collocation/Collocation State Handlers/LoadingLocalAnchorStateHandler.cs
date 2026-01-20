using VRSYS.Core.Logging;

namespace VRSYS.Meta.Collocation
{
    public class LoadingLocalAnchorStateHandler : CollocationStateHandler
    {
        
    #region Constructor

    public LoadingLocalAnchorStateHandler(CollocationManager manager) : base(manager)
    {
        State = CollocationState.LoadingLocalAnchor;
    }

    #endregion

    #region Collocation State Handler Methods

    public override void StartState()
    {
        // Check for existing anchors and load them
        LoadAnchor();
    }

    #endregion

    #region Private Methods

    private void LoadAnchor()
    {
        
    }

    protected override void EndState()
    {
        // Autoselect existing anchor
        // OR
        // Show UI for selecting existing anchors
        
        manager.EnterState(manager.LoadingLocalAnchorStateHandler);
    }

    #endregion
    }
}