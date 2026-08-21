<?php
// Endpoint de recepción de telemetría e inventario de equipos (CBMW)
header('Content-Type: application/json; charset=utf-8');

if ($_SERVER['REQUEST_METHOD'] !== 'POST') {
    http_response_code(405);
    echo json_encode(['error' => 'Método no permitido']);
    exit;
}

$rawInput = file_get_contents('php://input');
$data = json_decode($rawInput, true);

if (!$data) {
    http_response_code(400);
    echo json_encode(['error' => 'JSON inválido']);
    exit;
}

$logDir = __DIR__ . '/../../storage/logs';
if (!is_dir($logDir)) {
    @mkdir($logDir, 0755, true);
}

$logFile = $logDir . '/lab_telemetry.log';
$timestamp = date('Y-m-d H:i:s');
$hostname = $data['hostname'] ?? 'Desconocido';
$ip = $data['ip'] ?? '0.0.0.0';
$status = $data['status'] ?? 'OK';
$apps = isset($data['detected_apps']) ? implode(', ', $data['detected_apps']) : 'Ninguna';
$error = $data['error_message'] ?? '';

$entry = "[$timestamp] IP: $ip | PC: $hostname | Estado: $status | Apps Detectadas: [$apps]";
if (!empty($error)) {
    $entry .= " | ERROR: $error";
}
$entry .= PHP_EOL;

@file_put_contents($logFile, $entry, FILE_APPEND);

echo json_encode([
    'status' => 'success',
    'message' => 'Telemetría registrada correctamente',
    'timestamp' => $timestamp
]);
