namespace PawSip.Core;

internal static class Log
{
    private static readonly object Gate = new();

    public static void Error(Exception? ex, string? context = null)
    {
        if (ex == null) return;
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(DataStore.Folder);
                var file = Path.Combine(DataStore.Folder, "error.log");
                if (File.Exists(file) && new FileInfo(file).Length > 500_000) File.Delete(file);
                File.AppendAllText(file, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {context}{Environment.NewLine}{ex}{Environment.NewLine}{Environment.NewLine}");
            }
        }
        catch
        {
            // never crash because of logging
        }
    }
}
