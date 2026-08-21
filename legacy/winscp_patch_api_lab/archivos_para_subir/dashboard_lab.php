<?php
// Dashboard de Telemetría e Inventario de Equipos (CBMW)
// URL: https://sistemas.cbmw.cl/api/lab/dashboard.php
header('Content-Type: text/html; charset=utf-8');

$logFile = __DIR__ . '/../../storage/logs/lab_telemetry.log';

$records = [];
if (file_exists($logFile)) {
    $lines = file($logFile, FILE_IGNORE_NEW_LINES | FILE_SKIP_EMPTY_LINES);
    foreach ($lines as $line) {
        // Parsear log lines: [2026-08-19 10:30:00] IP: 192.168.1.50 | PC: PC-LAB-01 | Estado: OK | Apps Detectadas: [Steam Installed]
        if (preg_match('/^\[(.*?)\] IP: (.*?) \| PC: (.*?) \| Estado: (.*?) \| Apps Detectadas: \[(.*?)\](?: \| ERROR: (.*))?$/', $line, $matches)) {
            $records[$matches[3]] = [ // Index por Hostname para mostrar la última conexión
                'timestamp' => $matches[1],
                'ip' => $matches[2],
                'hostname' => $matches[3],
                'status' => trim($matches[4]),
                'apps' => trim($matches[5]),
                'error' => isset($matches[6]) ? trim($matches[6]) : ''
            ];
        }
    }
}

$totalEq = count($records);
$okCount = 0;
$alertCount = 0;
$errorCount = 0;

foreach ($records as $r) {
    if ($r['status'] === 'ERROR' || !empty($r['error'])) {
        $errorCount++;
    } elseif ($r['apps'] !== 'Ninguna' && !empty($r['apps'])) {
        $alertCount++;
    } else {
        $okCount++;
    }
}
?>
<!DOCTYPE html>
<html lang="es">
<head>
    <meta charset="UTF-8">
    <title>Panel de Control y Telemetría de Equipos - CBMW</title>
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <link href="https://cdn.jsdelivr.net/npm/bootstrap@5.3.0/dist/css/bootstrap.min.css" rel="stylesheet">
    <style>
        body { background-color: #f4f6f9; font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; }
        .navbar-brand { font-weight: 700; color: #0d6efd !important; }
        .card-stat { border: none; border-radius: 12px; box-shadow: 0 4px 12px rgba(0,0,0,0.05); }
        .stat-icon { font-size: 2.5rem; opacity: 0.8; }
        .table-card { background: #fff; border-radius: 12px; box-shadow: 0 4px 12px rgba(0,0,0,0.05); padding: 20px; }
        .badge-status-ok { background-color: #198754; }
        .badge-status-alert { background-color: #fd7e14; }
        .badge-status-error { background-color: #dc3545; }
    </style>
</head>
<body>
    <nav class="navbar navbar-expand-lg navbar-dark bg-dark">
        <div class="container">
            <a class="navbar-brand" href="#">💻 CBMW - Control de Equipos y Telemetría</a>
            <span class="navbar-text text-white">Servidor: sistemas.cbmw.cl</span>
        </div>
    </nav>

    <div class="container my-4">
        <div class="row g-3 mb-4">
            <div class="col-md-3">
                <div class="card card-stat bg-primary text-white p-3">
                    <div class="d-flex justify-content-between align-items-center">
                        <div>
                            <h6 class="text-uppercase mb-1">Total Equipos</h6>
                            <h2 class="mb-0 font-weight-bold"><?= $totalEq ?></h2>
                        </div>
                        <div class="stat-icon">💻</div>
                    </div>
                </div>
            </div>
            <div class="col-md-3">
                <div class="card card-stat bg-success text-white p-3">
                    <div class="d-flex justify-content-between align-items-center">
                        <div>
                            <h6 class="text-uppercase mb-1">Equipos en Norma</h6>
                            <h2 class="mb-0"><?= $okCount ?></h2>
                        </div>
                        <div class="stat-icon">✅</div>
                    </div>
                </div>
            </div>
            <div class="col-md-3">
                <div class="card card-stat bg-warning text-dark p-3">
                    <div class="d-flex justify-content-between align-items-center">
                        <div>
                            <h6 class="text-uppercase mb-1">Alertas (Apps/Steam)</h6>
                            <h2 class="mb-0"><?= $alertCount ?></h2>
                        </div>
                        <div class="stat-icon">⚠️</div>
                    </div>
                </div>
            </div>
            <div class="col-md-3">
                <div class="card card-stat bg-danger text-white p-3">
                    <div class="d-flex justify-content-between align-items-center">
                        <div>
                            <h6 class="text-uppercase mb-1">Errores de Agente</h6>
                            <h2 class="mb-0"><?= $errorCount ?></h2>
                        </div>
                        <div class="stat-icon">❌</div>
                    </div>
                </div>
            </div>
        </div>

        <div class="table-card">
            <div class="d-flex justify-content-between align-items-center mb-3">
                <h5 class="mb-0">Inventario y Telemetría de PCs / Notebooks</h5>
                <a href="dashboard.php" class="btn btn-sm btn-outline-primary">🔄 Actualizar Lista</a>
            </div>

            <div class="table-responsive">
                <table class="table table-hover align-middle">
                    <thead class="table-light">
                        <tr>
                            <th>Nombre del PC</th>
                            <th>Dirección IP</th>
                            <th>Estado</th>
                            <th>Apps Prohibidas Detectadas</th>
                            <th>Detalle / Log Error</th>
                            <th>Último Reporte</th>
                        </tr>
                    </thead>
                    <tbody>
                        <?php if (empty($records)): ?>
                            <tr>
                                <td colspan="6" class="text-center text-muted py-4">
                                    No hay reportes telemétricos registrados aún. Los datos aparecerán automáticamente cuando los PCs ejecuten el agente.
                                </td>
                            </tr>
                        <?php else: ?>
                            <?php foreach ($records as $rec): ?>
                                <tr>
                                    <td><strong><?= htmlspecialchars($rec['hostname']) ?></strong></td>
                                    <td><code><?= htmlspecialchars($rec['ip']) ?></code></td>
                                    <td>
                                        <?php if ($rec['status'] === 'ERROR' || !empty($rec['error'])): ?>
                                            <span class="badge badge-status-error">ERROR</span>
                                        <?php elseif ($rec['apps'] !== 'Ninguna' && !empty($rec['apps'])): ?>
                                            <span class="badge badge-status-alert">ALERTA JUEGOS</span>
                                        <?php else: ?>
                                            <span class="badge badge-status-ok">EN NORMA</span>
                                        <?php endif; ?>
                                    </td>
                                    <td>
                                        <?php if ($rec['apps'] !== 'Ninguna' && !empty($rec['apps'])): ?>
                                            <span class="text-danger font-weight-bold">⚠️ <?= htmlspecialchars($rec['apps']) ?></span>
                                        <?php else: ?>
                                            <span class="text-success">✓ Ninguna</span>
                                        <?php endif; ?>
                                    </td>
                                    <td>
                                        <?php if (!empty($rec['error'])): ?>
                                            <small class="text-danger"><?= htmlspecialchars($rec['error']) ?></small>
                                        <?php else: ?>
                                            <small class="text-muted">Sin errores</small>
                                        <?php endif; ?>
                                    </td>
                                    <td><small class="text-muted"><?= htmlspecialchars($rec['timestamp']) ?></small></td>
                                </tr>
                            <?php endforeach; ?>
                        <?php endif; ?>
                    </tbody>
                </table>
            </div>
        </div>
    </div>
</body>
</html>
