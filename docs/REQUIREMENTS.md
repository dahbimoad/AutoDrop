# AutoDrop — Requirements Document
**Version:** 1.0 MVP (Released)  
**Date:** January 12, 2026  
**Author:** Product Team

---

## 1. Overview

**AutoDrop** is a Windows desktop application that helps users organize files and folders quickly using drag-and-drop. Instead of manually navigating folders, users drop files onto a floating window and choose from smart suggestions.

---

## 2. Problem Statement

Users waste time organizing files:
- Downloading a file → manually opening folder → dragging to destination
- Forgetting where files should go
- Repeating the same actions for same file types

**Solution:** A floating drop zone that suggests destinations and learns user preferences.

---

## 3. Target User

- **Primary:** Personal use (you!)
- **Secondary:** Anyone who downloads many files and wants quick organization
- **Platform:** Windows 10/11

---

## 4. MVP Scope

### What We're Building (Phase 1)

| ID | Feature | Priority |
|----|---------|----------|
| US-01 | Floating Drop Zone | 🔴 Must |
| US-02 | Drag & Drop Files/Folders | 🔴 Must |
| US-03 | Suggest Destinations | 🔴 Must |
| US-04 | One-Click Move | 🔴 Must |
| US-05 | Undo via Notification | 🔴 Must |
| US-06 | Remember My Choice | 🔴 Must |
| US-07 | System Tray | 🟡 Should |

---

## 5. User Stories & Acceptance Criteria

### US-01: Floating Drop Zone

**Story:**  
As a user, I want a small floating window that stays on top of other apps so I can drag files anytime.

**Acceptance Criteria:**
- [x] Window is always-on-top (`Topmost = true`)
- [x] Window is small (~150x150 pixels)
- [x] Window can be dragged to any screen position
- [x] Window has minimal UI: icon + "Drop files here" text
- [x] Window shows visual feedback (color change) when file hovers over it

---

### US-02: Drag & Drop Files/Folders

**Story:**  
As a user, I want to drag and drop files or folders onto the drop zone.

**Acceptance Criteria:**
- [x] Accepts single file
- [x] Accepts multiple files (batch)
- [x] Accepts single folder
- [x] Accepts multiple folders
- [x] Shows file count when multiple items dropped

---

### US-03: Suggest Destinations

**Story:**  
As a user, after dropping a file, I want to see 3-4 suggested destination folders based on file type.

**Acceptance Criteria:**
- [x] Popup appears near drop zone after drop
- [x] Shows file name and detected type (e.g., "Image", "Document")
- [x] Shows 3-4 destination buttons based on extension mapping:

| Extension | Category | Default Destination |
|-----------|----------|---------------------|
| .jpg .png .gif .bmp .webp | Image | Pictures |
| .pdf .docx .xlsx .pptx .txt | Document | Documents |
| .mp4 .avi .mkv .mov | Video | Videos |
| .mp3 .wav .flac | Audio | Music |
| .zip .rar .7z | Archive | Downloads |
| .exe .msi | Installer | Downloads |
| Other | Unknown | Desktop |

- [x] Best match is visually highlighted
- [x] "Browse other folder..." option available
- [x] Popup has X button to cancel

---

### US-04: One-Click Move

**Story:**  
As a user, when I click a destination button, the file should move immediately.

**Acceptance Criteria:**
- [x] File/folder moves to selected destination
- [x] Original file is removed from source
- [x] If file exists at destination → auto-rename to `filename (1).ext`
- [x] If move fails (permissions, file locked) → show error message
- [x] Popup closes after successful move

---

### US-05: Undo via Notification

**Story:**  
As a user, after a move, I want a notification with an Undo button so I can recover from mistakes.

**Acceptance Criteria:**
- [x] Toast notification appears (bottom-right corner)
- [x] Shows: "✓ Moved filename.ext → Pictures"
- [x] Has [Undo] button
- [x] Clicking Undo moves file back to original location
- [x] Notification auto-dismisses after 5 seconds
- [x] Only last operation can be undone

---

### US-06: Remember My Choice

**Story:**  
As a user, I want to check "Always do this for .jpg files" so the app learns my preference.

**Acceptance Criteria:**
- [x] Checkbox in suggestion popup: "Always move .{ext} files here"
- [x] When checked + move confirmed → rule saved to local JSON file
- [x] Next time same extension dropped → auto-move without popup
- [x] Auto-move shows toast notification (not popup)
- [x] Rules stored in: `%AppData%/AutoDrop/rules.json`

**Rule Format:**
```json
{
  "rules": [
    { "extension": ".jpg", "destination": "C:\\Users\\Me\\Pictures" },
    { "extension": ".pdf", "destination": "C:\\Users\\Me\\Documents" }
  ]
}
```

---

### US-07: System Tray

**Story:**  
As a user, I want the app to minimize to system tray so it's always available but not intrusive.

**Acceptance Criteria:**
- [x] Minimize button sends app to system tray (not taskbar)
- [x] Tray icon visible in notification area
- [x] Double-click tray icon → restore drop zone
- [x] Right-click tray icon shows menu:
  - Show Drop Zone
  - Settings (disabled for MVP)
  - Exit

---

## 6. Technical Stack

| Component | Technology |
|-----------|------------|
| Framework | .NET 8 |
| UI | WPF (Windows Presentation Foundation) |
| UI Library | **WPF UI** (Fluent Design - Windows 11 style) |
| Package | `WPF-UI` via NuGet |
| Architecture | MVVM (Model-View-ViewModel) |
| MVVM Toolkit | CommunityToolkit.Mvvm |
| Storage | JSON files (rules, config) |
| Logging | Serilog (file sink with rolling logs) |
| Notifications | WPF UI Snackbar + Windows Toast |
| Installer | Inno Setup 6 (EXE) |

**Why WPF UI?**
- ✅ Modern Windows 11 Fluent Design (Mica, Acrylic effects)
- ✅ Native look and feel on Windows 10/11
- ✅ Built-in components: Cards, Dialogs, Snackbar, System Tray
- ✅ Free & Open Source (650K+ downloads, 9K GitHub stars)
- ✅ Perfect for floating windows and notifications
- ✅ Professional appearance for potential sale

**Project Architecture:**
```
AutoDrop/
├── AutoDrop/              # Main WPF Project
│   ├── Models/                  # Data models (POCOs)
│   ├── ViewModels/              # MVVM view logic + state
│   │   └── Base/                # Base classes (ViewModelBase)
│   ├── Views/                   # XAML UI
│   │   └── Windows/             # Main windows
│   ├── Services/                # Business logic layer
│   │   ├── Interfaces/          # Service contracts
│   │   └── Implementations/     # Service implementations
│   ├── Core/                    # Infrastructure (DI, Constants)
│   ├── Converters/              # XAML converters
│   └── Resources/               # Styles, icons, themes
├── docs/                        # Documentation
├── installer/                   # Inno Setup installer files
└── scripts/                     # Build scripts
```

**Key Principles:**
- Clean MVVM with strict separation
- Dependency Injection (Microsoft.Extensions.DependencyInjection)
- Interface-based services (testable)
- ViewModels never call Views, Views never call Services directly

---

## 6.1 Production Hardening 

The following technical improvements were implemented to ensure production readiness:

### Code Quality & Resource Management
- [x] **IDisposable implementation** on all services with cleanup logic
- [x] **ConfigureAwait(false)** on all async calls in services
- [x] **Event handler cleanup** to prevent memory leaks
- [x] **Try-catch in async void** event handlers with proper error logging
- [x] **Exception handling** in `DroppedItem.FromPath()` for robust file info retrieval

### Async & Cancellation Support
- [x] **CancellationToken** added to all async interfaces and implementations
- [x] Proper cancellation propagation through service layers
- [x] Graceful operation cancellation support

### Architecture Improvements
- [x] **IWindowService** extracted for window management (testability)
- [x] **Input validation** on all model setters with `ArgumentException`
- [x] **Import file size limit** (10MB max) for rule configuration files

### Logging & Diagnostics
- [x] **Serilog file logging** with rolling daily logs
- [x] Logs stored at `%AppData%\AutoDrop\Logs\autodrop-YYYYMMDD.log`
- [x] 7-day log retention policy
- [x] Debug level logging in DEBUG builds, Info level in Release

### Installer & Distribution
- [x] **Inno Setup 6** EXE installer with modern Windows 11 style
- [x] Per-user installation to `%LocalAppData%\AutoDrop` (no admin required)
- [x] Self-contained single-file deployment (~75MB EXE)
- [x] Professional license agreement and wizard branding
- [x] Local build script (`scripts/build-inno.ps1`)

### CI/CD Pipeline
- [x] **GitHub Actions** automated CI/CD
- [x] **Unit Tests** with xUnit, FluentAssertions, Moq
- [x] Tests run on every push and pull request
- [x] Tests run before release builds
- [x] Automated installer creation on release tags

### Bug Fixes (v1.0.1)
- [x] **Multi-drop rule creation** - When dropping multiple files with different extensions and checking "Remember", rules are now only saved for the displayed file type (not all extensions)

---

## 7. Post-MVP Features

### Phase 2: Automation & Safety (v1.1) ✅ COMPLETE

| ID | Feature | Priority | Status |
|----|---------|----------|--------|
| US-08 | Auto-Move with Rules | 🔴 Critical | ✅ Done |
| US-09 | Enhanced Undo History | 🔴 Critical | ⏳ Basic (single undo works) |
| US-10 | Smart Auto-Rename | 🔴 Critical | ✅ Done |
| US-11 | Duplicate Detection & Handling | 🟡 High | ⏳ Not started |

### Phase 3: User Control (v1.2) ✅ COMPLETE

| ID | Feature | Priority | Status |
|----|---------|----------|--------|
| US-12 | Rules Management UI | 🔴 Critical | ✅ Done |
| US-13 | Batch Operations | 🟡 High | ⏳ Basic (multi-drop works) |
| US-14 | Custom Folder Organization | 🟡 High | ✅ Done |
| US-15 | Copy Mode (Shift+Drop) | 🟢 Medium | ⏳ Not started |

### Phase 4: Intelligence (v1.3)

| ID | Feature | Priority | Status |
|----|---------|----------|--------|
| US-16 | AI-Powered Categorization | 🟡 High | ⏳ Not started |

### Phase 5: Distribution (v2.0) ✅ COMPLETE

| ID | Feature | Priority | Status |
|----|---------|----------|--------|
| US-17 | Professional Installer (EXE) | 🔴 Critical | ✅ Done (Inno Setup) |

---

## 8. Detailed User Stories (Post-MVP)

### US-08: Auto-Move with Rules ⭐ ✅ COMPLETE

**Story:**  
As a user, when I drop a file with an existing rule, I want it to move automatically without showing the popup, so I save time on repetitive tasks.

**Acceptance Criteria:**
- [x] Check for matching rule before showing popup
- [x] If rule exists and `autoMove` is enabled → move silently
- [x] Show toast notification: "✓ Auto-moved report.pdf → Documents"
- [x] Toast has [Undo] button for 5 seconds
- [x] If rule exists but `autoMove` is disabled → show popup as normal
- [x] If multiple files with different rules → batch auto-move each
- [x] If file has no rule → show suggestion popup

**Rule Format (Enhanced):**
```json
{
  "extension": ".pdf",
  "destination": "C:\\Users\\Me\\Documents\\Work",
  "autoMove": true,
  "createdAt": "2026-01-03T10:30:00Z",
  "lastUsedAt": "2026-01-03T15:45:00Z",
  "useCount": 12
}
```

**Priority:** 🔴 Critical - Core automation feature  

---

### US-09: Enhanced Undo History ⭐

**Story:**  
As a user, I want to see a history of my last 20 operations and undo multiple moves at once, so I can recover from mistakes easily.

**Acceptance Criteria:**
- [ ] Track last 20 operations in memory
- [ ] Right-click tray icon → "Show History"
- [ ] History window shows:
  - Timestamp
  - File name
  - Source → Destination
  - Status (Success/Failed/Undone)
- [ ] Click any operation → [Undo] button appears
- [ ] Support bulk undo: Select multiple → "Undo Selected"
- [ ] Operations persist across app restarts (save to `history.json`)
- [ ] Clear history option

**Data Structure:**
```json
{
  "history": [
    {
      "id": "uuid-here",
      "timestamp": "2026-01-03T15:45:30Z",
      "fileName": "report.pdf",
      "source": "C:\\Users\\Me\\Downloads\\report.pdf",
      "destination": "C:\\Users\\Me\\Documents\\report.pdf",
      "operation": "move",
      "status": "success",
      "undone": false
    }
  ]
}
```

**Priority:** 🔴 Critical - Safety feature  
**Estimated Effort:** 3 days

---

### US-10: Smart Auto-Rename ⭐ ✅ COMPLETE (Basic)

**Story:**  
As a user, when a file already exists at the destination, I want intelligent auto-renaming that preserves my intent, so I never lose files.

**Acceptance Criteria:**
- [x] Detect existing file before move
- [x] Auto-rename pattern: `filename (1).ext`, `filename (2).ext`, etc.
- [ ] If file with same content exists (hash check) → offer:
  - Skip (don't move)
  - Replace
  - Keep both (rename)
- [ ] Show notification: "report.pdf renamed to report (1).pdf"
- [x] User preference: "Always auto-rename" vs "Always ask" (settings model exists)
- [x] Store preference in `settings.json`

**Settings:**
```json
{
  "fileConflictBehavior": "auto-rename",  // "auto-rename", "ask", "skip"
  "compareFileContents": true,
  "renamePattern": "{name} ({n}){ext}"
}
```

**Priority:** 🔴 Critical - Prevents data loss  
**Estimated Effort:** 2 days

---

### US-11: Duplicate Detection & Handling

**Story:**  
As a user, I want the app to detect when I'm moving a duplicate file and give me smart options, so I don't waste disk space.

**Acceptance Criteria:**
- [ ] On move, check if identical file exists (SHA256 hash)
- [ ] If duplicate found → show dialog:
  - "Duplicate detected: report.pdf already exists"
  - Preview both files (size, date, thumbnail if image)
  - Options:
    - **Skip** (don't move, keep original)
    - **Replace** (delete destination, move new)
    - **Keep Both** (auto-rename)
    - **Delete Source** (destination is same, just remove source)
- [ ] Remember choice per session: "Do this for all duplicates"
- [ ] Option to enable/disable duplicate checking in settings
- [ ] Fast hash comparison (only compare hashes, not full file scan)

**Performance:**
- Only hash files < 100MB by default
- For large files → compare size + date only

**Priority:** 🟡 High - Quality of life  
**Estimated Effort:** 3 days

---

### US-12: Rules Management UI ⭐ ✅ COMPLETE

**Story:**  
As a user, I want a visual interface to view, edit, enable/disable, and delete my rules, so I don't have to edit JSON files manually.

**Acceptance Criteria:**
- [x] New window: "Manage Rules" (accessible from tray menu)
- [x] List view showing all rules:
  - Extension (icon + text)
  - Destination path
  - Auto-move toggle (checkbox)
  - Use count
  - Last used date
- [x] Actions per rule:
  - **Edit** → change destination (folder picker)
  - **Toggle Auto-Move** → enable/disable auto-move
  - **Delete** → remove rule with confirmation
- [x] Search/filter rules by extension
- [ ] Sort by: Extension, Use Count, Last Used, Destination
- [x] "Add New Rule" button → manual rule creation
- [x] Export/Import rules (JSON file)

**UI Mockup:**
```
╔═══════════════════════════════════════════╗
║  Manage Rules                    [X]      ║
╠═══════════════════════════════════════════╣
║  Search: [_________]  [+ Add Rule]        ║
╠═══════════════════════════════════════════╣
║ Extension | Destination    | Auto | Used  ║
║ 📄 .pdf   | C:\...\Work    | ☑   | 47x   ║
║ 🖼 .png    | C:\...\Desktop | ☐   | 12x   ║
║ 📦 .zip   | C:\...\Downloads| ☑   | 8x    ║
║                                           ║
║  [Edit] [Delete]                          ║
╚═══════════════════════════════════════════╝
```

**Priority:** 🔴 Critical - User empowerment  
**Estimated Effort:** 4 days

---

### US-13: Batch Operations

**Story:**  
As a user, I want to drop multiple files of different types and have them intelligently organized to their respective destinations in one action.

**Acceptance Criteria:**
- [ ] Accept multiple files (already supported)
- [ ] Group files by extension/category
- [ ] Show summary popup:
  - "5 PDFs → Documents"
  - "3 PNGs → Pictures"  
  - "2 ZIPs → Downloads"
- [ ] Single [Organize All] button → batch move
- [ ] Individual checkboxes to customize per category
- [ ] Progress bar for large batches (>10 files)
- [ ] Summary notification: "✓ Organized 10 files to 3 folders"
- [ ] Undo moves entire batch

**Popup Example:**
```
╔═══════════════════════════════════════╗
║  Organize 10 files?                   ║
╠═══════════════════════════════════════╣
║  ☑ 5 PDFs      → 📁 Documents         ║
║  ☑ 3 PNGs      → 📁 Pictures          ║
║  ☑ 2 ZIPs      → 📁 Downloads         ║
║                                       ║
║  [Organize All]  [Customize]          ║
╚═══════════════════════════════════════╝
```

**Priority:** 🟡 High - Productivity boost  
**Estimated Effort:** 3 days

---

### US-14: Custom Folder Organization ✅ COMPLETE

**Story:**  
As a user, I want to define custom categories and destination folders (e.g., "Work", "Personal", "Taxes") so files are organized exactly how I want.

**Acceptance Criteria:**
- [x] Settings window → "Custom Folders" tab (in Rules Manager)
- [x] User can add custom folders:
  - Display name: "Work Documents"
  - Path: `C:\Users\Me\Work`
  - Icon/color picker (uses folder icon)
- [x] Custom folders appear in suggestions list
- [x] Rules can target custom folders
- [x] Pin favorite folders to always show in suggestions
- [ ] Recent destinations (last 5 used) appear at top

**Settings Storage:**
```json
{
  "customFolders": [
    {
      "name": "Work Documents",
      "path": "C:\\Users\\Me\\Work",
      "icon": "💼",
      "pinned": true
    },
    {
      "name": "Personal Projects",
      "path": "D:\\Projects",
      "icon": "🚀",
      "pinned": false
    }
  ]
}
```

**Priority:** 🟡 High - Personalization  
**Estimated Effort:** 3 days

---

### US-15: Copy Mode (Shift+Drop)

**Story:**  
As a power user, I want to hold Shift while dropping files to copy instead of move, so I can keep originals while organizing copies.

**Acceptance Criteria:**
- [ ] Detect Shift key during drop operation
- [ ] If Shift held → change mode to "Copy"
- [ ] Visual indicator: "Drop to COPY" (instead of "Drop files here")
- [ ] Popup shows: "Copy to..." instead of "Move to..."
- [ ] Notification: "✓ Copied report.pdf → Documents"
- [ ] Undo operation removes copied file (not original)
- [ ] Rules still apply in copy mode
- [ ] Settings option: "Default mode" → Move or Copy

**Keyboard Shortcuts:**
- **Shift + Drop** → Copy mode
- **Ctrl + Drop** → Alternative copy mode
- **Alt + Drop** → Show advanced options

**Priority:** 🟢 Medium - Power user feature  
**Estimated Effort:** 2 days

---

### US-16: AI-Powered Categorization

**Story:**  
As a user, I want the app to analyze file content (not just extensions) to suggest better destinations, so invoices go to "Finances" and photos of receipts go to "Receipts".

**Acceptance Criteria:**
- [ ] Integrate ML model for content analysis
- [ ] Analyze text files (PDF, DOCX) for keywords:
  - "Invoice" → suggest Finance folder
  - "Receipt" → suggest Receipts folder
  - "Tax" → suggest Taxes folder
- [ ] Analyze images with OCR:
  - Receipts → detect dates, amounts
  - Screenshots → detect context
- [ ] Show AI confidence level in suggestions
- [ ] User can enable/disable AI in settings
- [ ] Privacy: All processing happens locally (no cloud)
- [ ] Fallback to extension-based if AI fails

**Technology:**
- **OCR:** Tesseract.NET (local)
- **Text Analysis:** ML.NET (local classification)
- **Model:** Custom-trained on document categories

**Settings:**
```json
{
  "aiEnabled": true,
  "aiConfidenceThreshold": 0.7,
  "analyzeTextFiles": true,
  "analyzeImages": true,
  "ocrLanguage": "en"
}
```

**Priority:** 🟡 High - Competitive advantage  
**Estimated Effort:** 7 days

---

### US-17: Professional Installer (MSI/MSIX) ⭐

**Story:**  
As an end user, I want a professional installer that makes setup easy and installs updates automatically, so I trust the application quality.

**Acceptance Criteria:**

**MSI Installer:**
- [ ] WiX Toolset v4 setup project
- [ ] Install to `Program Files\AutoDrop`
- [ ] Create Start Menu shortcuts
- [ ] Create Desktop shortcut (optional)
- [ ] Add to Windows "Add/Remove Programs"
- [ ] Uninstaller removes all files + AppData
- [ ] Silent install option: `/quiet`
- [ ] License agreement screen
- [ ] Custom banner/logo
- [ ] Code signing certificate (DigiCert/Sectigo)

**MSIX Package:**
- [ ] Modern MSIX packaging for Microsoft Store
- [ ] Auto-update through Store
- [ ] Sandboxed installation
- [ ] Portable settings (synced via Microsoft account)

**Update System:**
- [ ] Check for updates on startup
- [ ] Notification: "Update available (v1.2) → Install now"
- [ ] Download installer in background
- [ ] Auto-install on next launch (or prompt user)
- [ ] Release notes display
- [ ] Update channel: Stable / Beta

**Installer Features:**
- [ ] Detect .NET 8 Runtime → install if missing
- [ ] First-run tutorial/onboarding
- [ ] Analytics opt-in prompt (anonymous usage stats)
- [ ] Register file associations (optional: .autodrop rule files)

**Distribution:**
- [ ] Microsoft Store submission
- [ ] Direct download (website)
- [ ] Chocolatey package
- [ ] WinGet package

**Priority:** 🔴 Critical - Professional distribution  
**Estimated Effort:** 5 days

---

## 9. Development Roadmap

### Phase 1: MVP Foundation (Weeks 1-2) ✅
- [x] US-01: Floating Drop Zone
- [x] US-02: Drag & Drop Files/Folders
- [x] US-03: Suggest Destinations
- [x] US-04: One-Click Move
- [x] US-05: Undo via Notification
- [x] US-06: Remember My Choice
- [x] US-07: System Tray
- [x] Production hardening (logging, error handling, resource cleanup)
- [x] Professional EXE installer with Inno Setup 6

**Status:** ✅ Complete - MVP Pre-Released January 4, 2026

---

### Phase 2: Automation & Safety (Weeks 3-4)
**Goal:** Make the app intelligent and safe

**Week 3:**
- [x] US-08: Auto-Move with Rules (2 days) ✅
- [x] US-10: Smart Auto-Rename (2 days) ✅
- [x] Testing & bug fixes (1 day) ✅

**Week 4:**
- [ ] US-09: Enhanced Undo History (3 days)
- [ ] US-11: Duplicate Detection (2 days)

**Deliverable:** v1.1 - "AutoDrop Pro"

---

### Phase 3: User Control (Weeks 5-6)
**Goal:** Give users full control over organization

**Week 5:**
- [x] US-12: Rules Management UI (4 days) ✅
- [x] UI/UX polish (1 day) ✅

**Week 6:**
- [ ] US-13: Batch Operations (3 days)
- [x] US-14: Custom Folder Organization (2 days) ✅

**Deliverable:** v1.2 - "Power User Edition"

---

### Phase 4: Intelligence & Polish (Weeks 7-8)
**Goal:** Add competitive advantages

**Week 7:**
- [ ] US-16: AI-Powered Categorization (5 days)

**Week 8:**
- [ ] US-15: Copy Mode (Shift+Drop) (2 days)
- [ ] Performance optimization (2 days)
- [ ] Accessibility improvements (1 day)

**Deliverable:** v1.3 - "Smart Edition"

---

### Phase 5: Commercial Launch (Weeks 9-10)
**Goal:** Professional distribution & monetization

**Week 9:**
- [ ] US-17: MSI/MSIX Installer (5 days)

**Week 10:**
- [ ] Code signing certificate
- [ ] Microsoft Store submission
- [ ] Landing page + documentation
- [ ] Beta testing with 10-20 users
- [ ] License system integration (Gumroad/Paddle)

**Deliverable:** v2.0 - "Commercial Release"

---

## 10. Technical Enhancements

### Required Infrastructure Changes

**1. Enhanced Models:**
```csharp
// FileRule.cs - Add new properties
public bool AutoMove { get; set; } = false;
public string Category { get; set; } = "Unknown";

// OperationHistory.cs - New model
public class OperationHistory
{
    public Guid Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string FileName { get; set; }
    public string SourcePath { get; set; }
    public string DestinationPath { get; set; }
    public OperationType Type { get; set; } // Move, Copy
    public OperationStatus Status { get; set; }
    public bool Undone { get; set; }
}
```

**2. New Services:**
- `IHistoryService` - Track and manage operation history
- `IDuplicateDetectionService` - Hash comparison and duplicate handling
- `IAICategorizationService` - ML-powered file analysis
- `IUpdateService` - Check for updates and auto-install

**3. Storage Files:**
```
%AppData%\AutoDrop\
├── rules.json          # Enhanced with autoMove flag
├── settings.json       # App preferences
├── history.json        # Last 20 operations
├── customFolders.json  # User-defined folders
└── cache\
    └── file-hashes.db  # SQLite for duplicate detection
```

---

## 11. Out of Scope (Future Consideration)

| Feature | Status |
|---------|--------|
| Cloud sync (OneDrive/Dropbox) | v3.0 consideration |
| Mobile companion app | v3.0 consideration |
| Folder watching/monitoring | v2.1 feature |
| Network drive support | v2.1 feature |
| Team collaboration features | Enterprise edition |
| Scheduled organization | v2.1 feature |
| Statistics dashboard | v2.2 feature |

---

## 12. Success Metrics (Commercial Edition)

**v2.0 is successful when:**
1. ✅ Auto-move works 95%+ of the time for known extensions
2. ✅ Users can manage rules without touching JSON
3. ✅ Duplicate detection prevents data loss 100% of time
4. ✅ Batch operations handle 100+ files smoothly
5. ✅ AI categorization is 80%+ accurate
6. ✅ Installer completes in < 2 minutes with zero errors
7. ✅ App passes Microsoft Store certification

**Business Metrics:**
- 1,000 downloads in first month
- 10% conversion rate (free trial → paid)
- 4.5+ star rating on Microsoft Store
- < 1% refund rate

---

## 13. Risks & Mitigations (Enhanced)

| Risk | Impact | Mitigation |
|------|--------|------------|
| AI model too slow | High | Async processing + progress indicator |
| Hash calculation for large files | Medium | Skip files > 100MB, size-only comparison |
| Rules corruption | High | Validate JSON on load, auto-backup |
| Update breaks user rules | Critical | Schema versioning + migration logic |
| Store rejection | Critical | Follow guidelines, thorough testing |
| Performance with 1000+ rules | Medium | Index rules by extension, use caching |

---

## 14. Priority Matrix

**Must Have (Critical Path to v2.0):**
1. US-08: Auto-Move with Rules ⭐
2. US-09: Enhanced Undo History ⭐
3. US-10: Smart Auto-Rename ⭐
4. US-12: Rules Management UI ⭐
5. US-17: Professional Installer ⭐

**Should Have (Competitive Advantages):**
6. US-13: Batch Operations
7. US-14: Custom Folder Organization
8. US-16: AI-Powered Categorization
9. US-11: Duplicate Detection

**Nice to Have (Polish):**
10. US-15: Copy Mode (Shift+Drop)

---

## 15. Quality Assurance

**Testing Requirements:**
- [ ] Unit tests: 80%+ code coverage
- [ ] Integration tests for all file operations
- [ ] UI automation tests (WPF UI Testing Framework)
- [ ] Performance tests: 1000+ files batch operation
- [ ] Security audit: File system access patterns
- [ ] Accessibility audit: NVDA/Narrator compatibility
- [ ] Beta testing: 20+ real users for 2 weeks

**Supported Scenarios:**
- Windows 10 (21H2+) and Windows 11
- Files: 1 byte to 10 GB
- Network drives (local mapping)
- External USB drives
- OneDrive/Dropbox local folders
- Multi-monitor setups

---

## 16. Monetization Strategy

**Pricing Model:**

| Edition | Price | Features |
|---------|-------|----------|
| **Free** | $0 | Basic drop zone, 5 rules max, manual move only |
| **Pro** | $9.99 | Unlimited rules, auto-move, undo history, batch ops |
| **Business** | $29.99 | Pro + AI categorization, priority support, 5 devices |

**Revenue Projections:**
- Month 1: 1,000 downloads → 100 paid ($1,000)
- Month 6: 10,000 downloads → 1,500 paid ($15,000)
- Year 1 Goal: $50,000 revenue

**Marketing Channels:**
- Microsoft Store (primary)
- Product Hunt launch
- Reddit (r/productivity, r/software)
- YouTube demos (productivity influencers)
- Landing page with free trial

---

## 17. Next Steps (Action Plan)

### Immediate (Week 3):
1. ✅ Requirements v2.0 approved
2. ⬜ Create detailed technical design docs
3. ⬜ Set up project branches (main, develop, feature/*)
4. ⬜ Update project board with new user stories
5. ⬜ Begin US-08: Auto-Move implementation

### Short-term (Weeks 3-6):
- Complete Phase 2: Automation & Safety
- Complete Phase 3: User Control
- Internal testing & bug fixes

### Medium-term (Weeks 7-10):
- Complete Phase 4: Intelligence
- Complete Phase 5: Distribution
- Beta testing program
- Microsoft Store submission

### Long-term (Months 3-6):
- Launch v2.0 Commercial
- Monitor user feedback
- Plan v2.1 features
- Scale marketing efforts

---

**🎯 Target: Commercial Launch by March 2026**

---

## Appendix A: Keyboard Shortcuts Reference

| Shortcut | Action |
|----------|--------|
| **Shift + Drop** | Copy instead of move |
| **Ctrl + Drop** | Copy (alternative) |
| **Alt + Drop** | Show advanced options |
| **Ctrl + Z** | Undo last operation (global) |
| **Ctrl + Shift + H** | Show history window |
| **Ctrl + Shift + R** | Show rules manager |
| **Ctrl + Shift + D** | Show/hide drop zone |
| **Esc** | Close popup/dialog |

---

## Appendix B: File Conflict Resolution Logic

```
File drop detected
    ↓
Check destination for existing file
    ↓
File exists?
    ├─ NO → Move/Copy directly
    └─ YES → Check user preference
            ├─ Auto-rename → Create filename (1).ext
            ├─ Ask → Show conflict dialog
            └─ Skip → Cancel operation
                    ↓
            Duplicate detection enabled?
                ├─ YES → Compare hashes
                │       ├─ Same hash → "Delete source or skip?"
                │       └─ Different → "Replace, keep both, or skip?"
                └─ NO → Skip hash check
```

---

**Document Status:** ✅ MVP Released  
**Last Updated:** January 12, 2026  
**Version Control:** This document is the single source of truth for AutoDrop development.
