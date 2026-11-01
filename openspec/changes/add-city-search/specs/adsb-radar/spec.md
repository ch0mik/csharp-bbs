# Delta for ADS-B radar city search

## ADDED Requirements

### Requirement: Select city as session radar center
The radar center screen SHALL offer S (Search) alongside Enter, M and Q.
Search SHALL accept a city name of 2–64 characters and list up to five matches.
Each match SHALL include country, state/voivodeship and county, displaying a dash
for missing administrative data. Empty input SHALL return without changing the center.

#### Scenario: Choose a matching city
- GIVEN multiple matching cities are displayed with their administrative areas
- WHEN the user selects a valid numbered match
- THEN its coordinates become the session center with source SEARCH
- AND the screen displays those coordinates and waits for Enter before starting the radar.

#### Scenario: Unavailable provider or no matches
- WHEN the provider fails, times out or returns no matching cities
- THEN the UI explains the outcome and retains the previous center.

### Requirement: Bounded asynchronous geocoding
Geocoding SHALL use validated IOptions, HTTPS, URL-encoded user input, no redirects,
a 64 KiB response limit and a configurable 1–10 second deadline. Session cancellation
SHALL propagate through the request and body processing. Invalid coordinates and
terminal control characters SHALL NOT be passed through to the radar or terminal.
