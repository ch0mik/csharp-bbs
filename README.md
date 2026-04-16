# C# PETSCII BBS

C# PETSCII BBS is a PETSCII-first BBS server for Commodore 64 style clients.
It was inspired by https://github.com/sblendorio/petscii-bbs and ported to C#/.NET 10 in PETSCII-only mode.
The current setup runs in PETSCII-only mode and exposes:
- `6510/tcp` for BBS telnet sessions
- `9090/tcp` for diagnostics/status

## Features

- PETSCII-only terminal mode (no ASCII fallback)
- Modular tenant menu (`StdChoice` entrypoint)
- Built-in tenants:
  - PetsciiArtGallery
  - RssPetscii
  - WikipediaPetscii
  - CSDB Releases
  - CSDB SD2IEC
  - ZorkMachine
  - CommodoreNews (news list from `https://www.commodore.net/news` with sitemap fallback) - unique, completely new functionality in this project
  - 8-bitz blog
- Filesystem-driven PETSCII art gallery
- PNG/JPEG to PETSCII conversion on the fly in gallery
- Optional Redis-backed session presence tracking
- Online image-to-PETSCII rendering in:
  - CommodoreNews
  - WikipediaPetscii
  - WordPress / 8-bitz
  - Global per-session toggle from main menu to disable inline PETSCII images (`I`)
- Optional Redis cache for online PETSCII images (TTL: 7 days)
- Dockerized runtime with `docker compose`

## Configuration

Main runtime configuration is passed by command-line arguments and environment variables.

### Important environment variables

- `PETSCII_GALLERY_ROOT`
  - Path to PETSCII gallery directory.
  - Fallback: `AppContext.BaseDirectory/petscii-art-gallery`
- `ZMACHINE_STORY_ROOT`
  - Path to Z-machine stories directory.
  - Fallback: `AppContext.BaseDirectory/zmpp`
- `BBS_SESSION_STORE`
  - Session backend mode: `inmemory` (default) or `redis`
- `REDIS_HOST`, `REDIS_PORT`, `REDIS_PASSWORD`
  - Used for Redis session store (`BBS_SESSION_STORE=redis`)
  - Also used by online PETSCII image cache in CommodoreNews/Wikipedia/WordPress
- `BBS_INSTANCE_ID`
  - Optional instance id used in Redis keys
- `BBS_INLINE_PETSCII_IMAGES`
  - Global switch for online PETSCII images in content tenants
  - Default: `true`
- `BBS_COMMODORENEWS_INLINE_PETSCII_IMAGES`
  - CommodoreNews-specific override
  - Default: inherited from `BBS_INLINE_PETSCII_IMAGES`
- `BBS_WIKI_INLINE_PETSCII_IMAGES`
  - Wikipedia-specific override
  - Default: inherited from `BBS_INLINE_PETSCII_IMAGES`
- `BBS_WORDPRESS_INLINE_PETSCII_IMAGES`
  - WordPress / 8-bitz-specific override
  - Default: inherited from `BBS_INLINE_PETSCII_IMAGES`

### Docker Compose

#### 1) Simple (`inmemory`, no Redis)

```yaml
services:
  csharp-bbs:
    build:
      context: .
      dockerfile: Dockerfile
    ports:
      - "6510:6510"
      - "9090:9090"
    command: ["--bbs", "StdChoice:6510", "-s", "9090", "-t", "3600000"]
    environment:
      - PETSCII_GALLERY_ROOT=/app/petscii-art-gallery
      - ZMACHINE_STORY_ROOT=/app/zmpp
      - BBS_SESSION_STORE=inmemory
    volumes:
      - ./petscii-art-gallery:/app/petscii-art-gallery:ro
      - ./zmpp:/app/zmpp:ro
    restart: unless-stopped
```

#### 2) With Redis

```yaml
services:
  csharp-bbs:
    build:
      context: .
      dockerfile: Dockerfile
    ports:
      - "6510:6510"
      - "9090:9090"
    command: ["--bbs", "StdChoice:6510", "-s", "9090", "-t", "3600000"]
    environment:
      - PETSCII_GALLERY_ROOT=/app/petscii-art-gallery
      - ZMACHINE_STORY_ROOT=/app/zmpp
      - BBS_SESSION_STORE=redis
      - REDIS_HOST=redis
      - REDIS_PORT=6379
      - REDIS_PASSWORD=${REDIS_PASSWORD}
      - BBS_INSTANCE_ID=${HOSTNAME:-csharp-bbs}
      - BBS_INLINE_PETSCII_IMAGES=true
      - BBS_COMMODORENEWS_INLINE_PETSCII_IMAGES=true
      - BBS_WIKI_INLINE_PETSCII_IMAGES=true
      - BBS_WORDPRESS_INLINE_PETSCII_IMAGES=true
    volumes:
      - ./petscii-art-gallery:/app/petscii-art-gallery:ro
      - ./zmpp:/app/zmpp:ro
    networks:
      - bbs_net
      - redis_net
    restart: unless-stopped

  redis:
    image: redis:7.4-alpine
    command:
      - redis-server
      - --appendonly
      - "yes"
      - --save
      - "60"
      - "1000"
      - --requirepass
      - ${REDIS_PASSWORD}
    expose:
      - "6379"
    volumes:
      - redis-data:/data
    networks:
      - redis_net
    restart: unless-stopped

networks:
  bbs_net:
    driver: bridge
  redis_net:
    driver: bridge
    internal: true

volumes:
  redis-data:
```

Use `.env` for Redis secrets:

```env
BBS_SESSION_STORE=redis
REDIS_PASSWORD=change_me_to_a_long_random_value
```

## Run

### 1. Build and start

```powershell
docker compose up --build -d
```

### 2. Watch logs

```powershell
docker compose logs -f csharp-bbs redis
```

### 3. Connect to BBS

Use a PETSCII-capable client (for example SyncTERM):
- Host: `127.0.0.1`
- Port: `6510`

### 4. Stop

```powershell
docker compose down
```

## Inline Image Controls

In main menu (`StdChoice`):
- `I` toggles inline PETSCII images for current session in all supported tenants (`Inline IMG: ON/OFF`).
  - Affects: `8-bitz blog`, `CommodoreNews`, `WikipediaPetscii`.

Inside `8-bitz blog` tenant:
- `N+` / `N-` switch post list page.
- Post id (for example `1259`) opens article.
- While viewing an inline image: `ENTER=Next`, `T=Text`, `.=Back`.

## Local .NET build (optional)

```powershell
dotnet build Bbs.Server\Bbs.Server.csproj
```






