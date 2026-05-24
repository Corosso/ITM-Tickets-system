# =============================================================================
# ITM-Tickets Global — Verificación de Self-healing en Kubernetes
#
# Verifica el comportamiento que requiere la rúbrica:
#     "Simular la caída de un Pod y mostrar cómo Kubernetes lo recupera."
#
# El cluster es el integrado de Docker Desktop (context: docker-desktop).
# No requiere minikube, kind, AKS, ni nada externo.
#
# Uso:
#   .\scripts\k8s-self-healing-demo.ps1
#
# Pasos que ejecuta el script:
#   1. Verifica que kubectl funcione contra docker-desktop.
#   2. Verifica que la imagen local de inventory-api exista en el daemon.
#   3. Aplica el manifiesto (Namespace + Deployment + Service).
#   4. Espera a que el Pod esté Ready y muestra su nombre / IP / nodo.
#   5. Mata el Pod (kubectl delete pod) — caída simulada.
#   6. Hace polling y reporta cuándo K8s creó el Pod nuevo.
#   7. Muestra los logs del Pod nuevo para confirmar que es otro proceso.
# =============================================================================

$ErrorActionPreference = 'Stop'

function Write-Step {
    param([string]$Number, [string]$Title)
    Write-Host ""
    Write-Host "===========================================================" -ForegroundColor Cyan
    Write-Host " PASO $Number — $Title" -ForegroundColor Cyan
    Write-Host "===========================================================" -ForegroundColor Cyan
}

function Write-Info  { param([string]$m) Write-Host "  $m" -ForegroundColor Gray }
function Write-Ok    { param([string]$m) Write-Host "  ✓ $m" -ForegroundColor Green }
function Write-Warn  { param([string]$m) Write-Host "  ⚠ $m" -ForegroundColor Yellow }
function Write-Fail  { param([string]$m) Write-Host "  ✗ $m" -ForegroundColor Red }

$ns         = "itm-tickets"
$deployment = "inventory-api"
$manifest   = Join-Path $PSScriptRoot "..\infra\k8s\self-healing-demo.yaml"
$manifest   = (Resolve-Path $manifest).Path

Write-Host ""
Write-Host "===========================================================" -ForegroundColor Magenta
Write-Host " ITM-Tickets Global — Verificacion de Self-healing en K8s" -ForegroundColor Magenta
Write-Host " Festival de los Dos Mundos · World Tour 2026" -ForegroundColor Magenta
Write-Host "===========================================================" -ForegroundColor Magenta

# -----------------------------------------------------------------------------
# PASO 1 — kubectl conectado a docker-desktop
# -----------------------------------------------------------------------------
Write-Step "1" "Verificar kubectl y cluster docker-desktop"

try {
    $ctx = (kubectl config current-context).Trim()
    Write-Info "Contexto activo: $ctx"
    if ($ctx -ne "docker-desktop") {
        Write-Warn "El contexto activo no es docker-desktop. Cambiando..."
        kubectl config use-context docker-desktop | Out-Null
    }

    $nodes = kubectl get nodes --no-headers 2>$null
    if (-not $nodes) {
        throw "No hay nodos. Habilita Kubernetes en Docker Desktop → Settings → Kubernetes → Enable."
    }
    Write-Ok "Cluster vivo. Nodos:"
    kubectl get nodes
} catch {
    Write-Fail "kubectl no responde. Verifica que Kubernetes esté habilitado en Docker Desktop."
    throw
}

# -----------------------------------------------------------------------------
# PASO 2 — Imagen local presente
# -----------------------------------------------------------------------------
Write-Step "2" "Verificar que la imagen local exista"

$img = "itm-tickets-local/inventory-api:latest"
$found = docker images --format '{{.Repository}}:{{.Tag}}' | Where-Object { $_ -eq $img }
if (-not $found) {
    Write-Fail "No encuentro la imagen $img en el daemon."
    Write-Info "Construila primero con: cd infra/terraform && terraform apply"
    exit 1
}
Write-Ok "Imagen $img presente en el daemon."

# -----------------------------------------------------------------------------
# PASO 3 — Aplicar el manifiesto
# -----------------------------------------------------------------------------
Write-Step "3" "Aplicar manifiesto Kubernetes"

Write-Info "kubectl apply -f $manifest"
kubectl apply -f $manifest

Write-Ok "Manifiesto aplicado. Esperando a que el Deployment esté Available..."
kubectl rollout status deployment/$deployment -n $ns --timeout=60s

Write-Host ""
Write-Ok "Pod corriendo:"
kubectl get pods -n $ns -l app=$deployment -o wide

$originalPod = (kubectl get pods -n $ns -l app=$deployment -o jsonpath='{.items[0].metadata.name}').Trim()
Write-Host ""
Write-Info "Nombre del Pod original: $originalPod"

# -----------------------------------------------------------------------------
# PASO 4 — Mostrar logs del Pod original (para comparar después)
# -----------------------------------------------------------------------------
Write-Step "4" "Logs del Pod original (señal de vida)"

Start-Sleep -Seconds 3
kubectl logs -n $ns $originalPod --tail=8

# -----------------------------------------------------------------------------
# PASO 5 — Caída simulada
# -----------------------------------------------------------------------------
Write-Step "5" "Simular caída del Pod — kubectl delete pod"

Write-Warn "Matando el Pod $originalPod..."
$killTime = Get-Date
kubectl delete pod -n $ns $originalPod --grace-period=0 --force 2>&1 | Out-Null
Write-Info "Pod borrado a las $($killTime.ToString('HH:mm:ss'))"

# -----------------------------------------------------------------------------
# PASO 6 — Ver cómo K8s lo recupera
# -----------------------------------------------------------------------------
Write-Step "6" "Kubernetes reconcilia el Deployment"

Write-Info "El Deployment Controller detecta replicas=0 vs deseado=1."
Write-Info "Espero hasta 30s a que aparezca un Pod nuevo en estado Ready..."

$newPod = $null
$deadline = (Get-Date).AddSeconds(30)
while ((Get-Date) -lt $deadline) {
    $pods = kubectl get pods -n $ns -l app=$deployment `
        --field-selector=status.phase=Running `
        -o jsonpath='{.items[*].metadata.name}' 2>$null
    if ($pods) {
        $candidates = $pods.Split(' ') | Where-Object { $_ -and $_ -ne $originalPod }
        if ($candidates) {
            $newPod = $candidates[0]
            break
        }
    }
    Start-Sleep -Milliseconds 500
}

if (-not $newPod) {
    Write-Fail "Tras 30s no apareció un Pod nuevo. Revisa: kubectl describe deployment/$deployment -n $ns"
    exit 1
}

$recoveryTime = (Get-Date) - $killTime
Write-Ok "Pod nuevo en marcha en $([math]::Round($recoveryTime.TotalSeconds, 1)) segundos: $newPod"

Write-Host ""
Write-Info "Estado actual del Deployment:"
kubectl get pods -n $ns -l app=$deployment -o wide

# -----------------------------------------------------------------------------
# PASO 7 — Logs del Pod nuevo (prueba de que es uno distinto)
# -----------------------------------------------------------------------------
Write-Step "7" "Logs del Pod nuevo — recreado por K8s"

Start-Sleep -Seconds 3
kubectl logs -n $ns $newPod --tail=8

Write-Host ""
Write-Host "===========================================================" -ForegroundColor Green
Write-Host " ✅ SELF-HEALING DEMOSTRADO" -ForegroundColor Green
Write-Host "===========================================================" -ForegroundColor Green
Write-Host " Pod muerto:     $originalPod" -ForegroundColor Green
Write-Host " Pod recreado:   $newPod" -ForegroundColor Green
Write-Host " Tiempo:         $([math]::Round($recoveryTime.TotalSeconds, 1))s" -ForegroundColor Green
Write-Host ""
Write-Host " Mecanismo:      Deployment Controller del control-plane de" -ForegroundColor Green
Write-Host "                 Kubernetes (loop de reconciliacion)." -ForegroundColor Green
Write-Host "===========================================================" -ForegroundColor Green
Write-Host ""
Write-Host " Limpieza:" -ForegroundColor Gray
Write-Host "   kubectl delete -f infra/k8s/self-healing-demo.yaml" -ForegroundColor Gray
Write-Host ""
