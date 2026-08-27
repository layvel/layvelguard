<?php
// Dashboard Web Interactivo de Monitoreo, Control Remoto e Inventario CBMW
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
    if ($lastTime && ($now - $lastTime) < 300) { // 5 minutos
        $onlineCount++;
    }
}

// Lista de palabras clave de juegos no autorizados para alertas rojas
$unwantedKeywords = ['roblox', 'steam', 'minecraft', 'resident evil', 'epic games', 'counter-strike', 'valorant', 'gta', 'league of legends', 'fortnite', 'torrent', 'arena breakout', 'google play games', 'hotline miami', 'hydra', 'mem reduct', 'ht parental'];

// Consolidación de software único de todos los equipos
$allUniqueSoftware = [];
foreach ($computers as $c) {
    $inv = $c['full_inventory'] ?? [];
    foreach ($inv as $app) {
        $cleanApp = trim($app);
        if (!empty($cleanApp) && !in_array($cleanApp, $allUniqueSoftware)) {
            $allUniqueSoftware[] = $cleanApp;
        }
    }
}
sort($allUniqueSoftware, SORT_NATURAL | SORT_FLAG_CASE);

$reportLines = [];
$reportLines[] = "==================================================";
$reportLines[] = "REPORTE DE INVENTARIO CONSOLIDADO - CBMW LABS";
$reportLines[] = "Total Equipos Registrados: " . $totalComputers;
$reportLines[] = "Total Aplicaciones Únicas Detectadas: " . count($allUniqueSoftware);
$reportLines[] = "Fecha de Generación: " . date('Y-m-d H:i:s');
$reportLines[] = "==================================================";
$reportLines[] = "";
foreach ($allUniqueSoftware as $app) {
    $reportLines[] = "- " . $app;
}
$reportText = implode("\n", $reportLines);
?>
<!DOCTYPE html>
<html lang="es">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Monitoreo, Control Remoto e Inventario CBMW</title>
    <meta http-equiv="refresh" content="15">
    <link href="https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700&display=swap" rel="stylesheet">
    <style>
        * { box-sizing: border-box; margin: 0; padding: 0; }
        body { font-family: 'Inter', sans-serif; background-color: #0f172a; color: #f8fafc; padding: 24px; }
        .container { max-width: 1250px; margin: 0 auto; }
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
        td { padding: 14px 16px; font-size: 14px; border-bottom: 1px solid #334155; vertical-align: middle; }
        tr:last-child td { border-bottom: none; }
        .badge { display: inline-flex; align-items: center; gap: 6px; padding: 4px 10px; border-radius: 20px; font-size: 12px; font-weight: 600; }
        .badge-online { background: rgba(16, 185, 129, 0.15); color: #10b981; border: 1px solid #10b981; }
        .badge-offline { background: rgba(148, 163, 184, 0.15); color: #94a3b8; border: 1px solid #64748b; }
        .badge-danger { background: rgba(239, 68, 68, 0.15); color: #ef4444; border: 1px solid #ef4444; margin-bottom: 4px; display: inline-block; }
        .badge-success { background: rgba(16, 185, 129, 0.15); color: #10b981; border: 1px solid #10b981; margin-bottom: 4px; display: inline-block; }
        .pulse { width: 8px; height: 8px; border-radius: 50%; background-color: currentColor; display: inline-block; }
        .pulse-online { animation: pulse-animation 2s infinite; }
        @keyframes pulse-animation { 0% { opacity: 1; } 50% { opacity: 0.3; } 100% { opacity: 1; } }
        
        .btn-shutdown { background: #e11d48; color: #ffffff; border: none; padding: 6px 12px; border-radius: 6px; cursor: pointer; font-size: 12px; font-weight: 700; transition: 0.2s; }
        .btn-shutdown:hover { background: #be123c; }
        
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

        function shutdownRemote(hostname) {
            if (confirm("¿Estás seguro de que deseas APAGAR REMOTAMENTE la PC '" + hostname + "'?")) {
                var formData = new FormData();
                formData.append('hostname', hostname);
                formData.append('command', 'SHUTDOWN');

                fetch('reporte.php?action=send_command', {
                    method: 'POST',
                    body: formData
                })
                .then(r => r.json())
                .then(data => {
                    alert("⚡ Orden de APAGADO enviada a " + hostname + ". Se apagará en el próximo latido (máx 3 min).");
                })
                .catch(err => {
                    alert("Error enviando comando: " + err);
                });
            }
        }

        function uninstallRemote(hostname) {
            if (confirm("¿Estás seguro de desinstalar y purgar de manera remota todo el software prohibido en el PC '" + hostname + "'?")) {
                var formData = new FormData();
                formData.append('hostname', hostname);
                formData.append('command', 'UNINSTALL');

                fetch('reporte.php?action=send_command', {
                    method: 'POST',
                    body: formData
                })
                .then(r => r.json())
                .then(data => {
                    alert("🗑️ Orden de DESINSTALACIÓN REMOTA enviada a " + hostname + ". Se ejecutará silenciosamente en el próximo latido.");
                })
                .catch(err => {
                    alert("Error enviando comando: " + err);
                });
            }
        }

        function openReportModal() {
            document.getElementById('reportModal').style.display = 'flex';
        }
        function closeReportModal() {
            document.getElementById('reportModal').style.display = 'none';
        }
        function copyReportText() {
            var copyText = document.getElementById("reportTextArea");
            copyText.select();
            copyText.setSelectionRange(0, 99999);
            if (navigator.clipboard) {
                navigator.clipboard.writeText(copyText.value).then(function() {
                    alert("¡Reporte copiado al portapapeles exitosamente! Ahora puedes pegarlo en el chat.");
                }).catch(function() {
                    document.execCommand('copy');
                    alert("¡Reporte copiado!");
                });
            } else {
                document.execCommand('copy');
                alert("¡Reporte copiado!");
            }
        }
    </script>
</head>
<body>
    <div class="container">
        <div class="header">
            <div class="title">
                <h1>🖥️ Monitoreo e Inventario de Software CBMW</h1>
                <p>Auditoría Profunda y Apagado Remoto: sistemas.cbmw.cl</p>
            </div>
            <div style="display:flex; align-items:center; gap:16px;">
                <button onclick="openReportModal()" style="background:#2563eb; color:#ffffff; border:none; padding:10px 18px; border-radius:8px; font-weight:700; cursor:pointer; font-size:13px; display:inline-flex; align-items:center; gap:8px; box-shadow:0 4px 6px -1px rgba(37,99,235,0.3);">
                    📋 Copiar Reporte para la IA
                </button>
                <span style="font-size: 12px; color: #94a3b8;">Auto-refresco cada 15s</span>
            </div>
        </div>

        <div class="kpi-grid">
            <div class="kpi-card">
                <div class="kpi-title">Total Equipos Registrados</div>
                <div class="kpi-value blue"><?= $totalComputers ?></div>
            </div>
            <div class="kpi-card">
                <div class="kpi-title">Equipos Encendidos (Latido 5 min)</div>
                <div class="kpi-value green"><?= $onlineCount ?></div>
            </div>
            <div class="kpi-card">
                <div class="kpi-title">Servicio Telemétrico</div>
                <div class="kpi-value green">ACTIVO v3.4.0</div>
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
                        <th>Último Latido</th>
                        <th>Software e Inventario</th>
                        <th>Acción Remota</th>
                    </tr>
                </thead>
                <tbody>
                    <?php if (empty($computers)): ?>
                        <tr>
                            <td colspan="7" style="text-align: center; color: #94a3b8; padding: 32px;">
                                No se han recibido reportes telemétricos aún.
                            </td>
                        </tr>
                    <?php else: ?>
                        <?php 
                        $i = 0;
                        foreach ($computers as $host => $c): 
                            $i++;
                            $lastTime = strtotime($c['timestamp'] ?? '');
                            $isOnline = ($lastTime && ($now - $lastTime) < 300);
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
                                        📦 Ver Inventario (<?= count($fullInv) ?>)
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
                                <td>
                                    <?php if ($isOnline): ?>
                                        <button class="btn-shutdown" onclick="shutdownRemote('<?= htmlspecialchars($c['hostname']) ?>')">
                                            ⚡ Apagar PC
                                        </button>
                                        <button class="btn-shutdown" style="background:#e11d48; margin-top:6px;" onclick="uninstallRemote('<?= htmlspecialchars($c['hostname']) ?>')">
                                            🗑️ Desinstalar Apps
                                        </button>
                                    <?php else: ?>
                                        <span style="color: #64748b; font-size: 12px;">Inactivo</span>
                                    <?php endif; ?>
                                </td>
                            </tr>
                        <?php endforeach; ?>
                    <?php endif; ?>
                </tbody>
            </table>
        </div>
    </div>
    <!-- Modal de Reporte Consolidado -->
    <div id="reportModal" style="display:none; position:fixed; top:0; left:0; width:100%; height:100%; background:rgba(0,0,0,0.8); z-index:9999; justify-content:center; align-items:center; padding:16px;">
        <div style="background:#1e293b; width:100%; max-width:700px; padding:24px; border-radius:12px; border:1px solid #334155; box-shadow:0 20px 25px -5px rgba(0,0,0,0.5);">
            <div style="display:flex; justify-content:space-between; align-items:center; margin-bottom:14px;">
                <h2 style="font-size:18px; font-weight:700; color:#f8fafc;">📋 Reporte Consolidado de Software (<?= count($allUniqueSoftware) ?> Apps)</h2>
                <button onclick="closeReportModal()" style="background:transparent; border:none; color:#94a3b8; font-size:22px; cursor:pointer;">✕</button>
            </div>
            <p style="font-size:13px; color:#94a3b8; margin-bottom:12px;">Haz clic en el botón verde para copiar todo el reporte y pegárselo al asistente IA:</p>
            <textarea id="reportTextArea" readonly style="width:100%; height:320px; background:#090d16; color:#38bdf8; font-family:monospace; font-size:12px; padding:12px; border-radius:8px; border:1px solid #334155; resize:none; outline:none;"><?= htmlspecialchars($reportText) ?></textarea>
            <div style="display:flex; justify-content:flex-end; gap:12px; margin-top:16px;">
                <button onclick="closeReportModal()" style="background:#334155; color:#fff; border:none; padding:10px 18px; border-radius:6px; font-weight:600; cursor:pointer;">Cerrar</button>
                <button onclick="copyReportText()" style="background:#10b981; color:#fff; border:none; padding:10px 18px; border-radius:6px; font-weight:700; cursor:pointer; display:inline-flex; align-items:center; gap:6px;">📋 Copiar al Portapapeles</button>
            </div>
        </div>
    </div>
</body>
</html>
