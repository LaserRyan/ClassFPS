// Deliberately separate from future match/hero-selection rules.
public static class NetworkTestRules
{
    public static bool DevelopmentFeatures
    {
        get
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            return true;
#else
            return false;
#endif
        }
    }

    public static bool CanChangeHero(bool serverSetting, bool senderOwnsPlayer,
        bool knownHero, bool inProgress, uint expectedRevision, uint currentRevision) =>
        DevelopmentFeatures && serverSetting && senderOwnsPlayer && knownHero && !inProgress &&
        expectedRevision == currentRevision;
}
