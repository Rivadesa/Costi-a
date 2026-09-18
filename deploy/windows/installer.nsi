; D5.5 — Instalador de Costina para Windows (issue #27, ADR-011).
; El instalador SOLO copia ficheros y llama a comandos del motor (setup-server, stop-services,
; remove-server, first-user): toda la logica vive en Costina.Server.exe, donde se prueba en CI.
; Programa en Program Files; datos SIEMPRE en %ProgramData%\Costina y jamas se tocan al desinstalar.
;
; Modos:  Completo (servidor + puesto)  |  Solo servidor  |  Puesto adicional (aplicacion + CA del servidor)
; Silencioso: Costina-Setup.exe /S /MODE=full|server|client [/TENANT=.. /COMPANY=.. /LOCATION=..] [/CA=ruta\ca.crt]
;             [/RESTORE=ruta\costina-....backup]  (equipo nuevo a partir de una copia; mismo ambito que el original)
Unicode true
!include "MUI2.nsh"
!include "x64.nsh"
!include "LogicLib.nsh"
!include "FileFunc.nsh"
!include "Sections.nsh"
!include "nsDialogs.nsh"
!ifndef VERSION
  !define VERSION "0.0.0-dev"
!endif
!define PRODUCT "Costina"
!define UNINSTALL_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\Costina"

Name "${PRODUCT} ${VERSION}"
OutFile "..\..\artifacts\Costina-Setup-${VERSION}.exe"
InstallDir "$PROGRAMFILES64\Costina"
RequestExecutionLevel admin
SetCompressor /SOLID lzma
ShowInstDetails show

Var Tenant
Var Company
Var Location
Var CaFile
Var RestoreFile
Var FieldRestore
Var Mode
Var ExistingData
Var FieldTenant
Var FieldCompany
Var FieldLocation
Var FieldCa

!define MUI_ABORTWARNING
!define MUI_WELCOMEPAGE_TEXT "Instala Costina en este equipo.$\r$\n$\r$\n- Completo o Solo servidor: el motor del restaurante como servicio de Windows, con su propia base de datos PostgreSQL. Arranca con el equipo, sin iniciar sesion.$\r$\n- Puesto adicional: solo la aplicacion, para conectarse por HTTPS al servidor.$\r$\n$\r$\nLos datos se guardan aparte (ProgramData\Costina) y NUNCA se borran al desinstalar o actualizar. No instala Docker ni herramientas de desarrollo. Build sin firma de editor."
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_COMPONENTS
Page custom ScopePage ScopePageLeave
Page custom CaPage CaPageLeave
!insertmacro MUI_PAGE_INSTFILES
!define MUI_FINISHPAGE_TEXT "Instalacion terminada.$\r$\n$\r$\nSi este equipo es el servidor y es una instalacion nueva, se ha abierto una ventana para crear el primer usuario administrador. Anota la huella de la CA que aparece en LEEME y en el diagnostico: la necesitaras al anadir puestos y tablets."
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "Spanish"

InstType "Completo (servidor y puesto)"
InstType "Solo servidor"
InstType "Puesto adicional"

Section "Motor del restaurante (servicio de Windows + PostgreSQL)" SecServer
  SectionIn 1 2
  ; Actualizacion o reinstalacion: parar servicios ANTES de sustituir binarios.
  ${If} ${FileExists} "$INSTDIR\server\Costina.Server.exe"
    DetailPrint "Deteniendo servicios para actualizar..."
    nsExec::ExecToLog '"$INSTDIR\server\Costina.Server.exe" stop-services'
    Pop $0
  ${EndIf}
  SetOutPath "$INSTDIR\server"
  File /r "..\..\artifacts\package\server\*"
  SetOutPath "$INSTDIR\pgsql"
  File /r "..\..\artifacts\package\pgsql\*"
  System::Call 'Kernel32::SetEnvironmentVariable(t "COSTINA_TENANT", t "$Tenant")'
  System::Call 'Kernel32::SetEnvironmentVariable(t "COSTINA_COMPANY", t "$Company")'
  System::Call 'Kernel32::SetEnvironmentVariable(t "COSTINA_LOCATION", t "$Location")'
  ${If} $RestoreFile != ""
    System::Call 'Kernel32::SetEnvironmentVariable(t "COSTINA_RESTORE_FILE", t "$RestoreFile")'
  ${EndIf}
  DetailPrint "Configurando el servidor (base de datos, certificado, servicio). Puede tardar un minuto..."
  nsExec::ExecToLog '"$INSTDIR\server\Costina.Server.exe" setup-server'
  Pop $0
  ${If} $0 != 0
    DetailPrint "setup-server devolvio $0"
    MessageBox MB_ICONSTOP "No se pudo configurar el servidor (codigo $0). Revisa los detalles. Los datos existentes no se han tocado." /SD IDOK
    SetErrorLevel 2
    Abort
  ${EndIf}
SectionEnd

Section "Aplicacion de puesto (Windows)" SecDesktop
  SectionIn 1 3
  SetOutPath "$INSTDIR\desktop"
  File /r "..\..\artifacts\package\desktop\*"
  SetShellVarContext all
  CreateDirectory "$SMPROGRAMS\Costina"
  CreateShortcut "$SMPROGRAMS\Costina\Costina.lnk" "$INSTDIR\desktop\Costina.Desktop.exe"
  CreateShortcut "$DESKTOP\Costina.lnk" "$INSTDIR\desktop\Costina.Desktop.exe"
SectionEnd

Section "-Comun"
  SetOutPath "$INSTDIR"
  File "..\..\artifacts\package\build-manifest.json"
  File "..\..\artifacts\package\LEEME.txt"
  ; Puesto adicional: confiar en la CA local del servidor (solo una CA de Costina con restricciones de nombre).
  ${IfNot} ${SectionIsSelected} ${SecServer}
  ${AndIf} $CaFile != ""
    InitPluginsDir
    File "/oname=$PLUGINSDIR\import-ca.ps1" "import-ca.ps1"
    nsExec::ExecToLog 'powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$PLUGINSDIR\import-ca.ps1" -Path "$CaFile"'
    Pop $0
    ${If} $0 != 0
      MessageBox MB_ICONSTOP "No se pudo instalar la CA del servidor. La aplicacion queda instalada, pero no validara el HTTPS del servidor hasta instalarla." /SD IDOK
      SetErrorLevel 3
    ${EndIf}
  ${EndIf}
  WriteUninstaller "$INSTDIR\Uninstall.exe"
  WriteRegStr HKLM "${UNINSTALL_KEY}" "DisplayName" "${PRODUCT}"
  WriteRegStr HKLM "${UNINSTALL_KEY}" "DisplayVersion" "${VERSION}"
  WriteRegStr HKLM "${UNINSTALL_KEY}" "Publisher" "Xeitoso (build sin firma de editor)"
  WriteRegStr HKLM "${UNINSTALL_KEY}" "InstallLocation" "$INSTDIR"
  WriteRegStr HKLM "${UNINSTALL_KEY}" "UninstallString" '"$INSTDIR\Uninstall.exe"'
  WriteRegStr HKLM "${UNINSTALL_KEY}" "QuietUninstallString" '"$INSTDIR\Uninstall.exe" /S'
  WriteRegDWORD HKLM "${UNINSTALL_KEY}" "NoModify" 1
  WriteRegDWORD HKLM "${UNINSTALL_KEY}" "NoRepair" 1
  ; Instalacion NUEVA de servidor con interfaz: ventana para crear el primer administrador (contrasena oculta).
  ${If} ${SectionIsSelected} ${SecServer}
  ${AndIf} $ExistingData == "0"
  ${AndIf} $RestoreFile == ""
  ${AndIfNot} ${Silent}
    Exec '"$INSTDIR\server\Costina.Server.exe" first-user'
  ${EndIf}
SectionEnd

Function .onInit
  ${IfNot} ${RunningX64}
    MessageBox MB_ICONSTOP "Costina requiere Windows de 64 bits." /SD IDOK
    Abort
  ${EndIf}
  SetRegView 64
  StrCpy $Tenant "costina"
  StrCpy $Company "restaurante"
  StrCpy $Location "local-1"
  StrCpy $CaFile ""
  StrCpy $RestoreFile ""
  StrCpy $ExistingData "0"
  SetShellVarContext all   ; $APPDATA = C:\ProgramData
  ${If} ${FileExists} "$APPDATA\Costina\config\server.json"
    StrCpy $ExistingData "1"
  ${EndIf}
  ${GetParameters} $R0
  ClearErrors
  ${GetOptions} $R0 "/MODE=" $Mode
  ${IfNot} ${Errors}
    ${If} $Mode == "server"
      SetCurInstType 1
    ${ElseIf} $Mode == "client"
      SetCurInstType 2
    ${Else}
      SetCurInstType 0
    ${EndIf}
  ${EndIf}
  ClearErrors
  ${GetOptions} $R0 "/TENANT=" $R1
  ${IfNot} ${Errors}
    StrCpy $Tenant $R1
  ${EndIf}
  ClearErrors
  ${GetOptions} $R0 "/COMPANY=" $R1
  ${IfNot} ${Errors}
    StrCpy $Company $R1
  ${EndIf}
  ClearErrors
  ${GetOptions} $R0 "/LOCATION=" $R1
  ${IfNot} ${Errors}
    StrCpy $Location $R1
  ${EndIf}
  ClearErrors
  ${GetOptions} $R0 "/RESTORE=" $R1
  ${IfNot} ${Errors}
    StrCpy $RestoreFile $R1
  ${EndIf}
  ClearErrors
  ${GetOptions} $R0 "/CA=" $R1
  ${IfNot} ${Errors}
    StrCpy $CaFile $R1
  ${EndIf}
  ClearErrors
FunctionEnd

; Identificadores del negocio: solo en una instalacion NUEVA de servidor. Deben repetirse IGUALES al
; restaurar una copia en otro equipo, por eso se piden de forma explicita.
Function ScopePage
  ${IfNot} ${SectionIsSelected} ${SecServer}
    Abort
  ${EndIf}
  ${If} $ExistingData == "1"
    Abort
  ${EndIf}
  !insertmacro MUI_HEADER_TEXT "Identificacion del negocio" "Se usan para separar los datos. Anotalos: hacen falta para restaurar una copia en otro equipo."
  nsDialogs::Create 1018
  Pop $0
  ${NSD_CreateLabel} 0 0 100% 12u "Grupo (tenant): minusculas, numeros y guiones"
  Pop $0
  ${NSD_CreateText} 0 13u 100% 12u "$Tenant"
  Pop $FieldTenant
  ${NSD_CreateLabel} 0 32u 100% 12u "Empresa"
  Pop $0
  ${NSD_CreateText} 0 45u 100% 12u "$Company"
  Pop $FieldCompany
  ${NSD_CreateLabel} 0 64u 100% 12u "Local"
  Pop $0
  ${NSD_CreateText} 0 77u 100% 12u "$Location"
  Pop $FieldLocation
  ${NSD_CreateLabel} 0 98u 100% 12u "Opcional: restaurar desde una copia (.backup con su .json). Exige los MISMOS identificadores."
  Pop $0
  ${NSD_CreateFileRequest} 0 111u 100% 12u "$RestoreFile"
  Pop $FieldRestore
  nsDialogs::Show
FunctionEnd

Function ScopePageLeave
  ${NSD_GetText} $FieldTenant $Tenant
  ${NSD_GetText} $FieldCompany $Company
  ${NSD_GetText} $FieldLocation $Location
  ${NSD_GetText} $FieldRestore $RestoreFile
  ${If} $Tenant == ""
  ${OrIf} $Company == ""
  ${OrIf} $Location == ""
    MessageBox MB_ICONEXCLAMATION "Los tres identificadores son obligatorios."
    Abort
  ${EndIf}
FunctionEnd

; Puesto adicional: fichero ca.crt copiado desde el servidor (ProgramData\Costina\tls\ca.crt) y
; confirmacion de su huella frente a la que muestra el servidor.
Function CaPage
  ${If} ${SectionIsSelected} ${SecServer}
    Abort
  ${EndIf}
  !insertmacro MUI_HEADER_TEXT "Confianza en el servidor" "Copia ca.crt desde el servidor (ProgramData\Costina\tls) en un USB. Dejalo vacio para hacerlo mas tarde."
  nsDialogs::Create 1018
  Pop $0
  ${NSD_CreateLabel} 0 0 100% 24u "Ruta del fichero ca.crt del servidor Costina. En el paso siguiente se mostrara su huella SHA-256 para compararla con la del servidor."
  Pop $0
  ${NSD_CreateFileRequest} 0 28u 100% 12u "$CaFile"
  Pop $FieldCa
  nsDialogs::Show
FunctionEnd

Function CaPageLeave
  ${NSD_GetText} $FieldCa $CaFile
  ${If} $CaFile == ""
    Return
  ${EndIf}
  ${IfNot} ${FileExists} "$CaFile"
    MessageBox MB_ICONEXCLAMATION "No existe ese fichero."
    Abort
  ${EndIf}
  InitPluginsDir
  File "/oname=$PLUGINSDIR\import-ca.ps1" "import-ca.ps1"
  nsExec::ExecToStack 'powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$PLUGINSDIR\import-ca.ps1" -Path "$CaFile" -ShowOnly'
  Pop $0
  Pop $1
  ${If} $0 != 0
    MessageBox MB_ICONSTOP "Ese fichero no es una CA local de Costina valida:$\r$\n$1"
    Abort
  ${EndIf}
  MessageBox MB_YESNO|MB_ICONQUESTION "Huella SHA-256 de la CA:$\r$\n$\r$\n$1$\r$\nCompara con la que muestra el servidor. Coincide?" IDYES +2
  Abort
FunctionEnd

Function un.onInit
  SetRegView 64
FunctionEnd

Section "Uninstall"
  MessageBox MB_OKCANCEL|MB_ICONINFORMATION "Se retiraran el programa y los servicios. Los DATOS (base de datos, configuracion, certificados y copias en ProgramData\Costina) se CONSERVAN." /SD IDOK IDOK +2
  Abort
  ${If} ${FileExists} "$INSTDIR\server\Costina.Server.exe"
    nsExec::ExecToLog '"$INSTDIR\server\Costina.Server.exe" remove-server'
    Pop $0
    ${If} $0 != 0
      MessageBox MB_ICONSTOP "No se pudieron retirar los servicios (codigo $0). No se borra nada." /SD IDOK
      SetErrorLevel 2
      Abort
    ${EndIf}
  ${EndIf}
  SetShellVarContext all
  Delete "$DESKTOP\Costina.lnk"
  Delete "$SMPROGRAMS\Costina\Costina.lnk"
  RMDir "$SMPROGRAMS\Costina"
  ; Deliberadamente NUNCA se toca %ProgramData%\Costina.
  RMDir /r "$INSTDIR\server"
  RMDir /r "$INSTDIR\pgsql"
  RMDir /r "$INSTDIR\desktop"
  Delete "$INSTDIR\build-manifest.json"
  Delete "$INSTDIR\LEEME.txt"
  Delete "$INSTDIR\Uninstall.exe"
  RMDir "$INSTDIR"
  DeleteRegKey HKLM "${UNINSTALL_KEY}"
SectionEnd
