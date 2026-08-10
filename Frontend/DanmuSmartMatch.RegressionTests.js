"use strict";

const fs = require("fs");
const vm = require("vm");
const path = require("path");

function assert(condition, message) {
    if (!condition) throw new Error(message);
}

const documentStub = {
    body: {},
    addEventListener: function () {},
    querySelectorAll: function () { return []; }
};
const context = {
    window: {
        location: { hash: "" },
        setTimeout: function () { return 1; },
        clearTimeout: function () {}
    },
    document: documentStub,
    MutationObserver: function () { this.observe = function () {}; },
    console: { info: function () {}, debug: function () {}, error: function () {} },
    URLSearchParams: URLSearchParams,
    Date: Date
};
context.window.window = context.window;
context.window.document = documentStub;
vm.createContext(context);
const scriptPath = path.join(__dirname, "DanmuSmartMatch.CustomCssJS.js");
vm.runInContext(fs.readFileSync(scriptPath, "utf8"), context, { filename: scriptPath });
const hooks = context.window.__embyDanmuSmartMatchTest;

assert(hooks.isSupportedItemType("Series") && hooks.isSupportedItemType("Season") &&
    hooks.isSupportedItemType("Episode") && hooks.isSupportedItemType("Movie"),
"all smart-match item types should be supported");
assert(!hooks.isSupportedItemType("Folder") && !hooks.isSupportedItemType("CollectionFolder"),
    "unsupported menu item types must remain excluded");
assert(hooks.actionLabel("Episode") === "智能匹配并下载本集弹幕" &&
    hooks.actionLabel("Movie") === "智能匹配并下载电影弹幕",
"item types should receive deterministic labels");
assert(hooks.manualSearchDefault({ Type: "Movie", Name: "电影名" }, {}) === "电影名",
    "movie search should default to its own title");
assert(hooks.manualSearchDefault({ Type: "Episode", Name: "单集" }, { ParentName: "父剧名" }) === "父剧名",
    "episode search should default to its owning Series title");
assert(hooks.manualSearchDefault({ Type: "Season", Name: "第一季" }, { SeriesName: "父剧名" }) === "父剧名",
    "season search should default to its owning Series title");
assert(hooks.plausibleItemId("0123456789abcdef") === "0123456789abcdef" &&
    hooks.plausibleItemId("menu") === null,
"card identity should reject action ids and accept media ids");
const first = hooks.setPendingContext("first-item-id");
const second = hooks.setPendingContext("second-item-id");
assert(second.generation > first.generation && second.id === "second-item-id",
    "a later card menu should invalidate the previous asynchronous context");
assert(hooks.resolveMenuContextId(null, "menu-item-id") === "menu-item-id",
    "cards without DOM item ids should use the authoritative action-sheet preview id");
assert(hooks.resolveMenuContextId("clicked-item", "other-item") === null,
    "a mismatched action sheet must not reuse the clicked card identity");
assert(hooks.resolveMenuContextId(null, null) === null,
    "an action sheet without any resolvable identity must remain unsupported");
const fallbackAnchor = { id: "refresh" };
assert(hooks.findMenuInsertionAnchor({
    querySelector: function (selector) {
        return selector === '[data-id="refreshmetadata"]' ? fallbackAnchor : null;
    }
}) === fallbackAnchor,
"all item types should use the same fallback anchor instead of appending at the menu end");
const longPressedSeason = {
    closest: function () { return this; },
    matches: function (selector) { return selector.indexOf(".card") >= 0; },
    dataset: { itemId: "season-longpress-id" },
    querySelectorAll: function () { return []; },
    parentElement: null
};
assert(hooks.getGestureItemId(longPressedSeason) === "season-longpress-id",
    "Android long press should capture the pressed Season instead of its parent detail page");
assert(hooks.openedActionSheetContextId(
    { id: "stale-card-id", expires: Date.now() + 5000 },
    "authoritative-menu-id", 1, Date.now()) === "authoritative-menu-id",
    "an Android action sheet's authoritative item id should replace stale gesture context");
assert(hooks.openedActionSheetContextId(null, "menu-only-item-id", 1, Date.now()) === "menu-only-item-id",
    "an action sheet should bootstrap injection without a prior desktop click");
assert(hooks.openedActionSheetContextId(null, null, 1, Date.now()) === null,
    "an unidentified long-press action sheet must not guess the current detail item");

console.log("Danmu smart-match frontend regression checks passed.");
