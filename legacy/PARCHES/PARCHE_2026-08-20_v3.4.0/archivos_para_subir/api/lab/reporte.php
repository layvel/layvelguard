<?php
// API Endpoint para recibir reportes telemétricos e inventario y despachar comandos remotos
header('Content-Type: application/json');

$logFile = __DIR__ . '/lab_telemetry.log';
$cmdDir  = __DIR__ . '/pending_commands';

if (!file_exists($cmdDir)) {
    @mkdir($cmdDir, 0777, true);
}

// Normalización de Hostname (sin espacios y en minúsculas)
function cleanHost($host) {
    return strtolower(trim($host));
}

// Manejo de comandos remotos enviados desde el Dashboard Web
if (isset($_GET['action']) && $_GET['action'] === 'send_command') {
    $targetHost = cleanHost($_POST['hostname'] ?? '');
    $command    = strtoupper(trim($_POST['command'] ?? ''));
    if ($targetHost && $command) {
        $cmdFile = $cmdDir . '/' . md5($targetHost) . '.json';
        @file_put_contents($cmdFile, json_encode(['command' => $command, 'target' => $targetHost, 'created_at' => date('Y-m-d H:i:s')]), LOCK_EX);
        echo json_encode(['status' => 'success', 'message' => "Comando $command registrado para $targetHost"]);
        exit;
    }
}

// Procesar Heartbeat enviado por el Agente CBMW
$input = file_get_contents('php://input');
$data = json_decode($input, true);

if ($data && isset($data['hostname'])) {
    $rawHost = $data['hostname'];
    $cleanHost = cleanHost($rawHost);
    
    if (!isset($data['timestamp'])) {
        $data['timestamp'] = date('Y-m-d H:i:s');
    }

    // Leer registros existentes
    $records = [];
    if (file_exists($logFile)) {
        $lines = file($logFile, FILE_IGNORE_NEW_LINES | FILE_SKIP_EMPTY_LINES);
        foreach ($lines as $line) {
            $item = json_decode($line, true);
            if ($item && isset($item['hostname'])) {
                $records[cleanHost($item['hostname'])] = $item;
            }
        }
    }

    $records[$cleanHost] = array_merge($records[$cleanHost] ?? [], $data);

    $outputLines = [];
    foreach ($records as $item) {
        $outputLines[] = json_encode($item);
    }
    @file_put_contents($logFile, implode("\n", $outputLines) . "\n", LOCK_EX);

    // Revisar si hay un comando remoto pendiente para este equipo
    $pendingCmd = null;
    $cmdFile = $cmdDir . '/' . md5($cleanHost) . '.json';
    if (file_exists($cmdFile)) {
        $cmdData = json_decode(file_get_contents($cmdFile), true);
        if ($cmdData && isset($cmdData['command'])) {
            $pendingCmd = $cmdData['command'];
        }
        @unlink($cmdFile); // Eliminar comando tras ser entregado
    }

    $response = [
        'status' => 'success',
        'message' => 'Telemetría registrada correctamente',
        'timestamp' => $data['timestamp']
    ];

    if ($pendingCmd) {
        $response['command'] = $pendingCmd;
    }

    echo json_encode($response);
} else {
    http_response_code(400);
    echo json_encode(['error' => 'JSON inválido o datos faltantes']);
}
