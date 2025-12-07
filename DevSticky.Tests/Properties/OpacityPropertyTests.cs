using DevSticky.Helpers;
using FsCheck;
using FsCheck.Xunit;

namespace DevSticky.Tests.Properties;

/// <summary>
/// Property-based tests for Opacity clamping
/// **Feature: devsticky, Property 4: Opacity Value Clamping**
/// **Validates: Requirements 3.1, 3.2, 3.4**
/// </summary>
public class OpacityPropertyTests
{
    /// <summary>
    /// Property 4: Opacity Value Clamping
    /// For any opacity value V (including values outside valid range), 
    /// the clamped opacity SHALL be in range [0.2, 1.0].
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Opacity_Clamp_ShouldAlwaysBeInValidRange()
    {
        return Prop.ForAll(
            Arb.From(Gen.Choose(-100, 200).Select(x => x / 100.0)),
            value =>
            {
                var clamped = OpacityHelper.Clamp(value);
                return clamped >= OpacityHelper.MinOpacity && 
                       clamped <= OpacityHelper.MaxOpacity;
            });
    }

    [Property(MaxTest = 100)]
    public Property Opacity_BelowMinimum_ShouldClampToMinimum()
    {
        return Prop.ForAll(
            Arb.From(Gen.Choose(1, 100)),
            offset =>
            {
                var value = OpacityHelper.MinOpacity - (offset / 100.0);
                var clamped = OpacityHelper.Clamp(value);
                return clamped == OpacityHelper.MinOpacity;
            });
    }

    [Property(MaxTest = 100)]
    public Property Opacity_AboveMaximum_ShouldClampToMaximum()
    {
        return Prop.ForAll(
            Arb.From(Gen.Choose(1, 100)),
            offset =>
            {
                var value = OpacityHelper.MaxOpacity + (offset / 100.0);
                var clamped = OpacityHelper.Clamp(value);
                return clamped == OpacityHelper.MaxOpacity;
            });
    }

    [Property(MaxTest = 100)]
    public Property Opacity_ValidValue_ShouldBePreserved()
    {
        // Generate values in valid range [0.2, 1.0]
        return Prop.ForAll(
            Arb.From(Gen.Choose(20, 100).Select(x => x / 100.0)),
            value =>
            {
                var clamped = OpacityHelper.Clamp(value);
                return Math.Abs(clamped - value) < 0.001;
            });
    }
}
