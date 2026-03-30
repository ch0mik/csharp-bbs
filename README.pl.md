# C# PETSCII BBS (tylko PETSCII)

C# PETSCII BBS to serwer BBS nastawiony na klienty PETSCII (styl Commodore 64).
Projekt wzorowany na https://github.com/sblendorio/petscii-bbs zostal sportowany do C#/.NET 10 i dziala tylko w trybie PETSCII.
Aktualna konfiguracja dziala w trybie tylko PETSCII i wystawia:
- `6510/tcp` dla sesji BBS (telnet)
- `9090/tcp` dla diagnostyki/statusu

## Funkcjonalnosc

- Tryb terminala tylko PETSCII (bez fallbacku ASCII)
- Modularne menu tenantow (entrypoint `StdChoice`)
- Wbudowane tenanty:
  - PetsciiArtGallery
  - RssPetscii
  - WikipediaPetscii
  - CSDB Releases
  - CSDB SD2IEC
  - ZorkMachine
  - CommodoreNews (newsy z `https://www.commodore.net/news` + fallback przez sitemap) - to unikalna, calkowicie nowa funkcjonalnosc w tym projekcie
  - 8-bitz blog
- Galeria PETSCII oparta o pliki z dysku
- Opcjonalny backend sesji w Redis
- Uruchamianie przez Docker i `docker compose`

## Konfiguracja

Glowna konfiguracja runtime jest przekazywana przez argumenty startowe i zmienne srodowiskowe.

### Najwazniejsze zmienne ENV

- `PETSCII_GALLERY_ROOT`
  - Sciezka do katalogu galerii PETSCII.
  - Fallback: `AppContext.BaseDirectory/petscii-art-gallery`
- `ZMACHINE_STORY_ROOT`
  - Sciezka do katalogu plikow Z-machine.
  - Fallback: `AppContext.BaseDirectory/zmpp`
- `BBS_SESSION_STORE`
  - Tryb backendu sesji: `inmemory` (domyslnie) albo `redis`
- `REDIS_HOST`, `REDIS_PORT`, `REDIS_PASSWORD`
  - Uzywane tylko gdy `BBS_SESSION_STORE=redis`
- `BBS_INSTANCE_ID`
  - Opcjonalny identyfikator instancji (do kluczy Redis)

### Docker Compose

#### 1) Prosty (`inmemory`, bez Redis)

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

#### 2) Z Redis

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

Do Redis uzyj `.env` z sekretami:

```env
BBS_SESSION_STORE=redis
REDIS_PASSWORD=zmien_na_dlugi_losowy_ciag
```

## Uruchomienie

### 1. Build i start

```powershell
docker compose up --build -d
```

### 2. Podglad logow

```powershell
docker compose logs -f csharp-bbs redis
```

### 3. Polaczenie do BBS

Polacz sie klientem PETSCII (np. SyncTERM):
- Host: `127.0.0.1`
- Port: `6510`

### 4. Zatrzymanie

```powershell
docker compose down
```

## Lokalny build .NET (opcjonalnie)

```powershell
dotnet build Bbs.Server\Bbs.Server.csproj
```





