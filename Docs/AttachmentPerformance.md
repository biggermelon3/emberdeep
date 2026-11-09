# Attachment performance — 2026-10-14

The pickup slowdown came from two independent preview/highlight queries scanning every pair of sockets. The 15-part starting kit has 625 sockets; holding the chassis visited 81,400 socket pairs per query, including distant pieces. Each pair accessed Unity objects and searched the connection list for occupancy. Nearby candidates also repeated rotation and collision calculations.

## Implementation

- One preview query at 30 Hz in `WorkshopBootstrap.UpdatePreview`, after input movement. The ghost, line, and highlight share its result. Release always performs a fresh query. Hold, topology, and mode changes invalidate the preview.
- `AttachmentSearch` snapshots current rigidbody poses once per query and builds a reusable spatial hash with cells sized to `SocketDistance`. Only the 27 adjacent cells are searched. Candidates are checked nearest-first with deterministic ties.
- Occupancy is indexed as connections are committed and removed. Registration caches fixed part geometry; unregister removes it. No global physics synchronization is needed for preview searches.
- Cached collider-local geometry supplies conservative bounding boxes before exact penetration tests. Snapping still evaluates all 24 twist orientations.
- Ghost meshes are pooled by held assembly and topology, so changing candidate members does not recreate the ghost. Highlight renderers are cached and only retinted when selection or target changes.

Part geometry is fixed after factory initialization, as in the existing kit. New part geometry must be registered through the factory. Socket dimensions, grid density, collision rejection, save format, and snapping behavior are unchanged.

## Measurements

Unity 6000.3.21f1, Windows Editor, on the development PC, Deep Profile disabled. Thirty samples per case after warm-up. The 15-part benchmark contains one chassis, eight cubes, four cuboids, and two pyramids; the 50-part benchmark adds 35 pyramids. The chassis is the query source. “Near” moves one cube within connection range; “far” leaves all targets distant.

The frozen brute-force reference and optimized query run against identical layouts in the same test process. These are search CPU times, not headset frame-rate measurements.

| Parts | Placement | Old median / p95 (ms) | New median / p95 (ms) | New pair / penetration checks |
|---|---|---|---|---|
| 15 | Far | 70.005 / 128.202 | 0.318 / 0.404 | 0 / 0 |
| 15 | Near | 66.412 / 125.919 | 0.328 / 0.421 | 90 / 1 |
| 50 | Far | 163.358 / 245.151 | 0.502 / 0.722 | 0 / 0 |
| 50 | Near | 168.597 / 218.769 | 0.537 / 0.847 | 90 / 1 |

A separate PlayMode stress test runs one query every frame, rather than the production 30 Hz preview cadence. Both versions use the same workshop and target poses. Other editor activity and the workshop's 72 FPS cap contribute to these frame times; they are not a guarantee of normal gameplay FPS.

| Parts | Placement | Old frame median / p95 (ms) | New frame median / p95 (ms) |
|---|---|---|---|
| 15 | Far | 60.467 / 114.676 | 14.066 / 15.389 |
| 15 | Near | 76.035 / 133.263 | 13.812 / 14.865 |
| 50 | Far | 150.792 / 249.655 | 13.615 / 16.874 |
| 50 | Near | 149.019 / 199.202 | 13.956 / 16.129 |

Calibrated `GC.Alloc` profiler recorders detected **zero allocations** over repeated warmed search calls and repeated cached ghost/highlight updates. Initial pool growth and topology changes can allocate. The first baseline probe's `GC.GetAllocatedBytesForCurrentThread` returned zero even for the allocating legacy path, so it is not used as allocation evidence.

## Validation and reproduction

- **43/43 EditMode tests passed:** includes reference parity across 36 rotated/moving/blocked layouts, distance boundaries, distant-part culling, calibrated allocation recording, and benchmark budgets (p95 below 2 ms for 15 parts and 4 ms for 50 parts).
- **22/22 PlayMode tests passed:** includes shared preview scheduling, immediate release invalidation, fresh validation after target motion, pooled ghost membership, two-hand assembly, cancellation, save/load, container physics, and car regressions.
- Full results from the connected editor: `Logs/AttachmentSearch.EditMode.json` and `Logs/AttachmentSearch.PlayMode.json`.
- Run the `AttachmentSearchTests` and `PreviewPerformanceTests` fixtures in Unity Test Runner to reproduce the measurements. The frame comparison is Editor-only and skips on players where its reference assembly is absent.
- In Unity Profiler, inspect `Assembly.Query`, `Assembly.Placement`, `Assembly.Highlight`, and `Assembly.Ghost`. `AssemblyWorld` also exposes `QueryCount`, `LastSocketPairChecks`, and `LastPenetrationChecks`.

Quest hardware performance has not been measured in this revision.
