<?php
// API Endpoint para recibir reportes telemétricos e inventario
header('Content-Type: application/json');

$logFile = __DIR__ . '/lab_telemetry.log';

$input = file_get_contents('php://input');
$data = json_decode($input, true);

if ($data && isset($data['hostname'])) {
    if (!isset($data['timestamp'])) {
        $data['timestamp'] = date('Y-m-d H:i:s');
    }
    
    $records = [];
    if (file_exists($logFile)) {
        $lines = file($logFile, FILE_IGNORE_NEW_LINES | FILE_SKIP_EMPTY_LINES);
        foreach ($lines as $line) {
            $item = json_decode($line, true);
            if ($item && isset($item['hostname'])) {
                $records[$item['hostname']] = $item;
            }
        }
    }
    
    $records[$data['hostname']] = $data;
    
    $outputLines = [];
    foreach ($records as $item) {
        $outputLines[] = json_encode($item);
    }
    @file_put_contents($logFile, implode("\n", $outputLines) . "\n", LOCK_EX);
    
    echo json_encode([
        'status' => 'success',
        'message' => 'Telemetría registrada correctamente',
        'timestamp' => $data['timestamp']
    ]);
} else {
    http_response_code(400);
    echo json_encode(['error' => 'JSON inválido o datos faltantes']);
}
