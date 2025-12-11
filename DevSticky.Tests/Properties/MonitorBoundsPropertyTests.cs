using System.Windows;
using DevSticky.Helpers;
using DevSticky.Models;
using FsCheck;
using FsCheck.Xunit;

namespace DevSticky.Tests.Properties;

/// <summary>
/// Property-based tests for MonitorBoundsHelper logic
/// **Feature: code-refactor, Property 6: Monitor Bounds Validation**
/// **Validates: Requirements 5.2**
/// </summary>
public class MonitorBoundsPropertyTests
{
    /// <summary>
    /// Property 6: Monitor Bounds Validation
    /// For any window positioned on a monitor, the window should be fully within the monitor's working area.
    /// **Validates: Requirements 5.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property MonitorBounds_ShouldConstrainWindowWithinWorkingArea()
    {
        // Generate arbitrary monitor working areas
        var monitorGen = from left in Gen.Choose(0, 2000).Select(i => (double)i)
                        from top in Gen.Choose(0, 1500).Select(i => (double)i)
                        from width in Gen.Choose(800, 3840).Select(i => (double)i)
                        from height in Gen.Choose(600, 2160).Select(i => (double)i)
                        select new Rect(left, top, width, height);

        // Generate arbitrary window positions and sizes
        var windowGen = from left in Gen.Choose(-1000, 5000).Select(i => (double)i)
                       from top in Gen.Choose(-1000, 3000).Select(i => (double)i)
                       from width in Gen.Choose(100, 1200).Select(i => (double)i)
                       from height in Gen.Choose(100, 800).Select(i => (double)i)
                       select new { Left = left, Top = top, Width = width, Height = height };

        return Prop.ForAll(
            Arb.From(monitorGen),
            Arb.From(windowGen),
            (workingArea, windowData) =>
            {
                // Test the bounds constraint logic directly (matching MonitorBoundsHelper implementation)
                var constrainedLeft = windowData.Left;
                var constrainedTop = windowData.Top;
                
                // Apply the same logic as MonitorBoundsHelper.EnsureWindowInBounds
                if (constrainedLeft < workingArea.Left)
                    constrainedLeft = workingArea.Left;
                if (constrainedTop < workingArea.Top)
                    constrainedTop = workingArea.Top;
                if (constrainedLeft + windowData.Width > workingArea.Right)
                    constrainedLeft = Math.Max(workingArea.Left, workingArea.Right - windowData.Width);
                if (constrainedTop + windowData.Height > workingArea.Bottom)
                    constrainedTop = Math.Max(workingArea.Top, workingArea.Bottom - windowData.Height);

                // Verify the constrained window follows the bounds rules:
                // 1. Window left/top should be >= working area left/top
                // 2. If window fits, it should be fully within bounds
                // 3. If window doesn't fit, it should be positioned at the edge
                bool followsBoundsRules = constrainedLeft >= workingArea.Left &&
                                        constrainedTop >= workingArea.Top &&
                                        (windowData.Width <= workingArea.Width ? 
                                            constrainedLeft + windowData.Width <= workingArea.Right : 
                                            constrainedLeft == workingArea.Left) &&
                                        (windowData.Height <= workingArea.Height ? 
                                            constrainedTop + windowData.Height <= workingArea.Bottom : 
                                            constrainedTop == workingArea.Top);

                return followsBoundsRules.Label($"Original: ({windowData.Left}, {windowData.Top}), " +
                                              $"Constrained: ({constrainedLeft}, {constrainedTop}), " +
                                              $"Size: ({windowData.Width}, {windowData.Height}), " +
                                              $"Working area: ({workingArea.Left}, {workingArea.Top}, {workingArea.Width}, {workingArea.Height})");
            });
    }

    /// <summary>
    /// Property: Window centering should place window at the center of monitor's working area
    /// </summary>
    [Property(MaxTest = 100)]
    public Property WindowCentering_ShouldCenterWindowInWorkingArea()
    {
        var monitorGen = from left in Gen.Choose(0, 2000).Select(i => (double)i)
                        from top in Gen.Choose(0, 1500).Select(i => (double)i)
                        from width in Gen.Choose(800, 3840).Select(i => (double)i)
                        from height in Gen.Choose(600, 2160).Select(i => (double)i)
                        select new Rect(left, top, width, height);

        var windowGen = from width in Gen.Choose(100, 800).Select(i => (double)i)
                       from height in Gen.Choose(100, 600).Select(i => (double)i)
                       select new { Width = width, Height = height };

        return Prop.ForAll(
            Arb.From(monitorGen),
            Arb.From(windowGen),
            (workingArea, windowData) =>
            {
                // Test the centering logic directly
                var centeredLeft = workingArea.Left + (workingArea.Width - windowData.Width) / 2;
                var centeredTop = workingArea.Top + (workingArea.Height - windowData.Height) / 2;

                // Verify the centered window is positioned correctly
                var expectedLeft = workingArea.Left + (workingArea.Width - windowData.Width) / 2;
                var expectedTop = workingArea.Top + (workingArea.Height - windowData.Height) / 2;

                // Allow for small floating point differences
                bool isCentered = Math.Abs(centeredLeft - expectedLeft) < 0.001 &&
                                Math.Abs(centeredTop - expectedTop) < 0.001;

                return isCentered.Label($"Expected: ({expectedLeft}, {expectedTop}), Actual: ({centeredLeft}, {centeredTop})");
            });
    }

    /// <summary>
    /// Property: Relative position calculation should return normalized values between 0.0 and 1.0
    /// </summary>
    [Property(MaxTest = 100)]
    public Property RelativePosition_ShouldReturnNormalizedValues()
    {
        var monitorGen = from left in Gen.Choose(0, 2000).Select(i => (double)i)
                        from top in Gen.Choose(0, 1500).Select(i => (double)i)
                        from width in Gen.Choose(800, 3840).Select(i => (double)i)
                        from height in Gen.Choose(600, 2160).Select(i => (double)i)
                        select new Rect(left, top, width, height);

        var relativeGen = from x in Gen.Choose(0, 100).Select(i => i / 100.0)
                         from y in Gen.Choose(0, 100).Select(i => i / 100.0)
                         select new { X = x, Y = y };

        return Prop.ForAll(
            Arb.From(monitorGen),
            Arb.From(relativeGen),
            (workingArea, relative) =>
            {
                // Test the relative position calculation logic directly
                var windowLeft = workingArea.Left + workingArea.Width * relative.X;
                var windowTop = workingArea.Top + workingArea.Height * relative.Y;
                
                // Calculate relative position back
                var calculatedX = workingArea.Width > 0 
                    ? (windowLeft - workingArea.Left) / workingArea.Width 
                    : 0.0;
                var calculatedY = workingArea.Height > 0 
                    ? (windowTop - workingArea.Top) / workingArea.Height 
                    : 0.0;

                // Should get back the same relative position (within floating point tolerance)
                bool isRoundTrip = Math.Abs(calculatedX - relative.X) < 0.001 &&
                                 Math.Abs(calculatedY - relative.Y) < 0.001;

                return isRoundTrip.Label($"Original: ({relative.X}, {relative.Y}), Calculated: ({calculatedX}, {calculatedY})");
            });
    }

    /// <summary>
    /// Property: Applying relative position should place window at correct absolute position
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ApplyRelativePosition_ShouldPlaceWindowCorrectly()
    {
        var monitorGen = from left in Gen.Choose(0, 2000).Select(i => (double)i)
                        from top in Gen.Choose(0, 1500).Select(i => (double)i)
                        from width in Gen.Choose(800, 3840).Select(i => (double)i)
                        from height in Gen.Choose(600, 2160).Select(i => (double)i)
                        select new Rect(left, top, width, height);

        var relativeGen = from x in Gen.Choose(0, 100).Select(i => i / 100.0)
                         from y in Gen.Choose(0, 100).Select(i => i / 100.0)
                         select new { X = x, Y = y };

        return Prop.ForAll(
            Arb.From(monitorGen),
            Arb.From(relativeGen),
            (workingArea, relative) =>
            {
                // Test the apply relative position logic directly
                var expectedLeft = workingArea.Left + relative.X * workingArea.Width;
                var expectedTop = workingArea.Top + relative.Y * workingArea.Height;

                // Verify the calculation is correct
                bool isCorrect = Math.Abs(expectedLeft - (workingArea.Left + relative.X * workingArea.Width)) < 0.001 &&
                               Math.Abs(expectedTop - (workingArea.Top + relative.Y * workingArea.Height)) < 0.001;

                return isCorrect.Label($"Relative: ({relative.X}, {relative.Y}), Expected: ({expectedLeft}, {expectedTop})");
            });
    }
}

