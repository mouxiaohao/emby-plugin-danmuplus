"use strict";

const fs = require("fs");
const vm = require("vm");
const path = require("path");

function assert(condition, message) {
    if (!condition) throw new Error(message);
}

const anchorLayoutOffsets = {};

class FakeElement {
    constructor(tagName) {
        this.tagName = String(tagName || "div").toUpperCase();
        this._children = [];
        this.children = this._children;
        this.parentElement = null;
        this.dataset = {};
        this.style = {};
        this.attributes = {};
        this.listeners = {};
        this.className = "";
        this.textContent = "";
        this.isConnected = true;
        this.disabled = false;
        this.scrollTop = 0;
        this.scrollHeight = 0;
        this.clientHeight = 0;
        this._offsetTop = 0;
        this.classList = { contains: name => this.className.split(/\s+/).includes(name) };
    }

    append(...children) { children.forEach(child => this.appendChild(child)); }
    appendChild(child) {
        child.parentElement = this;
        this._children.push(child);
        return child;
    }

    get offsetTop() {
        const token = this.dataset && this.dataset.danmuNavAnchor;
        return token && Object.prototype.hasOwnProperty.call(anchorLayoutOffsets, token)
            ? anchorLayoutOffsets[token] : this._offsetTop;
    }
    set offsetTop(value) { this._offsetTop = Number(value || 0); }
    replaceChildren(...children) {
        this._children.forEach(child => { child.parentElement = null; });
        this._children.splice(0, this._children.length);
        this.append(...children);
    }
    before(child) {
        const index = this.parentElement._children.indexOf(this);
        child.parentElement = this.parentElement;
        this.parentElement._children.splice(index, 0, child);
    }
    remove() {
        if (this.parentElement) {
            const index = this.parentElement._children.indexOf(this);
            if (index >= 0) this.parentElement._children.splice(index, 1);
        }
        this.parentElement = null;
        this.isConnected = false;
    }
    cloneNode(deep) {
        const clone = new FakeElement(this.tagName);
        clone.className = this.className;
        clone.textContent = this.textContent;
        clone.dataset = Object.assign({}, this.dataset);
        clone.attributes = Object.assign({}, this.attributes);
        clone.scrollTop = this.scrollTop;
        clone.scrollHeight = this.scrollHeight;
        clone.clientHeight = this.clientHeight;
        clone.offsetTop = this.offsetTop;
        if (deep) this._children.forEach(child => clone.appendChild(child.cloneNode(true)));
        return clone;
    }
    setAttribute(name, value) { this.attributes[name] = String(value); }
    getAttribute(name) { return this.attributes[name] === undefined ? null : this.attributes[name]; }
    removeAttribute(name) { delete this.attributes[name]; }
    addEventListener(type, listener) {
        (this.listeners[type] || (this.listeners[type] = [])).push(listener);
    }
    async dispatch(type, overrides) {
        const event = Object.assign({
            target: this,
            isTrusted: type === "click" ? false : true,
            preventDefault: function () {},
            stopPropagation: function () {},
            stopImmediatePropagation: function () {}
        }, overrides || {});
        await Promise.all((this.listeners[type] || []).map(listener => listener(event)));
    }
    closest() { return null; }
    querySelector(selector) { return this.querySelectorAll(selector)[0] || null; }
    querySelectorAll(selector) {
        const matches = [];
        const dataMatch = selector.match(/^\[data-id="([^"]+)"\]$/);
        const classMatch = selector.match(/^\.([A-Za-z0-9_-]+)$/);
        const checkedInputMatch = selector.match(/^input\[name="([^"]+)"\]:checked$/);
        function visit(node) {
            node._children.forEach(child => {
                if ((dataMatch && child.dataset.id === dataMatch[1]) ||
                    (classMatch && child.className.split(/\s+/).includes(classMatch[1])) ||
                    (checkedInputMatch && child.tagName === "INPUT" &&
                        child.name === checkedInputMatch[1] && child.checked)) {
                    matches.push(child);
                }
                visit(child);
            });
        }
        visit(this);
        return matches;
    }
}

function useHtmlCollectionLikeChildren(element) {
    element.children = new Proxy({}, {
        get: function (_target, property) {
            if (property === "length") return element._children.length;
            if (typeof property === "string" && /^\d+$/.test(property)) {
                return element._children[Number(property)];
            }
            return undefined;
        }
    });
    element._children.forEach(useHtmlCollectionLikeChildren);
}

function menuFor(id) {
    const menu = new FakeElement("div");
    menu.className = "actionSheet opened";
    if (id) menu.dataset.itemId = id;
    const anchor = new FakeElement("button");
    anchor.className = "actionSheetMenuItem";
    anchor.dataset.id = "scan";
    menu.appendChild(anchor);
    return menu;
}

const documentStub = {
    body: new FakeElement("body"),
    head: new FakeElement("head"),
    openedMenus: [],
    listeners: {},
    captureListeners: {},
    addEventListener: function (type, listener, capture) {
        const target = capture === true ? this.captureListeners : this.listeners;
        (target[type] || (target[type] = [])).push(listener);
    },
    removeEventListener: function (type, listener, capture) {
        const target = capture === true ? this.captureListeners : this.listeners;
        const listeners = target[type] || [];
        const index = listeners.indexOf(listener);
        if (index >= 0) listeners.splice(index, 1);
    },
    dispatchKey: function (key) {
        const event = {
            key: key,
            defaultPrevented: false,
            propagationStopped: false,
            preventDefault: function () { this.defaultPrevented = true; },
            stopPropagation: function () { this.propagationStopped = true; }
        };
        (this.listeners.keydown || []).slice().forEach(listener => listener(event));
        return event;
    },
    dispatchEvent: function (type) {
        const event = {
            defaultPrevented: false,
            propagationStopped: false,
            preventDefault: function () { this.defaultPrevented = true; },
            stopPropagation: function () { this.propagationStopped = true; }
        };
        (this.listeners[type] || []).slice().forEach(listener => listener(event));
        return event;
    },
    dispatchCommand: function (command, options) {
        options = options || {};
        const event = {
            detail: { command: command },
            cancelable: options.cancelable !== false,
            defaultPrevented: options.defaultPrevented === true,
            propagationStopped: false,
            preventDefault: function () {
                if (options.preventBehavior === "throw") throw new Error("injected prevent failure");
                if (options.preventBehavior !== "noop" && this.cancelable) this.defaultPrevented = true;
            },
            stopPropagation: function () { this.propagationStopped = true; }
        };
        if (options.omitPreventDefault) delete event.preventDefault;
        (this.captureListeners.command || []).slice().forEach(listener => listener(event));
        if (!event.propagationStopped) {
            (this.listeners.command || []).slice().forEach(listener => listener(event));
        }
        return event;
    },
    createElement: tag => new FakeElement(tag),
    getElementById: function (id) {
        const all = this.body.querySelectorAll("." + id);
        return all[0] || null;
    },
    querySelectorAll: function (selector) {
        return selector === ".actionSheet.opened" ? this.openedMenus : [];
    }
};

const apiCalls = [];
const apiResponses = {};
const windowListeners = {};
const historyEntries = [{ state: null, url: "http://emby.test/web/index.html#!/item?id=series" }];
const historyCalls = { pushState: 0, replaceState: 0, back: 0 };
const historyStub = {
    state: null,
    pushState: function (state, _title, url) {
        historyCalls.pushState++;
        historyEntries.push({ state: state, url: url });
        this.state = state;
    },
    replaceState: function (state, _title, url) {
        historyCalls.replaceState++;
        historyEntries[historyEntries.length - 1] = { state: state, url: url };
        this.state = state;
    },
    back: function () {
        historyCalls.back++;
        if (historyEntries.length > 1) historyEntries.pop();
        const current = historyEntries[historyEntries.length - 1];
        this.state = current.state;
        (windowListeners.popstate || []).slice().forEach(listener => listener({ state: current.state }));
    }
};
const context = {
    window: {
        location: { hash: "", href: "http://emby.test/web/index.html#!/item?id=series" },
        navigator: { userAgentData: { platform: " Android " }, userAgent: "Fake Android WebView" },
        history: historyStub,
        addEventListener: function (type, listener) {
            (windowListeners[type] || (windowListeners[type] = [])).push(listener);
        },
        removeEventListener: function (type, listener) {
            const listeners = windowListeners[type] || [];
            const index = listeners.indexOf(listener);
            if (index >= 0) listeners.splice(index, 1);
        },
        setTimeout: function () { return 1; },
        clearTimeout: function () {}
    },
    document: documentStub,
    MutationObserver: function () { this.observe = function () {}; },
    console: { info: function () {}, debug: function () {}, error: function () {} },
    URLSearchParams: URLSearchParams,
    Date: Date,
    ApiClient: {
        getCurrentUserId: function () { return "user"; },
        getItem: async function (_user, id) { return { Id: id, Type: "Movie", Name: id }; },
        getUrl: function (url, query) { return { url: url, query: query }; },
        ajax: function (request) {
            const option = request.url.query.option;
            apiCalls.push({ option: option, itemId: request.url.url.split("/").pop(), parameters: request.url.query });
            const response = apiResponses[option];
            return typeof response === "function" ? response(request) : response;
        }
    }
};
context.window.window = context.window;
context.window.document = documentStub;
vm.createContext(context);
const scriptPath = path.join(__dirname, "DanmuSmartMatch.CustomCssJS.js");
const source = fs.readFileSync(scriptPath, "utf8");
vm.runInContext(source, context, { filename: scriptPath });
const hooks = context.window.__embyDanmuSmartMatchTest;

async function waitUntil(predicate, message) {
    for (let attempt = 0; attempt < 12; attempt++) {
        if (predicate()) return;
        await new Promise(resolve => setImmediate(resolve));
    }
    throw new Error(message);
}

function allVisibleText(node) {
    return [node.textContent || ""].concat((node.children || []).map(allVisibleText)).join(" ");
}

function fakeResponse(status, statusText, body, contentType) {
    let reads = 0;
    return {
        status: status,
        statusText: statusText || "",
        ok: status >= 200 && status < 300,
        headers: { get: function () { return contentType || "application/json"; } },
        text: async function () { reads++; return body; },
        readCount: function () { return reads; }
    };
}

async function main() {
    assert((source.match(/__embyDanmuSmartMenuV30/g) || []).length === 1 &&
        !source.includes("__embyDanmuSmartMenuV29") && !source.includes("CarHistoryProbe") &&
        !source.includes("CarBackChannelProbe") && !source.includes("CarCommandTraceProbe") &&
        !source.includes("CarCommandOwnerProbe") && !source.includes("__embyDanmuHistoryModeOverride"),
        "the formal frontend flag must be V30 exactly once with every diagnostic probe excluded");
    assert(!source.includes("MAPPING_PROTOCOL_GENERATION") && source.includes("var MAPPING_PROTOCOL_VERSION = 22"),
        "the sparse-alignment UI must use the backend numeric V22 mapping protocol and server-authored plan generation");
    const compositeFailure = "复合季映射需要重新确认：Selected candidate evidence expired or belongs to another Season.";
    assert(hooks.authoritativeCompositeFailureMessage({ Message: compositeFailure,
            DecisionReason: "hidden-fallback" }) === compositeFailure &&
        hooks.authoritativeCompositeFailureMessage({ Message: "", DecisionReason: "stale-protocol-generation",
            SearchErrors: ["private-provider-diagnostic"], SelectionEvidenceToken: "private-evidence" }) ===
            "服务器拒绝复合季映射：stale-protocol-generation" &&
        hooks.authoritativeCompositeFailureMessage(null) === "服务器没有返回权威复合季映射" &&
        hooks.authoritativeCompositeFailureMessage({ Message: "x".repeat(1200) }).length <= 800 &&
        hooks.authoritativeCompositeFailureMessage({ Message: "x".repeat(1200) }).endsWith("…") &&
        source.includes("throw new Error(authoritativeCompositeFailureMessage(confirmed));"),
        "authoritative composite failures must expose only the bounded public server reason and retain a generic fallback");
    const compositeMappingHint = "下列卡片仅用于本次下载映射，不会改变Emby 的季归属。";
    assert(!source.includes("该季包含多个来源或存在未识别区间；") &&
        (source.match(new RegExp(compositeMappingHint, "g")) || []).length === 1 &&
        !source.includes("不会改变 Emby") &&
        source.includes('hint.textContent = "' + compositeMappingHint + '";'),
        "the composite mapping hint must retain only the exact l1 copy, without the removed clause, extra Emby spacing, or altered punctuation");
    const decodedResponse = fakeResponse(200, "OK", '{"Seasons":[{"SeasonId":"s1"}]}');
    const decodedJson = await hooks.decodeApiResult(decodedResponse);
    assert(decodedJson.Seasons[0].SeasonId === "s1" && decodedResponse.readCount() === 1,
        "the asynchronous decoder must consume a successful Fetch Response body exactly once");
    assert((await hooks.decodeApiResult('{"Message":"decoded string"}')).Message === "decoded string" &&
        (await hooks.decodeApiResult({ Message: "decoded object" })).Message === "decoded object" &&
        (await hooks.decodeApiResult({ data: '{"Message":"jquery data"}' })).Message === "jquery data",
        "decoded objects, JSON strings, and compatible jQuery data shapes must share the decoder");
    const largeJson = JSON.stringify({ Seasons: [{ Payload: "x".repeat(2000) }] });
    assert((await hooks.decodeApiResult(fakeResponse(200, "OK", largeJson))).Seasons[0].Payload.length === 2000,
        "bounded plain-text diagnostics must not truncate a valid large whole-Series JSON response");
    const errorCases = [
        [fakeResponse(422, "Unprocessable", '{"code":"plan-invalid","message":"mapping rejected","retryable":false}'), "http", "plan-invalid", "mapping rejected"],
        [fakeResponse(503, "Unavailable", "provider temporarily unavailable", "text/plain"), "http", "server-error", "provider temporarily unavailable"],
        [fakeResponse(500, "Internal Server Error", ""), "http", "server-error", "HTTP 500 Internal Server Error"]
    ];
    for (const entry of errorCases) {
        let caught;
        try { await hooks.decodeApiResult(entry[0]); } catch (error) { caught = error; }
        assert(caught && caught.category === entry[1] && caught.code === entry[2] &&
            caught.message.includes(entry[3]) && !caught.message.includes("[object Response]") && entry[0].readCount() === 1,
            "HTTP JSON/text/empty errors must be structured, bounded, and never stringify a Response");
    }
    const networkError = await hooks.normalizeRejectedApiError(new Error("connection refused"));
    const timeoutError = await hooks.normalizeRejectedApiError({ statusText: "timeout" });
    const cancelError = await hooks.normalizeRejectedApiError({ name: "AbortError" });
    assert(networkError.category === "network" && timeoutError.category === "timeout" &&
        cancelError.category === "cancelled" && !cancelError.retryable,
        "network, timeout, and explicit cancellation must remain distinct normalized categories");
    const partialSeriesDialog = hooks.openDialog("partial Series");
    apiResponses.MatchPreview = fakeResponse(503, "Unavailable", JSON.stringify({
        code: "season-search-partial", message: "one sibling failed", retryable: true,
        Seasons: [{ SeasonId: "completed-season", SeasonName: "Season 1", SeasonNumber: 1, EpisodeCount: 12,
            Status: "matched", MatchOrigin: "scored", DecisionReason: "partial-confident" }]
    }));
    const partialCallStart = apiCalls.length;
    await hooks.runSmartDownload({ Id: "series-partial", Type: "Series", Name: "Series" }, partialSeriesDialog);
    const partialCall = apiCalls.slice(partialCallStart).find(call => call.option === "MatchPreview");
    assert(allVisibleText(partialSeriesDialog.body).includes("Season 1") &&
        !allVisibleText(partialSeriesDialog.body).includes("[object Response]") &&
        partialCall.parameters.mappingProtocolVersion === 22 &&
        partialCall.parameters.mappingProtocolGeneration === undefined,
        "a whole-Series partial HTTP failure must retain completed sibling Seasons and every API call must carry the V22 fence");
    partialSeriesDialog.forceClose();
    delete apiResponses.MatchPreview;

    const recoveredSeriesSeason = {
        SeriesId: "series-empty-recovered", SeasonId: "season-empty-recovered",
        SeriesName: "Recovered Series", SeasonName: "Recovered Season 1",
        SeasonNumber: 1, EpisodeCount: 12, Status: "not-matched", Candidates: []
    };
    const emptyThenValidDialog = hooks.openDialog("empty Series preview recovery");
    let emptyThenValidResponses = 0;
    apiResponses.MatchPreview = function () {
        emptyThenValidResponses++;
        return emptyThenValidResponses === 1 ? { Seasons: [] } : { Seasons: [recoveredSeriesSeason] };
    };
    const emptyThenValidCallStart = apiCalls.length;
    await hooks.runSmartDownload({ Id: "series-empty-recovered", Type: "Series", Name: "Recovered Series" },
        emptyThenValidDialog);
    const emptyThenValidCalls = apiCalls.slice(emptyThenValidCallStart)
        .filter(call => call.option === "MatchPreview");
    const emptyThenValidText = allVisibleText(emptyThenValidDialog.body);
    assert(emptyThenValidCalls.length === 2 &&
        emptyThenValidCalls[0].parameters.searchOperationId !==
            emptyThenValidCalls[1].parameters.searchOperationId &&
        emptyThenValidDialog.body.querySelectorAll(".danmuSeasonSummary").length === 1 &&
        emptyThenValidText.includes("Recovered Season 1") &&
        !emptyThenValidText.includes("未命名剧集") && !emptyThenValidText.includes("返回 0 季"),
        "a decoded empty whole-Series preview must retry exactly once with a fresh operation and render only the valid response");
    emptyThenValidDialog.forceClose();

    const twiceEmptyDialog = hooks.openDialog("twice empty Series preview");
    apiResponses.MatchPreview = { Message: "服务器仍在准备季度列表", Seasons: [] };
    const twiceEmptyCallStart = apiCalls.length;
    await hooks.runSmartDownload({ Id: "series-twice-empty", Type: "Series", Name: "Twice Empty" },
        twiceEmptyDialog);
    const twiceEmptyCalls = apiCalls.slice(twiceEmptyCallStart)
        .filter(call => call.option === "MatchPreview");
    const twiceEmptyText = allVisibleText(twiceEmptyDialog.body);
    assert(twiceEmptyCalls.length === 2 && twiceEmptyText.includes("服务器仍在准备季度列表") &&
        !twiceEmptyText.includes("未命名剧集") && !twiceEmptyText.includes("返回 0 季") &&
        twiceEmptyDialog.body.querySelectorAll(".danmuSeasonSummary").length === 0 &&
        twiceEmptyDialog.footer.children.length === 1 &&
        twiceEmptyDialog.footer.children[0].textContent === "重试搜索" &&
        !allVisibleText(twiceEmptyDialog.footer).includes("下载"),
        "two decoded empty whole-Series previews must stop after two requests and expose only the existing retryable failure UI");

    let manualRetryResponses = 0;
    apiResponses.MatchPreview = function () {
        manualRetryResponses++;
        return manualRetryResponses === 1 ? { Seasons: [] } : { Seasons: [recoveredSeriesSeason] };
    };
    const manualRetryCallStart = apiCalls.length;
    await twiceEmptyDialog.footer.children[0].dispatch("click");
    await waitUntil(() => apiCalls.slice(manualRetryCallStart)
        .filter(call => call.option === "MatchPreview").length === 2 &&
        twiceEmptyDialog.body.querySelectorAll(".danmuSeasonSummary").length === 1,
        "manual retry did not complete a fresh bounded empty-Series cycle");
    const manualRetryCalls = apiCalls.slice(manualRetryCallStart)
        .filter(call => call.option === "MatchPreview");
    assert(manualRetryCalls.length === 2 && manualRetryResponses === 2 &&
        manualRetryCalls[0].parameters.searchOperationId !== manualRetryCalls[1].parameters.searchOperationId &&
        allVisibleText(twiceEmptyDialog.body).includes("Recovered Season 1"),
        "each manual retry must reset the one-retry allowance without exceeding two whole-Series requests");
    twiceEmptyDialog.forceClose();
    delete apiResponses.MatchPreview;
    /* r2 intentionally hid all scores; r3 replaces that contract below.
    assert(!/\bScore\b|综合评分|评分：/.test(source),
        "the frontend must not calculate or display candidate scores");
    assert(!/textContent\s*=\s*["'](?:取消|关闭)["']/.test(source),
    */
    assert(hooks.matchScoreLine({ MatchScore: 0.934, ScoreOrigin: "search-confidence" }) ===
        "匹配分：93.4（标题匹配）",
        "searched candidates must display the server-authored score and provenance");
    assert(hooks.matchScoreLine({ MatchScore: 1, ScoreOrigin: "exact-episode-id" }) ===
        "匹配分：100（精确标识符）",
        "exact evidence must display identifier provenance, not title similarity");
    assert(hooks.matchScoreLine({ MatchScore: 1, ScoreOrigin: "exact-binding" }) ===
        "匹配分：100（精确标识符）" &&
        hooks.matchScoreLine({ MatchScore: 1, ScoreOrigin: "verified-binding" }) ===
        "匹配分：100（精确标识符）",
        "exact bindings must use the closed provenance label while legacy persisted values remain readable");
    assert(hooks.matchScoreLine({}) === "" && hooks.matchScoreLine(null) === "" &&
        hooks.matchScoreLine({ MatchScore: null, Score: null }) === "" &&
        hooks.matchScoreLine({ MatchScore: "   " }) === "" &&
        hooks.matchScoreLine({ MatchScore: "not-a-number" }) === "",
        "missing, blank, null, and invalid score fields must never be coerced into a visible zero");
    /* Superseded encoding-damaged assertion retained inert below.
    assert(hooks.matchScoreLine({ MatchScore: 0 }) === "鍖归厤鍒嗭細0锛堟湇鍔＄璇勫垎锛? &&
        hooks.matchScoreLine({ Score: "0", ScoreOrigin: "search-confidence" }) ===
            "鍖归厤鍒嗭細0锛堟爣棰樺尮閰嶏級",
        "an explicit finite server score of zero must remain displayable for a verified result");
    */
    assert(hooks.matchScoreLine({ MatchScore: 0 }) ===
            "\u5339\u914d\u5206\uff1a0\uff08\u670d\u52a1\u7aef\u8bc4\u5206\uff09" &&
        hooks.matchScoreLine({ Score: "0", ScoreOrigin: "search-confidence" }) ===
            "\u5339\u914d\u5206\uff1a0\uff08\u6807\u9898\u5339\u914d\uff09",
        "an explicit finite server score of zero must remain displayable for a verified result");
    assert(source.includes("matchScoreLine(candidate)") && source.includes("matchScoreLine(group.mappings[0])"),
        "candidate rows and confirmed mapping summaries must retain visible server scores");
    assert(!/textContent\s*=\s*["'](?:鍙栨秷|鍏抽棴)["']/.test(source),
        "smart-match footers must expose only the top-right close button and Escape for ordinary dismissal");
    assert(source.includes("env(safe-area-inset-top,0px)") &&
        source.includes(".danmuSmartHeader{padding-top:calc(1.75rem"),
        "the mobile header and close button must stay below the Android status-bar safe area");
    const rematch = hooks.rematchParameters({ keyword: "example" });
    assert(rematch.mode === "rematch" && rematch.rematch === "true" && rematch.force === "true" && rematch.keyword === "example",
        "a deliberate rematch should send explicit r6 mode/rematch and legacy force semantics");
    const automaticInput = new FakeElement("input");
    const automaticButton = new FakeElement("button");
    automaticInput.value = "爱书的下克上";
    hooks.initializeKeywordIntent(automaticInput, automaticButton, false);
    const automaticRematch = hooks.keywordRematchParameters({ seasonNumber: 4 }, automaticInput);
    assert(automaticButton.textContent === "重新智能匹配" &&
        automaticRematch.mode === "rematch" && automaticRematch.seasonNumber === 4 &&
        !Object.prototype.hasOwnProperty.call(automaticRematch, "keyword"),
        "an untouched default title must omit keyword so the backend can run alias discovery");
    automaticInput.value = "小书痴的下克上";
    await automaticInput.dispatch("input");
    const explicitRematch = hooks.keywordRematchParameters({}, automaticInput);
    assert(automaticButton.textContent === "按关键词搜索" &&
        explicitRematch.mode === "manual-keyword" && explicitRematch.keyword === "小书痴的下克上",
        "editing the input must switch to the isolated manual-keyword contract");
    const manualKeywordInput = " \t进击！ A+B  テスト\u3000 ";
    const trimmedManualKeyword = "进击！ A+B  テスト";
    ["Movie", "Episode-via-Season", "Season", "Series-per-Season"].forEach(entry => {
        const input = new FakeElement("input");
        const button = new FakeElement("button");
        input.value = "prefill";
        hooks.initializeKeywordIntent(input, button, false);
        input.value = manualKeywordInput;
        input.listeners.input[0]();
        const parameters = hooks.keywordRematchParameters({ entry: entry }, input);
        assert(parameters.mode === "manual-keyword" && parameters.keyword === trimmedManualKeyword &&
            parameters.entry === entry && parameters.rematch === "true" && parameters.force === "true",
            entry + " must trim outer whitespace while preserving internal spaces, punctuation, literal plus, and non-ASCII text");
    });
    const whitespaceInput = new FakeElement("input");
    whitespaceInput.value = " \t\u3000 ";
    whitespaceInput.dataset.danmuExplicitKeyword = "true";
    assert(hooks.manualKeywordParameters({}, whitespaceInput) === null &&
        hooks.keywordRematchParameters({}, whitespaceInput) === null,
        "manual-keyword parameters must reject whitespace-only explicit input instead of falling back to rematch");
    const retiredIntent = "manual-" + ["r", "a", "w"].join("");
    assert(hooks.isManualKeyword({ MatchIntent: "manual-keyword" }) &&
        !hooks.isManualKeyword({ MatchIntent: retiredIntent }) &&
        !hooks.isManualKeyword({ MatchIntent: "future-keyword" }) &&
        !hooks.isManualKeyword({ mode: "manual-keyword" }),
        "only the server-authored exact manual-keyword MatchIntent may activate isolated presentation");
    const defaultCancelledDiagnostic = hooks.searchDiagnosticsLine({
        SearchCompletionDiagnostics: [{ Provider: "DefaultCancelled", Status: "cancelled" }]
    });
    const manualKeywordCancelledDiagnostic = hooks.searchDiagnosticsLine({
        MatchIntent: "manual-keyword",
        SearchCompletionDiagnostics: [{ Provider: "ManualKeywordCancelled", Status: "cancelled" }]
    });
    assert(defaultCancelledDiagnostic === "搜索诊断：DefaultCancelled 已取消" && manualKeywordCancelledDiagnostic === "",
        "cancelled diagnostics must retain normal-mode presentation and be hidden only for manual-keyword results");
    const rangeInput = new FakeElement("input");
    rangeInput.value = manualKeywordInput;
    rangeInput.dataset.danmuExplicitKeyword = "true";
    const manualKeywordRangeParameters = hooks.temporaryRangeSearchParameters(
        {}, { Id: "series-manual-keyword", Type: "Series" },
        { SeriesId: "series-manual-keyword", SeasonId: "season-manual-keyword", SeasonNumber: 1 },
        { episodes: [{ ItemId: "episode-manual-keyword" }] }, {}, manualKeywordInput, rangeInput);
    assert(manualKeywordRangeParameters.mode === "manual-keyword" &&
        manualKeywordRangeParameters.keyword === trimmedManualKeyword &&
        manualKeywordRangeParameters.searchScope === "temporary-range",
        "an edited temporary-range keyword must trim outer whitespace, preserve its content, and retain its authoritative range scope");
    const origins = {
        " provider-id ": "本地外部标识符",
        "EXTERNAL_ID": "本地外部标识符",
        "binding": "已保存绑定",
        "saved-binding": "已保存绑定",
        "SCORED": "智能评分匹配",
        "manual": "手动选择",
        "manual_selection": "手动选择"
    };
    Object.keys(origins).forEach(code => {
        assert(hooks.matchOriginLabel({ MatchOrigin: code }) === origins[code],
            "known origin " + code + " should use its Chinese label");
    });
    const reasons = {
        " provider-id ": "使用本地外部标识符",
        "binding": "使用已保存绑定",
        "SAVED_BINDING": "使用已保存绑定",
        "confident-site-priority": "按站点优先级自动选择",
        "unresolved_provider": "本地标识符无法解析",
        "provider-id-unresolved": "本地标识符无法解析",
        "no-candidate": "未找到候选",
        "no-candidates": "未找到候选",
        "LOW_CONFIDENCE": "置信度不足，需手动选择",
        "manual": "手动选择",
        "manual-selection": "手动选择"
    };
    Object.keys(reasons).forEach(code => {
        assert(hooks.decisionReasonLabel({ DecisionReason: code }) === reasons[code],
            "known decision reason " + code + " should use its Chinese label");
    });
    const providerIdMatch = { MatchOrigin: "provider-id", DecisionReason: "provider-id" };
    assert(hooks.hasBackendMatch(providerIdMatch) && hooks.matchOriginLabel(providerIdMatch) === "本地外部标识符" &&
        hooks.backendDecisionLine(providerIdMatch) === "来源：本地外部标识符　决策：使用本地外部标识符",
        "provider-id results should retain r6 recognition while displaying Chinese labels");
    assert(hooks.backendDecisionLine({}) === "", "empty explanations should be omitted");
    const unknownLine = hooks.backendDecisionLine({ MatchOrigin: " Future-Origin ", DecisionReason: "future_reason" });
    assert(unknownLine.includes("未知匹配来源") && unknownLine.includes("未知决策") &&
        unknownLine.includes("诊断代码：Future-Origin") && unknownLine.includes("诊断代码：future_reason"),
        "unknown values should use Chinese primary fallbacks and retain raw codes only as diagnostics");
    assert(hooks.normalizeDecisionCode(" Provider_ID ") === "provider-id",
        "normalization should trim, case-normalize, and normalize separators");
    assert(!hooks.hasBackendMatch({ MatchOrigin: "provider-id", Status: "ambiguous" }),
        "an unresolved source-episode choice must not be displayed as a successful match");

    const manualKeywordCandidates = Array.from({ length: 65 }, (_unused, index) => ({
        Site: index < 2 ? "Dandan" : (index < 40 ? "Bilibili" : "Youku"),
        SiteName: index < 2 ? "弹弹Play" : (index < 40 ? "哔哩哔哩" : "优酷"),
        Id: index < 2 ? "duplicate-id" : "manual-keyword-" + index,
        Name: index < 2 ? "Exact Looking Duplicate" : "Provider Row " + index,
        Year: 2026, EpisodeSize: 12, Category: "Anime",
        Score: index === 0 ? 0.42 : (index === 1 ? 0.97 : (index === 2 ? 0.63 : 0.8)),
        MatchScore: index === 2 ? null :
            (index === 0 ? 0.42 : (index === 1 ? 0.97 : 0.8)),
        ScoreOrigin: "search-confidence", Reason: "Server score reason " + index,
        MatchOrigin: "manual-keyword-backend-origin-" + index,
        DecisionReason: "manual-keyword-backend-decision-" + index,
        SelectionEvidenceToken: "evidence-" + index
    }));
    const manualKeywordTarget = {
        MatchIntent: "manual-keyword", SelectedSite: "Dandan", SelectedId: "duplicate-id",
        MatchOrigin: "scored", DecisionReason: "confident-site-priority",
        Candidates: manualKeywordCandidates,
        SearchCompletionDiagnostics: [
            { Provider: "Bilibili", Status: "completed" },
            { Provider: "BrokenSite", Status: "failed" },
            { Provider: "CancelledSibling", Status: "cancelled" }
        ]
    };
    const manualKeywordDialog = hooks.openDialog("manual keyword rows");
    hooks.renderItemCandidatePicker(manualKeywordDialog,
        { Id: "manual-keyword-episode", Type: "Episode", Name: "Exact Looking Duplicate", SeriesName: "Manual Keyword" },
        manualKeywordTarget, manualKeywordInput);
    const manualKeywordRows = manualKeywordDialog.body.querySelectorAll(".danmuCandidate");
    assert(manualKeywordRows.length === 65 &&
        allVisibleText(manualKeywordRows[0]).includes("Exact Looking Duplicate") &&
        allVisibleText(manualKeywordRows[1]).includes("Exact Looking Duplicate") &&
        allVisibleText(manualKeywordRows[2]).includes("Provider Row 2") &&
        allVisibleText(manualKeywordRows[0]).includes("匹配分：42（标题匹配）") &&
        allVisibleText(manualKeywordRows[1]).includes("匹配分：97（标题匹配）") &&
        allVisibleText(manualKeywordRows[2]).includes("匹配分：63（标题匹配）") &&
        manualKeywordRows.every(row => !row.children[0].checked),
        "manual-keyword rendering must trust backend order despite score values, preserve duplicates and more than sixty rows, and avoid preselection");
    const manualKeywordVisibleText = allVisibleText(manualKeywordDialog.body);
    assert(manualKeywordRows.every((row, index) => allVisibleText(row).includes("评分理由：Server score reason " + index) &&
            !allVisibleText(row).includes("来源：") && !allVisibleText(row).includes("决策：")) &&
        manualKeywordVisibleText.includes("BrokenSite 失败") &&
        !manualKeywordVisibleText.includes("CancelledSibling") &&
        !manualKeywordVisibleText.includes("manual-keyword-backend-origin-") &&
        !manualKeywordVisibleText.includes("manual-keyword-backend-decision-"),
        "manual-keyword rows must show server score/reason fields while retaining provider faults and isolating automatic origin/decision fields");
    const manualKeywordMovieDialog = hooks.openDialog("manual keyword Movie heading");
    hooks.renderItemCandidatePicker(manualKeywordMovieDialog,
        { Id: "manual-keyword-movie", Type: "Movie", Name: "Local Movie" },
        { MatchIntent: "manual-keyword", Candidates: [{
            Site: "Bilibili", SiteName: "哔哩哔哩", Id: "manual-keyword-movie-candidate",
            Name: "Provider Movie Title", Score: 0.64, ScoreOrigin: "search-confidence",
            Reason: "Movie server score reason", MatchOrigin: "movie-automatic-origin",
            DecisionReason: "movie-automatic-decision", SelectionEvidenceToken: "manual-keyword-movie-evidence"
        }] }, manualKeywordInput);
    const manualKeywordMovieTitle = manualKeywordMovieDialog.body.querySelector(".danmuCandidateTitle");
    const manualKeywordMovieText = allVisibleText(manualKeywordMovieDialog.body);
    assert(manualKeywordMovieTitle && manualKeywordMovieTitle.textContent === "哔哩哔哩 · Provider Movie Title" &&
        manualKeywordMovieText.includes("匹配分：64（标题匹配）") &&
        manualKeywordMovieText.includes("评分理由：Movie server score reason") &&
        !manualKeywordMovieDialog.body.querySelector(".danmuCandidate").children[0].checked &&
        !manualKeywordMovieText.includes("来源：") && !manualKeywordMovieText.includes("决策：") &&
        !manualKeywordMovieText.includes("movie-automatic-origin") &&
        !manualKeywordMovieText.includes("movie-automatic-decision"),
        "a manual-keyword Movie row without SourceMetadata must show its public name, server score/reason, no automatic decision, and no preselection");
    manualKeywordMovieDialog.forceClose();
    apiResponses.MatchCandidateDetails = {
        Success: true, SourceEpisodes: [{ Id: "trusted-source", Number: 1, Title: "Trusted detail" }]
    };
    const manualKeywordDetailStart = apiCalls.length;
    await manualKeywordRows[0].querySelector(".danmuCandidateDetailAction").dispatch("click");
    await manualKeywordRows[1].querySelector(".danmuCandidateDetailAction").dispatch("click");
    const manualKeywordDetailCalls = apiCalls.slice(manualKeywordDetailStart)
        .filter(call => call.option === "MatchCandidateDetails");
    assert(manualKeywordDetailCalls.length === 2 &&
        manualKeywordDetailCalls.every(call => call.parameters.candidateId === "duplicate-id") &&
        manualKeywordDetailCalls.map(call => call.parameters.candidateEvidence).join(",") === "evidence-0,evidence-1" &&
        allVisibleText(manualKeywordRows[0]).includes("Trusted detail") &&
        allVisibleText(manualKeywordRows[1]).includes("Trusted detail"),
        "duplicate provider/id rows must keep evidence-token-scoped detail state and issue separate trusted requests");
    apiResponses.GetSelectedCandidatePreview = {
        Status: "ready", Episodes: [{ Id: "trusted-source", Number: 1, Title: "Trusted detail" }]
    };
    manualKeywordRows[1].children[0].checked = true;
    const trustedSelectionStart = apiCalls.length;
    const manualKeywordStart = manualKeywordDialog.footer.children.find(button => button.textContent === "解析所选候选的来源剧集");
    await manualKeywordStart.dispatch("click");
    await waitUntil(() => apiCalls.slice(trustedSelectionStart).some(call => call.option === "GetSelectedCandidatePreview"),
        "explicit manual-keyword selection should enter trusted detail resolution");
    const trustedSelectionCall = apiCalls.slice(trustedSelectionStart)
        .find(call => call.option === "GetSelectedCandidatePreview");
    assert(trustedSelectionCall.parameters.candidateId === "duplicate-id" &&
        trustedSelectionCall.parameters.selectionEvidenceToken === "evidence-1",
        "explicit manual-keyword selection must reuse the existing evidence-validated selection hook");
    await waitUntil(() => manualKeywordDialog.body.querySelectorAll(".danmuSourceEpisodeChoice").length === 1,
        "trusted manual-keyword Episode detail should enter the existing source Episode picker");
    const manualKeywordSourcePickerText = allVisibleText(manualKeywordDialog.body);
    assert(manualKeywordSourcePickerText.includes("匹配分：97（标题匹配）") &&
        manualKeywordSourcePickerText.includes("评分理由：Server score reason 1") &&
        !manualKeywordSourcePickerText.includes("来源：") && !manualKeywordSourcePickerText.includes("决策：") &&
        !manualKeywordSourcePickerText.includes("manual-keyword-backend-origin-1") &&
        !manualKeywordSourcePickerText.includes("manual-keyword-backend-decision-1"),
        "the manual-keyword Episode source picker must retain server score/reason presentation without reading automatic decisions");
    delete apiResponses.MatchCandidateDetails;
    delete apiResponses.GetSelectedCandidatePreview;

    apiResponses.StartTrackedDownload = {
        TaskId: "manual-keyword-progress", Status: "completed", Message: "done", Succeeded: 1,
        Episodes: [{ ItemId: "manual-keyword-progress-item", EpisodeNumber: 1, Status: "success" }]
    };
    for (const itemType of ["Episode", "Movie"]) {
        const progressDialog = {
            body: new FakeElement("div"), footer: new FakeElement("div"),
            overlay: { isConnected: false }, closable: false, forceRefresh: false,
            close: function () {}, forceClose: function () {}, setBackHandler: function () {}
        };
        await hooks.renderSingleTargetProgress(progressDialog,
            { Id: "manual-keyword-progress-" + itemType, Type: itemType, Name: "Manual keyword progress " + itemType },
            Object.assign({ MatchIntent: "manual-keyword" }, manualKeywordTarget), manualKeywordCandidates[0],
            itemType === "Episode" ? 1 : null, itemType === "Episode" ? "trusted-source" : null, true);
        const progressText = allVisibleText(progressDialog.body);
        assert(progressText.includes("匹配分：42（标题匹配）") &&
            progressText.includes("评分理由：Server score reason 0") &&
            !progressText.includes("来源：") && !progressText.includes("决策：") &&
            !progressText.includes("manual-keyword-backend-origin-0") &&
            !progressText.includes("manual-keyword-backend-decision-0"),
            "manual-keyword " + itemType + " progress must retain server score/reason without reading automatic decisions");
    }
    delete apiResponses.StartTrackedDownload;

    const manualKeywordSeriesSeason = {
        SeriesId: "manual-keyword-series", SeasonId: "manual-keyword-series-season", SeasonNumber: 1,
        SeasonName: "Manual Keyword Series Season", MatchIntent: "manual-keyword",
        Candidates: manualKeywordCandidates.slice(0, 2),
        MatchOrigin: "scored", DecisionReason: "confident-site-priority",
        Message: "Choose a candidate to continue.",
        SearchCompletionDiagnostics: [
            { Provider: "SeriesBroken", Status: "failed" },
            { Provider: "SeriesCancelled", Status: "cancelled" }
        ]
    };
    const manualKeywordSeriesDialog = hooks.openDialog("manual keyword Series per Season");
    hooks.renderSeriesSeasonPicker(manualKeywordSeriesDialog,
        { Id: "manual-keyword-series", Type: "Series", Name: "Manual Keyword Series" },
        [manualKeywordSeriesSeason], 0, {}, {});
    const manualKeywordSeriesRows = manualKeywordSeriesDialog.body.querySelectorAll(".danmuCandidate");
    const manualKeywordSeriesText = allVisibleText(manualKeywordSeriesDialog.body);
    assert(manualKeywordSeriesRows.length === 2 && manualKeywordSeriesRows.every(row => !row.children[0].checked) &&
        allVisibleText(manualKeywordSeriesRows[0]).includes("匹配分：42（标题匹配）") &&
        allVisibleText(manualKeywordSeriesRows[1]).includes("匹配分：97（标题匹配）") &&
        allVisibleText(manualKeywordSeriesRows[0]).includes("评分理由：Server score reason 0") &&
        allVisibleText(manualKeywordSeriesRows[1]).includes("评分理由：Server score reason 1") &&
        manualKeywordSeriesText.includes("SeriesBroken 失败") && !manualKeywordSeriesText.includes("SeriesCancelled") &&
        !manualKeywordSeriesText.includes("来源：") && !manualKeywordSeriesText.includes("决策：") &&
        !manualKeywordSeriesText.includes("manual-keyword-backend-origin-") &&
        !manualKeywordSeriesText.includes("manual-keyword-backend-decision-"),
        "Series-per-season manual-keyword rendering must preserve backend row order and score/reason fields without automatic decisions or preselection");
    manualKeywordSeriesDialog.forceClose();

    const manualKeywordSeasonDialog = hooks.openDialog("manual keyword standalone Season");
    hooks.renderCandidatePicker(manualKeywordSeasonDialog,
        { Id: "manual-keyword-season", Type: "Season", Name: "Manual Keyword Season" },
        manualKeywordSeriesSeason, manualKeywordInput);
    const manualKeywordSeasonRows = manualKeywordSeasonDialog.body.querySelectorAll(".danmuCandidate");
    const manualKeywordSeasonText = allVisibleText(manualKeywordSeasonDialog.body);
    assert(manualKeywordSeasonRows.length === 2 && manualKeywordSeasonRows.every(row => !row.children[0].checked) &&
        allVisibleText(manualKeywordSeasonRows[0]).includes("匹配分：42（标题匹配）") &&
        allVisibleText(manualKeywordSeasonRows[1]).includes("匹配分：97（标题匹配）") &&
        allVisibleText(manualKeywordSeasonRows[0]).includes("评分理由：Server score reason 0") &&
        allVisibleText(manualKeywordSeasonRows[1]).includes("评分理由：Server score reason 1") &&
        !manualKeywordSeasonText.includes("来源：") && !manualKeywordSeasonText.includes("决策：") &&
        !manualKeywordSeasonText.includes("manual-keyword-backend-origin-") &&
        !manualKeywordSeasonText.includes("manual-keyword-backend-decision-"),
        "standalone Season manual-keyword rows must retain backend order and score/reason fields without automatic decisions or preselection");
    manualKeywordSeasonDialog.forceClose();

    const manualKeywordSeriesOverviewDialog = hooks.openDialog("manual keyword Series overview");
    hooks.renderSeriesPicker(manualKeywordSeriesOverviewDialog,
        { Id: "manual-keyword-series", Type: "Series", Name: "Manual Keyword Series" },
        [manualKeywordSeriesSeason], {}, {});
    const manualKeywordSeriesOverviewText = allVisibleText(manualKeywordSeriesOverviewDialog.body);
    const manualKeywordSeriesOverviewState = manualKeywordSeriesOverviewDialog.body.querySelector(".danmuSeasonSummaryState");
    const manualKeywordSeriesOverviewAction = manualKeywordSeriesOverviewDialog.body
        .querySelectorAll(".danmuSmartButton")[0];
    assert(manualKeywordSeriesOverviewText.includes("SeriesBroken 失败") &&
        manualKeywordSeriesOverviewText.includes("Choose a candidate to continue.") &&
        manualKeywordSeriesOverviewState && manualKeywordSeriesOverviewState.textContent === "等待人工选择" &&
        manualKeywordSeriesOverviewAction && manualKeywordSeriesOverviewAction.textContent === "查看候选" &&
        !hooks.parentTitleRematchAvailable(manualKeywordSeriesSeason) &&
        !manualKeywordSeriesOverviewText.includes("✕ 匹配失败") &&
        !manualKeywordSeriesOverviewText.includes("SeriesCancelled") &&
        !manualKeywordSeriesOverviewText.includes("来源：") &&
        !manualKeywordSeriesOverviewText.includes("决策："),
        "Series overview manual-keyword cards must retain their candidate action and neutral state without entering parent-title rematch");
    const manualKeywordOverviewCallCount = apiCalls.length;
    await manualKeywordSeriesOverviewAction.dispatch("click");
    assert(apiCalls.length === manualKeywordOverviewCallCount &&
        manualKeywordSeriesOverviewDialog.body.querySelectorAll(".danmuCandidate").length === 2,
        "manual-keyword candidate viewing must remain local and independent from the l6 request path");
    manualKeywordSeriesOverviewDialog.forceClose();

    hooks.renderItemCandidatePicker(manualKeywordDialog,
        { Id: "default-episode", Type: "Episode", Name: "Default" },
        { SelectedSite: "Dandan", SelectedId: "default-id", Candidates: [{
            Site: "Dandan", Id: "default-id", Name: "Default candidate",
            MatchScore: 0.9, ScoreOrigin: "search-confidence"
        }] }, "");
    const defaultRows = manualKeywordDialog.body.querySelectorAll(".danmuCandidate");
    assert(defaultRows.length === 1 && defaultRows[0].children[0].checked &&
        allVisibleText(defaultRows[0]).includes("匹配分：90"),
        "rerendering a default result must replace, not inherit, manual-keyword presentation state");
    const whitespaceSearch = manualKeywordDialog.body.querySelector(".danmuSmartSearch");
    const whitespaceSearchInput = whitespaceSearch.children[0];
    const whitespaceSearchButton = whitespaceSearch.children[1];
    whitespaceSearchInput.value = "  \t\u3000  ";
    await whitespaceSearchInput.dispatch("input");
    const whitespaceCallCount = apiCalls.length;
    await whitespaceSearchButton.dispatch("click");
    assert(apiCalls.length === whitespaceCallCount,
        "whitespace-only explicit input must be rejected in the UI with zero provider requests");
    manualKeywordDialog.forceClose();

    assert(hooks.parentTitleRematchAvailable({ ParentTitleRematchAvailable: true }) &&
        !hooks.parentTitleRematchAvailable({ ParentTitleRematchAvailable: "true" }) &&
        !hooks.parentTitleRematchAvailable({ MatchIntent: "manual-keyword" }),
        "only the server-authored boolean l6 field may activate parent-title rematch");
    const exhaustedAliasCandidates = [0, 1].map(index => ({
        Site: "Dandan", SiteName: "弹弹Play", Id: "jojo-alias-duplicate",
        Name: "JOJO accumulated alias candidate " + index,
        MatchScore: 0.61, ScoreOrigin: "search-confidence",
        MatchOrigin: "tmdb-alias", DecisionReason: "alias-low-confidence"
    }));
    const exhaustedJojoSeason = {
        SeriesId: "jojo-series", SeasonId: "jojo-season-1", SeriesName: "JOJO的奇妙冒险",
        SeasonName: "JOJO Season 1", SeasonNumber: 1, Year: 2012, EpisodeCount: 26,
        MappingProtocolVersion: 22, PlanGeneration: 6106, PlanFingerprint: "jojo-authoritative-plan",
        ParentTitleRematchAvailable: true,
        Candidates: exhaustedAliasCandidates,
        SelectedSite: "Dandan", SelectedId: "jojo-alias-duplicate",
        SelectedCandidate: exhaustedAliasCandidates[0],
        MatchOrigin: "tmdb-alias", DecisionReason: "tmdb-alias-exhausted",
        Message: "TMDB aliases exhausted after trying JOJO alias values",
        SearchCompletionDiagnostics: [
            { Provider: "TMDB alias", Status: "failed" },
            { Provider: "Bilibili", Status: "failed" }
        ]
    };
    const jojoSelectionKey = "jojo-series::jojo-season-1";
    const jojoSelections = { __mappingContracts: {}, __compositeSelections: {} };
    jojoSelections.__mappingContracts[jojoSelectionKey] = "22:6106";
    jojoSelections[jojoSelectionKey] = exhaustedAliasCandidates[0];
    jojoSelections.__compositeSelections[jojoSelectionKey] = [{
        LocalStartEpisodeItemId: "stale-local-episode", RequestedEpisodeCount: 1,
        Source: { ProviderId: "Dandan", MediaId: "jojo-alias-duplicate" },
        SelectionEvidenceToken: "stale-alias-evidence"
    }];
    const jojoKeywords = {};
    jojoKeywords[jojoSelectionKey] = "stale manual keyword";
    const jojoSeasons = [exhaustedJojoSeason];
    const jojoDialog = hooks.openDialog("JOJO exhausted aliases");
    const jojoItem = { Id: "jojo-series", Type: "Series", Name: "browser JOJO fallback" };
    hooks.renderSeriesPicker(jojoDialog, jojoItem, jojoSeasons, jojoSelections, jojoKeywords);
    const exhaustedJojoText = allVisibleText(jojoDialog.body);
    const exhaustedJojoState = jojoDialog.body.querySelector(".danmuSeasonSummaryState");
    const exhaustedJojoActions = jojoDialog.body.querySelectorAll(".danmuSmartButton");
    assert(exhaustedJojoState && exhaustedJojoState.textContent === "✕ 匹配失败" &&
        exhaustedJojoActions.length === 1 && exhaustedJojoActions[0].textContent === "重新匹配" &&
        !exhaustedJojoText.includes("查看候选") &&
        !exhaustedJojoText.includes("JOJO accumulated alias candidate") &&
        !exhaustedJojoText.includes("JOJO alias values") &&
        !exhaustedJojoText.toLowerCase().includes("tmdb") &&
        exhaustedJojoText.includes("Bilibili 失败") &&
        !Object.prototype.hasOwnProperty.call(jojoSelections, jojoSelectionKey) &&
        !Object.prototype.hasOwnProperty.call(jojoSelections.__compositeSelections, jojoSelectionKey) &&
        !Object.prototype.hasOwnProperty.call(jojoKeywords, jojoSelectionKey),
        "JOJO alias exhaustion must be failed, hide accumulated aliases/TMDB diagnostics, retain unrelated faults, and expose only parent-title rematch");

    const parentTitleCandidates = [{
        Site: "Dandan", SiteName: "弹弹Play", Id: "jojo-parent-low", Name: "JOJO parent low",
        MatchScore: 0.72, ScoreOrigin: "search-confidence",
        MatchOrigin: "scored", DecisionReason: "low-confidence"
    }, {
        Site: "Bilibili", SiteName: "哔哩哔哩", Id: "jojo-parent-high", Name: "JOJO parent high",
        MatchScore: 0.93, ScoreOrigin: "search-confidence",
        MatchOrigin: "scored", DecisionReason: "confident-site-priority"
    }];
    const parentTitleSeason = {
        SeriesId: "jojo-series", SeasonId: "jojo-season-1", SeriesName: "JOJO的奇妙冒险",
        SeasonName: "JOJO Season 1", SeasonNumber: 1, Year: 2012, EpisodeCount: 26,
        MappingProtocolVersion: 22, PlanGeneration: 6206, PlanFingerprint: "jojo-parent-plan",
        ParentTitleRematchAvailable: false,
        Candidates: parentTitleCandidates,
        SelectedSite: "Bilibili", SelectedId: "jojo-parent-high",
        SelectedCandidate: parentTitleCandidates[1],
        MatchOrigin: "scored", DecisionReason: "confident-site-priority",
        SearchCompletionDiagnostics: [{ Provider: "Youku", Status: "failed" }]
    };
    apiResponses.MatchPreview = { Seasons: [parentTitleSeason] };
    const parentTitleCallStart = apiCalls.length;
    await exhaustedJojoActions[0].dispatch("click");
    const parentTitleCall = apiCalls.slice(parentTitleCallStart)
        .find(call => call.option === "MatchPreview");
    const forbiddenParentTitleParameters = [
        "keyword", "mode", "manual", "rematch", "force", "site", "candidateId",
        "candidateEvidence", "selectionEvidenceToken", "seriesName", "parentTitle"
    ];
    assert(parentTitleCall && parentTitleCall.itemId === "jojo-series" &&
        parentTitleCall.parameters.parentTitleRematch === true &&
        parentTitleCall.parameters.seriesId === "jojo-series" &&
        parentTitleCall.parameters.seasonName === "JOJO Season 1" &&
        parentTitleCall.parameters.seasonNumber === 1 && parentTitleCall.parameters.seasonYear === 2012 &&
        parentTitleCall.parameters.planGeneration === 6106 &&
        parentTitleCall.parameters.planFingerprint === "jojo-authoritative-plan" &&
        parentTitleCall.parameters.mappingProtocolVersion === 22 &&
        parentTitleCall.parameters.searchScope === "provider-search" &&
        forbiddenParentTitleParameters.every(name =>
            !Object.prototype.hasOwnProperty.call(parentTitleCall.parameters, name)),
        "parent-title rematch must send only its boolean discriminator plus authoritative Season/Series context and never l10 or candidate-selection fields");
    const parentTitlePickerText = allVisibleText(jojoDialog.body);
    const parentTitleRows = jojoDialog.body.querySelectorAll(".danmuCandidate");
    assert(jojoSeasons[0] === parentTitleSeason && parentTitleRows.length === 2 &&
        allVisibleText(parentTitleRows[0]).includes("匹配分：72（标题匹配）") &&
        allVisibleText(parentTitleRows[1]).includes("匹配分：93（标题匹配）") &&
        parentTitleRows[1].children[0].checked &&
        parentTitlePickerText.includes("Youku 失败") &&
        !parentTitlePickerText.includes("JOJO accumulated alias candidate") &&
        !jojoDialog.body.querySelectorAll(".danmuSmartButton")
            .some(button => button.textContent === "重新匹配"),
        "the fresh response must replace exhausted state and restore ordinary scored candidates, diagnostics, and normal selection");
    const parentTitleBack = jojoDialog.footer.children
        .find(button => button.textContent === "返回总览");
    await parentTitleBack.dispatch("click");
    const ordinaryJojoAction = jojoDialog.body.querySelectorAll(".danmuSmartButton")
        .find(button => button.textContent === "查看候选");
    assert(ordinaryJojoAction &&
        !jojoDialog.body.querySelectorAll(".danmuSmartButton")
            .some(button => button.textContent === "重新匹配") &&
        allVisibleText(jojoDialog.body).includes("Youku 失败"),
        "after replacement the overview must lose the exhausted action and restore the ordinary candidate path");
    const ordinaryJojoCallCount = apiCalls.length;
    await ordinaryJojoAction.dispatch("click");
    assert(apiCalls.length === ordinaryJojoCallCount &&
        jojoDialog.body.querySelectorAll(".danmuCandidate").length === 2,
        "ordinary 查看候选 must remain a local navigation path without issuing parent-title rematch");
    jojoDialog.forceClose();
    delete apiResponses.MatchPreview;

    const directJojoDialog = hooks.openDialog("direct Season exhausted aliases");
    const directJojoCallCount = apiCalls.length;
    hooks.renderCandidatePicker(directJojoDialog,
        { Id: "jojo-season-1", Type: "Season", Name: "JOJO Season 1" }, exhaustedJojoSeason, "");
    const directJojoText = allVisibleText(directJojoDialog.body);
    const directJojoActions = directJojoDialog.body.querySelectorAll(".danmuSmartButton");
    assert(directJojoDialog.title.textContent === "本季弹幕智能匹配" &&
        directJojoActions.length === 1 && directJojoActions[0].textContent === "重新匹配" &&
        directJojoDialog.body.querySelectorAll(".danmuCandidate").length === 0 &&
        directJojoText.includes("Bilibili 失败") && !directJojoText.toLowerCase().includes("tmdb") &&
        !directJojoText.includes("JOJO accumulated alias candidate") && apiCalls.length === directJojoCallCount,
        "a direct Season exhaustion response must use the same neutral l6 card without leaking alias rows or searching eagerly");
    directJojoDialog.forceClose();

    const compositeSeason = {
        SeriesId: "series", SeasonId: "season-composite", SeasonNumber: 1,
        SeasonName: "Composite", EpisodeCount: 5,
        MappingProtocolVersion: 22, PlanGeneration: 7341,
        CompositeSelections: [{
            LocalStartEpisodeItemId: "episode-1", RequestedEpisodeCount: 2,
            Site: "Dandan", CandidateId: "frieren-s1",
            SourceStartEpisodeId: "source-1", SourceStartEpisodeNumber: 1,
            MatchOrigin: "scored", SelectionEvidenceToken: "",
            AlignmentIntent: "DefaultZeroOffset", MappingProtocolVersion: 22,
            PlanGeneration: 7341
        }],
        CompositePlan: {
            OrderedEpisodes: [1, 2, 3, 4, 5].map(number => ({
                ItemId: "episode-" + number, EpisodeNumber: number, SortOrder: number,
                ParentSeasonNumber: 1,
                LocalDisplayLabel: "S01E" + String(number).padStart(2, "0")
            })),
            Mappings: [1, 2].map(number => ({
                LocalEpisodeItemId: "episode-" + number,
                Source: { ProviderId: "Dandan", MediaId: "frieren-s1" },
                SourceEpisodeId: "source-" + number, CommentId: "server-only-" + number,
                SourceEpisodeNumber: number, Origin: "scored",
                AlignmentIntent: "DefaultZeroOffset"
            })),
            UnmatchedRuns: [{ Episodes: [3, 4, 5].map(number => ({
                ItemId: "episode-" + number, EpisodeNumber: number, SortOrder: number,
                ParentSeasonNumber: 1,
                LocalDisplayLabel: "S01E" + String(number).padStart(2, "0")
            })) }]
        }
    };
    assert(hooks.hasCompositePlan(compositeSeason),
        "a server composite plan must activate the virtual-season UI");
    const compositeSelections = {};
    compositeSelections.__compositeSelections = {};
    compositeSelections.__compositeSelections["series::season-composite"] = [{
        LocalStartEpisodeItemId: "episode-3", RequestedEpisodeCount: 2,
        Source: { ProviderId: "Dandan", MediaId: "frieren-s2" },
        SourceStartEpisodeNumber: 1, AlignmentIntent: "ExplicitAnchor", Origin: "manual"
    }];
    const virtualGroups = hooks.compositeVirtualGroups(compositeSeason, compositeSelections);
    assert(virtualGroups.map(group => group.kind).join(",") === "mapped,manual,unmatched" &&
        virtualGroups[2].episodes.length === 1 && virtualGroups[2].episodes[0].ItemId === "episode-5",
        "a manual temporary season must consume only its chosen range and leave the next unmatched run visible");
    assert(hooks.compositeHasDownloadableMappings(compositeSeason, compositeSelections),
        "exact mappings or a manual virtual season must permit downloading the confirmed subset");
    const compactSelections = hooks.compositeRequestSelections(compositeSelections, compositeSeason);
    const currentSeasonRequest = hooks.seasonRequestParameters(compositeSeason);
    assert(currentSeasonRequest.mappingProtocolVersion === 22 &&
        currentSeasonRequest.planGeneration === compositeSeason.PlanGeneration &&
        currentSeasonRequest.mappingProtocolGeneration === undefined,
        "every Season rebuild/rematch/download request must echo the server-authored numeric V22 plan generation");
    assert(compactSelections.length === 2 && compactSelections[0].CandidateId === "frieren-s1" &&
        JSON.stringify(compactSelections[0]) === JSON.stringify(compositeSeason.CompositeSelections[0]) &&
        compactSelections[0].LocalStartEpisodeItemId === "episode-1" && compactSelections[0].RequestedEpisodeCount === 2 &&
        compactSelections[1].CandidateId === "frieren-s2" && compactSelections[1].SourceStartEpisodeNumber === 1 &&
        compactSelections[0].AlignmentIntent === "DefaultZeroOffset" &&
        compactSelections[1].AlignmentIntent === "ExplicitAnchor" &&
        compactSelections.every(selection => ["DefaultZeroOffset", "ExplicitAnchor"].includes(selection.AlignmentIntent)) &&
        JSON.stringify(compactSelections).indexOf("server-only") < 0 &&
        JSON.stringify(compactSelections).indexOf("CommentId") < 0 &&
        JSON.stringify(compactSelections).indexOf("Mappings") < 0,
        "the browser must resubmit closed V22 alignment intent and candidate anchors without CommentId or browser-authored exact mappings");

    const cleanFirstStart = new FakeElement("input");
    cleanFirstStart.dataset.danmuSourceStartDirty = "false";
    const dirtyFirstStart = new FakeElement("input");
    dirtyFirstStart.value = "5";
    dirtyFirstStart.dataset.danmuSourceStartDirty = "true";
    const confirmedFirstSelection = {
        AlignmentIntent: "DefaultZeroOffset",
        SourceStartEpisodeId: "source-1",
        SourceStartEpisodeNumber: 1
    };
    const missingIntentSelection = {
        SourceStartEpisodeId: "source-5",
        SourceStartEpisodeNumber: 5
    };
    const unknownIntentSelection = Object.assign({}, missingIntentSelection, {
        AlignmentIntent: "explicitanchor"
    });
    const firstGroup = { kind: "unmatched", episodes: [compositeSeason.CompositePlan.OrderedEpisodes[0]] };
    const nonFirstGroup = { kind: "unmatched", episodes: [compositeSeason.CompositePlan.OrderedEpisodes[2]] };
    assert(hooks.sourceStartAlignmentIntent(cleanFirstStart, compositeSeason, firstGroup, null) === "DefaultZeroOffset" &&
        hooks.sourceStartAlignmentIntent(dirtyFirstStart, compositeSeason, firstGroup, null) === "ExplicitAnchor" &&
        hooks.sourceStartAlignmentIntent(cleanFirstStart, compositeSeason, nonFirstGroup, null) === "ExplicitAnchor" &&
        hooks.sourceStartAlignmentIntent(cleanFirstStart, compositeSeason, firstGroup,
            { AlignmentIntent: "ExplicitAnchor" }) === "ExplicitAnchor" &&
        hooks.sourceStartAlignmentIntent(cleanFirstStart, compositeSeason, firstGroup,
            missingIntentSelection) === "" &&
        hooks.sourceStartAlignmentIntent(cleanFirstStart, compositeSeason, firstGroup,
            unknownIntentSelection) === "" &&
        hooks.sourceStartAlignmentIntent(dirtyFirstStart, compositeSeason, firstGroup,
            missingIntentSelection) === "" &&
        hooks.sourceStartAlignmentIntent(dirtyFirstStart, compositeSeason, firstGroup,
            unknownIntentSelection) === "",
        "local clean/dirty creation must choose a closed intent, while a server-confirmed selection with missing or non-exact intent must fail closed");
    const explicitFirstE5 = hooks.compactCompositeSelection(
        "episode-1", 2, "Dandan", "frieren-s1-e5",
        hooks.sourceStartEpisodeIdForSubmission(dirtyFirstStart, confirmedFirstSelection), 5,
        "manual", "candidate-anchor-only", hooks.sourceStartAlignmentIntent(
            dirtyFirstStart, compositeSeason, firstGroup, confirmedFirstSelection), compositeSeason.PlanGeneration);
    dirtyFirstStart.value = "1";
    const dirtyBackToOne = hooks.compactCompositeSelection(
        "episode-1", 2, "Dandan", "frieren-s1-e1",
        hooks.sourceStartEpisodeIdForSubmission(dirtyFirstStart, confirmedFirstSelection), 1,
        "manual", "candidate-anchor-only", hooks.sourceStartAlignmentIntent(
            dirtyFirstStart, compositeSeason, firstGroup, confirmedFirstSelection), compositeSeason.PlanGeneration);
    const invalidIntent = hooks.compactCompositeSelection(
        "episode-1", 2, "Dandan", "invalid-intent", "source-1", 1,
        "manual", "candidate-anchor-only", "BrowserChosenMode", compositeSeason.PlanGeneration);
    assert(explicitFirstE5.AlignmentIntent === "ExplicitAnchor" &&
        explicitFirstE5.SourceStartEpisodeId === "" &&
        explicitFirstE5.SourceStartEpisodeNumber === 5 &&
        dirtyBackToOne.AlignmentIntent === "ExplicitAnchor" &&
        dirtyBackToOne.SourceStartEpisodeId === "" &&
        dirtyBackToOne.SourceStartEpisodeNumber === 1 &&
        hooks.sourceStartEpisodeIdForSubmission(cleanFirstStart, confirmedFirstSelection) === "source-1" &&
        invalidIntent === null &&
        hooks.closedAlignmentIntent("DefaultZeroOffset") === "DefaultZeroOffset" &&
        hooks.closedAlignmentIntent("ExplicitAnchor") === "ExplicitAnchor" &&
        hooks.closedAlignmentIntent() === "" && hooks.closedAlignmentIntent(1) === "" &&
        hooks.closedAlignmentIntent("explicitanchor") === "" &&
        hooks.closedAlignmentIntent("BrowserChosenMode") === "" &&
        !Object.prototype.hasOwnProperty.call(dirtyBackToOne, "Mappings") &&
        !Object.prototype.hasOwnProperty.call(dirtyBackToOne, "CommentId"),
        "editing E5 and then returning to E1 must remain explicitly anchored, while invalid, missing, numeric, unknown, or case-mismatched intent cannot be compacted or defaulted");

    const missingServerIntentSeason = JSON.parse(JSON.stringify(compositeSeason));
    delete missingServerIntentSeason.CompositeSelections[0].AlignmentIntent;
    missingServerIntentSeason.CompositePlan.Mappings[0].SourceEpisodeId = "source-5";
    missingServerIntentSeason.CompositePlan.Mappings[0].SourceEpisodeNumber = 5;
    const unknownServerIntentSeason = JSON.parse(JSON.stringify(missingServerIntentSeason));
    unknownServerIntentSeason.CompositeSelections[0].AlignmentIntent = "BrowserChosenMode";
    const explicitServerIntentSeason = JSON.parse(JSON.stringify(compositeSeason));
    explicitServerIntentSeason.CompositeSelections.forEach(selection => {
        selection.AlignmentIntent = "ExplicitAnchor";
    });
    assert(!hooks.serverCompositeAlignmentIntentsAreClosed(missingServerIntentSeason) &&
        !hooks.serverCompositeAlignmentIntentsAreClosed(unknownServerIntentSeason) &&
        hooks.serverCompositeAlignmentIntentsAreClosed(compositeSeason) &&
        hooks.serverCompositeAlignmentIntentsAreClosed(explicitServerIntentSeason) &&
        hooks.compositeRequestSelections({}, missingServerIntentSeason).length === 0 &&
        hooks.compositeRequestSelections({}, unknownServerIntentSeason).length === 0 &&
        !hooks.compositeHasDownloadableMappings(missingServerIntentSeason, {}) &&
        !hooks.compositeHasDownloadableMappings(unknownServerIntentSeason, {}),
        "server-confirmed explicit anchors with missing/unknown intent must require a fresh preview, while both exact closed values remain valid");

    function canonicalWire(localStart, count, candidateId, sourceStartId, sourceStartNumber,
        intent, generation) {
        return {
            LocalStartEpisodeItemId: localStart, RequestedEpisodeCount: count,
            Site: "Dandan", CandidateId: candidateId,
            SourceStartEpisodeId: sourceStartId, SourceStartEpisodeNumber: sourceStartNumber,
            MatchOrigin: "scored", SelectionEvidenceToken: "canonical-evidence",
            AlignmentIntent: intent, MappingProtocolVersion: 22, PlanGeneration: generation
        };
    }

    const spyNumbers = [1, 2, 3, 4, 5, 6, 10, 11, 12, 13];
    const spyEpisodes = spyNumbers.map(number => ({
        ItemId: "spy-local-" + number, EpisodeNumber: number, ParentSeasonNumber: 3,
        SortOrder: number, LocalDisplayLabel: "S03E" + String(number).padStart(2, "0")
    }));
    const spyCanonical = canonicalWire(
        "spy-local-1", 0, "spy-source", "spy-source-1", 1, "DefaultZeroOffset", 9301);
    const spySparseSeason = {
        SeriesId: "spy-series", SeasonId: "spy-s3", SeasonNumber: 3, EpisodeCount: 10,
        MappingProtocolVersion: 22, PlanGeneration: 9301, RequiresCompositeMapping: true,
        CompositeSelections: [spyCanonical],
        CompositePlan: {
            OrderedEpisodes: spyEpisodes,
            Mappings: spyNumbers.map(number => ({
                LocalEpisodeItemId: "spy-local-" + number,
                Source: { ProviderId: "Dandan", MediaId: "spy-source" },
                SourceEpisodeId: "spy-source-" + number, SourceEpisodeNumber: number,
                Origin: "scored", AlignmentIntent: "DefaultZeroOffset"
            })),
            UnmatchedRuns: []
        },
        CompositeGroups: [[1, 2, 3, 4, 5, 6], [10, 11, 12, 13]].map(numbers => ({
            IsTemporary: false, Site: "Dandan", CandidateId: "spy-source", MatchOrigin: "scored",
            AlignmentIntent: "DefaultZeroOffset", Episodes: numbers.map(number => spyEpisodes.find(
                episode => episode.EpisodeNumber === number))
        }))
    };
    const spyRoundTrip = hooks.serverCompositeRequestSelections(spySparseSeason);
    assert(spyRoundTrip.length === 1 && JSON.stringify(spyRoundTrip[0]) === JSON.stringify(spyCanonical) &&
        spyRoundTrip[0].RequestedEpisodeCount === 0 &&
        hooks.selectionLocalEpisodeItemIds(spySparseSeason, spyRoundTrip[0], spyRoundTrip).join(",") ===
            spyEpisodes.map(episode => episode.ItemId).join(","),
        "Spy sparse display groups must round-trip one canonical count=0 selection from E1 without deriving count or splitting at E7-E9");

    const startsAtTenCanonical = canonicalWire(
        "starts-ten-local-10", 0, "starts-ten-source", "starts-ten-source-1", 1,
        "ExplicitAnchor", 9302);
    const startsAtTenSeason = {
        SeriesId: "starts-ten-series", SeasonId: "starts-ten-s1", SeasonNumber: 1,
        MappingProtocolVersion: 22, PlanGeneration: 9302, RequiresCompositeMapping: true,
        compositeSelections: [Object.fromEntries(Object.entries(startsAtTenCanonical).map(([key, val]) =>
            [key.charAt(0).toLowerCase() + key.slice(1), val]))],
        CompositePlan: {
            OrderedEpisodes: [10, 11, 12].map(number => ({ ItemId: "starts-ten-local-" + number,
                EpisodeNumber: number, ParentSeasonNumber: 1, SortOrder: number })),
            Mappings: [10, 11, 12].map((number, index) => ({
                LocalEpisodeItemId: "starts-ten-local-" + number,
                Source: { ProviderId: "Dandan", MediaId: "starts-ten-source" },
                SourceEpisodeId: "starts-ten-source-" + (index + 1), SourceEpisodeNumber: index + 1,
                Origin: "scored", AlignmentIntent: "ExplicitAnchor"
            })), UnmatchedRuns: []
        }
    };
    assert(JSON.stringify(hooks.serverCompositeRequestSelections(startsAtTenSeason)) ===
        JSON.stringify([startsAtTenCanonical]) &&
        hooks.serverCompositeRequestSelections(startsAtTenSeason)[0].LocalStartEpisodeItemId === "starts-ten-local-10" &&
        hooks.serverCompositeRequestSelections(startsAtTenSeason)[0].SourceStartEpisodeNumber === 1,
        "camel-case canonical payload must preserve a local inventory beginning at E10 anchored to source E1");

    const unnumberedPascalSeason = JSON.parse(JSON.stringify(compositeSeason));
    unnumberedPascalSeason.CompositeSelections[0].SourceStartEpisodeId = "unnumbered-exact-pascal";
    unnumberedPascalSeason.CompositeSelections[0].SourceStartEpisodeNumber = 0;
    unnumberedPascalSeason.CompositePlan.Mappings[0].SourceEpisodeNumber = 77;
    const unnumberedPascalRoundTrip = hooks.serverCompositeRequestSelections(unnumberedPascalSeason);
    const unnumberedCamelSeason = JSON.parse(JSON.stringify(startsAtTenSeason));
    unnumberedCamelSeason.compositeSelections[0].sourceStartEpisodeId = "unnumbered-exact-camel";
    unnumberedCamelSeason.compositeSelections[0].sourceStartEpisodeNumber = 0;
    unnumberedCamelSeason.CompositePlan.Mappings[0].SourceEpisodeNumber = 88;
    const unnumberedCamelRoundTrip = hooks.serverCompositeRequestSelections(unnumberedCamelSeason);
    const invalidUnnumberedValues = [null, -1, 0.5].map(sourceNumber => {
        const season = JSON.parse(JSON.stringify(unnumberedPascalSeason));
        season.CompositeSelections[0].SourceStartEpisodeNumber = sourceNumber;
        return season;
    });
    const missingUnnumberedValue = JSON.parse(JSON.stringify(unnumberedPascalSeason));
    delete missingUnnumberedValue.CompositeSelections[0].SourceStartEpisodeNumber;
    invalidUnnumberedValues.push(missingUnnumberedValue);
    assert(unnumberedPascalRoundTrip.length === 1 &&
        unnumberedPascalRoundTrip[0].SourceStartEpisodeId === "unnumbered-exact-pascal" &&
        unnumberedPascalRoundTrip[0].SourceStartEpisodeNumber === 0 &&
        unnumberedCamelRoundTrip.length === 1 &&
        unnumberedCamelRoundTrip[0].SourceStartEpisodeId === "unnumbered-exact-camel" &&
        unnumberedCamelRoundTrip[0].SourceStartEpisodeNumber === 0 &&
        invalidUnnumberedValues.every(season =>
            !hooks.serverCompositeAlignmentIntentsAreClosed(season) &&
            hooks.serverCompositeRequestSelections(season).length === 0),
        "exact unnumbered source anchors must round-trip stable number sentinel 0 in Pascal/camel payloads, while null, missing, negative, and non-integer values fail closed");

    const gapCanonical = canonicalWire(
        "gap-local-1", 0, "gap-source", "gap-source-1", 1, "DefaultZeroOffset", 9303);
    const gapEpisodes = [1, 2, 3].map(number => ({ ItemId: "gap-local-" + number,
        EpisodeNumber: number, ParentSeasonNumber: 1, SortOrder: number }));
    const gapSplitSeason = {
        SeriesId: "gap-series", SeasonId: "gap-s1", SeasonNumber: 1,
        MappingProtocolVersion: 22, PlanGeneration: 9303, RequiresCompositeMapping: true,
        CompositeSelections: [gapCanonical],
        CompositePlan: {
            OrderedEpisodes: gapEpisodes,
            Mappings: [1, 3].map(number => ({ LocalEpisodeItemId: "gap-local-" + number,
                Source: { ProviderId: "Dandan", MediaId: "gap-source" },
                SourceEpisodeId: "gap-source-" + number, SourceEpisodeNumber: number,
                Origin: "scored", AlignmentIntent: "DefaultZeroOffset" })),
            UnmatchedRuns: [{ Episodes: [gapEpisodes[1]] }]
        }
    };
    const gapGroups = hooks.compositeVirtualGroups(gapSplitSeason, {});
    const gapRoundTrip = hooks.serverCompositeRequestSelections(gapSplitSeason);
    const mappedGapRemovalResults = gapGroups.filter(group => group.kind === "mapped").map(group =>
        hooks.filterCompositeSelectionsByItemIds(gapSplitSeason, gapRoundTrip,
            group.episodes.map(episode => episode.ItemId)));
    const gapManual = hooks.compactCompositeSelection(
        "gap-local-2", 1, "Youku", "gap-special", "gap-special-1", 1,
        "manual", "gap-manual-evidence", "ExplicitAnchor", 9303);
    const manualGapRemoval = hooks.filterCompositeSelectionsByItemIds(
        gapSplitSeason, gapRoundTrip.concat([gapManual]), ["gap-local-2"]);
    assert(gapGroups.map(group => group.kind).join(",") === "mapped,unmatched,mapped" &&
        gapRoundTrip.length === 1 && JSON.stringify(gapRoundTrip[0]) === JSON.stringify(gapCanonical) &&
        mappedGapRemovalResults.every(result => result.removed.length === 1 && result.kept.length === 0 &&
            JSON.stringify(result.removed[0]) === JSON.stringify(gapCanonical)) &&
        manualGapRemoval.removed.length === 1 && manualGapRemoval.removed[0].CandidateId === "gap-special" &&
        manualGapRemoval.kept.length === 1 &&
        JSON.stringify(manualGapRemoval.kept[0]) === JSON.stringify(gapCanonical) &&
        !/[Mm]appings|CommentId|AlignmentMode|SourceFrontier/.test(JSON.stringify(gapRoundTrip)),
        "gap-split mapped removal must drop the unchanged canonical selection, while an exact manual unmatched selection removes only itself and cannot rewrite that canonical selection");

    const directOnlySeason = {
        SeriesId: "direct-only-series", SeasonId: "direct-only-s1", SeasonNumber: 1,
        MappingProtocolVersion: 22, PlanGeneration: 9304, RequiresCompositeMapping: true,
        CompositeSelections: [],
        CompositePlan: {
            OrderedEpisodes: [{ ItemId: "direct-only-local", EpisodeNumber: 1,
                ParentSeasonNumber: 1, SortOrder: 1 }],
            Mappings: [{ LocalEpisodeItemId: "direct-only-local",
                Source: { ProviderId: "Youku", MediaId: "direct-only-source" },
                SourceEpisodeId: "direct-only-episode", SourceEpisodeNumber: 1,
                Origin: "episode-provider-id" }], UnmatchedRuns: []
        }
    };
    const missingCanonicalSeason = JSON.parse(JSON.stringify(spySparseSeason));
    delete missingCanonicalSeason.CompositeSelections;
    const caseMismatchCanonicalSeason = JSON.parse(JSON.stringify(spySparseSeason));
    caseMismatchCanonicalSeason.CompositeSelections[0].AlignmentIntent = "defaultzerooffset";
    const serverFieldCanonicalSeason = JSON.parse(JSON.stringify(spySparseSeason));
    serverFieldCanonicalSeason.CompositeSelections[0].CommentId = "must-not-cross-wire";
    assert(hooks.serverCompositeAlignmentIntentsAreClosed(directOnlySeason) &&
        hooks.serverCompositeRequestSelections(directOnlySeason).length === 0 &&
        hooks.compositeHasDownloadableMappings(directOnlySeason, {}) &&
        !hooks.serverCompositeAlignmentIntentsAreClosed(missingCanonicalSeason) &&
        !hooks.serverCompositeAlignmentIntentsAreClosed(caseMismatchCanonicalSeason) &&
        !hooks.serverCompositeAlignmentIntentsAreClosed(serverFieldCanonicalSeason) &&
        hooks.serverCompositeRequestSelections(missingCanonicalSeason).length === 0 &&
        hooks.serverCompositeRequestSelections(caseMismatchCanonicalSeason).length === 0 &&
        hooks.serverCompositeRequestSelections(serverFieldCanonicalSeason).length === 0 &&
        !hooks.compositeHasDownloadableMappings(missingCanonicalSeason, {}) &&
        !hooks.compositeHasDownloadableMappings(caseMismatchCanonicalSeason, {}),
        "canonical empty is valid only for direct-only exact plans; missing, case-mismatched, or server-field-bearing payloads fail closed");
    const cachedV21Season = Object.assign({}, compositeSeason, {
        MappingProtocolVersion: 21, PlanGeneration: compositeSeason.PlanGeneration
    });
    assert(!hooks.hasCurrentMappingContract(cachedV21Season) && !hooks.hasCompositePlan(cachedV21Season) &&
        hooks.compositeRequestSelections(compositeSelections, cachedV21Season).length === 0,
        "a cached V21 Season draft must be discarded and cannot be submitted or restored");
    assert(!hooks.hasCurrentMappingContract(Object.assign({}, compositeSeason, { PlanGeneration: 0 })) &&
        !hooks.hasCurrentMappingContract(Object.assign({}, compositeSeason, { PlanGeneration: "invalid" })),
        "V22 requires a positive numeric server-authored plan generation");
    assert(hooks.decisionReasonLabel({ DecisionReason: "no-eligible-episodes" }) ===
        "\u672c\u5b63\u6ca1\u6709\u53ef\u53c2\u4e0e\u5339\u914d\u7684\u5267\u96c6" &&
        hooks.decisionReasonLabel({ DecisionReason: "target-season-inventory-unavailable" }) ===
        "\u65e0\u6cd5\u8bfb\u53d6\u672c\u5b63\u5267\u96c6\u6e05\u5355" &&
        hooks.decisionReasonLabel({ DecisionReason: "stale-scope" }) ===
        "\u672c\u5b63\u5267\u96c6\u8303\u56f4\u5df2\u53d8\u5316\uff0c\u8bf7\u91cd\u65b0\u9884\u89c8",
        "r5 inventory, empty-scope, and stale-scope diagnostics must have stable user-facing labels");

    const scopedS1 = {
        SeriesId: "scope-series", SeasonId: "scope-s1", SeasonNumber: 1, SeasonName: "Season 1",
        EpisodeCount: 12, DisplayedEpisodeCount: 19, EligibleEpisodeCount: 12,
        IgnoredParentZeroEpisodeCount: 7, IgnoredOtherSeasonEpisodeCount: 0,
        IgnoredUnknownParentEpisodeCount: 0, IgnoredInvalidEpisodeCount: 0,
        MappingProtocolVersion: 22, PlanGeneration: 8101, RequiresCompositeMapping: true,
        CompositeSelections: [{ LocalStartEpisodeItemId: "s1e1", RequestedEpisodeCount: 1,
            Site: "Dandan", CandidateId: "scope-source", SourceStartEpisodeId: "source-1",
            SourceStartEpisodeNumber: 1, MatchOrigin: "scored", SelectionEvidenceToken: "",
            AlignmentIntent: "DefaultZeroOffset", MappingProtocolVersion: 22, PlanGeneration: 8101 }],
        CompositePlan: {
            OrderedEpisodes: [{ ItemId: "s1e1", ParentSeasonNumber: 1, EpisodeNumber: 1,
                LocalDisplayLabel: "S01E01" }],
            Mappings: [{ LocalEpisodeItemId: "s1e1",
                Source: { ProviderId: "Dandan", MediaId: "scope-source" },
                SourceEpisodeId: "source-1", SourceEpisodeNumber: 1, Origin: "scored" },
                { LocalEpisodeItemId: "s0e1",
                    Source: { ProviderId: "Dandan", MediaId: "foreign-source" },
                    SourceEpisodeId: "foreign-1", SourceEpisodeNumber: 1, Origin: "scored" }],
            UnmatchedRuns: [{ Episodes: [{ ItemId: "s0e1", ParentSeasonNumber: 0,
                EpisodeNumber: 1, LocalDisplayLabel: "S00E01" }] }]
        },
        CompositeGroups: [{ IsTemporary: false, Site: "Dandan", CandidateId: "scope-source",
            MatchOrigin: "scored", AlignmentIntent: "DefaultZeroOffset",
            Episodes: [{ ItemId: "s1e1", ParentSeasonNumber: 1,
                EpisodeNumber: 1, LocalDisplayLabel: "S01E01", SourceEpisodeNumber: 1 }] },
            { IsTemporary: false, Site: "Dandan", CandidateId: "foreign-source",
                MatchOrigin: "scored", AlignmentIntent: "DefaultZeroOffset",
                Episodes: [{ ItemId: "s0e1", ParentSeasonNumber: 0,
                    EpisodeNumber: 1, LocalDisplayLabel: "S00E01", SourceEpisodeNumber: 1 }] },
            { IsTemporary: true, Episodes: [{ ItemId: "s0e1", ParentSeasonNumber: 0,
                EpisodeNumber: 1, LocalDisplayLabel: "S00E01" }] }]
    };
    assert(hooks.compositeVirtualGroups(scopedS1, {}).length === 1 &&
        hooks.compositeVirtualGroups(scopedS1, {})[0].episodes[0].ItemId === "s1e1" &&
        hooks.compositeRequestSelections({}, scopedS1).length === 1 &&
        hooks.compositeRequestSelections({}, scopedS1)[0].LocalStartEpisodeItemId === "s1e1" &&
        JSON.stringify(hooks.compositeRequestSelections({}, scopedS1)).indexOf("s0e1") < 0,
        "normal S1 must render and submit only Parent 1 mappings; foreign S00 mappings and temporary runs stay off wire");
    assert(hooks.scopeSummaryLine(scopedS1).includes("显示 19 集") &&
        hooks.scopeSummaryLine(scopedS1).includes("参与匹配 12 集") &&
        hooks.scopeSummaryLine(scopedS1).includes("S00 7 集"),
        "ignored cross-season counts must remain a read-only summary");

    const explicitS0 = {
        SeriesId: "scope-series", SeasonId: "scope-s0", SeasonNumber: 0, SeasonName: "Season 0",
        EpisodeCount: 1, DisplayedEpisodeCount: 1, EligibleEpisodeCount: 1,
        MappingProtocolVersion: 22, PlanGeneration: 8102, RequiresCompositeMapping: true,
        CompositeSelections: [{ LocalStartEpisodeItemId: "s0-own-e1", RequestedEpisodeCount: 1,
            Site: "Dandan", CandidateId: "special-source", SourceStartEpisodeId: "special-1",
            SourceStartEpisodeNumber: 1, MatchOrigin: "scored", SelectionEvidenceToken: "",
            AlignmentIntent: "DefaultZeroOffset", MappingProtocolVersion: 22, PlanGeneration: 8102 }],
        CompositePlan: {
            OrderedEpisodes: [{ ItemId: "s0-own-e1", ParentSeasonNumber: 0, EpisodeNumber: 1,
                LocalDisplayLabel: "S00E01" }],
            Mappings: [{ LocalEpisodeItemId: "s0-own-e1",
                Source: { ProviderId: "Dandan", MediaId: "special-source" },
                SourceEpisodeId: "special-1", SourceEpisodeNumber: 1, Origin: "scored" }],
            UnmatchedRuns: []
        },
        CompositeGroups: [{ IsTemporary: false, Site: "Dandan", CandidateId: "special-source",
            MatchOrigin: "scored", AlignmentIntent: "DefaultZeroOffset",
            Episodes: [{ ItemId: "s0-own-e1", ParentSeasonNumber: 0,
                EpisodeNumber: 1, LocalDisplayLabel: "S00E01", SourceEpisodeNumber: 1 }] }]
    };
    assert(hooks.isEligibleCompositeEpisode(explicitS0, explicitS0.CompositePlan.OrderedEpisodes[0]) &&
        hooks.compositeVirtualGroups(explicitS0, {}).length === 1 &&
        hooks.compositeRequestSelections({}, explicitS0)[0].LocalStartEpisodeItemId === "s0-own-e1",
        "an explicitly opened Season 0 must render and submit its own Parent 0 plan normally");

    const seriesScopeDialog = hooks.openDialog("r5 whole-Series scope");
    hooks.renderSeriesPicker(seriesScopeDialog,
        { Id: "scope-series", Type: "Series", Name: "Scope Series" },
        [explicitS0, scopedS1], {}, {});
    assert(!allVisibleText(seriesScopeDialog.body).includes("Season 0") &&
        allVisibleText(seriesScopeDialog.body).includes("Season 1") &&
        seriesScopeDialog.body.querySelectorAll(".danmuVirtualSeason").length === 1,
        "whole-Series rendering must omit Season 0 and must not add it back when the response contains one");
    seriesScopeDialog.forceClose();

    const incompatibleSelections = { __compositeSelections: {}, __mappingContracts: {} };
    incompatibleSelections["series::season-composite"] = { Id: "old-candidate" };
    incompatibleSelections.__compositeSelections["series::season-composite"] = [{
        LocalStartEpisodeItemId: "episode-1", RequestedEpisodeCount: 1
    }];
    hooks.discardIncompatibleSeasonDrafts(null, [cachedV21Season], incompatibleSelections);
    assert(!incompatibleSelections["series::season-composite"] &&
        !incompatibleSelections.__compositeSelections["series::season-composite"],
        "a fresh V22 dialog must clear cached V21 candidate and compact-selection state without submitting it");
    const invalidIntentDrafts = { __compositeSelections: {}, __mappingContracts: {} };
    invalidIntentDrafts.__mappingContracts["series::season-composite"] = "22:" + compositeSeason.PlanGeneration;
    invalidIntentDrafts.__compositeSelections["series::season-composite"] = [{
        LocalStartEpisodeItemId: "episode-3", RequestedEpisodeCount: 1,
        Source: { ProviderId: "Dandan", MediaId: "old-explicit-anchor" },
        SourceStartEpisodeNumber: 5
    }];
    hooks.discardIncompatibleSeasonDrafts(null, [compositeSeason], invalidIntentDrafts);
    assert(!invalidIntentDrafts.__compositeSelections["series::season-composite"] &&
        hooks.compositeRequestSelections(invalidIntentDrafts, compositeSeason).length === 1,
        "rerender/reset must clear a V22 draft whose intent is missing and retain only the valid server-confirmed selection");
    const staleIntentDialog = hooks.openDialog("stale alignment intent");
    hooks.renderSeriesPicker(staleIntentDialog,
        { Id: "series", Type: "Series", Name: "stale intent" },
        [missingServerIntentSeason], {}, {});
    assert(staleIntentDialog.body.querySelectorAll(".danmuAlignmentIntentStale").length === 1 &&
        allVisibleText(staleIntentDialog.body).includes("请重新预览") &&
        staleIntentDialog.footer.querySelector(".primary").disabled,
        "rerendering an authoritative Season with invalid intent must request a fresh preview and disable submission without retrying");
    staleIntentDialog.forceClose();
    const removeS1 = hooks.filterCompositeSelectionsByItemIds(
        compositeSeason, compactSelections, ["episode-1", "episode-2"]);
    const removeS2 = hooks.filterCompositeSelectionsByItemIds(
        compositeSeason, compactSelections, ["episode-3", "episode-4"]);
    assert(removeS1.removed.length === 1 && removeS1.removed[0].CandidateId === "frieren-s1" &&
        removeS1.kept.length === 1 && removeS1.kept[0].CandidateId === "frieren-s2" &&
        removeS2.removed.length === 1 && removeS2.removed[0].CandidateId === "frieren-s2" &&
        removeS2.kept.length === 1 && removeS2.kept[0].CandidateId === "frieren-s1",
        "mapped searched and manual selections must be removed only when their exact local Episode ItemIds overlap the clicked group");
    const directSeason = {
        SeriesId: compositeSeason.SeriesId, SeasonId: compositeSeason.SeasonId,
        SeasonNumber: compositeSeason.SeasonNumber, SeasonName: compositeSeason.SeasonName,
        MappingProtocolVersion: compositeSeason.MappingProtocolVersion,
        PlanGeneration: compositeSeason.PlanGeneration,
        CompositeSelections: JSON.parse(JSON.stringify(compositeSeason.CompositeSelections)),
        CompositePlan: {
            OrderedEpisodes: compositeSeason.CompositePlan.OrderedEpisodes,
            Mappings: compositeSeason.CompositePlan.Mappings.concat([{
                LocalEpisodeItemId: "episode-5",
                Source: { ProviderId: "YoukuID", MediaId: "2a5659587f87497d9aab" },
                SourceEpisodeId: "direct-source", CommentId: "direct-server-only",
                SourceEpisodeNumber: 5, Origin: "episode-provider-id"
            }]),
            UnmatchedRuns: [{ Episodes: compositeSeason.CompositePlan.UnmatchedRuns[0].Episodes.slice(0, 2) }]
        }
    };
    const directCompactSelections = hooks.compositeRequestSelections(compositeSelections, directSeason);
    assert(directCompactSelections.length === 2 &&
        directCompactSelections.map(selection => selection.CandidateId).join(",") === "frieren-s1,frieren-s2" &&
        JSON.stringify(directCompactSelections).indexOf("direct-episode") < 0 &&
        JSON.stringify(directCompactSelections).indexOf("CommentId") < 0,
        "direct Episode provider-id mappings must be rebuilt by the server and never submitted by the browser");
    assert(hooks.isForbiddenBatchOrigin("direct-episode-provider-id") &&
        hooks.isForbiddenBatchOrigin("exact-binding") && hooks.isForbiddenBatchOrigin("provider-id"),
        "V22 must discard all cached local-identifier-derived batch origins");
    const staleSelections = {};
    staleSelections.__compositeSelections = {};
    staleSelections.__compositeSelections["series::season-composite"] = [{
        LocalStartEpisodeItemId: "episode-1", RequestedEpisodeCount: 2,
        Source: { ProviderId: "Dandan", MediaId: "obsolete" }, SourceStartEpisodeNumber: 1,
        AlignmentIntent: "DefaultZeroOffset"
    }];
    assert(hooks.compositeVirtualGroups(compositeSeason, staleSelections).map(group => group.kind).join(",") === "mapped,unmatched" &&
        hooks.compositeRequestSelections(staleSelections, compositeSeason).length === 1 &&
        hooks.compositeRequestSelections(staleSelections, compositeSeason)[0].CandidateId === "frieren-s1",
        "a refreshed server plan must hide and omit stale manual choices that now overlap exact mappings");
    assert(hooks.removeCompositeSelection(compositeSelections, compositeSeason, "episode-3") &&
        hooks.compositeVirtualGroups(compositeSeason, compositeSelections).map(group => group.kind).join(",") === "mapped,unmatched" &&
        !hooks.removeCompositeSelection(compositeSelections, compositeSeason, "episode-3"),
        "a virtual season can be removed and the original unmatched run is restored for re-match");
    const groupOnlySeason = {
        SeriesId: "series", SeasonId: "group-only", SeasonNumber: 2, SeasonName: "Group only",
        MappingProtocolVersion: 22, PlanGeneration: 7342,
        RequiresCompositeMapping: true,
        CompositeGroups: [{ IsTemporary: false, Site: "Dandan", CandidateId: "s1",
            AlignmentIntent: "DefaultZeroOffset",
            MatchScore: 0, SourceMetadata: { Title: "Upstream Season", Year: 2024, Category: "Anime" },
            Episodes: [{ ItemId: "a", ParentSeasonNumber: 2, EpisodeNumber: 1 }] },
            { IsTemporary: true, MatchScore: 0, ScoreOrigin: "search-confidence",
                Episodes: [{ ItemId: "b", ParentSeasonNumber: 2, EpisodeNumber: 2 }] }]
    };
    assert(hooks.hasCompositePlan(groupOnlySeason) &&
        hooks.compositeVirtualGroups(groupOnlySeason, {}).map(group => group.kind).join(",") === "mapped,unmatched" &&
        !hooks.serverCompositeAlignmentIntentsAreClosed(groupOnlySeason) &&
        !hooks.compositeHasDownloadableMappings(groupOnlySeason, {}) &&
        hooks.compositeRequestSelections({}, groupOnlySeason).length === 0,
        "a group-only rollout response without server-canonical CompositeSelections must remain visible but fail closed before download");
    const secondGroupOnlySeason = Object.assign({}, groupOnlySeason, {
        SeasonId: "group-only-second", SeasonNumber: 3, SeasonName: "Group only second",
        CompositeGroups: groupOnlySeason.CompositeGroups.map(group => Object.assign({}, group, {
            Episodes: group.Episodes.map(episode => Object.assign({}, episode, { ParentSeasonNumber: 3 }))
        }))
    });
    const episodeShortfallText = "库内集数少于来源集数";
    const verifiedShortfallSeason = Object.assign({}, groupOnlySeason, {
        HasVerifiedSourceEpisodeSurplus: true,
        CompositeGroups: groupOnlySeason.CompositeGroups.map(group => Object.assign({}, group, {
            HasVerifiedSourceEpisodeSurplus: true
        }))
    });
    const episodeShortfallDialog = hooks.openDialog("verified source episode surplus");
    hooks.renderSeriesPicker(episodeShortfallDialog,
        { Id: "series", Type: "Series", Name: "shortfall fixture" },
        [verifiedShortfallSeason], {}, {});
    assert(episodeShortfallDialog.body.querySelectorAll(".danmuEpisodeShortfallNotice").length === 1 &&
        allVisibleText(episodeShortfallDialog.body).split(episodeShortfallText).length === 2,
        "a whole-Series result must show the verified source-episode surplus notice exactly once per Season, not once per temporary group");
    hooks.renderCompositeTargetPicker(episodeShortfallDialog,
        { Id: "group-only", Type: "Season", Name: "Group only" }, verifiedShortfallSeason);
    assert(episodeShortfallDialog.body.querySelectorAll(".danmuEpisodeShortfallNotice").length === 1 &&
        allVisibleText(episodeShortfallDialog.body).split(episodeShortfallText).length === 2,
        "a single-Season result must reuse the same verified source-episode surplus notice exactly once");
    hooks.renderSeriesPicker(episodeShortfallDialog,
        { Id: "series", Type: "Series", Name: "shortfall fixture" },
        [Object.assign({}, groupOnlySeason, {
            HasVerifiedSourceEpisodeSurplus: false, EpisodeCount: 12,
            SelectedCandidate: { EpisodeSize: 12 }
        }), Object.assign({}, secondGroupOnlySeason, {
            EpisodeCount: 13, SelectedCandidate: { EpisodeSize: 12 }
        })], {}, {});
    assert(episodeShortfallDialog.body.querySelectorAll(".danmuEpisodeShortfallNotice").length === 0 &&
        !allVisibleText(episodeShortfallDialog.body).includes(episodeShortfallText),
        "false or missing verified surplus state, including equal or local-greater counts, must render no notice and a rerender must not retain stale notice DOM");
    hooks.renderCandidatePicker(episodeShortfallDialog,
        { Id: "season", Type: "Season", Name: "candidate shortfall fixture" },
        { SeasonId: "candidate-shortfall", SeasonNumber: 1, SeasonName: "Candidate shortfall",
            EpisodeCount: 2, Candidates: [{ Site: "Dandan", Id: "candidate-surplus", Name: "Candidate surplus",
                EpisodeSize: 99, HasVerifiedSourceEpisodeSurplus: true }] }, "");
    assert(episodeShortfallDialog.body.querySelectorAll(".danmuEpisodeShortfallNotice").length === 0 &&
        !allVisibleText(episodeShortfallDialog.body).includes(episodeShortfallText),
        "candidate-reported episode counts or surplus fields must not create the authoritative Season notice");
    hooks.renderSeriesPicker(episodeShortfallDialog,
        { Id: "series", Type: "Series", Name: "failed plan fixture" },
        [{ SeasonId: "failed-plan", SeasonNumber: 1, SeasonName: "Failed plan",
            MappingProtocolVersion: 22, PlanGeneration: 0, RequiresCompositeMapping: true,
            HasVerifiedSourceEpisodeSurplus: true, CompositePlan: verifiedShortfallSeason.CompositePlan }], {}, {});
    assert(episodeShortfallDialog.body.querySelectorAll(".danmuEpisodeShortfallNotice").length === 0,
        "a failed or non-authoritative composite plan must not render the response-only surplus notice");
    assert(source.includes(".danmuEpisodeShortfallNotice{color:#ffd54f;") &&
        source.includes('shortfall.textContent = "' + episodeShortfallText + '";'),
        "the verified source-episode surplus notice must use a clearly distinguishable yellow style and exact required wording");
    episodeShortfallDialog.forceClose();
    const compositeHintDialog = hooks.openDialog("composite mapping hint");
    hooks.renderSeriesPicker(compositeHintDialog,
        { Id: "series", Type: "Series", Name: "mapping hint fixture" },
        [groupOnlySeason, secondGroupOnlySeason], {}, {});
    assert(compositeHintDialog.body.querySelectorAll(".danmuCompositeHint").length === 1 &&
        allVisibleText(compositeHintDialog.body).split(compositeMappingHint).length === 2 &&
        compositeHintDialog.body.querySelectorAll(".danmuCompositeSeason").length === 2 &&
        compositeHintDialog.body.querySelectorAll(".danmuVirtualSeason").length === 4,
        "a multi-season result with multiple virtual groups must show the mapping hint exactly once above its mapping cards");
    hooks.renderSeriesPicker(compositeHintDialog,
        { Id: "series", Type: "Series", Name: "mapping hint fixture" },
        [groupOnlySeason, secondGroupOnlySeason], {}, {});
    assert(compositeHintDialog.body.querySelectorAll(".danmuCompositeHint").length === 1,
        "a repeated Series render or rematch must replace, not accumulate, the mapping hint");
    hooks.renderCompositeTargetPicker(compositeHintDialog,
        { Id: "group-only", Type: "Season", Name: "Group only" }, groupOnlySeason);
    assert(compositeHintDialog.body.querySelectorAll(".danmuCompositeHint").length === 1,
        "an applicable direct Season result must show the mapping hint exactly once");
    hooks.renderSeriesPicker(compositeHintDialog,
        { Id: "plain-series", Type: "Series", Name: "plain fixture" },
        [{ SeasonId: "plain-s1", SeasonNumber: 1, SeasonName: "Plain", Candidates: [] }], {}, {});
    assert(compositeHintDialog.body.querySelectorAll(".danmuCompositeHint").length === 0,
        "a result without mapping cards must not show the mapping hint");
    compositeHintDialog.forceClose();
    const providerFailureDialog = hooks.openDialog("provider failure isolation");
    const diagnosedComposite = Object.assign({}, groupOnlySeason, {
        SearchCompletionDiagnostics: [{ Provider: "Bilibili", Status: "failed", Message: "fixture fault" }]
    });
    hooks.renderSeriesPicker(providerFailureDialog,
        { Id: "series", Type: "Series", Name: "provider failure fixture" }, [diagnosedComposite], {}, {});
    assert(allVisibleText(providerFailureDialog.body).includes("搜索诊断：Bilibili 失败") &&
        providerFailureDialog.body.querySelectorAll(".danmuVirtualSeason").length === 2,
        "a failed provider must remain a visible diagnostic without blocking usable successful-provider mappings");
    hooks.renderCandidatePicker(providerFailureDialog,
        { Id: "season", Type: "Season", Name: "provider failure fixture" },
        { SeasonId: "candidate-s1", SeasonNumber: 1, SeasonName: "Candidate Season",
            SearchCompletionDiagnostics: [{ Provider: "Bilibili", Status: "failed" }],
            Candidates: [{ Site: "Dandan", SiteName: "弹弹Play", Id: "candidate", Name: "Usable candidate",
                Year: 2024, EpisodeSize: 12, Category: "动漫" }] }, "");
    assert(allVisibleText(providerFailureDialog.body).includes("搜索诊断：Bilibili 失败") &&
        providerFailureDialog.body.querySelectorAll(".danmuCandidate").length === 1,
        "a failed provider diagnostic must not block a completed-provider candidate from remaining selectable");
    providerFailureDialog.forceClose();
    const unmatchedScoreDialog = hooks.openDialog("unmatched score visibility");
    hooks.renderCompositeSeasonSummary(unmatchedScoreDialog,
        { Id: "series", Type: "Series", Name: "score fixture" },
        groupOnlySeason, 0, [groupOnlySeason], {}, {});
    const scoreCards = unmatchedScoreDialog.body.querySelectorAll(".danmuVirtualSeason");
    const mappedZeroCard = scoreCards.find(card => card.className.includes("matched") &&
        !card.className.includes("unmatched"));
    const unmatchedZeroCard = scoreCards.find(card => card.className.includes("unmatched"));
    /* Superseded encoding-damaged assertion retained inert below.
    assert(mappedZeroCard && allVisibleText(mappedZeroCard).includes("鍖归厤鍒嗭細0锛堟湇鍔＄璇勫垎锛?) &&
        unmatchedZeroCard && !allVisibleText(unmatchedZeroCard).includes("鍖归厤鍒?"),
        "an explicit mapped zero score may render, but an unmatched temporary season must never render a score");
    */
    assert(mappedZeroCard && allVisibleText(mappedZeroCard).includes(
            "\u5339\u914d\u5206\uff1a0\uff08\u670d\u52a1\u7aef\u8bc4\u5206\uff09") &&
        allVisibleText(mappedZeroCard).includes("Upstream Season\uff082024\uff09") &&
        !allVisibleText(mappedZeroCard).includes("s1") &&
        unmatchedZeroCard && !allVisibleText(unmatchedZeroCard).includes("\u5339\u914d\u5206"),
        "a matched temporary card must render safe source title/year while hiding identity; unmatched cards show no score");
    assert(hooks.sourceMetadataPublicLabel({ Title: "No Year" }) === "No Year" &&
        hooks.sourceMetadataPublicLabel({ Title: "Invalid", Year: 42 }) === "Invalid",
        "missing or invalid source years must be omitted instead of borrowing local season metadata");
    assert(scoreCards.every(card => card.querySelector(".danmuVirtualSeasonTitle").textContent.indexOf("\u4e34\u65f6\u5b63 ") === 0),
        "mapped and unmatched virtual ranges must use the same temporary-season title convention");
    unmatchedScoreDialog.forceClose();

    const liveCompositeGroupsSeason = {
        SeriesId: "one-punch", SeasonId: "one-punch-s1", SeasonNumber: 1,
        SeasonName: "一拳超人 第一季", EpisodeCount: 2,
        MappingProtocolVersion: 22, PlanGeneration: 202034,
        RequiresCompositeMapping: true,
        CompositeSelections: [{
            LocalStartEpisodeItemId: "local-dandan-secret", RequestedEpisodeCount: 1,
            Site: "DandanID", CandidateId: "11123", SourceStartEpisodeId: "111230001",
            SourceStartEpisodeNumber: 1, MatchOrigin: "scored", SelectionEvidenceToken: "",
            AlignmentIntent: "DefaultZeroOffset", MappingProtocolVersion: 22, PlanGeneration: 202034
        }, {
            LocalStartEpisodeItemId: "local-youku-secret", RequestedEpisodeCount: 1,
            Site: "YoukuID", CandidateId: "cfd9e3748c8a4d52b10f",
            SourceStartEpisodeId: "youku-source-secret", SourceStartEpisodeNumber: 5,
            MatchOrigin: "scored", SelectionEvidenceToken: "",
            AlignmentIntent: "ExplicitAnchor", MappingProtocolVersion: 22, PlanGeneration: 202034
        }],
        CompositePlan: {
            OrderedEpisodes: [{ ItemId: "local-dandan-secret", ParentSeasonNumber: 1,
                EpisodeNumber: 1, LocalDisplayLabel: "S01E01" },
                { ItemId: "local-youku-secret", ParentSeasonNumber: 1,
                    EpisodeNumber: 2, LocalDisplayLabel: "S01E02" }],
            Mappings: [{ LocalEpisodeItemId: "local-dandan-secret",
                Source: { ProviderId: "DandanID", MediaId: "11123" },
                SourceEpisodeId: "111230001", SourceEpisodeNumber: 1, Origin: "scored",
                AlignmentIntent: "DefaultZeroOffset" },
                { LocalEpisodeItemId: "local-youku-secret",
                    Source: { ProviderId: "YoukuID", MediaId: "cfd9e3748c8a4d52b10f" },
                    SourceEpisodeId: "youku-source-secret", SourceEpisodeNumber: 5, Origin: "scored",
                    AlignmentIntent: "ExplicitAnchor" }],
            UnmatchedRuns: []
        },
        CompositeGroups: [{
            IsTemporary: false, Site: "DandanID", CandidateId: "11123",
            SourceStartEpisodeId: "111230001", SourceStartEpisodeNumber: 1,
            AlignmentIntent: "DefaultZeroOffset",
            MatchOrigin: "scored", MatchScore: 0.934, ScoreOrigin: "search-confidence",
            Episodes: [{ ItemId: "local-dandan-secret", ParentSeasonNumber: 1,
                EpisodeNumber: 1, LocalDisplayLabel: "S01E01", EpisodeName: "本地标题一",
                SourceEpisodeNumber: 1, SourceEpisodeTitle: "来源标题一" }]
        }, {
            IsTemporary: false, Site: "YoukuID", CandidateId: "cfd9e3748c8a4d52b10f",
            SourceStartEpisodeId: "youku-source-secret", SourceStartEpisodeNumber: 5,
            AlignmentIntent: "ExplicitAnchor",
            MatchOrigin: "scored", MatchScore: 0.887, ScoreOrigin: "search-confidence",
            Episodes: [{ ItemId: "local-youku-secret", ParentSeasonNumber: 1,
                EpisodeNumber: 2, LocalDisplayLabel: "S01E02", EpisodeName: "本地标题二",
                SourceEpisodeNumber: 5, SourceEpisodeName: "来源标题五" }]
        }]
    };
    const liveCompositeDialog = hooks.openDialog("live CompositeGroups visibility");
    hooks.renderCompositeSeasonSummary(liveCompositeDialog,
        { Id: "one-punch", Type: "Series", Name: "一拳超人" },
        liveCompositeGroupsSeason, 0, [liveCompositeGroupsSeason], {}, {});
    const liveCompositeVisibleText = allVisibleText(liveCompositeDialog.body);
    assert(liveCompositeVisibleText.includes("精确集映射 · 弹弹Play · 匹配分：93.4（标题匹配）") &&
        liveCompositeVisibleText.includes("精确集映射 · 优酷 · 匹配分：88.7（标题匹配）") &&
        !liveCompositeVisibleText.includes("DandanID") && !liveCompositeVisibleText.includes("YoukuID") &&
        !liveCompositeVisibleText.includes("11123") &&
        !liveCompositeVisibleText.includes("cfd9e3748c8a4d52b10f") &&
        !liveCompositeVisibleText.includes("direct-episode-provider") &&
        !liveCompositeVisibleText.includes("来源从第"),
        "the actual CompositeGroups card path must expose only localized providers and scores in summaries");
    const liveMappingText = liveCompositeDialog.body.querySelectorAll(".danmuVirtualSeasonMappings")
        .map(allVisibleText).join(" ");
    assert(liveMappingText.includes("S01E01") && liveMappingText.includes("来源第 1 集") &&
        liveMappingText.includes("S01E02") && liveMappingText.includes("来源第 5 集") &&
        liveMappingText.includes("本地标题一") && liveMappingText.includes("来源标题一") &&
        liveMappingText.includes("本地标题二") && liveMappingText.includes("来源标题五") &&
        !liveMappingText.includes("DandanID") && !liveMappingText.includes("YoukuID") &&
        !liveMappingText.includes("111230001") && !liveMappingText.includes("youku-source-secret") &&
        !liveMappingText.includes("匹配分"),
        "actual CompositeGroups mapping rows must expose paired local/source titles without internal identities or scores");
    const liveWireSelections = hooks.compositeRequestSelections({}, liveCompositeGroupsSeason);
    assert(liveWireSelections.length === 2 && liveWireSelections[0].Site === "DandanID" &&
        liveWireSelections[0].CandidateId === "11123" && liveWireSelections[0].SourceStartEpisodeId === "111230001" &&
        liveWireSelections[1].Site === "YoukuID" &&
        liveWireSelections[1].CandidateId === "cfd9e3748c8a4d52b10f" &&
        liveWireSelections[1].SourceStartEpisodeId === "youku-source-secret" &&
        liveWireSelections[0].AlignmentIntent === "DefaultZeroOffset" &&
        liveWireSelections[1].AlignmentIntent === "ExplicitAnchor",
        "visible-text sanitization must preserve trusted server-confirmed anchors and alignment intent");
    liveCompositeDialog.forceClose();

    const draftDialog = hooks.openDialog("composite draft");
    const directGroup = hooks.compositeVirtualGroups(directSeason, compositeSelections)
        .find(group => group.kind === "mapped" && group.origin === "episode-provider-id");
    assert(directGroup && directGroup.episodes.map(episode => episode.ItemId).join(",") === "episode-5",
        "the direct Episode group fixture must represent one authoritative contiguous run");
    hooks.excludeCompositeRun(draftDialog, directSeason, directGroup);
    const exclusionParameters = hooks.compositeDraftParameters(draftDialog, directSeason, { compositePlan: "true" });
    assert(exclusionParameters.excludedLocalEpisodeItemIds === '["episode-5"]' &&
        Array.isArray(JSON.parse(exclusionParameters.excludedLocalEpisodeItemIds)) &&
        hooks.compositeExcludedItemIds(draftDialog, directSeason).join(",") === "episode-5",
        "dialog exclusions must be submitted as one scalar JSON field containing real Episode ItemIds");
    hooks.restoreCompositeRun(draftDialog, directSeason, ["episode-5"]);
    assert(hooks.compositeExcludedItemIds(draftDialog, directSeason).length === 0,
        "Restore must remove only the selected run's real ItemIds from dialog-local exclusions");

    const rangeGroup = hooks.compositeVirtualGroups(compositeSeason, {}).find(group => group.kind === "unmatched");
    const laterRangeGroup = { kind: "unmatched", episodes: rangeGroup.episodes.slice(1) };
    assert(hooks.temporaryRangeStateKey(compositeSeason, rangeGroup) !==
        hooks.temporaryRangeStateKey(compositeSeason, laterRangeGroup),
        "temporary-range state must be keyed by the actual local range rather than only its Season");
    const rangeKeyword = hooks.temporaryRangeKeyword(
        { Id: "series", Type: "Series", Name: "葬送的芙莉莲" },
        Object.assign({}, compositeSeason, { SeriesName: "葬送的芙莉莲", Candidates: [{ Id: "stale-full-season" }] }));
    const rangePayload = hooks.temporaryRangeSearchParameters(
        draftDialog,
        { Id: "series", Type: "Series", Name: "葬送的芙莉莲" },
        compositeSeason,
        rangeGroup,
        {},
        rangeKeyword);
    assert(rangeKeyword === "葬送的芙莉莲" && rangePayload.searchScope === "temporary-range" &&
        rangePayload.compositeStartEpisodeItemId === "episode-3" &&
        rangePayload.compositeEpisodeCount === "3" && rangePayload.keyword === "葬送的芙莉莲" &&
        rangePayload.mode === "rematch" && rangePayload.force === "true" &&
        rangePayload.excludedLocalEpisodeItemIds === "[]",
        "temporary entry must submit an immediate explicit range request with authoritative start/count and the unchanged default Series title");
    assert(hooks.temporaryRangeKeyword(
        { Id: "season", Type: "Season", Name: "Season fallback" },
        { SeasonName: "Season fallback", SeriesName: "" }) === "Season fallback",
        "a Season entry must use the same range path and fall back to its Season title only when Series title is empty");

    const rangeDialog = hooks.openDialog("range search");
    const broadSeasonCandidates = [{ Id: "stale-full-season", Site: "Stale" }];
    const rangeSeason = Object.assign({}, compositeSeason, {
        SeriesName: "葬送的芙莉莲",
        Candidates: broadSeasonCandidates
    });
    apiResponses.MatchPreview = {
        Seasons: [Object.assign({}, rangeSeason, {
            CandidateGeneration: "range-generation",
            Candidates: [{ Id: "fresh-range", Site: "Dandan", Name: "葬送的芙莉莲 第二季", EpisodeSize: 3,
                MatchScore: 0.82, ScoreOrigin: "search-confidence", SelectionEvidenceToken: "range-private" }]
        })]
    };
    const rangeCallCount = apiCalls.length;
    hooks.renderCompositeGroupPicker(rangeDialog,
        { Id: "series", Type: "Series", Name: "葬送的芙莉莲" },
        rangeSeason, 0, [rangeSeason], {}, {}, rangeGroup);
    assert(rangeDialog.title.textContent === "手动匹配未匹配临时季" &&
        rangeDialog.body.querySelectorAll(".danmuBusy").length === 1 &&
        rangeDialog.body.querySelectorAll(".danmuCandidate").length === 0 &&
        rangeDialog.footer.querySelectorAll(".danmuForceRefresh").length === 0 &&
        rangeSeason.Candidates === broadSeasonCandidates,
        "both temporary-range entry states must share the manual menu title while busy hides force refresh and preserves broad candidates");
    await waitUntil(() => rangeDialog.body.querySelectorAll(".danmuCandidate").length === 1,
        "automatic temporary-range request should settle before assertions");
    const automaticRangeCall = apiCalls.slice(rangeCallCount).find(call => call.option === "MatchPreview");
    assert(automaticRangeCall && automaticRangeCall.parameters.searchScope === "temporary-range" &&
        automaticRangeCall.parameters.keyword === "葬送的芙莉莲" &&
        rangeDialog.body.querySelectorAll(".danmuCandidate").length === 1 &&
        rangeDialog.body.querySelectorAll(".danmuCandidateDetailAction").length === 1 &&
        rangeDialog.footer.querySelectorAll(".danmuForceRefresh").length === 1 &&
        rangeSeason.Candidates === broadSeasonCandidates &&
        Object.keys(rangeDialog.temporaryRangeCandidates).length === 1,
        "temporary-group entry must retain range-keyed candidates, restore one footer control, and not contaminate the Season candidate list");
    const editedRangeStart = rangeDialog.body.querySelector(".danmuCompositeSourceStart");
    assert(editedRangeStart && editedRangeStart.dataset.danmuSourceStartDirty === "false",
        "a freshly rendered source-start picker must begin with clean local edit state");
    editedRangeStart.value = "5";
    await editedRangeStart.dispatch("input");
    editedRangeStart.value = "1";
    await editedRangeStart.dispatch("input");
    assert(editedRangeStart.dataset.danmuSourceStartDirty === "true",
        "changing source start and then returning it to E1 must retain explicit dirty intent until the picker resets");
    hooks.renderCompositeGroupPicker(rangeDialog,
        { Id: "series", Type: "Series", Name: "葬送的芙莉莲" },
        rangeSeason, 0, [rangeSeason], {}, {}, rangeGroup,
        { skipAutomaticRangeSearch: true });
    const resetRangeStart = rangeDialog.body.querySelector(".danmuCompositeSourceStart");
    assert(resetRangeStart && resetRangeStart.dataset.danmuSourceStartDirty === "false" &&
        resetRangeStart.value === "1",
        "rerendering or starting a fresh preview must clear transient source-start dirty state");
    const rangeCandidateRow = rangeDialog.body.querySelector(".danmuCandidate");
    const rangeScore = hooks.matchScoreLine({ MatchScore: 0.82, ScoreOrigin: "search-confidence" });
    assert(allVisibleText(rangeCandidateRow).split(rangeScore).length === 2,
        "a temporary-range candidate must render its match score exactly once");
    apiResponses.MatchCandidateDetails = request => ({
        Success: true, Generation: request.url.query.generation,
        SourceEpisodes: [{ Id: "range-source", Number: 1, Title: "范围来源标题" }]
    });
    await rangeCandidateRow.querySelector(".danmuCandidateDetailAction").dispatch("click");
    const rangeDetailCall = apiCalls.filter(call => call.option === "MatchCandidateDetails").pop();
    assert(rangeDetailCall.parameters.candidateId === "fresh-range" &&
        rangeDetailCall.parameters.candidateEvidence === "range-private" &&
        allVisibleText(rangeCandidateRow).includes("范围来源标题") &&
        !allVisibleText(rangeCandidateRow).includes("range-private"),
        "temporary-range candidates must reuse lazy details with private evidence and row-local expansion");
    const rangeSearch = rangeDialog.body.querySelector(".danmuSmartSearch");
    const rangeManualKeywordInput = rangeSearch.children[0];
    const rangeManualKeywordButton = rangeSearch.children[1];
    rangeManualKeywordInput.value = manualKeywordInput;
    await rangeManualKeywordInput.dispatch("input");
    apiResponses.MatchPreview = {
        Seasons: [Object.assign({}, rangeSeason, {
            MatchIntent: "manual-keyword", CandidateGeneration: "range-manual-keyword-generation",
            SearchCompletionDiagnostics: [
                { Provider: "RangeBroken", Status: "failed" },
                { Provider: "RangeCancelled", Status: "cancelled" }
            ],
            Candidates: [0, 1].map(index => ({
                Id: "range-duplicate", Site: "Dandan", Name: "Manual keyword range duplicate",
                MatchScore: index === 0 ? 0.36 : 0.91, ScoreOrigin: "search-confidence",
                Reason: "Range score reason " + index,
                MatchOrigin: "range-automatic-origin-" + index,
                DecisionReason: "range-automatic-decision-" + index,
                SelectionEvidenceToken: "range-manual-keyword-" + index
            }))
        })]
    };
    const manualKeywordRangeCallStart = apiCalls.length;
    await rangeManualKeywordButton.dispatch("click");
    await waitUntil(() => rangeDialog.body.querySelectorAll(".danmuCandidate").length === 2,
        "explicit temporary-range manual-keyword search should rerender both duplicate rows");
    const submittedManualKeywordRange = apiCalls.slice(manualKeywordRangeCallStart)
        .find(call => call.option === "MatchPreview");
    const savedRangeState = Object.values(rangeDialog.temporaryRangeCandidates)[0];
    const manualKeywordRangeRows = rangeDialog.body.querySelectorAll(".danmuCandidate");
    const manualKeywordRangeText = allVisibleText(rangeDialog.body);
    assert(submittedManualKeywordRange.parameters.mode === "manual-keyword" &&
        submittedManualKeywordRange.parameters.keyword === trimmedManualKeyword &&
        savedRangeState.matchIntent === "manual-keyword" &&
        savedRangeState.searchCompletionDiagnostics.length === 2 &&
        manualKeywordRangeText.includes("RangeBroken 失败") && !manualKeywordRangeText.includes("RangeCancelled") &&
        manualKeywordRangeRows.every(row => !row.children[0].checked) &&
        allVisibleText(manualKeywordRangeRows[0]).includes("匹配分：36（标题匹配）") &&
        allVisibleText(manualKeywordRangeRows[1]).includes("匹配分：91（标题匹配）") &&
        allVisibleText(manualKeywordRangeRows[0]).includes("评分理由：Range score reason 0") &&
        allVisibleText(manualKeywordRangeRows[1]).includes("评分理由：Range score reason 1") &&
        !manualKeywordRangeText.includes("来源：") && !manualKeywordRangeText.includes("决策：") &&
        !manualKeywordRangeText.includes("range-automatic-origin-") &&
        !manualKeywordRangeText.includes("range-automatic-decision-"),
        "temporary-range must trim the keyword, refresh MatchIntent, trust backend score order, retain duplicates, and hide automatic decisions");
    manualKeywordRangeRows[1].children[0].checked = true;
    apiResponses.MatchPreview = { Seasons: [rangeSeason] };
    const manualKeywordRangePlanStart = apiCalls.length;
    const applyManualKeywordRange = rangeDialog.footer.children.find(button => button.textContent === "应用到此临时季");
    await applyManualKeywordRange.dispatch("click");
    const manualKeywordRangePlanCall = apiCalls.slice(manualKeywordRangePlanStart).find(call =>
        call.option === "MatchPreview" && call.parameters.compositePlan === "true");
    const submittedRangeSelections = manualKeywordRangePlanCall
        ? JSON.parse(manualKeywordRangePlanCall.parameters.compositeSelections) : [];
    assert(manualKeywordRangePlanCall &&
        manualKeywordRangePlanCall.parameters.compositeSelections.includes("range-manual-keyword-1") &&
        submittedRangeSelections.some(selection => selection.LocalStartEpisodeItemId === "episode-3" &&
            selection.AlignmentIntent === "ExplicitAnchor") &&
        submittedRangeSelections.every(selection => !selection.Mappings && !selection.CommentId),
        "an explicitly selected non-first range must submit ExplicitAnchor candidate intent without browser-authored mappings");
    rangeDialog.forceClose();
    delete apiResponses.MatchPreview;
    delete apiResponses.MatchCandidateDetails;

    const editableSelections = { __compositeSelections: {} };
    hooks.discardIncompatibleSeasonDrafts(draftDialog, [directSeason], editableSelections);
    editableSelections.__compositeSelections["series::season-composite"] = [{
        LocalStartEpisodeItemId: "episode-3", RequestedEpisodeCount: 2,
        Source: { ProviderId: "Dandan", MediaId: "frieren-s2" },
        SourceStartEpisodeNumber: 1, Origin: "manual"
    }];
    hooks.renderCompositeSeasonSummary(draftDialog,
        { Id: "series", Type: "Series", Name: "葬送的芙莉莲" },
        directSeason, 0, [directSeason], editableSelections, {});
    const sanitizedText = allVisibleText(draftDialog.body);
    assert(sanitizedText.includes("优酷") && sanitizedText.includes("精确集映射") &&
        !sanitizedText.includes("YoukuID") && !sanitizedText.includes("2a5659587f87497d9aab") &&
        !sanitizedText.includes("direct-source") && !sanitizedText.includes("direct-episode-provider") &&
        !sanitizedText.includes("ItemId") && !sanitizedText.includes("server-only"),
        "virtual-season visible text must localize the provider and hide every internal identity/origin token");
    const mappingDetails = draftDialog.body.querySelectorAll(".danmuVirtualSeasonMappings");
    assert(mappingDetails.length === 3 && source.includes(".danmuVirtualSeasonMappings{grid-column:1 / -1;min-width:0") &&
        source.includes("@media(max-width:520px)") && source.includes(".danmuVirtualSeasonMappings{grid-column:1 / -1;width:100%}"),
        "native mapping details must occupy the full card width on desktop and narrow/mobile layouts");
    assert(mappingDetails.every(details => allVisibleText(details).split(" ").every(fragment =>
        !fragment.includes("ItemId") && !fragment.includes("source-") && !fragment.includes("frieren-") &&
        !fragment.includes("匹配分"))),
        "expanded rows must contain only local/source episode coordinates, never IDs, provider, score, or provenance");
    assert(mappingDetails.some(details => allVisibleText(details).includes("本地 S01E05 → 来源第 5 集")),
        "an eligible Episode must use its server-authored local SxxExx coordinate in the compact mapping row");
    assert(compactSelections.every(selection => selection.MappingProtocolVersion === 22 &&
        selection.PlanGeneration === compositeSeason.PlanGeneration &&
        selection.MappingProtocolGeneration === undefined) &&
        compactSelections[1].LocalStartEpisodeItemId === "episode-3" &&
        compactSelections[1].CandidateId === "frieren-s2",
        "sanitizing visible text must preserve compact wire identity and add the V22 generation fence");
    const editableActionLabels = draftDialog.body.querySelectorAll(".danmuSmartButton")
        .map(button => button.textContent);
    assert(editableActionLabels.filter(label => label === "重新匹配").length === 3 &&
        editableActionLabels.filter(label => label === "移除").length === 3 &&
        source.includes('summary.textContent = "查看逐集映射（"'),
        "manual, searched, and direct-Episode contiguous cards must all expose view-mapping, rematch, and remove actions");

    apiResponses.MatchPreview = function (request) {
        const excluded = JSON.parse(request.url.query.excludedLocalEpisodeItemIds || "[]");
        const remainingMappings = directSeason.CompositePlan.Mappings.filter(mapping =>
            !excluded.includes(mapping.LocalEpisodeItemId));
        return {
            Seasons: [Object.assign({}, directSeason, {
                SeasonName: "Server-renamed season",
                Year: 2027,
                CompositePlan: Object.assign({}, directSeason.CompositePlan, {
                    Mappings: remainingMappings,
                    UnmatchedRuns: [{ Episodes: directSeason.CompositePlan.OrderedEpisodes.filter(episode =>
                        !remainingMappings.some(mapping => mapping.LocalEpisodeItemId === episode.ItemId)) }],
                    EffectiveExcludedLocalEpisodeItemIds: excluded
                })
            })]
        };
    };
    const directRemove = draftDialog.body.querySelectorAll(".danmuSmartButton")
        .filter(button => button.textContent === "移除").pop();
    await directRemove.dispatch("click");
    const removalCall = apiCalls.filter(call => call.option === "MatchPreview").pop();
    assert(removalCall && removalCall.parameters.excludedLocalEpisodeItemIds === '["episode-5"]',
        "direct-group remove must submit the exact real Episode ItemId in the authoritative rebuild payload");
    assert(hooks.compositeExcludedItemIds(draftDialog, directSeason).join(",") === "episode-5",
        "the authoritative rebuild must retain the effective exclusion in dialog-local state");
    assert(draftDialog.body.querySelectorAll(".danmuSmartButton").some(button =>
        button.textContent.indexOf("恢复 ") === 0),
        "the rebuilt overview must expose Restore without touching durable metadata");
    const directRestore = draftDialog.body.querySelectorAll(".danmuSmartButton")
        .find(button => button.textContent.indexOf("恢复 ") === 0);
    await directRestore.dispatch("click");
    const directRestoreCall = apiCalls.filter(call => call.option === "MatchPreview").pop();
    assert(directRestoreCall && directRestoreCall.parameters.excludedLocalEpisodeItemIds === "[]" &&
        JSON.parse(directRestoreCall.parameters.compositeSelections).every(selection =>
            selection.CandidateId !== "direct-episode") &&
        hooks.compositeExcludedItemIds(draftDialog, directSeason).length === 0,
        "direct Episode groups must restore by local Episode ItemIds and server reconstruction, never by resubmitting their provider id as a selection");
    draftDialog.forceClose();
    assert(hooks.compositeExcludedItemIds(draftDialog, directSeason).length === 0,
        "closing the dialog must discard its exclusion draft");
    delete apiResponses.MatchPreview;

    function authoritativeCompositeResponse(request, omitCandidateId) {
        const query = request.url.query;
        const excluded = JSON.parse(query.excludedLocalEpisodeItemIds || "[]");
        const requested = JSON.parse(query.compositeSelections || "[]")
            .filter(selection => selection.CandidateId !== omitCandidateId);
        const ordered = directSeason.CompositePlan.OrderedEpisodes;
        const mappings = [];
        requested.forEach(selection => {
            const start = ordered.findIndex(episode => episode.ItemId === selection.LocalStartEpisodeItemId);
            for (let offset = 0; start >= 0 && offset < selection.RequestedEpisodeCount; offset++) {
                const episode = ordered[start + offset];
                if (!episode || excluded.includes(episode.ItemId)) break;
                mappings.push({
                    LocalEpisodeItemId: episode.ItemId,
                    Source: { ProviderId: selection.Site, MediaId: selection.CandidateId },
                    SourceEpisodeNumber: (selection.SourceStartEpisodeNumber || 1) + offset,
                    Origin: selection.MatchOrigin || "manual",
                    AlignmentIntent: selection.AlignmentIntent
                });
            }
        });
        if (!excluded.includes("episode-5")) {
            mappings.push(directSeason.CompositePlan.Mappings.find(mapping =>
                mapping.LocalEpisodeItemId === "episode-5"));
        }
        const unmatched = ordered.filter(episode => !mappings.some(mapping =>
            mapping.LocalEpisodeItemId === episode.ItemId));
        return {
            Seasons: [Object.assign({}, directSeason, {
                CompositeSelections: requested,
                CompositePlan: {
                    OrderedEpisodes: ordered,
                    Mappings: mappings,
                    UnmatchedRuns: unmatched.length ? [{ Episodes: unmatched }] : [],
                    EffectiveExcludedLocalEpisodeItemIds: excluded
                }
            })]
        };
    }

    const searchedDialog = hooks.openDialog("searched selection restore");
    const searchedSelections = { __compositeSelections: {} };
    hooks.discardIncompatibleSeasonDrafts(searchedDialog, [directSeason], searchedSelections);
    searchedSelections.__compositeSelections["series::season-composite"] = [{
        LocalStartEpisodeItemId: "episode-3", RequestedEpisodeCount: 2,
        Source: { ProviderId: "Dandan", MediaId: "frieren-s2" },
        SourceStartEpisodeNumber: 1, AlignmentIntent: "ExplicitAnchor", Origin: "manual"
    }];
    const searchedSeasons = [directSeason];
    apiResponses.MatchPreview = request => authoritativeCompositeResponse(request);
    hooks.renderSeriesPicker(searchedDialog,
        { Id: "series", Type: "Series", Name: "葬送的芙莉莲" }, searchedSeasons, searchedSelections, {});
    const searchedRemove = searchedDialog.body.querySelectorAll(".danmuSmartButton")
        .filter(button => button.textContent === "移除")[0];
    await searchedRemove.dispatch("click");
    const searchedRemoveCall = apiCalls.filter(call => call.option === "MatchPreview").pop();
    const afterRemoveSelections = JSON.parse(searchedRemoveCall.parameters.compositeSelections);
    const removedSnapshot = hooks.compositeDraftSeasonState(
        searchedDialog, searchedSeasons[0], false).removedRuns[0];
    assert(afterRemoveSelections.map(selection => selection.CandidateId).join(",") === "frieren-s2" &&
        removedSnapshot && removedSnapshot.selections.length === 1 &&
        removedSnapshot.selections[0].CandidateId === "frieren-s1" &&
        hooks.compositeVirtualGroups(searchedSeasons[0], {}).some(group =>
            group.kind === "mapped" && group.source.MediaId === "frieren-s2"),
        "removing one searched mapped group must omit its overlap, preserve other selections, and retain a dialog-local restore snapshot");

    let searchedRestore = searchedDialog.body.querySelectorAll(".danmuSmartButton")
        .find(button => button.textContent.indexOf("恢复 ") === 0);
    await searchedRestore.dispatch("click");
    const searchedRestoreCall = apiCalls.filter(call => call.option === "MatchPreview").pop();
    const restoreCandidateIds = JSON.parse(searchedRestoreCall.parameters.compositeSelections)
        .map(selection => selection.CandidateId).sort().join(",");
    assert(restoreCandidateIds === "frieren-s1,frieren-s2" &&
        hooks.compositeDraftSeasonState(searchedDialog, searchedSeasons[0], false).removedRuns.length === 0 &&
        hooks.compositePlanCoversItemIds(searchedSeasons[0], ["episode-1", "episode-2"]),
        "restoring a non-direct group must re-add its snapshot only after removing old overlap and accept it only after server revalidation");

    hooks.renderSeriesPicker(searchedDialog,
        { Id: "series", Type: "Series", Name: "葬送的芙莉莲" }, searchedSeasons, {}, {});
    const expiredRemove = searchedDialog.body.querySelectorAll(".danmuSmartButton")
        .filter(button => button.textContent === "移除")[0];
    apiResponses.MatchPreview = request => authoritativeCompositeResponse(request);
    await expiredRemove.dispatch("click");
    apiResponses.MatchPreview = request => authoritativeCompositeResponse(request, "frieren-s1");
    searchedRestore = searchedDialog.body.querySelectorAll(".danmuSmartButton")
        .find(button => button.textContent.indexOf("恢复 ") === 0);
    await searchedRestore.dispatch("click");
    assert(hooks.compositeDraftSeasonState(searchedDialog, searchedSeasons[0], false).removedRuns.length === 1 &&
        hooks.compositeVirtualGroups(searchedSeasons[0], {}).some(group =>
            group.kind === "unmatched" && group.episodes.some(episode => episode.ItemId === "episode-1")),
        "an expired restored selection must fail validation, retain its Restore snapshot, and keep the local Episodes unmatched");
    searchedDialog.forceClose();
    assert(hooks.compositeDraftSeasonState(searchedDialog, searchedSeasons[0], false).removedRuns.length === 0 &&
        hooks.compositeExcludedItemIds(searchedDialog, searchedSeasons[0]).length === 0,
        "closing the dialog must discard removed-selection snapshots and exclusions together");
    delete apiResponses.MatchPreview;

    function rematchFixture(kind, suffix) {
        const seasonId = "rematch-" + kind + "-" + suffix;
        const ordered = [1, 2].map(number => ({
            ItemId: seasonId + "-episode-" + number, ParentSeasonNumber: 1,
            EpisodeNumber: number, LocalDisplayLabel: "S01E0" + number
        }));
        const mapping = {
            LocalEpisodeItemId: ordered[0].ItemId,
            Source: { ProviderId: "Dandan", MediaId: "mapped-source" },
            SourceEpisodeId: "mapped-source-1", SourceEpisodeNumber: 1, Origin: "scored",
            AlignmentIntent: "DefaultZeroOffset"
        };
        const season = {
            SeriesId: "rematch-series-" + suffix, SeasonId: seasonId, SeasonNumber: 1,
            SeasonName: "Rematch " + suffix, EpisodeCount: 2,
            MappingProtocolVersion: 22, PlanGeneration: 9100 + suffix.length,
            RequiresCompositeMapping: true,
            CompositeSelections: [{ LocalStartEpisodeItemId: ordered[0].ItemId,
                RequestedEpisodeCount: 1, Site: "Dandan", CandidateId: "mapped-source",
                SourceStartEpisodeId: "mapped-source-1", SourceStartEpisodeNumber: 1,
                MatchOrigin: "scored", SelectionEvidenceToken: "",
                AlignmentIntent: "DefaultZeroOffset", MappingProtocolVersion: 22,
                PlanGeneration: 9100 + suffix.length }],
            CompositePlan: {
                OrderedEpisodes: ordered, Mappings: [mapping],
                UnmatchedRuns: [{ Episodes: [ordered[1]] }]
            },
            CompositeGroups: [{ IsTemporary: false, Site: "Dandan", CandidateId: "mapped-source",
                MatchOrigin: "scored", AlignmentIntent: "DefaultZeroOffset",
                Episodes: [{ ItemId: ordered[0].ItemId,
                    ParentSeasonNumber: 1, EpisodeNumber: 1, LocalDisplayLabel: "S01E01" }] },
                { IsTemporary: true, Episodes: [{ ItemId: ordered[1].ItemId,
                    ParentSeasonNumber: 1, EpisodeNumber: 2, LocalDisplayLabel: "S01E02" }] }]
        };
        const selections = { __compositeSelections: {} };
        return { season: season, selections: selections, ordered: ordered, mapping: mapping };
    }

    async function verifyRematchReturn(entry, kind, returnPath, suffix) {
        const fixture = rematchFixture(kind, suffix);
        const seasons = [fixture.season];
        const dialog = hooks.openDialog("rematch " + entry + " " + kind);
        const item = { Id: entry === "series" ? fixture.season.SeriesId : fixture.season.SeasonId,
            Type: entry === "series" ? "Series" : "Season", Name: "Rematch" };
        const keywords = { preserved: "keep" };
        hooks.discardIncompatibleSeasonDrafts(dialog, seasons, fixture.selections);
        if (kind === "manual") {
            fixture.selections.__compositeSelections[fixture.season.SeriesId + "::" + fixture.season.SeasonId] = [{
                LocalStartEpisodeItemId: fixture.ordered[1].ItemId, RequestedEpisodeCount: 1,
                Source: { ProviderId: "Youku", MediaId: "manual-source" },
                SourceStartEpisodeNumber: 4, AlignmentIntent: "ExplicitAnchor",
                Origin: "manual", SelectionEvidenceToken: "manual-private"
            }];
        }
        const state = hooks.compositeDraftSeasonState(dialog, fixture.season, true);
        state.exclusions.push("pre-existing-exclusion");
        state.removedRuns.push({ start: "pre-existing-run", itemIds: ["pre-existing-exclusion"],
            label: "preserved run", selections: [] });
        const confirmed = JSON.parse(JSON.stringify(fixture.season));
        confirmed.CompositePlan.Mappings = kind === "mapped" ? [] : [fixture.mapping];
        confirmed.CompositePlan.UnmatchedRuns = [{ Episodes: kind === "mapped" ? fixture.ordered : [fixture.ordered[1]] }];
        apiResponses.MatchPreview = request => {
            if (request.url.query.searchScope === "temporary-range") {
                return { Seasons: [Object.assign({}, confirmed, { CandidateGeneration: "rematch-range",
                    Candidates: [{ Site: "Dandan", Id: "rematch-candidate", Name: "Rematch candidate",
                        SelectionEvidenceToken: "rematch-private" }] })] };
            }
            return { Seasons: [confirmed] };
        };
        if (entry === "series") hooks.renderSeriesPicker(dialog, item, seasons, fixture.selections, keywords);
        else {
            dialog.compositeTargetState = {
                seasonId: fixture.season.SeasonId, selections: fixture.selections, keywords: keywords
            };
            hooks.renderCompositeTargetPicker(dialog, item, seasons[0]);
        }
        const beforeSeason = JSON.stringify(seasons[0]);
        const beforeSelections = JSON.stringify(fixture.selections);
        const beforeKeywords = JSON.stringify(keywords);
        const beforeDraft = JSON.stringify(dialog.compositeDraft);
        const beforePayload = JSON.stringify(hooks.compositeRequestSelections(fixture.selections, seasons[0]));
        const targetCard = dialog.body.querySelectorAll(".danmuVirtualSeason").find(card =>
            kind === "manual" ? allVisibleText(card).includes("手动匹配") : allVisibleText(card).includes("精确集映射"));
        const rematch = targetCard.querySelectorAll(".danmuSmartButton")
            .find(button => button.textContent === "重新匹配");
        await rematch.dispatch("click", { isTrusted: true });
        await waitUntil(() => dialog.body.querySelectorAll(".danmuCandidate").length === 1,
            entry + "/" + kind + " rematch should enter the temporary-range picker");
        const detachedRematchBody = dialog.body.children[0];
        const detachedRematchApiCount = apiCalls.length;
        await rematch.dispatch("click", { isTrusted: true });
        assert(dialog.body.children[0] === detachedRematchBody && apiCalls.length === detachedRematchApiCount &&
            hooks.navigationContextDepth(dialog) === 1,
            entry + "/" + kind + " detached rematch must not render, request, or push again");
        if (returnPath === "android") documentStub.dispatchCommand("back");
        else await dialog.footer.children.find(button => button.textContent === "返回总览").dispatch("click");
        assert(JSON.stringify(seasons[0]) === beforeSeason &&
            JSON.stringify(fixture.selections) === beforeSelections &&
            JSON.stringify(keywords) === beforeKeywords &&
            JSON.stringify(dialog.compositeDraft) === beforeDraft &&
            JSON.stringify(hooks.compositeRequestSelections(fixture.selections, seasons[0])) === beforePayload &&
            dialog.body.querySelectorAll(".danmuVirtualSeason").some(card =>
                allVisibleText(card).includes(kind === "manual" ? "手动匹配" : "精确集映射")),
            entry + "/" + kind + " rematch return must restore the exact overview, draft, and download payload");
        dialog.forceClose();
        delete apiResponses.MatchPreview;
    }

    await verifyRematchReturn("series", "mapped", "footer", "a");
    await verifyRematchReturn("series", "manual", "android", "b");
    await verifyRematchReturn("season", "mapped", "android", "c");
    await verifyRematchReturn("season", "manual", "footer", "d");

    function deferredTransport() {
        let resolve;
        let reject;
        const promise = new Promise((onResolve, onReject) => {
            resolve = onResolve;
            reject = onReject;
        });
        promise.abortCount = 0;
        promise.abort = function () {
            promise.abortCount++;
            reject({ name: "AbortError" });
        };
        return { promise, resolve, reject };
    }

    const searchDialog = hooks.openDialog("search operation fencing");
    const firstTransport = deferredTransport();
    const secondTransport = deferredTransport();
    const pendingTransports = [firstTransport.promise, secondTransport.promise];
    apiResponses.MatchPreview = function () { return pendingTransports.shift(); };
    let restored = [];
    const firstSearch = hooks.runDialogSearch(searchDialog, "search-item", "provider-search", {},
        "searching first", status => restored.push("first:" + status));
    await new Promise(resolve => setImmediate(resolve));
    const firstOperation = apiCalls.filter(call => call.option === "MatchPreview").pop().parameters.searchOperationId;
    const secondSearch = hooks.runDialogSearch(searchDialog, "search-item", "detail-resolution", {},
        "resolving details", status => restored.push("second:" + status));
    await new Promise(resolve => setImmediate(resolve));
    const secondCall = apiCalls.filter(call => call.option === "MatchPreview").pop();
    assert(firstTransport.promise.abortCount === 1 && secondCall.parameters.searchScope === "detail-resolution" &&
        secondCall.parameters.searchOperationId !== firstOperation,
        "a new detail-resolution request must abort and cancel the previous provider-search using a distinct operation id");
    secondTransport.resolve({ result: "newest" });
    assert(await firstSearch === null && (await secondSearch).result === "newest" && restored.length === 0,
        "a late or aborted earlier search must not restore or overwrite the authoritative newest response");
    assert(apiCalls.some(call => call.option === "CancelSearch" && call.parameters.searchOperationId === firstOperation),
        "superseding a request must address CancelSearch with the exact prior client operation id");

    const cancelledTransport = deferredTransport();
    apiResponses.MatchPreview = function () { return cancelledTransport.promise; };
    const cancelledSearch = hooks.runDialogSearch(searchDialog, "search-item", "provider-search", {},
        "searching", status => restored.push("cancel:" + status));
    await new Promise(resolve => setImmediate(resolve));
    const cancelledOperation = apiCalls.filter(call => call.option === "MatchPreview").pop().parameters.searchOperationId;
    const visibleCancel = searchDialog.footer.children.find(button => button.textContent === "取消搜索");
    assert(visibleCancel, "a live provider-search must expose a visible cancellation control");
    await visibleCancel.dispatch("click");
    assert(cancelledTransport.promise.abortCount === 1,
        "visible cancellation must abort a cancellable browser transport");
    assert(await cancelledSearch === null && restored.includes("cancel:cancelled") &&
        apiCalls.some(call => call.option === "CancelSearch" && call.parameters.searchOperationId === cancelledOperation) &&
        searchDialog.activeSearch === null && !searchDialog.androidBackLocked,
        "user cancellation must call the backend endpoint and always restore busy state");

    const closeTransport = deferredTransport();
    apiResponses.MatchPreview = function () { return closeTransport.promise; };
    const closeSearch = hooks.runDialogSearch(searchDialog, "search-item", "provider-search", {},
        "searching", status => restored.push("close:" + status));
    await new Promise(resolve => setImmediate(resolve));
    const closeOperation = apiCalls.filter(call => call.option === "MatchPreview").pop().parameters.searchOperationId;
    assert(searchDialog.close() && closeTransport.promise.abortCount === 1 && await closeSearch === null &&
        apiCalls.some(call => call.option === "CancelSearch" && call.parameters.searchOperationId === closeOperation),
        "closing a dialog must cancel its live server search without allowing a late response to mutate closed UI");

    const cancelledSmartDialog = hooks.openDialog("cancelled whole-Series preview");
    const cancelledSmartTransport = deferredTransport();
    apiResponses.MatchPreview = function () { return cancelledSmartTransport.promise; };
    const cancelledSmartCallStart = apiCalls.length;
    const cancelledSmartRun = hooks.runSmartDownload(
        { Id: "series-cancelled-preview", Type: "Series", Name: "Cancelled Series" }, cancelledSmartDialog);
    await new Promise(resolve => setImmediate(resolve));
    const cancelledSmartButton = cancelledSmartDialog.footer.children
        .find(button => button.textContent === "取消搜索");
    await cancelledSmartButton.dispatch("click");
    await cancelledSmartRun;
    assert(apiCalls.slice(cancelledSmartCallStart)
        .filter(call => call.option === "MatchPreview").length === 1 &&
        cancelledSmartDialog.footer.children.some(button => button.textContent === "重试搜索"),
        "cancelling a whole-Series preview must not be mistaken for a decoded empty response or start another request");
    cancelledSmartDialog.forceClose();

    const errorDialog = hooks.openDialog("search error recovery");
    apiResponses.MatchPreview = function () { return Promise.reject(new Error("provider unavailable")); };
    const errorResult = await hooks.runDialogSearch(errorDialog, "search-item", "provider-search", {},
        "searching", status => {
            restored.push("error:" + status);
            hooks.setBusy(errorDialog, "restored controls");
            errorDialog.androidBackLocked = false;
        });
    assert(errorResult === null && restored.includes("error:error") && !errorDialog.androidBackLocked &&
        errorDialog.activeSearch === null,
        "provider errors must run the same finally cleanup path and release dialog controls");
    errorDialog.forceClose();

    const errorSmartDialog = hooks.openDialog("failed whole-Series preview");
    const errorSmartCallStart = apiCalls.length;
    await hooks.runSmartDownload(
        { Id: "series-failed-preview", Type: "Series", Name: "Failed Series" }, errorSmartDialog);
    assert(apiCalls.slice(errorSmartCallStart).filter(call => call.option === "MatchPreview").length === 1 &&
        errorSmartDialog.footer.children.some(button => button.textContent === "重试搜索"),
        "a whole-Series transport or HTTP failure must not enter the decoded-empty retry path");
    errorSmartDialog.forceClose();

    const malformedDialog = hooks.openDialog("malformed preview");
    apiResponses.MatchPreview = {};
    const malformedCallStart = apiCalls.length;
    await hooks.runSmartDownload({ Id: "malformed", Type: "Movie", Name: "Malformed" }, malformedDialog);
    assert(apiCalls.slice(malformedCallStart).filter(call => call.option === "MatchPreview").length === 1 &&
        !malformedDialog.androidBackLocked && malformedDialog.footer.children.some(button =>
            button.textContent === "重试搜索"),
        "non-Series malformed previews must remain single-request and restore a retryable dialog instead of leaving busy controls stuck");
    malformedDialog.forceClose();
    delete apiResponses.MatchPreview;

    const closableDialog = hooks.openDialog("closable");
    await closableDialog.overlay.dispatch("click");
    assert(closableDialog.overlay.isConnected && hooks.activeDialogCount() === 1,
        "a backdrop click must not dismiss a closable dialog");
    let unrelatedClicks = 0;
    documentStub.body.addEventListener("click", function () { unrelatedClicks++; });
    await documentStub.body.dispatch("click");
    assert(unrelatedClicks === 1 && closableDialog.overlay.isConnected,
        "dialog handling must not intercept unrelated Emby page clicks");
    const closableClose = closableDialog.overlay.children[0].children[0].children[1];
    await closableClose.dispatch("click");
    assert(!closableDialog.overlay.isConnected && hooks.activeDialogCount() === 0 &&
        (documentStub.listeners.keydown || []).length === 0,
        "the close action should dispose a closable dialog and its Escape listener");

    const protectedDialog = hooks.openDialog("protected");
    protectedDialog.closable = false;
    const protectedClose = protectedDialog.overlay.children[0].children[0].children[1];
    await protectedClose.dispatch("click");
    await protectedDialog.overlay.dispatch("click");
    const protectedEscape = documentStub.dispatchKey("Escape");
    assert(protectedDialog.overlay.isConnected && !protectedEscape.defaultPrevented,
        "close and Escape must preserve protected dialog state");
    assert(protectedDialog.forceClose() && !protectedDialog.forceClose() &&
        !protectedDialog.overlay.isConnected && (documentStub.listeners.keydown || []).length === 0,
        "force close must bypass protection and shared disposal must be idempotent");

    let hostCommandCalls = 0;
    documentStub.addEventListener("command", function () { hostCommandCalls++; });
    const androidHistoryBaseline = Object.assign({}, historyCalls);
    const androidHostRouteBaseline = context.window.location.href;
    const androidDialog = hooks.openDialog("android-command-secondary");
    let returnedToParent = 0;
    androidDialog.setBackHandler(function () {
        returnedToParent++;
        androidDialog.setBackHandler(null);
    });
    const childCommand = documentStub.dispatchCommand("back");
    assert(hooks.dialogBackMode(androidDialog) === "android-command" &&
        returnedToParent === 1 && androidDialog.overlay.isConnected && childCommand.defaultPrevented &&
        !childCommand.propagationStopped && hostCommandCalls === 1 &&
        (documentStub.listeners.backbutton || []).length === 0,
        "Android command must return a secondary view once without stopping propagation or registering backbutton");
    const topCommand = documentStub.dispatchCommand("back");
    assert(!androidDialog.overlay.isConnected && topCommand.defaultPrevented,
        "Android command at the top level must close exactly one dialog");

    const protectedAndroidDialog = hooks.openDialog("protected-android-command");
    protectedAndroidDialog.closable = false;
    documentStub.dispatchCommand("back");
    assert(protectedAndroidDialog.overlay.isConnected,
        "a protected Android command dialog must remain open");
    protectedAndroidDialog.forceClose();

    const busyBackDialog = hooks.openDialog("busy-android-command");
    hooks.setBusy(busyBackDialog, "searching");
    documentStub.dispatchCommand("back");
    assert(busyBackDialog.overlay.isConnected && busyBackDialog.androidBackLocked,
        "a busy Android command dialog must consume ownership while remaining open");
    busyBackDialog.forceClose();

    const lowerCommandDialog = hooks.openDialog("lower android-command");
    const upperCommandDialog = hooks.openDialog("upper android-command");
    documentStub.dispatchCommand("back");
    assert(!upperCommandDialog.overlay.isConnected && lowerCommandDialog.overlay.isConnected,
        "only the topmost connected Android command dialog may handle one command");
    lowerCommandDialog.forceClose();

    const noOverlayCommand = documentStub.dispatchCommand("back");
    const nonBackDialog = hooks.openDialog("non-back command");
    const nonBackCommand = documentStub.dispatchCommand("refresh");
    assert(!noOverlayCommand.defaultPrevented && !nonBackCommand.defaultPrevented &&
        nonBackDialog.overlay.isConnected,
        "no-overlay and non-back commands must pass through unchanged");
    nonBackDialog.forceClose();

    const failedCancelDialog = hooks.openDialog("failed cancellation");
    documentStub.dispatchCommand("back", { cancelable: false });
    documentStub.dispatchCommand("back", { omitPreventDefault: true });
    documentStub.dispatchCommand("back", { preventBehavior: "noop" });
    documentStub.dispatchCommand("back", { preventBehavior: "throw" });
    assert(failedCancelDialog.overlay.isConnected,
        "noncancelable, ineffective, and throwing cancellation must not mutate Smart Match");
    failedCancelDialog.forceClose();

    const preexistingDialog = hooks.openDialog("preexisting cancellation");
    documentStub.dispatchCommand("back", { defaultPrevented: true, preventBehavior: "noop" });
    assert(!preexistingDialog.overlay.isConnected,
        "a preexisting cancellation must still invoke the eligible handler once");

    const falseHandlerDialog = hooks.openDialog("false command handler");
    falseHandlerDialog.handleCommandBack = function () { return false; };
    const falseHandlerCommand = documentStub.dispatchCommand("back");
    assert(falseHandlerCommand.defaultPrevented && falseHandlerDialog.overlay.isConnected,
        "a false handler must retain cancellation without fallback");
    falseHandlerDialog.forceClose();
    const throwingHandlerDialog = hooks.openDialog("throwing command handler");
    throwingHandlerDialog.handleCommandBack = function () { throw new Error("injected command failure"); };
    const throwingHandlerCommand = documentStub.dispatchCommand("back");
    assert(throwingHandlerCommand.defaultPrevented && throwingHandlerDialog.overlay.isConnected,
        "a throwing handler must retain cancellation without fallback");
    throwingHandlerDialog.forceClose();
    let hostPopParentCalls = 0;
    const hostPopAndroidOne = hooks.openDialog("host pop android one");
    const hostPopAndroidTwo = hooks.openDialog("host pop android two");
    hostPopAndroidOne.setBackHandler(function () { hostPopParentCalls++; });
    hostPopAndroidTwo.setBackHandler(function () { hostPopParentCalls++; });
    (windowListeners.popstate || []).slice().forEach(listener => listener({ state: null }));
    assert(!hostPopAndroidOne.overlay.isConnected && !hostPopAndroidTwo.overlay.isConnected &&
        hostPopParentCalls === 0,
        "host popstate must dispose the complete Android overlay stack without internal parent return");
    assert(historyCalls.pushState === androidHistoryBaseline.pushState &&
        historyCalls.replaceState === androidHistoryBaseline.replaceState &&
        historyCalls.back === androidHistoryBaseline.back &&
        context.window.location.href === androidHostRouteBaseline,
        "all Android command paths must preserve the host route and perform zero dialog-owned history operations");

    const busyForceDialog = hooks.openDialog("busy force state");
    busyForceDialog.forceRefresh = true;
    hooks.setBusy(busyForceDialog, "searching");
    assert(busyForceDialog.footer.querySelectorAll(".danmuForceRefresh").length === 0 &&
        busyForceDialog.forceRefresh,
        "busy rendering must hide the force-refresh control without resetting the user's selected state");
    hooks.renderSeriesPicker(busyForceDialog,
        { Id: "busy-force-series", Type: "Series", Name: "Series" }, [], {}, {});
    assert(busyForceDialog.footer.querySelectorAll(".danmuForceRefresh").length === 1 &&
        busyForceDialog.footer.querySelector(".danmuForceRefresh").children[0].checked,
        "the saved force-refresh state must reappear when an editable picker is restored");
    busyForceDialog.forceClose();

    const completedSearchDialog = hooks.openDialog("completed-search");
    hooks.setBusy(completedSearchDialog, "searching");
    hooks.renderSeriesPicker(completedSearchDialog,
        { Id: "series-id", Type: "Series", Name: "Series" }, [], {}, {});
    assert(!completedSearchDialog.androidBackLocked,
        "rendering a completed search result must release the Android-back lock");
    documentStub.dispatchCommand("back");
    assert(!completedSearchDialog.overlay.isConnected,
        "normal Android back behavior must resume after search results are rendered");

    const seriesBackDialog = hooks.openDialog("series-navigation");
    const seriesItem = { Id: "series-id", Type: "Series", Name: "爱书的下克上" };
    const seriesSeasons = [{
        SeriesId: "series-id", SeasonId: "season-4", SeasonNumber: 4,
        SeasonName: "第 4 季", SeriesName: "爱书的下克上", EpisodeCount: 12,
        Candidates: []
    }];
    hooks.renderSeriesPicker(seriesBackDialog, seriesItem, seriesSeasons, {}, {});
    hooks.renderSeriesSeasonPicker(seriesBackDialog, seriesItem, seriesSeasons, 0, {}, {});
    assert(seriesBackDialog.title.textContent.includes("手动匹配"),
        "opening a Series Season must enter the secondary candidate view");
    documentStub.dispatchCommand("back");
    assert(seriesBackDialog.overlay.isConnected &&
        seriesBackDialog.title.textContent === "整部剧弹幕智能匹配",
        "Android back from a real Series Season candidate view must restore the Series overview");
    documentStub.dispatchCommand("back");
    assert(!seriesBackDialog.overlay.isConnected,
        "a second Android back at the restored Series top level must close the dialog");

    const lowerDialog = hooks.openDialog("lower");
    const upperDialog = hooks.openDialog("upper");
    const stackedEscape = documentStub.dispatchKey("Escape");
    assert(!upperDialog.overlay.isConnected && lowerDialog.overlay.isConnected && stackedEscape.defaultPrevented,
        "one Escape should close only the topmost closable dialog");
    documentStub.dispatchKey("Escape");
    assert(!lowerDialog.overlay.isConnected && hooks.activeDialogCount() === 0 &&
        (documentStub.listeners.keydown || []).length === 0,
        "repeated dialog cleanup should leave no active Escape listeners");
    assert(hooks.isSupportedItemType("Series") && hooks.isSupportedItemType("Season") &&
        hooks.isSupportedItemType("Episode") && hooks.isSupportedItemType("Movie"),
    "all smart-match item types should be supported");
    assert(!hooks.isSupportedItemType("Folder") && !hooks.isSupportedItemType("CollectionFolder"),
        "unsupported menu item types must remain excluded");
    assert(hooks.manualSearchDefault({ Type: "Movie", Name: "Movie name" }, {}) === "Movie name",
        "movie search should default to its own title");
    assert(hooks.manualSearchDefault({ Type: "Episode", Name: "Episode" }, { ParentName: "Series name" }) === "Series name",
        "episode search should default to its owning Series title");
    assert(hooks.manualSearchDefault({ Type: "Season", Name: "Season" }, { SeriesName: "Series name" }) === "Series name",
        "season search should default to its owning Series title");

    const first = hooks.setPendingContext("first-item-id");
    const second = hooks.setPendingContext("second-item-id");
    assert(second.generation > first.generation && second.id === "second-item-id",
        "a later card menu should invalidate the previous asynchronous context");
    assert(hooks.resolveMenuContextId("clicked-item", "other-item") === null &&
        hooks.resolveMenuContextId(null, null) === null,
        "unresolved or mismatched action sheets must not reuse stale identity");

    const fallbackAnchor = { id: "refresh" };
    ["Series", "Season", "Episode", "Movie"].forEach(function () {
        assert(hooks.findMenuInsertionAnchor({
            querySelector: selector => selector === '[data-id="refreshmetadata"]' ? fallbackAnchor : null
        }) === fallbackAnchor, "every supported type should use the same fallback anchor order");
    });

    let workflows = 0;
    hooks.setButtonWorkflow(function () { workflows++; });
    const stableMenu = menuFor("stable-item-id");
    documentStub.openedMenus = [stableMenu];
    context.ApiClient.getItem = async function (_user, id) { return { Id: id, Type: "Movie", Name: "Stable" }; };
    hooks.setPendingContext("stable-item-id");
    await hooks.injectButton();
    await hooks.injectButton();
    const injected = stableMenu.querySelectorAll('[data-id="danmu-bulk-download"]');
    assert(injected.length === 1, "repeated observer/injection runs should add exactly one action");
    await injected[0].dispatch("click");
    await injected[0].dispatch("click");
    assert(workflows === 1, "one injected action should start at most one workflow per click sequence");

    const unidentified = menuFor(null);
    documentStub.openedMenus = [unidentified];
    hooks.setPendingContext(null);
    await hooks.injectButton();
    assert(!unidentified.querySelector('[data-id="danmu-bulk-download"]'),
        "an action sheet without identity should not receive an action");

    const unsupported = menuFor("folder-item-id");
    documentStub.openedMenus = [unsupported];
    context.ApiClient.getItem = async function (_user, id) { return { Id: id, Type: "Folder", Name: "Folder" }; };
    hooks.setPendingContext("folder-item-id");
    await hooks.injectButton();
    assert(!unsupported.querySelector('[data-id="danmu-bulk-download"]'),
        "an authoritative unsupported item should not receive an action");

    let resolveSlow;
    const slowMenu = menuFor("slow-first-item");
    const fastMenu = menuFor("fast-second-item");
    context.ApiClient.getItem = function (_user, id) {
        if (id === "slow-first-item") return new Promise(resolve => { resolveSlow = resolve; });
        return Promise.resolve({ Id: id, Type: "Episode", Name: "Second" });
    };
    documentStub.openedMenus = [slowMenu];
    hooks.setPendingContext("slow-first-item");
    const slowInjection = hooks.injectButton();
    await Promise.resolve();
    documentStub.openedMenus = [fastMenu];
    hooks.setPendingContext("fast-second-item");
    await hooks.injectButton();
    resolveSlow({ Id: "slow-first-item", Type: "Movie", Name: "First" });
    await slowInjection;
    assert(!slowMenu.querySelector('[data-id="danmu-bulk-download"]') &&
        fastMenu.querySelectorAll('[data-id="danmu-bulk-download"]').length === 1,
        "two rapidly opened menus should inject only into the current action sheet");

    const manyCandidates = Array.from({ length: 60 }, function (_unused, index) {
        return {
            Site: "Fake", SiteName: "Fake", Id: "candidate-" + index,
            Name: "Candidate " + index, Year: 2026, EpisodeSize: 12, Category: "TV",
            SelectionEvidenceToken: "private-evidence-" + index
        };
    });
    const episodeTarget = {
        ParentName: "Series", SeasonName: "Season 2", EpisodeNumber: 3, ItemName: "Episode 3",
        ResolvedScopeType: "VirtualSeason", ResolvedScopeItemId: "virtual-season-2",
        __danmuManualCandidates: true, Candidates: manyCandidates
    };
    const candidateDialog = hooks.openDialog("episode candidates");
    const detailCallsBefore = apiCalls.filter(call => call.option === "GetSelectedCandidatePreview").length;
    hooks.renderItemCandidatePicker(candidateDialog,
        { Id: "episode-two-stage", Type: "Episode", Name: "Episode 3" }, episodeTarget, "Series");
    assert(candidateDialog.body.querySelectorAll(".danmuCandidate").length === 60 &&
        apiCalls.filter(call => call.option === "GetSelectedCandidatePreview").length === detailCallsBefore,
        "rendering 60 search candidates must use Search metadata without resolving any candidate details");
    assert(candidateDialog.body.querySelectorAll(".danmuCandidateDetailAction").length === 60 &&
        !allVisibleText(candidateDialog.body).includes("private-evidence-"),
        "manual episode candidates may show lazy controls, but never expose evidence tokens");

    apiResponses.MatchCandidateDetails = {
        Success: true, Generation: 1,
        SourceEpisodes: [{ Id: "inspected-a", Number: 1, Title: "Inspected A" }]
    };
    const inspectedMain = candidateDialog.body.querySelectorAll(".danmuCandidate")[0].children[1];
    await inspectedMain.querySelector(".danmuCandidateDetailAction").dispatch("click");
    const inspectCalls = apiCalls.filter(call => call.option === "MatchCandidateDetails" &&
        call.parameters.candidateId === "candidate-0");
    assert(inspectCalls.length === 1 && inspectCalls[0].parameters.candidateId === "candidate-0" &&
        inspectCalls[0].parameters.candidateEvidence === "private-evidence-0" &&
        candidateDialog.body.querySelectorAll(".danmuCandidateDetails").length === 1 &&
        inspectedMain.children.indexOf(inspectedMain.querySelector(".danmuCandidateDetailAction")) <
            inspectedMain.children.indexOf(inspectedMain.querySelector(".danmuCandidateDetails")),
        "clicking one lazy detail control must request only that candidate, pass evidence privately, and expand below its button");
    apiResponses.MatchCandidateDetails = { Success: false, Retryable: true, Message: "detail business failure" };
    const failureMain = candidateDialog.body.querySelectorAll(".danmuCandidate")[1].children[1];
    await failureMain.querySelector(".danmuCandidateDetailAction").dispatch("click");
    assert(failureMain.querySelector(".danmuCandidateDetails").className.includes("error") &&
        allVisibleText(failureMain).includes("detail business failure"),
        "HTTP-success business failures must remain on the candidate row with a retryable error");
    apiResponses.MatchCandidateDetails = {
        Success: true, Generation: 1,
        SourceEpisodes: [{ Id: "retried-b", Number: 2, Title: "Retried B" }]
    };
    await failureMain.querySelector(".danmuCandidateDetailAction").dispatch("click");
    assert(!failureMain.querySelector(".danmuCandidateDetails").className.includes("error") &&
        allVisibleText(failureMain).includes("Retried B"),
        "a candidate-detail business error must support an in-place retry without touching other candidates");
    delete apiResponses.MatchCandidateDetails;
    apiResponses.GetSelectedCandidatePreview = {
        Status: "ready", CandidateId: "candidate-37", ResolvedScopeType: "VirtualSeason",
        Episodes: [
            { Id: "source-episode-a", Number: 1, Title: "Episode A" },
            { Id: "source-episode-exact", Number: 3, Title: "Episode C" }
        ]
    };
    candidateDialog.body.querySelectorAll(".danmuCandidate")[37].children[0].checked = true;
    await candidateDialog.footer.children[candidateDialog.footer.children.length - 1].dispatch("click");
    await waitUntil(() => candidateDialog.body.querySelectorAll(".danmuSourceEpisodeChoice").length === 2,
        "the selected candidate detail should become visible");
    const selectedDetailCalls = apiCalls.filter(call => call.option === "GetSelectedCandidatePreview");
    const selectedDetailCall = selectedDetailCalls[selectedDetailCalls.length - 1];
    assert(selectedDetailCalls.length === detailCallsBefore + 1 &&
        selectedDetailCall.parameters.site === "Fake" &&
        selectedDetailCall.parameters.candidateId === "candidate-37" &&
        selectedDetailCall.parameters.searchOperationId &&
        candidateDialog.body.querySelectorAll(".danmuSourceEpisodeChoice").length === 2,
        "selecting one candidate must resolve exactly that candidate once and render its source episodes");
    assert(candidateDialog.body.children[0].textContent.includes("Series / Season 2 / 第 3 集 · Episode 3") &&
        !allVisibleText(candidateDialog.body).includes("virtual-season-2") &&
        !allVisibleText(candidateDialog.body).includes("标识符作用域"),
        "episode pages must keep the local episode summary without exposing scope or ItemId internals");
    candidateDialog.forceClose();

    const initialEpisodeDialog = hooks.openDialog("initial episode");
    hooks.renderItemCandidatePicker(initialEpisodeDialog,
        { Id: "initial-episode", Type: "Episode", Name: "Episode 3" }, {
            ParentName: "Series", SeasonName: "Season 2", EpisodeNumber: 3, ItemName: "Episode 3",
            Candidates: manyCandidates
        }, "Series");
    const initialForceControls = initialEpisodeDialog.footer.querySelectorAll(".danmuForceRefresh");
    assert(initialEpisodeDialog.body.querySelectorAll(".danmuCandidateDetailAction").length === 0 &&
        initialForceControls.length === 1 && initialForceControls[0].children[1].textContent === "强制刷新",
        "initial episode pages must preserve the original picker without lazy details and have one footer force-refresh checkbox");
    const initialDetailCalls = apiCalls.filter(call => call.option === "MatchCandidateDetails").length;
    initialEpisodeDialog.body.querySelectorAll(".danmuCandidate")[2].children[0].checked = true;
    await initialEpisodeDialog.footer.children[initialEpisodeDialog.footer.children.length - 1].dispatch("click");
    await waitUntil(() => initialEpisodeDialog.body.querySelectorAll(".danmuSourceEpisodeChoice").length === 2,
        "the original initial-episode parse flow must still open its authoritative source picker");
    assert(apiCalls.filter(call => call.option === "MatchCandidateDetails").length === initialDetailCalls &&
        apiCalls.some(call => call.option === "GetSelectedCandidatePreview" && call.parameters.candidateId === "candidate-2"),
        "initial candidates must retain GetSelectedCandidatePreview and never issue per-row details");
    initialEpisodeDialog.forceClose();

    let resolveStaleDetail;
    apiResponses.MatchCandidateDetails = function () {
        return new Promise(resolve => { resolveStaleDetail = resolve; });
    };
    const staleDialog = hooks.openDialog("stale lazy detail");
    hooks.renderItemCandidatePicker(staleDialog,
        { Id: "stale-episode", Type: "Episode", Name: "Episode 3" }, episodeTarget, "Series");
    const staleButton = staleDialog.body.querySelectorAll(".danmuCandidateDetailAction")[0];
    const staleRequest = staleButton.dispatch("click");
    await Promise.resolve();
    hooks.renderItemCandidatePicker(staleDialog,
        { Id: "stale-episode", Type: "Episode", Name: "Episode 3" }, {
            ParentName: "Series", SeasonName: "Season 2", EpisodeNumber: 3, ItemName: "Episode 3",
            __danmuManualCandidates: true,
            CandidateGeneration: "new-generation",
            Candidates: [{ Site: "Fake", Id: "new-candidate", Name: "New", SelectionEvidenceToken: "new-private" }]
        }, "Series");
    resolveStaleDetail({ Success: true, Generation: 1, SourceEpisodes: [{ Id: "old", Number: 1, Title: "Old result" }] });
    await staleRequest;
    assert(staleDialog.body.querySelectorAll(".danmuCandidateDetails").length === 0 &&
        !allVisibleText(staleDialog.body).includes("Old result"),
        "a stale candidate-details response must not expand or overwrite a newer candidate generation");
    staleDialog.forceClose();
    delete apiResponses.MatchCandidateDetails;

    let resolveRotatedTokenDetail;
    apiResponses.MatchCandidateDetails = function () {
        return new Promise(resolve => { resolveRotatedTokenDetail = resolve; });
    };
    const tokenDialog = hooks.openDialog("rotated evidence token");
    const tokenItem = { Id: "token-episode", Type: "Episode", Name: "Episode" };
    const tokenTarget = function (token) {
        return {
            ParentName: "Series", SeasonName: "Season", EpisodeNumber: 1, ItemName: "Episode",
            __danmuManualCandidates: true,
            Candidates: [{ Site: "Fake", Id: "same-candidate", Name: "Same candidate", SelectionEvidenceToken: token }]
        };
    };
    hooks.renderItemCandidatePicker(tokenDialog, tokenItem, tokenTarget("old-token"), "Series");
    const oldTokenGeneration = tokenDialog.candidateDetailGeneration;
    const oldTokenRequest = tokenDialog.body.querySelector(".danmuCandidateDetailAction").dispatch("click");
    await Promise.resolve();
    hooks.renderItemCandidatePicker(tokenDialog, tokenItem, tokenTarget("new-token"), "Series");
    assert(tokenDialog.candidateDetailGeneration > oldTokenGeneration,
        "rotating candidate evidence must advance the candidate-details generation even when target and candidate ID are unchanged");
    resolveRotatedTokenDetail({ Success: true, Generation: oldTokenGeneration,
        SourceEpisodes: [{ Id: "old-token-source", Number: 1, Title: "Old token result" }] });
    await oldTokenRequest;
    assert(tokenDialog.body.querySelectorAll(".danmuCandidateDetails").length === 0 &&
        !allVisibleText(tokenDialog.body).includes("Old token result"),
        "an old-token response must not render after refreshed evidence replaces the same candidate");
    apiResponses.MatchCandidateDetails = {
        Success: true, Generation: tokenDialog.candidateDetailGeneration,
        SourceEpisodes: [{ Id: "new-token-source", Number: 1, Title: "New token result" }]
    };
    await tokenDialog.body.querySelector(".danmuCandidateDetailAction").dispatch("click");
    const tokenCalls = apiCalls.filter(call => call.option === "MatchCandidateDetails" &&
        call.parameters.candidateId === "same-candidate");
    assert(tokenCalls[tokenCalls.length - 1].parameters.candidateEvidence === "new-token" &&
        allVisibleText(tokenDialog.body).includes("New token result") &&
        !allVisibleText(tokenDialog.body).includes("new-token"),
        "the refreshed candidate request must use the new private evidence token without rendering it");
    tokenDialog.forceClose();
    delete apiResponses.MatchCandidateDetails;

    const lazySeason = {
        SeasonId: "lazy-season", SeasonName: "Lazy Season", CandidateGeneration: "season-generation",
        Candidates: [{ Site: "Fake", SiteName: "Fake", Id: "season-candidate", Name: "Season candidate",
            SelectionEvidenceToken: "season-private", EpisodeSize: 12 }]
    };
    const seriesSeasonDialog = hooks.openDialog("series season lazy details");
    const beforeSeasonDetails = apiCalls.filter(call => call.option === "MatchCandidateDetails").length;
    hooks.renderSeriesSeasonPicker(seriesSeasonDialog, { Id: "series-lazy", Type: "Series", Name: "Series" },
        [lazySeason], 0, {}, {});
    assert(seriesSeasonDialog.body.querySelectorAll(".danmuCandidateDetailAction").length === 1 &&
        apiCalls.filter(call => call.option === "MatchCandidateDetails").length === beforeSeasonDetails &&
        seriesSeasonDialog.footer.querySelectorAll(".danmuForceRefresh").length === 1,
        "series per-season picker must render lazy details and exactly one footer force-refresh without eager requests");
    const seriesForce = seriesSeasonDialog.footer.querySelector(".danmuForceRefresh").children[0];
    seriesForce.checked = true;
    await seriesForce.dispatch("change");
    hooks.renderSeriesSeasonPicker(seriesSeasonDialog, { Id: "series-lazy", Type: "Series", Name: "Series" },
        [lazySeason], 0, {}, {});
    assert(seriesSeasonDialog.footer.querySelectorAll(".danmuForceRefresh").length === 1 &&
        seriesSeasonDialog.footer.querySelector(".danmuForceRefresh").children[0].checked,
        "force-refresh state must persist across pre-download picker re-renders");
    seriesSeasonDialog.forceClose();

    const directSeasonDialog = hooks.openDialog("direct season lazy details");
    hooks.renderCandidatePicker(directSeasonDialog, { Id: "season-item", Type: "Season", Name: "Season" }, lazySeason, "");
    assert(directSeasonDialog.body.querySelectorAll(".danmuCandidateDetailAction").length === 1 &&
        directSeasonDialog.footer.querySelectorAll(".danmuForceRefresh").length === 1 &&
        directSeasonDialog.footer.querySelector(".danmuForceRefresh").children[1].textContent === "强制刷新",
        "direct Season picker must use the same lazy control and one exact footer force-refresh label");
    directSeasonDialog.forceClose();
    assert(source.includes("查看逐集映射") && source.includes("danmuVirtualSeasonDetail"),
        "candidate inspection must not remove the existing authoritative per-episode mapping-detail UI");

    const failedDetailDialog = hooks.openDialog("failed detail resolution");
    hooks.renderItemCandidatePicker(failedDetailDialog,
        { Id: "episode-detail-failure", Type: "Episode", Name: "Episode 3" }, episodeTarget, "Series");
    apiResponses.GetSelectedCandidatePreview = { Status: "failed", Message: "provider detail failed", Episodes: [] };
    failedDetailDialog.body.querySelectorAll(".danmuCandidate")[8].children[0].checked = true;
    await failedDetailDialog.footer.children[failedDetailDialog.footer.children.length - 1].dispatch("click");
    await waitUntil(() => failedDetailDialog.body.querySelectorAll(".danmuCandidate").length === 60,
        "a failed detail request should restore candidates");
    assert(failedDetailDialog.body.querySelectorAll(".danmuCandidate").length === 60 &&
        !failedDetailDialog.body.querySelector(".danmuBusy") &&
        hooks.navigationContextDepth(failedDetailDialog) === 0,
        "detail-resolution failure must restore the intact candidate list, release busy controls, and consume only its provisional context");
    failedDetailDialog.forceClose();
    delete apiResponses.GetSelectedCandidatePreview;

    async function verifySingleTarget(type) {
        apiCalls.length = 0;
        const targetId = type === "Movie" ? "movie-target-id" : "episode-target-id";
        apiResponses.StartTrackedDownload = {
            TaskId: type.toLowerCase() + "-task", Status: "completed", Failed: 1,
            Episodes: [{ ItemId: targetId, EpisodeNumber: 3, EpisodeName: type, Status: "failed", Message: "provider failed" }]
        };
        apiResponses.RetryTrackedEpisode = {
            TaskId: type.toLowerCase() + "-task", Status: "completed", Succeeded: 1,
            Episodes: [{ ItemId: targetId, EpisodeNumber: 3, EpisodeName: type, Status: "success", Message: "ok" }]
        };
        const dialog = {
            body: new FakeElement("div"), footer: new FakeElement("div"),
            overlay: { isConnected: false }, closable: true, forceRefresh: false,
            close: function () {}, forceClose: function () {}, setBackHandler: function () {}
        };
        await hooks.renderSingleTargetProgress(
            dialog,
            { Id: targetId, Type: type, Name: type + " title" },
            { EpisodeNumber: 3 },
            { Site: "Fake", Id: "candidate", Name: "Candidate", SelectionEvidenceToken: "parent-proof" },
            type === "Episode" ? 4 : null,
            type === "Episode" ? "source-episode-exact" : null,
            true,
            type === "Movie" ? "opaque-part-proof" : null);
        const startCall = apiCalls.find(call => call.option === "StartTrackedDownload");
        if (type === "Episode") {
            assert(startCall.parameters.sourceEpisodeId === "source-episode-exact" &&
                !Object.prototype.hasOwnProperty.call(startCall.parameters, "commentId"),
                "episode confirmation must submit the exact resolved sourceEpisodeId without CommentId or positional guessing");
        } else {
            assert(startCall.parameters.selectionEvidenceToken === "parent-proof" &&
                startCall.parameters.moviePartToken === "opaque-part-proof" &&
                !Object.prototype.hasOwnProperty.call(startCall.parameters, "partId"),
                "Movie download must submit only scoped opaque evidence and never a raw part id");
        }
        let rows = dialog.body.querySelectorAll(".danmuEpisodeProgress");
        assert(rows.length === 1, type + " progress should render exactly one detailed item row");
        const retry = dialog.body.querySelector(".danmuEpisodeRetry");
        assert(retry && !retry.disabled, type + " terminal row should expose retry");
        await retry.dispatch("click");
        rows = dialog.body.querySelectorAll(".danmuEpisodeProgress");
        assert(rows.length === 1 && apiCalls.some(call => call.option === "RetryTrackedEpisode" && call.itemId === targetId),
            type + " retry should target its own item and keep one-row rendering");
    }
    await verifySingleTarget("Movie");
    await verifySingleTarget("Episode");

    const safeMovieHeading = hooks.movieCandidateHeading({
        SiteName: "哔哩哔哩", Name: "正片", Id: "raw-parent-id",
        SourceMetadata: { Title: "父电影标题", Year: 2024, Category: "电影" }
    }, null, "国语");
    assert(safeMovieHeading === "哔哩哔哩 · 父电影标题（2024） · 电影 · 国语" &&
        safeMovieHeading.indexOf("raw-parent-id") < 0 && safeMovieHeading.indexOf("正片") < 0,
        "Movie presentation must separate parent source identity from PartTitle and hide raw ids");
    const missingParentHeading = hooks.movieCandidateHeading({
        SiteName: "Bilibili", Name: "wrong leaf masquerading as parent"
    }, null, "正片");
    assert(missingParentHeading === "Bilibili · 正片" &&
        missingParentHeading.indexOf("wrong leaf") < 0 &&
        missingParentHeading.split("正片").length === 2,
        "Movie presentation without SourceTitle must use provider plus PartTitle exactly once");
    const normalizedDuplicateHeading = hooks.movieCandidateHeading({
        SiteName: "Bilibili", SourceMetadata: { Title: "ＦＥＡＴＵＲＥ   CUT" }
    }, null, " feature cut ");
    assert(normalizedDuplicateHeading === "Bilibili · ＦＥＡＴＵＲＥ   CUT",
        "Movie presentation must deduplicate parent and part after NFKC, whitespace, and case normalization");
    assert(hooks.moviePartChoices({ MovieParts: [
        { Token: "opaque-a", PartTitle: "国语" },
        { Token: "", PartTitle: "raw-id-only" }
    ] }).length === 1,
    "Movie selector must accept only server-issued opaque choices");

    apiCalls.length = 0;
    apiResponses.StartTrackedDownload = { TaskId: "", Status: "failed", Message: "single start failed" };
    let singleRecovery = 0;
    const singleFailureDialog = {
        body: new FakeElement("div"), footer: new FakeElement("div"), overlay: { isConnected: false },
        closable: false, forceRefresh: true, forceRefreshLocked: false, executionForceRefresh: null,
        close: function () {}, forceClose: function () {}, setBackHandler: function () {},
        preDownloadRecovery: function () { singleRecovery++; }
    };
    await hooks.renderSingleTargetProgress(singleFailureDialog,
        { Id: "single-failure", Type: "Movie", Name: "Movie" }, {},
        { Site: "Fake", Id: "candidate", Name: "Candidate" }, null, null, false);
    const singleStart = apiCalls.find(call => call.option === "StartTrackedDownload");
    assert(singleRecovery === 1 && singleFailureDialog.closable && !singleFailureDialog.forceRefreshLocked &&
        singleFailureDialog.executionForceRefresh === null && singleStart.parameters.forceRefresh === "true",
        "a zero-task single start must use the atomic force snapshot then unlock and restore the editable page");

    apiCalls.length = 0;
    apiResponses.StartTrackedDownload = { TaskId: "", Status: "failed", Message: "multi start failed" };
    let multiRecovery = 0;
    const multiFailureDialog = {
        body: new FakeElement("div"), footer: new FakeElement("div"), overlay: { isConnected: false },
        closable: false, forceRefresh: true, forceRefreshLocked: false, executionForceRefresh: null,
        close: function () {}, forceClose: function () {}, setBackHandler: function () {},
        preDownloadRecovery: function () { multiRecovery++; }
    };
    const failedSeason = { SeasonId: "multi-failure", SeasonName: "Failure season" };
    await hooks.renderDownloadProgress(multiFailureDialog, [failedSeason], {
        "::multi-failure": { Site: "Fake", Id: "candidate", Name: "Candidate" }
    });
    const multiStart = apiCalls.find(call => call.option === "StartTrackedDownload");
    assert(multiRecovery === 1 && multiFailureDialog.closable && !multiFailureDialog.forceRefreshLocked &&
        multiFailureDialog.executionForceRefresh === null && multiStart.parameters.forceRefresh === "true",
        "a zero-task multi-season start must share the snapshotted force flag then unlock and restore the picker");

    const contextSeasons = [
        { SeriesId: "context-series", SeasonId: "context-s0", SeasonNumber: 0,
            SeriesName: "服务端剧名", SeasonName: "特别篇", EpisodeCount: 2 },
        { SeriesId: "context-series", SeasonId: "context-s1", SeasonNumber: 1,
            SeriesName: "服务端剧名", SeasonName: "", Year: 2024,
            EpisodeCount: 12, EligibleEpisodeCount: 11 },
        { SeriesId: "context-series", SeasonId: "context-s2", SeasonNumber: 2,
            SeriesName: "服务端剧名", SeasonName: "第二季",
            EpisodeCount: 13, MappedEpisodeCount: 9 }
    ];
    const contextDialog = hooks.openDialog("server preview context");
    hooks.renderSeriesPicker(contextDialog,
        { Id: "context-series", Type: "Series", Name: "browser fallback name" }, contextSeasons, {}, {});
    const contextText = allVisibleText(contextDialog.body);
    assert(contextText.includes("库内信息：服务端剧名，返回 2 季，本地共 25 集。") &&
        contextText.includes("库内信息：服务端剧名 / 第 1 季，2024，本地 12 集，映射 11 集") &&
        contextText.includes("库内信息：服务端剧名 / 第二季，本地 13 集，映射 9 集") &&
        !contextText.includes("匹配状态、来源和决策原因均由服务器返回"),
        "Series and Season context must come from returned preview fields while the fixed authority paragraph stays removed");
    contextDialog.forceClose();

    const contextSeasonDialog = hooks.openDialog("direct Season context");
    hooks.renderCandidatePicker(contextSeasonDialog,
        { Id: "context-s1", Type: "Season", Name: "browser season name" }, contextSeasons[0], "");
    assert(allVisibleText(contextSeasonDialog.body).includes(
        "库内信息：服务端剧名 / 第 1 季，2024，本地 12 集，映射 11 集"),
        "a single Season must show returned Series/Season, optional year, local count, and mapped count");
    contextSeasonDialog.forceClose();

    const unchangedEpisodeDialog = hooks.openDialog("unchanged Episode context");
    hooks.renderItemCandidatePicker(unchangedEpisodeDialog,
        { Id: "unchanged-episode", Type: "Episode", Name: "browser episode name" },
        { ParentName: "父剧", SeasonName: "第一季", EpisodeNumber: 3, ItemName: "第三集", Candidates: [] }, "");
    assert(allVisibleText(unchangedEpisodeDialog.body).includes(
        "库内信息：父剧 / 第一季 / 第 3 集 · 第三集。请选择季度候选，再解析该候选的来源剧集。"),
        "single-Episode context must remain unchanged by Series/Season summary rendering");
    unchangedEpisodeDialog.forceClose();

    const unchangedMovieDialog = hooks.openDialog("unchanged Movie context");
    hooks.renderItemCandidatePicker(unchangedMovieDialog,
        { Id: "unchanged-movie", Type: "Movie", Name: "电影" },
        { ItemName: "服务端电影", Year: 2025, Candidates: [] }, "");
    assert(allVisibleText(unchangedMovieDialog.body).includes(
        "库内信息：服务端电影，2025。请选择正确电影。"),
        "Movie context must remain unchanged by Series/Season summary rendering");
    unchangedMovieDialog.forceClose();

    apiCalls.length = 0;
    apiResponses.StartTrackedDownload = {
        TaskId: "ineligible-origin", Status: "completed", Skipped: 4,
        Episodes: [{ ItemId: "ineligible-episode", Status: "skipped" }]
    };
    const ineligibleReplayDialog = {
        body: new FakeElement("div"), footer: new FakeElement("div"), overlay: { isConnected: false },
        closable: false, forceRefresh: false, close: function () {}, forceClose: function () {},
        setBackHandler: function () {}
    };
    await hooks.renderDownloadProgress(ineligibleReplayDialog,
        [{ SeriesId: "replay-series", SeasonId: "ineligible-season", SeasonName: "Ineligible" }], {});
    assert(!ineligibleReplayDialog.footer.children.some(button =>
        button.textContent.indexOf("忽略跳过再次下载") === 0 && button.style.display !== "none"),
        "skipped counts alone must not expose replay; only the latest server eligibility declaration can do so");

    apiCalls.length = 0;
    apiResponses.StartTrackedDownload = request => {
        const seasonId = request.url.url.split("/").pop();
        return {
            TaskId: "origin-" + seasonId, Status: "completed", Skipped: 2,
            ReplayEligible: true, ReplayEligibleCount: seasonId === "replay-a" ? 2 : 3,
            Episodes: [{ ItemId: seasonId + "-episode", Status: "skipped" }]
        };
    };
    apiResponses.ReplaySevenDaySkipped = request => {
        const taskId = request.url.query.taskId;
        if (taskId === "origin-replay-a") {
            return { TaskId: "child-replay-a", ReplayOriginTaskId: "origin-replay-a",
                ReplayKind: "seven_day_skipped", Status: "completed", Succeeded: 2,
                Episodes: [{ ItemId: "replay-a-episode", Status: "success" }] };
        }
        return { TaskId: "", Status: "failed", Message: "second replay rejected" };
    };
    const replayDialog = {
        body: new FakeElement("div"), footer: new FakeElement("div"), overlay: { isConnected: false },
        closable: false, forceRefresh: false, close: function () {}, forceClose: function () {},
        setBackHandler: function () {}
    };
    await hooks.renderDownloadProgress(replayDialog, [
        { SeriesId: "replay-series", SeasonId: "replay-a", SeasonName: "Replay A" },
        { SeriesId: "replay-series", SeasonId: "replay-b", SeasonName: "Replay B" }
    ], {});
    let replayButton = replayDialog.footer.children.find(button =>
        button.textContent === "忽略跳过再次下载（5 集）");
    assert(replayButton && !replayButton.disabled,
        "the lower-left replay action must appear only after every source task settles with server-declared eligibility");
    apiCalls.length = 0;
    await Promise.all([replayButton.dispatch("click"), replayButton.dispatch("click")]);
    const replayCalls = apiCalls.filter(call => call.option === "ReplaySevenDaySkipped");
    assert(replayCalls.length === 2 && replayCalls.map(call => call.parameters.taskId).join(",") ===
            "origin-replay-a,origin-replay-b" &&
        replayCalls.every(call => Object.keys(call.parameters).every(key =>
            ["option", "mappingProtocolVersion", "taskId"].includes(key))),
        "replay submits each eligible origin exactly once with only taskId, accepts child tasks into progress, and blocks duplicate clicks");
    replayButton = replayDialog.footer.children.find(button =>
        button.textContent === "忽略跳过再次下载（3 集）");
    assert(replayButton && !replayButton.disabled,
        "after a partial replay submission failure, only the still-eligible origin remains available for retry");
    delete apiResponses.ReplaySevenDaySkipped;

    apiResponses.StartTrackedDownload = {
        TaskId: "running-task", Status: "running",
        Episodes: [{ ItemId: "running-movie", EpisodeName: "Movie", Status: "running" }]
    };
    apiResponses.StopAllTrackedDownloads = { Message: "stopping" };
    const stopDialog = {
        body: new FakeElement("div"), footer: new FakeElement("div"),
        overlay: { isConnected: false }, closable: false, forceRefresh: false,
        close: function () {}, forceClose: function () {}, setBackHandler: function () {}
    };
    await hooks.renderSingleTargetProgress(stopDialog,
        { Id: "running-movie", Type: "Movie", Name: "Movie" }, {},
        { Site: "Fake", Id: "candidate", Name: "Candidate" }, null, null, false);
    const stop = stopDialog.footer.children.find(button => button.textContent === "强制停止全部下载");
    await stop.dispatch("click");
    assert(stopDialog.closable && !stopDialog.footer.children.some(button => button.textContent === "关闭"),
        "force-stop should make the single-target dialog immediately closable through only × or Escape");

    // r1 semantic viewport primitives: each fallback is independently observable.
    function viewportFallbackFixture(mode) {
        const dialog = {
            body: new FakeElement("div"), navigationContexts: [], presentationAnchors: {},
            presentationAnchorGeneration: 0
        };
        dialog.body.clientHeight = 200;
        dialog.body.scrollHeight = 1400;
        dialog.body.scrollTop = 700;
        const section = hooks.markPresentationAnchor(dialog, new FakeElement("section"), "section", "section");
        section.offsetTop = 600;
        const neighbor = hooks.markPresentationAnchor(dialog, new FakeElement("div"), "row", "neighbor");
        neighbor.offsetTop = 500;
        const row = hooks.markPresentationAnchor(dialog, new FakeElement("div"), "row", "row");
        row.offsetTop = 800;
        const action = hooks.markPresentationAnchor(dialog, new FakeElement("button"), "action", "action");
        action.offsetTop = 900;
        row.appendChild(action);
        section.append(neighbor, row);
        dialog.body.appendChild(section);
        hooks.captureParentNavigation(dialog, action, function () {
            dialog.body.replaceChildren();
            dialog.body.clientHeight = mode === "zero" ? 500 : 200;
            dialog.body.scrollHeight = mode === "zero" ? 300 : 1200;
            const rebuiltSection = hooks.markPresentationAnchor(dialog, new FakeElement("section"), "section", "section");
            rebuiltSection.offsetTop = 300;
            const rebuiltNeighbor = hooks.markPresentationAnchor(dialog, new FakeElement("div"), "row", "neighbor");
            rebuiltNeighbor.offsetTop = 550;
            const rebuiltRow = hooks.markPresentationAnchor(dialog, new FakeElement("div"), "row", "row");
            rebuiltRow.offsetTop = 450;
            const rebuiltAction = hooks.markPresentationAnchor(dialog, new FakeElement("button"), "action", "action");
            rebuiltAction.offsetTop = 600;
            if (mode === "action") rebuiltRow.appendChild(rebuiltAction);
            if (mode === "action" || mode === "row") rebuiltSection.appendChild(rebuiltRow);
            if (["action", "row", "section"].includes(mode)) dialog.body.appendChild(rebuiltSection);
            if (mode === "neighbor") dialog.body.appendChild(rebuiltNeighbor);
        }, { row: row, section: section, neighbor: neighbor });
        assert(hooks.returnFromChild(dialog), mode + " fixture must consume one context");
        return { offset: dialog.body.scrollTop, depth: hooks.navigationContextDepth(dialog) };
    }
    assert(viewportFallbackFixture("action").offset === 400,
        "semantic return must prefer the rebuilt initiating action and preserve its viewport-relative offset");
    assert(viewportFallbackFixture("row").offset === 350,
        "when the action disappears, semantic return must use the initiating row rather than the action geometry");
    assert(viewportFallbackFixture("section").offset === 400,
        "when action and row disappear, semantic return must use the surviving enclosing section");
    assert(viewportFallbackFixture("neighbor").offset === 750,
        "when the initiating section disappears, semantic return must use the pre-recorded logical neighbor");
    assert(viewportFallbackFixture("raw").offset === 700,
        "when no semantic anchor survives, semantic return must retain an in-range raw offset");
    assert(viewportFallbackFixture("zero").offset === 0,
        "a rebuilt parent with no scrollable range must use zero");

    const clampedDialog = { body: new FakeElement("div"), navigationContexts: [], presentationAnchors: {} };
    clampedDialog.body.scrollTop = 900;
    clampedDialog.body.scrollHeight = 1200;
    clampedDialog.body.clientHeight = 200;
    hooks.captureParentNavigation(clampedDialog, null, function () {
        clampedDialog.body.replaceChildren();
        clampedDialog.body.scrollHeight = 430;
        clampedDialog.body.clientHeight = 200;
    });
    hooks.returnFromChild(clampedDialog);
    assert(clampedDialog.body.scrollTop === 230,
        "the numeric fallback must clamp to the rebuilt parent's final valid scroll range");

    const nestedDialog = { body: new FakeElement("div"), navigationContexts: [], presentationAnchors: {} };
    const nestedRow = hooks.markPresentationAnchor(nestedDialog, new FakeElement("div"), "row", "nested");
    nestedDialog.body.appendChild(nestedRow);
    const nestedOrder = [];
    hooks.captureParentNavigation(nestedDialog, nestedRow, function () { nestedOrder.push("outer"); });
    hooks.captureParentNavigation(nestedDialog, nestedRow, function () { nestedOrder.push("inner"); });
    hooks.returnFromChild(nestedDialog);
    hooks.returnFromChild(nestedDialog);
    assert(nestedOrder.join(",") === "inner,outer" && hooks.navigationContextDepth(nestedDialog) === 0,
        "nested navigation contexts must remain per-dialog and last-in-first-out");

    // Real Series trigger: capture occurs before replacement, child starts at zero, and server order stays intact.
    const scrollSeriesDialog = hooks.openDialog("r1 Series scroll entry");
    const scrollSeriesSeasons = [{
        SeriesId: "scroll-series", SeasonId: "scroll-season", SeasonNumber: 2,
        SeriesName: "Scroll Series", SeasonName: "Season 2", Status: "not-matched",
        Candidates: [
            { Site: "Fake", Id: "ordered-a", Name: "First server candidate" },
            { Site: "Fake", Id: "ordered-b", Name: "Second server candidate" }
        ]
    }];
    const savedPointerEvent = context.window.PointerEvent;
    context.window.PointerEvent = function PointerEvent() {};
    hooks.renderSeriesPicker(scrollSeriesDialog,
        { Id: "scroll-series", Type: "Series", Name: "Scroll Series" }, scrollSeriesSeasons, {}, {});
    const seriesEntry = scrollSeriesDialog.body.querySelectorAll(".danmuSmartButton")
        .find(button => button.textContent === "查看候选");
    anchorLayoutOffsets[seriesEntry.dataset.danmuNavAnchor] = 706;
    scrollSeriesDialog.body.scrollTop = 326;
    useHtmlCollectionLikeChildren(scrollSeriesDialog.body);
    assert(scrollSeriesDialog.body.children.length > 0 &&
        scrollSeriesDialog.body.children.forEach === undefined &&
        scrollSeriesDialog.body.children.some === undefined &&
        scrollSeriesDialog.body.children[Symbol.iterator] === undefined,
        "the real Series workflow fixture must expose HTMLCollection-like children without Array methods or iteration");
    await seriesEntry.dispatch("pointerdown", { pointerType: "mouse" });
    assert(hooks.navigationContextDepth(scrollSeriesDialog) === 0,
        "pointer preactivation must sample geometry without entering navigation");
    scrollSeriesDialog.body.scrollTop = 426;
    await seriesEntry.dispatch("click", { isTrusted: true });
    assert(scrollSeriesDialog.body.scrollTop === 0 && hooks.navigationContextDepth(scrollSeriesDialog) === 1 &&
        scrollSeriesDialog.body.querySelectorAll(".danmuCandidateTitle").map(node => node.textContent).join("|") ===
            "Fake · First server candidate|Fake · Second server candidate",
        "the real Series action must synchronously enter at zero without reordering server candidates");
    scrollSeriesDialog.body.scrollHeight = 200;
    scrollSeriesDialog.body.clientHeight = 200;
    await scrollSeriesDialog.footer.children.find(button => button.textContent === "返回总览").dispatch("click");
    const restoredSeriesAction = scrollSeriesDialog.body.querySelectorAll(".danmuSmartButton")
        .find(button => button.textContent === "查看候选");
    assert(restoredSeriesAction && scrollSeriesDialog.body.scrollTop === 326 &&
        hooks.navigationContextDepth(scrollSeriesDialog) === 0,
        "pointerdown must preserve the pre-focus 326 offset for contentTop 706 through HTMLCollection-like entry and action return instead of capturing the native-focus 426 offset");
    delete anchorLayoutOffsets[seriesEntry.dataset.danmuNavAnchor];
    context.window.PointerEvent = savedPointerEvent;
    scrollSeriesDialog.forceClose();

    async function verifySeriesActivationTiming(name, armType, armEvent, useAndroidBack, expectedOffset, clickEvent) {
        context.window.PointerEvent = function PointerEvent() {};
        const dialog = hooks.openDialog("activation " + name);
        hooks.renderSeriesPicker(dialog, { Id: "activation-series", Type: "Series", Name: "Activation" },
            [Object.assign({}, scrollSeriesSeasons[0], { SeasonId: "activation-" + name })], {}, {});
        const action = dialog.body.querySelectorAll(".danmuSmartButton")
            .find(button => button.textContent === "查看候选");
        anchorLayoutOffsets[action.dataset.danmuNavAnchor] = 706;
        dialog.body.scrollTop = 326;
        if (armType) await action.dispatch(armType, armEvent);
        assert(hooks.navigationContextDepth(dialog) === 0,
            name + " preactivation must not push a navigation context");
        dialog.body.scrollTop = 426;
        await action.dispatch("click", clickEvent);
        assert(dialog.body.scrollTop === 0 && hooks.navigationContextDepth(dialog) === 1,
            name + " click must push exactly one context and enter the child at zero");
        const detachedBody = dialog.body.children[0];
        const detachedApiCount = apiCalls.length;
        await action.dispatch("click", { isTrusted: true });
        assert(hooks.navigationContextDepth(dialog) === 1 && dialog.body.children[0] === detachedBody &&
            apiCalls.length === detachedApiCount,
            name + " detached second click must not push, render, or request again");
        if (useAndroidBack) documentStub.dispatchCommand("back");
        else await dialog.footer.children.find(button => button.textContent === "返回总览").dispatch("click");
        assert(dialog.body.scrollTop === expectedOffset && hooks.navigationContextDepth(dialog) === 0,
            name + " return must use the expected activation geometry");
        delete anchorLayoutOffsets[action.dataset.danmuNavAnchor];
        dialog.forceClose();
    }
    await verifySeriesActivationTiming("pointer-touch", "pointerdown", { pointerType: "touch" }, true, 326,
        { isTrusted: true });
    await verifySeriesActivationTiming("keyboard-enter", "keydown", { key: "Enter" }, false, 326,
        { isTrusted: true });
    await verifySeriesActivationTiming("keyboard-space", "keydown", { key: " " }, false, 326,
        { isTrusted: true });
    await verifySeriesActivationTiming("programmatic", null, null, false, 426);
    await verifySeriesActivationTiming("untrusted-after-stale-arm", "pointerdown", { pointerType: "mouse" },
        false, 426);

    async function verifyLegacyActivation(name, navigatorIdentity, armType, syntheticType) {
        const previousNavigator = context.window.navigator;
        context.window.navigator = navigatorIdentity;
        context.window.PointerEvent = undefined;
        const dialog = hooks.openDialog("legacy " + name);
        hooks.renderSeriesPicker(dialog, { Id: "legacy-series", Type: "Series", Name: "Legacy" },
            [Object.assign({}, scrollSeriesSeasons[0], { SeasonId: "legacy-" + name })], {}, {});
        const action = dialog.body.querySelectorAll(".danmuSmartButton")
            .find(button => button.textContent === "查看候选");
        anchorLayoutOffsets[action.dataset.danmuNavAnchor] = 706;
        dialog.body.scrollTop = 326;
        await action.dispatch(armType);
        dialog.body.scrollTop = 426;
        if (syntheticType) await action.dispatch(syntheticType);
        await action.dispatch("click", { isTrusted: true });
        await dialog.footer.children.find(button => button.textContent === "返回总览").dispatch("click");
        assert(dialog.body.scrollTop === 326 && hooks.navigationContextDepth(dialog) === 0,
            name + " legacy activation must preserve pre-focus geometry without synthesized-event overwrite");
        delete anchorLayoutOffsets[action.dataset.danmuNavAnchor];
        dialog.forceClose();
        context.window.navigator = previousNavigator;
    }
    await verifyLegacyActivation("android-touch", { userAgent: "Android WebView" }, "touchstart", "mousedown");
    await verifyLegacyActivation("desktop-mouse", { userAgent: "Desktop Chrome" }, "mousedown", "touchstart");

    async function verifyCancelledActivation(name, pointerSupported, armType, cancelType, armEvent) {
        context.window.PointerEvent = pointerSupported ? function PointerEvent() {} : undefined;
        const dialog = hooks.openDialog("cancel " + name);
        hooks.renderSeriesPicker(dialog, { Id: "cancel-series", Type: "Series", Name: "Cancel" },
            [Object.assign({}, scrollSeriesSeasons[0], { SeasonId: "cancel-" + name })], {}, {});
        const action = dialog.body.querySelectorAll(".danmuSmartButton")
            .find(button => button.textContent === "查看候选");
        dialog.body.scrollTop = 326;
        await action.dispatch(armType, armEvent);
        await action.dispatch(cancelType);
        assert(hooks.navigationContextDepth(dialog) === 0,
            name + " cancellation without click must retain zero navigation depth");
        dialog.forceClose();
    }
    await verifyCancelledActivation("pointercancel", true, "pointerdown", "pointercancel", { pointerType: "mouse" });
    await verifyCancelledActivation("touchcancel", false, "touchstart", "touchcancel");
    await verifyCancelledActivation("contextmenu", true, "pointerdown", "contextmenu", { pointerType: "touch" });
    await verifyCancelledActivation("dragstart", true, "pointerdown", "dragstart", { pointerType: "mouse" });
    await verifyCancelledActivation("blur", true, "pointerdown", "blur", { pointerType: "pen" });
    await verifyCancelledActivation("longpress-without-click", false, "touchstart", "contextmenu");

    context.window.PointerEvent = function PointerEvent() {};
    const crossDialog = hooks.openDialog("activation ownership");
    const crossSeasons = [
        Object.assign({}, scrollSeriesSeasons[0], { SeasonId: "activation-a", SeasonName: "Season A" }),
        Object.assign({}, scrollSeriesSeasons[0], { SeasonId: "activation-b", SeasonName: "Season B" })
    ];
    hooks.renderSeriesPicker(crossDialog, { Id: "activation-series", Type: "Series", Name: "Activation" },
        crossSeasons, {}, {});
    const crossActions = crossDialog.body.querySelectorAll(".danmuSmartButton")
        .filter(button => button.textContent === "查看候选");
    anchorLayoutOffsets[crossActions[0].dataset.danmuNavAnchor] = 706;
    anchorLayoutOffsets[crossActions[1].dataset.danmuNavAnchor] = 906;
    crossDialog.body.scrollTop = 326;
    await crossActions[0].dispatch("pointerdown", { pointerType: "mouse" });
    crossDialog.body.scrollTop = 426;
    await crossActions[1].dispatch("pointerdown", { pointerType: "mouse" });
    await crossActions[1].dispatch("click", { isTrusted: true });
    assert(hooks.navigationContextDepth(crossDialog) === 1,
        "a new activation must overwrite A and B must consume exactly its own pending sample");
    await crossDialog.footer.children.find(button => button.textContent === "返回总览").dispatch("click");
    assert(crossDialog.body.scrollTop === 426 && hooks.navigationContextDepth(crossDialog) === 0,
        "A armed without click must never leak its geometry into B");
    delete anchorLayoutOffsets[crossActions[0].dataset.danmuNavAnchor];
    delete anchorLayoutOffsets[crossActions[1].dataset.danmuNavAnchor];
    crossDialog.forceClose();
    context.window.PointerEvent = savedPointerEvent;

    // Real successful Season save: the ordinary row becomes a composite section,
    // so action/row anchors disappear and the stable Season section must restore it.
    const seasonToCompositeDialog = hooks.openDialog("r1 Season save changes parent structure");
    const seasonToComposite = {
        SeriesId: "section-series", SeasonId: "section-season", SeasonNumber: 1,
        SeriesName: "Section Series", SeasonName: "Season 1", Status: "not-matched",
        MappingProtocolVersion: 22, PlanGeneration: 7341, CandidateGeneration: "section-generation",
        Candidates: [{ Site: "Fake", Id: "section-candidate", Name: "Section candidate",
            SelectionEvidenceToken: "section-proof" }]
    };
    const confirmedComposite = Object.assign({}, compositeSeason, {
        SeriesId: seasonToComposite.SeriesId,
        SeasonId: seasonToComposite.SeasonId,
        SeasonNumber: seasonToComposite.SeasonNumber,
        SeriesName: seasonToComposite.SeriesName,
        SeasonName: seasonToComposite.SeasonName
    });
    const seasonToCompositeSeasons = [seasonToComposite];
    hooks.renderSeriesPicker(seasonToCompositeDialog,
        { Id: "section-series", Type: "Series", Name: "Section Series" },
        seasonToCompositeSeasons, {}, {});
    seasonToCompositeDialog.body.clientHeight = 300;
    seasonToCompositeDialog.body.scrollHeight = 1600;
    const ordinarySection = seasonToCompositeDialog.body.querySelector(".danmuSeasonSummary");
    const ordinaryRow = ordinarySection.children[0];
    const ordinaryAction = ordinarySection.querySelectorAll(".danmuSmartButton")
        .find(button => button.textContent === "查看候选");
    ordinarySection.offsetTop = 600;
    ordinaryRow.offsetTop = 700;
    ordinaryAction.offsetTop = 800;
    seasonToCompositeDialog.body.scrollTop = 900;
    const originalSectionViewportOffset = ordinarySection.offsetTop - seasonToCompositeDialog.body.scrollTop;
    await ordinaryAction.dispatch("click");
    seasonToCompositeDialog.body.querySelector(".danmuCandidate").children[0].checked = true;
    assert(seasonToCompositeDialog.body.querySelector('input[name="danmuSeriesManualCandidate"]:checked'),
        "the real Season-save fixture must select its rendered candidate radio");
    apiResponses.MatchPreview = { Seasons: [confirmedComposite] };
    await seasonToCompositeDialog.footer.children
        .find(button => button.textContent === "保存本季选择").dispatch("click");
    for (let attempt = 0; attempt < 12 &&
        !seasonToCompositeDialog.body.querySelector(".danmuCompositeSeason"); attempt++) {
        await new Promise(resolve => setImmediate(resolve));
    }
    const rebuiltCompositeSection = seasonToCompositeDialog.body.querySelector(".danmuCompositeSeason");
    assert(rebuiltCompositeSection &&
        seasonToCompositeDialog.body.querySelectorAll(".danmuSeasonSummary").length === 0 &&
        !seasonToCompositeDialog.body.querySelectorAll(".danmuSmartButton")
            .some(button => button.textContent === "查看候选") &&
        rebuiltCompositeSection.offsetTop - seasonToCompositeDialog.body.scrollTop ===
            originalSectionViewportOffset &&
        seasonToCompositeDialog.body.scrollTop === 300 &&
        hooks.navigationContextDepth(seasonToCompositeDialog) === 0,
        "a successful Season save that replaces the ordinary action/row with composite content must restore through the stable Season section at its original viewport-relative offset");
    delete apiResponses.MatchPreview;
    seasonToCompositeDialog.forceClose();

    // Real composite trigger: the immediate busy/result child is reset before its asynchronous search.
    context.window.PointerEvent = function PointerEvent() {};
    const scrollCompositeDialog = hooks.openDialog("r1 composite scroll entry");
    const scrollCompositeSeasons = [groupOnlySeason];
    hooks.renderSeriesPicker(scrollCompositeDialog,
        { Id: "scroll-composite", Type: "Series", Name: "Composite" }, scrollCompositeSeasons, {}, {});
    scrollCompositeDialog.body.scrollTop = 326;
    apiResponses.MatchPreview = { Seasons: [groupOnlySeason] };
    const compositeEntry = scrollCompositeDialog.body.querySelectorAll(".danmuSmartButton")
        .find(button => button.textContent === "手动匹配");
    anchorLayoutOffsets[compositeEntry.dataset.danmuNavAnchor] = 706;
    await compositeEntry.dispatch("pointerdown", { pointerType: "mouse" });
    assert(hooks.navigationContextDepth(scrollCompositeDialog) === 0,
        "expanded composite pointer preactivation must remain a pending sample only");
    scrollCompositeDialog.body.scrollTop = 426;
    await compositeEntry.dispatch("click", { isTrusted: true });
    assert(scrollCompositeDialog.body.scrollTop === 0 && hooks.navigationContextDepth(scrollCompositeDialog) === 1,
        "the real temporary-range action must enter its child at zero before automatic range-search completion");
    await waitUntil(() => scrollCompositeDialog.footer.children
        .some(button => button.textContent === "返回总览"), "composite return action did not render");
    const detachedCompositeBody = scrollCompositeDialog.body.children[0];
    const detachedCompositeApiCount = apiCalls.length;
    await compositeEntry.dispatch("click", { isTrusted: true });
    assert(scrollCompositeDialog.body.children[0] === detachedCompositeBody &&
        apiCalls.length === detachedCompositeApiCount && hooks.navigationContextDepth(scrollCompositeDialog) === 1,
        "a detached composite match trigger must not render, request, or push again");
    scrollCompositeDialog.body.scrollHeight = 1600;
    scrollCompositeDialog.body.clientHeight = 300;
    await scrollCompositeDialog.footer.children.find(button => button.textContent === "返回总览").dispatch("click");
    assert(scrollCompositeDialog.body.scrollTop === 326 && hooks.navigationContextDepth(scrollCompositeDialog) === 0,
        "expanded composite return must preserve pre-focus 326 instead of click-time 426 geometry");
    delete anchorLayoutOffsets[compositeEntry.dataset.danmuNavAnchor];
    scrollCompositeDialog.forceClose();
    context.window.PointerEvent = savedPointerEvent;
    delete apiResponses.MatchPreview;

    async function verifyNestedItemEntry(type) {
        const previousPointerEvent = context.window.PointerEvent;
        context.window.PointerEvent = function PointerEvent() {};
        const dialog = hooks.openDialog("r1 " + type + " nested entry");
        const candidate = { Site: "Fake", Id: type.toLowerCase() + "-candidate", Name: "Candidate",
            SelectionEvidenceToken: type.toLowerCase() + "-proof" };
        const alternateCandidate = { Site: "Fake", Id: type.toLowerCase() + "-alternate", Name: "Alternate",
            SelectionEvidenceToken: type.toLowerCase() + "-alternate-proof" };
        const target = { ItemName: type, Candidates: [candidate, alternateCandidate] };
        if (type === "Episode") {
            target.ParentName = "Parent";
            target.SeasonName = "Season";
            target.EpisodeNumber = 3;
        }
        hooks.renderItemCandidatePicker(dialog,
            { Id: type.toLowerCase() + "-item", Type: type, Name: type }, target, "");
        const candidateRows = dialog.body.querySelectorAll(".danmuCandidate");
        anchorLayoutOffsets[candidateRows[0].children[1].dataset.danmuNavAnchor] = 706;
        anchorLayoutOffsets[candidateRows[1].children[1].dataset.danmuNavAnchor] = 906;
        dialog.body.scrollTop = 326;
        candidateRows[0].children[0].checked = true;
        apiResponses.GetSelectedCandidatePreview = type === "Episode"
            ? { Status: "ready", Episodes: [
                { Id: "source-a", Number: 1, Title: "First source" },
                { Id: "source-b", Number: 2, Title: "Second source" }
            ] }
            : { Status: "ready", MovieParts: [
                { Token: "part-a", PartTitle: "First version" },
                { Token: "part-b", PartTitle: "Second version" }
            ] };
        const startAction = dialog.footer.children[dialog.footer.children.length - 1];
        await startAction.dispatch("pointerdown", { pointerType: "mouse" });
        candidateRows[0].children[0].checked = false;
        candidateRows[1].children[0].checked = true;
        dialog.body.scrollTop = 426;
        await startAction.dispatch("click", { isTrusted: true });
        const childClass = type === "Episode" ? ".danmuSourceEpisodeChoice" : ".danmuMoviePartChoice";
        await waitUntil(() => dialog.body.querySelectorAll(childClass).length === 2,
            type + " nested selector did not render");
        const detachedChild = dialog.body.children[0];
        const detachedApiCount = apiCalls.length;
        await startAction.dispatch("click", { isTrusted: true });
        assert(dialog.body.scrollTop === 0 && hooks.navigationContextDepth(dialog) === 1 &&
            dialog.body.children[0] === detachedChild && apiCalls.length === detachedApiCount &&
            dialog.body.querySelectorAll(childClass).map(row => row.querySelector(".danmuCandidateTitle").textContent)
                .join("|").includes("First") &&
            dialog.body.querySelectorAll(childClass)[1].querySelector(".danmuCandidateTitle").textContent.includes("Second"),
            type + " real candidate transition must enter at zero, preserve authoritative child order, and ignore a detached second start without render or request");
        apiResponses.StartTrackedDownload = { TaskId: "", Status: "failed", Message: "recoverable fixture" };
        dialog.body.querySelectorAll(childClass)[0].children[0].checked = true;
        const submitChild = dialog.footer.children[dialog.footer.children.length - 1];
        await submitChild.dispatch("click");
        await waitUntil(() => dialog.body.querySelectorAll(childClass).length === 2 && dialog.closable,
            type + " recoverable submission did not restore its child");
        assert(hooks.navigationContextDepth(dialog) === 1,
            type + " failed submission must retain exactly one original candidate-parent context");
        await dialog.footer.children.find(button => button.textContent === "返回候选列表").dispatch("click");
        assert(hooks.navigationContextDepth(dialog) === 0 && dialog.body.scrollTop === 426,
            type + " changed checked candidate must reject the armed row sample, fall back to click-time geometry, and consume one retained context");
        delete anchorLayoutOffsets[candidateRows[0].children[1].dataset.danmuNavAnchor];
        delete anchorLayoutOffsets[candidateRows[1].children[1].dataset.danmuNavAnchor];
        context.window.PointerEvent = previousPointerEvent;
        dialog.forceClose();
    }
    await verifyNestedItemEntry("Episode");
    await verifyNestedItemEntry("Movie");
    delete apiResponses.GetSelectedCandidatePreview;
    delete apiResponses.StartTrackedDownload;

    const acceptedContextDialog = {
        body: new FakeElement("div"), footer: new FakeElement("div"), overlay: { isConnected: false },
        closable: true, forceRefresh: false, forceRefreshLocked: false, executionForceRefresh: null,
        navigationContexts: [{ renderParent: function () {} }], preDownloadRecovery: function () {},
        setBackHandler: function () {}, close: function () {}, forceClose: function () {}
    };
    apiResponses.StartTrackedDownload = { TaskId: "accepted-r1", Status: "completed", Episodes: [] };
    await hooks.renderSingleTargetProgress(acceptedContextDialog,
        { Id: "accepted-movie", Type: "Movie", Name: "Accepted" }, {},
        { Site: "Fake", Id: "accepted-candidate", SelectionEvidenceToken: "accepted-proof" },
        null, null, false, "accepted-part");
    assert(hooks.navigationContextDepth(acceptedContextDialog) === 0 &&
        acceptedContextDialog.preDownloadRecovery === null,
        "a valid accepted TaskId must clear the now non-returnable navigation context and recovery closure");
    delete apiResponses.StartTrackedDownload;

    const directCompositeDialog = hooks.openDialog("direct composite has no parent");
    hooks.renderCandidatePicker(directCompositeDialog,
        { Id: "direct-composite", Type: "Season", Name: "Direct composite" }, groupOnlySeason, "");
    assert(hooks.navigationContextDepth(directCompositeDialog) === 0,
        "a direct Season composite target must not acquire a fictitious parent-return context");
    const contextBeforeBusy = hooks.navigationContextDepth(directCompositeDialog);
    hooks.setBusy(directCompositeDialog, "same-page busy");
    assert(hooks.navigationContextDepth(directCompositeDialog) === contextBeforeBusy,
        "setBusy and other same-page surfaces must not create navigation contexts");
    directCompositeDialog.forceClose();

    const opaqueAnchorDialog = { body: new FakeElement("div"), navigationContexts: [], presentationAnchors: {} };
    const opaqueAnchor = hooks.markPresentationAnchor(opaqueAnchorDialog, new FakeElement("div"), "row",
        "private-media-id-should-not-be-in-dom");
    assert(/^nav-[0-9a-z]+$/.test(opaqueAnchor.dataset.danmuNavAnchor) &&
        !opaqueAnchor.dataset.danmuNavAnchor.includes("private-media-id"),
        "presentation anchors must remain opaque instead of exposing their stable logical identity");

    assert(hooks.isAndroidCommandEnvironment({ userAgentData: { platform: " aNdRoId " } }) &&
        hooks.isAndroidCommandEnvironment({ userAgent: "Mozilla/5.0 (Linux; ANDROID 15)" }) &&
        !hooks.isAndroidCommandEnvironment({ userAgentData: { platform: 42 }, userAgent: "Desktop" }) &&
        !hooks.isAndroidCommandEnvironment({ userAgent: "Desktop", maxTouchPoints: 10, innerWidth: 360 }),
        "Android mode must accept only trimmed case-insensitive UA-CH or UA identity, never touch/width hints");
    const throwingIdentity = {};
    Object.defineProperty(throwingIdentity, "userAgentData", { get: function () { throw new Error("blocked"); } });
    Object.defineProperty(throwingIdentity, "userAgent", { get: function () { throw new Error("blocked"); } });
    assert(!hooks.isAndroidCommandEnvironment(throwingIdentity),
        "throwing Android identity getters must conservatively resolve to desktop");

    const androidNavigator = context.window.navigator;
    context.window.navigator = { userAgentData: { platform: "Windows" }, userAgent: "Desktop Chrome",
        maxTouchPoints: 10, innerWidth: 360 };
    const desktopCounts = Object.assign({}, historyCalls);
    const desktopX = hooks.openDialog("desktop X");
    await desktopX.overlay.querySelector(".danmuSmartClose").dispatch("click");
    const desktopOrdinary = hooks.openDialog("desktop ordinary close");
    desktopOrdinary.close();
    const desktopOne = hooks.openDialog("desktop one");
    const desktopTwo = hooks.openDialog("desktop two");
    assert(hooks.dialogBackMode(desktopOne) === "desktop" && hooks.dialogBackMode(desktopTwo) === "desktop" &&
        historyCalls.pushState === desktopCounts.pushState && historyCalls.replaceState === desktopCounts.replaceState,
        "desktop dialog open, including responsive/touch desktop, must perform zero history mutations");
    context.window.navigator = androidNavigator;
    assert(hooks.dialogBackMode(desktopOne) === "desktop",
        "back mode must remain frozen for a dialog after platform identity changes");
    const desktopBackBefore = historyCalls.back;
    desktopTwo.forceClose();
    documentStub.dispatchKey("Escape");
    assert(historyCalls.back === desktopBackBefore,
        "desktop force close and Escape must never traverse history");
    context.window.navigator = { userAgent: "Desktop Chrome" };
    const desktopStackOne = hooks.openDialog("desktop stack one");
    const desktopStackTwo = hooks.openDialog("desktop stack two");
    const hostBackBefore = historyCalls.back;
    (windowListeners.popstate || []).slice().forEach(listener => listener({ state: null }));
    assert(!desktopStackOne.overlay.isConnected && !desktopStackTwo.overlay.isConnected &&
        historyCalls.back === hostBackBefore,
        "a desktop host popstate must dispose every stacked overlay without a second traversal");
    context.window.navigator = androidNavigator;

    assert(!source.includes("CloseWatcher") && !source.includes("requestAnimationFrame") &&
        !source.includes("window.navigation") && !source.includes("navigator.maxTouchPoints") &&
        !source.includes("innerWidth") && !source.includes("hostScroller"),
        "r1 must not add experimental navigation, scheduled restoration, host scrollers, or responsive/touch heuristics");
    const commandOwnerSource = source.slice(source.indexOf("function commandBackListener"),
        source.indexOf("function hostPopStateListener"));
    assert((source.match(/addEventListener\("command", commandBackListener, true\)/g) || []).length === 1 &&
        !source.includes('addEventListener("backbutton"') && !source.includes("history.pushState") &&
        !source.includes("history.replaceState") && !source.includes("history.back") &&
        !source.includes("dialogHistory") && !source.includes("ignoredDialogHistoryPops") &&
        !commandOwnerSource.includes("stopPropagation") && !commandOwnerSource.includes("setTimeout") &&
        !commandOwnerSource.includes("backbutton"),
        "formal V30 must have one command owner and no dialog history, Smart backbutton, cancellation, or timer fallback");
    const activationHelperSource = source.slice(source.indexOf("function armParentNavigationTrigger"),
        source.indexOf("function resetSecondaryViewport"));
    assert(activationHelperSource.includes('trigger.addEventListener("pointerdown"') &&
        activationHelperSource.includes('{ passive: true }') &&
        !activationHelperSource.includes("document.") && !activationHelperSource.includes("window.addEventListener") &&
        !activationHelperSource.includes("preventDefault") && !activationHelperSource.includes("stopPropagation") &&
        !activationHelperSource.includes(".focus(") && !activationHelperSource.includes("scrollTop =") &&
        !activationHelperSource.includes("setTimeout") && !activationHelperSource.includes("requestAnimationFrame"),
        "preactivation sampling must stay passive and trigger-local without input ownership, focus/scroll writes, global listeners, or scheduling");

    console.log("Danmu smart-match frontend regression checks passed.");
}

main().catch(function (error) {
    console.error(error);
    process.exitCode = 1;
});
