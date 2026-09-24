# Frozen inputs

These files are immutable audit/build inputs, not installable assets. Install
only the generated file under `dist/`. The frozen scripts intentionally retain
their original bytes, including historical diagnostic behavior that is removed
from the generated public asset.

| Input | Bytes | SHA-256 | Purpose |
| --- | ---: | --- | --- |
| `baselines/ede.v1.47.downloads.js` | 266934 | `B6E8DF2ED50C3B2B924A8181950D3A058F40EC4C34B93EDEAF088EEC0CE843C4` | Untouched downloaded v1.47 reference |
| `baselines/ede.v1.47.active-synology.js` | 275918 | `725497AD43B9725121270B2CCC37338C90516301554D48B280084E0AE03FA857` | Active `danmuku` entry read before this change |
| `vendor/danmaku-2.0.8/danmaku.js` | 19340 | `DCD71F40D28C2742B869211106B1219D3F209E0052AE034E3EBBB28652398E4C` | Readable upstream UMD source |

The active v1.47 script contains 38 pre-existing change hunks relative to the
downloaded reference. Those changes are outside this work and are preserved by
building from the frozen active input.

The upstream package was fetched as `danmaku@2.0.8` from the npm registry. Its
package metadata declares `git+https://github.com/weizhenye/Danmaku.git` as the
repository, MIT as the license, npm shasum
`baa4851bb7b925b478ffdd355b29e32b50083910`, and integrity beginning with
`sha512-nawwVo51E/W53`.
