# MANIFIESTO DE PARCHE - PARCHE_2026-08-19_v2.0.0

- **Nombre del Parche:** `2026-08-19_1235_agente_cbmw_v2.0.0`
- **Fecha:** 19/08/2026 12:35
- **Versión:** 2.0.0
- **Motivo del Cambio:**
  1. Habilitación de descarga directa por web desde `sistemas.cbmw.cl/bat`.
  2. Solución a errores de ejecucion en CMD (Sintaxis ASCII pura sin `#`).
  3. Soporte para descarga nativa ultra-rápida con `curl.exe` y bypass de caché HTTP.
  4. Adición de acceso directo a UMaximo (`https://www.umaximo.com/`) con comprobación anti-duplicados en el escritorio.
  5. Dashboard visual telemétrico en `sistemas.cbmw.cl/api/lab/dashboard.php`.

## Archivos para subir al hosting (`public_html/sistemas.cbmw.cl/`)

```txt
archivos_para_subir/
├── api/
│   └── lab/
│       ├── dashboard.php
│       └── reporte.php
├── bat/
│   ├── descargar_e_instalar_cbmw.bat
│   └── index.php
├── lab/
│   ├── agente_cbmw_global.ps1
│   └── Menu_Administracion_CBMW.bat
└── lab-config.json
```
