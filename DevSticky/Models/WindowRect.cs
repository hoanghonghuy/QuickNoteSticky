namespace DevSticky.Models;

/// <summary>
/// Value object for window position and size
/// </summary>
public class WindowRect
{
    public const double MinWidth = 200;
    public const double MinHeight = 150;
    public const double DefaultWidth = 300;
    public const double DefaultHeight = 200;

    private double _width = DefaultWidth;
    private double _height = DefaultHeight;

    public double Top { get; set; } = 100;
    public double Left { get; set; } = 100;
    
    public double Width
    {
        get => _width;
        set => _width = double.IsNaN(value) || double.IsInfinity(value) ? MinWidth : Math.Max(value, MinWidth);
    }
    
    public double Height
    {
        get => _height;
        set => _height = double.IsNaN(value) || double.IsInfinity(value) ? MinHeight : Math.Max(value, MinHeight);
    }

    /// <summary>
    /// Creates a validated WindowRect with minimum size constraints
    /// </summary>
    public static WindowRect CreateValidated(double top, double left, double width, double height)
    {
        return new WindowRect
        {
            Top = top,
            Left = left,
            Width = width,  // Setter will clamp to minimum
            Height = height // Setter will clamp to minimum
        };
    }
}
