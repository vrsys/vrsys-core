namespace VRSYS.Meta.Collocation
{
    public class AligningToAnchorStateHandler : CollocationStateHandler
    {
        public AligningToAnchorStateHandler(CollocationManager manager) : base(manager)
        {
            State = CollocationState.AligningToAnchor;
        }

        public override void StartState()
        {
            throw new System.NotImplementedException();
        }

        protected override void EndState()
        {
            throw new System.NotImplementedException();
        }
    }
}