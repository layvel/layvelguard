# MANIFIESTO DE PARCHE - PARCHE_2026-08-20_v3.4.0

- **Nombre del Parche:** `2026-08-20_agente_cbmw_v3.4.0`
- **Fecha:** 20/08/2026
- **Versión:** 3.4.0
- **Novedades y Cambios:**
  1. **Rediseño de Interfaz Gráfica con Pestañas:**
     - **Pestaña 1 (Mantenimiento y Consola):** Ejecución en 1-clic con la consola de ejecución en vivo corregida y rediseñada.
     - **Pestaña 2 (Estatus del Equipo & Switches):** Panel interactivo con indicadores en vivo `[ 🟢 ACTIVADO ]` / `[ 🔴 DESACTIVADO ]` para cada protección del sistema, con **botones de interruptor individual (On/Off)** para activar o desactivar cada función por separado.
  2. **Bloqueo Web Completo de Steam:**
     - Bloqueo directo de `store.steampowered.com`, `steamcommunity.com` y patrones `*steam*` en Chrome/Edge (`URLBlocklist`) y en el archivo `hosts`.
  3. **Servicio Telemétrico en Segundo Plano (Detector de Encendido):**
     - Registra la tarea `CBMW_Heartbeat_Daemon` que corre silenciosamente sin mostrar ventanas al encender Windows. Envía latidos telemétricos cada 3 minutos a `sistemas.cbmw.cl`.
  4. **Apagado Remoto desde el Dashboard Web:**
     - Botón **"⚡ Apagar PC"** en `dashboard_lab.php` que permite apagar remotamente cualquier equipo dejado encendido (con la pantalla apagada) o en caso de uso no autorizado.

---

## Archivos para subir al hosting (`public_html/sistemas.cbmw.cl/`)

```txt
archivos_para_subir/
├── api/
│   └── lab/
│       ├── dashboard.php            <-- Dashboard web con botón Apagar PC Remoto
│       └── reporte.php              <-- Receptor telemétrico y despachador de comandos
├── bat/
│   ├── index.php                    <-- Descarga directa /bat
│   ├── descargar_e_instalar_cbmw.bat
│   └── Menu_Administracion_CBMW.exe <-- Ejecutable C# Nativo v3.4.0 compilado
├── lab/
│   ├── agente_cbmw_global.ps1       <-- Agente PowerShell v3.4.0
│   └── Menu_Administracion_CBMW.exe
└── lab-config.json                  <-- Configuración global v3.4.0
```
