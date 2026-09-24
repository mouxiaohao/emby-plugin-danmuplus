import vm from 'node:vm';
import { readFile } from 'node:fs/promises';

class FakeEventTarget {
  constructor() {
    this.listeners = new Map();
  }

  addEventListener(type, listener) {
    const list = this.listeners.get(type) || [];
    list.push(listener);
    this.listeners.set(type, list);
  }

  removeEventListener(type, listener) {
    const list = this.listeners.get(type) || [];
    this.listeners.set(type, list.filter(item => item !== listener));
  }

  dispatchEvent(event) {
    event.target = event.target || this;
    for (const listener of [...(this.listeners.get(event.type) || [])]) {
      listener.call(this, event);
    }
    return !event.defaultPrevented;
  }
}

class FakeElement extends FakeEventTarget {
  constructor(tagName = 'div') {
    super();
    this.tagName = tagName.toUpperCase();
    this.style = { cssText: '', transform: '' };
    this.children = [];
    this.parentNode = null;
    this.offsetWidth = tagName === 'comment' ? 120 : 1000;
    this.offsetHeight = tagName === 'comment' ? 24 : 500;
    this.textContent = '';
  }

  get lastChild() {
    return this.children.length ? this.children[this.children.length - 1] : null;
  }

  appendChild(node) {
    if (node && node.isFragment) {
      for (const child of [...node.children]) this.appendChild(child);
      node.children = [];
      return node;
    }
    if (node.parentNode) node.parentNode.removeChild(node);
    node.parentNode = this;
    this.children.push(node);
    return node;
  }

  append(...nodes) {
    for (const node of nodes) this.appendChild(node);
  }

  prepend(node) {
    if (node.parentNode) node.parentNode.removeChild(node);
    node.parentNode = this;
    this.children.unshift(node);
  }

  removeChild(node) {
    const index = this.children.indexOf(node);
    if (index >= 0) this.children.splice(index, 1);
    node.parentNode = null;
    return node;
  }

  remove() {
    if (this.parentNode) this.parentNode.removeChild(this);
  }
}

class FakeCanvasElement extends FakeElement {
  constructor() {
    super('canvas');
    this.context = this.getContext('2d');
  }

  getContext() {
    return {
      clearRect() {}, drawImage() {}, fillText() {}, strokeText() {}, scale() {},
      measureText(text) { return { width: String(text).length * 10 }; },
      font: '', textBaseline: '', lineWidth: 0
    };
  }
}

export class FakeMedia extends FakeEventTarget {
  constructor({ currentTime = 0, playbackRate = 1, paused = false } = {}) {
    super();
    this.currentTime = currentTime;
    this.playbackRate = playbackRate;
    this.paused = paused;
  }

  dispatch(type) {
    if (type === 'pause') this.paused = true;
    if (type === 'play' || type === 'playing') this.paused = false;
    this.dispatchEvent({ type });
  }
}

export async function createEngineHarness(enginePath) {
  const code = await readFile(enginePath, 'utf8');
  let nowMs = 0;
  let nextRafId = 1;
  const rafCallbacks = new Map();

  const document = {
    createElement(tagName) {
      if (tagName === 'canvas') return new FakeCanvasElement();
      const element = new FakeElement(tagName);
      if (tagName === 'div') {
        element.offsetWidth = 120;
        element.offsetHeight = 24;
      }
      return element;
    },
    createDocumentFragment() {
      const fragment = new FakeElement('fragment');
      fragment.isFragment = true;
      return fragment;
    },
    getElementsByTagName() {
      return [new FakeElement('html')];
    }
  };

  const windowBindings = {
    performance: { now: () => nowMs },
    devicePixelRatio: 1,
    requestAnimationFrame(callback) {
      const id = nextRafId++;
      rafCallbacks.set(id, callback);
      return id;
    },
    cancelAnimationFrame(id) {
      rafCallbacks.delete(id);
    },
    getComputedStyle() {
      return { getPropertyValue: () => '16px' };
    }
  };

  const context = {
    module: { exports: {} },
    exports: {},
    document,
    HTMLElement: FakeElement,
    HTMLCanvasElement: FakeCanvasElement,
    setTimeout,
    clearTimeout,
    Date,
    Object,
    Math,
    Number,
    isFinite,
    isNaN,
    console,
    ...windowBindings
  };
  context.window = context;
  context.globalThis = context;
  vm.runInNewContext(code, context, { filename: enginePath });

  return {
    Danmaku: context.module.exports,
    globalDanmaku: context.window.Danmaku,
    makeContainer(width = 1000, height = 500) {
      const container = new FakeElement('container');
      container.offsetWidth = width;
      container.offsetHeight = height;
      return container;
    },
    setNow(ms) {
      nowMs = ms;
    },
    step(ms) {
      nowMs = ms;
      const callbacks = [...rafCallbacks.entries()];
      rafCallbacks.clear();
      for (const [, callback] of callbacks) callback(ms);
    },
    pendingFrames() {
      return rafCallbacks.size;
    }
  };
}

export function comment(time, mode = 'rtl', text = 'test') {
  return { time, mode, text };
}
