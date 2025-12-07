# DevSticky

A lightweight sticky notes application for developers with syntax highlighting support.

## Features

- Always on Top - Keep notes visible while coding
- Syntax Highlighting - C#, JavaScript, JSON, SQL, XML, and more
- Quick Format - Auto-format JSON/XML with Ctrl+Shift+F
- Opacity Control - Adjustable transparency (20%-100%)
- Multi-Note Support - Create and manage multiple notes
- System Tray - Runs quietly in background
- Dark/Light Theme - Follows system or manual selection
- Multi-Language - English and Vietnamese
- Auto-Save - Never lose your notes

## Downloads

| Version | Size | Requirements |
|---------|------|--------------|
| Portable | ~2.5 MB | .NET 8 Desktop Runtime |
| Standalone | ~165 MB | None |

Download from [Releases](https://github.com/hoanghonghuy/QuickNoteSticky/releases).

## System Requirements

- Windows 10/11 (x64)
- .NET 8 Desktop Runtime (portable version only)

## Build

```bash
# Framework-dependent (small)
dotnet publish -c Release -p:SelfContained=false

# Self-contained (large, no runtime needed)
dotnet publish -c Release -p:SelfContained=true
```

## Tech Stack

- .NET 8 / WPF
- AvalonEdit
- MVVM Architecture

## License

MIT
