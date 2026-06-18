#define MyAppName      "WinMD"
#define MyAppVersion   "1.0"
#define MyAppPublisher "tmfgroup"
#define MyAppExeName   "WinMD.exe"
#define MyAppId        "{B7C2E4F1-3A8D-4B6E-9F2C-1D5E8A7B0C3F}"
#define PublishDir     "..\bin\Release\net10.0-windows10.0.19041.0\win-x64\publish"
#define IconFile       "..\icon.ico"

#define DotNetUrl      "https://aka.ms/dotnet/10.0/windowsdesktop-runtime-win-x64.exe"
#define DotNetFile     "dotnet-runtime-10.0-win-x64.exe"
#define WasdkUrl       "https://aka.ms/windowsappsdk/1.8/latest/windowsappruntimeinstall-x64.exe"
#define WasdkFile      "windowsappruntimeinstall-x64.exe"

[Setup]
AppId={{#MyAppId}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={localappdata}\Programs\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
OutputDir=.
OutputBaseFilename=WinMD-Setup
SetupIconFile={#IconFile}
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
MinVersion=10.0.19041
CloseApplications=yes
CloseApplicationsFilter=WinMD.exe
RestartApplications=no
; Podpisywanie kodu — narzędzie "winmdsign" jest definiowane przez ISCC (/S...),
; patrz installer\build.ps1. Podpisujemy też deinstalator.
SignTool=winmdsign
SignedUninstaller=yes

[Languages]
Name: "polish";  MessagesFile: "compiler:Languages\Polish.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}";                          Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\icon.ico"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}";    Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}";                    Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\icon.ico"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Classes\.md";                         ValueType: string; ValueName: ""; ValueData: "WinMD.md";     Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\WinMD.md";                    ValueType: string; ValueName: ""; ValueData: "Markdown file"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\WinMD.md\DefaultIcon";        ValueType: string; ValueName: ""; ValueData: "{app}\icon.ico,0"
Root: HKCU; Subkey: "Software\Classes\WinMD.md\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\WinMD.exe"" ""%1"""

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
var
  DownloadPage: TDownloadWizardPage;

// ─── .NET 10 ───────────────────────────────────────────────────────────────

function IsDotNet10Installed(): Boolean;
var
  FindRec: TFindRec;
  RuntimePath: string;
begin
  RuntimePath := GetEnv('ProgramFiles') + '\dotnet\shared\Microsoft.NETCore.App';
  Result := FindFirst(RuntimePath + '\10.*', FindRec);
  if Result then FindClose(FindRec);
end;

// ─── Windows App SDK 1.8 ───────────────────────────────────────────────────

function IsWasdkInstalled(): Boolean;
var
  SubKeyNames: TArrayOfString;
  i: Integer;
  RegBase: string;
begin
  Result := False;
  // Szukamy "Microsoft.WindowsAppRuntime.1.8_*" w katalogu pakietów użytkownika
  RegBase := 'Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\Repository\Packages';
  if RegGetSubkeyNames(HKCU, RegBase, SubKeyNames) then
    for i := 0 to GetArrayLength(SubKeyNames) - 1 do
      if Pos('Microsoft.WindowsAppRuntime.1.8', SubKeyNames[i]) > 0 then
      begin
        Result := True;
        Exit;
      end;
  // Fallback: pakiet systemowy (HKLM)
  RegBase := 'Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\Repository\Packages';
  if RegGetSubkeyNames(HKLM, RegBase, SubKeyNames) then
    for i := 0 to GetArrayLength(SubKeyNames) - 1 do
      if Pos('Microsoft.WindowsAppRuntime.1.8', SubKeyNames[i]) > 0 then
      begin
        Result := True;
        Exit;
      end;
end;

// ─── Kreator pobierania ────────────────────────────────────────────────────

procedure InitializeWizard;
begin
  DownloadPage := CreateDownloadPage(
    'Pobieranie wymaganych składników',
    'Trwa pobieranie wymaganych składników. Proszę czekać...',
    nil);
end;

function NextButtonClick(CurPageID: Integer): Boolean;
var
  NeedDownload: Boolean;
begin
  Result := True;
  if CurPageID <> wpReady then Exit;

  DownloadPage.Clear;
  NeedDownload := False;

  if not IsDotNet10Installed() then
  begin
    DownloadPage.Add('{#DotNetUrl}', '{#DotNetFile}', '');
    NeedDownload := True;
  end;

  if not IsWasdkInstalled() then
  begin
    DownloadPage.Add('{#WasdkUrl}', '{#WasdkFile}', '');
    NeedDownload := True;
  end;

  if not NeedDownload then Exit;

  DownloadPage.Show;
  try
    try
      DownloadPage.Download;
    except
      SuppressibleMsgBox(
        'Nie udało się pobrać wymaganych składników.' + #13#10 + GetExceptionMessage,
        mbCriticalError, MB_OK, IDOK);
      Result := False;
    end;
  finally
    DownloadPage.Hide;
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
  Path: string;
begin
  Result := '';

  if not IsDotNet10Installed() then
  begin
    Path := ExpandConstant('{tmp}\{#DotNetFile}');
    if FileExists(Path) then
    begin
      if not ShellExec('runas', Path, '/install /quiet /norestart', '', SW_SHOW, ewWaitUntilTerminated, ResultCode) then
        Result := 'Nie można zainstalować .NET 10: ' + SysErrorMessage(ResultCode)
      else if (ResultCode = 3010) or (ResultCode = 1641) then
        NeedsRestart := True;
    end;
  end;

  if not IsWasdkInstalled() then
  begin
    Path := ExpandConstant('{tmp}\{#WasdkFile}');
    if FileExists(Path) then
    begin
      if not ShellExec('', Path, '--quiet', '', SW_SHOW, ewWaitUntilTerminated, ResultCode) then
        Result := 'Nie można zainstalować Windows App SDK: ' + SysErrorMessage(ResultCode)
      else if (ResultCode = 3010) or (ResultCode = 1641) then
        NeedsRestart := True;
    end;
  end;
end;
