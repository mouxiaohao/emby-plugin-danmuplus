import { createHash } from 'node:crypto';
import { readFile, writeFile } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

const scriptDir = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(scriptDir, '..');
const checkOnly = process.argv.includes('--check');

const files = {
  active: path.join(root, 'baselines', 'ede.v1.47.active-synology.js'),
  downloaded: path.join(root, 'baselines', 'ede.v1.47.downloads.js'),
  vendor: path.join(root, 'vendor', 'danmaku-2.0.8', 'danmaku.js'),
  engine: path.join(root, 'src', 'danmaku.fixed-speed.js'),
  requireBlock: path.join(root, 'src', 'require-block.js.inc'),
  createDanmaku: path.join(root, 'src', 'create-danmaku.js.inc'),
  sliderHelpers: path.join(root, 'src', 'tv-slider-helpers.js.inc'),
  fixedSpeedControl: path.join(root, 'src', 'fixed-speed-control.js.inc'),
  license: path.join(root, 'LICENSE'),
  output: path.join(root, 'dist', 'dd-danmaku.CustomCssJS.js')
};

const expectedHashes = {
  active: '725497AD43B9725121270B2CCC37338C90516301554D48B280084E0AE03FA857',
  downloaded: 'B6E8DF2ED50C3B2B924A8181950D3A058F40EC4C34B93EDEAF088EEC0CE843C4',
  vendor: 'DCD71F40D28C2742B869211106B1219D3F209E0052AE034E3EBBB28652398E4C'
};

function sha256(value) {
  return createHash('sha256').update(value).digest('hex').toUpperCase();
}

function normalizeLf(value) {
  return value.replace(/\r\n?/g, '\n');
}

function assertHash(label, value, expected) {
  const actual = sha256(value);
  if (actual !== expected) {
    throw new Error(`${label} hash mismatch: expected ${expected}, got ${actual}`);
  }
}

function replaceOnce(source, search, replacement, label) {
  const first = source.indexOf(search);
  if (first < 0) {
    throw new Error(`Missing build anchor: ${label}`);
  }
  if (source.indexOf(search, first + search.length) >= 0) {
    throw new Error(`Ambiguous build anchor: ${label}`);
  }
  return source.slice(0, first) + replacement + source.slice(first + search.length);
}

function replaceRange(source, start, end, replacement, label) {
  const startIndex = source.indexOf(start);
  if (startIndex < 0) {
    throw new Error(`Missing range start: ${label}`);
  }
  const endIndex = source.indexOf(end, startIndex + start.length);
  if (endIndex < 0) {
    throw new Error(`Missing range end: ${label}`);
  }
  if (source.indexOf(start, startIndex + start.length) >= 0) {
    throw new Error(`Ambiguous range start: ${label}`);
  }
  return source.slice(0, startIndex) + replacement + source.slice(endIndex);
}

const [activeBytes, downloadedBytes, vendorBytes, engineTextRaw, requireBlockRaw,
  createDanmakuRaw, sliderHelpersRaw, fixedSpeedControlRaw, ddLicenseRaw] = await Promise.all([
  readFile(files.active),
  readFile(files.downloaded),
  readFile(files.vendor),
  readFile(files.engine, 'utf8'),
  readFile(files.requireBlock, 'utf8'),
  readFile(files.createDanmaku, 'utf8'),
  readFile(files.sliderHelpers, 'utf8'),
  readFile(files.fixedSpeedControl, 'utf8'),
  readFile(files.license, 'utf8')
]);

assertHash('active Synology v1.47 baseline', activeBytes, expectedHashes.active);
assertHash('downloaded v1.47 baseline', downloadedBytes, expectedHashes.downloaded);
assertHash('upstream Danmaku 2.0.8 source', vendorBytes, expectedHashes.vendor);

let source = normalizeLf(activeBytes.toString('utf8'));
const engineText = normalizeLf(engineTextRaw).trimEnd();
const requireBlock = normalizeLf(requireBlockRaw)
  .replace('/*__CONTROLLED_DANMAKU_ENGINE__*/', engineText)
  .trimEnd();
const createDanmaku = normalizeLf(createDanmakuRaw).trimEnd();
const sliderHelpers = normalizeLf(sliderHelpersRaw).trimEnd();
const fixedSpeedControl = normalizeLf(fixedSpeedControlRaw).trimEnd();
const distributionNotice = [
  '/*',
  ' * Unofficial self-use build maintained for personal use.',
  ' * These local changes are not intended for an upstream pull request or merge.',
  ' *',
  ...normalizeLf(ddLicenseRaw).trim().split('\n').map(line => line ? ` * ${line}` : ' *'),
  ' */'
].join('\n');

if (!engineText.includes("id: 'dd-danmaku-wall-clock-v1'") ||
    !engineText.includes("engineVersion: '2.0.8'")) {
  throw new Error('Controlled engine capability marker is missing');
}

source = replaceOnce(source, '// @version      1.47', '// @version      1.48.1',
  'userscript version');
source = replaceOnce(source, "self: { version: '1.47'", "self: { version: '1.48.1'",
  'internal version');
source = replaceOnce(source, '// ==/UserScript==',
  '// ==/UserScript==\n\n' + distributionNotice,
  'self-use and dd-danmaku license notice');

source = replaceOnce(
  source,
  "        speed: { id: 'danmakuBaseSpeed', defaultValue: 1, name: '速度', min: 0.1, max: 3, step: 0.1 },\n        timelineOffset:",
  "        speed: { id: 'danmakuBaseSpeed', defaultValue: 1, name: '速度', min: 0.1, max: 3, step: 0.1 },\n        fixedSpeed: { id: 'danmakuFixedSpeed', defaultValue: false, name: '倍速时保持弹幕滚动速度' },\n        timelineOffset:",
  'fixed-speed setting registry');

source = replaceOnce(
  source,
  "        danmakuSpeedDiv: 'danmakuSpeedDiv',\n        danmakuFontWeightDiv:",
  "        danmakuSpeedDiv: 'danmakuSpeedDiv',\n        danmakuFixedSpeedDiv: 'danmakuFixedSpeedDiv',\n        danmakuFixedSpeedStatus: 'danmakuFixedSpeedStatus',\n        danmakuFontWeightDiv:",
  'fixed-speed element ids');

source = replaceRange(
  source,
  '    // ------ require start ------',
  '    // ------ require end ------',
  requireBlock + '\n',
  'controlled engine block');

source = replaceRange(
  source,
  '    async function createDanmaku(comments) {',
  '    function buildProgressBarChart(chartHeightNum) {',
  createDanmaku + '\n\n',
  'transactional createDanmaku');

const timelineRow = [
  '                    <div style="${styles.embySlider}">',
  '                        <label class="${classes.embyLabel}" style="width: 5em;">${lsKeys.timelineOffset.name}: </label>'
].join('\n');
const fixedSpeedRow = [
  '                    <div id="${eleIds.danmakuFixedSpeedDiv}" style="margin: 0.5em 0 0.2em 5em;"></div>',
  '                    <div id="${eleIds.danmakuFixedSpeedStatus}" class="${classes.embyFieldDesc}"',
  '                        style="display:none;margin:0 0 0.5em 5em;color:#ffcc66;"></div>'
].join('\n');
source = replaceOnce(source, timelineRow, fixedSpeedRow + '\n' + timelineRow,
  'fixed-speed settings row');

const speedControl = [
  '        getById(eleIds.danmakuSpeedDiv, container).append(',
  '            embySlider({ lsKey: lsKeys.speed }, onSliderChange, onSliderChangeLabel)',
  '        );'
].join('\n');
source = replaceOnce(source, speedControl, speedControl + '\n' + fixedSpeedControl,
  'fixed-speed checkbox wiring');

source = replaceOnce(
  source,
  '            return objectEntries(keyValues).reduce((acc, [id, value]) => (acc || lsCheckSet(id, value)), false);',
  '            return objectEntries(keyValues).reduce((acc, [id, value]) => lsCheckSet(id, value) || acc, false);',
  'settings batch import applies every key');

source = replaceOnce(
  source,
  '    function embySlider(opts = {}, onChange, onSliding) {',
  sliderHelpers + '\n\n    function embySlider(opts = {}, onChange, onSliding) {',
  'TV slider helpers');

const remoteHandlerStart = [
  '        {',
  '            // 控制器操作锁定滑块焦点,防止方向键触发空间导航跳转焦点'
].join('\n');
source = replaceRange(
  source,
  remoteHandlerStart,
  '        return slider;',
  '        installDdSliderDirectionHandler(slider);\n',
  'shared slider remote handler');

source = source.endsWith('\n') ? source : source + '\n';

const outputHash = sha256(Buffer.from(source, 'utf8'));
if (checkOnly) {
  const existing = await readFile(files.output);
  if (!existing.equals(Buffer.from(source, 'utf8'))) {
    throw new Error(`Generated script drift: expected SHA-256 ${outputHash}`);
  }
} else {
  await writeFile(files.output, source, { encoding: 'utf8' });
}

console.log(JSON.stringify({
  mode: checkOnly ? 'check' : 'write',
  output: path.relative(root, files.output).replaceAll('\\', '/'),
  bytes: Buffer.byteLength(source, 'utf8'),
  sha256: outputHash,
  scriptVersion: '1.48.1',
  engineCapability: 'dd-danmaku-wall-clock-v1',
  engineVersion: '2.0.8',
  baselineSha256: expectedHashes.active
}, null, 2));
