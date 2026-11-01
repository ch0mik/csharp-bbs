# Delta for ADS-B radar

## ADDED Requirements

### Requirement: Shared feed polling
The system SHALL fetch each configured ADS-B feed once per polling interval regardless of client count.

#### Scenario: Multiple radar clients
- GIVEN two BBS clients open the radar
- WHEN the polling interval elapses
- THEN the system makes one feed request and both clients render the same snapshot.

### Requirement: Validated radar configuration
The system SHALL reject enabled radar configuration with an unsupported provider, invalid feed URL, or inconsistent timeouts.

#### Scenario: Invalid enabled feed
- GIVEN `AdsbRadar:Enabled` is true and `FeedUrl` is not HTTP(S)
- WHEN the server starts
- THEN startup fails before a BBS listener is opened.
