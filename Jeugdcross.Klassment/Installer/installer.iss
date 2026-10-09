#define MyAppName "Jeugdcross Klassement"
#ifndef MyAppVersion
 #define MyAppVersion "1.0.5"
#endif
[Setup]
AppId={{DDF673F8-3267-45AA-A387-7C4969E87202}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
DefaultDirName={localappdata}\Programs\Jeugdcross.Klassment
DefaultGroupName={#MyAppName}
PrivilegesRequired=lowest
OutputDir=..\artifacts\installer
OutputBaseFilename=Jeugdcross.Klassment-Setup-{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
SetupIconFile=..\Assets\app.ico
UninstallDisplayIcon={app}\Jeugdcross.Klassment.exe
DisableProgramGroupPage=yes
[Languages]
Name: "dutch"; MessagesFile: "compiler:Languages\Dutch.isl"
[Tasks]
Name: "desktopicon"; Description: "Snelkoppeling op bureaublad"; Flags: unchecked
[Files]
Source: "..\artifacts\publish\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion
[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\Jeugdcross.Klassment.exe"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\Jeugdcross.Klassment.exe"; Tasks: desktopicon
[Run]
Filename: "{app}\Jeugdcross.Klassment.exe"; Description: "Start {#MyAppName}"; Flags: nowait postinstall skipifsilent

