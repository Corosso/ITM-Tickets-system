# Infraestructura como Código — Terraform + Docker Desktop

Provisiona el ecosistema completo de ITM-Tickets Global en el daemon de
Docker local. Reemplaza al `docker-compose.yml` mostrando Infraestructura
como Código real, sin necesidad de cloud ni cuenta cloud.

## Prerrequisitos

- Docker Desktop corriendo (Windows / macOS / Linux)
- Terraform >= 1.6.0 instalado (`terraform -version`)

## Despliegue

```powershell
cd infra/terraform
terraform init                       # descarga los providers
terraform plan                       # muestra los 25 recursos que va a crear
terraform apply -auto-approve
```

El primer `apply` tarda 3-8 minutos porque compila las 7 imágenes Docker de
los microservicios. Los siguientes son segundos (solo recrea lo que cambió).

Al terminar Terraform imprime los puertos:

```
gateway_url         = "http://localhost:8080"
rabbitmq_management = "http://localhost:15672"
elasticsearch_url   = "http://localhost:9200"
qdrant_dashboard    = "http://localhost:6333/dashboard"
```

## Verificar que todo arrancó

```powershell
docker ps --filter "network=itm-net"
```

Deberías ver 13 contenedores: 5 de infraestructura (postgres, redis, rabbitmq,
elasticsearch, qdrant) y 7 microservicios + el gateway.

## Probar el flujo de compra

### En PowerShell (Windows)

```powershell
# 1. Login → guarda el JWT en una variable
$loginBody = @{ Username = "user"; Password = "user123" } | ConvertTo-Json
$auth = Invoke-RestMethod -Uri "http://localhost:8080/api/auth/login" `
    -Method POST -ContentType "application/json" -Body $loginBody
$token = $auth.token
Write-Host "Token: $token"

# 2. Buscar eventos (Elasticsearch + Qdrant)
$headers = @{ Authorization = "Bearer $token" }
Invoke-RestMethod -Uri "http://localhost:8080/api/search?q=festival&vibe=fiesta" `
    -Headers $headers | ConvertTo-Json -Depth 5

# 3. Crear orden (dispara Saga + gRPC + RabbitMQ + SignalR)
$orderBody = @{
    userId      = "11111111-1111-1111-1111-111111111111"
    email       = "demo@itm.edu.co"
    phoneNumber = "300-000-0000"
    items       = @(
        @{
            eventId    = "aaaaaaaa-1111-1111-1111-111111111111"
            section    = "VIP"
            row        = 1
            seatNumber = 1
            quantity   = 1
            unitPrice  = 250
        }
    )
} | ConvertTo-Json -Depth 5

$orderHeaders = @{
    Authorization      = "Bearer $token"
    "X-Correlation-Id" = "demo-001"
}

Invoke-RestMethod -Uri "http://localhost:8080/api/orders" `
    -Method POST -ContentType "application/json" `
    -Headers $orderHeaders -Body $orderBody | ConvertTo-Json -Depth 5
```

### En bash (Git Bash / WSL / Linux / macOS)

```bash
# 1. Login (Auth API vía Gateway)
TOKEN=$(curl -s -X POST http://localhost:8080/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"Username":"user","Password":"user123"}' | jq -r .token)

# 2. Buscar eventos (Elasticsearch + Qdrant)
curl -s "http://localhost:8080/api/search?q=festival&vibe=fiesta" \
  -H "Authorization: Bearer $TOKEN" | jq

# 3. Crear orden (dispara Saga + gRPC + RabbitMQ + SignalR)
curl -s -X POST http://localhost:8080/api/orders \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -H "X-Correlation-Id: demo-001" \
  -d '{
    "userId":"11111111-1111-1111-1111-111111111111",
    "email":"demo@itm.edu.co",
    "phoneNumber":"300-000-0000",
    "items":[{"eventId":"aaaaaaaa-1111-1111-1111-111111111111","section":"VIP","row":1,"seatNumber":1,"quantity":1,"unitPrice":250}]
  }' | jq
```

### Alternativas sin terminal

- **Archivos `.http` en Visual Studio**: el repo ya tiene varios
  (`ITM-Tickets Global.Order.Api.http`, `ITM-Tickets Global.ApiService.http`,
  etc.). Abrirlos en VS 2022/2026 muestra botones "Send Request" sobre cada
  petición — un click y listo.
- **App MAUI**: ver sección abajo.

## Verificar la propagación del Correlation ID entre los 3 servicios

### En PowerShell

```powershell
docker logs order-api 2>&1        | Select-String "demo-001"
docker logs inventory-api 2>&1    | Select-String "demo-001"
docker logs notification-api 2>&1 | Select-String "demo-001"
```

### En bash

```bash
docker logs order-api 2>&1        | grep "demo-001"
docker logs inventory-api 2>&1    | grep "demo-001"
docker logs notification-api 2>&1 | grep "demo-001"
```

El correlation id `demo-001` (el que se envió en el header de la petición
inicial) debe aparecer en los logs de los tres servicios. Cada uno lo emite
con el prefijo `[CID=demo-001]`: order-api desde el middleware HTTP y desde
el consume filter de RabbitMQ, inventory-api desde el server interceptor
gRPC, y notification-api desde el consume filter de RabbitMQ.

## Probar con la App MAUI

```powershell
dotnet run --project "src\Mobile\ITM-Tickets Global.MauiApp" -f net10.0-windows10.0.19041.0
```

1. Login con `user` / `user123`.
2. Buscar "festival" con vibe "fiesta" → ves los resultados del Search API.
3. Click "Comprar VIP fila 1 asiento 1".
4. El ticket aparece en pantalla en tiempo real vía SignalR.

## Destruir todo

```powershell
terraform destroy -auto-approve
```

Esto elimina los 13 contenedores, la red `itm-net` y los volúmenes. Las
imágenes oficiales de Docker Hub quedan en cache local (`keep_locally = true`);
las imágenes locales de los microservicios sí se borran.

## Estructura del main.tf

| Recurso | Propósito |
| --- | --- |
| `docker_network.itm` | Red bridge `itm-net` con DNS interno por nombre |
| `docker_volume.*` | Volúmenes persistentes para datos |
| `docker_image.postgres / redis / rabbitmq / elasticsearch / qdrant` | Pull de imágenes oficiales |
| `docker_container.postgres` | Postgres + script de init que crea las dos BDs |
| `docker_container.rabbitmq` | Broker con UI en :15672 |
| `docker_container.elasticsearch / qdrant` | Backends de búsqueda híbrida |
| `null_resource.build_images` (for_each) | Compila las 7 imágenes locales con `docker build` vía PowerShell |
| `docker_container.*_api` | Levanta los contenedores con env vars + `depends_on` |
| `docker_container.apigateway` | Punto único de entrada, expone puerto 8080 al host |

## Variables que se pueden sobreescribir

```powershell
terraform apply `
  -var="image_tag=itm-tickets-prod" `
  -var="rabbitmq_password=OtraClave!" `
  -auto-approve
```

## Notas

- El provider `kreuzwerker/docker` lee `DOCKER_HOST` del entorno; con Docker
  Desktop instalado funciona "out of the box" en Windows, macOS y Linux.
- El `null_resource` usa `PowerShell` como interpreter porque `cmd.exe` tiene
  un bug con paths que contienen espacios (como "Materias ITM").
- Para regenerar solo una imagen tras un cambio de código:
  `terraform apply -replace='null_resource.build_images["order-api"]'`.
- Si Elasticsearch falla por falta de memoria, sube los `ES_JAVA_OPTS` en el
  bloque `env` del recurso `docker_container.elasticsearch`.
- Si el puerto 5432 (Postgres) está ocupado, parar el servicio del sistema
  o cambiar el `external` en el bloque `ports` del `docker_container.postgres`.
