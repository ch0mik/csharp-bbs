# C# PETSCII BBS

C# PETSCII BBS to serwer BBS nastawiony na klienty PETSCII (styl Commodore 64).
Projekt wzorowany na https://github.com/sblendorio/petscii-bbs zostal sportowany do C#/.NET 10 i dziala w trybie tylko PETSCII i wystawia:
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
  - QuizPetscii (quiz w stylu Milionerzy: sesja, wznowienie, JSON, A/B/C/D, paginacja N/P)
- Galeria PETSCII oparta o pliki z dysku
- Konwersja PNG/JPEG do PETSCII w locie w galerii
- Opcjonalny backend sesji w Redis
- Konwersja online obrazkow do PETSCII w:
  - CommodoreNews
  - WikipediaPetscii
  - WordPress / 8-bitz
  - Globalny przelacznik sesyjny w menu glownym do wylaczenia obrazkow PETSCII (`I`)
- Opcjonalny cache Redis dla online PETSCII (TTL: 7 dni)
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
- `QUIZ_PACKS_ROOT`
  - Sciezka do zewnetrznego katalogu paczek quizu (`*.json`).
  - Rekomendowany mount docker: `./quiz-packs:/app/quiz-packs:ro`
  - Fallback: automatyczne wykrycie lokalnego katalogu `quiz-packs`.
- `BBS_SESSION_STORE`
  - Tryb backendu sesji: `inmemory` (domyslnie) albo `redis`
- `REDIS_HOST`, `REDIS_PORT`, `REDIS_PASSWORD`
  - Uzywane dla backendu sesji (`BBS_SESSION_STORE=redis`)
  - Uzywane tez przez cache obrazkow PETSCII online (CommodoreNews/Wikipedia/WordPress)
- `BBS_INSTANCE_ID`
  - Opcjonalny identyfikator instancji (do kluczy Redis)
- `BBS_INLINE_PETSCII_IMAGES`
  - Globalny przelacznik obrazkow PETSCII online w tenantach contentowych
  - Domyslnie: `true`
- `BBS_COMMODORENEWS_INLINE_PETSCII_IMAGES`
  - Nadpisanie tylko dla CommodoreNews
  - Domyslnie: dziedziczone z `BBS_INLINE_PETSCII_IMAGES`
- `BBS_WIKI_INLINE_PETSCII_IMAGES`
  - Nadpisanie tylko dla WikipediaPetscii
  - Domyslnie: dziedziczone z `BBS_INLINE_PETSCII_IMAGES`
- `BBS_WORDPRESS_INLINE_PETSCII_IMAGES`
  - Nadpisanie tylko dla WordPress / 8-bitz
  - Domyslnie: dziedziczone z `BBS_INLINE_PETSCII_IMAGES`

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
      - QUIZ_PACKS_ROOT=/app/quiz-packs
      - BBS_SESSION_STORE=inmemory
    volumes:
      - ./petscii-art-gallery:/app/petscii-art-gallery:ro
      - ./zmpp:/app/zmpp:ro
      - ./quiz-packs:/app/quiz-packs:ro
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
      - QUIZ_PACKS_ROOT=/app/quiz-packs
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

## Sterowanie Obrazkami Inline

W menu glownym (`StdChoice`):
- `I` przelacza obrazki PETSCII na biezaca sesje we wszystkich wspieranych tenantach (`Inline IMG: ON/OFF`).
  - Dotyczy: `8-bitz blog`, `CommodoreNews`, `WikipediaPetscii`.
- `7` uruchamia `QuizPetscii`.

W tenantcie `8-bitz blog`:
- `N+` / `N-` zmienia strone listy wpisow.
- Id wpisu (np. `1259`) otwiera artykul.
- Podczas ogladania obrazka: `ENTER=Next`, `T=Text`, `.=Back`.

## Format JSON Quizu

Paczki quizu to zwykle pliki JSON w katalogu `QUIZ_PACKS_ROOT`.

```json
{
  "header": {
    "id": "quiz_pl_8bit_v1",
    "language": "pl",
    "title": "Quiz 8-bit PL v1",
    "description": "Krotki quiz o komputerach 8-bit i klasycznych grach.",
    "version": "1.0.0",
    "author": "CsharpBbs",
    "createdAt": "2026-04-23",
    "tags": ["8-bit", "retro", "gry"]
  },
  "questions": [
    {
      "id": "PL001",
      "q": "Kto wyprodukowal C64?",
      "a": "Commodore",
      "b": "Atari",
      "c": "Sinclair",
      "d": "Apple",
      "correct": "A"
    }
  ]
}
```

Walidacja:
- `header` jest wymagany.
- `header.description` jest wymagany.
- minimum `25` pytan.
- kazde pytanie musi miec `a/b/c/d`.
- `correct` musi byc jednym z `A/B/C/D`.

## Podsumowanie Quizu

- Przeplyw startu: jezyk -> paczka quizu -> naglowek quizu -> gra.
- Dlugosc rundy: `25` losowych pytan z wybranej paczki.
- Sterowanie w pytaniu: `A/B/C/D` odpowiedz, `N/P` nawigacja stron dla dlugich pytan, `.` wyjscie z potwierdzeniem.
- Zachowanie sesji: niedokonczona gra moze byc wznowiona w tej samej sesji BBS.
- Punktacja: wynik jest pokazywany tylko na koncu (`score` + `%`), bez podpowiedzi per pytanie w trakcie gry.
- Ekran koncowy zawiera buzie PETSCII wg progu wyniku: `0-30`, `31-50`, `51-70`, `71-90`, `91-100`.

## Lokalny build .NET (opcjonalnie)

```powershell
dotnet build Bbs.Server\Bbs.Server.csproj
```






