#define MyAppName "Jeugd crosscompetitie"
#ifndef MyAppVersion
  #define MyAppVersion "1.0.0"
#endif
#define MyAppPublisher "Jeugdcross"
#define MyAppExeName "jeugdcrossdata.exe"
#define MySourceDir "..\\artifacts\\publish"

[Setup]
AppId={{A9FFB2B0-19F2-4A56-8B58-6C8C4D0B3910}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\\Jeugd crosscompetitie
DefaultGroupName=Jeugd crosscompetitie
OutputDir=..\\artifacts\\installer
OutputBaseFilename=Jeugdcrosscompetitie_Installer_{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
SetupIconFile=setup-icon.ico
WizardImageFile=wizard.bmp
WizardSmallImageFile=wizard-small.bmp
UninstallDisplayIcon={app}\\{#MyAppExeName}
DisableProgramGroupPage=yes

[Languages]
Name: "dutch"; MessagesFile: "compiler:Languages\\Dutch.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Snelkoppeling op bureaublad"; GroupDescription: "Extra snelkoppelingen:"; Flags: unchecked

[Files]
Source: "{#MySourceDir}\\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion
Source: "setup-icon.ico"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\\Jeugd crosscompetitie"; Filename: "{app}\\{#MyAppExeName}"; IconFilename: "{app}\\setup-icon.ico"
Name: "{autodesktop}\\Jeugd crosscompetitie"; Filename: "{app}\\{#MyAppExeName}"; IconFilename: "{app}\\setup-icon.ico"; Tasks: desktopicon

[Run]
Filename: "{app}\\{#MyAppExeName}"; Description: "Start Jeugd crosscompetitie"; Flags: nowait postinstall skipifsilent
