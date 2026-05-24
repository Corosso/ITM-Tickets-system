#!/bin/bash
# Crea las dos bases de datos que usan los microservicios.
# itm_tickets: para Inventory.Api (eventos, secciones, asientos)
# itm_tickets_orders: para Order.Api (saga state + tabla de órdenes)
set -e

psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" <<-EOSQL
    CREATE DATABASE itm_tickets_orders;
    GRANT ALL PRIVILEGES ON DATABASE itm_tickets_orders TO $POSTGRES_USER;
EOSQL
