# City search for the ADS-B radar center

Provide S (Search) on the radar center screen so users behind Azure ACI NAT can
select their location without knowing coordinates. Use the Open-Meteo geocoding
service, present up to five matching cities with country and region, and return
to coordinate confirmation before starting the radar. Preserve selection per session.
The shared aircraft feed and its coverage remain unchanged.

Use validated IOptions for endpoint and timeout. Bound input and response size,
escape user input, disable redirects and filter remote terminal control characters.
All I/O must be asynchronous and propagate CancellationToken. On provider failure,
retain the previous center and allow retry or manual coordinate entry.
Each list entry includes country, state/voivodeship (admin1) and county (admin2).
Missing administrative fields are shown as a dash; all five entries fit a 40x25 terminal.
