# Attribution and local modification notice

This directory contains an unofficial, self-use build derived from
[`chen3861229/dd-danmaku`](https://github.com/chen3861229/dd-danmaku), based on
the v1.47 script. The local changes are maintained only for the maintainer's
personal use. There is no plan to submit these changes to the upstream project
or request that they be merged through a pull request.

The original dd-danmaku authorship and MIT license remain intact. See
`LICENSE`. The upstream userscript header is retained in generated assets; the
local maintainer does not claim authorship of the upstream work or of the 38
pre-existing customization hunks in the frozen active baseline.

The bundled rendering engine is derived from
[`weizhenye/Danmaku`](https://github.com/weizhenye/Danmaku) 2.0.8:

- Copyright (c) 2014 Zhenye Wei
- Licensed under the MIT License
- Full license: `vendor/danmaku-2.0.8/LICENSE`

Local self-use changes in 1.48/1.48.1 include:

- an opt-in wall-clock rolling-speed mode;
- TV-remote Left/Right handling for shared settings sliders;
- transactional runtime replacement and concurrent-reload protection;
- deterministic build and regression tooling;
- publication hardening that embeds the applicable license notice in the
  standalone asset and verifies that no runtime API token value is embedded or
  printed.

This build is unofficial and is not endorsed by the upstream projects.
