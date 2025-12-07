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

#### Multi-Monitor Support
- Notes remember which monitor they belong to
- Automatic restoration to correct monitor on startup
- "Move to Monitor" context menu option
- Fallback to primary monitor if assigned monitor unavailable

#### Code Snippet Library
- Save and organize reusable code snippets
- Placeholder support with tab navigation (${1:name})
- Search across name, description, content, and tags
- Import/Export snippets as JSON
- Keyboard shortcuts: Ctrl+Shift+S (save), Ctrl+Shift+I (browse)

#### Markdown Preview
- Real-time split-view preview for Markdown notes
- Syntax highlighting in code blocks
- Support for tables, task lists, and more
- Export as HTML, PDF, or plain Markdown
- Internal note link support

#### Note Templates
- Create notes from predefined templates
- Built-in templates: Meeting Notes, Code Review, Bug Report, Daily Standup, TODO List
- Custom template creation with placeholders
- Date/time and user placeholder auto-replacement
- Import/Export templates

#### Note Linking
- Create links between notes with [[note-title]] syntax
- Autocomplete dropdown when typing [[
- Backlinks panel showing all notes linking to current note
- Graph view visualization of note connections
- Broken link detection when notes are deleted

#### Cloud Synchronization
- Sync notes across devices via OneDrive or Google Drive
- AES-256 encryption with user passphrase
- Automatic conflict detection and resolution
- Configurable sync interval
- Offline support with sync queue

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

## Tech Stack

- .NET 8 / WPF
- AvalonEdit (code editing)
- Markdig (Markdown rendering)
- Microsoft.Web.WebView2 (preview)
- MVVM Architecture

## License

MIT
