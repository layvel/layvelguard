<?php
// Endpoint de descarga directa del instalador .bat para CBMW
// URL: https://sistemas.cbmw.cl/bat/

$batFile = __DIR__ . '/descargar_e_instalar_cbmw.bat';

if (!file_exists($batFile)) {
    http_response_code(404);
    echo "Error: El archivo del instalador no se encuentra en el servidor.";
    exit;
}

header('Content-Description: File Transfer');
header('Content-Type: application/x-msdos-program');
header('Content-Disposition: attachment; filename="descargar_e_instalar_cbmw.bat"');
header('Expires: 0');
header('Cache-Control: must-revalidate');
header('Pragma: public');
header('Content-Length: ' . filesize($batFile));
readfile($batFile);
exit;
