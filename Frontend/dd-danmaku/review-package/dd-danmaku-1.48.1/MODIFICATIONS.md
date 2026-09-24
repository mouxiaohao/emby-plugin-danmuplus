# Unofficial self-use modifications

This is an unofficial modified build based on dd-danmaku v1.47. The local
changes are maintained only for personal use. There is no plan to submit them
to the upstream project or request that they be merged through a pull request.

The original authorship and licenses remain intact. This package does not
claim authorship of the upstream project or of pre-existing local
customizations in the frozen active baseline.

Changes in this self-use build include:

- opt-in wall-clock rolling speed while retaining media-time scheduling;
- TV-remote Left/Right handling for shared settings sliders;
- transactional runtime replacement and concurrent-reload protection;
- deterministic source generation and regression tests;
- publication hardening that embeds the applicable license notice and verifies
  that no runtime API token value is embedded or printed.

See `dd-danmaku-LICENSE.txt` and `Danmaku-2.0.8-LICENSE.txt`.
