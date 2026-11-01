# Tasks

- [x] Introduce a shared 100 MiB bounded-copy helper; allow environment override through `C64_ARCHIVE_MAX_BYTES`.
- [ ] Reject archive downloads whose HTTP body exceeds 100 MiB before buffering.
- [ ] Enforce ZIP entry size, actual extracted size, and compression-ratio limits.
- [ ] Enforce actual GZip output size while streaming decompression.
- [ ] Return a non-sensitive failure to the BBS client and log structured rejection reason.
- [ ] Add unit tests for normal archives, declared-size bypasses, ZIP bombs, and GZip bombs.
- [ ] Run build, full tests, and a memory-constrained container verification.
