# DevSticky Troubleshooting Guide

This guide helps you resolve common issues with DevSticky and understand the crash handling features.

## Table of Contents

- [Application Won't Start](#application-wont-start)
- [Crash Detection & Recovery](#crash-detection--recovery)
- [Safe Mode](#safe-mode)
- [Configuration Issues](#configuration-issues)
- [Performance Problems](#performance-problems)
- [Cloud Sync Issues](#cloud-sync-issues)
- [Getting Help](#getting-help)

## Application Won't Start

### Symptoms
- DevSticky crashes immediately on startup
- Application window never appears
- Error dialogs appear during startup

### Automatic Recovery
DevSticky includes automatic crash detection and recovery:

1. **Crash Analysis**: The system automatically analyzes Windows Event Logs to identify crash causes
2. **Recovery Attempts**: Common issues are fixed automatically (missing files, corrupted config)
3. **Safe Mode Prompt**: If normal startup fails, you'll be offered Safe Mode
4. **Diagnostic Information**: Detailed crash reports are generated for troubleshooting

### Manual Steps

#### Step 1: Check Event Logs
1. Press `Win + R`, type `eventvwr.msc`, press Enter
2. Navigate to `Windows Logs > Application`
3. Look for entries with Source "DevSticky" or ".NET Runtime"
4. Note any error messages or exception details

#### Step 2: Check Crash Reports
1. Navigate to `%APPDATA%\DevSticky\Logs\`
2. Look for recent crash report files (`.crash` extension)
3. Open the most recent file to see detailed error information

#### Step 3: Try Safe Mode
1. If prompted, click "Start in Safe Mode" after a crash
2. Or manually start with command line: `DevSticky.exe --safe-mode`
3. Safe Mode uses minimal services and default configuration

#### Step 4: Reset Configuration
If Safe Mode works but normal mode doesn't:
1. In Safe Mode, click "Reset Configuration to Defaults"
2. Or manually delete `%APPDATA%\DevSticky\config.json`
3. Restart normally - configuration will be recreated

## Crash Detection & Recovery

### How It Works

DevSticky monitors its own health through several mechanisms:

#### Startup Validation
- **Directory Check**: Verifies all required directories exist
- **Configuration Validation**: Ensures config files are valid JSON
- **Dependency Verification**: Checks all required DLLs are available
- **Service Registration**: Validates dependency injection container
- **Resource Loading**: Confirms themes and resources are accessible

#### Exception Handling
- **Comprehensive Logging**: All exceptions are logged with full context
- **Dual Logging**: Writes to both file logs and Windows Event Log
- **Stack Trace Capture**: Complete call stack information preserved
- **Component Identification**: Identifies which component failed

#### Recovery Actions
- **Missing Files**: Creates default configuration files automatically
- **Corrupted Config**: Backs up corrupted files and creates new defaults
- **Missing Directories**: Recreates required directory structure
- **Service Failures**: Falls back to alternative implementations
- **Resource Issues**: Uses embedded default resources

### Diagnostic Information

#### Startup Diagnostics
When diagnostic mode is enabled, DevSticky logs:
- Each startup step with timestamps
- Service registration details
- Configuration loading progress
- Memory usage and performance metrics
- Any validation warnings or errors

#### Performance Monitoring
- **Startup Time**: Total time from launch to ready state
- **Memory Usage**: RAM consumption during startup
- **Validation Overhead**: Time spent on safety checks
- **Recovery Actions**: Any automatic fixes applied

## Safe Mode

### When Safe Mode Activates
- Normal startup fails due to configuration issues
- Service initialization encounters critical errors
- User manually requests safe mode startup
- Automatic activation after repeated crashes

### Safe Mode Features
- **Minimal Services**: Only essential functionality enabled
- **Default Settings**: Uses built-in configuration instead of user settings
- **Recovery Tools**: Options to fix common configuration problems
- **Clear Indicators**: UI shows safe mode status and available actions

### Safe Mode Options

#### Configuration Reset
- **Reset to Defaults**: Removes all user configuration, uses built-in defaults
- **Backup Current**: Saves current config before reset (for manual recovery)
- **Selective Reset**: Choose which settings to reset (themes, hotkeys, etc.)

#### Service Management
- **Disable Cloud Sync**: Temporarily disable cloud synchronization
- **Reset Cache**: Clear all cached data and rebuild
- **Rebuild Index**: Recreate search index from scratch

#### Diagnostic Export
- **Export Logs**: Package all log files for support analysis
- **System Information**: Collect system details for troubleshooting
- **Configuration Dump**: Export current settings for analysis

### Exiting Safe Mode
1. **Fix and Restart**: Apply fixes and restart normally
2. **Continue in Safe Mode**: Keep using limited functionality
3. **Reset and Restart**: Reset configuration and restart normally

## Configuration Issues

### Common Problems

#### Invalid JSON Configuration
**Symptoms**: Startup fails with JSON parsing errors
**Solution**: 
1. Check `%APPDATA%\DevSticky\config.json` for syntax errors
2. Use online JSON validator to check file format
3. Delete file to regenerate defaults, or restore from backup

#### Missing Configuration Files
**Symptoms**: Application creates new default settings
**Solution**: 
1. Check if files exist in `%APPDATA%\DevSticky\`
2. Restore from backup if available
3. Reconfigure settings as needed

#### Corrupted Settings
**Symptoms**: Settings don't persist or cause crashes
**Solution**:
1. Safe Mode will automatically backup and replace corrupted files
2. Manually delete `%APPDATA%\DevSticky\settings\` folder
3. Restart application to regenerate defaults

### Configuration File Locations

```
%APPDATA%\DevSticky\
├── config.json              # Main application configuration
├── notes\                   # Note storage directory
│   ├── *.json              # Individual note files
│   └── index.json          # Note index and metadata
├── settings\               # Additional settings
│   ├── hotkeys.json        # Global hotkey configuration
│   ├── themes.json         # Theme settings
│   └── cloud-sync.json     # Cloud synchronization settings
├── cache\                  # Temporary cache files
├── logs\                   # Application and crash logs
└── backups\               # Automatic configuration backups
```

## Performance Problems

### Slow Startup
**Symptoms**: Application takes long time to start
**Causes & Solutions**:
1. **Large number of notes**: Enable note indexing, consider archiving old notes
2. **Cloud sync delays**: Disable cloud sync temporarily, check network connection
3. **Antivirus scanning**: Add DevSticky folder to antivirus exclusions
4. **Disk performance**: Check available disk space, run disk cleanup

### High Memory Usage
**Symptoms**: DevSticky uses excessive RAM
**Solutions**:
1. **Clear cache**: Use Safe Mode to clear all cached data
2. **Reduce note count**: Archive or delete unused notes
3. **Disable features**: Turn off markdown preview, cloud sync, or other features
4. **Restart application**: Memory usage resets on restart

### UI Responsiveness
**Symptoms**: Interface feels slow or unresponsive
**Solutions**:
1. **Reduce opacity effects**: Set notes to 100% opacity
2. **Disable animations**: Turn off UI animations in settings
3. **Close unused notes**: Keep only necessary notes open
4. **Update graphics drivers**: Ensure latest GPU drivers installed

## Cloud Sync Issues

### Connection Problems
**Symptoms**: Cannot connect to OneDrive or Google Drive
**Solutions**:
1. **Check internet connection**: Verify network connectivity
2. **Reauthorize account**: Disconnect and reconnect cloud provider
3. **Check firewall**: Ensure DevSticky can access internet
4. **Update credentials**: Sign out and sign back in to cloud account

### Sync Conflicts
**Symptoms**: Notes show conflict markers or duplicate content
**Solutions**:
1. **Choose resolution**: Select "Keep Local", "Keep Remote", or "Merge"
2. **Manual merge**: Edit conflicted notes to combine changes
3. **Disable sync temporarily**: Resolve conflicts offline, then re-enable

### Encryption Issues
**Symptoms**: Cannot decrypt synced notes
**Solutions**:
1. **Verify passphrase**: Ensure encryption passphrase is correct
2. **Reset encryption**: Disable sync, delete cloud data, re-enable with new passphrase
3. **Backup local notes**: Export notes before resetting encryption

## Getting Help

### Before Contacting Support

1. **Check this guide**: Review relevant troubleshooting sections
2. **Try Safe Mode**: Use Safe Mode to isolate configuration issues
3. **Collect information**: Gather crash logs, system info, and error messages
4. **Note reproduction steps**: Document exactly what you were doing when the issue occurred

### Information to Include

When reporting issues, please provide:

#### System Information
- Windows version (e.g., Windows 11 22H2)
- .NET version (check in Control Panel > Programs)
- DevSticky version (Help > About)
- Available RAM and disk space

#### Error Details
- Exact error messages
- When the error occurs (startup, during use, shutdown)
- Steps to reproduce the problem
- Screenshots of error dialogs

#### Log Files
Located in `%APPDATA%\DevSticky\Logs\`:
- `application.log` - General application logs
- `crash-*.log` - Crash reports with stack traces
- `startup-diagnostics.log` - Detailed startup information
- `performance.log` - Performance metrics and timing

#### Configuration Files
If relevant to the issue:
- `config.json` - Main configuration
- `settings\*.json` - Specific setting files
- Note: Remove sensitive information before sharing

### Support Channels

1. **GitHub Issues**: [Create an issue](https://github.com/hoanghonghuy/QuickNoteSticky/issues) for bugs and feature requests
2. **Documentation**: Check [API Documentation](API_DOCUMENTATION.md) for developer information
3. **Community**: Search existing issues for similar problems and solutions

### Emergency Recovery

If DevSticky is completely unusable:

1. **Backup data**: Copy entire `%APPDATA%\DevSticky\` folder to safe location
2. **Clean install**: 
   - Uninstall DevSticky
   - Delete `%APPDATA%\DevSticky\` folder
   - Reinstall DevSticky
   - Restore notes from backup if needed
3. **Portable mode**: Use portable version to avoid configuration conflicts

## Advanced Troubleshooting

### Command Line Options

```bash
# Start in safe mode
DevSticky.exe --safe-mode

# Enable verbose logging
DevSticky.exe --verbose

# Reset configuration on startup
DevSticky.exe --reset-config

# Disable cloud sync
DevSticky.exe --no-cloud-sync

# Run startup diagnostics
DevSticky.exe --diagnose
```

### Registry Settings

DevSticky stores minimal information in the Windows Registry:
- `HKEY_CURRENT_USER\Software\DevSticky\` - Basic application settings
- To reset: Delete the entire DevSticky registry key

### Environment Variables

Set these environment variables for advanced debugging:
- `DEVSTICKY_LOG_LEVEL=Debug` - Enable debug logging
- `DEVSTICKY_SAFE_MODE=1` - Force safe mode startup
- `DEVSTICKY_NO_CACHE=1` - Disable all caching

This troubleshooting guide should help you resolve most issues with DevSticky. If you continue to experience problems, please don't hesitate to reach out for support with the information outlined above.