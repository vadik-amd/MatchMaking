# MatchMaking System

## Prerequisites

- Docker and Docker Compose
- .NET 9 SDK (for local development)

## Running the Solution

1. Start all services:
```bash
docker compose up -d --build
```

2. Verify services are running:
```bash
docker compose ps
```

3. Access the API at `http://localhost:5001/swagger`


## Stop the Application

```bash
docker compose down
```
