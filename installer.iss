; ──────────────────────────────────────────────
; Inno Setup Script for Tama's Palace POS
; Supports: Fresh install, In-place upgrade
; Data Safety: DB is stored in AppData (never overwritten)
; ──────────────────────────────────────────────

#define MyAppName "Tama's Palace"
#define MyAppVersion "2.2.0"
#define MyAppPublisher "Tama's Palace"
#define MyAppURL "https://tamaspalacespot.com"
#define MyAppExeName "Tama's Palace.exe"

[Setup]
; Unique AppId ensures upgrades replace the same installation
AppId={{A7B3C4D5-E6F7-8901-2345-6789ABCDEF01}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
; Output to PROJECT FOLDER for easy access
OutputDir=.\installer_output
OutputBaseFilename=TamasPalace_Setup_v2.2.0
SetupIconFile=app.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\{#MyAppExeName}
DisableProgramGroupPage=yes
; Allow upgrading without uninstalling first
UsePreviousAppDir=yes
; Don't require admin if possible (installs to Program Files though)
PrivilegesRequired=admin
; Update mode — version info lets Windows know this is an upgrade
VersionInfoVersion=2.2.0.0
VersionInfoDescription=Tama's Palace POS System
VersionInfoCopyright=© 2026 Tama's Palace
VersionInfoProductName=Tama's Palace
VersionInfoProductVersion=2.2.0.0

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"

[Files]
; Main application files and native libraries
Source: "publish\RestaurantManager.exe"; DestDir: "{app}"; DestName: "{#MyAppExeName}"; Flags: ignoreversion
Source: "publish\*.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "publish\*.pdb"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
; Application icon
Source: "app.ico"; DestDir: "{app}"; Flags: ignoreversion

; Branding images — deploy to target user's AppData so they are available on fresh installs
; Logo
Source: "{%APPDATA}\RestaurantManager\logos\*"; DestDir: "{userappdata}\RestaurantManager\logos"; Flags: skipifsourcedoesntexist onlyifdoesntexist
; Sidebar & Welcome background images
Source: "{%APPDATA}\RestaurantManager\images\*"; DestDir: "{userappdata}\RestaurantManager\images"; Flags: skipifsourcedoesntexist onlyifdoesntexist

; NOTE: The SQLite database (restaurant.db) lives in %APPDATA%\RestaurantManager.
; It is NOT included in [Files], so it is NEVER overwritten.
; The app's InitializeDatabase() handles all schema migrations automatically.

[Icons]
; Start Menu shortcuts
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\app.ico"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
; Desktop shortcut
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\app.ico"; Tasks: desktopicon

[Run]
; Launch after install
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent

[Code]
var
  IsUpgrade: Boolean;
  OldVersion: String;

// ─── DETECT EXISTING INSTALLATION ───
function GetPreviousVersion(): String;
var
  PrevVersion: String;
begin
  Result := '';
  if RegQueryStringValue(HKLM, 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{A7B3C4D5-E6F7-8901-2345-6789ABCDEF01}_is1',
    'DisplayVersion', PrevVersion) then
    Result := PrevVersion
  else if RegQueryStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{A7B3C4D5-E6F7-8901-2345-6789ABCDEF01}_is1',
    'DisplayVersion', PrevVersion) then
    Result := PrevVersion;
end;

// ─── CLOSE APP BEFORE UPGRADE ───
function InitializeSetup(): Boolean;
var
  ResultCode: Integer;
begin
  Result := True;
  
  // Detect if this is an upgrade
  OldVersion := GetPreviousVersion();
  IsUpgrade := (OldVersion <> '');
  
  if IsUpgrade then
  begin
    // Show upgrade confirmation with version info
    if MsgBox('Tama''s Palace v' + OldVersion + ' is currently installed.' + #13#10 + #13#10 +
              'Do you want to update to v{#MyAppVersion}?' + #13#10 + #13#10 +
              '• Your data and settings will be preserved' + #13#10 +
              '• Only the application will be updated',
              mbConfirmation, MB_YESNO) = IDNO then
    begin
      Result := False;
      Exit;
    end;
  end;
  
  // Try to close existing running instance
  if Exec('taskkill', '/IM "Tama''s Palace.exe" /F', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
  begin
    Sleep(1000); // Wait for process to fully exit
  end;
  // Also try the original name in case it's from an older version
  if Exec('taskkill', '/IM "RestaurantManager.exe" /F', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
  begin
    Sleep(1000);
  end;
end;

// ─── SKIP UNNECESSARY PAGES ON UPDATE ───
function ShouldSkipPage(PageID: Integer): Boolean;
begin
  // During an upgrade, skip the directory selection and tasks pages
  // (we already know where it's installed and shortcuts exist)
  if IsUpgrade then
  begin
    if (PageID = wpSelectDir) or (PageID = wpSelectProgramGroup) or (PageID = wpSelectTasks) then
    begin
      Result := True;
      Exit;
    end;
  end;
  Result := False;
end;

// ─── CUSTOM WELCOME MESSAGE FOR UPDATES ───
procedure CurPageChanged(CurPageID: Integer);
begin
  if CurPageID = wpWelcome then
  begin
    if IsUpgrade then
      WizardForm.WelcomeLabel2.Caption :=
        'Setup will now update Tama''s Palace from v' + OldVersion + ' to v{#MyAppVersion}.' + #13#10 + #13#10 +
        'Your database, settings, and all user data will be preserved. ' +
        'Only the application files will be replaced.' + #13#10 + #13#10 +
        'Click Next to continue, or Cancel to exit Setup.'
    else
      WizardForm.WelcomeLabel2.Caption :=
        'This will install Tama''s Palace v{#MyAppVersion} on your computer.' + #13#10 + #13#10 +
        'It is recommended that you close all other applications before continuing.' + #13#10 + #13#10 +
        'Click Next to continue, or Cancel to exit Setup.';
  end;

  // On the Ready page, show what will happen
  if CurPageID = wpReady then
  begin
    if IsUpgrade then
      WizardForm.ReadyLabel.Caption :=
        'Setup is ready to update Tama''s Palace.' + #13#10 + #13#10 +
        'Update: v' + OldVersion + '  →  v{#MyAppVersion}' + #13#10 + #13#10 +
        'What will be updated:' + #13#10 +
        '  ✓ Application executable' + #13#10 + #13#10 +
        'What will NOT be changed:' + #13#10 +
        '  ✓ Database (orders, menu items, users)' + #13#10 +
        '  ✓ Settings (branding, theme, currency)' + #13#10 +
        '  ✓ Uploaded images and logos' + #13#10 + #13#10 +
        'Click Install to start the update.';
  end;
end;

// ─── UPDATE FINISH MESSAGE ───
procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    if IsUpgrade then
      WizardForm.FinishedLabel.Caption :=
        'Tama''s Palace has been successfully updated to v{#MyAppVersion}!' + #13#10 + #13#10 +
        'All your data and settings have been preserved.'
    else
      WizardForm.FinishedLabel.Caption :=
        'Tama''s Palace v{#MyAppVersion} has been successfully installed!' + #13#10 + #13#10 +
        'Click Finish to launch the application.';
  end;
end;
