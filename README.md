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
  - CommodoreNews
  - 8-bitz blog
  - QuizPetscii (Milionerzy-style PETSCII quiz with session resume, JSON packs, A/B/C/D answers, N/P paging for long questions)
- Filesystem-driven PETSCII art gallery
- PNG/JPEG to PETSCII conversion on the fly in gallery
- Optional Redis-backed session presence tracking
- Online image-to-PETSCII rendering in:
  - WikipediaPetscii
  - CommodoreNews
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
- `QUIZ_PACKS_ROOT`
  - Path to external quiz packs directory (`*.json`).
  - Recommended docker mount: `./quiz-packs:/app/quiz-packs:ro`
  - Fallback: auto-detected local `quiz-packs` folder.
- `BBS_SESSION_STORE`
  - Session backend mode: `inmemory` (default) or `redis`
- `REDIS_HOST`, `REDIS_PORT`, `REDIS_PASSWORD`
  - Used for Redis session store (`BBS_SESSION_STORE=redis`)
  - Also used by online PETSCII image cache in Wikipedia/WordPress/8-bitz
- `BBS_INSTANCE_ID`
  - Optional instance id used in Redis keys
- `BBS_INLINE_PETSCII_IMAGES`
  - Global switch for online PETSCII images in content tenants
  - Default: `true`
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
      - QUIZ_PACKS_ROOT=/app/quiz-packs
      - BBS_SESSION_STORE=inmemory
    volumes:
      - ./petscii-art-gallery:/app/petscii-art-gallery:ro
      - ./zmpp:/app/zmpp:ro
      - ./quiz-packs:/app/quiz-packs:ro
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
      - QUIZ_PACKS_ROOT=/app/quiz-packs
      - BBS_SESSION_STORE=redis
      - REDIS_HOST=redis
      - REDIS_PORT=6379
      - REDIS_PASSWORD=${REDIS_PASSWORD}
      - BBS_INSTANCE_ID=${HOSTNAME:-csharp-bbs}
      - BBS_INLINE_PETSCII_IMAGES=true
      - BBS_WIKI_INLINE_PETSCII_IMAGES=true
      - BBS_WORDPRESS_INLINE_PETSCII_IMAGES=true
    volumes:
      - ./petscii-art-gallery:/app/petscii-art-gallery:ro
      - ./zmpp:/app/zmpp:ro
      - ./quiz-packs:/app/quiz-packs:ro
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
- `6` opens `CommodoreNews`.
- `7` opens `QuizPetscii`.
- `C` also opens `CommodoreNews`.

Inside `CommodoreNews` tenant:
- `N+` / `N-` switch post list page.
- Post id opens article.
- While viewing an inline image: `ENTER=Next`, `T=Text`, `.=Back`.

Inside `8-bitz blog` tenant:
- `N+` / `N-` switch post list page.
- Post id (for example `1259`) opens article.
- While viewing an inline image: `ENTER=Next`, `T=Text`, `.=Back`.

## Quiz JSON Format

Quiz packs are regular JSON files stored in the `QUIZ_PACKS_ROOT` directory.

```json
{
  "header": {
    "id": "quiz_en_8bit_v1",
    "language": "en",
    "title": "8-bit Quiz EN v1",
    "description": "Short quiz about 8-bit computers and classic games.",
    "version": "1.0.0",
    "author": "CsharpBbs",
    "createdAt": "2026-04-23",
    "tags": ["8-bit", "retro", "games"]
  },
  "questions": [
    {
      "id": "EN001",
      "q": "Who made C64?",
      "a": "Commodore",
      "b": "Atari",
      "c": "Sinclair",
      "d": "Apple",
      "correct": "A"
    }
  ]
}
```

Validation rules:
- `header` is required.
- `header.description` is required.
- at least `25` questions are required.
- each question must contain `a/b/c/d`.
- `correct` must be one of `A/B/C/D`.

## Quiz Summary

- Start flow: language -> quiz pack -> quiz header -> game.
- Round length: `25` random questions from selected pack.
- Controls in question view: `A/B/C/D` answer, `N/P` page navigation for long questions, `.` exit with confirmation.
- Session behavior: unfinished game can be resumed in the same BBS session.
- Scoring: points are shown only at the end (`score` + `%`), without per-question reveal during play.
- Final screen includes a PETSCII face based on result range: `0-30`, `31-50`, `51-70`, `71-90`, `91-100`.

## Local .NET build (optional)

```powershell
dotnet build Bbs.Server\Bbs.Server.csproj
```

## WOPR / WarGames

- One integrated `W) WarGames Simulator` tenant built around an IMSAI 8080 terminal.
- Seattle Public School District scenario with the `PENCIL` password, student lookup, grade editing, pagination, and session state.
- War dialer with configurable ranges, animated carrier scanning, remembered discoveries, and the original WOPR number `(311) 437-8739`.
- Native WOPR/JOSHUA conversation, Global Thermonuclear War, target and side selection, DEFCON escalation, trajectory displays, accelerated tic-tac-toe learning, session resume, and the cinematic ending.
- A 40-column PETSCII interface with cancellation-aware input and animations; no Linux programs, speech synthesizer, or WAV playback are required.

Choose `W) WarGames Simulator` in `StdChoice`, or enter the `WOPR`, `WARGAMES`, or `WAR GAMES` alias. This opens one IMSAI 8080 terminal experience. Call the known school computer with option `1`, or use option `2` to war dial for unknown systems and discover WOPR. Log on to WOPR as `JOSHUA` and follow the terminal conversation to play Global Thermonuclear War. `.` or `QUIT` moves back through the terminal stack to the BBS.

The v1 tenant is a native 40-column PETSCII adaptation of the cinematic game path. It includes side and target selection, DEFCON escalation, deterministic tic-tac-toe learning, and the movie ending. It intentionally excludes the source project's regular user accounts, mail, administration, GPT, Internet/ARPANET, Linux commands, speech, and WAV playback.

### Seattle school scenario

Inside the WarGames IMSAI terminal choose `1) CALL SCHOOL DISTRICT`. The password is `PENCIL` (case-insensitive). Search for `LIGHTMAN` or `MACK`, browse course records with `N/P`, and use `E` to change a grade by class number. Grade changes remain available for the current BBS session. `.` disconnects back to the IMSAI terminal.

### War dialer

Inside the WarGames IMSAI terminal choose `2) WAR DIAL FOR UNKNOWN SYSTEMS`. The default scan covers `(311) 437-8700..8750` and discovers WOPR at `(311) 437-8739`. Use `V` to view systems found in the current session and select its letter to dial. Scans are limited to 251 numbers; entries backed by unavailable original Linux programs are listed but cannot be connected in v1.
