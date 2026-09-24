import assert from 'node:assert/strict';
import test from 'node:test';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { createEngineHarness, FakeMedia, comment } from './engine-harness.mjs';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const fixedEngine = path.join(root, 'src', 'danmaku.fixed-speed.js');
const upstreamEngine = path.join(root, 'vendor', 'danmaku-2.0.8', 'danmaku.js');

async function makeInstance(enginePath, {
  fixedSpeed = false,
  rate = 1,
  speed = 100,
  comments = [comment(0)]
} = {}) {
  const harness = await createEngineHarness(enginePath);
  const media = new FakeMedia({ currentTime: 0, playbackRate: rate, paused: false });
  const instance = new harness.Danmaku({
    container: harness.makeContainer(),
    media,
    comments,
    engine: 'dom',
    speed,
    fixedSpeed
  });
  return { harness, media, instance };
}

function position(instance, index = 0) {
  return instance._.runningList[index] && instance._.runningList[index].x;
}

function runningSnapshot(instance) {
  return JSON.parse(JSON.stringify(
    instance._.runningList.map(item => [item.text, item.x, item.y])
  ));
}

test('controlled engine exposes an immutable capability marker', async () => {
  const harness = await createEngineHarness(fixedEngine);
  assert.equal(harness.globalDanmaku, harness.Danmaku,
    'the controlled constructor is assigned globally even when a module loader exists');
  assert.deepEqual(
    JSON.parse(JSON.stringify(harness.Danmaku.DD_DANMAKU_CAPABILITY)),
    { id: 'dd-danmaku-wall-clock-v1', engineVersion: '2.0.8', option: 'fixedSpeed' }
  );
  assert.equal(Object.isFrozen(harness.Danmaku.DD_DANMAKU_CAPABILITY), true);
  const descriptor = Object.getOwnPropertyDescriptor(harness.Danmaku, 'DD_DANMAKU_CAPABILITY');
  assert.equal(descriptor.writable, false);
  assert.equal(descriptor.configurable, false);
});

test('disabled mode matches upstream 2.0.8 fixtures at every playback rate', async () => {
  for (const rate of [0.5, 1, 1.5, 2]) {
    const patched = await makeInstance(fixedEngine, { rate, fixedSpeed: false });
    const upstream = await makeInstance(upstreamEngine, { rate });
    for (const wallMs of [100, 600, 1100, 3100, 6100]) {
      const mediaTime = 0.01 + wallMs / 1000 * rate;
      patched.media.currentTime = mediaTime;
      upstream.media.currentTime = mediaTime;
      patched.harness.step(wallMs);
      upstream.harness.step(wallMs);
      assert.equal(patched.instance._.runningList.length, upstream.instance._.runningList.length,
        `running count at ${rate}x / ${wallMs}ms`);
      if (patched.instance._.runningList.length) {
        assert.equal(position(patched.instance), position(upstream.instance),
          `position at ${rate}x / ${wallMs}ms`);
        assert.equal(patched.instance._.runningList[0].y, upstream.instance._.runningList[0].y);
      }
    }
  }
});

test('disabled pause/resume, seek and collision allocation match upstream 2.0.8', async () => {
  const comments = [comment(0, 'rtl', 'first'), comment(0.1, 'rtl', 'second'), comment(50, 'rtl', 'seek')];
  const patched = await makeInstance(fixedEngine, { rate: 2, fixedSpeed: false, comments: structuredClone(comments) });
  const upstream = await makeInstance(upstreamEngine, { rate: 2, comments: structuredClone(comments) });
  for (const state of [patched, upstream]) {
    state.media.currentTime = 0.01;
    state.harness.step(100);
    state.media.currentTime = 0.4;
    state.harness.step(300);
  }
  assert.deepEqual(
    runningSnapshot(patched.instance),
    runningSnapshot(upstream.instance)
  );
  for (const state of [patched, upstream]) {
    state.media.dispatch('pause');
    state.harness.setNow(2300);
    state.media.dispatch('play');
    state.media.currentTime = 0.4;
    state.harness.step(2300);
  }
  assert.deepEqual(
    runningSnapshot(patched.instance),
    runningSnapshot(upstream.instance)
  );
  for (const state of [patched, upstream]) {
    state.media.currentTime = 50;
    state.media.dispatch('seeking');
    state.media.currentTime = 50.01;
    state.harness.step(2400);
  }
  assert.deepEqual(
    runningSnapshot(patched.instance),
    runningSnapshot(upstream.instance)
  );
});

test('fixed mode has the same wall-clock slope at 0.5x, 1x, 1.5x and 2x', async () => {
  const deltas = [];
  for (const rate of [0.5, 1, 1.5, 2]) {
    const state = await makeInstance(fixedEngine, { rate, fixedSpeed: true });
    state.media.currentTime = 0.01;
    state.harness.step(100);
    const start = position(state.instance);
    state.media.currentTime = 0.01 + rate;
    state.harness.step(1100);
    deltas.push(start - position(state.instance));
  }
  for (const delta of deltas) assert.ok(Math.abs(delta - deltas[0]) < 1e-9);
  assert.ok(deltas[0] > 0);
});

test('fixed collision holds a busy track and releases it from wall-clock motion age', async () => {
  const comments = [
    comment(0, 'rtl', 'leader'),
    comment(0.1, 'rtl', 'blocked'),
    comment(8, 'rtl', 'released')
  ];
  const state = await makeInstance(fixedEngine, { rate: 2, fixedSpeed: true, comments });
  state.media.currentTime = 0.01;
  state.harness.step(100);
  state.media.currentTime = 0.4;
  state.harness.step(300);
  assert.equal(state.instance._.runningList.find(item => item.text === 'leader').y, 0);
  assert.ok(state.instance._.runningList.find(item => item.text === 'blocked').y > 0);
  state.media.currentTime = 8.2;
  state.harness.step(4200);
  assert.equal(state.instance._.runningList.find(item => item.text === 'released').y, 0);
});

test('live rate changes do not jump or reset visible rolling comments', async () => {
  const state = await makeInstance(fixedEngine, { rate: 0.5, fixedSpeed: true });
  state.media.currentTime = 0.01;
  state.harness.step(100);
  state.media.currentTime = 0.51;
  state.harness.step(1100);
  const before = position(state.instance);
  const sameComment = state.instance._.runningList[0];
  state.media.playbackRate = 2;
  state.harness.step(1100);
  assert.equal(position(state.instance), before);
  assert.equal(state.instance._.runningList[0], sameComment);
  state.media.currentTime = 2.51;
  state.harness.step(2100);
  assert.ok(position(state.instance) < before);
  assert.equal(state.instance._.runningList.length, 1);
});

test('2x to 0.5x and 1x to 1.5x transitions keep the same motion anchor', async () => {
  for (const [fromRate, toRate] of [[2, 0.5], [1, 1.5]]) {
    const state = await makeInstance(fixedEngine, { rate: fromRate, fixedSpeed: true });
    state.media.currentTime = 0.01;
    state.harness.step(100);
    state.media.currentTime = 0.01 + fromRate;
    state.harness.step(1100);
    const before = position(state.instance);
    const anchor = state.instance._.runningList[0]._motionAnchor;
    state.media.playbackRate = toRate;
    state.harness.step(1100);
    assert.equal(position(state.instance), before, `${fromRate}x -> ${toRate}x position`);
    assert.equal(state.instance._.runningList[0]._motionAnchor, anchor,
      `${fromRate}x -> ${toRate}x anchor`);
    state.media.currentTime += toRate;
    state.harness.step(2100);
    assert.ok(position(state.instance) < before);
  }
});

test('pause, wait and resume freeze then continue from the same position', async () => {
  const state = await makeInstance(fixedEngine, { rate: 2, fixedSpeed: true });
  state.media.currentTime = 0.01;
  state.harness.step(100);
  state.media.currentTime = 2.01;
  state.harness.step(1100);
  const frozen = position(state.instance);
  state.media.dispatch('pause');
  assert.equal(state.harness.pendingFrames(), 0);
  state.harness.setNow(4100);
  state.media.dispatch('play');
  state.harness.step(4100);
  assert.equal(position(state.instance), frozen);
  state.media.currentTime = 4.01;
  state.harness.step(5100);
  assert.ok(position(state.instance) < frozen);

  const afterPlay = position(state.instance);
  state.media.dispatch('waiting');
  state.harness.setNow(7100);
  state.media.dispatch('playing');
  state.harness.step(7100);
  assert.equal(position(state.instance), afterPlay);
});

test('seek clears rolling motion and schedules from destination media time', async () => {
  const comments = [comment(0, 'rtl', 'old'), comment(50, 'rtl', 'destination')];
  const state = await makeInstance(fixedEngine, { rate: 2, fixedSpeed: true, comments });
  state.media.currentTime = 0.01;
  state.harness.step(100);
  assert.equal(state.instance._.runningList[0].text, 'old');
  state.media.currentTime = 50;
  state.media.dispatch('seeking');
  assert.equal(state.instance._.runningList.length, 0);
  state.media.currentTime = 50.01;
  state.harness.step(200);
  assert.equal(state.instance._.runningList[0].text, 'destination');
  assert.equal(state.instance._.runningList[0].time, 50);
});

test('backward and paused seek restart from the destination without reviving stale motion', async () => {
  const comments = [comment(10, 'rtl', 'backward'), comment(50, 'rtl', 'forward')];
  const state = await makeInstance(fixedEngine, { rate: 2, fixedSpeed: true, comments });
  state.media.currentTime = 50.01;
  state.harness.step(100);
  assert.equal(state.instance._.runningList[0].text, 'forward');
  state.media.dispatch('pause');
  state.media.currentTime = 10;
  state.media.dispatch('seeking');
  assert.equal(state.instance._.runningList.length, 0);
  state.harness.setNow(500);
  state.media.dispatch('play');
  state.media.currentTime = 10.01;
  state.harness.step(500);
  assert.equal(state.instance._.runningList[0].text, 'backward');
  assert.equal(state.instance._.runningList[0]._motionElapsed, 0);
});

test('hide clears the stage and show resumes selection from current media time', async () => {
  const comments = [comment(0, 'rtl', 'before-hide'), comment(30, 'rtl', 'after-show')];
  const state = await makeInstance(fixedEngine, { rate: 2, fixedSpeed: true, comments });
  state.media.currentTime = 0.01;
  state.harness.step(100);
  assert.equal(state.instance._.runningList[0].text, 'before-hide');
  state.instance.hide();
  assert.equal(state.instance._.runningList.length, 0);
  assert.equal(state.harness.pendingFrames(), 0);
  state.media.currentTime = 30.01;
  state.harness.setNow(2100);
  state.instance.show();
  state.harness.step(2100);
  assert.equal(state.instance._.runningList[0].text, 'after-show');
});

test('rolling removal follows motion age while top/bottom lifetime stays media-time based', async () => {
  const rolling = await makeInstance(fixedEngine, { rate: 2, fixedSpeed: true, comments: [comment(0)] });
  rolling.media.currentTime = 0.01;
  rolling.harness.step(100);
  rolling.media.currentTime = 12;
  rolling.harness.step(6100);
  assert.equal(rolling.instance._.runningList.length, 1, 'rolling comment survives until wall duration');
  rolling.media.currentTime = 21;
  rolling.harness.step(10200);
  assert.equal(rolling.instance._.runningList.length, 0);

  const fixed = await makeInstance(fixedEngine, { rate: 2, fixedSpeed: true, comments: [comment(0, 'top')] });
  fixed.media.currentTime = 0.01;
  fixed.harness.step(100);
  fixed.media.currentTime = 12;
  fixed.harness.step(6100);
  assert.equal(fixed.instance._.runningList.length, 0, 'top comment retains media-time lifetime');
});

test('off-screen rolling comments release collision tracks before new allocation', async () => {
  const comments = [
    comment(0, 'rtl', 'departed'),
    comment(20, 'rtl', 'replacement')
  ];
  const state = await makeInstance(fixedEngine, { rate: 2, fixedSpeed: true, comments });
  state.media.currentTime = 0.01;
  state.harness.step(100);
  assert.equal(state.instance._.runningList[0].y, 0);

  state.media.currentTime = 20.01;
  state.harness.step(10200);
  assert.equal(state.instance._.runningList.length, 1);
  assert.equal(state.instance._.runningList[0].text, 'replacement');
  assert.equal(state.instance._.runningList[0].y, 0);
  assert.equal(state.instance._.space.rtl.filter(item => item.comment).length, 1);
  assert.equal(Object.hasOwn(comments[0], '_motionElapsed'), false);
  assert.equal(Object.hasOwn(comments[0], '_motionAnchor'), false);
});

test('fixed-mode clear releases active collision tracks and motion state', async () => {
  const comments = [comment(0, 'rtl', 'active')];
  const state = await makeInstance(fixedEngine, { fixedSpeed: true, comments });
  state.media.currentTime = 0.01;
  state.harness.step(100);
  assert.equal(state.instance._.space.rtl.filter(item => item.comment).length, 1);

  state.instance.clear();
  assert.equal(state.instance._.runningList.length, 0);
  assert.equal(state.instance._.space.rtl.length, 2);
  assert.equal(Object.hasOwn(comments[0], '_motionElapsed'), false);
  assert.equal(Object.hasOwn(comments[0], '_motionAnchor'), false);
});

test('fixed mode tolerates invalid rates, supports three base speeds, ltr, and cleans dense lists', async () => {
  const slopes = [];
  for (const speed of [50, 100, 200]) {
    const state = await makeInstance(fixedEngine, {
      rate: Number.NaN,
      speed,
      fixedSpeed: true,
      comments: [comment(0, 'ltr')]
    });
    state.media.currentTime = 0.01;
    state.harness.step(100);
    const start = position(state.instance);
    state.media.currentTime = 1.01;
    state.harness.step(1100);
    const end = position(state.instance);
    assert.ok(Number.isFinite(end));
    slopes.push(end - start);
  }
  assert.ok(slopes[0] < slopes[1] && slopes[1] < slopes[2]);

  const denseComments = Array.from({ length: 500 }, (_, index) => comment(index / 10000));
  const dense = await makeInstance(fixedEngine, { rate: 2, fixedSpeed: true, comments: denseComments });
  dense.media.currentTime = 0.1;
  dense.harness.step(100);
  assert.equal(dense.instance._.runningList.length, 500);
  dense.media.currentTime = 30;
  dense.harness.step(10200);
  assert.equal(dense.instance._.runningList.length, 0);
  assert.deepEqual(denseComments.map(item => item.time),
    Array.from({ length: 500 }, (_, index) => index / 10000));
});
