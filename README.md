# ITM-Tickets Global "The World Tour 2026"

Sistema distribuido de boletería para el **Festival de los Dos Mundos**, con sedes simultáneas en Medellín (Colombia) y Madrid (España). Soporta 50,000 usuarios concurrentes en el minuto cero de la venta, garantizando consistencia transaccional, alta disponibilidad y comunicación en tiempo real.

**Asignatura:** Programación Distribuida — Instituto Tecnológico Metropolitano (ITM)

**Stack:** .NET 10 (Preview) · .NET Aspire · YARP · gRPC · MassTransit + RabbitMQ · Redis · SignalR · Elasticsearch + Qdrant · Docker · Kubernetes · GitHub Actions

---

## Arquitectura del Sistema

```
┌─────────────────────────────────────────────────────────────┐
│                    Clientes (Blazor Web)                     │
└─────────────────────┬───────────────────────────────────────┘
                      │ HTTPS
┌─────────────────────▼───────────────────────────────────────┐
│          YARP API Gateway (Rate Limiting + JWT)             │
│   /api/auth/*  /api/orders/*  /api/prices/*  /api/search/* │
│                     /api/notifications/*                    │
└──┬────────────┬──────────┬──────────┬───────────┬──────────┘
   │            │          │          │           │
   ▼            ▼          ▼          ▼           ▼
┌──────┐ ┌──────────┐ ┌─────────┐ ┌──────────┐ ┌───────────────┐
│ Auth │ │  Order   │ │  Price  │ │  Search  │ │ Notification  │
│ API  │ │   API    │ │   API   │ │   API    │ │     API       │
│      │ │          │ │         │ │          │ │               │
│      │ │◄─gRPC──►│ │  Redis  │ │  Elastic │ │  SignalR Hub  │
│      │ │ Inventory│ │         │ │  Qdrant  │ │               │
│      │ │   API    │ │         │ │          │ │               │
└──────┘ └──┬───┬───┘ └─────────┘ └──────────┘ └──┬────────┬───┘
            │   │                                  │        │
            │   │  MassTransit                     │        │
            ▼   ▼  (RabbitMQ)                      │        │
       ┌──────────────┐                            │        │
       │   RabbitMQ   │◄───────────────────────────┘        │
       │  (Message    │  Publica OrderCreatedEvent          │
       │   Broker)    │  Consume OrderConfirmedEvent        │
       └──────────────┘                                     │
              │                                              │
              ▼                                              │
       ┌──────────────┐                                     │
       │ PostgreSQL   │                                     │
       └──────────────┘                                     │
                                                            │
       ┌────────────────────────────────────────────────────┘
       │  SignalR notifica en tiempo real: TicketReadyEvent
       ▼
    [Cliente recibe notificación de boleta lista]
```

## Componentes del Ecosistema

### A. Frontera y Seguridad

| Componente | Descripción | Tecnología |
|---|---|---|
| **API Gateway** | Punto de entrada único. Valida JWT, aplica Rate Limiting (100 req/s), rutea a microservicios internos. | YARP Reverse Proxy 2.3.0 |
| **Auth API** | Generación y validación de tokens JWT para autenticación de clientes. | JWT Bearer |

### B. Núcleo de Microservicios

| Servicio | Puerto | Responsabilidad | Patrón/Tecnología |
|---|---|---|---|
| **Order.Api** | 5003 | Gestión transaccional de órdenes de compra. Coordina con inventario vía gRPC. | SAGA (MassTransit State Machine) |
| **Inventory.Api** | 5002 | Validación de disponibilidad de asientos. Comunicación binaria de alta velocidad. | gRPC Server (protobuf) |
| **Price.Api** | 5004 | Consulta de precios con caché distribuida (90% hit rate esperado). | Redis (StackExchange.Redis) |
| **Search.Api** | 5006 | Búsqueda textual y semántica de eventos. Recomendaciones por "vibe". | Elasticsearch (NEST) + Qdrant |
| **Notification.Api** | 5005 | Envío de confirmaciones en tiempo real a clientes conectados. | SignalR + MassTransit Consumer |

### C. Comunicación Asíncrona y Tiempo Real

- **MassTransit + RabbitMQ**: El Order.Api publica `OrderCreatedEvent` → Saga State Machine coordina el flujo → Notification.Api consume `OrderConfirmedEvent` y `TicketReadyEvent`.
- **SignalR**: NotificationHub (`/hubs/notifications`) notifica al cliente cuando la boleta está lista, permitiendo suscripción por `orderId`.

### D. Infraestructura y DevOps

- **Contenerización**: 7 Dockerfiles con multi-stage build (SDK → ASPNET runtime).
- **Orquestación local**: `docker-compose.yml` con RabbitMQ, Redis, PostgreSQL y todos los microservicios.
- **Kubernetes**: Manifiestos completos en `k8s/` con Deployments, Services, HPA (CPU > 70%, Mem > 80%), PVCs para infraestructura.
- **CI/CD**: GitHub Actions pipeline que compila, prueba, construye imágenes Docker y despliega en Kubernetes al hacer push a `main`.

## Estructura del Proyecto

```
ITM-Tickets Global/
├── .github/workflows/
│   └── ci-cd.yaml                        # Pipeline CI/CD
├── k8s/
│   ├── namespace.yaml                    # Namespace: itm-tickets
│   ├── secrets.yaml                      # Credenciales (JWT, DB, RabbitMQ)
│   ├── infrastructure/                   # postgres, redis, rabbitmq
│   ├── apigateway/                       # deployment + service (LoadBalancer) + hpa
│   ├── inventory-api/                    # deployment + service + hpa
│   ├── order-api/                        # deployment + service + hpa
│   ├── price-api/                        # deployment + service + hpa
│   ├── search-api/                       # deployment + service + hpa
│   └── notification-api/                 # deployment + service + hpa
├── src/
│   ├── ApiGateway/
│   │   └── ITM-Tickets Global.ApiGateway/    # YARP Gateway
│   ├── Services/
│   │   ├── ITM-Tickets Global.Inventory.Api/ # gRPC Server
│   │   ├── ITM-Tickets Global.Order.Api/     # REST + Saga
│   │   ├── ITM-Tickets Global.Price.Api/     # Redis Cache
│   │   ├── ITM-Tickets Global.Search.Api/    # Elasticsearch + Qdrant
│   │   └── ITM-Tickets Global.Notification.Api/ # SignalR + Consumers
│   └── Shared/
│       └── ITM-Tickets Global.Shared/        # Protos, DTOs, Eventos, Modelos
├── ITM-Tickets Global.ApiService/            # Auth API (JWT)
├── ITM-Tickets Global.AppHost/               # .NET Aspire Orchestrator
├── ITM-Tickets Global.Web/                   # Blazor Web Frontend
├── ITM-Tickets Global.ServiceDefaults/       # OpenTelemetry, Resiliencia, Service Discovery
├── ITM-Tickets Global.slnx                   # Solución .NET 10
└── docker-compose.yml                        # Orquestación local
```

## Ejecución Local

### Requisitos

- .NET 10.0 SDK (Preview)
- Docker Desktop
- PowerShell / Bash

### Con Docker Compose

```bash
# Levantar toda la infraestructura y servicios
docker compose up -d

# Verificar estado
docker compose ps

# La API Gateway estará disponible en http://localhost:8080
```

### Servicios y Puertos

| Servicio | Puerto |
|---|---|
| API Gateway | 8080 |
| Order API | 5003 |
| Inventory API (gRPC) | 5002 |
| Price API | 5004 |
| Notification API | 5005 |
| Search API | 5006 |
| Auth API | 5007 |
| RabbitMQ Management | 15672 |
| Redis | 6379 |
| PostgreSQL | 5432 |

### Con .NET Aspire (Desarrollo)

```bash
dotnet run --project "ITM-Tickets Global.AppHost"
```

### Flujo de Compra (Happy Path)

```
1. POST /api/auth/login         → Obtener token JWT
2. GET  /api/search?query=...   → Buscar eventos
3. POST /api/orders             → Crear orden (SAGA inicia)
4. gRPC ReserveSeats            → Inventory valida disponibilidad
5. GET  /api/prices/{eventId}   → Precio desde caché Redis
6. SignalR NotificationHub      → Cliente recibe TicketReadyEvent en tiempo real
```

## Despliegue en Kubernetes

```bash
# Aplicar namespace e infraestructura
kubectl apply -f k8s/namespace.yaml
kubectl apply -f k8s/secrets.yaml
kubectl apply -f k8s/infrastructure/

# Desplegar microservicios
kubectl apply -f k8s/inventory-api/
kubectl apply -f k8s/order-api/
kubectl apply -f k8s/price-api/
kubectl apply -f k8s/notification-api/
kubectl apply -f k8s/search-api/
kubectl apply -f k8s/apigateway/

# Verificar despliegue
kubectl get pods -n itm-tickets
kubectl get hpa -n itm-tickets
```

### Autoescalado (HPA)

Todos los microservicios escalan automáticamente:

| Servicio | Réplicas base | Min | Max |
|---|---|---|---|
| apigateway | 2 | 2 | 10 |
| inventory-api | 2 | 2 | 10 |
| order-api | 3 | 3 | 15 |
| price-api | 2 | 2 | 8 |
| search-api | 2 | 2 | 8 |
| notification-api | 2 | 2 | 8 |

## CI/CD Pipeline

El pipeline de GitHub Actions (`.github/workflows/ci-cd.yaml`) ejecuta automáticamente:

1. **Build & Test**: Restaura dependencias, compila la solución, ejecuta pruebas.
2. **Docker Build & Push** (solo `main`): Construye 6 imágenes con multi-stage build y las publica en Docker Hub.
3. **Deploy** (solo `main`): Aplica los manifiestos de Kubernetes y verifica el rollout de cada deployment.

Secrets requeridos en GitHub:
- `DOCKER_HUB_USERNAME` / `DOCKER_HUB_TOKEN`
- `KUBE_CONFIG` (kubeconfig del clúster)

---

## Pendiente por Terminar / Mejoras Futuras

### Alta Prioridad (Requerimientos del Proyecto)

| Tarea | Descripción | Impacto en rúbrica |
|---|---|---|
| **App .NET MAUI** | El frontend actual es Blazor Web, no una app móvil .NET MAUI. Falta crear el proyecto MAUI con consumo del API Gateway vía HTTPS. | Integración Funcional (1.5) |
| **Terraform (main.tf)** | No existe infraestructura como código. Falta el archivo `main.tf` para recrear la infraestructura cloud (AKS/EKS, redes, etc.). | DevOps y Cloud (1.0) |
| **Implementación real de Elasticsearch + Qdrant** | `SearchService.cs` retorna datos mock/hardcodeados. No hay integración real con Elasticsearch ni Qdrant. Tampoco están configurados en `docker-compose.yml` ni en `k8s/infrastructure/`. | IA Semántica (0.5) |
| **Persistencia real en base de datos** | `InventoryServiceImpl.cs` usa `Random.Shared.Next()` para simular respuestas. Ningún servicio tiene DbContext, migraciones ni consultas reales a PostgreSQL. | Resiliencia y SAGA (1.0) |

### Media Prioridad

| Tarea | Descripción |
|---|---|
| **Usuarios reales con base de datos** | Auth API tiene usuarios hardcodeados (`admin/admin123`, `user/user123`). Falta Identity con Entity Framework y migraciones. |
| **Pruebas unitarias y de integración** | No existe ningún proyecto de tests (xUnit/NUnit). El pipeline CI/CD ejecuta `dotnet test` pero no hay tests que correr. |
| **Correlation ID viajando entre servicios** | Falta implementar el encabezado `X-Correlation-Id` propagándose a través de gRPC, HTTP y RabbitMQ para trazabilidad en logs. |
| **Índices y mappings de Elasticsearch** | Search.Api referencia los paquetes NuGet (NEST, Qdrant.Client) pero no crea índices ni realiza operaciones reales. |

### Baja Prioridad

| Tarea | Descripción |
|---|---|
| **.dockerignore** | No existe archivo `.dockerignore` para optimizar el contexto de build de Docker. |
| **Archivo .env** | No hay variables de entorno unificadas para desarrollo local fuera de Docker Compose. |
| **Health Checks UI** | .NET Aspire AppHost está scaffold pero no integrado con todos los servicios para dashboard de monitoreo. |
| **API Documentation (Swagger/OpenAPI)** | Solo Search.Api y Notification.Api tienen referencias a OpenApi, falta Swagger en todos los endpoints REST. |

---

## Rúbrica de Calificación

| Criterio | Peso | Estado actual |
|---|---|---|
| **Integración Funcional** (MAUI + SignalR) | 1.5 | Pendiente (falta MAUI) |
| **Resiliencia y SAGA** (Saga + RabbitMQ) | 1.0 | Estructura lista, falta persistencia real |
| **Rendimiento** (Redis/gRPC) | 1.0 | Estructura lista, falta validación de latencias |
| **DevOps y Cloud** (GitHub Actions + K8s + Terraform) | 1.0 | CI/CD y K8s listos, falta Terraform |
| **IA Semántica** (Elasticsearch + Qdrant) | 0.5 | Paquetes referenciados, implementación mock |

---

## Créditos

Proyecto desarrollado para la asignatura **Programación Distribuida** del **Instituto Tecnológico Metropolitano (ITM)** — Semestre 2026-1.

**Empresa ficticia:** Estudiantes ITM S.A.S.  
**Evento:** Festival de los Dos Mundos — The World Tour 2026
