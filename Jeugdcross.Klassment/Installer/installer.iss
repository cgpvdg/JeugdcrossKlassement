#define MyAppName "Jeugdcrosscompetitie"
#ifndef MyAppVersion
 #define MyAppVersion "1.0.10"
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
UninstallDisplayIcon={app}\Jeugdcrosscompetitie.exe
DisableProgramGroupPage=yes
[Languages]
Name: "dutch"; MessagesFile: "compiler:Languages\Dutch.isl"
[Tasks]
Name: "desktopicon"; Description: "Snelkoppeling op bureaublad"; Flags: unchecked
[Files]
Source: "..\artifacts\publish\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion
[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\Jeugdcrosscompetitie.exe"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\Jeugdcrosscompetitie.exe"; Tasks: desktopicon
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
