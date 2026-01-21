
namespace VRSYS.Meta.Collocation
{
    public class CreateSessionAnchorStateHandler : CollocationStateHandler
    {
        #region Constructor

        public CreateSessionAnchorStateHandler(CollocationManager manager) : base(manager)
        {
            State = CollocationState.CreatingSessionAnchor;
        }

        #endregion

        #region Collocation State Handler Methodds

        public override void StartState()
        {
            
        }

        protected override void EndState()
        {
            
        }

        #endregion
    }
}
