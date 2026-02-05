
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

        /// <summary>
        /// Base implementation that does not enter a next state.
        /// Overrride to perform teardown actions.
        /// </summary>
        protected virtual void EndState()
        {
            return;
        }

        /// <summary>
        /// Base implementation enters the given next state.
        /// Extend to perform teardown actions before entering the next state.
        /// </summary>
        /// <param name="nextStateHandler"></param>
        protected virtual void EndState<T>() where T : CollocationStateHandler
        {
            _manager.EnterState<T>();
        }

        #endregion
    }
}
