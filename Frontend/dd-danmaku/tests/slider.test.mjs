import assert from 'node:assert/strict';
import test from 'node:test';
import vm from 'node:vm';
import { readFile } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');

class FakeEvent {
  constructor(type, options = {}) {
    this.type = type;
    this.bubbles = !!options.bubbles;
    this.key = options.key;
    this.code = options.code;
    this.keyCode = options.keyCode || 0;
    this.which = options.which || 0;
    this.repeat = !!options.repeat;
    this.detail = options.detail;
    this.defaultPrevented = false;
    this.propagationStopped = false;
  }

  preventDefault() {
    this.defaultPrevented = true;
  }

  stopPropagation() {
    this.propagationStopped = true;
  }
}

class FakeSlider {
  constructor(document, { min, max, step, value = min ?? 0 } = {}) {
    this.document = document;
    this.attributes = new Map();
    this.listeners = new Map();
    this.value = String(value);
    this.disabled = false;
    this.hidden = false;
    this.tabIndex = 0;
    if (min !== undefined) this.setAttribute('min', min);
    if (max !== undefined) this.setAttribute('max', max);
    if (step !== undefined) this.setAttribute('step', step);
    this.setAttribute('orient', 'horizontal');
  }

  setAttribute(name, value) {
    this.attributes.set(name, String(value));
  }

  getAttribute(name) {
    return this.attributes.has(name) ? this.attributes.get(name) : null;
  }

  addEventListener(type, listener) {
    const list = this.listeners.get(type) || [];
    list.push(listener);
    this.listeners.set(type, list);
  }

  dispatchEvent(event) {
    event.target = this;
    for (const listener of [...(this.listeners.get(event.type) || [])]) listener.call(this, event);
    return !event.defaultPrevented;
  }

  focus() {
    this.document.activeElement = this;
  }
}

async function loadSliderHelpers() {
  const helperPath = path.join(root, 'src', 'tv-slider-helpers.js.inc');
  const source = await readFile(helperPath, 'utf8');
  const document = { activeElement: null };
  const context = {
    module: { exports: {} },
    document,
    Event: FakeEvent,
    Number,
    Math,
    String,
    parseFloat,
    parseInt,
    console
  };
  vm.runInNewContext(source + '\nmodule.exports = {' +
    ' normalizeDdSliderDirection, calculateDdSliderStep, installDdSliderDirectionHandler };', context);
  return { ...context.module.exports, document };
}

function press(slider, key, extra = {}) {
  const event = new FakeEvent('keydown', { key, ...extra });
  slider.dispatchEvent(event);
  return event;
}

test('fractional remote step is normalized and dispatches input then change once', async () => {
  const helpers = await loadSliderHelpers();
  const slider = new FakeSlider(helpers.document, { min: 0.1, max: 3, step: 0.1, value: 0.9 });
  const events = [];
  slider.addEventListener('input', () => events.push('input'));
  slider.addEventListener('change', () => events.push('change'));
  slider.focus();
  helpers.installDdSliderDirectionHandler(slider);

  const event = press(slider, 'ArrowRight');
  assert.equal(slider.value, '1');
  assert.deepEqual(events, ['input', 'change']);
  assert.equal(event.defaultPrevented, true);
  assert.equal(event.propagationStopped, true);
  assert.equal(helpers.document.activeElement, slider);
});

test('aliases, key codes, nonzero minima and step=any use one declared step', async () => {
  const helpers = await loadSliderHelpers();
  const slider = new FakeSlider(helpers.document, { min: 3, max: 10, step: 'any', value: 5 });
  helpers.installDdSliderDirectionHandler(slider);
  press(slider, 'Right');
  assert.equal(slider.value, '6');
  press(slider, undefined, { keyCode: 37 });
  assert.equal(slider.value, '5');
  press(slider, 'Left');
  assert.equal(slider.value, '4');
});

test('outward boundary is consumed with no value or commit events', async () => {
  const helpers = await loadSliderHelpers();
  const slider = new FakeSlider(helpers.document, { min: 0, max: 2, step: 1, value: 0 });
  let inputs = 0;
  let changes = 0;
  slider.addEventListener('input', () => inputs++);
  slider.addEventListener('change', () => changes++);
  slider.focus();
  helpers.installDdSliderDirectionHandler(slider);
  const event = press(slider, 'ArrowLeft');
  assert.equal(slider.value, '0');
  assert.equal(inputs, 0);
  assert.equal(changes, 0);
  assert.equal(event.defaultPrevented, true);
  assert.equal(event.propagationStopped, true);
  assert.equal(helpers.document.activeElement, slider);
});

test('each delivered repeat commits at most one step and stops at the maximum', async () => {
  const helpers = await loadSliderHelpers();
  const slider = new FakeSlider(helpers.document, { min: 0, max: 3, step: 1, value: 0 });
  let changes = 0;
  slider.addEventListener('change', () => changes++);
  helpers.installDdSliderDirectionHandler(slider);
  for (let index = 0; index < 8; index++) press(slider, 'ArrowRight', { repeat: index > 0 });
  assert.equal(slider.value, '3');
  assert.equal(changes, 3);
});

test('DOM and per-element Emby routes deduplicate the same physical action', async () => {
  const helpers = await loadSliderHelpers();
  const slider = new FakeSlider(helpers.document, { min: 0, max: 10, step: 1, value: 4 });
  let changes = 0;
  slider.addEventListener('change', () => changes++);
  helpers.installDdSliderDirectionHandler(slider);
  const physicalEvent = press(slider, 'ArrowRight');
  slider.dispatchEvent(new FakeEvent('emby-direction', {
    detail: { direction: 'right', originalEvent: physicalEvent }
  }));
  assert.equal(slider.value, '5');
  assert.equal(changes, 1);

  slider.ddDanmakuAdjustDirection('left', physicalEvent);
  assert.equal(slider.value, '5', 'same event identity is still deduplicated');
});

test('Up/Down, disabled controls and vertical controls remain host navigation actions', async () => {
  const helpers = await loadSliderHelpers();
  const slider = new FakeSlider(helpers.document, { min: 0, max: 10, step: 1, value: 4 });
  helpers.installDdSliderDirectionHandler(slider);
  const up = press(slider, 'ArrowUp');
  assert.equal(slider.value, '4');
  assert.equal(up.defaultPrevented, false);
  assert.equal(up.propagationStopped, false);

  slider.disabled = true;
  const right = press(slider, 'ArrowRight');
  assert.equal(slider.value, '4');
  assert.equal(right.defaultPrevented, false);
  slider.disabled = false;
  slider.setAttribute('orient', 'vertical');
  const verticalRight = press(slider, 'ArrowRight');
  assert.equal(verticalRight.defaultPrevented, false);
});

test('all registry-backed visible slider shapes adjust at min, middle and max', async () => {
  const helpers = await loadSliderHelpers();
  const candidate = await readFile(path.join(root, 'dist', 'dd-danmaku.CustomCssJS.js'), 'utf8');
  const definitions = [...candidate.matchAll(
    /^\s+([A-Za-z0-9_]+): \{ id: '[^']+', defaultValue: [^,]+, name: '[^']+', min: (-?[\d.]+), max: (-?[\d.]+), step: (-?[\d.]+)\s*\},?$/gm
  )].map(match => ({
    name: match[1], min: Number(match[2]), max: Number(match[3]), step: Number(match[4])
  }));
  assert.ok(definitions.length >= 10, 'expected the full numeric settings registry');

  for (const definition of definitions) {
    const middle = definition.min + definition.step * 2 <= definition.max
      ? definition.min + definition.step
      : definition.min;
    const slider = new FakeSlider(helpers.document, { ...definition, value: middle });
    helpers.installDdSliderDirectionHandler(slider);
    const before = Number(slider.value);
    press(slider, 'ArrowRight');
    const expected = Number(Math.min(definition.max, before + definition.step).toFixed(9));
    assert.equal(Number(slider.value), expected, definition.name);
    slider.value = String(definition.max);
    const boundary = press(slider, 'ArrowRight');
    assert.equal(Number(slider.value), definition.max, `${definition.name} max`);
    assert.equal(boundary.defaultPrevented, true);
    slider.value = String(definition.min);
    press(slider, 'ArrowLeft');
    assert.equal(Number(slider.value), definition.min, `${definition.name} min`);
  }
});

test('one remote action produces one label update, persistence, reload, and no media seek', async () => {
  const helpers = await loadSliderHelpers();
  const slider = new FakeSlider(helpers.document, { min: 1, max: 99, step: 1, value: 95 });
  const state = { labels: 0, saves: 0, reloads: 0, seeks: 0 };
  slider.addEventListener('input', () => state.labels++);
  slider.addEventListener('change', () => {
    state.saves++;
    state.reloads++;
  });
  helpers.installDdSliderDirectionHandler(slider);
  press(slider, 'ArrowRight');
  assert.deepEqual(state, { labels: 1, saves: 1, reloads: 1, seeks: 0 });
});
