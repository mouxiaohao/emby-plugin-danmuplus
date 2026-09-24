# dd-danmaku 1.48 live verification record

## Superseded candidate and rollback

The first confirmed candidate had SHA-256
`BBE723C486B37A7948834F43732576954613B3628F1935B338CB6933C23B6CD9`.
Real-video testing loaded 2608 comments and verified remote-style Left/Right
operation for the five main sliders. A reload-heavy sequence followed by the
new option toggle then left two connected `danmakuWrapper` elements. This
failed the single-runtime acceptance criterion.

The complete pre-deployment configuration was restored immediately and Emby
was restarted. Readback verified the original full-configuration hash, active
v1.47 entry hash, unchanged Smart Match entry, original mode and ownership, and
HTTP 200 health. The verified rollback asset for that attempt remains in a
private Synology rollback directory; its host-specific path is intentionally
omitted from the public record.

The cause was an overlapping creation race. A later call captured the same old
runtime as an earlier call and did not destroy the candidate committed by that
earlier call. The fix resolves and replaces the current runtime at commit time.
Its regression test overlaps two successful creations and proves that the
first new engine is destroyed, the second remains current, and exactly one
connected wrapper remains.

## Deployed candidate

- Size: 301818 bytes
- SHA-256:
  `64175BD3F553B0B80AB6DBA833C5497DDDC552F886765690BF55EBC107570828`
- Controlled engine: Danmaku 2.0.8
- Capability: `dd-danmaku-wall-clock-v1`
- Deployment state: deployed and live-verified on 2026-08-25

The user authorized autonomous deploy-test-fix iteration for this scoped
change, so this corrected binary was deployed without another per-candidate
approval pause.

## Backup, atomic replacement, and server readback

Before replacement, the exact active configuration was verified as the
restored v1.47 baseline. A new complete backup, extracted v1.47 entry, and the
reviewed candidate were written and independently hash-checked. The staged XML
parsed with exactly two entries and was atomically installed during an
Emby-only stop/start.

| Readback | Value |
| --- | --- |
| Installed CustomCssJS XML | 557476 bytes, `EA186091C5B9A17ABCA99FDE3C541AD2A700DD630FABEBA732A42BD8FC24FD96` |
| Installed dd-danmaku entry | 301818 bytes, `64175BD3F553B0B80AB6DBA833C5497DDDC552F886765690BF55EBC107570828` |
| Unchanged Smart Match entry | 247121 bytes, `0DE51FD71EDE3582175B84281F2DDE4D6654EDFB74941CE1BC1A1034EDE4FE33` |
| Configuration metadata | mode `444`, owner `emby`, group `users` |
| Emby service | running, HTTP 200, version 4.9.5.0 |

The directly usable rollback asset remains in a private Synology rollback
directory; its host-specific path is intentionally omitted from the public
record.

Its complete XML retains SHA-256
`FA640EC74F2986528074CBEDD6354140811C0443CB7173C6D7175B24882E3899`,
and its extracted v1.47 entry retains SHA-256
`725497AD43B9725121270B2CCC37338C90516301554D48B280084E0AE03FA857`.

## Real-video fixed-speed results

The actual Emby player loaded 2608 comments, one connected
`danmakuWrapper`, no pending wrapper, and the capable Danmaku 2.0.8 engine.
The requested and effective fixed-speed values were both true.

All eight rates exposed by the Emby playback menu were selected through the
real UI. Media time advanced at the selected rate while the measured rolling
position slope remained approximately one wall-clock speed:

| Playback rate | Normalized rolling slope |
| ---: | ---: |
| 0.25x | 0.958 |
| 0.5x | 1.020 |
| 0.75x | 0.997 |
| 1x | 0.999 |
| 1.25x | 0.993 |
| 1.5x | 0.996 |
| 1.75x | 0.999 |
| 2x | 0.999 |

The 0.5x to 2x, 2x to 0.5x, and 1x to 1.5x live transitions each had zero
immediate position jump and normalized post-transition slopes between 0.987
and 1.021.

- Pause held media time and rolling position exactly; play resumed motion.
- A synthetic real-media `waiting` event froze rolling position while media
  time continued, and `playing` resumed it without a jump.
- Forward playing seek and backward paused seek reset running state and aligned
  selection to the destination media time. Restoration retained one wrapper.
- A real top comment at 2x remained for 8.793 media seconds versus the 8.889
  second configured lifetime, corresponding to 4.395 wall seconds. This
  confirms fixed comments retain the legacy media-time lifetime path.
- Disabling the option preserved time, rate, paused state, comments, and one
  wrapper. Its normalized slopes returned to 0.499 at 0.5x and 2.025 at 2x.
  Re-enabling restored effective fixed mode and persisted after reopening.

## TV-direction and slider results

With media paused at a fixed timestamp, the five main settings sliders were
operated through their real DOM inputs:

| Setting | Observed sequence |
| --- | --- |
| Filter strength | boundary Left `0`, Right `1`, Left `0` |
| Display area | boundary Right `100`, Left `99`, Right `100` |
| Font size | Right `1.1`, Left `1` |
| Opacity | boundary Right `1`, Left `0.9`, Right `1` |
| Base speed | Right `1.1`, Left `1` |

Every action retained focus. Media time stayed exactly unchanged, and all
values persisted at their original values after reopening the dialog.

A 50-event rapid sequence covered repeats and every reload-heavy main slider.
After settling, all values were restored, 2608 comments remained available,
the requested/effective fixed-speed mode stayed true, and the document had
exactly one wrapper with no pending wrapper.

The live per-element `emby-direction` path changed font size from `1` to
`1.1`; the matching DOM event identity was deduplicated and did not move it a
second time; a new Left direction restored `1`. Exactly two `input` and two
`change` events were emitted. Up/Down were not consumed by the slider handler
and remained available to the Emby host navigation path.

No physical Android TV device was connected. These results cover the actual
Emby Web DOM `keydown` and `emby-direction` interfaces used by the implementation.
The deterministic 39-test suite separately covers every registry-backed
slider shape, aliases/codes, decimal grids, boundaries, repeats, focus,
event ordering, and route deduplication.

## Final state and non-blocking baseline observation

The browser was left at 1x, playing, with the settings dialog closed, 2608
comments, effective fixed-speed mode, one connected wrapper, and no pending
wrapper. The server was read back again after browser acceptance and remained
healthy with the deployed hashes above.

One home-page console error in the unchanged v1.47 `onViewShow` path was also
observed: it writes `itemId` before `window.ede` exists on that view. The same
error was present before this candidate, did not block player initialization,
and is outside the playback-rate and slider change scope.
