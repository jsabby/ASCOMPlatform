;
; Script generated by the ASCOM Driver Installer Script Generator 1.2.0.0
; Generated by Bob Denny on 1/4/2008 (UTC)
; Bob Denny (5.0.2) - Include TheSky Driver-ref.dll and Astro32.dll in sources.
; Bob Denny (5.0.3) - New track offset capability control
; Bob Denny (5.2.4) - Telescope V2, TheSky X, several other improvements
; Modified for 64-bit and Platform > 5 by Bob Denny 16-Sep-09
; Bob Denny (5.2.5) - Fix parking logic, remove disconnect simulation.
; Bob Denny (5.2.6) - Fix another case of crazy coordinates from TheSky 6,
;                     fix TheSky 5 compatibility. Cosmetics.
; Bob Denny (5.2.7) - Dang it, one more fix for TheSky 5.
; Chris R   (5.2.8) - Change to guiding as suggested by Matthew Bisque
; Chris R	(5.2.9) - Another change suggested by MB
;
[Setup]
AppName=ASCOM TheSky Telescope Driver
AppVerName=ASCOM TheSky Telescope Driver 5.2.9
AppVersion=5.2.9
AppPublisher=Bob Denny <rdenny@dc3.com>
AppPublisherURL=mailto:rdenny@dc3.com
AppSupportURL=http://tech.groups.yahoo.com/group/ASCOM-Talk/
AppUpdatesURL=http://ascom-standards.org/
MinVersion=0,5.0.2195sp4
DefaultDirName={cf}\ASCOM\Telescope
DisableDirPage=yes
DisableProgramGroupPage=yes
OutputDir=.
OutputBaseFilename=TheSkyTelescope(5.2.9)Setup
Compression=lzma
SolidCompression=yes
; Put there by Platform if Driver Installer Support selected
;WizardImageFile=C:\Program Files (x86)\ASCOM\InstallGen\Resources\WizardImage.bmp
;LicenseFile=C:\Program Files (x86)\ASCOM\InstallGen\Resources\CreativeCommons.txt
WizardImageFile=C:\Program Files\ASCOM\Platform 6 Developer Components\Installer Generator\Resources\WizardImage.bmp
LicenseFile=C:\Program Files\ASCOM\Platform 6 Developer Components\Installer Generator\Resources\CreativeCommons.txt
; {cf}\ASCOM\Uninstall\Telescope folder created by Platform, always
UninstallFilesDir={cf}\ASCOM\Uninstall\Telescope\TheSky

[Languages]
Name: english; MessagesFile: compiler:Default.isl

[Dirs]
Name: {cf}\ASCOM\Uninstall\Telescope\TheSky
; TODO: Add subfolders below {app} as needed (e.g. Name: "{app}\MyFolder")

;  Add an option to install the source files
[Tasks]
Name: source; Description: Install the Source files; Flags: unchecked

[Files]
; regserver flag only if native COM, not .NET
Source: TheSky Driver.dll; DestDir: {app}; AfterInstall: RegASCOM(); Flags: regserver
; Optional source files (COM and .NET aware)
Source: *; Excludes: *.zip,*.exe,*.dll, \bin\*, \obj\*; DestDir: {app}\Source\TheSky Driver; Tasks: source; Flags: recursesubdirs
Source: TheSky Driver-ref.dll; DestDir: {app}\Source\TheSky Driver; Tasks: source
; TODO: Add other files needed by your driver here (add subfolders above)
Source: astro32.dll; DestDir: {app}; Flags: sharedfile
Source: astro32.dll; DestDir: {app}\Source\TheSky Driver; Tasks: source

[Code]
//
// Before the installer UI appears, verify that the (prerequisite)
// ASCOM Platform 5.x is installed, including both Helper components.
// Helper is required for all types (COM and .NET)!
//
function InitializeSetup(): Boolean;
var
   H : Variant;
   H2 : Variant;
begin
   Result := FALSE;  // Assume failure
   try               // Will catch all errors including missing reg data
      H := CreateOLEObject('DriverHelper.Util');  // Assure both are available
      H2 := CreateOleObject('DriverHelper2.Util');
      if (H2.PlatformVersion >= 5.0) then
         Result := TRUE;
   except
   end;
   if(not Result) then
      MsgBox('The ASCOM Platform 5 or greater is required for this driver.', mbInformation, MB_OK);
end;

//
// Register and unregister the driver with the Chooser
// We already know that the Helper is available
//
procedure RegASCOM();
var
   Helper: Variant;
begin
   Helper := CreateOleObject('DriverHelper.Profile');
   Helper.DeviceType := 'Telescope';
   Helper.Register('TheSky.Telescope', 'TheSky-controlled Telescope');
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
   Helper: Variant;
begin
   if CurUninstallStep = usUninstall then
   begin
     Helper := CreateOleObject('DriverHelper.Profile');
     Helper.DeviceType := 'Telescope';
     Helper.Unregister('TheSky.Telescope');
  end;
end;
