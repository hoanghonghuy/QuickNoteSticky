using DevSticky.Models;
using FsCheck;
using FsCheck.Xunit;

namespace DevSticky.Tests.Properties;

/// <summary>
/// Property-based tests for WindowRect validation
/// **Feature: devsticky, Property 3: Window Size Minimum Constraints**
/// **Validates: Requirements 1.4**
/// </summary>
public class WindowRectPropertyTests
{
    /// <summary>
    /// Property 3: Window Size Minimum Constraints
    /// For any WindowRect with width W and height H, the validated WindowRect 
    /// SHALL have width >= 200 and height >= 150, clamping smaller values to these minimums.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property WindowRect_Width_ShouldBeAtLeastMinimum()
    {
        return Prop.ForAll(
            Arb.From(Gen.Choose(-1000, 1000).Select(x => (double)x)),
            width =>
            {
                var rect = new WindowRect { Width = width };
                return rect.Width >= WindowRect.MinWidth;
            });
    }

    [Property(MaxTest = 100)]
    public Property WindowRect_Height_ShouldBeAtLeastMinimum()
    {
        return Prop.ForAll(
            Arb.From(Gen.Choose(-1000, 1000).Select(x => (double)x)),
            height =>
            {
                var rect = new WindowRect { Height = height };
                return rect.Height >= WindowRect.MinHeight;
            });
    }

    [Property(MaxTest = 100)]
    public Property WindowRect_CreateValidated_ShouldClampBothDimensions()
    {
        var gen = from top in Gen.Choose(-500, 500).Select(x => (double)x)
                  from left in Gen.Choose(-500, 500).Select(x => (double)x)
                  from width in Gen.Choose(-500, 1000).Select(x => (double)x)
                  from height in Gen.Choose(-500, 1000).Select(x => (double)x)
                  select (top, left, width, height);

        return Prop.ForAll(Arb.From(gen), tuple =>
        {
            var rect = WindowRect.CreateValidated(tuple.top, tuple.left, tuple.width, tuple.height);
            return rect.Width >= WindowRect.MinWidth && 
                   rect.Height >= WindowRect.MinHeight &&
                   rect.Top == tuple.top &&
                   rect.Left == tuple.left;
        });
    }

    [Property(MaxTest = 100)]
    public Property WindowRect_ValidDimensions_ShouldBePreserved(PositiveInt width, PositiveInt height)
    {
        var w = Math.Max(width.Get, (int)WindowRect.MinWidth);
        var h = Math.Max(height.Get, (int)WindowRect.MinHeight);
        
        var rect = new WindowRect { Width = w, Height = h };
        
        return (rect.Width == w && rect.Height == h).ToProperty();
    }
}
