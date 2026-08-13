# 2.0.3r5 isolated fixture index attempt — 2026-08-13

## Safety boundary

Credentials were passed only to the invoking processes and are not retained.
The authenticated Emby query returned 25 production libraries. Production
media roots were below canonical `/volume1/NAS`; Emby internal playlist and
collection roots were below `/var/packages/EmbyServer/var/data`.

These initially absent paths were reserved for the fixture:

- `/volume1/__DanmuPlusFixture_2.0.3r5__`
- `/volume1/__DanmuPlusFixture_2.0.3r5_stage__`

Canonical `/volume1` resolved to itself and was not a symlink. Both candidates
were proved neither ancestors nor descendants of the canonical production
roots. The exact literal names were enforced before creation.

## Bounded index probe

Only the disposable root was populated: one TV NFO, one Season NFO, one S01E01
NFO, and one 1,425-byte MKV generated from a single raw black frame. No
production file or sidecar was copied, changed, or created. The MKV SHA-256 was
`3326d9160e3347d5d04882f45d6d7c17546ad104d42433efdb73742ca5b77f1f`.

Emby created an isolated TV virtual folder with ItemId `521378`, exact fixture
location, and real-time monitoring disabled. One recursive item refresh was
submitted and returned HTTP 204. Bounded queries at 0, 4, and 12 seconds all
returned zero Series, zero Seasons, and zero Episodes; waiting then stopped.

Because Emby never allocated disposable Series/Season/Episode ItemIds, task
10.4 could not safely begin. No StartTrackedDownload, download, bind, retry,
cancellation, ProviderId, metadata, or plugin media-write endpoint was called.
Task 10.4 remains unchecked.

## Ordered cleanup

1. Virtual-library deletion returned HTTP 204.
2. The virtual-folder query contained zero matching id/name/location records.
3. User ItemId `521378` returned HTTP 404.
4. Immediately before filesystem deletion, both paths resolved exactly to the
   literal paths, were directories, were not symlinks, and again passed
   bidirectional production-ancestor exclusion.
5. Only the two exact disposable paths were recursively deleted. The listing
   contained the four probe files and one staging checksum file.
6. Both exact paths and both symlink names were absent afterward. The two exact
   `/tmp` helper files were also verified as regular non-symlink files and
   removed.

No fixture SeasonId existed, so no plugin-state file was eligible for deletion.
Private plugin state remained byte-identical to the predeployment backup.
Although the fail-closed cleanup branch completed, task 10.5 remains unchecked
because its required successfully indexed fixture lifecycle could not begin.

## Final live state

- Emby remained PID `13245`, HTTP 200, version `4.9.5.0`.
- r5 DLL remained
  `123ee755f22ae20a1a2492f4d616c4b6f8cd232bfc629fac25f0a4c466b8d552`.
- Danmu XML remained
  `a3be897f9fb84fa19cba5b226cac0b5e2f942a5b2117a5379cca851ca407c973`.
- CustomCssJS XML remained
  `b6fad60cb15139ca605bc78521e42497a9e99f935564644f4fab620cc26d54b5`.
- V21=1 and V20=0; Danmu loading and ServiceRegistrator completion remained
  present without a Danmu load error.

This is a clean, fail-closed indexing blocker, not task 10.4/10.5 acceptance.
