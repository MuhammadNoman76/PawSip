namespace PawSip;

/// <summary>Math.Clamp, which .NET Framework 4.8 doesn't have.</summary>
internal static class MathEx
{
    public static int Clamp(int value, int min, int max) => value < min ? min : value > max ? max : value;
    public static double Clamp(double value, double min, double max) => value < min ? min : value > max ? max : value;
}
