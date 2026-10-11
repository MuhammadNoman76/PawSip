using System.Diagnostics;

namespace PawSip.Core;

/// <summary>
/// Small helpers that behave the same on .NET 10 (Windows 10/11 edition) and .NET Framework 4.8
/// (Windows 7/8 edition), so the rest of the code is shared unchanged.
/// </summary>
internal static class Compat
{
    public static int ProcessId
    {
        get
        {
#if NETFRAMEWORK
            using var p = Process.GetCurrentProcess();
            return p.Id;
#else
            return Environment.ProcessId;
#endif
        }
    }

    public static string? ExecutablePath
    {
        get
        {
#if NETFRAMEWORK
            using var p = Process.GetCurrentProcess();
            return p.MainModule?.FileName;
#else
            return Environment.ProcessPath;
#endif
        }
    }

    /// <summary>Atomic-ish replace of <paramref name="target"/> with <paramref name="source"/>.</summary>
    public static void MoveOverwrite(string source, string target)
    {
#if NETFRAMEWORK
        if (File.Exists(target)) File.Replace(source, target, null);
        else File.Move(source, target);
#else
        File.Move(source, target, overwrite: true);
#endif
    }
}

#if NETFRAMEWORK
internal static class LegacyExtensions
{
    // Lets "foreach (var (key, value) in dictionary)" compile on .NET Framework.
    public static void Deconstruct<TKey, TValue>(this KeyValuePair<TKey, TValue> pair, out TKey key, out TValue value)
    {
        key = pair.Key;
        value = pair.Value;
    }
}
#endif
