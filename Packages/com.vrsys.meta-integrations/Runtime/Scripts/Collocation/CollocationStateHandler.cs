
using System;

namespace VRSYS.Meta.Collocation
{
    [Serializable]
    public abstract class CollocationStateHandler
    {
        #region Properties

        public CollocationState State { get; protected set; }

        protected CollocationManager _manager;

        protected int _retryCount;

        #endregion

        #region Constructor

        public CollocationStateHandler(CollocationManager manager)
        {
            _manager = manager;

            _retryCount = 0;
        }

        #endregion

        #region Public Methods

        public abstract void StartState();
        
        protected abstract void EndState();

        #endregion
    }
}
