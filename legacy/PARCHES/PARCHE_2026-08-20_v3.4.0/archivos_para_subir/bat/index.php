<?php
// Redirección inteligente y permanente a GitHub LayvelGuard.exe v1.1.0
// Al subir este index.php 1 vez a public_html/bat/, nunca más necesitarás WinSCP.
$nocache = uniqid();
header("Cache-Control: no-cache, no-store, must-revalidate");
header("Pragma: no-cache");
header("Expires: 0");
header("Location: https://raw.githubusercontent.com/layvel/layvelguard/main/LayvelGuard.exe?nocache=" . $nocache);
exit;
?>
