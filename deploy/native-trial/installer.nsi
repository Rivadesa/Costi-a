Unicode true
!include "MUI2.nsh"
!include "x64.nsh"
Name "Costina - Ensayo nativo D1.3"
OutFile "..\..\artifacts\Costina-D1.3-Setup.exe"
InstallDir "$LOCALAPPDATA\Programs\Xeitoso\CostinaTrialD13"
RequestExecutionLevel user
SetCompressor /SOLID lzma
!define MUI_ABORTWARNING
!define MUI_WELCOMEPAGE_TEXT "Instala la aplicación WPF, el motor local y PostgreSQL nativo para un ensayo con datos ficticios en este PC.$\r$\n$\r$\nNo instala Docker ni herramientas de desarrollo. No sustituye Verial, no migra datos y todavía no registra servicios de inicio automático en Windows."
!define MUI_FINISHPAGE_RUN "$INSTDIR\launcher\Costina.Launcher.exe"
!define MUI_FINISHPAGE_RUN_TEXT "Abrir asistente de prueba local"
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "Spanish"
Function .onInit
 ${IfNot} ${RunningX64}
  MessageBox MB_ICONSTOP "Requiere Windows de 64 bits."
  Abort
 ${EndIf}
FunctionEnd
Section "Aplicación y motor de ensayo"
 IfFileExists "$INSTDIR\trial-package.json" 0 NewInstall
  MessageBox MB_ICONSTOP "Esta carpeta ya contiene D1.3. Cierra la prueba y desinstala el programa antes de reinstalar. Los datos del usuario se conservan. No hay actualización automática todavía."
  Abort
 NewInstall:
 SetOutPath "$INSTDIR"
 File /r "..\..\artifacts\trial-package\*"
 CreateDirectory "$SMPROGRAMS\Costina Ensayo"
 CreateShortcut "$SMPROGRAMS\Costina Ensayo\Costina D1.3.lnk" "$INSTDIR\launcher\Costina.Launcher.exe"
 CreateShortcut "$DESKTOP\Costina D1.3.lnk" "$INSTDIR\launcher\Costina.Launcher.exe"
 WriteUninstaller "$INSTDIR\Uninstall.exe"
 WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\CostinaTrialD13" "DisplayName" "Costina - Ensayo nativo D1.3"
 WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\CostinaTrialD13" "DisplayVersion" "0.2.1"
 WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\CostinaTrialD13" "UninstallString" '$"$INSTDIR\Uninstall.exe$"'
 WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\CostinaTrialD13" "Publisher" "Xeitoso (build de ensayo sin firma)"
SectionEnd
Section "Uninstall"
 MessageBox MB_OKCANCEL "Cierra los puestos y detén la instalación desde el asistente. Se retirarán los programas; se conservarán los datos en LocalAppData\Xeitoso\CostinaTrialD13." /SD IDOK IDOK Continue
 Abort
 Continue:
 Delete "$DESKTOP\Costina D1.3.lnk"
 Delete "$SMPROGRAMS\Costina Ensayo\Costina D1.3.lnk"
 RMDir "$SMPROGRAMS\Costina Ensayo"
 ; Deliberately never remove the separate data directory.
 RMDir /r "$INSTDIR\desktop"
 RMDir /r "$INSTDIR\server"
 RMDir /r "$INSTDIR\launcher"
 RMDir /r "$INSTDIR\pgsql"
 Delete "$INSTDIR\trial-package.json"
 Delete "$INSTDIR\build-manifest.json"
 Delete "$INSTDIR\LEEME.txt"
 Delete "$INSTDIR\Uninstall.exe"
 RMDir "$INSTDIR"
 DeleteRegKey HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\CostinaTrialD13"
SectionEnd
