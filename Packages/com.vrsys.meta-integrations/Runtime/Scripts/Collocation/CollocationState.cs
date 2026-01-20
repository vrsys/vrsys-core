namespace VRSYS.Meta.Collocation
{
    public enum CollocationState
    {
        Idle,
        LoadingLocalAnchor,
        SearchingCollocationSession,
        LoadingSessionAnchor,
        AligningToAnchor,
        CreatingSession,
        AdvertisingSession,
        CreatingSessionAnchor,
        SavingSessionAnchor,
        SharingSessionAnchor,
        DisplaySessions
    }
    
    public enum CollocationStateStatus
    {
        Started,
        Retry,
        Running,
        Success,
        Failed
    }
}
