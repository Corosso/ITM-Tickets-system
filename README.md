# ITM-Tickets Global "The World Tour 2026"

Sistema distribuido de boletería para el **Festival de los Dos Mundos**, con sedes simultáneas en Medellín (Colombia) y Madrid (España). Soporta 50.000 usuarios concurrentes en el minuto cero de la venta, garantizando consistencia transaccional, alta disponibilidad y comunicación en tiempo real.

**Asignatura:** Programación Distribuida — Instituto Tecnológico Metropolitano (ITM)

**Stack:** .NET 10 (Preview) · .NET Aspire · .NET MAUI · YARP · gRPC · MassTransit + RabbitMQ · Redis · SignalR · Elasticsearch + Qdrant · EF Core + PostgreSQL · Docker · Kubernetes · Terraform · GitHub Actions

---

## Arquitectura del Sistema

```
┌──────────────────┐       ┌──────────────────┐
│   App .NET MAUI  │       │  Blazor Web      │
│   (móvil)        │       │  (alternativo)   │
└────────┬─────────┘       └────────┬─────────┘
         │ HTTPS + JWT              │
         └──────────┬───────────────┘
                    ▼
       ┌────────────────────────────┐
       │   YARP API Gateway         │
       │   - Valida JWT             │
       │   - Rate Limit 100 req/s   │
       │   - Inyecta CorrelationId  │
       └─────────┬──────────────────┘
                 │
   ┌─────────┬───┴──────┬─────────┬──────────┐
   ▼         ▼          ▼         ▼          ▼
┌──────┐ ┌──────┐ ┌─────────┐ ┌──────┐ ┌──────────────┐
│ Auth │ │Order │ │  Price  │ │Search│ │ Notification │
│ JWT  │ │ Saga │ │  Redis  │ │Elastic│ │  SignalR     │
└──────┘ └──┬───┘ └─────────┘ │+Qdrant│ └──────┬───────┘
            │                  └──────┘        │
            │ gRPC                              │
            ▼                                   │
       ┌──────────┐                             │
       │Inventory │── PostgreSQL (seats reales) │
       │  gRPC    │                             │
       └────┬─────┘                             │
            │                                   │
            ▼ MassTransit (RabbitMQ)            │
       ┌──────────┐                             │
       │ RabbitMQ │────────────────────────────►│
       └──────────┘   OrderConfirmed/TicketReady│
                                                ▼
                                       [MAUI recibe ticket]
```

## Componentes del Ecosistema

### A. Frontera y Movilidad

| Componente | Descripción | Tecnología |
|---|---|---|
| **App MAUI** | Cliente móvil multiplataforma. Login JWT, búsqueda, compra y recepción de ticket en tiempo real vía SignalR. | .NET MAUI + SignalR Client |
| **API Gateway** | Punto de entrada único. Valida JWT, aplica Rate Limiting (100 req/s), inyecta `X-Correlation-Id`, rutea a microservicios. | YARP Reverse Proxy |
| **Auth API** | Generación y validación de tokens JWT. | JWT Bearer |

### B. Núcleo de Microservicios

| Servicio | Responsabilidad | Patrón/Tecnología |
|---|---|---|
| **Order.Api** | Gestión transaccional. Persiste estado del Saga en PostgreSQL para resiliencia ante caídas. | SAGA (MassTransit + EF Core) |
| **Inventory.Api** | Reserva real de asientos con transacciones Serializable + índice único en `(section, row, seat_number)` para evitar venta doble. | gRPC + EF Core + PostgreSQL |
| **Price.Api** | Consulta de precios con caché distribuida. | Redis (StackExchange.Redis) |
| **Search.Api** | Búsqueda híbrida: texto en Elasticsearch + semántica en Qdrant. | Elastic.Clients + Qdrant.Client |
| **Notification.Api** | Notificaciones en tiempo real (TicketReady). | SignalR + MassTransit Consumer |

### C. Trazabilidad — Correlation ID

Un `X-Correlation-Id` se inyecta en el Gateway (o se acepta del cliente) y viaja por:

- **HTTP**: header propagado por `CorrelationIdHttpHandler` (DelegatingHandler en todos los `HttpClient`).
- **gRPC**: metadata `x-correlation-id` propagada por `CorrelationIdClientInterceptor` (Order) y leída por `CorrelationIdServerInterceptor` (Inventory).
- **RabbitMQ**: header `X-Correlation-Id` en los mensajes vía `CorrelationIdPublishFilter` / `CorrelationIdConsumeFilter` de MassTransit.
- **Logs**: scope con clave `CorrelationId` en todos los `ILogger`.

Esto permite filtrar logs de los 3 servicios con un solo comando:

```powershell
# PowerShell sobre el despliegue Docker / Terraform.
# Reemplazar <id> por el UUID que la MAUI imprime en pantalla al comprar.
docker logs order-api 2>&1        | Select-String "<id>"
docker logs inventory-api 2>&1    | Select-String "<id>"
docker logs notification-api 2>&1 | Select-String "<id>"

# bash equivalente (Linux/macOS/WSL/Git Bash)
docker logs order-api 2>&1 | grep "<id>"

# Kubernetes equivalente
kubectl logs -n itm-tickets -l app=order-api,inventory-api,notification-api | grep "<id>"

# Los tres servicios emiten al menos una línea con el prefijo `[CID=<id>]`,
# que es el formato uniforme usado por el middleware HTTP de order-api, el
# interceptor gRPC server de inventory-api y el consume filter de
# notification-api.
```

### D. Infraestructura y DevOps

- **Contenerización**: 7 Dockerfiles con multi-stage build.
- **Orquestación local**: `docker-compose.yml` con RabbitMQ, Redis, PostgreSQL, Elasticsearch, Qdrant y todos los microservicios.
- **Kubernetes**: Manifiestos en `k8s/` con Deployments, Services, HPA, StatefulSets para Elastic.
- **Infraestructura como Código**: `infra/terraform/main.tf` recrea todo el ecosistema (red, volúmenes, imágenes y 13 contenedores) directamente sobre Docker Desktop. Sin nube, sin credenciales.
- **CI/CD**: GitHub Actions compila, prueba, sube imágenes a Docker Hub y despliega en Kubernetes al hacer push a `main`.

## Estructura del Proyecto

```
ITM-Tickets Global/
├── .github/workflows/
│   └── ci-cd.yaml                        # Pipeline CI/CD
├── infra/
│   ├── terraform/
│   │   ├── main.tf                       # IaC: red + volúmenes + 7 imágenes + 13 contenedores en Docker
│   │   └── README.md
│   └── k8s/
│       ├── self-healing-demo.yaml        # Verificación de self-healing (imagen local)
│       └── self-healing-demo-nginx.yaml  # Variante con imagen pública
├── k8s/
│   ├── namespace.yaml
│   ├── secrets.yaml
│   ├── infrastructure/                   # postgres, redis, rabbitmq, elasticsearch, qdrant
│   ├── apigateway/ inventory-api/ order-api/ price-api/ search-api/ notification-api/
│   └── (cada uno con deployment.yaml, service.yaml, hpa.yaml)
├── scripts/
│   ├── postgres-init.sh                  # Crea las dos BDs en Postgres
│   ├── k8s-self-healing-demo.ps1         # Procedimiento automatizado de self-healing
│   └── watchdog.ps1                      # Utilidad opcional: process supervisor local
├── src/
│   ├── ApiGateway/                       # YARP
│   ├── Services/                         # Order, Inventory, Price, Search, Notification
│   ├── Shared/                           # Protos, DTOs, Eventos, Modelos
│   └── Mobile/
│       └── ITM-Tickets Global.MauiApp/   # App .NET MAUI
├── tests/
│   └── ITM-Tickets Global.Tests/         # xUnit
├── ITM-Tickets Global.ApiService/        # Auth API (JWT)
├── ITM-Tickets Global.AppHost/           # .NET Aspire Orchestrator
├── ITM-Tickets Global.Web/               # Blazor Web Frontend (alternativo a MAUI)
├── ITM-Tickets Global.ServiceDefaults/   # OpenTelemetry + CorrelationId middleware
├── ITM-Tickets Global.slnx
└── docker-compose.yml
```

## Ejecución Local

### Requisitos

- .NET 10 SDK (Preview)
- Workload de MAUI: `dotnet workload install maui`
- Docker Desktop
- (Opcional) Visual Studio 2026 / Rider para abrir MAUI
- (Opcional) kubectl + minikube/Docker Desktop K8s para probar en cluster

### Levantar todo con Docker Compose

```bash
docker compose up -d
docker compose ps   # verificar healthchecks
```

| Servicio | Puerto local |
|---|---|
| API Gateway | 8080 |
| Auth API | 5007 |
| Order API | 5003 |
| Inventory API (gRPC sobre HTTP/2) | 5002 |
| Price API | 5004 |
| Notification API (SignalR) | 5005 |
| Search API | 5006 |
| RabbitMQ Management | 15672 (user `itm_admin` / `ChangeMe123!`) |
| Redis | 6379 |
| PostgreSQL | 5432 |
| Elasticsearch | 9200 |
| Qdrant (gRPC) | 6334 |

### Probar la compra (Happy Path)

Ver `infra/terraform/README.md` para los comandos completos en **PowerShell**
(Windows) y **bash** (Git Bash / WSL / Linux / macOS).

Resumen PowerShell:

```powershell
# 1. Login
$auth = Invoke-RestMethod -Uri "http://localhost:8080/api/auth/login" `
    -Method POST -ContentType "application/json" `
    -Body (@{ Username = "user"; Password = "user123" } | ConvertTo-Json)
$token = $auth.token

# 2. Buscar eventos
Invoke-RestMethod -Uri "http://localhost:8080/api/search?q=festival&vibe=fiesta" `
    -Headers @{ Authorization = "Bearer $token" } | ConvertTo-Json -Depth 5
```

> 💡 Alternativa sin terminal: usar los archivos `.http` que están en cada
> proyecto API (botón "Send Request" en Visual Studio).

### Correr la App MAUI

```bash
# Windows
dotnet build "src/Mobile/ITM-Tickets Global.MauiApp" -f net10.0-windows10.0.19041.0
dotnet run --project "src/Mobile/ITM-Tickets Global.MauiApp" -f net10.0-windows10.0.19041.0

# Android emulator
dotnet build "src/Mobile/ITM-Tickets Global.MauiApp" -f net10.0-android
```

## Despliegue en Kubernetes

```bash
# Opción A: Terraform sobre Docker Desktop (modo por defecto del proyecto)
cd infra/terraform && terraform init && terraform apply -auto-approve
# Listo: el gateway queda en http://localhost:8080

# Opción B: Kubernetes local (minikube / Docker Desktop K8s)
kubectl apply -f k8s/namespace.yaml
kubectl apply -f k8s/secrets.yaml
kubectl apply -f k8s/infrastructure/
kubectl apply -f k8s/inventory-api/ k8s/order-api/ k8s/price-api/ k8s/search-api/ k8s/notification-api/ k8s/apigateway/

# Verificar
kubectl get pods -n itm-tickets
kubectl get hpa -n itm-tickets
```

## Verificación de los criterios de la rúbrica

Cada uno de los criterios se verifica con los pasos descritos a continuación.
Los comandos están en PowerShell (Windows) sobre el setup de Terraform/Docker;
hay equivalentes en Kubernetes donde aplica.

### Integración funcional (MAUI + SignalR) + IA semántica

1. Abrir la App MAUI (Windows) e iniciar sesión con `user` / `user123`. La app
   guarda el JWT y queda disponible la pantalla de búsqueda.
2. Buscar "festival" con vibe "fiesta". El primer resultado es el Festival de
   los Dos Mundos, obtenido por la combinación de Elasticsearch (texto) y
   Qdrant (similitud vectorial) que implementa `SearchService`.
3. En el evento Medellín, comprar un asiento VIP. La MAUI genera un
   `X-Correlation-Id` y lo muestra en pantalla.
4. En una terminal aparte, ese correlation id puede rastrearse en los tres
   servicios:
   ```powershell
   docker logs order-api 2>&1        | Select-String "<correlation-id>"
   docker logs inventory-api 2>&1    | Select-String "<correlation-id>"
   docker logs notification-api 2>&1 | Select-String "<correlation-id>"
   ```
   Cada servicio emite una línea con el prefijo `[CID=<correlation-id>]`:
   order-api lo hace en el middleware HTTP (`[CID=...] >> POST /api/orders`),
   inventory-api en el interceptor gRPC server (`[CID=...] gRPC ...`), y
   notification-api en el consume filter de MassTransit (`[CID=...]
   Consumiendo OrderConfirmedEvent`).
   (En Kubernetes:
   `kubectl logs -n itm-tickets -l app=order-api | grep "<correlation-id>"`.)
5. El ticket llega a la MAUI en tiempo real por SignalR.

### Resiliencia y SAGA

Demuestra que un fallo transitorio del backend no rompe la orden ni pierde el
mensaje, gracias al Saga persistido y a la cola durable de RabbitMQ.

```powershell
# 1. Detener el consumer del Saga antes de comprar
docker stop inventory-api

# 2. Desde la MAUI, comprar un asiento. La orden queda en Processing.
#    Verificar que el mensaje quedó encolado, no perdido:
docker exec rabbitmq rabbitmqctl list_queues name messages | Select-String "inventory-request"
# inventory-request  1

# 3. Restablecer el consumer
docker start inventory-api
```

En 2–5 s MassTransit reentrega el mensaje, el Saga llama gRPC, publica
`OrderConfirmed`, y la MAUI recibe el ticket sin que se haya reintentado nada
manualmente.

Equivalente en Kubernetes (con los manifiestos de `k8s/`):

```bash
kubectl scale deployment/inventory-api -n itm-tickets --replicas=0
# comprar desde la MAUI …
kubectl scale deployment/inventory-api -n itm-tickets --replicas=2
```

### Self-healing — recuperación de un Pod en Kubernetes

Requisito: Kubernetes habilitado en Docker Desktop
(Settings → Kubernetes → Enable Kubernetes).

El manifiesto está en
[`infra/k8s/self-healing-demo-nginx.yaml`](infra/k8s/self-healing-demo-nginx.yaml).
Declara un `Namespace itm-tickets` + `Deployment inventory-api` con
`replicas: 1` usando `nginx:alpine` como contenedor de carga.

> Nota sobre la imagen: existe también
> [`infra/k8s/self-healing-demo.yaml`](infra/k8s/self-healing-demo.yaml) con
> la imagen local `itm-tickets-local/inventory-api:latest`. Solo funciona si
> Docker Desktop tiene desactivado *"Use containerd for pulling and storing
> images"* (Settings → General); de lo contrario, kubelet no la encuentra y
> reporta `ErrImageNeverPull`. La variante con `nginx:alpine` se usa porque
> es independiente de esa configuración y el patrón de self-healing que se
> verifica es el mismo (lo que se prueba es la reconciliación del
> Deployment Controller, no el contenido del contenedor).

Procedimiento. K8s genera el nombre del Pod dinámicamente; se captura en una
variable `$pod` para no depender de un nombre literal:

```powershell
# 1. Aplicar el manifiesto
kubectl apply -f infra/k8s/self-healing-demo-nginx.yaml

# 2. Esperar a que el Pod esté Ready
kubectl wait --for=condition=Ready pod -l app=inventory-api -n itm-tickets --timeout=120s

# 3. Capturar el nombre actual del Pod
$pod = (kubectl get pods -n itm-tickets -l app=inventory-api -o jsonpath='{.items[0].metadata.name}')
Write-Host "Pod ANTES: $pod"
kubectl get pods -n itm-tickets -o wide

# 4. Simular la caída del Pod
kubectl delete pod -n itm-tickets $pod --grace-period=0 --force

# 5. A los pocos segundos K8s ya creó un Pod nuevo
Start-Sleep -Seconds 3
kubectl get pods -n itm-tickets -o wide
$nuevo = (kubectl get pods -n itm-tickets -l app=inventory-api -o jsonpath='{.items[0].metadata.name}')
Write-Host "Pod DESPUES: $nuevo"

# 6. Eventos del Deployment Controller reconciliando
kubectl get events -n itm-tickets --sort-by='.lastTimestamp' | Select-Object -Last 10

# 7. Limpieza
kubectl delete -f infra/k8s/self-healing-demo-nginx.yaml
```

Salida esperada: el Pod inicial (`inventory-api-<hash>-<sufijo1>`) aparece en
estado `Running`; tras `kubectl delete pod`, en 2–5 s aparece otro Pod con
sufijo distinto y estado `Running`. El cambio de sufijo (e IP) confirma que es
un Pod nuevo creado por el Deployment Controller, no el mismo reiniciado.

Mecanismo: el `Deployment inventory-api` declara `replicas: 1`. El Deployment
Controller del control-plane ejecuta un loop continuo de reconciliación: si
detecta que el número de Pods actuales difiere del deseado, le pide al
scheduler que cree (o termine) Pods hasta cerrar el gap. Cuando el Pod se
borra con `kubectl delete`, el controller observa `current=0 ≠ desired=1` y
agenda un Pod nuevo, que el kubelet del nodo arranca a partir de la misma
imagen. Es el mecanismo nativo de self-healing de Kubernetes.

### Rendimiento (Redis + gRPC)

```powershell
Measure-Command { Invoke-RestMethod "http://localhost:8080/api/prices/aaaaaaaa-1111-1111-1111-111111111111" }
Measure-Command { Invoke-RestMethod "http://localhost:8080/api/prices/aaaaaaaa-1111-1111-1111-111111111111" }
Measure-Command { Invoke-RestMethod "http://localhost:8080/api/prices/aaaaaaaa-1111-1111-1111-111111111111" }
```

Comportamiento esperado:

- **Primera llamada**: cache miss + cold start del proceso .NET (JIT, pool de
  conexiones a Postgres, etc.). En Docker Desktop sobre Windows ronda los
  1–4 segundos la primera vez que se golpea el endpoint tras `terraform apply`.
- **Segunda y tercera llamadas**: cache hit servido por Redis, en el rango
  de 20–80 ms. El speedup respecto a la primera llamada es de uno o dos
  órdenes de magnitud.

La evidencia explícita de cache MISS vs HIT está en los logs del servicio:

```powershell
docker logs price-api 2>&1 | Select-String "Cache"
```

Devuelve una línea `Cache MISS` por la primera consulta y `Cache HIT` por
las siguientes. El TTL configurado es de 5 minutos.

La llamada interna `Order → Inventory` viaja por gRPC sobre HTTP/2 en
plaintext (h2c), no por HTTP/1.1 REST.

### DevOps y Cloud — Terraform

`infra/terraform/main.tf` describe la totalidad del ecosistema en código:

- `docker_network.itm`: red bridge con DNS interno por nombre de servicio.
- `docker_volume.*`: persistencia para Postgres, Redis, RabbitMQ,
  Elasticsearch y Qdrant.
- `docker_image.*` con `build {}`: compila cada microservicio desde su
  Dockerfile.
- `docker_container.*` con `depends_on`: orquesta el orden de arranque.

`terraform destroy && terraform apply` reconstruye los 13 contenedores desde
cero sin requerir credenciales cloud — todo en Docker Desktop local.

## CI/CD Pipeline

El pipeline (`.github/workflows/ci-cd.yaml`) ejecuta automáticamente:

1. **Build & Test**: Restaura, compila la solución y corre tests (xUnit).
2. **Docker Build & Push** (solo `main`): 6 imágenes con multi-stage build a Docker Hub.
3. **Deploy** (solo `main`): Aplica manifiestos de Kubernetes y verifica el rollout.

Secrets requeridos:
- `DOCKER_HUB_USERNAME`, `DOCKER_HUB_TOKEN`
- `KUBE_CONFIG` (kubeconfig en base64)

## Rúbrica de Calificación — Estado

| Criterio | Peso | Estado |
|---|---|---|
| **Integración Funcional** (MAUI + SignalR) | 1.5 | ✅ App MAUI con login JWT, búsqueda, compra y recepción de ticket vía SignalR |
| **Resiliencia y SAGA** | 1.0 | ✅ Saga persistido en PostgreSQL + EF Core; mensajes durables en RabbitMQ; consumer reintenta tras caída |
| **Rendimiento** (Redis/gRPC) | 1.0 | ✅ Redis distributed cache con expiración 5 min; gRPC HTTP/2 entre Order e Inventory |
| **DevOps y Cloud** | 1.0 | ✅ Dockerfiles multi-stage + K8s + HPA + GitHub Actions + Terraform main.tf |
| **IA Semántica** | 0.5 | ✅ Búsqueda híbrida Elasticsearch (texto) + Qdrant (vectores) en `SearchService` |

## Créditos

Proyecto desarrollado para la asignatura **Programación Distribuida** del **Instituto Tecnológico Metropolitano (ITM)** — Semestre 2026-1.

**Empresa ficticia:** Estudiantes ITM S.A.S.
**Evento:** Festival de los Dos Mundos — The World Tour 2026
