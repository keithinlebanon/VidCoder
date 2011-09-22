; Setup for VidCoder. NOTE: To build this installer you must first build
;  VidCoder.sln in Release|x86 mode.

[Setup]
AppName=VidCoder
AppVerName=VidCoder 1.0.4 (x86)

DefaultDirName={pf}\VidCoder
DisableProgramGroupPage=yes
;DisableReadyPage=yes
UninstallDisplayIcon={app}\VidCoder.exe
Compression=lzma
SolidCompression=yes

OutputDir=BuiltInstallers
OutputBaseFilename=VidCoder-1.0.4-x86

AppId=VidCoder
UsePreviousAppDir=yes

[Languages]
Name: "en"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "..\VidCoder\bin\x86\Release\VidCoder.exe"; DestDir: "{app}"
Source: "..\VidCoder\bin\x86\Release\VidCoder.pdb"; DestDir: "{app}"
Source: "..\VidCoder\bin\x86\Release\VidCoder.exe.config"; DestDir: "{app}"
Source: "..\VidCoder\bin\x86\Release\VidCoder.XmlSerializers.dll"; DestDir: "{app}"
Source: "..\Lib\Ookii.Dialogs.Wpf.dll"; DestDir: "{app}"
Source: "..\Lib\Ookii.Dialogs.Wpf.pdb"; DestDir: "{app}"
Source: "..\Lib\Microsoft.Practices.Unity.dll"; DestDir: "{app}"
Source: "..\Lib\Hardcodet.Wpf.TaskbarNotification.dll"; DestDir: "{app}"
Source: "..\VidCoder\BuiltInPresets.xml"; DestDir: "{app}"
Source: "..\Lib\x86\hb.dll"; DestDir: "{app}"
Source: "..\Lib\x86\System.Data.SQLite.dll"; DestDir: "{app}"
Source: "..\Lib\x86\HandBrakeInterop.dll"; DestDir: "{app}"
Source: "..\Lib\x86\HandBrakeInterop.pdb"; DestDir: "{app}"
Source: "..\Lib\x86\HandBrakeInterop.XmlSerializers.dll"; DestDir: "{app}"
Source: "..\License.txt"; DestDir: "{app}"

[Messages]
WelcomeLabel2=This will install [name/ver] on your computer.

[Icons]
Name: "{commonprograms}\VidCoder"; Filename: "{app}\VidCoder.exe"; WorkingDir: "{app}"

[Run]
Filename: "{app}\VidCoder.exe"; Description: "Run VidCoder"; Flags: postinstall shellexec

[UninstallDelete]
Type: filesandordirs; Name: "{userappdata}\VidCoder\Updates"

[CustomMessages]
dotnetmissing=VidCoder requires Microsoft .NET Framework 4, which is not installed. Would you like to download it now?

[Code]

function InitializeSetup(): Boolean;
var
  Version: TWindowsVersion;
  netFrameWorkInstalled : Boolean;
  isInstalled: Cardinal;
  ErrorCode: Integer;
begin
	GetWindowsVersionEx(Version);
	
	if (Version.Major < 5) or ((Version.Major = 5) and (Version.Minor < 1)) or ((Version.Major = 5) and (Version.Minor = 1) and (Version.ServicePackMajor < 2)) then
	begin
    MsgBox('VidCoder cannot install on your operating system.', mbError, MB_OK);
    exit;
	end;
	
  result := true;

  isInstalled := 0;
  netFrameworkInstalled := RegQueryDWordValue(HKLM, 'Software\Microsoft\NET Framework Setup\NDP\v4\Client', 'Install', isInstalled);
  if ((netFrameworkInstalled)  and (isInstalled <> 1)) then netFrameworkInstalled := false;

  if netFrameworkInstalled = false then
  begin
    if (MsgBox(ExpandConstant('{cm:dotnetmissing}'),
        mbConfirmation, MB_YESNO) = idYes) then
    begin
      ShellExec('open',
      'http://www.microsoft.com/downloads/details.aspx?FamilyID=e5ad0459-cbcc-4b4f-97b6-fb17111cf544',
      '','',SW_SHOWNORMAL,ewNoWait,ErrorCode);
    end;
    result := false;
  end;
end;

// Delete app data
procedure DeleteUserFiles();
var
  iAns: Integer;
begin
  DelTree(ExpandConstant('{userappdata}') + '\VidCoder\Updates', True, True, True);

  iAns := MsgBox('Do you want to delete your settings and presets as well?', mbConfirmation, MB_YESNO or MB_DEFBUTTON2);

  if iAns = IDYES then
  begin
    DelTree(ExpandConstant('{userappdata}') + '\VidCoder', True, True, True);
    DelTree(ExpandConstant('{localappdata}') + '\VidCoder', True, True, True);
  end; 
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  case CurUninstallStep of
    usUninstall:
      begin
        DeleteUserFiles;
      end;
  end;
end;




















