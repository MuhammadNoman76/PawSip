namespace PawSip.Core;

/// <summary>Only one PawSip runs; starting it again just opens the existing window.</summary>
internal static class SingleInstance
{
    private static readonly string Name = "PawSip-" + Environment.UserName;
    private static Mutex? s_mutex;
    private static EventWaitHandle? s_show;

    public static bool TryStart()
    {
        s_mutex = new Mutex(true, @"Local\" + Name, out var created);
        s_show = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\" + Name + "-show");
        return created;
    }

    public static void SignalFirstInstance() => s_show?.Set();

    public static void ListenForShow(Action onShow)
    {
        var handle = s_show;
        if (handle == null) return;
        var thread = new Thread(() =>
        {
            while (true)
            {
                handle.WaitOne();
                onShow();
            }
        })
        {
            IsBackground = true,
            Name = "PawSip show listener",
        };
        thread.Start();
    }
}
