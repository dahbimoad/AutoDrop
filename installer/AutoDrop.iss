; ============================================================================
; AutoDrop Installer Script
; Built with Inno Setup 6 - Modern Windows Installer
; https://jrsoftware.org/isinfo.php
; ============================================================================

#define MyAppName "AutoDrop"
#define MyAppVersion GetEnv('APP_VERSION')
#if MyAppVersion == ""
#define MyAppVersion "1.0.1"
#endif
#define MyAppPublisher "Moad Dahbi"
#define MyAppURL "https://github.com/dahbimoad/AutoDrop"
#define MyAppExeName "AutoDrop.exe"
#define MyAppDescription "Smart File Organizer - Drag, Drop, Organize"
#define MyAppCopyright "Copyright (c) 2026 Moad Dahbi"

[Setup]
; ---- Application Identity ----
; Unique App ID - DO NOT CHANGE after first release (used for upgrades)
AppId={{8F2C7B1E-4D3A-4E5F-9B8C-1A2D3E4F5678}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/issues
AppUpdatesURL={#MyAppURL}/releases
AppCopyright={#MyAppCopyright}
AppComments={#MyAppDescription}
VersionInfoVersion={#MyAppVersion}
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription={#MyAppDescription}
VersionInfoCopyright={#MyAppCopyright}
VersionInfoProductName={#MyAppName}
VersionInfoProductVersion={#MyAppVersion}

; ---- Installation Directories ----
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
AllowNoIcons=yes

; ---- License ----
LicenseFile=License.rtf

; ---- Output Settings ----
OutputDir=..\output
OutputBaseFilename=AutoDrop-{#MyAppVersion}-win-x64-setup

; ---- Compression (maximum) ----
Compression=lzma2/ultra64
SolidCompression=yes
LZMAUseSeparateProcess=yes

; ---- Modern Windows 11 Style UI ----
WizardStyle=modern
WizardSizePercent=120,120

; ---- System Requirements ----
MinVersion=10.0
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

; ---- Privileges (per-user install, no admin needed) ----
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog

; ---- Uninstaller ----
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}
Uninstallable=yes
CreateUninstallRegKey=yes

; ---- Branding ----
SetupIconFile=..\AutoDrop\Assets\app.ico
WizardImageFile=wizard-large.bmp
WizardSmallImageFile=wizard-small.bmp

; ---- Upgrade Behavior ----
UsePreviousAppDir=yes
UsePreviousGroup=yes
UsePreviousTasks=yes
CloseApplications=yes
RestartApplications=yes
CloseApplicationsFilter=*.exe

; ---- Setup Behavior ----
DisableWelcomePage=no
DisableDirPage=auto
DisableReadyPage=no
ShowLanguageDialog=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Messages]
WelcomeLabel1=Welcome to {#MyAppName} Setup
WelcomeLabel2=This will install {#MyAppName} {#MyAppVersion} on your computer.%n%n{#MyAppDescription}%n%nClick Next to continue.
FinishedHeadingLabel=Setup Complete!
FinishedLabel={#MyAppName} has been installed successfully.%n%nDrop files onto the floating window to organize them instantly.

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Shortcuts:"; Flags: checkedonce

[Files]
; Main executable
Source: "..\publish\win-x64\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
; Start Menu shortcut
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Comment: "{#MyAppDescription}"
; Desktop shortcut (optional)
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon; Comment: "{#MyAppDescription}"

[Registry]
; Store app info in registry for detection by other tools
Root: HKCU; Subkey: "Software\{#MyAppName}"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\{#MyAppName}"; ValueType: string; ValueName: "InstallPath"; ValueData: "{app}"
Root: HKCU; Subkey: "Software\{#MyAppName}"; ValueType: string; ValueName: "Version"; ValueData: "{#MyAppVersion}"

[Run]
; Launch app after install (optional)
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Clean up app folder on uninstall
Type: filesandordirs; Name: "{app}"

[Code]
// ============================================================================
// Custom Pascal Script for enhanced installer behavior
// ============================================================================

var
  IsUpgrade: Boolean;

// Check if this is an upgrade
function IsUpgradeInstall: Boolean;
var
  PrevPath: String;
begin
  Result := RegQueryStringValue(HKCU, 'Software\{#MyAppName}', 'InstallPath', PrevPath) and (PrevPath <> '');
end;

// Initialize setup
function InitializeSetup: Boolean;
begin
  IsUpgrade := IsUpgradeInstall;
  Result := True;
end;

// Custom welcome message for upgrades
procedure InitializeWizard;
begin
  if IsUpgrade then
  begin
    WizardForm.WelcomeLabel2.Caption := 
      'Setup will upgrade {#MyAppName} to version {#MyAppVersion}.' + #13#10 + #13#10 +
      'Your settings and rules will be preserved.' + #13#10 + #13#10 +
      'Click Next to continue.';
  end;
end;

// Close running instance before install
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
begin
  Result := '';
  // Try to close AutoDrop if running
  Exec('taskkill', '/f /im {#MyAppExeName}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Sleep(500); // Wait for process to fully close
end;

// Offer to remove user data on uninstall
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  AppDataPath: String;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    AppDataPath := ExpandConstant('{userappdata}\{#MyAppName}');
    if DirExists(AppDataPath) then
    begin
      if MsgBox('Do you want to remove your AutoDrop settings and rules?' + #13#10 + #13#10 +
                'Click Yes to remove all data, or No to keep your settings.', 
                mbConfirmation, MB_YESNO) = IDYES then
      begin
        DelTree(AppDataPath, True, True, True);
      end;
    end;
  end;
end;
