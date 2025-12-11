# Changelog

All notable changes to DevSticky will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [2.0.2] - 2025-12-12

### Fixed
- Fixed STA thread and WPF threading issues
- Improved localization handling

## [2.0.1] - 2025-12-09

### Fixed
- Fixed i18n: Implement dynamic language switching for Dashboard and persistent windows
  - Dashboard now updates all localized text when language changes in Settings
  - SettingsWindow and NoteWindow also refresh their UI text on language change
  - Persistent windows maintain event subscriptions while hidden, only cleanup on app exit
  - XAML bindings refresh via PropertyChanged with "Item[]" for indexer updates
  - Improved LocalizationExtension with UpdateSourceTrigger.PropertyChanged

## [2.0.0] - 2025-12-07

### Added

#### Global Hotkey System
- System-wide keyboard shortcuts that work from any application
- `Ctrl+Shift+N` - Create new note
- `Ctrl+Shift+D` - Toggle visibility of all notes
- `Ctrl+Shift+Q` - Quick capture with clipboard content
- `Ctrl+Shift+I` - Open snippet browser
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
- Placeholder syntax support (`${1:variableName}`) with tab navigation
- Snippet browser with search and category filtering
- Tree view organization by category
- Import/Export snippets as JSON with conflict resolution
- Keyboard shortcuts: `Ctrl+Shift+S` (save selection), `Ctrl+Shift+I` (browse)

#### Markdown Preview
- Real-time split-view preview for Markdown notes
- Support for standard Markdown syntax (headers, lists, code blocks, links, images, tables, blockquotes)
- Syntax highlighting in code blocks with language specification
- Internal note link support (`[[note-id]]`)
- External link handling (opens in default browser)
- Relative image path resolution
- Export options: HTML, PDF, plain Markdown
- 300ms debounced preview updates

#### Note Templates
- Template selection dialog when creating new notes
- Built-in templates:
  - Meeting Notes (date, attendees, agenda, action items)
  - Code Review (file, reviewer, comments, approval status)
  - Bug Report (title, steps to reproduce, expected/actual behavior, environment)
  - Daily Standup (yesterday, today, blockers)
  - TODO List (checkbox list with priority markers)
- Custom template creation with placeholder support
- Placeholder types: Text, Date, DateTime, User, Custom
- Auto-replacement of date/time and user placeholders
- "Save as Template" option for existing notes
- Import/Export templates as JSON

#### Note Linking
- Create links between notes with `[[note-title]]` syntax
- Autocomplete dropdown when typing `[[`
- Link format: `[[note-id|display-text]]`
- Click to navigate to linked note
- Hover tooltip preview (title + first 100 characters)
- Backlinks panel showing all notes linking to current note
- Graph view visualization of note connections
- Zoom, pan, and click-to-open in graph view
- Broken link detection when notes are deleted
- Automatic display text update when note title changes

#### Cloud Synchronization
- Sync notes across devices via OneDrive or Google Drive
- OAuth 2.0 authentication with secure token storage
- AES-256 encryption with user-provided passphrase
- Automatic sync within 5 seconds of note changes
- Conflict detection and resolution options:
  - Keep local version
  - Keep remote version
  - Merge with conflict markers
- Exponential backoff retry on network failures
- Configurable sync interval
- Sync status indicator in system tray and dashboard
- Manual sync button

### Changed
- Dashboard footer now includes quick access buttons for Snippet Library, Template Management, and Graph View
- Dashboard displays cloud sync status when connected
- New Note flow now shows template selection dialog

### Technical
- Added FsCheck for property-based testing
- Added Markdig for Markdown rendering
- Added Microsoft.Web.WebView2 for preview rendering
- Added Microsoft.Graph SDK for OneDrive integration
- Added Google.Apis.Drive.v3 for Google Drive integration
- Comprehensive property-based test coverage for all new features

## [1.0.0] - 2025-12-07

### Added
- Initial release of DevSticky
- Always on Top functionality - keep notes visible while coding
- Syntax highlighting support (C#, JavaScript, JSON, SQL, XML, and more)
- Quick Format - auto-format JSON/XML with `Ctrl+Shift+F`
- Opacity control - adjustable transparency (20%-100%)
- Multi-note support - create and manage multiple notes
- System tray integration - runs quietly in background
- Dark/Light theme - follows system or manual selection
- Multi-language support - English & Vietnamese
- Auto-save with debounce - never lose your notes
- Dashboard window for note management
- Tag and group organization for notes
- Search functionality across all notes

### Technical
- Built with .NET 8 / WPF
- AvalonEdit for code editing
- MVVM Architecture with Dependency Injection
- JSON-based local storage

[Unreleased]: https://github.com/hoanghonghuy/QuickNoteSticky/compare/v2.0.2...HEAD
[2.0.2]: https://github.com/hoanghonghuy/QuickNoteSticky/compare/v2.0.1...v2.0.2
[2.0.1]: https://github.com/hoanghonghuy/QuickNoteSticky/compare/v2.0.0...v2.0.1
[2.0.0]: https://github.com/hoanghonghuy/QuickNoteSticky/compare/v1.0.0...v2.0.0
[1.0.0]: https://github.com/hoanghonghuy/QuickNoteSticky/releases/tag/v1.0.0
