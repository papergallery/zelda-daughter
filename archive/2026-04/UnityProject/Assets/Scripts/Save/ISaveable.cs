namespace ZeldaDaughter.Save
{
    public interface ISaveable
    {
        string SaveId { get; }
        object CaptureState();
        void RestoreState(object state);
    }
}
