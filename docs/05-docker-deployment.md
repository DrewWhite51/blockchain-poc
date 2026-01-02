# Phase 5: Docker Deployment

## Overview

Package the blockchain node as a Docker container to enable easy deployment and multi-node network creation. Users can run containers to join the blockchain network.

## Goals

- Create Dockerfile for containerization
- Create Docker Compose for multi-node network
- Configure networking and volumes
- Enable environment-based configuration
- Test local multi-node deployment

## Step 1: Create Dockerfile

**File:** `Dockerfile`

```dockerfile
# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project file and restore dependencies
COPY *.csproj ./
RUN dotnet restore

# Copy source code and build
COPY . ./
RUN dotnet build -c Release -o /app/build

# Publish application
FROM build AS publish
RUN dotnet publish -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Copy published application
COPY --from=publish /app/publish .

# Create data directory for blockchain storage
RUN mkdir -p /app/data

# Expose ports
# 8080 - Web UI/API
# 5001 - P2P Communication
EXPOSE 8080 5001

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV Blockchain__DataDirectory=/app/data

# Run the application
ENTRYPOINT ["dotnet", "blockchain-example-project.dll"]
```

## Step 2: Create .dockerignore

**File:** `.dockerignore`

```
# Build outputs
bin/
obj/
out/

# IDE
.vs/
.vscode/
.idea/
*.user
*.suo

# Data files (don't copy into image)
data/
*.json
*.db

# Git
.git/
.gitignore

# Documentation
*.md
docs/

# OS files
.DS_Store
Thumbs.db

# Other
node_modules/
TestResults/
```

## Step 3: Create Docker Compose (Multi-Node Network)

**File:** `docker-compose.yml`

```yaml
version: '3.8'

services:
  # Blockchain Node 1 (Seed Node)
  blockchain-node-1:
    build: .
    container_name: blockchain-node-1
    ports:
      - "8081:8080"  # Web UI
      - "5001:5001"  # P2P (exposed for external connections)
    volumes:
      - node1-data:/app/data
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ASPNETCORE_URLS=http://+:8080
      - Blockchain__Difficulty=2
      - Blockchain__MiningReward=50.0
      - Blockchain__DataDirectory=/app/data
      - P2P__ListenPort=5001
      - P2P__SeedNodes__0=blockchain-node-2:5001
      - P2P__SeedNodes__1=blockchain-node-3:5001
      - P2P__MaxPeers=10
      - P2P__HeartbeatInterval=30000
      - P2P__SyncInterval=60000
    networks:
      - blockchain-network
    hostname: blockchain-node-1
    restart: unless-stopped

  # Blockchain Node 2
  blockchain-node-2:
    build: .
    container_name: blockchain-node-2
    ports:
      - "8082:8080"  # Web UI
      - "5002:5001"  # P2P
    volumes:
      - node2-data:/app/data
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ASPNETCORE_URLS=http://+:8080
      - Blockchain__Difficulty=2
      - Blockchain__MiningReward=50.0
      - Blockchain__DataDirectory=/app/data
      - P2P__ListenPort=5001
      - P2P__SeedNodes__0=blockchain-node-1:5001
      - P2P__SeedNodes__1=blockchain-node-3:5001
      - P2P__MaxPeers=10
      - P2P__HeartbeatInterval=30000
      - P2P__SyncInterval=60000
    networks:
      - blockchain-network
    hostname: blockchain-node-2
    restart: unless-stopped
    depends_on:
      - blockchain-node-1

  # Blockchain Node 3
  blockchain-node-3:
    build: .
    container_name: blockchain-node-3
    ports:
      - "8083:8080"  # Web UI
      - "5003:5001"  # P2P
    volumes:
      - node3-data:/app/data
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ASPNETCORE_URLS=http://+:8080
      - Blockchain__Difficulty=2
      - Blockchain__MiningReward=50.0
      - Blockchain__DataDirectory=/app/data
      - P2P__ListenPort=5001
      - P2P__SeedNodes__0=blockchain-node-1:5001
      - P2P__SeedNodes__1=blockchain-node-2:5001
      - P2P__MaxPeers=10
      - P2P__HeartbeatInterval=30000
      - P2P__SyncInterval=60000
    networks:
      - blockchain-network
    hostname: blockchain-node-3
    restart: unless-stopped
    depends_on:
      - blockchain-node-1
      - blockchain-node-2

volumes:
  node1-data:
    driver: local
  node2-data:
    driver: local
  node3-data:
    driver: local

networks:
  blockchain-network:
    driver: bridge
```

### What This Creates

- **3 independent blockchain nodes**
- **Separate web UIs** on ports 8081, 8082, 8083
- **P2P network** connecting all nodes
- **Persistent storage** for each node (survives restarts)
- **Automatic peer discovery** via seed nodes
- **Isolated network** for node communication

## Step 4: Build and Test

### 4.1 Build Docker Image

```bash
# Build the image
docker build -t blockchain-poc:latest .

# Verify image was created
docker images | grep blockchain-poc
```

### 4.2 Test Single Node

```bash
# Run single node
docker run -d \
  --name blockchain-test \
  -p 8080:8080 \
  -p 5001:5001 \
  -v blockchain-test-data:/app/data \
  blockchain-poc:latest

# View logs
docker logs -f blockchain-test

# Access UI
# Open browser to http://localhost:8080

# Stop and remove
docker stop blockchain-test
docker rm blockchain-test
```

### 4.3 Test Multi-Node Network with Docker Compose

```bash
# Build and start all nodes
docker-compose up -d --build

# View all logs
docker-compose logs -f

# View specific node logs
docker-compose logs -f blockchain-node-1

# Check status of all containers
docker-compose ps
```

Expected output:
```
NAME                   STATUS    PORTS
blockchain-node-1      Up        0.0.0.0:8081->8080/tcp, 0.0.0.0:5001->5001/tcp
blockchain-node-2      Up        0.0.0.0:8082->8080/tcp, 0.0.0.0:5002->5001/tcp
blockchain-node-3      Up        0.0.0.0:8083->8080/tcp, 0.0.0.0:5003->5001/tcp
```

### 4.4 Access Node UIs

Open browser to:
- **Node 1**: http://localhost:8081
- **Node 2**: http://localhost:8082
- **Node 3**: http://localhost:8083

### 4.5 Test P2P Network

1. **Create transaction on Node 1**:
   - Go to http://localhost:8081
   - Submit a transaction (Alice → Bob, 100)

2. **Check Node 2 and 3**:
   - Go to http://localhost:8082
   - Should see the transaction in pending list
   - Same on http://localhost:8083

3. **Mine on Node 2**:
   - Go to http://localhost:8082
   - Mine the block

4. **Verify on all nodes**:
   - All three nodes should show the new block
   - Blockchain synchronized across network!

### 4.6 Test Node Resilience

```bash
# Stop Node 2
docker stop blockchain-node-2

# Create transaction on Node 1
# Mine on Node 3
# Network continues without Node 2!

# Restart Node 2
docker start blockchain-node-2

# Watch logs - Node 2 syncs with network
docker logs -f blockchain-node-2
```

## Step 5: Management Commands

### Start/Stop Network

```bash
# Start all nodes
docker-compose up -d

# Stop all nodes (keeps data)
docker-compose stop

# Start again
docker-compose start

# Stop and remove containers (keeps volumes)
docker-compose down

# Stop and remove everything including blockchain data
docker-compose down -v

# Rebuild and restart
docker-compose up -d --build
```

### View Logs

```bash
# All nodes
docker-compose logs

# Follow all logs
docker-compose logs -f

# Specific node
docker-compose logs blockchain-node-1

# Last 100 lines
docker-compose logs --tail=100
```

### Inspect Volumes

```bash
# List volumes
docker volume ls | grep node

# Inspect volume
docker volume inspect blockchain-example-project_node1-data

# View blockchain data (while container is running)
docker exec blockchain-node-1 cat /app/data/blockchain.json
docker exec blockchain-node-1 cat /app/data/peers.json
```

### Execute Commands in Container

```bash
# Open shell in container
docker exec -it blockchain-node-1 /bin/bash

# View blockchain file
docker exec blockchain-node-1 cat /app/data/blockchain.json | jq .

# Check running processes
docker exec blockchain-node-1 ps aux
```

## Step 6: Deploy to Cloud

### Option 1: Deploy to DigitalOcean

```bash
# Build and tag image
docker build -t blockchain-poc:v1.0 .

# Save image
docker save blockchain-poc:v1.0 | gzip > blockchain-poc-v1.0.tar.gz

# Upload to server
scp blockchain-poc-v1.0.tar.gz user@server:/path/

# On server
docker load < blockchain-poc-v1.0.tar.gz
docker run -d --name blockchain-node \
  -p 80:8080 \
  -p 5001:5001 \
  -v blockchain-data:/app/data \
  -e P2P__SeedNodes__0=node1.example.com:5001 \
  blockchain-poc:v1.0
```

### Option 2: Docker Hub

```bash
# Tag image
docker tag blockchain-poc:latest yourusername/blockchain-poc:latest

# Push to Docker Hub
docker push yourusername/blockchain-poc:latest

# Anyone can now run:
docker run -d \
  -p 8080:8080 \
  -p 5001:5001 \
  -v blockchain-data:/app/data \
  -e P2P__SeedNodes__0=your-seed-node.com:5001 \
  yourusername/blockchain-poc:latest
```

### Option 3: AWS ECS / Azure Container Instances

Use the Docker image with container orchestration platforms. Configuration varies by platform.

## Step 7: Production Configuration

### Environment Variables for Production

```bash
docker run -d \
  --name blockchain-node \
  -p 80:8080 \
  -p 5001:5001 \
  -v blockchain-data:/app/data \
  -e ASPNETCORE_ENVIRONMENT=Production \
  -e Blockchain__Difficulty=3 \
  -e Blockchain__MiningReward=50.0 \
  -e P2P__SeedNodes__0=seed1.blockchain.com:5001 \
  -e P2P__SeedNodes__1=seed2.blockchain.com:5001 \
  -e P2P__MaxPeers=20 \
  --restart unless-stopped \
  blockchain-poc:latest
```

### docker-compose.prod.yml

**File:** `docker-compose.prod.yml`

```yaml
version: '3.8'

services:
  blockchain-node:
    image: yourusername/blockchain-poc:latest
    ports:
      - "80:8080"
      - "5001:5001"
    volumes:
      - blockchain-data:/app/data
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - Blockchain__Difficulty=3
      - Blockchain__MiningReward=50.0
      - P2P__SeedNodes__0=seed-node-1.example.com:5001
      - P2P__SeedNodes__1=seed-node-2.example.com:5001
      - P2P__MaxPeers=20
    restart: unless-stopped
    logging:
      driver: "json-file"
      options:
        max-size: "10m"
        max-file: "3"

volumes:
  blockchain-data:
    driver: local
```

## Completion Checklist

- [ ] Dockerfile created
- [ ] Multi-stage build working
- [ ] .dockerignore created
- [ ] docker-compose.yml created
- [ ] Image builds successfully
- [ ] Single node runs in Docker
- [ ] Multi-node network starts
- [ ] Nodes can communicate
- [ ] Blockchain syncs across nodes
- [ ] Data persists in volumes
- [ ] Can stop/start without data loss
- [ ] Logs accessible via docker logs
- [ ] UI accessible from host browser

## Troubleshooting

### Issue: Build fails

**Solution:** Check Dockerfile syntax, ensure all files exist

### Issue: Container exits immediately

**Solution:** Check logs with `docker logs <container>`

### Issue: Nodes can't connect

**Solution:** Ensure they're on same Docker network, check seed node addresses

### Issue: Port already in use

**Solution:** Change port mapping: `"8090:8080"` instead of `"8080:8080"`

### Issue: Data not persisting

**Solution:** Verify volume mount: `-v blockchain-data:/app/data`

### Issue: Network not synchronizing

**Solution:** Check that seed nodes are correct and accessible

## Next Steps

1. Test the complete multi-node network
2. Move on to **Phase 6: User Documentation** for final guides
3. You now have a fully Dockerized, deployable blockchain!

## Summary

You now have:
- Containerized blockchain node
- Multi-stage Docker build
- Docker Compose for local testing
- 3-node test network
- Persistent storage
- Production deployment options
- Management commands
- Cloud deployment guides

Users can now run `docker run` to join your blockchain network!
