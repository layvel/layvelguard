# 🛡️ LayvelGuard Pro (v1.5.0)

**LayvelGuard Pro** es una suite independiente de administración, control, telemetría y mantenimiento para estaciones de trabajo y laboratorios Windows.

![LayvelGuard Logo](layvelguard_logo.png)

---

## 🚀 Características Principales

- **Desinstalador Seleccionable Interactivo:** Escaneo profundo de Registro (`HKLM`, `HKCU`, `HKEY_USERS`) y carpetas `AppData` en todos los perfiles de usuario.
- **Limpieza de Registro Huérfano:** Elimina rastros y entradas residuales del Registro de Windows tras desinstalar aplicaciones.
- **Filtro de Navegadores & Apps por Defecto:** Permite exclusivamente **Google Chrome** (y Edge), asignándolo como navegador predeterminado y desinstalando browsers no autorizados.
- **Enrutado y Filtro de Ofimática:** Forzado automático de **Microsoft Office** (Word, Excel, PowerPoint) para documentos e imposición como suite por defecto, purgando editores de terceros (*LibreOffice, WPS Office, OpenOffice, OnlyOffice*) y manteniendo autorizados **Nitro PDF** y Adobe Reader.
- **Bloqueo de Personalización de Mouse y Punteros:** Restricción estricta (`NoChangingMousePointers`) contra cambios de esquemas de cursor o agrandamiento de tamaño, forzando cursores aero estándar y recarga dinámica Win32 (`SPI_SETCURSORS`).
- **Filtro de Antivirus:** Mantiene activo únicamente **Windows Defender**, purgando antivirus terceros y limpiadores.
- **Restricción de Cuentas:** Bloquea el inicio de sesión con cuentas Microsoft/Escuela, forzando cuentas locales.
- **Filtro Web & DNS:** Bloquea dominios de juegos (Steam, Roblox, Minecraft) vía políticas Chrome/Edge y archivo `hosts`.
- **Auto-Update Cloud:** Actualización silenciosa directa desde GitHub sin necesidad de servidores ni hosting web.

---

## 🛠️ Estructura del Repositorio

- `LayvelGuard.exe`: Ejecutable nativo con icono embebido de Zote.
- `LayvelGuard.cs`: Código fuente completo en C# (.NET 4.8).
- `config.json`: Configuración remota para despliegue y auto-actualización.
- `layvelguard_logo.png` / `layvelguard_icon.ico`: Recursos gráficos oficiales.

---

*Desarrollado por Layvel - 2026*
