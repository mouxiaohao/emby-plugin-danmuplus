import assert from 'node:assert/strict';
import test from 'node:test';
import vm from 'node:vm';
import { readFile } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');

function extractRequireBlock(candidate) {
  const start = candidate.indexOf('    // ------ require start ------');
  const end = candidate.indexOf('    // ------ require end ------', start);
  assert.ok(start >= 0 && end > start);
  return candidate.slice(start, end);
}

function createLoaderContext(customConstructor) {
  let importCount = 0;
  let amdDefineCount = 0;
  const document = {
    createElement() {
      return { style: { transform: '', cssText: '' } };
    },
    createDocumentFragment() {
      return { appendChild() {} };
    },
    getElementsByTagName() {
      return [{ style: {} }];
    }
  };
  const context = {
    document,
    navigator: {},
    performance: { now: () => 0 },
    devicePixelRatio: 1,
    requestAnimationFrame: () => 1,
    cancelAnimationFrame() {},
    getComputedStyle: () => ({ getPropertyValue: () => '16px' }),
    HTMLElement: class {},
    HTMLCanvasElement: class {},
    Date,
    Object,
    Number,
    Math,
    isFinite,
    isNaN,
    setTimeout,
    clearTimeout,
    console,
    eleIds: { danmakuFixedSpeedStatus: 'status' },
    getById: () => null,
    Emby: {
      importModule: async () => {
        importCount++;
        return customConstructor;
      }
    },
    define() {
      amdDefineCount++;
    }
  };
  context.define.amd = {};
  context.window = context;
  context.self = context;
  context.globalThis = context;
  return {
    context,
    importCount: () => importCount,
    amdDefineCount: () => amdDefineCount
  };
}

test('CustomCssJS loader captures the bundled engine even when AMD is present', async () => {
  const candidate = await readFile(path.join(root, 'dist', 'dd-danmaku.CustomCssJS.js'), 'utf8');
  const block = extractRequireBlock(candidate);
  function IncompatibleCustomEngine() {}
  const state = createLoaderContext(IncompatibleCustomEngine);
  const source = [
    "let requireDanmakuPath = 'https://danmaku.7o7o.cc/danmaku.min.js';",
    block,
    'globalThis.__loader = {',
    '  controlled: controlledDanmakuConstructor,',
    '  resolve: resolveDanmakuConstructor,',
    '  capable: hasFixedSpeedCapability,',
    '  diagnose: setFixedSpeedDiagnostic,',
    '  setPath: value => { requireDanmakuPath = value; }',
    '};'
  ].join('\n');
  vm.runInNewContext(source, state.context);

  assert.equal(state.amdDefineCount(), 0, 'the controlled build does not disappear into AMD registration');
  assert.equal(state.context.__loader.controlled, state.context.Danmaku);
  assert.equal(state.context.__loader.capable(state.context.__loader.controlled), true);
  const defaultConstructor = await state.context.__loader.resolve();
  assert.equal(defaultConstructor, state.context.__loader.controlled);
  assert.equal(state.importCount(), 0, 'default CustomCssJS path is fully bundled');

  state.context.__loader.setPath('https://example.invalid/custom-danmaku.js');
  const custom = await state.context.__loader.resolve();
  assert.equal(custom, IncompatibleCustomEngine);
  assert.equal(state.context.__loader.capable(custom), false);
  assert.equal(state.importCount(), 1);
  assert.equal(await state.context.__loader.resolve(), IncompatibleCustomEngine);
  assert.equal(state.importCount(), 1, 'same explicit custom path is loaded once');

  state.context.__loader.diagnose(true, false, custom, 'engine-capability-missing');
  assert.equal(state.context.__ddDanmakuFixedSpeedDiagnostic.requested, true);
  assert.equal(state.context.__ddDanmakuFixedSpeedDiagnostic.effective, false);
});
