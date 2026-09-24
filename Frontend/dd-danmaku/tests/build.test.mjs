import assert from 'node:assert/strict';
import test from 'node:test';
import { createHash } from 'node:crypto';
import { readFile } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');

function sha256(value) {
  return createHash('sha256').update(value).digest('hex').toUpperCase();
}

test('frozen baseline and upstream source hashes remain exact', async () => {
  const cases = [
    ['baselines/ede.v1.47.downloads.js', 'B6E8DF2ED50C3B2B924A8181950D3A058F40EC4C34B93EDEAF088EEC0CE843C4'],
    ['baselines/ede.v1.47.active-synology.js', '725497AD43B9725121270B2CCC37338C90516301554D48B280084E0AE03FA857'],
    ['vendor/danmaku-2.0.8/danmaku.js', 'DCD71F40D28C2742B869211106B1219D3F209E0052AE034E3EBBB28652398E4C']
  ];
  for (const [relativePath, expected] of cases) {
    const bytes = await readFile(path.join(root, relativePath));
    assert.equal(sha256(bytes), expected, relativePath);
  }
});

test('standalone candidate bundles one controlled engine and keeps custom-engine fallback', async () => {
  const candidate = await readFile(path.join(root, 'dist', 'dd-danmaku.CustomCssJS.js'), 'utf8');
  const engine = (await readFile(path.join(root, 'src', 'danmaku.fixed-speed.js'), 'utf8')).trimEnd();
  assert.ok(candidate.includes(engine));
  assert.equal((candidate.match(/dd-danmaku-wall-clock-v1/g) || []).length >= 2, true);
  assert.equal(candidate.includes('skipInnerModule'), false);
  assert.match(candidate, /const controlledDanmakuConstructor = window\.Danmaku;/);
  assert.match(candidate, /requestedPath !== bundledDefaultDanmakuPath/);
  assert.match(candidate, /Emby\.importModule\(requestedPath\)/);
  assert.match(candidate, /engine-capability-missing/);
});

test('fixed-speed setting is opt-in, exported by the registry, and rolls back transactionally', async () => {
  const candidate = await readFile(path.join(root, 'dist', 'dd-danmaku.CustomCssJS.js'), 'utf8');
  assert.match(candidate, /fixedSpeed: \{ id: 'danmakuFixedSpeed', defaultValue: false/);
  assert.match(candidate, /Object\.fromEntries\(objectEntries\(lsKeys\)\.map/);
  assert.match(candidate, /if \(typeof defaultValue === 'boolean'\) \{ return item === 'true'; \}/);
  assert.match(candidate, /lsSetItem\(lsKeys\.fixedSpeed\.id, previousValue\)/);
  assert.match(candidate, /切换失败，已恢复原设置和实例/);

  const constructorIndex = candidate.indexOf('nextDanmaku = new DanmakuConstructor');
  const oldDestroyIndex = candidate.indexOf('committedDanmaku.destroy()', constructorIndex);
  assert.ok(constructorIndex >= 0 && oldDestroyIndex > constructorIndex,
    'the active instance at commit time is destroyed only after the replacement is constructed');
});

test('real and hidden media paths pass one effective mode while retaining timeline/rate sync', async () => {
  const candidate = await readFile(path.join(root, 'dist', 'dd-danmaku.CustomCssJS.js'), 'utf8');
  assert.match(candidate, /media: _media,[\s\S]*fixedSpeed: effectiveFixedSpeed/);
  assert.match(candidate, /const realCurrentTime = playbackManager\.currentTime/);
  assert.match(candidate, /_media\.currentTime = realCurrentTime/);
  assert.match(candidate, /_media\.playbackRate = embyPlaybackRate \? embyPlaybackRate : 1/);
  assert.match(candidate, /_media\.dispatchEvent\(new Event\('seeking'\)\)/);
  assert.match(candidate, /time: values\[0\] \* 1 \+ timelineOffset/);
});

test('existing active matching customizations survive the standalone build', async () => {
  const candidate = await readFile(path.join(root, 'dist', 'dd-danmaku.CustomCssJS.js'), 'utf8');
  const retainedMarkers = [
    'async function fetchSearchEpisodesByTmdbId',
    'function prioritizeSeasonCandidates',
    'function isSeasonCompatible',
    'function createSeasonInfo',
    "if (!fn || typeof fn.toString !== 'function') { return ''; }"
  ];
  for (const marker of retainedMarkers) assert.ok(candidate.includes(marker), marker);
});

test('candidate is v1.48.1, contains notices, and has no credential disclosure', async () => {
  const candidate = await readFile(path.join(root, 'dist', 'dd-danmaku.CustomCssJS.js'), 'utf8');
  assert.match(candidate, /^\/\/ @version\s+1\.48\.1$/m);
  assert.match(candidate, /Copyright \(c\) 2022 Lee/);
  assert.match(candidate, /Copyright \(c\) 2014 Zhenye Wei/);
  assert.match(candidate, /not intended for an upstream pull request or merge/);
  assert.match(candidate, /Source and provenance: vendor\/danmaku-2\.0\.8/);
  assert.doesNotMatch(candidate, /ApiKey:\s*\$\{apiKey\},/);
  assert.match(candidate, /\$\{apiKey \? '已获取' : '未获取'\}/);
  assert.doesNotMatch(candidate, /-----BEGIN (?:OPENSSH |RSA |EC )?PRIVATE KEY-----/);
  assert.doesNotMatch(candidate, /\b(?:sshpass|Authorization:\s*Basic)\b/i);
});
