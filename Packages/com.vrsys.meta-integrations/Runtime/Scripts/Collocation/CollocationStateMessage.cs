
namespace VRSYS.Meta.Collocation
{
    public struct CollocationStateMessage
    {
        #region Properties

        public CollocationState State { get; private set; }
        
        public CollocationStateStatus Status { get; private set; }
        
        public string Message { get; private set; }

        #endregion

        #region Constructor

        public CollocationStateMessage(CollocationState state, CollocationStateStatus status, string message)
        {
            State = state;
            Status = status;
            Message = message;
        }

        #endregion
    }
}
