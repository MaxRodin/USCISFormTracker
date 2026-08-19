# Docker Deployment Guide

## Quick Start

### 1. Prerequisites

- Docker (v20.10+)
- Docker Compose (v2.0+)

### 2. Configuration

Copy the example environment file and configure your settings:

```bash
cp .env.example .env
```

Edit `.env` and set your Mailgun credentials:

```bash
# Required Mailgun settings
MAILGUN_API_KEY=your_api_key_here
MAILGUN_DOMAIN=your_domain_here
MAILGUN_FROM_EMAIL=noreply@yourdomain.com
MAILGUN_MAILING_LIST_ADDRESS=your_list@yourdomain.com

# Optional: Change default passwords
DATABASE_PASSWORD=secure_password
RABBITMQ_PASSWORD=secure_password
```

#### HTTPS (optional)

To serve the web frontend over HTTPS, place a PFX certificate (e.g., a
Cloudflare Origin Certificate) at `./certs/origin.pfx` — it is mounted into
the container and port 443 is enabled automatically. Without it, the site
serves HTTP-only on port 80.

### 3. Start Services

```bash
# Build and start all services
docker-compose up -d

# View logs
docker-compose logs -f

# View specific service logs
docker-compose logs -f processor
docker-compose logs -f emailer
```

### 4. Verify Services

- **RabbitMQ Management UI**: http://localhost:15672 (guest/guest)
- **Web frontend**: http://localhost (mailing-list signup page and recent changes)
- **Processor**: http://localhost:5000
- **PostgreSQL**: localhost:5432

Swagger UI (`/swagger`) is only available when `ASPNETCORE_ENVIRONMENT=Development`;
docker-compose defaults to `Production`.

## Services Overview

### Processor
- Monitors USCIS forms for changes
- Runs on schedule (default: daily at 2:00 AM)
- Publishes messages to RabbitMQ

### Emailer
- Consumes messages from RabbitMQ
- Sends email notifications via Mailgun
- Handles aggregate summaries and individual changes

### Web
- Static mailing-list signup page (`/`)
- `POST /mailing-list` — subscribe an email address
- `GET /changes/recent` — recent detected form changes
- Swagger UI at `/swagger` (Development environment only)

### PostgreSQL
- Stores form records and change history
- Persistent volume: `postgres_data`

### RabbitMQ
- Message broker for async communication
- Persistent volume: `rabbitmq_data`

## Common Commands

```bash
# Stop all services
docker-compose down

# Stop and remove volumes (deletes data!)
docker-compose down -v

# Rebuild after code changes
docker-compose up -d --build

# View service status
docker-compose ps

# Execute command in container
docker-compose exec processor /bin/bash

# Trigger manual form check (instead of waiting for schedule)
docker-compose restart processor
```

## Scheduling

The Processor runs on a configurable schedule. Modify in `.env`:

```bash
# Examples:
QUARTZ_CRON_SCHEDULE=0 0 2 * * ?    # Daily at 2:00 AM
QUARTZ_CRON_SCHEDULE=0 0 */6 * * ?  # Every 6 hours
QUARTZ_CRON_SCHEDULE=0 */30 * * * ? # Every 30 minutes
```

Cron format: `second minute hour day month dayOfWeek`

## Volumes

Persistent data is stored in Docker volumes:

```bash
# List volumes
docker volume ls | grep uscis

# Backup database
docker-compose exec postgres pg_dump -U postgres uscis_forms > backup.sql

# Restore database
docker-compose exec -T postgres psql -U postgres uscis_forms < backup.sql
```

## Troubleshooting

### Services won't start

Check logs for specific service:
```bash
docker-compose logs processor
docker-compose logs emailer
```

### Database connection errors

Ensure PostgreSQL is healthy:
```bash
docker-compose ps postgres
docker-compose logs postgres
```

### RabbitMQ connection errors

Check RabbitMQ status:
```bash
docker-compose ps rabbitmq
docker-compose logs rabbitmq
```

### No emails being sent

1. Check Mailgun credentials in `.env`
2. Verify Emailer is running: `docker-compose ps emailer`
3. Check Emailer logs: `docker-compose logs emailer`
4. Verify RabbitMQ has messages: http://localhost:15672

## Production Deployment

For production, consider:

1. **Use secrets management** instead of `.env` file
2. **Change default passwords** for PostgreSQL and RabbitMQ
3. **Enable SSL/TLS** for PostgreSQL and RabbitMQ
4. **Set up backups** for PostgreSQL database
5. **Use external volumes** for production data
6. **Configure monitoring** (Prometheus, Grafana)
7. **Set resource limits** in docker-compose.yml:

```yaml
services:
  processor:
    deploy:
      resources:
        limits:
          cpus: '1'
          memory: 512M
```

## First Run

On first run, the Processor will:
1. Discover all USCIS forms
2. Save them to the database
3. Send ONE aggregate email with summary

Subsequent runs will send individual emails for changes.

## Network Architecture

```
┌─────────────┐     ┌─────────────┐     ┌─────────────┐
│  Processor  │────▶│  RabbitMQ   │────▶│   Emailer   │
└─────────────┘     └─────────────┘     └─────────────┘
       │                                        │
       │                                        │
       ▼                                        ▼
┌─────────────┐                          ┌─────────────┐
│ PostgreSQL  │                          │   Mailgun   │
└─────────────┘                          └─────────────┘
       ▲
       │
┌─────────────┐
│     Web     │
└─────────────┘
```

All services communicate through the `uscis-network` bridge network.
