using System;
using DevSticky.Models;
using DevSticky.Services;
using Xunit;

namespace DevSticky.Tests;

public class SafeModeDebugTest
{
    [Fact]
    public void TestSafeModeConfigDirectly()
    {
        // Test SafeModeConfig directly
        var config = SafeModeConfig.CreateDefault();
        Assert.False(config.IsEnabled, "SafeModeConfig.CreateDefault() should have IsEnabled = false");
        Assert.Equal(false, config.IsEnabled);
    }

    [Fact]
    public void TestSafeModeControllerWithConfig()
    {
        // Test SafeModeController with explicit config
        var defaultConfig = SafeModeConfig.CreateDefault();
        var controller = new SafeModeController(defaultConfig, null, null);
        
        Assert.False(controller.IsInSafeMode, "Controller should not be in safe mode");
        Assert.False(controller.Configuration.IsEnabled, "Configuration should have IsEnabled = false");
    }
}
