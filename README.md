<p align="center">
  <img src="AutoDrop/Assets/logo.png" alt="AutoDrop Logo" width="128" height="128">
</p>

<h1 align="center">AutoDrop</h1>

<p align="center">
  <strong>Smart File Organizer for Windows</strong><br>
  Drag. Drop. Organize. Done.
</p>

<p align="center">
  <a href="https://github.com/dahbimoad/AutoDrop/releases/latest">
    <img src="https://img.shields.io/github/v/release/dahbimoad/AutoDrop?style=flat-square&color=blue" alt="Latest Release">
  </a>
  <a href="https://github.com/dahbimoad/AutoDrop/releases">
    <img src="https://img.shields.io/github/downloads/dahbimoad/AutoDrop/total?style=flat-square&color=green" alt="Downloads">
  </a>
  <a href="LICENSE">
    <img src="https://img.shields.io/github/license/dahbimoad/AutoDrop?style=flat-square" alt="License">
  </a>
</p>

---

## ✨ What is AutoDrop?

AutoDrop is a lightweight Windows utility that makes file organization effortless. Instead of manually navigating folders, simply drag files onto the floating drop zone and choose from smart destination suggestions.

**Stop wasting time organizing files. Let AutoDrop do the thinking.**

---

## 🎯 Features

- **🎯 Floating Drop Zone** — Always-on-top window ready to receive your files
- **🧠 Smart Suggestions** — Automatic destination recommendations based on file type
- **📁 Custom Rules** — Create your own rules for specific file types or patterns
- **⚡ One-Click Move** — Select a destination and files move instantly
- **↩️ Undo Support** — Made a mistake? Undo the last move with one click
- **📦 Batch Operations** — Drop multiple files and organize them all at once
- **🔔 System Tray** — Runs quietly in the background, always accessible
- **🎨 Modern UI** — Clean Windows 11-style Fluent Design interface
- **🤖 AI-Powered Analysis** — Content-based file categorization and smart renaming
- **🔐 Privacy-First AI** — Choose from cloud providers or run 100% locally with built-in Local AI

---

## 📥 Installation

### Recommended: Installer
Download the latest installer from the [Releases](https://github.com/dahbimoad/AutoDrop/releases/latest) page:

| Platform | Download |
|----------|----------|
| Windows x64 | `AutoDrop-x.x.x-win-x64-setup.exe` |
| Windows x86 | `AutoDrop-x.x.x-win-x86-setup.exe` |

**Installer includes:**
- ✅ Desktop shortcut
- ✅ Start menu entry  
- ✅ Proper uninstaller (Add/Remove Programs)

### Portable Version
Prefer no installation? Download the portable ZIP, extract anywhere, and run `AutoDrop.exe`.

---

## 🚀 Quick Start

1. **Launch AutoDrop** — A small floating window appears on your screen
2. **Drag files** onto the drop zone
3. **Pick a destination** from the smart suggestions
4. **Done!** — Your files are moved instantly

<p align="center">
  <em>It's that simple.</em>
</p>

---

## 📋 Supported File Types

AutoDrop automatically categorizes files and suggests appropriate destinations:

| Category | Extensions | Default Destination |
|----------|------------|---------------------|
| 🖼️ Images | `.jpg` `.png` `.gif` `.bmp` `.webp` `.svg` | Pictures |
| 📄 Documents | `.pdf` `.docx` `.xlsx` `.pptx` `.txt` | Documents |
| 🎬 Videos | `.mp4` `.avi` `.mkv` `.mov` `.webm` | Videos |
| 🎵 Audio | `.mp3` `.wav` `.flac` `.aac` `.ogg` | Music |
| 📦 Archives | `.zip` `.rar` `.7z` `.tar` `.gz` | Downloads |
| 💻 Code | `.js` `.py` `.cs` `.html` `.css` `.json` | Projects |

*You can customize these rules in Settings.*

---

## 🤖 AI-Powered Features

AutoDrop includes powerful AI capabilities for content-based file organization:

### Supported AI Providers

| Provider | Models | Vision | PDF | Text Prompts | Notes |
|----------|--------|--------|-----|--------------|-------|
| **OpenAI** | GPT-4o, GPT-4o-mini | ✅ | ❌ | ✅ | Best quality |
| **Claude** | Claude 3.5 Sonnet, Haiku, Opus | ✅ | ✅ | ✅ | Best for documents |
| **Gemini** | Gemini 2.5 Flash, 3.0 Flash (Preview) | ✅ | ✅ | ✅ | Latest frontier AI |
| **Groq** | Llama 3.3 70B, 3.2 90B Vision | ✅ | ❌ | ✅ | Ultra-fast inference |
| **Local AI** | ONNX embedding models | ✅ | ❌ | ❌ | 100% offline/private (default) |

> **Note:** Local AI uses embedding models for content classification. It can analyze images and documents but cannot generate text responses (e.g., smart filename suggestions). For AI-powered filename analysis, use a cloud provider.

### AI Capabilities

- **🖼️ Image Analysis** — Analyzes image content (photos, screenshots, receipts) to suggest categories
- **📄 Document Analysis** — Reads PDFs and text files to categorize by content
- **✨ Smart Rename** — Suggests descriptive filenames based on file content
- **📂 Folder Matching** — AI prioritizes your existing custom folders over creating new ones
- **🗂️ Folder Organization** — Organize entire folders using AI content analysis
- **🔐 Secure Storage** — API keys encrypted with Windows DPAPI

### Privacy Options

- **Cloud Providers** — OpenAI, Claude, Gemini, Groq (require API key, data sent to cloud)
- **Local AI (Default)** — Run AI 100% offline using embedded ONNX models, no data leaves your computer

*Configure AI in Settings → AI Settings*

---

## ⚙️ Configuration

Access settings by right-clicking the system tray icon → **Settings**

- **Custom Folders** — Add your own destination folders
- **File Rules** — Create rules based on extension, name pattern, or size
- **Appearance** — Adjust drop zone size and position

---

## 🛠️ Build from Source

### Requirements
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Windows 10/11

### Build
```bash
git clone https://github.com/dahbimoad/AutoDrop.git
cd AutoDrop
dotnet build
```

### Run
```bash
dotnet run --project AutoDrop
```

### Publish
```bash
dotnet publish AutoDrop -c Release -r win-x64 --self-contained
```

---

## 🤝 Contributing

Contributions are welcome! Feel free to:

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

---

## 📄 License

This project is licensed under the MIT License — see the [LICENSE](LICENSE) file for details.

---

## 👤 Author

**Moad Dahbi** — Software Engineer | .NET & DevOps Specialist

- 🌐 Website: [dahbimoad.com](https://dahbimoad.com)
- 💻 GitHub: [@dahbimoad](https://github.com/dahbimoad)
---

<p align="center">
  <sub>Built with ❤️ using WPF and .NET 8</sub>
</p>
