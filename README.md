# DevSticky

A lightweight sticky notes application for developers with syntax highlighting, code snippets, cloud sync, and more.

## Features

### Core Features
- **Always on Top** - Keep notes visible while coding
- **Syntax Highlighting** - C#, JavaScript, JSON, SQL, XML, Python, Markdown, and more
- **Quick Format** - Auto-format JSON/XML with Ctrl+Shift+F
- **Opacity Control** - Adjustable transparency (20%-100%)
- **Multi-Note Support** - Create and manage multiple notes
- **System Tray** - Runs quietly in background
- **Dark/Light Theme** - Follows system or manual selection
- **Multi-Language** - English and Vietnamese
- **Auto-Save** - Never lose your notes

### v2.0 Features

#### Global Hotkeys
- **Ctrl+Shift+N** - Create new note from anywhere
- **Ctrl+Shift+D** - Toggle visibility of all notes
- **Ctrl+Shift+Q** - Quick capture with clipboard content
- **Ctrl+Shift+I** - Open snippet browser
- Configurable hotkeys in Settings
- Hotkey conflict detection with alternative suggestions

#### Multi-Monitor Support
- Notes remember which monitor they belong to
- Automatic restoration to correct monitor on startup
- "Move to Monitor" context menu option
- Fallback to primary monitor when assigned monitor is unavailable
- Monitor change detection while application is running
- Automatic repositioning of notes outside visible screen bounds

#### Code Snippet Library
- Save and organize reusable code snippets
- Snippet metadata: name, description, language, category, tags
- Placeholder syntax support (${1:variableName}) with tab navigation
- Snippet browser with search and category filtering
- Tree view organization by category
- Search across name, description, content, and tags
- Import/Export snippets as JSON with conflict resolution
- Keyboard shortcuts: Ctrl+Shift+S (save), Ctrl+Shift+I (browse)

#### Markdown Preview
- Real-time split-view preview for Markdown notes
- Support for standard Markdown syntax (headers, lists, code blocks, links, images, tables, blockquotes)
- Syntax highlighting in code blocks with language specification
- Internal note link support
- External link handling (opens in default browser)
- Relative image path resolution
- Export options: HTML, PDF, plain Markdown
- 300ms debounced preview updates

#### Note Templates
- Create notes from predefined templates
- Built-in templates: Meeting Notes, Code Review, Bug Report, Daily Standup, TODO List
- Custom template creation with placeholder support
- Placeholder types: Text, Date, DateTime, User, Custom
- Auto-replacement of date/time and user placeholders
- "Save as Template" option for existing notes
- Import/Export templates as JSON

#### Note Linking
- Create links between notes with [[note-title]] syntax
- Autocomplete dropdown when typing [[
- Backlinks panel showing all notes linking to current note
- Graph view visualization of note connections with zoom and pan
- Broken link detection when notes are deleted
- Automatic display text update when note title changes

#### Cloud Synchronization
- Sync notes across devices via OneDrive or Google Drive
- OAuth 2.0 authentication with secure token storage
- AES-256 encryption with user-provided passphrase
- Automatic sync within 5 seconds of note changes
- Conflict detection and resolution:
  - Keep local version
  - Keep remote version
  - Merge with conflict markers
- Exponential backoff retry on network failures
- Configurable sync interval
- Offline support with sync queue
- Sync status indicator in system tray and dashboard

## Keyboard Shortcuts

| Shortcut | Action |
|----------|--------|
| Ctrl+Shift+N | New note (global) |
| Ctrl+Shift+D | Toggle all notes visibility (global) |
| Ctrl+Shift+Q | Quick capture (global) |
| Ctrl+Shift+I | Open snippet browser (global) |
| Ctrl+Shift+F | Format JSON/XML |
| Ctrl+Shift+S | Save selection as snippet |
| Ctrl+F | Find in note |
| Ctrl+S | Save note |
| Ctrl+W | Close note |

## Downloads

| Version | Size | Requirements |
|---------|------|--------------|
| Portable | ~2.5 MB | .NET 8 Desktop Runtime |
| Standalone | ~165 MB | None |

Download from [Releases](https://github.com/hoanghonghuy/QuickNoteSticky/releases).

## System Requirements

- Windows 10/11 (x64)
- .NET 8 Desktop Runtime (portable version only)

## Cloud Sync Setup

### OneDrive
1. Open Settings > Cloud Sync
2. Select "OneDrive" as provider
3. Click "Connect" and sign in with your Microsoft account
4. Set an encryption passphrase (keep it safe!)
5. Notes will sync automatically

### Google Drive
1. Open Settings > Cloud Sync
2. Select "Google Drive" as provider
3. Click "Connect" and sign in with your Google account
4. Set an encryption passphrase (keep it safe!)
5. Notes will sync automatically

**Important:** Your encryption passphrase cannot be recovered. If you forget it, you won't be able to decrypt your synced notes.

## Build

```bash
# Framework-dependent (small)
dotnet publish -c Release -p:SelfContained=false

# Self-contained (large, no runtime needed)
dotnet publish -c Release -p:SelfContained=true
```

## Architecture

DevSticky follows a layered architecture with SOLID principles and dependency injection:

```
┌─────────────────────────────────────────┐
│           Presentation Layer            │
│  (Views, ViewModels, Coordinators)      │
├─────────────────────────────────────────┤
│         Application Services            │
│  (Orchestration, Workflows)             │
├─────────────────────────────────────────┤
│           Domain Services               │
│  (Business Logic, Validation)           │
├─────────────────────────────────────────┤
│         Infrastructure Layer            │
│  (Storage, External APIs, Caching)      │
└─────────────────────────────────────────┘
```

### Key Architectural Improvements

- **SOLID Principles**: Single Responsibility, Open/Closed, Liskov Substitution, Interface Segregation, Dependency Inversion
- **Memory Optimization**: LRU caching, dirty tracking, proper resource disposal
- **Performance**: Optimized LINQ queries, async operations, debounced updates
- **Testability**: Dependency injection, interface abstractions, property-based testing
- **Maintainability**: Centralized error handling, code deduplication, consistent patterns

### Core Services

#### Memory Management
- **LruCache**: Generic LRU cache with size limits and automatic eviction
- **EnhancedCacheService**: Caching layer for tags and groups with statistics
- **DirtyTracker**: Tracks modified entities for optimized saves

#### Business Logic
- **GroupManagementService**: Manages note groups and organization
- **TagManagementService**: Handles note tagging and categorization
- **NoteService**: Core note operations and lifecycle management
- **SearchService**: Full-text search across notes and metadata

#### Infrastructure
- **CloudProviderRegistry**: Extensible cloud provider management
- **ErrorHandler**: Centralized error handling with context and recovery
- **FileSystemAdapter**: Abstracted file system operations for testability
- **DialogService**: Abstracted UI dialogs for better separation of concerns

#### Performance & Optimization
- **OptimizedDebounceService**: Single-timer debouncing for multiple operations
- **MonitorBoundsHelper**: Multi-monitor window positioning utilities
- **JsonSerializerOptionsFactory**: Shared JSON serialization configuration

## Crash Handling & Recovery

DevSticky includes comprehensive crash detection and recovery mechanisms to ensure reliable operation:

### Automatic Crash Detection
- **Event Log Analysis** - Monitors Windows Event Logs for crash patterns
- **Exception Tracking** - Captures detailed crash information with stack traces
- **Component Identification** - Identifies which startup component caused failures
- **Diagnostic Logging** - Comprehensive startup process monitoring

### Recovery Mechanisms
- **Automatic Recovery** - Fixes common issues like missing configuration files
- **Service Fallbacks** - Uses backup implementations when services fail
- **Configuration Reset** - Restores corrupted settings to defaults
- **Directory Recreation** - Rebuilds missing application directories

### Safe Mode
- **Minimal Startup** - Launches with essential services only when normal startup fails
- **Default Configuration** - Uses built-in settings instead of user configuration
- **Recovery Options** - Provides tools to fix configuration issues
- **Clear Indicators** - Shows when running in safe mode with recovery options

### Troubleshooting
If DevSticky fails to start:
1. Check Windows Event Viewer for DevSticky entries
2. Look for crash reports in `%APPDATA%\DevSticky\Logs\`
3. Try starting in Safe Mode (automatic prompt after crash)
4. Use "Reset Configuration" option in Safe Mode if needed
5. Contact support with crash logs if issues persist

## Tech Stack

- .NET 8 / WPF
- AvalonEdit (code editing)
- Markdig (Markdown rendering)
- Microsoft.Web.WebView2 (preview)
- MVVM Architecture with Dependency Injection
- FsCheck (property-based testing)
- BenchmarkDotNet (performance testing)

## Documentation

- **[API Documentation](API_DOCUMENTATION.md)** - Comprehensive service and interface documentation
- **[Architecture Guide](ARCHITECTURE.md)** - Detailed architectural decisions and patterns
- **[Migration Guide](MIGRATION_GUIDE.md)** - Guide for migrating to the new architecture
- **[Troubleshooting Guide](TROUBLESHOOTING.md)** - User guide for resolving common issues
- **[Safe Mode Guide](SAFE_MODE_GUIDE.md)** - Complete guide to using Safe Mode for recovery
- **[Crash Handling Developer Guide](CRASH_HANDLING_DEVELOPER_GUIDE.md)** - Developer documentation for crash handling system
- **[Helper Utilities](DevSticky/Helpers/README.md)** - Documentation for utility classes

## Performance Improvements

The refactored architecture delivers significant performance improvements:

| Metric | Before | After | Improvement |
|--------|--------|-------|-------------|
| Memory Usage (100 notes) | 83MB | <50MB | 40% reduction |
| Save Performance | 200ms | <50ms | 75% faster |
| Code Duplication | 15% | <5% | 67% reduction |
| Test Coverage | 45% | >80% | 78% improvement |
| Cache Hit Rate | N/A | >90% | New feature |

## License

MIT
