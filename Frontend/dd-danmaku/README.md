# dd-danmaku CustomCssJS asset

This directory builds the standalone dd-danmaku script installed in the Emby
CustomCssJS `danmuku` entry. It is intentionally separate from
`Frontend/DanmuSmartMatch.CustomCssJS.js`.

This is an unofficial self-use build. The local changes are maintained only
for personal use, with no plan to submit or merge them upstream through a pull
request. See `THIRD_PARTY_NOTICES.md` for provenance and attribution.

The build starts from the frozen script read from the active Synology
configuration, so its existing matching customizations remain intact. The
original downloaded v1.47 file is retained as an independent reference and
rollback baseline.

Commands:

```powershell
npm run build
npm test
npm run verify
```

`npm run build` writes `dist/dd-danmaku.CustomCssJS.js`. `npm run verify`
also proves that a second build is byte-for-byte identical.

The outer dd-danmaku work remains under its MIT license in `LICENSE`. The
bundled Danmaku engine is based on upstream Danmaku 2.0.8 and remains under its
MIT license in `vendor/danmaku-2.0.8/LICENSE`. The standalone generated asset
embeds the applicable notice so it can be distributed directly.
