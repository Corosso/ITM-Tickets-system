# =============================================================================
# Watchdog supervisor — utilidad opcional para entornos Docker locales.
#
# Implementa un Process Supervisor que vigila la lista de containers y los
# reinicia si su estado pasa a "exited". Es el mismo patrón que kubelet usa
# para los Pods en Kubernetes, systemd para los servicios en Linux, o
# launchd en macOS.
#
# La solución oficial de self-healing del proyecto se hace en Kubernetes
# (ver infra/k8s/self-healing-demo-nginx.yaml). Este script existe como
# utilidad complementaria para sesiones de desarrollo donde se quiere
# mantener viva la red de containers de Docker sin K8s de por medio.
#
# Uso:
#   .\scripts\watchdog.ps1
# =============================================================================

$containers = @(
    "auth-api",
    "order-api",
    "inventory-api",
    "notification-api",
    "price-api",
    "search-api",
    "apigateway"
)

Write-Host ""
Write-Host "===========================================================" -ForegroundColor Cyan
Write-Host " ITM-Tickets Global — Watchdog Supervisor" -ForegroundColor Cyan
Write-Host "===========================================================" -ForegroundColor Cyan
Write-Host " Patrón: Supervisor / Process Manager"
Write-Host " Equivalente: kubelet (K8s), systemd (Linux), launchd (macOS)"
Write-Host ""
Write-Host " Vigilando contenedores cada 2 segundos:"
foreach ($c in $containers) { Write-Host "   - $c" }
Write-Host ""
Write-Host " Ctrl+C para detener el watchdog."
Write-Host "===========================================================" -ForegroundColor Cyan
Write-Host ""

while ($true) {
    foreach ($name in $containers) {
        $status = docker inspect --format '{{.State.Status}}' $name 2>$null

        if ($status -eq "exited") {
            $timestamp = Get-Date -Format 'HH:mm:ss'
            Write-Host "[$timestamp] ⚠  $name murió. Recuperando..." -ForegroundColor Yellow

            docker start $name | Out-Null

            $newStatus = docker inspect --format '{{.State.Status}}' $name 2>$null
            if ($newStatus -eq "running") {
                Write-Host "[$timestamp] ✅ $name recuperado por el supervisor" -ForegroundColor Green
            } else {
                Write-Host "[$timestamp] ❌ Falló reiniciar $name (estado=$newStatus)" -ForegroundColor Red
            }
        }
    }
    Start-Sleep -Seconds 2
}
