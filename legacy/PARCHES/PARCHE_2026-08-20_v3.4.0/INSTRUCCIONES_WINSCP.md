# Instrucciones WinSCP: Parche Agente y Descarga Web CBMW

Este parche habilita la descarga directa por web (`sistemas.cbmw.cl/bat`) y la recepción de reportes de telemetría e inventario de los equipos.

## Estructura de Archivos a Subir

Subir la carpeta `archivos_para_subir/` directamente a la raíz de tu sitio en el hosting (`public_html` / `htdocs`):

```txt
archivos_para_subir/
├── bat/
│   ├── index.php                      <-- Endpoint de descarga (sistemas.cbmw.cl/bat)
│   └── descargar_e_instalar_cbmw.bat   <-- Instalador ejecutable que se descarga
├── lab/
│   ├── agente_cbmw_global.ps1          <-- Script del agente para clientes
│   └── Menu_Administracion_CBMW.bat    <-- Menú interactivo
├── api/
│   └── lab/
│       └── reporte.php                <-- Recepción de telemetría y errores
└── lab-config.json                    <-- Interruptor ON/OFF y reglas globales
```

## Pasos en WinSCP:

1. Conéctate a tu servidor FTP/SFTP de `sistemas.cbmw.cl`.
2. Ve a la carpeta raíz de tu sitio web (generalmente `public_html` o `htdocs`).
3. Copia el contenido de `archivos_para_subir/` dentro de la raíz.
4. **Verificación:**
   - Abre en tu navegador: `https://sistemas.cbmw.cl/bat`
   - Inmediatamente comenzará la descarga de `descargar_e_instalar_cbmw.bat`.
   - Abre `https://sistemas.cbmw.cl/lab-config.json` para verificar que el JSON responda correctamente.

## Uso por parte del Técnico:
1. En cualquier equipo o notebook con internet, el técnico abre Chrome/Edge e ingresa a: `sistemas.cbmw.cl/bat`
2. Ejecuta el archivo `.bat` descargado como Administrador.
3. El script descargará e instalará automáticamente todos los componentes en `C:\Proyectos\agente-cbmw` y abrirá el menú interactivo sin usar pendrive.
