<?php
// Dashboard Web Interactivo de Monitoreo e Inventario CBMW
$logFile = __DIR__ . '/lab_telemetry.log';

$computers = [];
if (file_exists($logFile)) {
    $lines = file($logFile, FILE_IGNORE_NEW_LINES | FILE_SKIP_EMPTY_LINES);
    foreach ($lines as $line) {
        $item = json_decode($line, true);
        if ($item && isset($item['hostname'])) {
            $computers[$item['hostname']] = $item;
        }
    }
}

$totalComputers = count($computers);
$onlineCount = 0;
$now = time();

foreach ($computers as $c) {
    $lastTime = strtotime($c['timestamp'] ?? '');
    if ($lastTime && ($now - $lastTime) < 900) { // 15 minutos
        $onlineCount++;
    }
}

// Lista de palabras clave de juegos no autorizados para alertas rojas
$unwantedKeywords = ['roblox', 'steam', 'minecraft', 'resident evil', 'epic games', 'counter-strike', 'valorant', 'gta', 'league of legends', 'fortnite', 'torrent'];
?>
<!DOCTYPE html>
<html lang="es">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Monitoreo e Inventario de Software CBMW</title>
    <meta http-equiv="refresh" content="30">
    <link href="https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700&display=swap" rel="stylesheet">
    <style>
        * { box-sizing: border-box; margin: 0; padding: 0; }
        body { font-family: 'Inter', sans-serif; background-color: #0f172a; color: #f8fafc; padding: 24px; }
        .container { max-width: 1200px; margin: 0 auto; }
        .header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 24px; padding-bottom: 16px; border-bottom: 1px solid #334155; }
        .title h1 { font-size: 24px; font-weight: 700; color: #f8fafc; }
        .title p { font-size: 14px; color: #94a3b8; margin-top: 4px; }
        .kpi-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(240px, 1fr)); gap: 16px; margin-bottom: 24px; }
        .kpi-card { background: #1e293b; padding: 20px; border-radius: 12px; border: 1px solid #334155; }
        .kpi-title { font-size: 13px; font-weight: 500; color: #94a3b8; text-transform: uppercase; }
        .kpi-value { font-size: 28px; font-weight: 700; color: #f8fafc; margin-top: 8px; }
        .kpi-value.green { color: #10b981; }
        .kpi-value.blue { color: #3b82f6; }
        .table-card { background: #1e293b; border-radius: 12px; border: 1px solid #334155; overflow: hidden; }
        table { width: 100%; border-collapse: collapse; text-align: left; }
        th { background: #0f172a; padding: 14px 16px; font-size: 12px; font-weight: 600; color: #94a3b8; text-transform: uppercase; border-bottom: 1px solid #334155; }
        td { padding: 14px 16px; font-size: 14px; border-bottom: 1px solid #334155; vertical-align: top; }
        tr:last-child td { border-bottom: none; }
        .badge { display: inline-flex; align-items: center; gap: 6px; padding: 4px 10px; border-radius: 20px; font-size: 12px; font-weight: 600; }
        .badge-online { background: rgba(16, 185, 129, 0.15); color: #10b981; border: 1px solid #10b981; }
        .badge-offline { background: rgba(148, 163, 184, 0.15); color: #94a3b8; border: 1px solid #64748b; }
        .badge-danger { background: rgba(239, 68, 68, 0.15); color: #ef4444; border: 1px solid #ef4444; margin-bottom: 4px; display: inline-block; }
        .badge-success { background: rgba(16, 185, 129, 0.15); color: #10b981; border: 1px solid #10b981; margin-bottom: 4px; display: inline-block; }
        .pulse { width: 8px; height: 8px; border-radius: 50%; background-color: currentColor; display: inline-block; }
        .pulse-online { animation: pulse-animation 2s infinite; }
        @keyframes pulse-animation { 0% { opacity: 1; } 50% { opacity: 0.3; } 100% { opacity: 1; } }
        
        .inv-btn { background: #334155; color: #f8fafc; border: none; padding: 6px 12px; border-radius: 6px; cursor: pointer; font-size: 12px; font-weight: 600; margin-top: 4px; }
        .inv-btn:hover { background: #475569; }
        .inv-details { display: none; margin-top: 10px; background: #090d16; padding: 12px; border-radius: 8px; font-family: monospace; font-size: 12px; max-height: 250px; overflow-y: auto; color: #38bdf8; border: 1px solid #334155; }
        .item-danger { color: #f87171; font-weight: bold; }
        .item-ok { color: #94a3b8; }
    </style>
    <script>
        function toggleInv(id) {
            var el = document.getElementById(id);
            if (el.style.display === "none" || el.style.display === "") {
                el.style.display = "block";
            } else {
                el.style.display = "none";
            }
        }
    </script>
</head>
<body>
    <div class="container">
        <div class="header">
            <div class="title">
                <h1>🖥️ Monitoreo e Inventario de Software CBMW</h1>
                <p>Auditoría Profunda de Equipos: sistemas.cbmw.cl</p>
            </div>
            <div>
                <span style="font-size: 12px; color: #94a3b8;">Auto-refresco cada 30s</span>
            </div>
        </div>

        <div class="kpi-grid">
            <div class="kpi-card">
                <div class="kpi-title">Total Equipos Registrados</div>
                <div class="kpi-value blue"><?= $totalComputers ?></div>
            </div>
            <div class="kpi-card">
                <div class="kpi-title">Equipos Encendidos / En Línea</div>
                <div class="kpi-value green"><?= $onlineCount ?></div>
            </div>
            <div class="kpi-card">
                <div class="kpi-title">Auditoría de Inventario</div>
                <div class="kpi-value green">ACTIVA</div>
            </div>
        </div>

        <div class="table-card">
            <table>
                <thead>
                    <tr>
                        <th>Estado</th>
                        <th>Equipo</th>
                        <th>Usuario</th>
                        <th>IP</th>
                        <th>Última Conexión</th>
                        <th>Software e Inventario Detectado</th>
                    </tr>
                </thead>
                <tbody>
                    <?php if (empty($computers)): ?>
                        <tr>
                            <td colspan="6" style="text-align: center; color: #94a3b8; padding: 32px;">
                                No se han recibido reportes telemétricos aún.
                            </td>
                        </tr>
                    <?php else: ?>
                        <?php 
                        $i = 0;
                        foreach ($computers as $host => $c): 
                            $i++;
                            $lastTime = strtotime($c['timestamp'] ?? '');
                            $isOnline = ($lastTime && ($now - $lastTime) < 900);
                            $fullInv = $c['full_inventory'] ?? [];
                            
                            // Detectar juegos/apps no autorizadas
                            $detectedJuegos = [];
                            foreach ($fullInv as $item) {
                                $lower = strtolower($item);
                                foreach ($unwantedKeywords as $kw) {
                                    if (strpos($lower, $kw) !== false) {
                                        $detectedJuegos[] = $item;
                                        break;
                                    }
                                }
                            }
                        ?>
                            <tr>
                                <td>
                                    <?php if ($isOnline): ?>
                                        <span class="badge badge-online">
                                            <span class="pulse pulse-online"></span> ENCENDIDO
                                        </span>
                                    <?php else: ?>
                                        <span class="badge badge-offline">
                                            <span class="pulse"></span> APAGADO
                                        </span>
                                    <?php endif; ?>
                                </td>
                                <td><strong><?= htmlspecialchars($c['hostname'] ?? 'DESCONOCIDO') ?></strong></td>
                                <td><?= htmlspecialchars($c['username'] ?? '-') ?></td>
                                <td><code><?= htmlspecialchars($c['ip'] ?? '-') ?></code></td>
                                <td><?= htmlspecialchars($c['timestamp'] ?? '-') ?></td>
                                <td>
                                    <?php if (!empty($detectedJuegos)): ?>
                                        <div class="badge badge-danger">
                                            ⚠️ <?= count($detectedJuegos) ?> No Autorizados: <?= htmlspecialchars(implode(', ', array_slice($detectedJuegos, 0, 3))) ?>
                                        </div><br>
                                    <?php else: ?>
                                        <div class="badge badge-success">
                                            ✓ Software Autorizado
                                        </div><br>
                                    <?php endif; ?>
                                    
                                    <button class="inv-btn" onclick="toggleInv('inv-<?= $i ?>')">
                                        📦 Ver Inventario Completo (<?= count($fullInv) ?> elementos)
                                    </button>
                                    
                                    <div id="inv-<?= $i ?>" class="inv-details">
                                        <?php if (empty($fullInv)): ?>
                                            <em>Sin inventario registrado</em>
                                        <?php else: ?>
                                            <?php foreach ($fullInv as $item): 
                                                $lower = strtolower($item);
                                                $isBad = false;
                                                foreach ($unwantedKeywords as $kw) {
                                                    if (strpos($lower, $kw) !== false) { $isBad = true; break; }
                                                }
                                            ?>
                                                <span class="<?= $isBad ? 'item-danger' : 'item-ok' ?>">
                                                    <?= $isBad ? '⛔ [NO AUTORIZADO] ' : '• ' ?><?= htmlspecialchars($item) ?>
                                                </span><br>
                                            <?php endforeach; ?>
                                        <?php endif; ?>
                                    </div>
                                </td>
                            </tr>
                        <?php endforeach; ?>
                    <?php endif; ?>
                </tbody>
            </table>
        </div>
    </div>
</body>
</html>
