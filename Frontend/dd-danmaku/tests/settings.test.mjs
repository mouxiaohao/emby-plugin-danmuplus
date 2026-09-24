import assert from 'node:assert/strict';
import test from 'node:test';
import vm from 'node:vm';
import { readFile } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');

class MemoryStorage {
  constructor() {
    this.data = new Map();
    this.writeCount = 0;
  }
  getItem(key) { return this.data.has(key) ? this.data.get(key) : null; }
  setItem(key, value) { this.data.set(key, String(value)); this.writeCount++; }
  removeItem(key) { this.data.delete(key); }
}

async function loadSettingsFunctions(storage) {
  const candidate = await readFile(path.join(root, 'dist', 'dd-danmaku.CustomCssJS.js'), 'utf8');
  const start = candidate.indexOf('    function getSettingsJson(space = 4) {');
  const end = candidate.indexOf('    function destroyAllInterval()', start);
  assert.ok(start >= 0 && end > start);
  const functions = candidate.slice(start, end);
  const context = {
    module: { exports: {} },
    localStorage: storage,
    lsKeys: {
      fixedSpeed: { id: 'danmakuFixedSpeed', defaultValue: false, name: 'fixed' },
      speed: { id: 'danmakuBaseSpeed', defaultValue: 1, name: 'speed' },
      filters: { id: 'danmakuFilters', defaultValue: [], name: 'filters' },
      filterKeywords: { id: 'danmakuFilterKeywords', defaultValue: '', name: 'keywords' }
    },
    objectEntries: Object.entries,
    Object,
    Array,
    JSON,
    parseFloat
  };
  const exportCode = [
    functions,
    'module.exports = { getSettingsJson, lsGetItem, lsSetItem, lsBatchSet };'
  ].join('\n');
  vm.runInNewContext(exportCode, context);
  return context.module.exports;
}

test('missing and invalid fixed-speed values read false without eager migration writes', async () => {
  const storage = new MemoryStorage();
  const settings = await loadSettingsFunctions(storage);
  assert.equal(settings.lsGetItem('danmakuFixedSpeed'), false);
  assert.equal(storage.writeCount, 0);
  storage.setItem('danmakuFixedSpeed', 'invalid');
  const writesBeforeRead = storage.writeCount;
  assert.equal(settings.lsGetItem('danmakuFixedSpeed'), false);
  assert.equal(storage.writeCount, writesBeforeRead);
  storage.setItem('danmakuFixedSpeed', 'true');
  assert.equal(settings.lsGetItem('danmakuFixedSpeed'), true);
  storage.setItem('danmakuFixedSpeed', 'false');
  assert.equal(settings.lsGetItem('danmakuFixedSpeed'), false);
});

test('explicit choice persists across a fresh settings-function instance', async () => {
  const storage = new MemoryStorage();
  const first = await loadSettingsFunctions(storage);
  first.lsSetItem('danmakuFixedSpeed', true);
  first.lsSetItem('danmakuBaseSpeed', 1.7);
  const reopened = await loadSettingsFunctions(storage);
  assert.equal(reopened.lsGetItem('danmakuFixedSpeed'), true);
  assert.equal(reopened.lsGetItem('danmakuBaseSpeed'), 1.7);
});

test('JSON export/import includes fixed speed and applies every key without discarding unrelated storage', async () => {
  const storage = new MemoryStorage();
  const settings = await loadSettingsFunctions(storage);
  settings.lsSetItem('danmakuFixedSpeed', true);
  settings.lsSetItem('danmakuBaseSpeed', 1.5);
  settings.lsSetItem('danmakuFilters', ['a', 'b']);
  const exported = JSON.parse(settings.getSettingsJson(0));
  assert.deepEqual(JSON.parse(JSON.stringify(exported)), {
    danmakuFixedSpeed: true,
    danmakuBaseSpeed: 1.5,
    danmakuFilters: ['a', 'b'],
    danmakuFilterKeywords: ''
  });

  const importedStorage = new MemoryStorage();
  importedStorage.setItem('unrelated-user-key', 'keep');
  const importedSettings = await loadSettingsFunctions(importedStorage);
  importedSettings.lsBatchSet(exported);
  assert.equal(importedSettings.lsGetItem('danmakuFixedSpeed'), true);
  assert.equal(importedSettings.lsGetItem('danmakuBaseSpeed'), 1.5);
  assert.deepEqual(JSON.parse(JSON.stringify(importedSettings.lsGetItem('danmakuFilters'))), ['a', 'b']);
  assert.equal(importedStorage.getItem('unrelated-user-key'), 'keep');
});
