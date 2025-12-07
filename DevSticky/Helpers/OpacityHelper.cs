namespace DevSticky.Helpers;

/// <summary>
/// Helper class for opacity value operations
/// </summary>
public static class OpacityHelper
{
    public const double MinOpacity = 0.2;
    public const double MaxOpacity = 1.0;
    public const double DefaultOpacity = 0.9;
    public const double Step = 0.1;

    /// <summary>
    /// Clamps opacity value to valid range [0.2, 1.0]
    /// </summary>
    public static double Clamp(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
            return DefaultOpacity;
        return Math.Clamp(value, MinOpacity, MaxOpacity);
    }

    /// <summary>
    /// Increases opacity by step, clamped to max
    /// </summary>
    public static double Increase(double current)
    {
        return Clamp(current + Step);
    }

    /// <summary>
    /// Decreases opacity by step, clamped to min
    /// </summary>
    public static double Decrease(double current)
    {
        return Clamp(current - Step);
    }

    /// <summary>
    /// Adjusts opacity by given step (positive or negative)
    /// </summary>
    public static double Adjust(double current, double step)
    {
        return Clamp(current + step);
    }
}
