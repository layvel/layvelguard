# MANIFIESTO DE PARCHE - PARCHE_2026-08-26_v1.5.0

- **Nombre del Parche:** `2026-08-26_layvelguard_v1.5.0_sistemas.cbmw.cl`
- **Fecha:** 26/08/2026
- **Versión:** 1.5.0
- **Novedades y Cambios:**
  1. **Bloqueo Total de Personalización y Tamaño del Mouse:**
     - Restricción estricta (`NoChangingMousePointers`) que impide modificar esquemas de cursor o aumentar el tamaño del puntero desde el Panel de Control y Configuración de Windows.
     - Forzado en tiempo real de punteros Aero predeterminados estándar de 32px y recarga Win32 `SPI_SETCURSORS` sin reiniciar.
     - Nuevo botón de acceso rápido **`[13] Bloquear Personalizacion / Tamano Mouse`** en el menú principal (Pestaña 1).
     - Nuevo interruptor y monitor en vivo en la **Pestaña 2 (Estatus del Equipo & Switches)**.
  2. **Actualización de Configuración Global:**
     - Incorporación del parámetro `"block_mouse_customization": true` en `config.json` y `lab-config.json`.
  3. **Descarga e Instalador Remoto con Anti-Caché:**
     - `bat/index.php` y `descargar_e_instalar_cbmw.bat` actualizados con parámetros anti-caché (`?v=%RANDOM%`) para garantizar la descarga inmediata de la versión v1.5.0 más reciente.

---

## Archivos para subir al hosting (`public_html/sistemas.cbmw.cl/`)

```txt
archivos_para_subir/
├── api/
│   └── lab/
│       ├── dashboard.php            <-- Dashboard web con telemetría y control
│       └── reporte.php              <-- Receptor telemétrico y despachador
├── bat/
│   ├── index.php                    <-- Descarga directa /bat con anti-caché
│   ├── descargar_e_instalar_cbmw.bat<-- Instalador remoto v1.5.0
│   └── LayvelGuard.exe              <-- Binario compilado nativo C# v1.5.0
├── lab/
│   └── LayvelGuard.exe              <-- Binario nativo v1.5.0 para clientes
└── lab-config.json                  <-- Configuración global v1.5.0
```
