# DevSticky Safe Mode Guide

Safe Mode is a special startup mode that helps you recover from configuration issues and startup problems. This guide explains how to use Safe Mode effectively.

## Table of Contents

- [What is Safe Mode?](#what-is-safe-mode)
- [When Safe Mode Activates](#when-safe-mode-activates)
- [Starting Safe Mode](#starting-safe-mode)
- [Safe Mode Interface](#safe-mode-interface)
- [Recovery Options](#recovery-options)
- [Troubleshooting in Safe Mode](#troubleshooting-in-safe-mode)
- [Exiting Safe Mode](#exiting-safe-mode)

## What is Safe Mode?

Safe Mode is a minimal startup configuration that:
- Loads only essential services and features
- Uses built-in default settings instead of user configuration
- Provides tools to diagnose and fix configuration problems
- Allows access to notes even when normal startup fails

### Key Differences from Normal Mode

| Feature | Normal Mode | Safe Mode |
|---------|-------------|-----------|
| Services | All services loaded | Essential services only |
| Configuration | User settings | Built-in defaults |
| Cloud Sync | Enabled if configured | Disabled |
| Themes | User-selected theme | Default theme |
| Hotkeys | Custom hotkeys | Default hotkeys only |
| Plugins | All enabled plugins | Core functionality only |
| Cache | Full caching system | Minimal caching |
| Performance | Optimized for speed | Optimized for stability |

## When Safe Mode Activates

### Automatic Activation
Safe Mode automatically activates when:
- Normal startup fails due to configuration errors
- Critical services fail to initialize
- Repeated crashes are detected (3+ crashes in 5 minutes)
- Corrupted configuration files are detected
- Required dependencies are missing or invalid

### Manual Activation
You can manually start Safe Mode:
- From the crash recovery dialog (after a crash)
- Using command line: `DevSticky.exe --safe-mode`
- From Windows Run dialog: `DevSticky --safe-mode`
- Through the system tray context menu (if available)

## Starting Safe Mode

### After a Crash
1. When DevSticky crashes, a recovery dialog appears
2. Click "Start in Safe Mode" button
3. Safe Mode launches with recovery options
4. Follow the on-screen guidance to fix issues

### Manual Start
1. Close DevSticky completely (check system tray)
2. Open Command Prompt or PowerShell
3. Navigate to DevSticky installation folder
4. Run: `DevSticky.exe --safe-mode`
5. Safe Mode interface appears with diagnostic information

### From Windows Run
1. Press `Win + R` to open Run dialog
2. Type: `DevSticky --safe-mode`
3. Press Enter to start Safe Mode

## Safe Mode Interface

### Main Window
The Safe Mode interface includes:
- **Status Bar**: Shows "Safe Mode" indicator with yellow background
- **Recovery Panel**: Lists detected issues and available fixes
- **Diagnostic Information**: System status and configuration details
- **Action Buttons**: Options to fix problems or exit Safe Mode

### Recovery Panel
Displays:
- **Detected Issues**: Problems found during startup validation
- **Suggested Actions**: Recommended fixes for each issue
- **Risk Assessment**: Impact level of each suggested action
- **Progress Indicators**: Status of ongoing recovery operations

### Diagnostic Information
Shows:
- **System Status**: Overall health of DevSticky installation
- **Configuration Status**: Validity of configuration files
- **Service Status**: Which services are running/failed
- **Performance Metrics**: Startup time, memory usage, etc.

## Recovery Options

### Configuration Recovery

#### Reset to Defaults
- **What it does**: Replaces all user configuration with built-in defaults
- **When to use**: When configuration is severely corrupted
- **Data preserved**: Notes content, but settings are lost
- **Backup created**: Yes, original config saved to `backups\` folder

#### Selective Reset
- **What it does**: Resets only specific configuration sections
- **Options available**:
  - Theme settings
  - Hotkey configuration
  - Cloud sync settings
  - Window positions
  - Performance settings
- **When to use**: When only certain features are problematic

#### Restore from Backup
- **What it does**: Restores configuration from automatic backups
- **Available backups**: Last 10 automatic backups shown
- **Backup information**: Date, time, and configuration version
- **When to use**: When recent changes caused problems

### Service Recovery

#### Restart Failed Services
- **What it does**: Attempts to restart services that failed during startup
- **Success rate**: High for temporary failures, low for configuration issues
- **When to use**: When specific services show as "Failed" in diagnostics

#### Use Fallback Services
- **What it does**: Switches to backup implementations of failed services
- **Functionality**: Reduced but stable functionality
- **When to use**: When primary services consistently fail

#### Disable Problematic Services
- **What it does**: Temporarily disables services causing startup failures
- **Impact**: Some features may be unavailable
- **When to use**: To isolate which service is causing problems

### Data Recovery

#### Rebuild Note Index
- **What it does**: Recreates the note index from individual note files
- **When to use**: When notes don't appear in the dashboard
- **Time required**: 1-30 seconds depending on note count
- **Data safety**: Very safe, only rebuilds index

#### Repair Note Files
- **What it does**: Attempts to fix corrupted note files
- **Success rate**: High for minor corruption, low for severe damage
- **Backup created**: Yes, original files backed up before repair
- **When to use**: When specific notes won't open or display incorrectly

#### Clear Cache and Temporary Files
- **What it does**: Removes all cached data and temporary files
- **Impact**: Slower performance until cache rebuilds
- **When to use**: When cache corruption is suspected
- **Data safety**: Very safe, only removes temporary data

## Troubleshooting in Safe Mode

### Diagnostic Tools

#### System Information
View detailed information about:
- Operating system version and architecture
- .NET Framework version and installation
- Available memory and disk space
- DevSticky version and installation path
- Last startup time and duration

#### Configuration Analysis
Analyze configuration files for:
- JSON syntax errors
- Missing required properties
- Invalid values or ranges
- Circular dependencies
- Version compatibility issues

#### Service Status Check
Monitor service health:
- Service initialization status
- Dependency resolution results
- Error messages and stack traces
- Performance metrics
- Resource usage

### Step-by-Step Troubleshooting

#### Step 1: Identify the Problem
1. Review the "Detected Issues" list in the Recovery Panel
2. Check diagnostic information for error details
3. Note which services failed to start
4. Look for patterns in error messages

#### Step 2: Try Quick Fixes
1. Click "Apply Suggested Fixes" for automatic resolution
2. Use "Restart Failed Services" for temporary failures
3. Try "Clear Cache" for performance-related issues
4. Use "Rebuild Index" for note display problems

#### Step 3: Progressive Recovery
If quick fixes don't work:
1. Try "Selective Reset" for specific problem areas
2. Use "Restore from Backup" if recent changes caused issues
3. Consider "Reset to Defaults" as a last resort
4. Export diagnostic information for support if needed

#### Step 4: Verify Recovery
After applying fixes:
1. Check that "Detected Issues" list is empty or reduced
2. Verify all services show "Running" status
3. Test basic functionality (create/edit notes)
4. Exit Safe Mode and test normal startup

### Common Issues and Solutions

#### "Configuration file is corrupted"
- **Solution**: Use "Reset to Defaults" or "Restore from Backup"
- **Prevention**: Regular automatic backups are created

#### "Service failed to initialize"
- **Solution**: Try "Restart Failed Services" then "Use Fallback Services"
- **Investigation**: Check diagnostic details for specific error

#### "Notes not appearing in dashboard"
- **Solution**: Use "Rebuild Note Index"
- **Cause**: Usually index corruption or incomplete shutdown

#### "Cloud sync authentication failed"
- **Solution**: Use "Selective Reset" for cloud sync settings
- **Follow-up**: Reconfigure cloud sync after exiting Safe Mode

#### "Theme or UI issues"
- **Solution**: Use "Selective Reset" for theme settings
- **Alternative**: "Reset to Defaults" if theme corruption is severe

## Exiting Safe Mode

### Normal Exit
When issues are resolved:
1. Click "Exit Safe Mode and Restart Normally"
2. DevSticky closes and restarts in normal mode
3. Verify all features work correctly
4. Monitor for any recurring issues

### Continue in Safe Mode
If you need to keep working:
1. Click "Continue in Safe Mode"
2. Safe Mode remains active with limited functionality
3. You can still create, edit, and save notes
4. Exit and restart normally when convenient

### Force Normal Mode
If you want to try normal mode despite warnings:
1. Click "Force Normal Startup"
2. DevSticky attempts normal startup even with detected issues
3. May result in crashes or instability
4. Use only when you understand the risks

### Exit Without Changes
To exit without applying any fixes:
1. Click "Exit Without Changes"
2. DevSticky closes without modifying configuration
3. Next startup will likely encounter the same issues
4. Use when you want to investigate problems manually

## Best Practices

### Prevention
- **Regular Backups**: Enable automatic configuration backups
- **Gradual Changes**: Make one configuration change at a time
- **Monitor Performance**: Watch for signs of degradation
- **Keep Updated**: Install DevSticky updates promptly

### When Using Safe Mode
- **Read Carefully**: Review all suggested actions before applying
- **Start Small**: Try least invasive fixes first
- **Backup First**: Ensure backups exist before major changes
- **Document Issues**: Note what caused the problem for future reference

### After Recovery
- **Test Thoroughly**: Verify all features work correctly
- **Monitor Stability**: Watch for recurring issues over several days
- **Update Configuration**: Reconfigure any reset settings
- **Report Bugs**: Submit bug reports for reproducible issues

## Advanced Safe Mode Features

### Command Line Options
```bash
# Start Safe Mode with verbose logging
DevSticky.exe --safe-mode --verbose

# Safe Mode with specific diagnostics
DevSticky.exe --safe-mode --diagnose-services

# Safe Mode without automatic fixes
DevSticky.exe --safe-mode --no-auto-fix

# Safe Mode with configuration export
DevSticky.exe --safe-mode --export-config
```

### Environment Variables
Set these for advanced Safe Mode behavior:
```bash
# Force Safe Mode startup
set DEVSTICKY_FORCE_SAFE_MODE=1

# Enable detailed diagnostic logging
set DEVSTICKY_SAFE_MODE_VERBOSE=1

# Disable automatic recovery attempts
set DEVSTICKY_SAFE_MODE_NO_AUTO_RECOVERY=1
```

### Registry Settings
Advanced users can configure Safe Mode behavior:
- `HKEY_CURRENT_USER\Software\DevSticky\SafeMode\AutoRecovery` - Enable/disable automatic fixes
- `HKEY_CURRENT_USER\Software\DevSticky\SafeMode\VerboseLogging` - Control logging detail
- `HKEY_CURRENT_USER\Software\DevSticky\SafeMode\BackupCount` - Number of backups to keep

Safe Mode is designed to help you recover from almost any configuration or startup issue. When in doubt, start with the least invasive options and work your way up to more comprehensive fixes. Most issues can be resolved quickly and safely using the built-in recovery tools.