# Proposal: bound archive downloads and decompression

## Why

The BBS accepts C64 archives fetched from remote sources. ZIP and GZip expansion currently has no limit, so a compressed payload can exhaust process memory.

## Scope

- Limit downloaded archive payloads and decompressed output to 100 MiB.
- Reject ZIP entries with oversized declared or actual output, excessive compression ratio, or unsupported archive metadata.
- Stream GZip decompression through a counting bounded stream.
- Add tests for ZIP/GZip payloads at, below, and above the limit.
- Preserve the existing user-facing behaviour: rejected download yields a safe failure message.

## Non-goals

- No automatic extraction of arbitrary multi-file archives.
- No change to Telnet transport or the public diagnostic endpoint.
