# ============================================================================
# ITM-Tickets Global · Infraestructura como Código (Docker Desktop)
# ----------------------------------------------------------------------------
# Provisiona el ecosistema completo en el daemon de Docker local. Crea:
#   - Red bridge `itm-net` con DNS interno por nombre de servicio.
#   - Volúmenes persistentes para datos.
#   - 5 contenedores de infraestructura (Postgres, Redis, RabbitMQ, ES, Qdrant).
#   - 7 imágenes Docker locales construidas con `docker build` (via local-exec
#     para evitar un bug del provider legacy con paths que tienen espacios).
#   - 7 contenedores de microservicios + API Gateway.
#
# Uso:
#   cd infra/terraform
#   terraform init
#   terraform apply -auto-approve
#
# Destruir todo:
#   terraform destroy -auto-approve
# ============================================================================

terraform {
  required_version = ">= 1.6.0"

  required_providers {
    docker = {
      source  = "kreuzwerker/docker"
      version = "~> 3.0"
    }
    null = {
      source  = "hashicorp/null"
      version = "~> 3.2"
    }
  }
}

provider "docker" {}

# ----------------------------------------------------------------------------
# Variables
# ----------------------------------------------------------------------------
variable "image_tag" {
  description = "Prefijo del tag para imágenes locales."
  type        = string
  default     = "itm-tickets-local"
}

variable "rabbitmq_password" {
  description = "Contraseña compartida (RabbitMQ + Postgres)."
  type        = string
  default     = "ChangeMe123!"
  sensitive   = true
}

variable "project_root" {
  description = "Raíz del repo (dos niveles arriba de infra/terraform/)."
  type        = string
  default     = "../.."
}

locals {
  network_name            = "itm-net"
  rabbitmq_url            = "amqp://itm_admin:${var.rabbitmq_password}@rabbitmq:5672"
  postgres_inventory_conn = "Host=postgres;Database=itm_tickets;Username=itm_admin;Password=${var.rabbitmq_password}"
  postgres_orders_conn    = "Host=postgres;Database=itm_tickets_orders;Username=itm_admin;Password=${var.rabbitmq_password}"

  # Definición declarativa de cada microservicio: dockerfile, tag y dependencias.
  # Esto permite iterar para construir todos con un solo patrón.
  services = {
    inventory-api    = "src/Services/ITM-Tickets Global.Inventory.Api/Dockerfile"
    order-api        = "src/Services/ITM-Tickets Global.Order.Api/Dockerfile"
    price-api        = "src/Services/ITM-Tickets Global.Price.Api/Dockerfile"
    notification-api = "src/Services/ITM-Tickets Global.Notification.Api/Dockerfile"
    search-api       = "src/Services/ITM-Tickets Global.Search.Api/Dockerfile"
    auth-api         = "ITM-Tickets Global.ApiService/Dockerfile"
    apigateway       = "src/ApiGateway/ITM-Tickets Global.ApiGateway/Dockerfile"
  }

  # Ruta absoluta del project_root para que docker build no se confunda.
  project_root_abs = abspath(var.project_root)
}

# ----------------------------------------------------------------------------
# Red compartida
# ----------------------------------------------------------------------------
resource "docker_network" "itm" {
  name   = local.network_name
  driver = "bridge"
}

# ============================================================================
# IMÁGENES LOCALES — Construidas con `docker build` vía local-exec.
# ----------------------------------------------------------------------------
# Por qué local-exec en vez de docker_image.build: el provider legacy de
# kreuzwerker/docker tiene un bug que falla con "unexpected EOF" cuando el
# Dockerfile está en una carpeta con espacios. Usar el CLI directo lo evita.
# ============================================================================
resource "null_resource" "build_images" {
  for_each = local.services

  triggers = {
    # Reconstruye si cambia el Dockerfile.
    dockerfile = filesha256("${local.project_root_abs}/${each.value}")
  }

  provisioner "local-exec" {
    # PowerShell evita el bug de stripping de comillas que tiene cmd.exe
    # cuando el path contiene espacios (ej: "Materias ITM"). Docker en
    # Windows acepta forward slashes así que no necesitamos `replace`.
    interpreter = ["PowerShell", "-NoProfile", "-Command"]
    command     = "& docker build -t '${var.image_tag}/${each.key}:latest' -f '${local.project_root_abs}/${each.value}' '${local.project_root_abs}'"
  }
}

# ============================================================================
# INFRAESTRUCTURA (imágenes oficiales)
# ============================================================================

# ---- PostgreSQL --------------------------------------------------------------
resource "docker_volume" "postgres" {
  name = "itm-postgres-data"
}

resource "docker_image" "postgres" {
  name         = "postgres:17-alpine"
  keep_locally = true
}

resource "docker_container" "postgres" {
  name  = "postgres"
  image = docker_image.postgres.image_id

  networks_advanced {
    name = docker_network.itm.name
  }

  ports {
    internal = 5432
    external = 5432
  }

  env = [
    "POSTGRES_DB=itm_tickets",
    "POSTGRES_USER=itm_admin",
    "POSTGRES_PASSWORD=${var.rabbitmq_password}",
  ]

  volumes {
    volume_name    = docker_volume.postgres.name
    container_path = "/var/lib/postgresql/data"
  }

  upload {
    file       = "/docker-entrypoint-initdb.d/01-init.sh"
    executable = true
    content    = <<-EOT
      #!/bin/bash
      set -e
      psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" <<-EOSQL
        CREATE DATABASE itm_tickets_orders;
        GRANT ALL PRIVILEGES ON DATABASE itm_tickets_orders TO $POSTGRES_USER;
      EOSQL
    EOT
  }

  restart = "always"
}

# ---- Redis -------------------------------------------------------------------
resource "docker_volume" "redis" {
  name = "itm-redis-data"
}

resource "docker_image" "redis" {
  name         = "redis:7.4-alpine"
  keep_locally = true
}

resource "docker_container" "redis" {
  name  = "redis"
  image = docker_image.redis.image_id

  networks_advanced {
    name = docker_network.itm.name
  }

  ports {
    internal = 6379
    external = 6379
  }

  volumes {
    volume_name    = docker_volume.redis.name
    container_path = "/data"
  }

  restart = "always"
}

# ---- RabbitMQ ----------------------------------------------------------------
resource "docker_volume" "rabbitmq" {
  name = "itm-rabbitmq-data"
}

resource "docker_image" "rabbitmq" {
  name         = "rabbitmq:4.0-management-alpine"
  keep_locally = true
}

resource "docker_container" "rabbitmq" {
  name  = "rabbitmq"
  image = docker_image.rabbitmq.image_id

  networks_advanced {
    name = docker_network.itm.name
  }

  ports {
    internal = 5672
    external = 5672
  }

  ports {
    internal = 15672
    external = 15672
  }

  env = [
    "RABBITMQ_DEFAULT_USER=itm_admin",
    "RABBITMQ_DEFAULT_PASS=${var.rabbitmq_password}",
  ]

  volumes {
    volume_name    = docker_volume.rabbitmq.name
    container_path = "/var/lib/rabbitmq"
  }

  restart = "always"
}

# ---- Elasticsearch -----------------------------------------------------------
resource "docker_volume" "elasticsearch" {
  name = "itm-elastic-data"
}

resource "docker_image" "elasticsearch" {
  name         = "docker.elastic.co/elasticsearch/elasticsearch:8.13.4"
  keep_locally = true
}

resource "docker_container" "elasticsearch" {
  name  = "elasticsearch"
  image = docker_image.elasticsearch.image_id

  networks_advanced {
    name = docker_network.itm.name
  }

  ports {
    internal = 9200
    external = 9200
  }

  env = [
    "discovery.type=single-node",
    "xpack.security.enabled=false",
    "ES_JAVA_OPTS=-Xms512m -Xmx512m",
  ]

  volumes {
    volume_name    = docker_volume.elasticsearch.name
    container_path = "/usr/share/elasticsearch/data"
  }

  restart = "always"
}

# ---- Qdrant ------------------------------------------------------------------
resource "docker_volume" "qdrant" {
  name = "itm-qdrant-data"
}

resource "docker_image" "qdrant" {
  name         = "qdrant/qdrant:v1.12.4"
  keep_locally = true
}

resource "docker_container" "qdrant" {
  name  = "qdrant"
  image = docker_image.qdrant.image_id

  networks_advanced {
    name = docker_network.itm.name
  }

  ports {
    internal = 6333
    external = 6333
  }

  ports {
    internal = 6334
    external = 6334
  }

  volumes {
    volume_name    = docker_volume.qdrant.name
    container_path = "/qdrant/storage"
  }

  restart = "always"
}

# ============================================================================
# CONTENEDORES DE MICROSERVICIOS (imágenes construidas por null_resource)
# ============================================================================

resource "docker_container" "inventory_api" {
  name       = "inventory-api"
  image      = "${var.image_tag}/inventory-api:latest"
  depends_on = [null_resource.build_images, docker_container.postgres]

  networks_advanced {
    name = docker_network.itm.name
  }

  ports {
    internal = 8080
    external = 5002
  }

  env = [
    "ASPNETCORE_URLS=http://+:8080",
    "ConnectionStrings__DefaultConnection=${local.postgres_inventory_conn}",
  ]

  restart = "always"
}

resource "docker_container" "order_api" {
  name  = "order-api"
  image = "${var.image_tag}/order-api:latest"
  depends_on = [
    null_resource.build_images,
    docker_container.postgres,
    docker_container.rabbitmq,
    docker_container.inventory_api,
  ]

  networks_advanced {
    name = docker_network.itm.name
  }

  ports {
    internal = 8080
    external = 5003
  }

  env = [
    "ASPNETCORE_URLS=http://+:8080",
    "ConnectionStrings__DefaultConnection=${local.postgres_orders_conn}",
    "ConnectionStrings__RabbitMQ=${local.rabbitmq_url}",
    "services__inventory-api__http__0=http://inventory-api:8080",
  ]

  restart = "always"
}

resource "docker_container" "price_api" {
  name       = "price-api"
  image      = "${var.image_tag}/price-api:latest"
  depends_on = [null_resource.build_images, docker_container.redis]

  networks_advanced {
    name = docker_network.itm.name
  }

  ports {
    internal = 8080
    external = 5004
  }

  env = [
    "ASPNETCORE_URLS=http://+:8080",
    "ConnectionStrings__Redis=redis:6379",
  ]

  restart = "always"
}

resource "docker_container" "notification_api" {
  name       = "notification-api"
  image      = "${var.image_tag}/notification-api:latest"
  depends_on = [null_resource.build_images, docker_container.rabbitmq]

  networks_advanced {
    name = docker_network.itm.name
  }

  ports {
    internal = 8080
    external = 5005
  }

  env = [
    "ASPNETCORE_URLS=http://+:8080",
    "ConnectionStrings__RabbitMQ=${local.rabbitmq_url}",
  ]

  restart = "always"
}

resource "docker_container" "search_api" {
  name  = "search-api"
  image = "${var.image_tag}/search-api:latest"
  depends_on = [
    null_resource.build_images,
    docker_container.elasticsearch,
    docker_container.qdrant,
  ]

  networks_advanced {
    name = docker_network.itm.name
  }

  ports {
    internal = 8080
    external = 5006
  }

  env = [
    "ASPNETCORE_URLS=http://+:8080",
    "Elasticsearch__Uri=http://elasticsearch:9200",
    "Qdrant__Host=qdrant",
    "Qdrant__Port=6334",
  ]

  restart = "always"
}

resource "docker_container" "auth_api" {
  name       = "auth-api"
  image      = "${var.image_tag}/auth-api:latest"
  depends_on = [null_resource.build_images]

  networks_advanced {
    name = docker_network.itm.name
  }

  ports {
    internal = 8080
    external = 5007
  }

  env = ["ASPNETCORE_URLS=http://+:8080"]

  restart = "always"
}

resource "docker_container" "apigateway" {
  name  = "apigateway"
  image = "${var.image_tag}/apigateway:latest"
  depends_on = [
    null_resource.build_images,
    docker_container.auth_api,
    docker_container.order_api,
    docker_container.price_api,
    docker_container.search_api,
    docker_container.notification_api,
  ]

  networks_advanced {
    name = docker_network.itm.name
  }

  ports {
    internal = 8080
    external = 8080
  }

  env = [
    "ASPNETCORE_URLS=http://+:8080",
    "services__auth-api__http__0=http://auth-api:8080",
    "services__order-api__http__0=http://order-api:8080",
    "services__price-api__http__0=http://price-api:8080",
    "services__search-api__http__0=http://search-api:8080",
    "services__notification-api__http__0=http://notification-api:8080",
  ]

  restart = "always"
}

# ----------------------------------------------------------------------------
# Outputs
# ----------------------------------------------------------------------------
output "gateway_url" {
  value       = "http://localhost:8080"
  description = "Punto de entrada de toda la app"
}

output "rabbitmq_management" {
  value       = "http://localhost:15672"
  description = "UI de RabbitMQ (user: itm_admin)"
}

output "elasticsearch_url" {
  value       = "http://localhost:9200"
  description = "Para inspeccionar índices"
}

output "qdrant_dashboard" {
  value       = "http://localhost:6333/dashboard"
  description = "UI web de Qdrant"
}

output "services_running" {
  value = {
    apigateway       = "localhost:8080"
    auth_api         = "localhost:5007"
    order_api        = "localhost:5003"
    inventory_api    = "localhost:5002"
    price_api        = "localhost:5004"
    notification_api = "localhost:5005"
    search_api       = "localhost:5006"
    postgres         = "localhost:5432"
    redis            = "localhost:6379"
    rabbitmq         = "localhost:5672"
    elasticsearch    = "localhost:9200"
    qdrant_grpc      = "localhost:6334"
  }
  description = "Tabla de servicios"
}
