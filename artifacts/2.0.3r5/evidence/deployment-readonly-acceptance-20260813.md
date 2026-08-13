# 2.0.3r5 deployment and read-only acceptance — 2026-08-13

## Approved live baseline

The user explicitly approved Emby `4.9.5.0` as the deployment and acceptance
baseline after the initial fail-closed preflight. Credentials were held only in
the invoking processes and are not retained in this repository or evidence.

## Predeployment backup and rollback dry run

The active r4 state matched the frozen release pair before backup:

- DLL: `e933fe9734cc000ebaa177058398d8301c70a06394fd5195a1f884071b5f97be`
- Danmu XML: `a3be897f9fb84fa19cba5b226cac0b5e2f942a5b2117a5379cca851ca407c973`
- CustomCssJS XML: `f8f6dd7876dec44d41f7c7b0764ad6be5cf84e0d263f16c76c46701ac5e1cf09`
- markers: V20=1, V21=0

New absolute backup:

`/var/packages/EmbyServer/var/plugins/backups/danmu-2.0.3r5-predeploy-20260813-161314`

The backup contains the active DLL, Danmu configuration, CustomCssJS
configuration, raw `library.db`/WAL/SHM, an online consistent SQLite backup,
the complete Emby library configuration, private plugin state, absolute-path
inventory, and SHA-256 manifests. It contains 64 files and occupied
340,259,361 bytes at creation. `SHA256SUMS` SHA-256:
`e47034e8bb7b1fe163923f3d1a2b8811107fa5ce7511ac308a56619cac19cb10`.

An isolated r4-trio destructive-copy/restore dry run reproduced all three
expected hashes. The backup was made recursively non-writable after manifest
verification.

## Atomic deployment and restart

The staged r5 DLL, JavaScript, updater, and LF restart helper matched the hashes
in `VERIFICATION.md`. The first replacement attempt wrote the paired DLL/V21
configuration but could not execute the helper directly because `/tmp` is
mounted no-exec. Emby had not restarted. The r4 trio was immediately restored
atomically from the verified backup; PID `20365`, all r4 hashes, V20=1/V21=0,
and HTTP 200 were confirmed.

The one authorized retry reused the same verified backup and staging files and
invoked the LF helper explicitly through POSIX `sh`. Mandatory deployment gates
then passed:

- Emby PID changed from `20365` to `13245`.
- Public system information returned HTTP 200 and version `4.9.5.0`.
- active r5 DLL: `123ee755f22ae20a1a2492f4d616c4b6f8cd232bfc629fac25f0a4c466b8d552`
- unchanged Danmu XML: `a3be897f9fb84fa19cba5b226cac0b5e2f942a5b2117a5379cca851ca407c973`
- deployed CustomCssJS XML: `b6fad60cb15139ca605bc78521e42497a9e99f935564644f4fab620cc26d54b5`
- markers: V20=0, V21=1
- latest startup log loaded `Emby.Plugin.Danmu, Version=2.0.3.0` and completed
  both the Danmu and CustomCssJS entry points without a Danmu load error.

## Authenticated read-only acceptance

Only library GETs and `MatchPreview` GETs were used. No Start, download, bind,
refresh, metadata mutation, or fixture-write endpoint was called.

### One Punch Man

- Explicit S1: protocol V21; displayed 19, eligible 12, ignored Parent 0 = 7;
  12 unique mappings to source Episodes 1–12; zero unmatched runs and zero
  temporary groups.
- Whole Series: only Seasons 1, 2, and 3 were returned; no Season 0 or
  unknown-number target was present. Its S1 result was semantically identical
  to explicit S1.

### Seitokai Yakuindomo

- Explicit S1: protocol V21; displayed 21, eligible 13, ignored Parent 0 = 8;
  Dandan candidate `7532` mapped 13 unique Episodes to source Episodes 1–13;
  zero unmatched runs and zero temporary groups.
- Whole Series: only Seasons 1 and 2 were returned; no Season 0 or
  unknown-number target was present. Its S1 result was semantically identical
  to explicit S1. The top-level response was `incomplete` only because the
  unrelated S2 provider search was incomplete; no write followed.
- Explicit real S0 (`519629`): SeasonNumber=0, displayed=eligible=18, all
  ignored counts zero. The independent Emby inventory contained exactly 18
  unique ItemIds, all with ParentIndexNumber 0 and SeasonId `519629`.
  Upstream search did not offer one source covering all 18 specials, so no
  complete single-source S0 mapping is claimed and no candidate was bound.

## Final invariants and retained rollback

- One Punch S1 inventory remained exactly 19 preobserved ItemIds: 12 Parent 1
  plus seven Parent 0.
- Seitokai S1 remained exactly 21 preobserved ItemIds: 13 Parent 1 plus eight
  Parent 0.
- Seitokai S0 remained exactly 18 preobserved ItemIds, all Parent 0.
- Private plugin-state files remained byte-identical to the predeployment
  backup; the full backup manifest revalidated.
- Final HTTP/version, active hashes, V21 uniqueness, startup loading, and
  membership checks passed.

Rollback remains: stop Emby, atomically restore the three r4 files named in
`R4_TRIO.sha256` from the absolute backup, restart, then require the recorded r4
hashes, V20=1/V21=0, HTTP 200/4.9.5.0, and clean entry-point loading.

## Browser UI acceptance

An authenticated Chrome session opened the production One Punch Man Series and
S1 pages and used the injected V21 menu entries without starting a download.

- Whole-Series UI rendered only S1-S3. S1 displayed 19, matched 12 and reported
  seven S00 Episodes as read-only ignored records. The mapped card retained the
  server score and source label; the ignored records had no controls or wire
  selection.
- The explicit S1 UI rendered the same displayed/eligible/ignored counts, the
  same twelve-Episode mapping and zero temporary remainder, proving entry-point
  presentation parity.
- Both dialogs exposed rematch/remove and the final download control, but the
  dialogs were closed without invoking download, bind or metadata mutation.
- No V20 draft was restored or submitted after the V21 reload.
- No Smart Match JavaScript error was emitted. The console continued to show the
  previously documented independent `danmuku` entry error at its own
  `onViewShow` line 5347, plus an unrelated browser translation-extension error;
  neither stack contained the V21 Smart Match script and both predate r5.

Disposable write-fixture acceptance remains outside this run. Tasks 10.4-10.5
remain unchecked; this evidence does not claim full write-path acceptance.
