
using System;

namespace VRSYS.Meta.Collocation
{
    [Serializable]
    public abstract class CollocationStateHandler
    {
        #region Properties

        public CollocationState State { get; protected set; }

        protected CollocationManager manager;

        protected int _retryCount;

        #endregion

        #region Constructor

        public CollocationStateHandler(CollocationManager manager)
        {
            this.manager = manager;

            _retryCount = 0;
        }

        #endregion

        #region Public Methods

        public abstract void StartState();

        #endregion
    }
}
