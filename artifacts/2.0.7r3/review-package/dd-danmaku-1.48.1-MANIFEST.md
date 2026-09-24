# dd-danmaku 1.48.1 review package

The functional changes were prepared on 2026-08-24 and live-verified as 1.48
on 2026-08-25. Version 1.48.1 adds publication hardening: the complete outer
license and self-use notice are embedded, and verification confirms that no
runtime API token value is embedded or printed.

This is an unofficial self-use build maintained only for personal use. There
is no plan to submit these changes upstream or request their merger through a
pull request.

## Candidate identity

| Field | Value |
| --- | --- |
| File | `dd-danmaku.CustomCssJS.js` |
| Size | 303089 bytes |
| SHA-256 | `76F05EFE46346B8612FE082DC7AE42E20E7CF7E20A61D35C84D695FAEC9E9D50` |
| Script version | 1.48.1 |
| Controlled engine | Danmaku 2.0.8 |
| Capability | `dd-danmaku-wall-clock-v1` |
| Capability option | `fixedSpeed` |

The outer dd-danmaku work and bundled `danmaku@2.0.8` engine are distributed
under their included MIT licenses. The candidate embeds the applicable notice
and source provenance. See `MODIFICATIONS.md`, `dd-danmaku-LICENSE.txt`, and
`Danmaku-2.0.8-LICENSE.txt`.

## Frozen inputs

| Input | Size | SHA-256 |
| --- | ---: | --- |
| Active Synology `danmuku` entry read before the change | 275918 bytes | `725497AD43B9725121270B2CCC37338C90516301554D48B280084E0AE03FA857` |
| Untouched downloaded v1.47 reference | 266934 bytes | `B6E8DF2ED50C3B2B924A8181950D3A058F40EC4C34B93EDEAF088EEC0CE843C4` |
| Readable upstream Danmaku 2.0.8 UMD source | 19340 bytes | `DCD71F40D28C2742B869211106B1219D3F209E0052AE034E3EBBB28652398E4C` |

The active input contains 38 pre-existing customization hunks relative to the
downloaded reference. Generation starts from the active input so those changes
remain present.

## Behavior and compatibility

- `倍速时保持弹幕滚动速度` is opt-in and defaults to false for missing or
  invalid saved values; old storage is not eagerly migrated.
- When enabled with the bundled capable engine, media time still schedules
  timestamps and seek destinations, while rolling `rtl`/`ltr` position,
  collision, track release, and removal use monotonic playing time.
- Pause/wait freezes rolling motion; play resumes it; rate changes do not reset
  visible comments. Top/bottom comment lifetime stays on the legacy media-time
  path.
- Disabling the option follows the frozen Danmaku 2.0.8 behavior. An explicitly
  configured incompatible custom engine retains requested=true but runs the
  effective legacy mode with an inactive diagnostic.
- Runtime switching constructs the replacement before destroying the previous
  instance and rolls back the saved choice on construction failure. Each
  successful commit replaces the runtime that is current at commit time, so
  overlapping reloads cannot leave duplicate connected wrappers or engines.
- Every shared horizontal range input handles Left/Right aliases and key codes
  one decimal-grid step at a time, consumes boundaries, emits one `input` then
  one `change`, leaves Up/Down to host navigation, and deduplicates a matching
  per-element Emby direction event.
- Runtime authentication remains dynamic, but the final asset does not print
  the API token value to the browser console.
- This package changes no DLL, backend provider, XML model, or separate Smart
  Match asset.

## Deterministic verification

Run from `Frontend/dd-danmaku`:

```powershell
npm run build
npm run verify
node --check dist/dd-danmaku.CustomCssJS.js
node --check src/danmaku.fixed-speed.js
```

The final run regenerated the exact candidate above and passed 39 of 39 Node
tests, including disabled legacy parity, all required playback rates and live
rate transitions, pause/wait/seek/hide, rolling track cleanup, real and hidden
media adapters, settings persistence/import, transactional failures,
concurrent successful reload replacement, and all registry-backed slider
shapes.

Repository gates also passed:

```powershell
node Frontend/DanmuSmartMatch.RegressionTests.js
dotnet run --project RegressionTests/Emby.Plugin.Danmu.RegressionTests.csproj -c Release --no-build --no-restore
dotnet clean Emby.Plugin.Danmu.sln -c Release --nologo
dotnet build Emby.Plugin.Danmu.sln -c Release --no-restore --nologo -v:minimal
openspec.cmd validate fix-dd-danmaku-playback-rate-and-tv-sliders --strict
git diff --check
```

Both adjacent regression suites passed. The clean Release build completed with
0 errors and 131 pre-existing warnings. Strict OpenSpec validation, generated
file drift, authored whitespace, private-host/credential-assignment, and scope
checks passed. The unrelated active OpenSpec change remains untouched.

## Superseded live attempt and verified rollback

The previously confirmed candidate with SHA-256
`BBE723C486B37A7948834F43732576954613B3628F1935B338CB6933C23B6CD9`
was deployed only after its exact identity was approved. Real-video testing
loaded 2608 comments and verified remote Left/Right operation for all five
visible main sliders, including focus retention and boundary behavior.

Rapid reload-producing slider changes followed by the new option toggle then
exposed two connected `danmakuWrapper` elements. The candidate was immediately
removed by restoring the complete v1.47 configuration backup. Post-rollback
checks confirmed the original full-configuration and active-entry hashes, the
unchanged neighboring Smart Match entry, original file metadata, and healthy
Emby HTTP service.

The defect was an overlapping-creation commit race: each creation captured the
old runtime too early, so a later commit did not replace a candidate committed
by an earlier overlapping call. The current candidate resolves the runtime at
commit time and adds a regression test that runs two successful creations
concurrently, then proves that only the final engine and one connected wrapper
remain.

## Current live deployment and acceptance

The user authorized autonomous candidate iteration and testing for this scoped
change. The current candidate was backed up, staged, parsed, hash-checked,
atomically deployed, and read back after an Emby-only restart.

| Live field | Verified value |
| --- | --- |
| Installed CustomCssJS XML SHA-256 | `EA186091C5B9A17ABCA99FDE3C541AD2A700DD630FABEBA732A42BD8FC24FD96` |
| Installed dd-danmaku entry | 301818 bytes, `64175BD3F553B0B80AB6DBA833C5497DDDC552F886765690BF55EBC107570828` |
| Unchanged Smart Match entry | 247121 bytes, `0DE51FD71EDE3582175B84281F2DDE4D6654EDFB74941CE1BC1A1034EDE4FE33` |
| Configuration metadata | mode `444`, owner `emby`, group `users` |
| Emby health | running, HTTP 200, version 4.9.5.0 |

Real-video acceptance loaded 2608 comments with the controlled capability and
one connected wrapper. All eight Emby playback menu rates from 0.25x through
2x retained approximately 1.0 normalized wall-clock rolling speed while media
time advanced at the selected rate. The three required live transitions had
zero immediate position jump. Pause and waiting froze motion, play resumed it,
forward/backward and paused seeks stayed aligned to media time, and a live top
comment retained its legacy media-time lifetime.

Turning the option off transactionally preserved media state and restored
legacy rate-scaled motion; turning it back on restored effective fixed mode and
persisted after reopening settings. Five main sliders passed real key focus,
step, decimal, and boundary checks while the media was paused. A 50-event
reload-heavy stress sequence returned every value to its starting point with
one wrapper and no pending wrapper. The per-element `emby-direction` route
also moved one step, deduplicated the matching DOM event, restored the value,
and retained focus.

No physical Android TV device was connected for this run. TV input coverage is
therefore the live Emby `keydown` and `emby-direction` paths plus the 39-test
deterministic suite covering every registry-backed slider shape, rather than a
claim about device-specific remote firmware or focus routing.

## Deployment and rollback

The deployment followed the package gate and copied the entire active
CustomCssJS configuration to a timestamped rollback directory before
replacement. The backup, extracted v1.47 entry, and reviewed candidate were
read back by hash. Only the `danmuku` entry changed; the neighboring entry and
configuration ownership/mode were preserved.

If XML parsing, candidate hash/capability, Emby health, browser playback, or TV
input checks fail, restore the complete configuration backup, restart Emby, and
verify the pre-deployment configuration and active-entry hashes. Leaving the
new option disabled is a functional fallback for rolling timing, but the saved
configuration backup remains the authoritative asset rollback.

The directly usable rollback asset remains in a private Synology rollback
directory; its host-specific path is intentionally omitted from the public
record.
