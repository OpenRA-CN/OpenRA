; Copyright 2007-2022 OpenRA developers (see AUTHORS)
; This file is part of OpenRA.
;
;  OpenRA is free software: you can redistribute it and/or modify
;  it under the terms of the GNU General Public License as published by
;  the Free Software Foundation, either version 3 of the License, or
;  (at your option) any later version.
;
;  OpenRA is distributed in the hope that it will be useful,
;  but WITHOUT ANY WARRANTY; without even the implied warranty of
;  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
;  GNU General Public License for more details.
;
;  You should have received a copy of the GNU General Public License
;  along with OpenRA.  If not, see <http://www.gnu.org/licenses/>.

!include "MUI2.nsh"
!include "FileFunc.nsh"
!include "WordFunc.nsh"

Name "OpenMeow"
OutFile "${OUTFILE}"

ManifestDPIAware true

Unicode True

Function .onInit
	!ifndef USE_PROGRAMFILES32
		SetRegView 64
	!endif
	ReadRegStr $INSTDIR HKLM "Software\OpenMeow${SUFFIX}" "InstallDir"
	StrCmp $INSTDIR "" unset done
	unset:
	!ifndef USE_PROGRAMFILES32
		StrCpy $INSTDIR "$PROGRAMFILES64\OpenMeow${SUFFIX}"
	!else
		StrCpy $INSTDIR "$PROGRAMFILES32\OpenMeow${SUFFIX}"
	!endif
	done:
FunctionEnd

SetCompressor lzma
RequestExecutionLevel admin

!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_LICENSE "${SRCDIR}\COPYING"
!insertmacro MUI_PAGE_DIRECTORY

!define MUI_STARTMENUPAGE_REGISTRY_ROOT "HKLM"
!define MUI_STARTMENUPAGE_REGISTRY_KEY "Software\OpenMeow${SUFFIX}"
!define MUI_STARTMENUPAGE_REGISTRY_VALUENAME "Start Menu Folder"
!define MUI_STARTMENUPAGE_DEFAULTFOLDER "OpenMeow"

Var StartMenuFolder
!insertmacro MUI_PAGE_STARTMENU Application $StartMenuFolder

!insertmacro MUI_PAGE_COMPONENTS
!insertmacro MUI_PAGE_INSTFILES

!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_UNPAGE_FINISH

!insertmacro MUI_LANGUAGE "English"

!define TS_DISCORDID 699223250181292033

;***************************
;Section Definitions
;***************************
Section "-Reg" Reg

	; Installation directory
	WriteRegStr HKLM "Software\OpenMeow${SUFFIX}" "InstallDir" $INSTDIR

	; Join server URL Scheme
	WriteRegStr HKLM "Software\Classes\openmeow-ts-${TAG}" "" "URL:Join OpenMeow server"
	WriteRegStr HKLM "Software\Classes\openmeow-ts-${TAG}" "URL Protocol" ""
	WriteRegStr HKLM "Software\Classes\openmeow-ts-${TAG}\DefaultIcon" "" "$INSTDIR\ts.ico,0"
	WriteRegStr HKLM "Software\Classes\openmeow-ts-${TAG}\Shell\Open\Command" "" "$INSTDIR\TiberianSun.exe Launch.URI=%1"

	WriteRegStr HKLM "Software\Classes\discord-${TS_DISCORDID}" "" "URL:Run game ${TS_DISCORDID} protocol"
	WriteRegStr HKLM "Software\Classes\discord-${TS_DISCORDID}" "URL Protocol" ""
	WriteRegStr HKLM "Software\Classes\discord-${TS_DISCORDID}\DefaultIcon" "" "$INSTDIR\ts.ico,0"
	WriteRegStr HKLM "Software\Classes\discord-${TS_DISCORDID}\Shell\Open\Command" "" "$INSTDIR\TiberianSun.exe"

	; Remove obsolete file associations
	DeleteRegKey HKLM "Software\Classes\.orarep"
	DeleteRegKey HKLM "Software\Classes\OpenMeow_replay"
	DeleteRegKey HKLM "Software\Classes\.oramod"
	DeleteRegKey HKLM "Software\Classes\OpenMeow_mod"
	DeleteRegKey HKLM "Software\Classes\openmeow"

SectionEnd

Section "Game" GAME
	SectionIn RO

	RMDir /r "$INSTDIR\mods"
	SetOutPath "$INSTDIR\mods"
	File /r "${SRCDIR}\mods\common"
	File /r "${SRCDIR}\mods\ts"
	File /r "${SRCDIR}\mods\modcontent"

	SetOutPath "$INSTDIR"
	File "${SRCDIR}\*.exe"
	File "${SRCDIR}\*.dll.config"
	File "${SRCDIR}\*.dll"
	File "${SRCDIR}\*.ico"
	File "${SRCDIR}\*.deps.json"
	File "${SRCDIR}\*.runtimeconfig.json"
	File "${SRCDIR}\global mix database.dat"
	File "${SRCDIR}\IP2LOCATION-LITE-DB1.IPV6.BIN.ZIP"
	File "${SRCDIR}\VERSION"
	File "${SRCDIR}\AUTHORS"
	File "${SRCDIR}\COPYING"

	!insertmacro MUI_STARTMENU_WRITE_BEGIN Application
		CreateDirectory "$SMPROGRAMS\$StartMenuFolder"
		CreateShortCut "$SMPROGRAMS\$StartMenuFolder\Tiberian Sun${SUFFIX}.lnk" $OUTDIR\TiberianSun.exe "" \
			"$OUTDIR\TiberianSun.exe" "" "" "" ""
	!insertmacro MUI_STARTMENU_WRITE_END

	SetOutPath "$INSTDIR\lua"
	File "${SRCDIR}\lua\*.lua"

	SetOutPath "$INSTDIR\glsl"
	File "${SRCDIR}\glsl\*.frag"
	File "${SRCDIR}\glsl\*.vert"
	File "${SRCDIR}\glsl\*.glsl"

	; Estimated install size for the control panel properties
	${GetSize} "$INSTDIR" "/S=0K" $0 $1 $2
	IntFmt $0 "0x%08X" $0
	WriteRegDWORD HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\OpenMeow${SUFFIX}" "EstimatedSize" "$0"

	SetShellVarContext all
	CreateDirectory "$APPDATA\OpenMeow\ModMetadata"
	SetOutPath "$INSTDIR"
	nsExec::ExecToLog '"$INSTDIR\OpenRA.Utility.exe" ts --register-mod "$INSTDIR\TiberianSun.exe" system'
	nsExec::ExecToLog '"$INSTDIR\OpenRA.Utility.exe" ts --clear-invalid-mod-registrations system'
	SetShellVarContext current

SectionEnd

Section "Desktop Shortcut" DESKTOPSHORTCUT
	SetOutPath "$INSTDIR"
	CreateShortCut "$DESKTOP\OpenMeow - Tiberian Sun${SUFFIX}.lnk" $INSTDIR\TiberianSun.exe "" \
		"$INSTDIR\TiberianSun.exe" "" "" "" ""
SectionEnd

;***************************
;Uninstaller Sections
;***************************
Section "-Uninstaller"
	WriteUninstaller $INSTDIR\uninstaller.exe
	WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\OpenMeow${SUFFIX}" "DisplayName" "OpenMeow${SUFFIX}"
	WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\OpenMeow${SUFFIX}" "UninstallString" "$INSTDIR\uninstaller.exe"
	WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\OpenMeow${SUFFIX}" "QuietUninstallString" "$\"$INSTDIR\uninstaller.exe$\" /S"
	WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\OpenMeow${SUFFIX}" "InstallLocation" "$INSTDIR"
	WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\OpenMeow${SUFFIX}" "DisplayIcon" "$INSTDIR\ts.ico"
	WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\OpenMeow${SUFFIX}" "Publisher" "OpenMeow developers"
	WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\OpenMeow${SUFFIX}" "URLInfoAbout" "http://openra.net"
	WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\OpenMeow${SUFFIX}" "DisplayVersion" "${TAG}"
	WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\OpenMeow${SUFFIX}" "NoModify" "1"
	WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\OpenMeow${SUFFIX}" "NoRepair" "1"
SectionEnd

!macro Clean UN
Function ${UN}Clean
	nsExec::ExecToLog '"$INSTDIR\OpenRA.Utility.exe" ts --unregister-mod system'

	RMDir /r $INSTDIR\mods
	RMDir /r $INSTDIR\maps
	RMDir /r $INSTDIR\glsl
	RMDir /r $INSTDIR\lua
	Delete $INSTDIR\*.exe
	Delete $INSTDIR\*.dll
	Delete $INSTDIR\*.ico
	Delete $INSTDIR\*.dll.config
	Delete $INSTDIR\*.deps.json
	Delete $INSTDIR\*.runtimeconfig.json
	Delete $INSTDIR\VERSION
	Delete $INSTDIR\AUTHORS
	Delete $INSTDIR\COPYING
	Delete "$INSTDIR\global mix database.dat"
	Delete $INSTDIR\IP2LOCATION-LITE-DB1.IPV6.BIN.ZIP

	RMDir /r $INSTDIR\Support

	DeleteRegKey HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\OpenMeow${SUFFIX}"
	DeleteRegKey HKLM "Software\Classes\openmeow-ts-${TAG}"

	DeleteRegKey HKLM "Software\Classes\discord-${TS_DISCORDID}"

	Delete $INSTDIR\uninstaller.exe
	RMDir $INSTDIR

	!insertmacro MUI_STARTMENU_GETFOLDER Application $StartMenuFolder

	; Clean up start menu: Delete all our icons, and the OpenRA folder
	; *only* if we were the only installed version
	Delete "$SMPROGRAMS\$StartMenuFolder\Tiberian Sun${SUFFIX}.lnk"
	RMDir "$SMPROGRAMS\$StartMenuFolder"

	Delete "$DESKTOP\OpenMeow - Tiberian Sun${SUFFIX}.lnk"
	DeleteRegKey HKLM "Software\OpenMeow${SUFFIX}"
FunctionEnd
!macroend

!insertmacro Clean ""
!insertmacro Clean "un."

Section "Uninstall"
	Call un.Clean
SectionEnd

;***************************
;Section Descriptions
;***************************
LangString DESC_GAME ${LANG_ENGLISH} "OpenMeow engine, official mods and dependencies"
LangString DESC_DESKTOPSHORTCUT ${LANG_ENGLISH} "Place shortcut on the Desktop."

!insertmacro MUI_FUNCTION_DESCRIPTION_BEGIN
	!insertmacro MUI_DESCRIPTION_TEXT ${GAME} $(DESC_GAME)
	!insertmacro MUI_DESCRIPTION_TEXT ${DESKTOPSHORTCUT} $(DESC_DESKTOPSHORTCUT)
!insertmacro MUI_FUNCTION_DESCRIPTION_END

;***************************
;Callbacks
;***************************

Function .onInstFailed
	Call Clean
FunctionEnd
