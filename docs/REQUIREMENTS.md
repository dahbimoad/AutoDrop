# AutoDrop — Requirements Document
**Version:** 1.0 MVP  
**Date:** January 1, 2026  
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
- [ ] Window is always-on-top (`Topmost = true`)
- [ ] Window is small (~150x150 pixels)
- [ ] Window can be dragged to any screen position
- [ ] Window has minimal UI: icon + "Drop files here" text
- [ ] Window shows visual feedback (color change) when file hovers over it

---

### US-02: Drag & Drop Files/Folders

**Story:**  
As a user, I want to drag and drop files or folders onto the drop zone.

**Acceptance Criteria:**
- [ ] Accepts single file
- [ ] Accepts multiple files (batch)
- [ ] Accepts single folder
- [ ] Accepts multiple folders
- [ ] Shows file count when multiple items dropped

---

### US-03: Suggest Destinations

**Story:**  
As a user, after dropping a file, I want to see 3-4 suggested destination folders based on file type.

**Acceptance Criteria:**
- [ ] Popup appears near drop zone after drop
- [ ] Shows file name and detected type (e.g., "Image", "Document")
- [ ] Shows 3-4 destination buttons based on extension mapping:

| Extension | Category | Default Destination |
|-----------|----------|---------------------|
| .jpg .png .gif .bmp .webp | Image | Pictures |
| .pdf .docx .xlsx .pptx .txt | Document | Documents |
| .mp4 .avi .mkv .mov | Video | Videos |
| .mp3 .wav .flac | Audio | Music |
| .zip .rar .7z | Archive | Downloads |
| .exe .msi | Installer | Downloads |
| Other | Unknown | Desktop |

- [ ] Best match is visually highlighted
- [ ] "Browse other folder..." option available
- [ ] Popup has X button to cancel

---

### US-04: One-Click Move

**Story:**  
As a user, when I click a destination button, the file should move immediately.

**Acceptance Criteria:**
- [ ] File/folder moves to selected destination
- [ ] Original file is removed from source
- [ ] If file exists at destination → auto-rename to `filename (1).ext`
- [ ] If move fails (permissions, file locked) → show error message
- [ ] Popup closes after successful move

---

### US-05: Undo via Notification

**Story:**  
As a user, after a move, I want a notification with an Undo button so I can recover from mistakes.

**Acceptance Criteria:**
- [ ] Toast notification appears (bottom-right corner)
- [ ] Shows: "✓ Moved filename.ext → Pictures"
- [ ] Has [Undo] button
- [ ] Clicking Undo moves file back to original location
- [ ] Notification auto-dismisses after 5 seconds
- [ ] Only last operation can be undone

---

### US-06: Remember My Choice

**Story:**  
As a user, I want to check "Always do this for .jpg files" so the app learns my preference.

**Acceptance Criteria:**
- [ ] Checkbox in suggestion popup: "Always move .{ext} files here"
- [ ] When checked + move confirmed → rule saved to local JSON file
- [ ] Next time same extension dropped → auto-move without popup
- [ ] Auto-move shows toast notification (not popup)
- [ ] Rules stored in: `%AppData%/AutoDrop/rules.json`

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
- [ ] Minimize button sends app to system tray (not taskbar)
- [ ] Tray icon visible in notification area
- [ ] Double-click tray icon → restore drop zone
- [ ] Right-click tray icon shows menu:
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
| Storage | JSON files (rules, config) |
| Notifications | WPF UI Snackbar + Windows Toast |

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
├── src/
│   └── AutoDrop/          # Main WPF Project
│       ├── Models/                  # Data models (POCOs)
│       ├── ViewModels/              # MVVM view logic + state
│       │   └── Base/                # Base classes (ViewModelBase, RelayCommand)
│       ├── Views/                   # XAML UI
│       │   ├── Windows/             # Main windows
│       │   ├── Dialogs/             # Popup dialogs
│       │   └── Controls/            # Reusable controls
│       ├── Services/                # Business logic layer
│       │   ├── Interfaces/          # Service contracts
│       │   └── Implementations/     # Service implementations
│       ├── Core/                    # Infrastructure (DI, Config)
│       ├── Helpers/                 # Utility classes
│       ├── Converters/              # XAML converters
│       └── Resources/               # Styles, icons, themes
├── tests/
│   ├── AutoDrop.Tests/
│   └── AutoDrop.IntegrationTests/
└── docs/
```

**Key Principles:**
- Clean MVVM with strict separation
- Dependency Injection (Microsoft.Extensions.DependencyInjection)
- Interface-based services (testable)
- ViewModels never call Views, Views never call Services directly

---

## 7. Out of Scope (NOT in MVP)

| Feature | Reason |
|---------|--------|
| ❌ File renaming | Complexity |
| ❌ Folder watching | Automation later |
| ❌ AI analysis | Future enhancement |
| ❌ Rule editor UI | Keep simple |
| ❌ History screen | Session undo is enough |
| ❌ Start with Windows | Polish later |
| ❌ Profiles | Single user for now |
| ❌ Cloud sync | Local-first |

---

## 8. Development Phases

### Phase 1: Core (Days 1-3)
- [ ] US-01: Drop Zone window
- [ ] US-02: Drag & drop handling
- [ ] US-03: Suggestion popup
- [ ] US-04: Move file logic

### Phase 2: Safety (Days 4-5)
- [ ] US-05: Toast notification + Undo

### Phase 3: Intelligence (Days 6-7)
- [ ] US-06: Remember choice + auto-move

### Phase 4: Polish (Day 8)
- [ ] US-07: System tray
- [ ] Bug fixes
- [ ] Testing

---

## 9. Success Criteria

MVP is complete when:
1. ✅ User can drop a file and move it in 2 clicks
2. ✅ App suggests correct destination 80% of the time
3. ✅ User can undo a mistake
4. ✅ App remembers preferences for next time
5. ✅ App runs quietly in system tray

---

## 10. Risks & Mitigations

| Risk | Mitigation |
|------|------------|
| File permission errors | Show clear error message, don't crash |
| File in use | Detect and notify user |
| Overwriting files | Auto-rename, never overwrite |
| Lost files | Undo feature + keep source until confirmed |

---

## Next Steps

1. ✅ Requirements approved
2. ⬜ Create project structure
3. ⬜ Build US-01 (Drop Zone)
4. ⬜ Build US-02 (Drag & Drop)
5. ⬜ Continue...

---

**Ready to start coding!**
