#define MyAppName "Jeugdcrosscompetitie"
#ifndef MyAppVersion
 #define MyAppVersion "1.0.16"
#endif
[Setup]
AppId={{DDF673F8-3267-45AA-A387-7C4969E87202}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
DefaultDirName={localappdata}\Programs\Jeugdcross.Klassment
DefaultGroupName={#MyAppName}
PrivilegesRequired=lowest
OutputDir=..\artifacts\installer
OutputBaseFilename=Jeugdcrosscompetitie-Setup-{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
SetupIconFile=..\Assets\app.ico
UninstallDisplayIcon={app}\Icons\app-{#MyAppVersion}.ico
DisableProgramGroupPage=yes
[Languages]
Name: "dutch"; MessagesFile: "compiler:Languages\Dutch.isl"
[Tasks]
Name: "desktopicon"; Description: "Snelkoppeling op bureaublad"; Flags: unchecked
[Files]
Source: "..\artifacts\publish\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion
[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\Jeugdcrosscompetitie.exe"; IconFilename: "{app}\Icons\app-{#MyAppVersion}.ico"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\Jeugdcrosscompetitie.exe"; IconFilename: "{app}\Icons\app-{#MyAppVersion}.ico"; Check: ShouldUpdateDesktopIcon
[InstallDelete]
Type: files; Name: "{app}\Jeugdcross.Klassment.exe"
Type: files; Name: "{app}\Jeugdcross.Klassment.dll"
Type: files; Name: "{app}\Jeugdcross.Klassment.pdb"
Type: files; Name: "{app}\Jeugdcross.Klassment.deps.json"
Type: files; Name: "{app}\Jeugdcross.Klassment.runtimeconfig.json"
Type: files; Name: "{autoprograms}\Jeugdcross Klassement.lnk"
Type: files; Name: "{autodesktop}\Jeugdcross Klassement.lnk"
[Run]
Filename: "{app}\Jeugdcrosscompetitie.exe"; Description: "Start {#MyAppName}"; Flags: nowait postinstall skipifsilent

[Code]
procedure SHChangeNotify(EventId: Integer; Flags: Cardinal; Item1, Item2: Integer);
 external 'SHChangeNotify@shell32.dll stdcall';

function ShouldUpdateDesktopIcon: Boolean;
begin
 Result := WizardIsTaskSelected('desktopicon') or
   FileExists(ExpandConstant('{autodesktop}\{#MyAppName}.lnk'));
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
 FindRec: TFindRec;
 PinnedFolder, Target, LinkPath: String;
 Shell, Link: Variant;
begin
 if CurStep <> ssPostInstall then Exit;
 { Update only existing pinned shortcuts pointing to this installed application. }
 PinnedFolder := ExpandConstant('{userappdata}\Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar\');
 if FindFirst(PinnedFolder + '*.lnk', FindRec) then begin
  try
   Shell := CreateOleObject('WScript.Shell');
   repeat
    try
     LinkPath := PinnedFolder + FindRec.Name;
     Link := Shell.CreateShortcut(LinkPath);
     Target := Link.TargetPath;
     if CompareText(Target, ExpandConstant('{app}\Jeugdcrosscompetitie.exe')) = 0 then begin
      Link.IconLocation := ExpandConstant('{app}\Icons\app-{#MyAppVersion}.ico,0');
      Link.Save;
     end;
    except
     Log('Pinned shortcut icon could not be refreshed: ' + FindRec.Name);
    end;
   until not FindNext(FindRec);
  finally FindClose(FindRec); end;
 end;
 SHChangeNotify($08000000, 0, 0, 0);
end;
