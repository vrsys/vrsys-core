
namespace VRSYS.Meta.Collocation
{
    public class LoadSessionAnchorStateHandler : CollocationStateHandler
    {
        #region Constructor

        public LoadSessionAnchorStateHandler(CollocationManager manager) : base(manager)
        {
            State = CollocationState.LoadingSessionAnchor;
        }

        #endregion

        #region Collocation State Handler Methods

        public override void StartState()
        {
            
        }

        protected override void EndState()
        {
            
        }

        #endregion
    }
}
