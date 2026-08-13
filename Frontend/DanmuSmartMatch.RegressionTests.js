"use strict";

const fs = require("fs");
const vm = require("vm");
const path = require("path");

function assert(condition, message) {
    if (!condition) throw new Error(message);
}

class FakeElement {
    constructor(tagName) {
        this.tagName = String(tagName || "div").toUpperCase();
        this.children = [];
        this.parentElement = null;
        this.dataset = {};
        this.style = {};
        this.attributes = {};
        this.listeners = {};
        this.className = "";
        this.textContent = "";
        this.isConnected = true;
        this.disabled = false;
        this.classList = { contains: name => this.className.split(/\s+/).includes(name) };
    }

    append(...children) { children.forEach(child => this.appendChild(child)); }
    appendChild(child) {
        child.parentElement = this;
        this.children.push(child);
        return child;
    }
    replaceChildren(...children) {
        this.children.forEach(child => { child.parentElement = null; });
        this.children = [];
        this.append(...children);
    }
    before(child) {
        const index = this.parentElement.children.indexOf(this);
        child.parentElement = this.parentElement;
        this.parentElement.children.splice(index, 0, child);
    }
    remove() {
        if (this.parentElement) {
            const index = this.parentElement.children.indexOf(this);
            if (index >= 0) this.parentElement.children.splice(index, 1);
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
        if (deep) this.children.forEach(child => clone.appendChild(child.cloneNode(true)));
        return clone;
    }
    setAttribute(name, value) { this.attributes[name] = String(value); }
    getAttribute(name) { return this.attributes[name] === undefined ? null : this.attributes[name]; }
    removeAttribute(name) { delete this.attributes[name]; }
    addEventListener(type, listener) {
        (this.listeners[type] || (this.listeners[type] = [])).push(listener);
    }
    async dispatch(type) {
        const event = {
            target: this,
            preventDefault: function () {},
            stopPropagation: function () {},
            stopImmediatePropagation: function () {}
        };
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
            node.children.forEach(child => {
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
    addEventListener: function (type, listener) {
        (this.listeners[type] || (this.listeners[type] = [])).push(listener);
    },
    removeEventListener: function (type, listener) {
        const listeners = this.listeners[type] || [];
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
const historyStub = {
    state: null,
    pushState: function (state, _title, url) {
        historyEntries.push({ state: state, url: url });
        this.state = state;
    },
    back: function () {
        if (historyEntries.length > 1) historyEntries.pop();
        const current = historyEntries[historyEntries.length - 1];
        this.state = current.state;
        (windowListeners.popstate || []).slice().forEach(listener => listener({ state: current.state }));
    }
};
const context = {
    window: {
        location: { hash: "", href: "http://emby.test/web/index.html#!/item?id=series" },
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
    assert((source.match(/__embyDanmuSmartMenuV23/g) || []).length === 1 &&
        !source.includes("__embyDanmuSmartMenuV22") && !source.includes("__embyDanmuSmartMenuV21"),
        "the r7 frontend installation flag should be V23 exactly once");
    assert(!source.includes("MAPPING_PROTOCOL_GENERATION") && source.includes("var MAPPING_PROTOCOL_VERSION = 21"),
        "the r7 UI must retain the backend numeric V21 mapping protocol and server-authored plan generation");
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
        partialCall.parameters.mappingProtocolVersion === 21 &&
        partialCall.parameters.mappingProtocolGeneration === undefined,
        "a whole-Series partial HTTP failure must retain completed sibling Seasons and every API call must carry the V21 fence");
    partialSeriesDialog.forceClose();
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
        explicitRematch.keyword === "小书痴的下克上",
        "editing the input must switch to an explicit isolated custom-keyword search");
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

    const compositeSeason = {
        SeriesId: "series", SeasonId: "season-composite", SeasonNumber: 1,
        SeasonName: "Composite", EpisodeCount: 5,
        MappingProtocolVersion: 21, PlanGeneration: 7341,
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
                SourceEpisodeNumber: number, Origin: "scored"
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
        SourceStartEpisodeNumber: 1, Origin: "manual"
    }];
    const virtualGroups = hooks.compositeVirtualGroups(compositeSeason, compositeSelections);
    assert(virtualGroups.map(group => group.kind).join(",") === "mapped,manual,unmatched" &&
        virtualGroups[2].episodes.length === 1 && virtualGroups[2].episodes[0].ItemId === "episode-5",
        "a manual temporary season must consume only its chosen range and leave the next unmatched run visible");
    assert(hooks.compositeHasDownloadableMappings(compositeSeason, compositeSelections),
        "exact mappings or a manual virtual season must permit downloading the confirmed subset");
    const compactSelections = hooks.compositeRequestSelections(compositeSelections, compositeSeason);
    const currentSeasonRequest = hooks.seasonRequestParameters(compositeSeason);
    assert(currentSeasonRequest.mappingProtocolVersion === 21 &&
        currentSeasonRequest.planGeneration === compositeSeason.PlanGeneration &&
        currentSeasonRequest.mappingProtocolGeneration === undefined,
        "every Season rebuild/rematch/download request must echo the server-authored numeric V21 plan generation");
    assert(compactSelections.length === 2 && compactSelections[0].CandidateId === "frieren-s1" &&
        compactSelections[0].LocalStartEpisodeItemId === "episode-1" && compactSelections[0].RequestedEpisodeCount === 2 &&
        compactSelections[1].CandidateId === "frieren-s2" && compactSelections[1].SourceStartEpisodeNumber === 1 &&
        JSON.stringify(compactSelections).indexOf("server-only") < 0,
        "the browser must resubmit the verified S1 base group together with manual S2 intent and never expose CommentId values");
    const cachedV20Season = Object.assign({}, compositeSeason, {
        MappingProtocolVersion: 20, PlanGeneration: compositeSeason.PlanGeneration
    });
    assert(!hooks.hasCurrentMappingContract(cachedV20Season) && !hooks.hasCompositePlan(cachedV20Season) &&
        hooks.compositeRequestSelections(compositeSelections, cachedV20Season).length === 0,
        "a cached V20 Season draft must be discarded and cannot be submitted or restored");
    assert(!hooks.hasCurrentMappingContract(Object.assign({}, compositeSeason, { PlanGeneration: 0 })) &&
        !hooks.hasCurrentMappingContract(Object.assign({}, compositeSeason, { PlanGeneration: "invalid" })),
        "V21 requires a positive numeric server-authored plan generation");
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
        MappingProtocolVersion: 21, PlanGeneration: 8101, RequiresCompositeMapping: true,
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
            MatchOrigin: "scored", Episodes: [{ ItemId: "s1e1", ParentSeasonNumber: 1,
                EpisodeNumber: 1, LocalDisplayLabel: "S01E01", SourceEpisodeNumber: 1 }] },
            { IsTemporary: false, Site: "Dandan", CandidateId: "foreign-source",
                MatchOrigin: "scored", Episodes: [{ ItemId: "s0e1", ParentSeasonNumber: 0,
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
        MappingProtocolVersion: 21, PlanGeneration: 8102, RequiresCompositeMapping: true,
        CompositePlan: {
            OrderedEpisodes: [{ ItemId: "s0-own-e1", ParentSeasonNumber: 0, EpisodeNumber: 1,
                LocalDisplayLabel: "S00E01" }],
            Mappings: [{ LocalEpisodeItemId: "s0-own-e1",
                Source: { ProviderId: "Dandan", MediaId: "special-source" },
                SourceEpisodeId: "special-1", SourceEpisodeNumber: 1, Origin: "scored" }],
            UnmatchedRuns: []
        },
        CompositeGroups: [{ IsTemporary: false, Site: "Dandan", CandidateId: "special-source",
            MatchOrigin: "scored", Episodes: [{ ItemId: "s0-own-e1", ParentSeasonNumber: 0,
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
    hooks.discardIncompatibleSeasonDrafts(null, [cachedV20Season], incompatibleSelections);
    assert(!incompatibleSelections["series::season-composite"] &&
        !incompatibleSelections.__compositeSelections["series::season-composite"],
        "a fresh r5 dialog must clear cached V20 candidate and compact-selection state without submitting it");
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
        "V21 must discard all cached local-identifier-derived batch origins");
    const staleSelections = {};
    staleSelections.__compositeSelections = {};
    staleSelections.__compositeSelections["series::season-composite"] = [{
        LocalStartEpisodeItemId: "episode-1", RequestedEpisodeCount: 2,
        Source: { ProviderId: "Dandan", MediaId: "obsolete" }, SourceStartEpisodeNumber: 1
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
        MappingProtocolVersion: 21, PlanGeneration: 7342,
        RequiresCompositeMapping: true,
        CompositeGroups: [{ IsTemporary: false, Site: "Dandan", CandidateId: "s1",
            MatchScore: 0, Episodes: [{ ItemId: "a", ParentSeasonNumber: 2, EpisodeNumber: 1 }] },
            { IsTemporary: true, MatchScore: 0, ScoreOrigin: "search-confidence",
                Episodes: [{ ItemId: "b", ParentSeasonNumber: 2, EpisodeNumber: 2 }] }]
    };
    assert(hooks.hasCompositePlan(groupOnlySeason) &&
        hooks.compositeVirtualGroups(groupOnlySeason, {}).map(group => group.kind).join(",") === "mapped,unmatched" &&
        hooks.compositeHasDownloadableMappings(groupOnlySeason, {}) &&
        hooks.compositeRequestSelections({}, groupOnlySeason).length === 1 &&
        hooks.compositeRequestSelections({}, groupOnlySeason)[0].CandidateId === "s1",
        "the UI must also accept the compact CompositeGroups preview contract during controller rollout");
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
        unmatchedZeroCard && !allVisibleText(unmatchedZeroCard).includes("\u5339\u914d\u5206"),
        "an explicit mapped zero score may render, but an unmatched temporary season must never render a score");
    assert(scoreCards.every(card => card.querySelector(".danmuVirtualSeasonTitle").textContent.indexOf("\u4e34\u65f6\u5b63 ") === 0),
        "mapped and unmatched virtual ranges must use the same temporary-season title convention");
    unmatchedScoreDialog.forceClose();

    const liveCompositeGroupsSeason = {
        SeriesId: "one-punch", SeasonId: "one-punch-s1", SeasonNumber: 1,
        SeasonName: "一拳超人 第一季", EpisodeCount: 2,
        MappingProtocolVersion: 21, PlanGeneration: 202034,
        RequiresCompositeMapping: true,
        CompositePlan: {
            OrderedEpisodes: [{ ItemId: "local-dandan-secret", ParentSeasonNumber: 1,
                EpisodeNumber: 1, LocalDisplayLabel: "S01E01" },
                { ItemId: "local-youku-secret", ParentSeasonNumber: 1,
                    EpisodeNumber: 2, LocalDisplayLabel: "S01E02" }],
            Mappings: [{ LocalEpisodeItemId: "local-dandan-secret",
                Source: { ProviderId: "DandanID", MediaId: "11123" },
                SourceEpisodeId: "111230001", SourceEpisodeNumber: 1, Origin: "scored" },
                { LocalEpisodeItemId: "local-youku-secret",
                    Source: { ProviderId: "YoukuID", MediaId: "cfd9e3748c8a4d52b10f" },
                    SourceEpisodeId: "youku-source-secret", SourceEpisodeNumber: 5, Origin: "scored" }],
            UnmatchedRuns: []
        },
        CompositeGroups: [{
            IsTemporary: false, Site: "DandanID", CandidateId: "11123",
            SourceStartEpisodeId: "111230001", SourceStartEpisodeNumber: 1,
            MatchOrigin: "scored", MatchScore: 0.934, ScoreOrigin: "search-confidence",
            Episodes: [{ ItemId: "local-dandan-secret", ParentSeasonNumber: 1,
                EpisodeNumber: 1, LocalDisplayLabel: "S01E01", EpisodeName: "本地标题一",
                SourceEpisodeNumber: 1, SourceEpisodeTitle: "来源标题一" }]
        }, {
            IsTemporary: false, Site: "YoukuID", CandidateId: "cfd9e3748c8a4d52b10f",
            SourceStartEpisodeId: "youku-source-secret", SourceStartEpisodeNumber: 5,
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
        liveWireSelections[1].SourceStartEpisodeId === "youku-source-secret",
        "visible-text sanitization must not delete trusted CompositeGroups wire identities");
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
    assert(compactSelections.every(selection => selection.MappingProtocolVersion === 21 &&
        selection.PlanGeneration === compositeSeason.PlanGeneration &&
        selection.MappingProtocolGeneration === undefined) &&
        compactSelections[1].LocalStartEpisodeItemId === "episode-3" &&
        compactSelections[1].CandidateId === "frieren-s2",
        "sanitizing visible text must preserve compact wire identity and add the V21 generation fence");
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
                    Origin: selection.MatchOrigin || "manual"
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
        SourceStartEpisodeNumber: 1, Origin: "manual"
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
            SourceEpisodeId: "mapped-source-1", SourceEpisodeNumber: 1, Origin: "scored"
        };
        const season = {
            SeriesId: "rematch-series-" + suffix, SeasonId: seasonId, SeasonNumber: 1,
            SeasonName: "Rematch " + suffix, EpisodeCount: 2,
            MappingProtocolVersion: 21, PlanGeneration: 9100 + suffix.length,
            RequiresCompositeMapping: true,
            CompositePlan: {
                OrderedEpisodes: ordered, Mappings: [mapping],
                UnmatchedRuns: [{ Episodes: [ordered[1]] }]
            },
            CompositeGroups: [{ IsTemporary: false, Site: "Dandan", CandidateId: "mapped-source",
                MatchOrigin: "scored", Episodes: [{ ItemId: ordered[0].ItemId,
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
                SourceStartEpisodeNumber: 4, Origin: "manual", SelectionEvidenceToken: "manual-private"
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
        await rematch.dispatch("click");
        await waitUntil(() => dialog.body.querySelectorAll(".danmuCandidate").length === 1,
            entry + "/" + kind + " rematch should enter the temporary-range picker");
        if (returnPath === "android") dialog.handleAndroidBack();
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

    const malformedDialog = hooks.openDialog("malformed preview");
    apiResponses.MatchPreview = {};
    await hooks.runSmartDownload({ Id: "malformed", Type: "Movie", Name: "Malformed" }, malformedDialog);
    assert(!malformedDialog.androidBackLocked && malformedDialog.footer.children.some(button =>
        button.textContent === "重试搜索"),
        "malformed preview responses must restore a retryable dialog instead of leaving busy controls stuck");
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

    const androidDialog = hooks.openDialog("android-navigation");
    let returnedToParent = 0;
    androidDialog.setBackHandler(function () {
        returnedToParent++;
        androidDialog.setBackHandler(null);
    });
    historyStub.back();
    assert(returnedToParent === 1 && androidDialog.overlay.isConnected &&
        historyStub.state && historyStub.state.__danmuSmartDialog,
        "Android history back from a secondary view must return to its parent and restore the guard");
    historyStub.back();
    assert(!androidDialog.overlay.isConnected && hooks.activeDialogCount() === 0 &&
        (windowListeners.popstate || []).length === 0 &&
        (documentStub.listeners.backbutton || []).length === 0,
        "Android history back from the top-level view must close and clean up the dialog");

    const protectedAndroidDialog = hooks.openDialog("protected-android");
    protectedAndroidDialog.closable = false;
    historyStub.back();
    assert(protectedAndroidDialog.overlay.isConnected &&
        historyStub.state && historyStub.state.__danmuSmartDialog,
        "Android history back must not dismiss a protected download view and must restore its guard");
    protectedAndroidDialog.closable = true;
    await documentStub.dispatchKey("Escape");
    assert(!protectedAndroidDialog.overlay.isConnected,
        "the existing Escape close path must remain available after protected Android back handling");

    const nativeBackDialog = hooks.openDialog("native-backbutton");
    let nativeParentReturns = 0;
    nativeBackDialog.setBackHandler(function () {
        nativeParentReturns++;
        nativeBackDialog.setBackHandler(null);
    });
    const childBackButton = documentStub.dispatchEvent("backbutton");
    assert(nativeParentReturns === 1 && nativeBackDialog.overlay.isConnected &&
        childBackButton.defaultPrevented && childBackButton.propagationStopped,
        "a native Android backbutton event must return from a secondary view without reaching Emby");
    const topBackButton = documentStub.dispatchEvent("backbutton");
    assert(!nativeBackDialog.overlay.isConnected && topBackButton.defaultPrevented,
        "a native Android backbutton event at the top level must close the smart-match dialog");

    const busyBackDialog = hooks.openDialog("busy-android-back");
    hooks.setBusy(busyBackDialog, "searching");
    historyStub.back();
    const busyNativeBack = documentStub.dispatchEvent("backbutton");
    assert(busyBackDialog.overlay.isConnected && busyBackDialog.androidBackLocked &&
        historyStub.state && historyStub.state.__danmuSmartDialog &&
        busyNativeBack.defaultPrevented && busyNativeBack.propagationStopped,
        "history and native Android back must be consumed while a smart-match search is busy");
    const busyClose = busyBackDialog.overlay.children[0].children[0].children[1];
    await busyClose.dispatch("click");
    assert(!busyBackDialog.overlay.isConnected,
        "the top-right close button must remain effective while Android back is locked");

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
    historyStub.back();
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
    historyStub.back();
    assert(seriesBackDialog.overlay.isConnected &&
        seriesBackDialog.title.textContent === "整部剧弹幕智能匹配",
        "Android back from a real Series Season candidate view must restore the Series overview");
    historyStub.back();
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
        !failedDetailDialog.body.querySelector(".danmuBusy"),
        "detail-resolution failure must restore the intact candidate list and release busy controls");
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
            { Site: "Fake", Id: "candidate", Name: "Candidate" },
            type === "Episode" ? 4 : null,
            type === "Episode" ? "source-episode-exact" : null,
            true);
        const startCall = apiCalls.find(call => call.option === "StartTrackedDownload");
        if (type === "Episode") {
            assert(startCall.parameters.sourceEpisodeId === "source-episode-exact" &&
                !Object.prototype.hasOwnProperty.call(startCall.parameters, "commentId"),
                "episode confirmation must submit the exact resolved sourceEpisodeId without CommentId or positional guessing");
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

    console.log("Danmu smart-match frontend regression checks passed.");
}

main().catch(function (error) {
    console.error(error);
    process.exitCode = 1;
});
