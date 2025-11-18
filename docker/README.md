# TextProcessor Docker Setup

This directory contains Docker configurations for running the TextProcessor application in a production-like environment.

## Architecture

The application consists of three main components:

1. **API Container** (`textprocessor-api`) - .NET 9 Web API with SignalR
2. **UI Container** (`textprocessor-ui`) - React SPA served by Nginx
3. **Nginx Proxy** (`textprocessor-nginx`) - Reverse proxy with basic authentication

## Quick Start

### Prerequisites

- Docker Engine 20.10+
- Docker Compose V2
- At least 2GB RAM available for containers

### Development Setup

1. **Build and start all services:**
   ```bash
   cd docker
   docker-compose up --build
   ```

2. **Access the application:**
   - Application: http://localhost (with basic auth)
   - API directly: http://localhost:8080
   - Frontend directly: http://localhost:3000

3. **Default credentials:**
   - Username: `admin`
   - Password: `textprocessor2024`

### Production Setup

1. **Generate secure htpasswd file:**
   ```bash
   # Linux/macOS
   ./generate-htpasswd.sh myuser mypassword
   
   # Windows
   generate-htpasswd.bat myuser mypassword
   ```

2. **Update environment variables:**
   ```bash
   # Edit docker-compose.yml and set production values
   ```

3. **Deploy:**
   ```bash
   docker-compose up -d --build
   ```

## Services

### API Service
- **Port:** 8080
- **Health Check:** `/health`
- **Features:**
- Text processing endpoints
- SignalR real-time communication
- In-memory background job service with cooperative cancellation
- Structured logging with Serilog

### UI Service
- **Port:** 3000
- **Health Check:** `/health`
- **Features:**
  - React TypeScript SPA
  - Bootstrap UI components
  - Real-time SignalR integration
  - Responsive design

### Nginx Proxy
- **Port:** 80 (HTTP)
- **Features:**
  - Basic authentication for entire application
  - Reverse proxy to API and UI
  - WebSocket support for SignalR
  - Rate limiting
  - Security headers
  - Gzip compression

## Configuration

### Basic Authentication

The Nginx proxy requires basic authentication. Default credentials:
- **Username:** admin
- **Password:** textprocessor2024

To change credentials:
1. Generate new htpasswd file using the provided scripts
2. Replace the existing `htpasswd` file
3. Restart the nginx container

### Environment Variables

#### API Container
- `ASPNETCORE_ENVIRONMENT`: Production
- `ASPNETCORE_URLS`: http://+:8080
- `ASPNETCORE_FORWARDEDHEADERS_ENABLED`: true
- `SignalRHealthUrl`: `http://localhost:8080/hubs/processing`

#### UI Container

- Built with production environment variables from `.env.production`, keeping browser requests on nginx via `/api` and `/hubs/processing`

### Volumes

- `api-logs`: API application logs
- `nginx-logs`: Nginx access and error logs

### Networking

All containers run on the `textprocessor-network` bridge network with subnet `172.20.0.0/16`.

## Monitoring

### Health Checks

All services include health check endpoints:
- API: `http://api:8080/health`
- UI: `http://ui:3000/health`
- Nginx: `http://nginx/health`

### Logs

View logs for specific services:
```bash
# API logs
docker-compose logs -f api

# UI logs
docker-compose logs -f ui

# Nginx logs
docker-compose logs -f nginx

# All services
docker-compose logs -f
```

### Container Status

Check container health:
```bash
docker-compose ps
```

## Development

### Local Development with Docker

For development, you can override specific services:

```yaml
# docker-compose.override.yml
version: '3.8'
services:
  api:
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
    ports:
      - "7180:8080"  # Different port to avoid conflicts
```

### Hot Reload

For development with hot reload:
1. Run only the API in Docker
2. Run the React app locally with `npm start`
3. Update environment variables to point to Docker API

## Security

### Production Considerations

1. **Change default credentials** in htpasswd file
2. **Use HTTPS** with proper SSL certificates
3. **Implement proper secrets management** (Azure Key Vault, etc.)
4. **Configure firewall rules** to restrict access
5. **Regular security updates** for base images
6. **Monitor for vulnerabilities** in dependencies

### Network Security

- All inter-service communication happens on internal network
- Only Nginx proxy is exposed to external traffic
- Rate limiting prevents abuse
- Security headers prevent common attacks

## Troubleshooting

### Common Issues

1. **Port conflicts:**
   ```bash
   # Check what's using the ports
   netstat -tulpn | grep :80
   ```

2. **Container startup failures:**
   ```bash
   # Check container logs
   docker-compose logs [service-name]
   ```

3. **SignalR connection issues:**
   - Ensure WebSocket support is enabled in Nginx
   - Check CORS configuration
   - Verify authentication headers

4. **Health check failures:**
   ```bash
   # Test health endpoints manually
   curl http://localhost/health
   curl http://localhost:8080/health
   curl http://localhost:3000/health
   ```

### Performance Issues

1. **Increase memory limits:**
   ```yaml
   services:
     api:
       mem_limit: 1g
   ```

2. **Optimize Nginx worker processes:**
   ```nginx
   worker_processes auto;
   worker_connections 2048;
   ```

## Scaling

### Horizontal Scaling

To scale the API service:
```bash
docker-compose up --scale api=3
```

Note: You'll need to configure Nginx load balancing and shared state storage (Redis) for proper scaling.

### Vertical Scaling

Adjust container resource limits in docker-compose.yml:
```yaml
services:
  api:
    mem_limit: 2g
    cpus: 1.5
```

## Backup and Recovery

### Data Backup
```bash
# Backup logs
docker run --rm -v textprocessor_api-logs:/data -v $(pwd):/backup alpine tar czf /backup/api-logs-backup.tar.gz -C /data .

# Backup nginx config
docker run --rm -v textprocessor_nginx-logs:/data -v $(pwd):/backup alpine tar czf /backup/nginx-logs-backup.tar.gz -C /data .
```

### Disaster Recovery

1. **Save Docker images:**
   ```bash
   docker save textprocessor-api:latest | gzip > textprocessor-api.tar.gz
   docker save textprocessor-ui:latest | gzip > textprocessor-ui.tar.gz
   ```

2. **Restore from backup:**
   ```bash
   docker load < textprocessor-api.tar.gz
   docker load < textprocessor-ui.tar.gz
   ```

