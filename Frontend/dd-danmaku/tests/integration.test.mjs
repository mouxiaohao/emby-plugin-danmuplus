import assert from 'node:assert/strict';
import test from 'node:test';
import vm from 'node:vm';
import { readFile } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');

async function createTransactionHarness({
  constructorFails = false,
  postCommitFails = false,
  capable = true,
  requested = true,
  paused = false,
  hiddenAdapter = false
} = {}) {
  const include = await readFile(path.join(root, 'src', 'create-danmaku.js.inc'), 'utf8');
  const events = [];
  const media = {
    id: hiddenAdapter ? 'h5VideoAdapter' : '',
    currentTime: 42.25,
    playbackRate: 2,
    paused,
    dispatchEvent(event) { events.push(`media:${event.type}`); }
  };
  const previousWrapper = {
    removed: false,
    remove() { this.removed = true; events.push('old-wrapper:remove'); }
  };
  const previousDanmaku = {
    destroyed: false,
    destroy() { this.destroyed = true; events.push('old-engine:destroy'); }
  };
  const previousObserver = {
    disconnected: false,
    disconnect() { this.disconnected = true; events.push('old-observer:disconnect'); }
  };
  const container = {
    prepended: [],
    prepend(node) { this.prepended.push(node); events.push('pending-wrapper:prepend'); }
  };
  const values = {
    fixed: requested,
    height: 100,
    debug: false,
    speed: 1,
    engine: 'canvas',
    switch: true,
    chart: false
  };
  let constructedOptions = null;
  const constructedInstances = [];
  let diagnostic = null;

  class CandidateEngine {
    constructor(options) {
      if (constructorFails) throw new Error('simulated construction failure');
      constructedOptions = options;
      this.shown = false;
      this.destroyed = false;
      constructedInstances.push(this);
      events.push('new-engine:construct');
    }
    show() { this.shown = true; events.push('new-engine:show'); }
    hide() { events.push('new-engine:hide'); }
    destroy() { this.destroyed = true; events.push('new-engine:destroy'); }
    resize() {}
  }

  class FakeResizeObserver {
    constructor(callback) { this.callback = callback; }
    observe() { events.push('new-observer:observe'); }
    disconnect() { events.push('new-observer:disconnect'); }
  }

  const context = {
    console: { log() {}, warn() {}, error() {} },
    Event: class { constructor(type) { this.type = type; } },
    ResizeObserver: FakeResizeObserver,
    document: {
      querySelector: () => media,
      createElement() {
        return {
          id: '', style: {}, removed: false,
          remove() { this.removed = true; events.push('pending-wrapper:remove'); }
        };
      }
    },
    window: {
      ede: {
        danmaku: previousDanmaku,
        ob: previousObserver,
        commentsOriginal: [{ old: true }],
        commentsParsed: [{ old: true }]
      }
    },
    mediaQueryStr: '.media',
    mediaContainerQueryStr: '.container',
    isVersionOld: true,
    eleIds: {
      danmakuWrapper: 'danmaku-wrapper',
      heightPercent: 'height',
      debugShowDanmakuWrapper: 'debug',
      fixedSpeed: 'fixed',
      speed: 'speed',
      engine: 'engine',
      switch: 'switch',
      osdLineChartEnable: 'chart'
    },
    lsKeys: {
      heightPercent: { id: 'height' },
      debugShowDanmakuWrapper: { id: 'debug' },
      fixedSpeed: { id: 'fixed' },
      speed: { id: 'speed' },
      engine: { id: 'engine' },
      switch: { id: 'switch' },
      osdLineChartEnable: { id: 'chart' }
    },
    styles: { colors: { highlight: 'red' } },
    getById: id => {
      if (id !== 'danmaku-wrapper') return null;
      const committedCandidate = [...container.prepended].reverse()
        .find(node => !node.removed && node.id === id);
      return committedCandidate || (!previousWrapper.removed ? previousWrapper : null);
    },
    waitForElement: async () => container,
    danmakuParser: comments => comments.map(item => ({ ...item })),
    danmakuFilter: comments => comments,
    resolveDanmakuConstructor: async () => CandidateEngine,
    hasFixedSpeedCapability: () => capable,
    setFixedSpeedDiagnostic(request, effective, ctor, reason) {
      diagnostic = { request, effective, ctor, reason };
    },
    lsGetItem: id => values[id],
    buildCurrentDanmakuInfo() {
      if (postCommitFails) throw new Error('simulated post-commit UI failure');
    },
    currentDanmakuInfoContainerId: 'info',
    appendvideoOsdDanmakuInfo() {},
    buildProgressBarChart() {},
    require(dependencies, callback) {
      callback({
        getCurrentPlayer: () => ({}),
        getPlayerState: () => ({ PlayState: { IsPaused: paused } })
      });
    }
  };
  context.globalThis = context;
  vm.runInNewContext(include + '\nmoduleResult = createDanmaku;', context);

  return {
    createDanmaku: context.moduleResult,
    context,
    media,
    previousWrapper,
    previousDanmaku,
    previousObserver,
    container,
    events,
    constructedOptions: () => constructedOptions,
    constructedInstances,
    diagnostic: () => diagnostic
  };
}

test('successful runtime mode switch replaces exactly one instance without touching media state', async () => {
  const harness = await createTransactionHarness({ requested: true, capable: true, paused: false });
  const originalMedia = {
    currentTime: harness.media.currentTime,
    playbackRate: harness.media.playbackRate,
    paused: harness.media.paused
  };
  const comments = [{ time: 1, text: 'a' }, { time: 2, text: 'b' }];
  await harness.createDanmaku(comments);

  assert.deepEqual(
    { currentTime: harness.media.currentTime, playbackRate: harness.media.playbackRate, paused: harness.media.paused },
    originalMedia
  );
  assert.equal(harness.constructedOptions().fixedSpeed, true);
  assert.deepEqual(harness.constructedOptions().comments.map(item => item.time), [1, 2]);
  assert.equal(harness.previousDanmaku.destroyed, true);
  assert.equal(harness.previousWrapper.removed, true);
  assert.equal(harness.previousObserver.disconnected, true);
  assert.notEqual(harness.context.window.ede.danmaku, harness.previousDanmaku);
  assert.equal(harness.events.filter(item => item === 'new-engine:construct').length, 1);
  assert.equal(harness.diagnostic().request, true);
  assert.equal(harness.diagnostic().effective, true);
});

test('paused hidden adapter stays paused and receives the existing pause synchronization', async () => {
  const harness = await createTransactionHarness({ requested: true, capable: true, paused: true, hiddenAdapter: true });
  await harness.createDanmaku([{ time: 5, text: 'paused' }]);
  assert.equal(harness.media.paused, true);
  assert.equal(harness.media.currentTime, 42.25);
  assert.equal(harness.media.playbackRate, 2);
  assert.deepEqual(harness.events.filter(item => item === 'media:pause'), ['media:pause']);
});

test('incompatible custom engine keeps requested=true but runs effective legacy mode', async () => {
  const harness = await createTransactionHarness({ requested: true, capable: false });
  await harness.createDanmaku([{ time: 1, text: 'fallback' }]);
  assert.equal(harness.constructedOptions().fixedSpeed, false);
  assert.equal(harness.diagnostic().request, true);
  assert.equal(harness.diagnostic().effective, false);
  assert.equal(harness.diagnostic().reason, 'engine-capability-missing');
});

test('construction failure leaves the old instance, observer, wrapper and comments untouched', async () => {
  const harness = await createTransactionHarness({ constructorFails: true, requested: true });
  const oldCommentsOriginal = harness.context.window.ede.commentsOriginal;
  const oldCommentsParsed = harness.context.window.ede.commentsParsed;
  await assert.rejects(() => harness.createDanmaku([{ time: 9, text: 'failure' }]),
    /simulated construction failure/);
  assert.equal(harness.context.window.ede.danmaku, harness.previousDanmaku);
  assert.equal(harness.context.window.ede.ob, harness.previousObserver);
  assert.equal(harness.previousDanmaku.destroyed, false);
  assert.equal(harness.previousObserver.disconnected, false);
  assert.equal(harness.previousWrapper.removed, false);
  assert.equal(harness.context.window.ede.commentsOriginal, oldCommentsOriginal);
  assert.equal(harness.context.window.ede.commentsParsed, oldCommentsParsed);
  assert.ok(harness.events.includes('pending-wrapper:remove'));
});

test('post-commit UI failure does not roll back a successfully installed instance', async () => {
  const harness = await createTransactionHarness({ postCommitFails: true, requested: true });
  await assert.doesNotReject(() => harness.createDanmaku([{ time: 9, text: 'committed' }]));
  assert.notEqual(harness.context.window.ede.danmaku, harness.previousDanmaku);
  assert.equal(harness.previousDanmaku.destroyed, true);
  assert.equal(harness.previousObserver.disconnected, true);
  assert.equal(harness.previousWrapper.removed, true);
  assert.equal(harness.constructedOptions().fixedSpeed, true);
});

test('concurrent successful reloads replace the runtime at commit time without duplicate wrappers', async () => {
  const harness = await createTransactionHarness({ requested: true });
  await Promise.all([
    harness.createDanmaku([{ time: 1, text: 'first' }]),
    harness.createDanmaku([{ time: 2, text: 'second' }])
  ]);

  const liveWrappers = harness.container.prepended
    .filter(node => !node.removed && node.id === 'danmaku-wrapper');
  assert.equal(harness.events.filter(item => item === 'new-engine:construct').length, 2);
  assert.equal(liveWrappers.length, 1);
  assert.equal(harness.constructedInstances[0].destroyed, true);
  assert.equal(harness.constructedInstances[1].destroyed, false);
  assert.equal(harness.context.window.ede.danmaku, harness.constructedInstances[1]);
  assert.equal(typeof harness.context.window.ede.ob.callback, 'function');
  assert.equal(harness.previousDanmaku.destroyed, true);
  assert.equal(harness.previousWrapper.removed, true);
});
