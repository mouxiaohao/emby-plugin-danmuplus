/*
 * Emby.CustomCssJS: 电视剧/季/集/电影智能匹配并一键下载弹幕
 * 适用于 Emby 4.9.x + 本方案配套的 Emby.Plugin.Danmu 2.0.3r2 DLL
 */
(function () {
    "use strict";

    // V18 adds bounded two-stage episode candidate resolution and exact source episode binding.
    var INSTALL_FLAG = "__embyDanmuSmartMenuV18";
    var BUTTON_ID = "danmu-bulk-download";
    var activeDialogs = [];
    var dialogHistoryGeneration = 0;
    var ignoredDialogHistoryPops = 0;

    if (window[INSTALL_FLAG]) {
        return;
    }
    window[INSTALL_FLAG] = true;

    var pendingContext = null;
    var retryTimer = 0;
    var contextGeneration = 0;

    function value(object, pascal, camel, fallback) {
        if (!object) {
            return fallback;
        }
        if (object[pascal] !== undefined && object[pascal] !== null) {
            return object[pascal];
        }
        if (object[camel] !== undefined && object[camel] !== null) {
            return object[camel];
        }
        return fallback;
    }

    function asJson(result) {
        if (typeof result === "string") {
            return JSON.parse(result);
        }
        return result || {};
    }

    function getCurrentItemId() {
        var hash = window.location.hash || "";
        var question = hash.indexOf("?");
        if (question < 0) {
            return null;
        }
        return new URLSearchParams(hash.slice(question + 1)).get("id");
    }

    function getMenuItemId(menu) {
        var dataset = menu.dataset || {};
        var direct = plausibleItemId(dataset.itemId || dataset.itemid);
        if (direct) return direct;
        var image = menu.querySelector(".actionsheetItemPreviewImage-bg");
        var background = image && image.style.backgroundImage;
        var match = background && background.match(/\/Items\/([^/?]+)\/Images/i);
        if (match) return decodeURIComponent(match[1]);
        var links = Array.from(menu.querySelectorAll ? menu.querySelectorAll("a[href]") : []);
        for (var index = 0; index < links.length; index++) {
            var href = links[index].getAttribute("href");
            var hrefMatch = href && href.match(/[?&]id=([^&#]+)/i);
            var fromHref = hrefMatch && plausibleItemId(decodeURIComponent(hrefMatch[1]));
            if (fromHref) return fromHref;
        }
        return null;
    }

    function plausibleItemId(value) {
        var id = String(value || "").trim();
        return id.length >= 8 && !/^(menu|more|scan|none)$/i.test(id) ? id : null;
    }

    function getTriggerItemId(trigger) {
        var nodes = [];
        var current = trigger;
        while (current && current !== document.body && nodes.length < 8) {
            nodes.push(current);
            if (current.matches && current.matches(".card,.listItem,.visualCardBox")) break;
            current = current.parentElement;
        }
        for (var index = 0; index < nodes.length; index++) {
            var node = nodes[index];
            var dataset = node.dataset || {};
            var direct = plausibleItemId(dataset.itemId || dataset.itemid || dataset.id);
            if (direct) return direct;
            var links = [node].concat(Array.from(node.querySelectorAll ? node.querySelectorAll("a[href]") : []));
            for (var linkIndex = 0; linkIndex < links.length; linkIndex++) {
                var href = links[linkIndex].getAttribute && links[linkIndex].getAttribute("href");
                var match = href && href.match(/[?&]id=([^&#]+)/i);
                var fromHref = match && plausibleItemId(decodeURIComponent(match[1]));
                if (fromHref) return fromHref;
            }
        }
        return null;
    }

    function manualSearchDefault(item, target) {
        if (item && item.Type === "Movie") return item.Name || value(target, "ItemName", "itemName", "");
        return value(target, "ParentName", "parentName",
            value(target, "SeriesName", "seriesName", item && (item.SeriesName || item.Name) || ""));
    }

    function isSupportedItemType(type) {
        return ["Series", "Season", "Episode", "Movie"].indexOf(type) >= 0;
    }

    function actionLabel(type) {
        return type === "Series" ? "智能匹配并下载整部剧弹幕" :
            (type === "Season" ? "智能匹配并下载本季弹幕" :
                (type === "Episode" ? "智能匹配并下载本集弹幕" : "智能匹配并下载电影弹幕"));
    }

    function setPendingContext(id) {
        contextGeneration++;
        pendingContext = { id: id, generation: contextGeneration, expires: Date.now() + 5000 };
        return pendingContext;
    }

    function resolveMenuContextId(contextId, menuItemId) {
        if (contextId && menuItemId && contextId !== menuItemId) return null;
        return contextId || menuItemId || null;
    }

    function getGestureItemId(target) {
        return target && target.closest ? getTriggerItemId(target) : null;
    }

    function openedActionSheetContextId(context, menuItemId, menuCount, now) {
        if (menuCount !== 1) return null;
        if (menuItemId) return menuItemId;
        return context && context.id && now <= context.expires ? context.id : null;
    }

    function apiRequest(itemId, option, parameters) {
        var query = Object.assign({ option: option }, parameters || {});
        var transport = ApiClient.ajax({
            url: ApiClient.getUrl("plugin/danmu/" + encodeURIComponent(itemId), query),
            type: "GET",
            dataType: "json",
            timeout: 180000
        });
        return {
            transport: transport,
            promise: Promise.resolve(transport).then(asJson)
        };
    }

    function api(itemId, option, parameters) {
        return apiRequest(itemId, option, parameters).promise;
    }

    function nextSearchOperationId() {
        if (window.crypto && typeof window.crypto.randomUUID === "function") {
            return "danmu-" + window.crypto.randomUUID();
        }
        return "danmu-" + Date.now().toString(36) + "-" + Math.random().toString(36).slice(2);
    }

    function isAbortError(error) {
        var name = String(error && (error.name || error.statusText || "") || "").toLowerCase();
        return name === "abort" || name === "aborted" || name === "cancellederror" ||
            name === "operationcanceledexception" || name === "aborterror";
    }

    function isCurrentSearch(dialog, search) {
        return Boolean(dialog && search && dialog.activeSearch === search &&
            dialog.searchGeneration === search.generation && dialog.overlay && dialog.overlay.isConnected);
    }

    function cancelDialogSearch(dialog, restore) {
        var search = dialog && dialog.activeSearch;
        if (!search) return false;
        dialog.searchGeneration++;
        dialog.activeSearch = null;
        dialog.androidBackLocked = false;
        search.cancelled = true;
        if (search.transport && typeof search.transport.abort === "function") {
            try { search.transport.abort(); } catch (_error) { /* transport is already terminal */ }
        }
        // Cancellation is deliberately best-effort: transport abortion reduces browser work,
        // while the server operation id releases provider requests and their deadlines.
        if (search.operationId) {
            api(search.itemId, "CancelSearch", { searchOperationId: search.operationId })
                .catch(function () { /* the local generation fence remains authoritative */ });
        }
        if (restore && typeof search.restore === "function" && dialog.overlay.isConnected) {
            search.restore("cancelled");
            notify("已取消当前搜索。", false);
        }
        return true;
    }

    async function runDialogSearch(dialog, itemId, phase, parameters, message, restore, option) {
        cancelDialogSearch(dialog, false);
        var search = {
            generation: ++dialog.searchGeneration,
            operationId: nextSearchOperationId(),
            itemId: itemId,
            phase: phase || "provider-search",
            restore: restore,
            cancelled: false,
            transport: null
        };
        var query = Object.assign({}, parameters || {}, {
            searchOperationId: search.operationId
        });
        query.searchScope = query.searchScope || search.phase;
        dialog.activeSearch = search;
        setBusy(dialog, message, search);
        try {
            var request = apiRequest(itemId, option || "MatchPreview", query);
            search.transport = request.transport;
            var result = await request.promise;
            return isCurrentSearch(dialog, search) ? result : null;
        } catch (error) {
            if (!isCurrentSearch(dialog, search)) return null;
            if (typeof restore === "function") {
                restore(search.cancelled || isAbortError(error) ? "cancelled" : "error", error);
            }
            return null;
        } finally {
            if (dialog.activeSearch === search) {
                dialog.activeSearch = null;
                dialog.androidBackLocked = false;
            }
        }
    }

    function notify(message, isError) {
        var old = document.getElementById("danmu-smart-toast");
        if (old) {
            old.remove();
        }

        var toast = document.createElement("div");
        toast.id = "danmu-smart-toast";
        toast.textContent = message;
        toast.style.cssText = [
            "position:fixed", "left:50%", "bottom:3.5rem", "transform:translateX(-50%)",
            "z-index:100002", "max-width:min(40rem,calc(100vw - 2rem))", "padding:.8rem 1.1rem",
            "border-radius:.45rem", "box-shadow:0 4px 20px rgba(0,0,0,.35)", "color:#fff",
            "font-size:1rem", "line-height:1.4", "text-align:center",
            "background:" + (isError ? "#c62828" : "#2e7d32")
        ].join(";");
        document.body.appendChild(toast);
        window.setTimeout(function () { toast.remove(); }, 6000);
    }

    function closeMenu(menu) {
        try {
            if (typeof require === "function") {
                require(["dialogHelper"], function (dialogHelper) { dialogHelper.close(menu); });
                return;
            }
        } catch (error) {
            console.debug("[Danmu Smart Match] 使用备用方式关闭菜单", error);
        }
        var backdrop = menu.closest(".dialogContainer.dialogBackdrop");
        if (backdrop) {
            backdrop.click();
        }
    }

    function ensureStyles() {
        if (document.getElementById("danmu-smart-style")) {
            return;
        }
        var style = document.createElement("style");
        style.id = "danmu-smart-style";
        style.textContent = [
            ".danmuSmartOverlay{position:fixed;inset:0;z-index:100001;background:rgba(0,0,0,.7);display:flex;align-items:center;justify-content:center;padding:1rem}",
            ".danmuSmartCard{width:min(54rem,100%);max-height:min(48rem,92vh);display:flex;flex-direction:column;background:#202020;color:#fff;border-radius:.55rem;box-shadow:0 10px 40px rgba(0,0,0,.6);overflow:hidden}",
            ".danmuSmartHeader{display:flex;align-items:center;gap:1rem;padding:1rem 1.2rem;border-bottom:1px solid rgba(255,255,255,.14)}",
            ".danmuSmartTitle{font-size:1.25rem;font-weight:600;flex:1}",
            ".danmuSmartClose,.danmuSmartButton{border:0;border-radius:.35rem;color:#fff;cursor:pointer}",
            ".danmuSmartClose{background:transparent;font-size:1.7rem;padding:.1rem .45rem}",
            ".danmuSmartBody{padding:1rem 1.2rem;overflow:auto;line-height:1.5}",
            ".danmuSmartFooter{display:flex;justify-content:flex-end;gap:.7rem;padding:.9rem 1.2rem;border-top:1px solid rgba(255,255,255,.14)}",
            ".danmuSmartButton{background:#555;padding:.65rem 1rem;font-size:.95rem}",
            ".danmuSmartButton.primary{background:#00a4dc}.danmuSmartButton.danger{background:#c62828}.danmuSmartButton:disabled{opacity:.5;cursor:default}",
            ".danmuForceRefresh{display:flex;align-items:flex-start;gap:.65rem;margin:1rem 0 .2rem;padding:.8rem;border:1px solid rgba(255,255,255,.16);border-radius:.4rem;background:rgba(255,255,255,.035);cursor:pointer}.danmuForceRefresh input{margin-top:.25rem}.danmuForceRefresh strong{display:block}.danmuForceRefresh small{display:block;opacity:.72;margin-top:.15rem}",
            ".danmuSmartSearch{display:flex;gap:.6rem;margin-bottom:1rem}.danmuSmartSearch input{flex:1;min-width:0;padding:.65rem .75rem;border:1px solid #777;border-radius:.35rem;background:#111;color:#fff;font-size:1rem}",
            ".danmuCandidate{display:flex;gap:.7rem;padding:.8rem;border:1px solid rgba(255,255,255,.16);border-radius:.4rem;margin:.55rem 0;cursor:pointer;background:rgba(255,255,255,.035)}",
            ".danmuCandidate:hover{background:rgba(255,255,255,.08)}.danmuCandidate input{margin-top:.3rem}",
            ".danmuSourceEpisode{display:none;align-items:center;gap:.35rem;white-space:nowrap;margin-left:.6rem}.danmuSourceEpisode.active{display:flex}.danmuSourceEpisode input{width:5.2rem;margin:0;padding:.45rem;border:1px solid #777;border-radius:.3rem;background:#111;color:#fff}",
            ".danmuCandidateMain{flex:1;min-width:0}.danmuCandidateTitle{font-weight:600;word-break:break-all}",
            ".danmuCandidateMeta{opacity:.8;font-size:.9rem;margin-top:.2rem}.danmuCandidateReason{color:#8dd7f2;font-size:.88rem;margin-top:.25rem}",
            ".danmuSeasonProblem{padding:.75rem 0;border-bottom:1px solid rgba(255,255,255,.12)}",
            ".danmuSeasonSummary{display:grid;grid-template-columns:minmax(0,1fr) auto;gap:.75rem;align-items:center;padding:.9rem;margin:.65rem 0;border:1px solid rgba(255,255,255,.16);border-radius:.45rem;background:rgba(255,255,255,.025)}",
            ".danmuSeasonSummary.matched{border-color:#2e7d32}.danmuSeasonSummary.unmatched{border-color:#c62828}.danmuSeasonSummaryTitle{font-weight:600}.danmuSeasonSummaryState{margin:.22rem 0;font-size:.92rem}.danmuSeasonSummary.matched .danmuSeasonSummaryState{color:#81c784}.danmuSeasonSummary.unmatched .danmuSeasonSummaryState{color:#ef9a9a}.danmuSeasonSummaryDetail{opacity:.78;font-size:.88rem;word-break:break-all}",
            ".danmuCompositeSeason{margin:.7rem 0;padding:.8rem;border:1px solid rgba(255,255,255,.2);border-radius:.48rem;background:rgba(255,255,255,.02)}.danmuCompositeHeader{font-weight:600;margin-bottom:.45rem}.danmuCompositeHint{opacity:.78;font-size:.88rem;margin:.25rem 0 .55rem}.danmuVirtualSeason{display:grid;grid-template-columns:minmax(0,1fr) auto;gap:.7rem;align-items:center;margin:.45rem 0;padding:.7rem;border-left:3px solid #666;border-radius:.35rem;background:rgba(255,255,255,.035)}.danmuVirtualSeason.matched{border-left-color:#2e7d32}.danmuVirtualSeason.unmatched{border-left-color:#ffb300}.danmuVirtualSeasonTitle{font-weight:600}.danmuVirtualSeasonDetail{opacity:.8;font-size:.88rem;word-break:break-all}.danmuCompositeWarning{margin:.5rem 0;padding:.55rem .65rem;border-radius:.35rem;background:rgba(255,179,0,.15);color:#ffe082;font-size:.88rem}.danmuCompositeInputs{display:flex;flex-wrap:wrap;gap:.6rem;margin:.65rem 0}.danmuCompositeInputs label{display:flex;align-items:center;gap:.35rem;font-size:.9rem}.danmuCompositeInputs input{width:5.8rem;padding:.4rem;border:1px solid #777;border-radius:.3rem;background:#111;color:#fff}",
            ".danmuProgressSeason{padding:.9rem;margin:.7rem 0;border:1px solid rgba(255,255,255,.14);border-radius:.45rem;background:rgba(255,255,255,.025)}",
            ".danmuProgressSeason.running{border-color:#00a4dc}.danmuProgressSeason.success{border-color:#2e7d32}.danmuProgressSeason.warning{border-color:#ffb300}.danmuProgressSeason.failed{border-color:#c62828}.danmuProgressSeason.cancelled{border-color:#777}",
            ".danmuProgressTitle{display:flex;justify-content:space-between;gap:1rem;font-weight:600}.danmuProgressMeta{opacity:.8;font-size:.9rem;margin:.3rem 0 .55rem}",
            ".danmuEpisodeProgress{display:grid;grid-template-columns:minmax(4.5rem,auto) minmax(8rem,1fr) minmax(6rem,1.3fr) auto;gap:.55rem;align-items:center;padding:.36rem .2rem;border-top:1px solid rgba(255,255,255,.08);font-size:.9rem}",
            ".danmuEpisodeProgress.running{color:#8dd7f2}.danmuEpisodeProgress.success{color:#81c784}.danmuEpisodeProgress.partial{color:#ffd54f}.danmuEpisodeProgress.skipped{color:#ffb74d}.danmuEpisodeProgress.failed{color:#ef9a9a}.danmuEpisodeProgress.cancelled{color:#aaa}.danmuEpisodeProgress.pending,.danmuEpisodeProgress.queued{opacity:.55}",
            ".danmuEpisodeRetry{border:1px solid rgba(255,255,255,.28);border-radius:.3rem;background:#444;color:#fff;padding:.28rem .55rem;cursor:pointer;white-space:nowrap}.danmuEpisodeRetry:hover{background:#666}.danmuEpisodeRetry:disabled{opacity:.45;cursor:default}",
            ".danmuProgressSummary{position:sticky;top:-1rem;z-index:2;margin:-1rem -1.2rem .8rem;padding:.8rem 1.2rem;background:#202020;border-bottom:1px solid rgba(255,255,255,.14)}",
            ".danmuMuted{opacity:.72}.danmuBusy{text-align:center;padding:2.2rem 1rem;font-size:1.05rem}",
            "@media(max-width:520px){.danmuSmartOverlay{padding:0}.danmuSmartCard{height:100%;max-height:none;border-radius:0}.danmuSmartHeader{padding-top:calc(1.75rem + env(safe-area-inset-top,0px))}.danmuSmartSearch{flex-wrap:wrap}.danmuSmartSearch input{flex-basis:100%}.danmuEpisodeProgress{grid-template-columns:auto 1fr auto}.danmuEpisodeProgress>span:nth-child(3){grid-column:1/3}}"
        ].join("");
        document.head.appendChild(style);
    }

    function openDialog(title) {
        ensureStyles();
        var overlay = document.createElement("div");
        overlay.className = "danmuSmartOverlay";
        var card = document.createElement("div");
        card.className = "danmuSmartCard";
        var header = document.createElement("div");
        header.className = "danmuSmartHeader";
        var heading = document.createElement("div");
        heading.className = "danmuSmartTitle";
        heading.textContent = title;
        var close = document.createElement("button");
        close.className = "danmuSmartClose";
        close.type = "button";
        close.setAttribute("aria-label", "关闭");
        close.textContent = "×";
        var body = document.createElement("div");
        body.className = "danmuSmartBody";
        var footer = document.createElement("div");
        footer.className = "danmuSmartFooter";
        header.append(heading, close);
        card.append(header, body, footer);
        overlay.appendChild(card);
        document.body.appendChild(overlay);
        var disposed = false;
        var backHandler = null;
        var historyGuardActive = false;
        var historyToken = "danmu-smart-" + (++dialogHistoryGeneration);
        var dialog = {
            overlay: overlay,
            title: heading,
            body: body,
            footer: footer,
            closable: true,
            androidBackLocked: false,
            searchGeneration: 0,
            activeSearch: null,
            forceRefresh: false,
            compositeDraft: { exclusions: {}, removedRuns: {} },
            close: function () {
                return dialog.closable ? dispose(false) : false;
            },
            forceClose: function () { return dispose(false); },
            setBackHandler: function (handler) {
                backHandler = typeof handler === "function" ? handler : null;
            },
            handleAndroidBack: function () { return handleBack(false); }
        };
        function isTopmost() {
            for (var index = activeDialogs.length - 1; index >= 0; index--) {
                if (activeDialogs[index].overlay.isConnected) {
                    return activeDialogs[index] === dialog;
                }
            }
            return false;
        }
        function escapeListener(event) {
            if (event.key === "Escape" && isTopmost() && dialog.close()) {
                event.preventDefault();
                event.stopPropagation();
            }
        }
        function hasOwnHistoryGuard() {
            return Boolean(historyGuardActive && window.history && window.history.state &&
                window.history.state.__danmuSmartDialog === historyToken);
        }
        function installHistoryGuard() {
            if (disposed || historyGuardActive || !window.history ||
                typeof window.history.pushState !== "function") {
                return false;
            }
            try {
                var state = Object.assign({}, window.history.state || {});
                state.__danmuSmartDialog = historyToken;
                window.history.pushState(state, "", window.location.href);
                historyGuardActive = true;
                return true;
            } catch (_error) {
                return false;
            }
        }
        function handleBack(fromHistory) {
            if (!isTopmost()) return false;
            if (fromHistory) historyGuardActive = false;
            if (dialog.androidBackLocked) {
                if (fromHistory) installHistoryGuard();
                return true;
            }
            if (!dialog.closable) {
                if (fromHistory) installHistoryGuard();
                return true;
            }
            if (backHandler) {
                backHandler();
                if (fromHistory) installHistoryGuard();
                return true;
            }
            return dispose(Boolean(fromHistory));
        }
        function popStateListener() {
            if (ignoredDialogHistoryPops > 0 && isTopmost()) {
                return;
            }
            handleBack(true);
        }
        function ignoreNextPopState() {
            ignoredDialogHistoryPops++;
            var fallbackTimer = 0;
            function consumeIgnoredPop() {
                ignoredDialogHistoryPops = Math.max(0, ignoredDialogHistoryPops - 1);
                if (window.removeEventListener) {
                    window.removeEventListener("popstate", consumeIgnoredPop);
                }
                if (fallbackTimer && window.clearTimeout) window.clearTimeout(fallbackTimer);
            }
            if (window.addEventListener) {
                window.addEventListener("popstate", consumeIgnoredPop);
                if (window.setTimeout) fallbackTimer = window.setTimeout(consumeIgnoredPop, 1000);
            } else {
                ignoredDialogHistoryPops = Math.max(0, ignoredDialogHistoryPops - 1);
            }
        }
        function backButtonListener(event) {
            if (handleBack(false)) {
                event.preventDefault();
                event.stopPropagation();
            }
        }
        function dispose(fromHistory) {
            if (disposed) return false;
            disposed = true;
            cancelDialogSearch(dialog, false);
            dialog.compositeDraft = { exclusions: {}, removedRuns: {} };
            document.removeEventListener("keydown", escapeListener);
            document.removeEventListener("backbutton", backButtonListener);
            if (window.removeEventListener) window.removeEventListener("popstate", popStateListener);
            var index = activeDialogs.indexOf(dialog);
            if (index >= 0) activeDialogs.splice(index, 1);
            overlay.remove();
            if (!fromHistory && hasOwnHistoryGuard() &&
                typeof window.history.back === "function") {
                historyGuardActive = false;
                ignoreNextPopState();
                window.history.back();
            }
            return true;
        }
        activeDialogs.push(dialog);
        close.addEventListener("click", function () { dialog.close(); });
        document.addEventListener("keydown", escapeListener);
        document.addEventListener("backbutton", backButtonListener);
        if (window.addEventListener) window.addEventListener("popstate", popStateListener);
        installHistoryGuard();
        return dialog;
    }

    function setBusy(dialog, message, search) {
        dialog.androidBackLocked = true;
        dialog.body.replaceChildren();
        dialog.footer.replaceChildren();
        var busy = document.createElement("div");
        busy.className = "danmuBusy";
        busy.textContent = message;
        dialog.body.appendChild(busy);
        if (search) {
            var cancel = document.createElement("button");
            cancel.className = "danmuSmartButton danger";
            cancel.textContent = "取消搜索";
            cancel.addEventListener("click", function () { cancelDialogSearch(dialog, true); });
            dialog.footer.appendChild(cancel);
        }
    }

    function appendForceRefreshOption(dialog) {
        var label = document.createElement("label");
        label.className = "danmuForceRefresh";
        var checkbox = document.createElement("input");
        checkbox.type = "checkbox";
        checkbox.checked = Boolean(dialog.forceRefresh);
        var text = document.createElement("span");
        var title = document.createElement("strong");
        title.textContent = "强制刷新弹幕";
        var help = document.createElement("small");
        help.textContent = "默认不勾选：同一来源的弹幕 XML 在最近 7 天内更新过时，将显示“重复已跳过”。勾选后忽略该限制并重新下载。";
        text.append(title, help);
        label.append(checkbox, text);
        checkbox.addEventListener("change", function () {
            dialog.forceRefresh = checkbox.checked;
        });
        dialog.body.appendChild(label);
    }

    function seasonCandidates(season) {
        return value(season, "Candidates", "candidates", []) || [];
    }

    function seasonAutoSelected(season) {
        return Boolean(value(season, "AutoSelected", "autoSelected", false));
    }

    function seasonSelectionKey(season) {
        var seasonId = String(value(season, "SeasonId", "seasonId", "") || "").trim();
        var seriesId = String(value(season, "SeriesId", "seriesId", "") || "").trim();
        if (seasonId) return seriesId + "::" + seasonId;
        return [seriesId,
            value(season, "SeasonNumber", "seasonNumber", ""),
            value(season, "SeasonName", "seasonName", "")
        ].join("::");
    }

    function seasonRequestParameters(season) {
        return {
            seriesId: value(season, "SeriesId", "seriesId", ""),
            seasonName: value(season, "SeasonName", "seasonName", ""),
            seasonNumber: value(season, "SeasonNumber", "seasonNumber", ""),
            seasonYear: value(season, "Year", "year", "")
        };
    }

    // A deliberate re-match must be unambiguous to both r6 and pre-r6 servers.
    // `force` is retained for older controllers; `mode`/`rematch` express the
    // intent to an r6 controller without asking the browser to make a decision.
    function rematchParameters(parameters) {
        return Object.assign({
            mode: "rematch",
            rematch: "true",
            force: "true"
        }, parameters || {});
    }

    function initializeKeywordIntent(input, button, explicitKeyword) {
        input.dataset.danmuExplicitKeyword = explicitKeyword ? "true" : "false";
        function updateLabel() {
            button.textContent = input.dataset.danmuExplicitKeyword === "true"
                ? "按关键词搜索"
                : "重新智能匹配";
        }
        input.addEventListener("input", function () {
            input.dataset.danmuExplicitKeyword = "true";
            updateLabel();
        });
        updateLabel();
    }

    function keywordRematchParameters(parameters, input) {
        var keyword = String(input && input.value || "").trim();
        var request = Object.assign({}, parameters || {});
        if (input && input.dataset.danmuExplicitKeyword === "true") {
            request.keyword = keyword;
        }
        return rematchParameters(request);
    }

    function matchOrigin(target) {
        return String(value(target, "MatchOrigin", "matchOrigin", "") || "").trim();
    }

    function decisionReason(target) {
        return String(value(target, "DecisionReason", "decisionReason", "") || "").trim();
    }

    function normalizeDecisionCode(code) {
        return String(code || "").trim().toLowerCase()
            .replace(/[\s_]+/g, "-")
            .replace(/-+/g, "-");
    }

    function codePresentation(raw, labels, fallback) {
        var normalized = normalizeDecisionCode(raw);
        if (!normalized) return { label: "", diagnostic: "" };
        return {
            label: labels[normalized] || fallback,
            diagnostic: labels[normalized] ? "" : String(raw).trim()
        };
    }

    function matchOriginPresentation(target) {
        return codePresentation(matchOrigin(target), {
            "provider-id": "本地外部标识符",
            "providerid": "本地外部标识符",
            "external-id": "本地外部标识符",
            "externalid": "本地外部标识符",
            "local-external-id": "本地外部标识符",
            "binding": "已保存绑定",
            "saved-binding": "已保存绑定",
            "scored": "智能评分匹配",
            "score": "智能评分匹配",
            "manual": "手动选择",
            "manual-selection": "手动选择"
        }, "未知匹配来源");
    }

    function decisionReasonPresentation(target) {
        return codePresentation(decisionReason(target), {
            "provider-id": "使用本地外部标识符",
            "providerid": "使用本地外部标识符",
            "external-id": "使用本地外部标识符",
            "externalid": "使用本地外部标识符",
            "binding": "使用已保存绑定",
            "saved-binding": "使用已保存绑定",
            "confident-site-priority": "按站点优先级自动选择",
            "site-priority": "按站点优先级自动选择",
            "provider-id-unresolved": "本地标识符无法解析",
            "unresolved-provider": "本地标识符无法解析",
            "no-candidates": "未找到候选",
            "no-candidate": "未找到候选",
            "low-confidence": "置信度不足，需手动选择",
            "manual": "手动选择",
            "manual-selection": "手动选择"
        }, "未知决策");
    }

    function isProviderIdMatch(target) {
        var normalized = normalizeDecisionCode(matchOrigin(target));
        return ["provider-id", "providerid", "external-id", "externalid", "local-external-id"].indexOf(normalized) >= 0;
    }

    function matchOriginLabel(target) {
        return matchOriginPresentation(target).label;
    }

    function decisionReasonLabel(target) {
        return decisionReasonPresentation(target).label;
    }

    function decisionFragment(prefix, presentation) {
        if (!presentation.label) return "";
        return prefix + "：" + presentation.label +
            (presentation.diagnostic ? "（诊断代码：" + presentation.diagnostic + "）" : "");
    }

    function backendDecisionLine(target) {
        var parts = [];
        var origin = matchOriginPresentation(target);
        var reason = decisionReasonPresentation(target);
        var originFragment = decisionFragment("来源", origin);
        var reasonFragment = decisionFragment("决策", reason);
        if (originFragment) parts.push(originFragment);
        if (reasonFragment) parts.push(reasonFragment);
        return parts.join("　");
    }

    function hasBackendMatch(target) {
        var origin = matchOrigin(target).toLowerCase();
        var status = String(value(target, "Status", "status", "") || "").trim().toLowerCase();
        if (["ambiguous", "no_match", "not_found", "notfound", "failed", "unsupported", "unmatched"].indexOf(status) >= 0) {
            return false;
        }
        return Boolean(origin) && ["none", "not_found", "notfound", "failed", "unmatched"].indexOf(origin) < 0;
    }

    function candidateLine(candidate) {
        var site = value(candidate, "SiteName", "siteName", value(candidate, "Site", "site", "未知网站"));
        var name = value(candidate, "Name", "name", "未命名项目");
        var year = value(candidate, "Year", "year", null);
        var episodes = value(candidate, "EpisodeSize", "episodeSize", 0);
        return site + "｜" + name + "｜" + (year || "年份未知") + "｜" + (episodes > 0 ? episodes + " 集" : "集数未知");
    }

    // The server owns validation and produces the canonical plan.  These
    // helpers only turn that plan into cards and send compact *intent* for
    // later manual groups; they never manufacture CommentId or episode IDs.
    function compositePlan(season) {
        return value(season, "CompositePlan", "compositePlan", null);
    }

    function compositeArray(object, pascal, camel) {
        return value(object, pascal, camel, []) || [];
    }

    function hasCompositePlan(season) {
        var plan = compositePlan(season);
        return Boolean(plan && (compositeArray(plan, "Mappings", "mappings").length ||
            compositeArray(plan, "UnmatchedRuns", "unmatchedRuns").length)) ||
            Boolean(value(season, "RequiresCompositeMapping", "requiresCompositeMapping", false)) ||
            compositeArray(season, "CompositeGroups", "compositeGroups").length > 0;
    }

    function compositeSelectionStore(selections, season, create) {
        var key = seasonSelectionKey(season);
        var store = selections.__compositeSelections;
        if (!store && create) store = selections.__compositeSelections = {};
        if (!store) return [];
        if (!store[key] && create) store[key] = [];
        return store[key] || [];
    }

    function compositeDraftSeasonState(dialog, season, create) {
        var draft = dialog && dialog.compositeDraft;
        if (!draft && create && dialog) draft = dialog.compositeDraft = { exclusions: {}, removedRuns: {} };
        if (!draft) return { exclusions: [], removedRuns: [] };
        var key = seasonSelectionKey(season);
        if (!draft.exclusions[key] && create) draft.exclusions[key] = [];
        if (!draft.removedRuns[key] && create) draft.removedRuns[key] = [];
        return {
            exclusions: draft.exclusions[key] || [],
            removedRuns: draft.removedRuns[key] || []
        };
    }

    function compositeExcludedItemIds(dialog, season) {
        return compositeDraftSeasonState(dialog, season, false).exclusions.slice();
    }

    function cloneCompositeSelections(selections) {
        return (selections || []).map(function (selection) {
            return JSON.parse(JSON.stringify(selection));
        });
    }

    function selectionLocalEpisodeItemIds(season, selection) {
        var ordered = compositeOrderedEpisodesForSeason(season);
        var startId = String(value(selection, "LocalStartEpisodeItemId", "localStartEpisodeItemId", "") || "");
        var start = ordered.findIndex(function (episode) {
            return String(value(episode, "ItemId", "itemId", "") || "") === startId;
        });
        var requested = Math.max(1, Number(value(selection, "RequestedEpisodeCount", "requestedEpisodeCount", 0)) || 1);
        return start < 0 ? [] : ordered.slice(start, start + requested).map(function (episode) {
            return String(value(episode, "ItemId", "itemId", "") || "");
        }).filter(Boolean);
    }

    function filterCompositeSelectionsByItemIds(season, selections, localItemIds) {
        var targets = {};
        (localItemIds || []).forEach(function (id) { targets[String(id)] = true; });
        var result = { kept: [], removed: [] };
        (selections || []).forEach(function (selection) {
            var overlaps = selectionLocalEpisodeItemIds(season, selection).some(function (id) { return targets[id]; });
            result[overlaps ? "removed" : "kept"].push(selection);
        });
        return result;
    }

    function excludeCompositeRun(dialog, season, group, removedSelections) {
        var state = compositeDraftSeasonState(dialog, season, true);
        var ids = (group.episodes || []).map(function (episode) {
            return String(value(episode, "ItemId", "itemId", "") || "");
        }).filter(Boolean);
        ids.forEach(function (id) {
            if (state.exclusions.indexOf(id) < 0) state.exclusions.push(id);
        });
        var start = ids[0] || "";
        if (start && !state.removedRuns.some(function (run) { return run.start === start; })) {
            state.removedRuns.push({
                start: start,
                itemIds: ids.slice(),
                label: episodeRangeLabel(group.episodes),
                selections: cloneCompositeSelections(removedSelections)
            });
        }
        return ids;
    }

    function restoreCompositeRun(dialog, season, itemIds) {
        var state = compositeDraftSeasonState(dialog, season, true);
        var restored = {};
        (itemIds || []).forEach(function (id) { restored[String(id)] = true; });
        var remaining = state.exclusions.filter(function (id) { return !restored[id]; });
        state.exclusions.splice.apply(state.exclusions, [0, state.exclusions.length].concat(remaining));
        var remainingRuns = state.removedRuns.filter(function (run) {
            return !run.itemIds.some(function (id) { return restored[id]; });
        });
        state.removedRuns.splice.apply(state.removedRuns, [0, state.removedRuns.length].concat(remainingRuns));
        return state.exclusions.slice();
    }

    function confirmedCompositeItemIds(season) {
        var ids = {};
        compositeArray(compositePlan(season), "Mappings", "mappings").forEach(function (mapping) {
            ids[String(value(mapping, "LocalEpisodeItemId", "localEpisodeItemId", "") || "")] = true;
        });
        compositeArray(season, "CompositeGroups", "compositeGroups").forEach(function (group) {
            if (value(group, "IsTemporary", "isTemporary", false)) return;
            compositeArray(group, "Episodes", "episodes").forEach(function (episode) {
                ids[String(value(episode, "ItemId", "itemId", "") || "")] = true;
            });
        });
        return ids;
    }

    function compositePlanCoversItemIds(season, itemIds) {
        var confirmed = confirmedCompositeItemIds(season);
        return (itemIds || []).every(function (id) { return confirmed[String(id)]; });
    }

    function compositeDraftParameters(dialog, season, parameters) {
        var request = Object.assign({}, parameters || {});
        request.excludedLocalEpisodeItemIds = JSON.stringify(compositeExcludedItemIds(dialog, season));
        return request;
    }

    function adoptAuthoritativeCompositeExclusions(dialog, season) {
        var plan = compositePlan(season);
        var effective = compositeArray(plan, "EffectiveExcludedLocalEpisodeItemIds",
            "effectiveExcludedLocalEpisodeItemIds").map(String);
        var state = compositeDraftSeasonState(dialog, season, true);
        state.exclusions.splice.apply(state.exclusions, [0, state.exclusions.length].concat(effective));
        return effective;
    }

    function temporaryRangeKeyword(item, season) {
        return String(value(season, "SeriesName", "seriesName",
            item && item.Type === "Series" ? item.Name : "") ||
            value(season, "SeasonName", "seasonName", item && item.Name || "") || "").trim();
    }

    function temporaryRangeSearchParameters(dialog, item, season, group, selections, keyword) {
        var parameters = compositeDraftParameters(dialog, season, seasonRequestParameters(season));
        parameters.searchScope = "temporary-range";
        parameters.compositeStartEpisodeItemId = value(group.episodes[0], "ItemId", "itemId", "");
        parameters.compositeEpisodeCount = String(group.episodes.length);
        parameters.compositeSelections = JSON.stringify(compositeRequestSelections(selections, season));
        parameters.compositePlan = "true";
        parameters.keyword = String(keyword || "").trim();
        return rematchParameters(parameters);
    }

    function removeCompositeSelection(selections, season, localStartEpisodeItemId) {
        var key = seasonSelectionKey(season);
        var store = selections && selections.__compositeSelections;
        var entries = store && store[key];
        if (!entries || !entries.length) return false;
        var remaining = entries.filter(function (selection) {
            return value(selection, "LocalStartEpisodeItemId", "localStartEpisodeItemId", "") !== localStartEpisodeItemId;
        });
        if (remaining.length === entries.length) return false;
        if (remaining.length) store[key] = remaining;
        else delete store[key];
        return true;
    }

    function localEpisodeLabel(episode) {
        var number = value(episode, "EpisodeNumber", "episodeNumber", null);
        return number && Number(number) > 0 ? "第 " + number + " 集" : "未编号剧集";
    }

    function episodeRangeLabel(episodes) {
        episodes = episodes || [];
        if (!episodes.length) return "0 集";
        var first = localEpisodeLabel(episodes[0]);
        var last = localEpisodeLabel(episodes[episodes.length - 1]);
        return episodes.length === 1 ? first : first + "–" + last + "（" + episodes.length + " 集）";
    }

    function sourceLabel(source, sourceStart) {
        source = source || {};
        var provider = value(source, "ProviderId", "providerId", "未知来源");
        var media = value(source, "MediaId", "mediaId", "");
        var start = sourceStart === undefined || sourceStart === null || sourceStart === "" ? "" :
            "，来源从第 " + sourceStart + " 集开始";
        return provider + (media ? " · " + media : "") + start;
    }

    function sourceKey(source) {
        return String(value(source, "ProviderId", "providerId", "")).toLowerCase() + "\u001f" +
            String(value(source, "MediaId", "mediaId", "")).toLowerCase();
    }

    function orderedCompositeEpisodes(plan) {
        return compositeArray(plan, "OrderedEpisodes", "orderedEpisodes");
    }

    function compositeOrderedEpisodesForSeason(season) {
        var ordered = orderedCompositeEpisodes(compositePlan(season));
        if (ordered.length) return ordered;
        var flattened = [];
        compositeArray(season, "CompositeGroups", "compositeGroups").forEach(function (group) {
            compositeArray(group, "Episodes", "episodes").forEach(function (episode) { flattened.push(episode); });
        });
        return flattened;
    }

    function compositeEpisodeIndex(ordered, episode) {
        var id = value(episode, "ItemId", "itemId", "");
        return ordered.findIndex(function (candidate) {
            return value(candidate, "ItemId", "itemId", "") === id;
        });
    }

    function manualCompositeGroups(selections, season) {
        var ordered = compositeOrderedEpisodesForSeason(season);
        var plan = compositePlan(season);
        var permitted = {};
        if (plan) {
            compositeArray(plan, "UnmatchedRuns", "unmatchedRuns").forEach(function (run) {
                compositeArray(run, "Episodes", "episodes").forEach(function (episode) {
                    permitted[value(episode, "ItemId", "itemId", "")] = true;
                });
            });
        } else {
            compositeArray(season, "CompositeGroups", "compositeGroups").forEach(function (group) {
                if (value(group, "IsTemporary", "isTemporary", false)) {
                    compositeArray(group, "Episodes", "episodes").forEach(function (episode) {
                        permitted[value(episode, "ItemId", "itemId", "")] = true;
                    });
                }
            });
        }
        var hasPermittedEpisodes = Object.keys(permitted).length > 0;
        return compositeSelectionStore(selections, season, false).map(function (selection) {
            var startId = value(selection, "LocalStartEpisodeItemId", "localStartEpisodeItemId", "");
            var start = ordered.findIndex(function (episode) {
                return value(episode, "ItemId", "itemId", "") === startId;
            });
            var requested = Number(value(selection, "RequestedEpisodeCount", "requestedEpisodeCount", 0));
            var episodes = [];
            for (var offset = 0; start >= 0 && offset < Math.max(1, requested || 1) && start + offset < ordered.length; offset++) {
                var episode = ordered[start + offset];
                if (hasPermittedEpisodes && !permitted[value(episode, "ItemId", "itemId", "")]) break;
                episodes.push(episode);
            }
            return { kind: "manual", episodes: episodes, selection: selection, index: start < 0 ? Number.MAX_SAFE_INTEGER : start };
        }).filter(function (group) { return group.episodes.length > 0; });
    }

    function compositeVirtualGroups(season, selections) {
        var plan = compositePlan(season);
        if (!plan) {
            var legacyGroups = compositeArray(season, "CompositeGroups", "compositeGroups");
            if (!legacyGroups.length) return [];
            var legacyManual = manualCompositeGroups(selections, season);
            var claimed = {};
            legacyManual.forEach(function (group) {
                group.episodes.forEach(function (episode) { claimed[value(episode, "ItemId", "itemId", "")] = true; });
            });
            var result = [];
            var sequence = 0;
            legacyGroups.forEach(function (group) {
                var episodes = compositeArray(group, "Episodes", "episodes");
                var temporary = Boolean(value(group, "IsTemporary", "isTemporary", false));
                if (!temporary) {
                    result.push({ kind: "mapped", source: {
                        ProviderId: value(group, "Site", "site", ""),
                        MediaId: value(group, "CandidateId", "candidateId", "")
                    }, sourceStart: value(group, "SourceStartEpisodeId", "sourceStartEpisodeId", ""),
                        origin: value(group, "MatchOrigin", "matchOrigin", ""),
                        mappings: [], episodes: episodes, index: sequence });
                    sequence += episodes.length;
                    return;
                }
                var pending = [];
                episodes.forEach(function (episode) {
                    if (claimed[value(episode, "ItemId", "itemId", "")]) {
                        if (pending.length) {
                            result.push({ kind: "unmatched", episodes: pending, index: sequence });
                            pending = [];
                        }
                    } else pending.push(episode);
                });
                if (pending.length) result.push({ kind: "unmatched", episodes: pending, index: sequence });
                sequence += episodes.length;
            });
            legacyManual.forEach(function (group) { result.push(group); });
            return result.sort(function (left, right) { return left.index - right.index; });
        }
        var ordered = orderedCompositeEpisodes(plan);
        var mapped = compositeArray(plan, "Mappings", "mappings");
        var byLocalId = {};
        mapped.forEach(function (mapping) {
            byLocalId[value(mapping, "LocalEpisodeItemId", "localEpisodeItemId", "")] = mapping;
        });
        var groups = [];
        var current = null;
        ordered.forEach(function (episode, index) {
            var mapping = byLocalId[value(episode, "ItemId", "itemId", "")];
            if (!mapping) {
                current = null;
                return;
            }
            var mappingOrigin = value(mapping, "Origin", "origin", "");
            var key = sourceKey(value(mapping, "Source", "source", {})) + "\u001f" + normalizeDecisionCode(mappingOrigin);
            if (!current || current.key !== key) {
                current = { kind: "mapped", key: key, source: value(mapping, "Source", "source", {}),
                    origin: mappingOrigin, mappings: [], episodes: [], index: index };
                groups.push(current);
            }
            current.episodes.push(episode);
            current.mappings.push(mapping);
        });

        var manual = manualCompositeGroups(selections, season);
        manual.forEach(function (group) { groups.push(group); });
        var claimed = {};
        manual.forEach(function (group) {
            group.episodes.forEach(function (episode) { claimed[value(episode, "ItemId", "itemId", "")] = true; });
        });
        compositeArray(plan, "UnmatchedRuns", "unmatchedRuns").forEach(function (run) {
            var pending = [];
            compositeArray(run, "Episodes", "episodes").forEach(function (episode) {
                if (claimed[value(episode, "ItemId", "itemId", "")]) {
                    if (pending.length) {
                        groups.push({ kind: "unmatched", episodes: pending, index: compositeEpisodeIndex(ordered, pending[0]) });
                        pending = [];
                    }
                    return;
                }
                pending.push(episode);
            });
            if (pending.length) groups.push({ kind: "unmatched", episodes: pending, index: compositeEpisodeIndex(ordered, pending[0]) });
        });
        return groups.sort(function (left, right) { return left.index - right.index; });
    }

    function compositeHasDownloadableMappings(season, selections) {
        var plan = compositePlan(season);
        var exactGroups = compositeArray(season, "CompositeGroups", "compositeGroups").some(function (group) {
            return !value(group, "IsTemporary", "isTemporary", false) &&
                compositeArray(group, "Episodes", "episodes").length > 0;
        });
        return compositeArray(plan, "Mappings", "mappings").length > 0 || exactGroups ||
            manualCompositeGroups(selections, season).length > 0;
    }

    function isDirectCompositeOrigin(origin) {
        var normalized = normalizeDecisionCode(origin);
        return normalized === "direct" || normalized === "episode-provider-id" ||
            normalized === "direct-episode-provider-id";
    }

    function compactCompositeSelection(localStart, requestedCount, site, candidateId,
        sourceStartEpisodeId, sourceStartEpisodeNumber, matchOrigin) {
        return {
            LocalStartEpisodeItemId: localStart || "",
            RequestedEpisodeCount: Number(requestedCount) || 0,
            Site: site || "",
            CandidateId: candidateId || "",
            SourceStartEpisodeId: sourceStartEpisodeId || "",
            SourceStartEpisodeNumber: sourceStartEpisodeNumber === null || sourceStartEpisodeNumber === undefined
                ? 0 : Number(sourceStartEpisodeNumber) || 0,
            MatchOrigin: matchOrigin || "manual"
        };
    }

    function serverCompositeRequestSelections(season) {
        var previewGroups = compositeArray(season, "CompositeGroups", "compositeGroups");
        if (previewGroups.length) {
            return previewGroups.filter(function (group) {
                return !value(group, "IsTemporary", "isTemporary", false) &&
                    !isDirectCompositeOrigin(value(group, "MatchOrigin", "matchOrigin", "")) &&
                    compositeArray(group, "Episodes", "episodes").length > 0 &&
                    value(group, "Site", "site", "") && value(group, "CandidateId", "candidateId", "");
            }).map(function (group) {
                var episodes = compositeArray(group, "Episodes", "episodes");
                return compactCompositeSelection(
                    value(episodes[0], "ItemId", "itemId", ""),
                    episodes.length,
                    value(group, "Site", "site", ""),
                    value(group, "CandidateId", "candidateId", ""),
                    value(group, "SourceStartEpisodeId", "sourceStartEpisodeId", ""),
                    value(episodes[0], "SourceEpisodeNumber", "sourceEpisodeNumber", null),
                    value(group, "MatchOrigin", "matchOrigin", "")
                );
            });
        }

        var plan = compositePlan(season);
        var ordered = orderedCompositeEpisodes(plan);
        var byLocalId = {};
        compositeArray(plan, "Mappings", "mappings").forEach(function (mapping) {
            byLocalId[value(mapping, "LocalEpisodeItemId", "localEpisodeItemId", "")] = mapping;
        });
        var groups = [];
        var current = null;
        ordered.forEach(function (episode, index) {
            var mapping = byLocalId[value(episode, "ItemId", "itemId", "")];
            var origin = value(mapping, "Origin", "origin", "");
            var source = value(mapping, "Source", "source", null);
            if (!mapping || !source || isDirectCompositeOrigin(origin)) {
                current = null;
                return;
            }
            var key = sourceKey(source) + "\u001f" + normalizeDecisionCode(origin);
            if (!current || current.key !== key || current.lastIndex + 1 !== index) {
                current = { key: key, lastIndex: index, mappings: [] };
                groups.push(current);
            }
            current.lastIndex = index;
            current.mappings.push(mapping);
        });
        return groups.filter(function (group) {
            var first = group.mappings[0] || {};
            var source = value(first, "Source", "source", {});
            return value(first, "LocalEpisodeItemId", "localEpisodeItemId", "") &&
                value(source, "ProviderId", "providerId", "") && value(source, "MediaId", "mediaId", "");
        }).map(function (group) {
            var first = group.mappings[0];
            var source = value(first, "Source", "source", {});
            return compactCompositeSelection(
                value(first, "LocalEpisodeItemId", "localEpisodeItemId", ""),
                group.mappings.length,
                value(source, "ProviderId", "providerId", ""),
                value(source, "MediaId", "mediaId", ""),
                value(first, "SourceEpisodeId", "sourceEpisodeId", ""),
                value(first, "SourceEpisodeNumber", "sourceEpisodeNumber", null),
                value(first, "Origin", "origin", "")
            );
        });
    }

    function compositeRequestSelections(selections, season) {
        // Only submit choices still attached to an unmatched run in the current
        // server plan. A refreshed preview can turn an old local range into an
        // exact mapping; keeping that stale browser choice would make the
        // server rightly reject an overlap.
        var verified = serverCompositeRequestSelections(season);
        var manual = manualCompositeGroups(selections, season).map(function (group) {
            var selection = group.selection;
            return compactCompositeSelection(
                value(selection, "LocalStartEpisodeItemId", "localStartEpisodeItemId", ""),
                value(selection, "RequestedEpisodeCount", "requestedEpisodeCount", 0),
                value(value(selection, "Source", "source", {}), "ProviderId", "providerId", ""),
                value(value(selection, "Source", "source", {}), "MediaId", "mediaId", ""),
                value(selection, "SourceStartEpisodeId", "sourceStartEpisodeId", ""),
                value(selection, "SourceStartEpisodeNumber", "sourceStartEpisodeNumber", null),
                "manual"
            );
        });
        return verified.concat(manual);
    }

    async function requestAuthoritativeCompositePlan(dialog, item, season, compactSelections) {
        var parameters = compositeDraftParameters(dialog, season, seasonRequestParameters(season));
        parameters.compositePlan = "true";
        parameters.compositeSelections = JSON.stringify(compactSelections || []);
        var preview = await runDialogSearch(
            dialog, parameters.seriesId || item.Id, "detail-resolution", parameters,
            "正在由服务器验证逐集精确映射…");
        if (!preview) throw new Error("逐集映射验证已取消或失败");
        var confirmed = (value(preview, "Seasons", "seasons", []) || [])[0];
        if (!confirmed || !compositePlan(confirmed)) {
            throw new Error("服务器没有返回权威复合季映射");
        }
        return confirmed;
    }

    function clearCompositeSelectionStore(selections, season) {
        var store = selections && selections.__compositeSelections;
        if (store) delete store[seasonSelectionKey(season)];
    }

    function compositeCoverage(season, selections) {
        var groups = compositeVirtualGroups(season, selections);
        var processed = groups.filter(function (group) { return group.kind !== "unmatched"; })
            .reduce(function (sum, group) { return sum + group.episodes.length; }, 0);
        var total = compositeOrderedEpisodesForSeason(season).length ||
            Number(value(season, "EpisodeCount", "episodeCount", 0)) || processed;
        return { processed: processed, skipped: Math.max(0, total - processed), total: total };
    }

    async function bindSeason(season, manual) {
        var seasonId = value(season, "SeasonId", "seasonId", "");
        var site = value(season, "SelectedSite", "selectedSite", "");
        var candidateId = value(season, "SelectedId", "selectedId", "");
        var result = await api(seasonId, "BindMatch", {
            site: site,
            candidateId: candidateId,
            manual: manual ? "true" : "false",
            seriesId: value(season, "SeriesId", "seriesId", ""),
            seasonName: value(season, "SeasonName", "seasonName", ""),
            seasonNumber: value(season, "SeasonNumber", "seasonNumber", ""),
            seasonYear: value(season, "Year", "year", "")
        });
        if (!value(result, "Success", "success", false)) {
            throw new Error(value(result, "Message", "message", "绑定失败"));
        }
        return result;
    }

    function seasonWasManuallyBound(season) {
        return value(season, "Status", "status", "") === "bound";
    }

    function selectedCandidate(season) {
        var selectedId = value(season, "SelectedId", "selectedId", "");
        var selectedSite = value(season, "SelectedSite", "selectedSite", "");
        if (!selectedId || !selectedSite) return null;
        return seasonCandidates(season).find(function (candidate) {
            return value(candidate, "Id", "id", "") === selectedId &&
                value(candidate, "Site", "site", "") === selectedSite;
        }) || {
            Id: selectedId,
            Site: selectedSite,
            SiteName: value(season, "SelectedSiteName", "selectedSiteName", selectedSite),
            Name: "当前已绑定的项目"
        };
    }

    function selectionWasChanged(season, candidate) {
        return value(candidate, "Id", "id", "") !== value(season, "SelectedId", "selectedId", "") ||
            value(candidate, "Site", "site", "") !== value(season, "SelectedSite", "selectedSite", "");
    }

    function wait(milliseconds) {
        return new Promise(function (resolve) { window.setTimeout(resolve, milliseconds); });
    }

    async function submitSeriesSelections(dialog, seasons, selections) {
        renderDownloadProgress(dialog, seasons, selections);
    }

    async function renderDownloadProgress(dialog, seasons, selections) {
        dialog.setBackHandler(null);
        dialog.androidBackLocked = false;
        dialog.closable = false;
        dialog.body.replaceChildren();
        dialog.footer.replaceChildren();

        var summary = document.createElement("div");
        summary.className = "danmuProgressSummary";
        summary.textContent = "准备开始下载，共 " + seasons.length + " 季";
        dialog.body.appendChild(summary);

        var views = {};
        seasons.forEach(function (season) {
            var key = seasonSelectionKey(season);
            var block = document.createElement("div");
            block.className = "danmuProgressSeason";
            var title = document.createElement("div");
            title.className = "danmuProgressTitle";
            var name = document.createElement("span");
            name.textContent = value(season, "SeasonName", "seasonName", "未命名季度");
            var state = document.createElement("span");
            state.textContent = "等待中";
            title.append(name, state);
            var meta = document.createElement("div");
            meta.className = "danmuProgressMeta";
            var candidate = selections[key];
            meta.textContent = hasCompositePlan(season)
                ? "复合季映射：已确认 " + compositeVirtualGroups(season, selections).filter(function (group) {
                    return group.kind !== "unmatched";
                }).length + " 个临时季"
                : (candidate ? candidateLine(candidate) : "尚未选择");
            var episodes = document.createElement("div");
            block.append(title, meta, episodes);
            dialog.body.appendChild(block);
            views[key] = { block: block, state: state, meta: meta, episodes: episodes };
        });

        var background = document.createElement("button");
        background.className = "danmuSmartButton";
        background.textContent = "后台下载";
        background.disabled = true;
        var stop = document.createElement("button");
        stop.className = "danmuSmartButton danger";
        stop.textContent = "强制停止全部下载";
        stop.disabled = true;
        dialog.footer.append(background, stop);

        var detached = false;
        var stopRequested = false;
        var launchFailures = 0;
        var taskEntries = [];
        var monitoring = false;

        function isTerminal(status) {
            return status === "completed" || status === "completed_with_warnings" ||
                status === "completed_with_errors" ||
                status === "failed" || status === "cancelled" || status === "not_found";
        }

        function updateView(season, task, entry) {
            var view = views[seasonSelectionKey(season)];
            var status = value(task, "Status", "status", "running");
            var completed = value(task, "Completed", "completed", 0);
            var total = value(task, "Total", "total", 0);
            var succeeded = value(task, "Succeeded", "succeeded", 0);
            var skipped = value(task, "Skipped", "skipped", 0);
            var partial = value(task, "Partial", "partial", 0);
            var failed = value(task, "Failed", "failed", 0);
            var blockStatus = status === "completed" ? "success" :
                (status === "completed_with_warnings" ? "warning" :
                (status === "cancelled" ? "cancelled" :
                    ((status === "queued" || status === "running" || status === "stopping") ? "running" : "failed")));
            view.block.className = "danmuProgressSeason " + blockStatus;
            view.state.textContent = status === "queued" ? "后台队列中" :
                (status === "running" ? "进行中 " + completed + "/" + total :
                    (status === "stopping" ? "正在停止" :
                        (status === "completed" ? "完成 ✓" :
                            (status === "completed_with_warnings" ? "完成（部分缺失）" :
                                (status === "cancelled" ? "已停止" : "完成（有失败）")))));
            view.meta.textContent = value(task, "SiteName", "siteName", "") + "　" +
                value(task, "Message", "message", "") + "　成功 " + succeeded +
                " / 部分缺失 " + partial + " / 重复已跳过 " + skipped + " / 失败 " + failed;
            view.episodes.replaceChildren();
            (value(task, "Episodes", "episodes", []) || []).forEach(function (episode) {
                var row = document.createElement("div");
                var episodeStatus = value(episode, "Status", "status", "pending");
                row.className = "danmuEpisodeProgress " + episodeStatus;
                var number = document.createElement("span");
                number.textContent = "第 " + (value(episode, "EpisodeNumber", "episodeNumber", "?") || "?") + " 集";
                var episodeName = document.createElement("span");
                episodeName.textContent = value(episode, "EpisodeName", "episodeName", "未命名剧集");
                var result = document.createElement("span");
                result.textContent = episodeStatus === "success" ? "✓ 下载成功" :
                    (episodeStatus === "running" ? "● 正在下载" :
                        (episodeStatus === "queued" ? "● 等待重试" :
                            (episodeStatus === "partial" ? "⚠ " + value(episode, "Message", "message", "部分弹幕缺失") :
                                (episodeStatus === "skipped" ? "↷ 重复已跳过" :
                                    (episodeStatus === "cancelled" ? "■ 已强制停止" :
                                        (episodeStatus === "failed" ? "✕ " + value(episode, "Message", "message", "下载失败") : "等待中"))))));
                var retry = document.createElement("button");
                retry.className = "danmuEpisodeRetry";
                retry.textContent = "重试";
                retry.title = "强制重新下载该集并覆盖现有弹幕 XML";
                retry.disabled = !entry || !isTerminal(status) || !value(episode, "ItemId", "itemId", "");
                retry.addEventListener("click", async function () {
                    if (!entry || retry.disabled) return;
                    retry.disabled = true;
                    retry.textContent = "提交中…";
                    try {
                        entry.task = await api(
                            value(episode, "ItemId", "itemId", ""),
                            "RetryTrackedEpisode",
                            { taskId: entry.taskId });
                        updateView(entry.season, entry.task, entry);
                        if (!isTerminal(value(entry.task, "Status", "status", ""))) {
                            await monitorTasks();
                        } else {
                            notify(value(entry.task, "Message", "message", "未能启动单集重试"), true);
                        }
                    } catch (error) {
                        retry.disabled = false;
                        retry.textContent = "重试";
                        notify("单集重试提交失败：" + (error.message || error), true);
                    }
                });
                row.append(number, episodeName, result, retry);
                view.episodes.appendChild(row);
            });
        }

        background.addEventListener("click", function () {
            detached = true;
            dialog.forceClose();
            notify("下载任务已转入服务器后台队列，关闭页面不会取消任务。", false);
        });

        stop.addEventListener("click", async function () {
            if (stopRequested) return;
            stopRequested = true;
            stop.disabled = true;
            stop.textContent = "正在停止…";
            try {
                var result = await api("all", "StopAllTrackedDownloads");
                notify(value(result, "Message", "message", "已提交停止请求"), false);
                // Some legacy provider calls cannot be physically cancelled. Let the
                // user close immediately after the server accepted the stop request.
                dialog.closable = true;
                background.style.display = "none";
                stop.style.display = "none";
            } catch (error) {
                stopRequested = false;
                stop.disabled = false;
                stop.textContent = "强制停止全部下载";
                notify("停止下载失败：" + (error.message || error), true);
            }
        });

        for (var index = 0; index < seasons.length; index++) {
            var season = seasons[index];
            var key = seasonSelectionKey(season);
            var candidate = selections[key];
            var view = views[key];
            view.block.className = "danmuProgressSeason running";
            view.state.textContent = "正在准备";
            summary.textContent = "正在处理第 " + (index + 1) + " / " + seasons.length + " 季：" +
                value(season, "SeasonName", "seasonName", "未命名季度");
            try {
                var taskParameters = seasonRequestParameters(season);
                if (hasCompositePlan(season)) {
                    // JSON contains only user intent (source + start + range).
                    // The controller re-resolves and verifies every source episode.
                    taskParameters.compositeSelections = JSON.stringify(compositeRequestSelections(selections, season));
                    taskParameters.compositePlan = "true";
                    taskParameters.excludedLocalEpisodeItemIds = JSON.stringify(compositeExcludedItemIds(dialog, season));
                } else {
                    taskParameters.site = value(candidate, "Site", "site", "");
                    taskParameters.candidateId = value(candidate, "Id", "id", "");
                    taskParameters.manual = (seasonWasManuallyBound(season) || selectionWasChanged(season, candidate)) ? "true" : "false";
                }
                taskParameters.forceRefresh = dialog.forceRefresh ? "true" : "false";
                var task = await api(value(season, "SeasonId", "seasonId", ""), "StartTrackedDownload", taskParameters);
                var taskId = value(task, "TaskId", "taskId", "");
                if (!taskId || value(task, "Status", "status", "") === "failed") {
                    throw new Error(value(task, "Message", "message", "无法启动本季下载任务"));
                }
                var entry = { season: season, taskId: taskId, task: task, pollErrors: 0 };
                taskEntries.push(entry);
                updateView(season, task, entry);
            } catch (error) {
                launchFailures++;
                view.block.className = "danmuProgressSeason failed";
                view.state.textContent = "启动失败 ✕";
                view.meta.textContent = error.message || String(error);
            }
        }

        background.disabled = taskEntries.length === 0;
        stop.disabled = taskEntries.length === 0;
        summary.textContent = taskEntries.length
            ? "全部季度已提交服务器后台队列，正在更新执行状态…"
            : "没有成功启动的下载任务。";

        async function monitorTasks() {
            if (monitoring || detached) return;
            monitoring = true;
            stopRequested = false;
            background.style.display = "";
            background.disabled = false;
            stop.style.display = "";
            stop.disabled = false;
            stop.textContent = "强制停止全部下载";
            dialog.closable = false;

            while (!detached && taskEntries.some(function (entry) {
                return !isTerminal(value(entry.task, "Status", "status", ""));
            })) {
                await wait(750);
                if (detached) {
                    monitoring = false;
                    return;
                }
                var activeEntries = taskEntries.filter(function (entry) {
                    return !isTerminal(value(entry.task, "Status", "status", ""));
                });
                await Promise.all(activeEntries.map(async function (entry) {
                    try {
                        entry.task = await api(
                            value(entry.season, "SeasonId", "seasonId", ""),
                            "GetDownloadProgress",
                            { taskId: entry.taskId });
                        entry.pollErrors = 0;
                        updateView(entry.season, entry.task, entry);
                    } catch (error) {
                        entry.pollErrors++;
                        if (entry.pollErrors >= 3) {
                            entry.task = {
                                Status: "not_found",
                                Message: "连续三次读取任务状态失败：" + (error.message || error),
                                Failed: 0,
                                Partial: 0,
                                Skipped: 0,
                                Succeeded: 0,
                                Episodes: []
                            };
                            updateView(entry.season, entry.task, entry);
                        }
                    }
                }));
            }

            monitoring = false;
            if (detached) return;
            renderSettlement();
        }

        function renderSettlement() {
            var totalSucceeded = taskEntries.reduce(function (sum, entry) {
                return sum + Number(value(entry.task, "Succeeded", "succeeded", 0));
            }, 0);
            var totalPartial = taskEntries.reduce(function (sum, entry) {
                return sum + Number(value(entry.task, "Partial", "partial", 0));
            }, 0);
            var totalSkipped = taskEntries.reduce(function (sum, entry) {
                return sum + Number(value(entry.task, "Skipped", "skipped", 0));
            }, 0);
            var totalFailed = taskEntries.reduce(function (sum, entry) {
                return sum + Number(value(entry.task, "Failed", "failed", 0));
            }, 0);
            var cancelledSeasons = taskEntries.filter(function (entry) {
                return value(entry.task, "Status", "status", "") === "cancelled";
            }).length;
            var failedSeasons = launchFailures + taskEntries.filter(function (entry) {
                var status = value(entry.task, "Status", "status", "");
                return status !== "completed" && status !== "completed_with_warnings" &&
                    status !== "cancelled";
            }).length;

            summary.textContent = (cancelledSeasons > 0 ? "下载已停止：" : "全部处理完成：") +
                "成功 " + totalSucceeded + " 集，部分弹幕缺失 " + totalPartial +
                " 集，重复已跳过 " + totalSkipped + " 集，失败 " + totalFailed +
                " 集，异常季度 " + failedSeasons + " 个" +
                (cancelledSeasons > 0 ? "，已停止季度 " + cancelledSeasons + " 个。" :
                    "。每集右侧均可单独重试，请检查明细后手动关闭。");
            taskEntries.forEach(function (entry) {
                updateView(entry.season, entry.task, entry);
            });
            background.style.display = "none";
            stop.style.display = "none";
            dialog.closable = true;
        }

        await monitorTasks();
    }

    function appendCompositeMappingDetails(card, group) {
        if ((group.kind !== "mapped" && group.kind !== "manual") || !group.episodes.length) return;
        var details = document.createElement("details");
        var summary = document.createElement("summary");
        summary.textContent = "查看逐集映射（" + group.episodes.length + " 集）";
        details.appendChild(summary);
        group.episodes.forEach(function (episode, index) {
            var mapping = group.kind === "mapped" ? (group.mappings[index] || episode) : {};
            var source = group.kind === "mapped" ? group.source : value(group.selection, "Source", "source", {});
            var line = document.createElement("div");
            line.className = "danmuVirtualSeasonDetail";
            var manualStart = Number(value(group.selection, "SourceStartEpisodeNumber", "sourceStartEpisodeNumber", 0));
            var sourceNumber = value(mapping, "SourceEpisodeNumber", "sourceEpisodeNumber",
                manualStart > 0 ? manualStart + index : null);
            var sourceEpisodeId = value(mapping, "SourceEpisodeId", "sourceEpisodeId", "");
            line.textContent = localEpisodeLabel(episode) + "（ItemId " +
                value(episode, "ItemId", "itemId", "") + "） → " + sourceLabel(source, sourceNumber) +
                (sourceEpisodeId ? " · 来源集ID " + sourceEpisodeId : "");
            details.appendChild(line);
        });
        card.appendChild(details);
    }

    function renderCompositeSeasonSummary(dialog, item, season, seasonIndex, seasons, selections, keywords) {
        var container = document.createElement("div");
        container.className = "danmuCompositeSeason";
        var header = document.createElement("div");
        header.className = "danmuCompositeHeader";
        header.textContent = value(season, "SeasonName", "seasonName", "未命名季度") + "（库内 " +
            value(season, "EpisodeCount", "episodeCount", 0) + " 集）";
        var hint = document.createElement("div");
        hint.className = "danmuCompositeHint";
        hint.textContent = "该季包含多个来源或存在未识别区间；下列卡片仅用于本次下载映射，不会改变 Emby 的季归属。";
        container.append(header, hint);
        var groups = compositeVirtualGroups(season, selections);
        async function rebuildWithoutGroup(group, reopenPicker) {
            var excludedBefore = compositeExcludedItemIds(dialog, season);
            var removedBefore = compositeDraftSeasonState(dialog, season, false).removedRuns.slice();
            var selectionsBefore = compositeSelectionStore(selections, season, false).slice();
            var groupItemIds = (group.episodes || []).map(function (episode) {
                return String(value(episode, "ItemId", "itemId", "") || "");
            }).filter(Boolean);
            // Compute the request before mutating the dialog draft, then drop every
            // authoritative/manual selection that overlaps the exact clicked run.
            // This prevents an excluded mapping from being re-submitted in the same request.
            var filtered = filterCompositeSelectionsByItemIds(
                season, compositeRequestSelections(selections, season), groupItemIds);
            excludeCompositeRun(dialog, season, group, filtered.removed);
            setBusy(dialog, reopenPicker ? "正在移除旧映射并准备重新匹配…" : "正在移除虚拟季映射…");
            try {
                var confirmed = await requestAuthoritativeCompositePlan(dialog, item, season, filtered.kept);
                adoptAuthoritativeCompositeExclusions(dialog, confirmed);
                seasons[seasonIndex] = confirmed;
                clearCompositeSelectionStore(selections, season);
                if (reopenPicker) {
                    var start = value(group.episodes[0], "ItemId", "itemId", "");
                    var run = compositeVirtualGroups(confirmed, selections).find(function (candidate) {
                        return candidate.kind === "unmatched" && candidate.episodes.some(function (episode) {
                            return value(episode, "ItemId", "itemId", "") === start;
                        });
                    });
                    if (run) {
                        renderCompositeGroupPicker(dialog, item, confirmed, seasonIndex, seasons, selections, keywords, run);
                        return;
                    }
                }
                renderSeriesPicker(dialog, item, seasons, selections, keywords);
            } catch (error) {
                var state = compositeDraftSeasonState(dialog, season, true);
                state.exclusions.splice.apply(state.exclusions,
                    [0, state.exclusions.length].concat(excludedBefore));
                state.removedRuns.splice.apply(state.removedRuns,
                    [0, state.removedRuns.length].concat(removedBefore));
                if (selectionsBefore.length) {
                    var store = selections.__compositeSelections || (selections.__compositeSelections = {});
                    store[seasonSelectionKey(season)] = selectionsBefore;
                }
                renderSeriesPicker(dialog, item, seasons, selections, keywords);
                notify("更新虚拟季失败：" + (error.message || error), true);
            }
        }
        groups.forEach(function (group, groupIndex) {
            var card = document.createElement("div");
            card.className = "danmuVirtualSeason " + (group.kind === "unmatched" ? "unmatched" : "matched");
            var main = document.createElement("div");
            var title = document.createElement("div");
            title.className = "danmuVirtualSeasonTitle";
            var detail = document.createElement("div");
            detail.className = "danmuVirtualSeasonDetail";
            if (group.kind === "unmatched") {
                title.textContent = "未匹配临时季 " + (groupIndex + 1) + "：" + episodeRangeLabel(group.episodes);
                detail.textContent = "尚未找到来源。可手动匹配，或跳过该区间并下载已匹配剧集。";
            } else if (group.kind === "manual") {
                var manualSource = value(group.selection, "Source", "source", {});
                title.textContent = "临时虚拟季 " + (groupIndex + 1) + "：" + episodeRangeLabel(group.episodes);
                detail.textContent = "手动匹配 · " + sourceLabel(manualSource,
                    value(group.selection, "SourceStartEpisodeNumber", "sourceStartEpisodeNumber", ""));
            } else {
                var firstMapping = group.mappings[0] || {};
                title.textContent = "临时虚拟季 " + (groupIndex + 1) + "：" + episodeRangeLabel(group.episodes);
                detail.textContent = "精确集映射 · " + sourceLabel(group.source,
                    value(firstMapping, "SourceEpisodeNumber", "sourceEpisodeNumber", group.sourceStart || ""));
            }
            main.append(title, detail);
            card.appendChild(main);
            appendCompositeMappingDetails(card, group);
            if (group.kind === "unmatched") {
                var match = document.createElement("button");
                match.className = "danmuSmartButton";
                match.textContent = "手动匹配";
                match.addEventListener("click", function () {
                    renderCompositeGroupPicker(dialog, item, season, seasonIndex, seasons, selections, keywords, group);
                });
                card.appendChild(match);
            } else if (group.kind === "manual") {
                var actions = document.createElement("div");
                var rematch = document.createElement("button");
                rematch.className = "danmuSmartButton";
                rematch.textContent = "重新匹配";
                rematch.addEventListener("click", function () {
                    return rebuildWithoutGroup(group, true);
                });
                var remove = document.createElement("button");
                remove.className = "danmuSmartButton";
                remove.textContent = "移除";
                remove.addEventListener("click", function () {
                    return rebuildWithoutGroup(group, false);
                });
                actions.append(rematch, remove);
                card.appendChild(actions);
            } else if (group.kind === "mapped") {
                var mappedActions = document.createElement("div");
                var mappedRematch = document.createElement("button");
                mappedRematch.className = "danmuSmartButton";
                mappedRematch.textContent = "重新匹配";
                mappedRematch.addEventListener("click", function () { return rebuildWithoutGroup(group, true); });
                var mappedRemove = document.createElement("button");
                mappedRemove.className = "danmuSmartButton";
                mappedRemove.textContent = "移除";
                mappedRemove.addEventListener("click", function () { return rebuildWithoutGroup(group, false); });
                mappedActions.append(mappedRematch, mappedRemove);
                card.appendChild(mappedActions);
            }
            container.appendChild(card);
        });
        compositeDraftSeasonState(dialog, season, false).removedRuns.forEach(function (removed) {
            var restore = document.createElement("button");
            restore.className = "danmuSmartButton";
            restore.textContent = "恢复 " + removed.label;
            restore.addEventListener("click", async function () {
                var before = compositeExcludedItemIds(dialog, season);
                var removedBefore = compositeDraftSeasonState(dialog, season, false).removedRuns.slice();
                var currentSelections = filterCompositeSelectionsByItemIds(
                    season, compositeRequestSelections(selections, season), removed.itemIds).kept;
                var restoreSelections = cloneCompositeSelections(removed.selections);
                restoreCompositeRun(dialog, season, removed.itemIds);
                setBusy(dialog, "正在恢复已移除的虚拟季…");
                try {
                    var confirmed = await requestAuthoritativeCompositePlan(
                        dialog, item, season, currentSelections.concat(restoreSelections));
                    if (!compositePlanCoversItemIds(confirmed, removed.itemIds)) {
                        throw new Error(restoreSelections.length
                            ? "原映射已过期，服务器未能重新验证。"
                            : "本地 Episode 标识符已失效，服务器未能重建映射。");
                    }
                    adoptAuthoritativeCompositeExclusions(dialog, confirmed);
                    seasons[seasonIndex] = confirmed;
                    clearCompositeSelectionStore(selections, season);
                    renderSeriesPicker(dialog, item, seasons, selections, keywords);
                } catch (error) {
                    var state = compositeDraftSeasonState(dialog, season, true);
                    state.exclusions.splice.apply(state.exclusions, [0, state.exclusions.length].concat(before));
                    state.removedRuns.splice.apply(state.removedRuns,
                        [0, state.removedRuns.length].concat(removedBefore));
                    renderSeriesPicker(dialog, item, seasons, selections, keywords);
                    notify("恢复虚拟季失败，已保持为未匹配：" + (error.message || error), true);
                }
            });
            container.appendChild(restore);
        });
        if (groups.some(function (group) { return group.kind === "unmatched"; })) {
            var coverage = compositeCoverage(season, selections);
            var warning = document.createElement("div");
            warning.className = "danmuCompositeWarning";
            warning.textContent = "部分下载确认：将处理 " + coverage.processed + " 集，跳过 " + coverage.skipped +
                " 集未匹配剧集。可继续循环匹配，或下载已经确认的虚拟季。";
            container.appendChild(warning);
        }
        dialog.body.appendChild(container);
    }

    function renderCompositeGroupPicker(dialog, item, season, seasonIndex, seasons, selections, keywords, group, options) {
        options = options || {};
        var automaticRangeSearch = !options.skipAutomaticRangeSearch;
        dialog.androidBackLocked = false;
        dialog.setBackHandler(function () {
            renderSeriesPicker(dialog, item, seasons, selections, keywords);
        });
        var existingSelection = group.kind === "manual" ? group.selection : null;
        if (dialog.title) dialog.title.textContent = existingSelection ? "重新匹配临时季" : "手动匹配未匹配临时季";
        dialog.body.replaceChildren();
        dialog.footer.replaceChildren();
        var summary = document.createElement("p");
        summary.textContent = "本次范围：" + episodeRangeLabel(group.episodes) + "。选择来源后需确认来源起始集；服务器会重新验证来源剧集并只应用于此连续区间。";
        dialog.body.appendChild(summary);
        var inputs = document.createElement("div");
        inputs.className = "danmuCompositeInputs";
        var countLabel = document.createElement("label");
        countLabel.textContent = "匹配集数";
        var count = document.createElement("input");
        count.type = "number";
        count.min = "1";
        count.max = String(group.episodes.length);
        count.value = String(Math.min(group.episodes.length,
            Number(value(existingSelection, "RequestedEpisodeCount", "requestedEpisodeCount", group.episodes.length)) || group.episodes.length));
        countLabel.appendChild(count);
        var startLabel = document.createElement("label");
        startLabel.textContent = "来源起始集";
        var sourceStart = document.createElement("input");
        sourceStart.type = "number";
        sourceStart.min = "1";
        sourceStart.value = String(Math.max(1,
            Number(value(existingSelection, "SourceStartEpisodeNumber", "sourceStartEpisodeNumber", 1)) || 1));
        startLabel.appendChild(sourceStart);
        inputs.append(countLabel, startLabel);
        dialog.body.appendChild(inputs);

        var search = document.createElement("div");
        search.className = "danmuSmartSearch";
        var input = document.createElement("input");
        input.type = "search";
        input.placeholder = "输入该临时季的关键词重新搜索";
        var key = seasonSelectionKey(season) + "::" + value(group.episodes[0], "ItemId", "itemId", "");
        var hasExplicitKeyword = Object.prototype.hasOwnProperty.call(keywords, key);
        input.value = hasExplicitKeyword ? keywords[key] : temporaryRangeKeyword(item, season);
        var searchButton = document.createElement("button");
        searchButton.className = "danmuSmartButton";
        initializeKeywordIntent(input, searchButton, hasExplicitKeyword);
        search.append(input, searchButton);
        dialog.body.appendChild(search);

        if (automaticRangeSearch) {
            // Never show a saved ProviderId/full-Season candidate while the
            // authoritative range search is being prepared.
            season.Candidates = [];
            season.candidates = [];
        }
        var candidates = automaticRangeSearch ? [] : seasonCandidates(season);
        var list = document.createElement("div");
        if (!candidates.length) {
            var empty = document.createElement("p");
            empty.className = "danmuMuted";
            empty.textContent = value(season, "Message", "message", "没有候选结果，请更换关键词重试。");
            list.appendChild(empty);
        }
        candidates.forEach(function (candidate, index) {
            var row = document.createElement("label");
            row.className = "danmuCandidate";
            var radio = document.createElement("input");
            radio.type = "radio";
            radio.name = "danmuCompositeCandidate";
            radio.value = String(index);
            var main = document.createElement("div");
            main.className = "danmuCandidateMain";
            var title = document.createElement("div");
            title.className = "danmuCandidateTitle";
            title.textContent = candidateLine(candidate);
            var reason = document.createElement("div");
            reason.className = "danmuCandidateReason";
            reason.textContent = backendDecisionLine(candidate);
            main.append(title, reason);
            row.append(radio, main);
            list.appendChild(row);
        });
        dialog.body.appendChild(list);

        async function searchCurrentGroup(isAutomatic) {
            var keyword = input.value.trim();
            if (!keyword) {
                notify("请输入临时季的搜索关键词。", true);
                return;
            }
            try {
                var parameters = temporaryRangeSearchParameters(
                    dialog, item, season, group, selections, keyword);
                var refreshed = await runDialogSearch(
                    dialog, parameters.seriesId || item.Id, "provider-search", parameters,
                    "正在搜索临时季候选…", function (status, error) {
                        season.Message = status === "cancelled"
                            ? "临时季搜索已取消，可重试。"
                            : "临时季搜索失败，可重试：" + (error && (error.message || error) || "未知错误");
                        renderCompositeGroupPicker(dialog, item, season, seasonIndex, seasons, selections, keywords, group,
                            { skipAutomaticRangeSearch: true });
                    });
                if (!refreshed) return;
                var refreshedSeason = (value(refreshed, "Seasons", "seasons", []) || [])[0];
                if (!refreshedSeason) throw new Error("服务器没有返回临时季候选");
                // This request searches candidates for one still-unmatched
                // range. Keep the canonical base plan currently on screen:
                // replacing it with the search response's provisional
                // auto-selection would silently override the radio choice the
                // user is about to make.
                season.Candidates = seasonCandidates(refreshedSeason);
                season.Message = value(refreshedSeason, "Message", "message", value(season, "Message", "message", ""));
                seasons[seasonIndex] = season;
                keywords[key] = keyword;
                renderCompositeGroupPicker(dialog, item, season, seasonIndex, seasons, selections, keywords, group,
                    { skipAutomaticRangeSearch: true });
            } catch (error) {
                season.Message = "临时季搜索失败，可重试：" + (error.message || error);
                renderCompositeGroupPicker(dialog, item, season, seasonIndex, seasons, selections, keywords, group,
                    { skipAutomaticRangeSearch: true });
                notify("临时季搜索失败：" + (error.message || error), true);
            }
        }
        searchButton.addEventListener("click", function () { searchCurrentGroup(false); });
        input.addEventListener("keydown", function (event) {
            if (event.key === "Enter") { event.preventDefault(); searchCurrentGroup(false); }
        });

        var back = document.createElement("button");
        back.className = "danmuSmartButton";
        back.textContent = "返回总览";
        back.addEventListener("click", function () { renderSeriesPicker(dialog, item, seasons, selections, keywords); });
        var save = document.createElement("button");
        save.className = "danmuSmartButton primary";
        save.textContent = "应用到此临时季";
        save.disabled = !candidates.length;
        save.addEventListener("click", async function () {
            var checked = list.querySelector('input[name="danmuCompositeCandidate"]:checked');
            if (!checked) { notify("请选择一个候选结果。", true); return; }
            var sourceNumber = Math.max(1, Number(sourceStart.value) || 1);
            var candidate = candidates[Number(checked.value)];
            var requested = Math.max(1, Math.min(group.episodes.length,
                Number(count.value) || group.episodes.length));
            var selectionsForSeason = compositeSelectionStore(selections, season, true);
            var previousSelections = selectionsForSeason.slice();
            var localStart = value(group.episodes[0], "ItemId", "itemId", "");
            var replacement = {
                LocalStartEpisodeItemId: localStart,
                RequestedEpisodeCount: requested,
                Source: { ProviderId: value(candidate, "Site", "site", ""), MediaId: value(candidate, "Id", "id", "") },
                SourceStartEpisodeNumber: sourceNumber,
                Origin: "manual"
            };
            var old = selectionsForSeason.findIndex(function (choice) {
                return value(choice, "LocalStartEpisodeItemId", "localStartEpisodeItemId", "") === localStart;
            });
            if (old >= 0) selectionsForSeason[old] = replacement;
            else selectionsForSeason.push(replacement);
            setBusy(dialog, "正在由服务器验证逐集精确映射…");
            try {
                var confirmed = await requestAuthoritativeCompositePlan(
                    dialog, item, season, compositeRequestSelections(selections, season));
                adoptAuthoritativeCompositeExclusions(dialog, confirmed);
                seasons[seasonIndex] = confirmed;
                clearCompositeSelectionStore(selections, season);
                renderSeriesPicker(dialog, item, seasons, selections, keywords);
            } catch (error) {
                var store = selections.__compositeSelections || (selections.__compositeSelections = {});
                store[seasonSelectionKey(season)] = previousSelections;
                renderCompositeGroupPicker(dialog, item, season, seasonIndex, seasons, selections, keywords, group);
                notify("临时季映射验证失败：" + (error.message || error), true);
            }
        });
        dialog.footer.append(back, save);
        if (automaticRangeSearch) searchCurrentGroup(true);
    }

    function renderSeriesPicker(dialog, item, seasons, selections, keywords) {
        dialog.setBackHandler(null);
        dialog.androidBackLocked = false;
        selections = selections || {};
        keywords = keywords || {};
        if (dialog.title) dialog.title.textContent = "整部剧弹幕智能匹配";
        dialog.body.replaceChildren();
        dialog.footer.replaceChildren();

        seasons.forEach(function (season) {
            var key = seasonSelectionKey(season);
            if (!selections[key] && hasBackendMatch(season)) {
                var current = selectedCandidate(season);
                if (current) selections[key] = current;
            }
        });
        var message = document.createElement("p");
        message.textContent = "匹配状态、来源和决策原因均由服务器返回。需要修改时可查看服务器返回的全部候选；浏览器不会重新评分或调整候选顺序。";
        dialog.body.appendChild(message);

        seasons.forEach(function (season, seasonIndex) {
            if (hasCompositePlan(season)) {
                renderCompositeSeasonSummary(dialog, item, season, seasonIndex, seasons, selections, keywords);
                return;
            }
            var selectionKey = seasonSelectionKey(season);
            var selection = selections[selectionKey];
            var block = document.createElement("div");
            block.className = "danmuSeasonSummary " + (selection ? "matched" : "unmatched");
            var main = document.createElement("div");
            var title = document.createElement("div");
            title.className = "danmuSeasonSummaryTitle";
            title.textContent = value(season, "SeasonName", "seasonName", "未命名季度") + "（" +
                (value(season, "Year", "year", null) || "年份未知") + "，库内 " +
                value(season, "EpisodeCount", "episodeCount", 0) + " 集）";
            var matched = hasBackendMatch(season);
            var state = document.createElement("div");
            state.className = "danmuSeasonSummaryState";
            state.textContent = matched ? "✓ 匹配成功" : "✕ 匹配失败";
            var detail = document.createElement("div");
            detail.className = "danmuSeasonSummaryDetail";
            detail.textContent = backendDecisionLine(season) ||
                value(season, "Message", "message", "服务器未返回决策说明");
            if (selection) detail.textContent += (detail.textContent ? "　" : "") + candidateLine(selection);
            main.append(title, state, detail);
            var manual = document.createElement("button");
            manual.className = "danmuSmartButton";
            manual.textContent = isProviderIdMatch(season) ? "重新智能匹配" : "查看候选";
            manual.addEventListener("click", async function () {
                if (!isProviderIdMatch(season)) {
                    renderSeriesSeasonPicker(dialog, item, seasons, seasonIndex, selections, keywords);
                    return;
                }
                try {
                    var parameters = seasonRequestParameters(season);
                    var refreshed = await runDialogSearch(
                        dialog, parameters.seriesId || item.Id, "provider-search", rematchParameters(parameters),
                        "正在请求服务器重新智能匹配…", function (status, error) {
                            renderSeriesPicker(dialog, item, seasons, selections, keywords);
                            notify(status === "cancelled" ? "已取消重新智能匹配。" :
                                "重新智能匹配失败：" + (error && (error.message || error) || "未知错误"), true);
                        });
                    if (!refreshed) return;
                    var refreshedSeason = (value(refreshed, "Seasons", "seasons", []) || [])[0];
                    if (!refreshedSeason) throw new Error("服务器没有返回本季候选");
                    seasons[seasonIndex] = refreshedSeason;
                    renderSeriesSeasonPicker(dialog, item, seasons, seasonIndex, selections, keywords);
                } catch (error) {
                    renderSeriesPicker(dialog, item, seasons, selections, keywords);
                    notify("重新智能匹配失败：" + (error.message || error), true);
                }
            });
            block.append(main, manual);
            dialog.body.appendChild(block);
        });

        appendForceRefreshOption(dialog);

        var ok = document.createElement("button");
        ok.className = "danmuSmartButton primary";
        var matchedSeasons = seasons.filter(function (season) {
            return hasCompositePlan(season)
                ? compositeHasDownloadableMappings(season, selections)
                : Boolean(selections[seasonSelectionKey(season)]);
        });
        var compositeTotals = matchedSeasons.filter(hasCompositePlan).reduce(function (totals, season) {
            var coverage = compositeCoverage(season, selections);
            totals.processed += coverage.processed;
            totals.skipped += coverage.skipped;
            return totals;
        }, { processed: 0, skipped: 0 });
        ok.textContent = compositeTotals.skipped > 0
            ? "确认部分下载（处理 " + compositeTotals.processed + " 集，跳过 " + compositeTotals.skipped + " 集）"
            : (matchedSeasons.length === seasons.length ? "下载全部匹配季度" :
                "下载已匹配季度（" + matchedSeasons.length + "/" + seasons.length + "）");
        ok.disabled = matchedSeasons.length === 0;
        ok.addEventListener("click", function () {
            submitSeriesSelections(dialog, matchedSeasons, selections);
        });
        dialog.footer.appendChild(ok);
    }

    function renderSeriesSeasonPicker(dialog, item, seasons, seasonIndex, selections, keywords) {
        dialog.androidBackLocked = false;
        dialog.setBackHandler(function () {
            renderSeriesPicker(dialog, item, seasons, selections, keywords);
        });
        var season = seasons[seasonIndex];
        var selectionKey = seasonSelectionKey(season);
        var candidates = seasonCandidates(season);
        var current = selections[selectionKey] || selectedCandidate(season);
        if (dialog.title) {
            dialog.title.textContent = "手动匹配：" + value(season, "SeasonName", "seasonName", "本季");
        }
        dialog.body.replaceChildren();
        dialog.footer.replaceChildren();

        var summary = document.createElement("p");
        summary.textContent = "库内信息：" + value(season, "SeriesName", "seriesName", "") + " / " +
            value(season, "SeasonName", "seasonName", "") + "，" +
            (value(season, "Year", "year", null) || "年份未知") + "，" +
            value(season, "EpisodeCount", "episodeCount", 0) + " 集。" +
            (isProviderIdMatch(season) && hasBackendMatch(season) ? "　✓ 匹配成功" : "") +
            (backendDecisionLine(season) ? "　" + backendDecisionLine(season) : "");
        dialog.body.appendChild(summary);

        var search = document.createElement("div");
        search.className = "danmuSmartSearch";
        var input = document.createElement("input");
        input.type = "search";
        input.placeholder = "输入本季关键词重新搜索";
        var hasExplicitKeyword = Object.prototype.hasOwnProperty.call(keywords, selectionKey);
        input.value = hasExplicitKeyword
            ? keywords[selectionKey]
            : value(season, "SeriesName", "seriesName", item.SeriesName || item.Name || "");
        var searchButton = document.createElement("button");
        searchButton.className = "danmuSmartButton";
        initializeKeywordIntent(input, searchButton, hasExplicitKeyword);
        search.append(input, searchButton);
        dialog.body.appendChild(search);

        var list = document.createElement("div");
        if (!candidates.length) {
            var empty = document.createElement("p");
            empty.className = "danmuMuted";
            empty.textContent = value(season, "Message", "message", "没有候选结果，请更换关键词重试。");
            list.appendChild(empty);
        }
        candidates.forEach(function (candidate, candidateIndex) {
            var row = document.createElement("label");
            row.className = "danmuCandidate";
            var radio = document.createElement("input");
            radio.type = "radio";
            radio.name = "danmuSeriesManualCandidate";
            radio.value = String(candidateIndex);
            radio.checked = Boolean(current &&
                value(current, "Id", "id", "") === value(candidate, "Id", "id", "") &&
                value(current, "Site", "site", "") === value(candidate, "Site", "site", ""));
            var main = document.createElement("div");
            main.className = "danmuCandidateMain";
            var candidateTitle = document.createElement("div");
            candidateTitle.className = "danmuCandidateTitle";
            candidateTitle.textContent = value(candidate, "SiteName", "siteName", value(candidate, "Site", "site", "未知网站")) + " · " + value(candidate, "Name", "name", "未命名项目");
            var meta = document.createElement("div");
            meta.className = "danmuCandidateMeta";
            meta.textContent = "年份：" + (value(candidate, "Year", "year", null) || "未知") +
                "　集数：" + (value(candidate, "EpisodeSize", "episodeSize", 0) || "未知") +
                "　类型：" + (value(candidate, "Category", "category", "未知") || "未知");
            var reason = document.createElement("div");
            reason.className = "danmuCandidateReason";
            reason.textContent = backendDecisionLine(candidate);
            main.append(candidateTitle, meta, reason);
            row.append(radio, main);
            list.appendChild(row);
        });
        dialog.body.appendChild(list);

        async function searchCurrentSeason() {
            var keyword = input.value.trim();
            if (!keyword) {
                notify("请输入本季搜索关键词。", true);
                return;
            }
            try {
                var parameters = seasonRequestParameters(season);
                var searchItemId = parameters.seriesId || item.Id;
                var requestParameters = keywordRematchParameters(parameters, input);
                var explicitKeyword = input.dataset.danmuExplicitKeyword === "true";
                var refreshed = await runDialogSearch(
                    dialog, searchItemId, "provider-search", requestParameters,
                    "正在使用新关键词搜索本季候选…", function (status, error) {
                        renderSeriesSeasonPicker(dialog, item, seasons, seasonIndex, selections, keywords);
                        notify(status === "cancelled" ? "已取消本季搜索。" :
                            "本季重新搜索失败：" + (error && (error.message || error) || "未知错误"), true);
                    });
                if (!refreshed) return;
                var refreshedSeason = (value(refreshed, "Seasons", "seasons", []) || [])[0];
                if (!refreshedSeason) throw new Error("服务器没有返回本季候选");
                var oldKey = selectionKey;
                seasons[seasonIndex] = refreshedSeason;
                var newKey = seasonSelectionKey(refreshedSeason);
                if (newKey !== oldKey && selections[oldKey] && !selections[newKey]) {
                    selections[newKey] = selections[oldKey];
                    delete selections[oldKey];
                }
                if (explicitKeyword) keywords[newKey] = keyword;
                else delete keywords[newKey];
                renderSeriesSeasonPicker(dialog, item, seasons, seasonIndex, selections, keywords);
            } catch (error) {
                renderSeriesSeasonPicker(dialog, item, seasons, seasonIndex, selections, keywords);
                notify("本季重新搜索失败：" + (error.message || error), true);
            }
        }
        searchButton.addEventListener("click", searchCurrentSeason);
        input.addEventListener("keydown", function (event) {
            if (event.key === "Enter") {
                event.preventDefault();
                searchCurrentSeason();
            }
        });

        var back = document.createElement("button");
        back.className = "danmuSmartButton";
        back.textContent = "返回总览";
        back.addEventListener("click", function () {
            renderSeriesPicker(dialog, item, seasons, selections, keywords);
        });
        var save = document.createElement("button");
        save.className = "danmuSmartButton primary";
        save.textContent = "保存本季选择";
        save.disabled = !candidates.length;
        save.addEventListener("click", function () {
            var checked = list.querySelector('input[name="danmuSeriesManualCandidate"]:checked');
            if (!checked) {
                notify("请选择一个候选结果。", true);
                return;
            }
            selections[seasonSelectionKey(season)] = candidates[Number(checked.value)];
            renderSeriesPicker(dialog, item, seasons, selections, keywords);
        });
        dialog.footer.append(back, save);
    }

    function renderCandidatePicker(dialog, item, season, keyword) {
        if (hasCompositePlan(season)) {
            renderCompositeTargetPicker(dialog, item, season);
            return;
        }
        dialog.setBackHandler(null);
        dialog.androidBackLocked = false;
        dialog.body.replaceChildren();
        dialog.footer.replaceChildren();

        var summary = document.createElement("p");
        summary.textContent = "库内信息：" + value(season, "SeriesName", "seriesName", "") + " / " + value(season, "SeasonName", "seasonName", "") + "，" + (value(season, "Year", "year", null) || "年份未知") + "，" + value(season, "EpisodeCount", "episodeCount", 0) + " 集。请选择正确项目，绑定会被保存供以后使用。" +
            (isProviderIdMatch(season) && hasBackendMatch(season) ? "　✓ 匹配成功" : "") +
            (backendDecisionLine(season) ? "　" + backendDecisionLine(season) : "");
        dialog.body.appendChild(summary);

        var search = document.createElement("div");
        search.className = "danmuSmartSearch";
        var input = document.createElement("input");
        input.type = "search";
        input.placeholder = "换一个关键词重新搜索，例如：唐诡奇潭";
        input.value = keyword || value(season, "SeriesName", "seriesName", item.SeriesName || item.Name || "");
        var searchButton = document.createElement("button");
        searchButton.className = "danmuSmartButton";
        initializeKeywordIntent(input, searchButton, Boolean(keyword));
        search.append(input, searchButton);
        dialog.body.appendChild(search);

        var list = document.createElement("div");
        var candidates = seasonCandidates(season);
        var currentCandidate = selectedCandidate(season);
        if (!candidates.length) {
            var empty = document.createElement("p");
            empty.className = "danmuMuted";
            empty.textContent = value(season, "Message", "message", "没有候选结果，请更换关键词重试。");
            list.appendChild(empty);
        }
        candidates.forEach(function (candidate, index) {
            var row = document.createElement("label");
            row.className = "danmuCandidate";
            var radio = document.createElement("input");
            radio.type = "radio";
            radio.name = "danmuCandidateChoice";
            radio.value = String(index);
            if (currentCandidate &&
                value(currentCandidate, "Id", "id", "") === value(candidate, "Id", "id", "") &&
                value(currentCandidate, "Site", "site", "") === value(candidate, "Site", "site", "")) {
                radio.checked = true;
            }
            var main = document.createElement("div");
            main.className = "danmuCandidateMain";
            var title = document.createElement("div");
            title.className = "danmuCandidateTitle";
            title.textContent = value(candidate, "SiteName", "siteName", value(candidate, "Site", "site", "未知网站")) + " · " + value(candidate, "Name", "name", "未命名项目");
            var meta = document.createElement("div");
            meta.className = "danmuCandidateMeta";
            meta.textContent = "年份：" + (value(candidate, "Year", "year", null) || "未知") + "　集数：" + (value(candidate, "EpisodeSize", "episodeSize", 0) || "未知") + "　类型：" + (value(candidate, "Category", "category", "未知") || "未知");
            var reason = document.createElement("div");
            reason.className = "danmuCandidateReason";
            reason.textContent = backendDecisionLine(candidate);
            main.append(title, meta, reason);
            row.append(radio, main);
            list.appendChild(row);
        });
        dialog.body.appendChild(list);

        appendForceRefreshOption(dialog);

        var bind = document.createElement("button");
        bind.className = "danmuSmartButton primary";
        bind.textContent = "绑定并下载";
        bind.disabled = !candidates.length;
        dialog.footer.appendChild(bind);

        searchButton.addEventListener("click", async function () {
            var newKeyword = input.value.trim();
            if (!newKeyword) {
                notify("请输入搜索关键词。", true);
                return;
            }
            try {
                var explicitKeyword = input.dataset.danmuExplicitKeyword === "true";
                var refreshed = await runDialogSearch(
                    dialog, item.Id, "provider-search", keywordRematchParameters({}, input),
                    "正在使用新关键词搜索所有已启用网站…", function (status, error) {
                        renderCandidatePicker(dialog, item, season,
                            input.dataset.danmuExplicitKeyword === "true" ? newKeyword : "");
                        notify(status === "cancelled" ? "已取消重新搜索。" :
                            "重新搜索失败：" + (error && (error.message || error) || "未知错误"), true);
                    });
                if (!refreshed) return;
                var refreshedSeason = (value(refreshed, "Seasons", "seasons", []) || [])[0];
                renderCandidatePicker(dialog, item, refreshedSeason || season, explicitKeyword ? newKeyword : "");
            } catch (error) {
                renderCandidatePicker(dialog, item, season,
                    input.dataset.danmuExplicitKeyword === "true" ? newKeyword : "");
                notify("重新搜索失败：" + (error.message || error), true);
            }
        });
        input.addEventListener("keydown", function (event) {
            if (event.key === "Enter") {
                event.preventDefault();
                searchButton.click();
            }
        });

        bind.addEventListener("click", async function () {
            var checked = list.querySelector('input[name="danmuCandidateChoice"]:checked');
            if (!checked) {
                return;
            }
            var candidate = candidates[Number(checked.value)];
            var selections = {};
            selections[seasonSelectionKey(season)] = candidate;
            submitSeriesSelections(dialog, [season], selections);
        });
    }

    function renderCompositeTargetPicker(dialog, item, season) {
        // Reuse the same virtual-season summary and submit path as the Series
        // picker.  It intentionally keeps the season as a one-item list so the
        // generated request still targets its real Emby SeasonId.
        renderSeriesPicker(dialog, item, [season], {}, {});
        if (dialog.title) dialog.title.textContent = "本季弹幕智能匹配";
    }

    function itemCandidates(target) {
        return value(target, "Candidates", "candidates", []) || [];
    }

    function itemSelectedCandidate(target) {
        var selectedId = value(target, "SelectedId", "selectedId", "");
        var selectedSite = value(target, "SelectedSite", "selectedSite", "");
        return itemCandidates(target).find(function (candidate) {
            return value(candidate, "Id", "id", "") === selectedId &&
                value(candidate, "Site", "site", "") === selectedSite;
        }) || null;
    }

    function resolvedScopeLine(target) {
        var scopeType = String(value(target, "ResolvedScopeType", "resolvedScopeType", "") || "").trim();
        var scopeItemId = String(value(target, "ResolvedScopeItemId", "resolvedScopeItemId", "") || "").trim();
        if (!scopeType && !scopeItemId) return "";
        return "标识符作用域：" + (scopeType || "未知") + (scopeItemId ? " · ItemId " + scopeItemId : "");
    }

    function selectedCandidateEpisodes(detail) {
        return (value(detail, "Episodes", "episodes", []) || []).filter(function (episode) {
            return Boolean(String(value(episode, "Id", "id", "") || "").trim());
        });
    }

    function renderEpisodeSourcePicker(dialog, item, target, candidate, detail, keyword, manual) {
        var episodes = selectedCandidateEpisodes(detail);
        dialog.androidBackLocked = false;
        dialog.body.replaceChildren();
        dialog.footer.replaceChildren();
        dialog.setBackHandler(function () { renderItemCandidatePicker(dialog, item, target, keyword); });

        var summary = document.createElement("p");
        summary.textContent = candidateLine(candidate) + "。请选择要绑定到本地第 " +
            (value(target, "EpisodeNumber", "episodeNumber", "?") || "?") + " 集的来源剧集。";
        var scope = resolvedScopeLine(target);
        if (scope) summary.textContent += "　" + scope;
        dialog.body.appendChild(summary);

        var list = document.createElement("div");
        episodes.forEach(function (episode, index) {
            var row = document.createElement("label");
            row.className = "danmuCandidate danmuSourceEpisodeChoice";
            var radio = document.createElement("input");
            radio.type = "radio";
            radio.name = "danmuSourceEpisodeChoice";
            radio.value = String(index);
            var main = document.createElement("div");
            main.className = "danmuCandidateMain";
            var title = document.createElement("div");
            title.className = "danmuCandidateTitle";
            var number = value(episode, "Number", "number", null);
            title.textContent = (number ? "第 " + number + " 集 · " : "") +
                value(episode, "Title", "title", "未命名来源剧集");
            var meta = document.createElement("div");
            meta.className = "danmuCandidateMeta";
            meta.textContent = "来源 Episode ID：" + value(episode, "Id", "id", "");
            main.append(title, meta);
            row.append(radio, main);
            list.appendChild(row);
        });
        dialog.body.appendChild(list);

        var back = document.createElement("button");
        back.className = "danmuSmartButton";
        back.textContent = "返回候选列表";
        back.addEventListener("click", function () { renderItemCandidatePicker(dialog, item, target, keyword); });
        var start = document.createElement("button");
        start.className = "danmuSmartButton primary";
        start.textContent = "绑定并下载本集弹幕";
        start.disabled = !episodes.length;
        start.addEventListener("click", function () {
            var checked = list.querySelector('input[name="danmuSourceEpisodeChoice"]:checked');
            if (!checked) {
                notify("请选择一个来源剧集。", true);
                return;
            }
            var episode = episodes[Number(checked.value)];
            renderSingleTargetProgress(dialog, item, target, candidate,
                value(episode, "Number", "number", null), value(episode, "Id", "id", ""), manual);
        });
        dialog.footer.append(back, start);
    }

    async function resolveSelectedCandidateDetail(dialog, item, target, candidate, keyword, manual) {
        var detail = await runDialogSearch(
            dialog, item.Id, "detail-resolution", {
                site: value(candidate, "Site", "site", ""),
                candidateId: value(candidate, "Id", "id", "")
            }, "正在解析所选候选的来源剧集…", function (status, error) {
                renderItemCandidatePicker(dialog, item, target, keyword);
                notify(status === "cancelled" ? "已取消来源剧集解析。" :
                    "来源剧集解析失败：" + (error && (error.message || error) || "未知错误"), true);
            }, "GetSelectedCandidatePreview");
        if (!detail) return null;
        var status = String(value(detail, "Status", "status", "") || "").toLowerCase();
        var episodes = selectedCandidateEpisodes(detail);
        if (status !== "ready" || !episodes.length) {
            renderItemCandidatePicker(dialog, item, target, keyword);
            notify(value(detail, "Message", "message", "所选候选没有可用的来源剧集。"), true);
            return null;
        }
        renderEpisodeSourcePicker(dialog, item, target, candidate, detail, keyword, manual);
        return detail;
    }

    function renderItemCandidatePicker(dialog, item, target, keyword) {
        dialog.setBackHandler(null);
        dialog.androidBackLocked = false;
        dialog.body.replaceChildren();
        dialog.footer.replaceChildren();
        var isEpisode = item.Type === "Episode";
        var candidates = itemCandidates(target);
        var selected = itemSelectedCandidate(target);
        var summary = document.createElement("p");
        if (isEpisode) {
            summary.textContent = "库内信息：" + value(target, "ParentName", "parentName", "") + " / " +
                value(target, "SeasonName", "seasonName", "") + " / 第 " +
                (value(target, "EpisodeNumber", "episodeNumber", "?") || "?") + " 集 · " +
                value(target, "ItemName", "itemName", item.Name || "未命名剧集") +
                "。请选择季度候选，再解析该候选的来源剧集。";
        } else {
            summary.textContent = "库内信息：" + value(target, "ItemName", "itemName", item.Name || "未命名电影") +
                "，" + (value(target, "Year", "year", null) || "年份未知") + "。请选择正确电影。";
        }
        if (isProviderIdMatch(target) && hasBackendMatch(target)) summary.textContent += "　✓ 匹配成功";
        if (backendDecisionLine(target)) summary.textContent += "　" + backendDecisionLine(target);
        var scope = resolvedScopeLine(target);
        if (scope) summary.textContent += "　" + scope;
        dialog.body.appendChild(summary);

        var search = document.createElement("div");
        search.className = "danmuSmartSearch";
        var input = document.createElement("input");
        input.type = "search";
        input.placeholder = "输入关键词重新搜索";
        input.value = keyword || manualSearchDefault(item, target);
        var searchButton = document.createElement("button");
        searchButton.className = "danmuSmartButton";
        initializeKeywordIntent(input, searchButton, Boolean(keyword));
        search.append(input, searchButton);
        dialog.body.appendChild(search);

        var list = document.createElement("div");
        if (!candidates.length) {
            var empty = document.createElement("p");
            empty.className = "danmuMuted";
            empty.textContent = value(target, "Message", "message", "没有候选结果，请更换关键词重试。");
            list.appendChild(empty);
        }
        candidates.forEach(function (candidate, index) {
            var row = document.createElement("label");
            row.className = "danmuCandidate";
            var radio = document.createElement("input");
            radio.type = "radio";
            radio.name = "danmuItemCandidateChoice";
            radio.value = String(index);
            radio.checked = Boolean(selected &&
                value(selected, "Id", "id", "") === value(candidate, "Id", "id", "") &&
                value(selected, "Site", "site", "") === value(candidate, "Site", "site", ""));
            var main = document.createElement("div");
            main.className = "danmuCandidateMain";
            var title = document.createElement("div");
            title.className = "danmuCandidateTitle";
            title.textContent = value(candidate, "SiteName", "siteName", value(candidate, "Site", "site", "未知网站")) +
                " · " + value(candidate, "Name", "name", "未命名项目");
            var meta = document.createElement("div");
            meta.className = "danmuCandidateMeta";
            meta.textContent = "年份：" + (value(candidate, "Year", "year", null) || "未知") +
                "　集数：" + (value(candidate, "EpisodeSize", "episodeSize", 0) || "未知") +
                "　类型：" + (value(candidate, "Category", "category", "未知") || "未知");
            var reason = document.createElement("div");
            reason.className = "danmuCandidateReason";
            reason.textContent = backendDecisionLine(candidate);
            main.append(title, meta, reason);
            row.append(radio, main);
            list.appendChild(row);
        });
        dialog.body.appendChild(list);
        appendForceRefreshOption(dialog);

        searchButton.addEventListener("click", async function () {
            var newKeyword = input.value.trim();
            if (!newKeyword) {
                notify("请输入搜索关键词。", true);
                return;
            }
            try {
                var explicitKeyword = input.dataset.danmuExplicitKeyword === "true";
                var refreshed = await runDialogSearch(
                    dialog, item.Id, "provider-search", keywordRematchParameters({}, input),
                    "正在使用新关键词搜索所有已启用网站…", function (status, error) {
                        renderItemCandidatePicker(dialog, item, target,
                            input.dataset.danmuExplicitKeyword === "true" ? newKeyword : "");
                        notify(status === "cancelled" ? "已取消重新搜索。" :
                            "重新搜索失败：" + (error && (error.message || error) || "未知错误"), true);
                    });
                if (!refreshed) return;
                var refreshedTarget = value(refreshed, "Target", "target", null);
                if (!refreshedTarget) throw new Error("服务器没有返回媒体候选");
                renderItemCandidatePicker(dialog, item, refreshedTarget, explicitKeyword ? newKeyword : "");
            } catch (error) {
                renderItemCandidatePicker(dialog, item, target,
                    input.dataset.danmuExplicitKeyword === "true" ? newKeyword : "");
                notify("重新搜索失败：" + (error.message || error), true);
            }
        });
        input.addEventListener("keydown", function (event) {
            if (event.key === "Enter") {
                event.preventDefault();
                searchButton.click();
            }
        });

        var start = document.createElement("button");
        start.className = "danmuSmartButton primary";
        start.textContent = isEpisode ? "解析所选候选的来源剧集" : "绑定并下载电影弹幕";
        start.disabled = !candidates.length;
        start.addEventListener("click", function () {
            var checked = list.querySelector('input[name="danmuItemCandidateChoice"]:checked');
            if (!checked) {
                notify("请选择一个候选结果。", true);
                return;
            }
            var candidateIndex = Number(checked.value);
            var candidate = candidates[candidateIndex];
            var manual = !selected ||
                value(selected, "Id", "id", "") !== value(candidate, "Id", "id", "") ||
                value(selected, "Site", "site", "") !== value(candidate, "Site", "site", "");
            if (isEpisode) {
                resolveSelectedCandidateDetail(dialog, item, target, candidate, keyword, manual);
            } else {
                renderSingleTargetProgress(dialog, item, target, candidate, null, null, manual);
            }
        });
        dialog.footer.appendChild(start);
    }

    async function renderSingleTargetProgress(dialog, item, target, candidate, sourceEpisodeNumber, sourceEpisodeId, manual) {
        if (item.Type === "Episode" && !String(sourceEpisodeId || "").trim()) {
            notify("请选择有效的来源剧集后再下载。", true);
            return;
        }
        dialog.setBackHandler(null);
        dialog.closable = false;
        setBusy(dialog, "正在提交下载任务…");
        var parameters = {
            site: value(candidate, "Site", "site", ""),
            candidateId: value(candidate, "Id", "id", ""),
            manual: manual ? "true" : "false",
            forceRefresh: dialog.forceRefresh ? "true" : "false"
        };
        if (sourceEpisodeNumber) parameters.sourceEpisodeNumber = sourceEpisodeNumber;
        if (sourceEpisodeId) parameters.sourceEpisodeId = sourceEpisodeId;
        var task = await api(item.Id, "StartTrackedDownload", parameters);
        var taskId = value(task, "TaskId", "taskId", "");
        var monitoring = false;

        dialog.androidBackLocked = false;
        dialog.body.replaceChildren();
        dialog.footer.replaceChildren();
        var summary = document.createElement("div");
        summary.className = "danmuProgressSummary";
        var block = document.createElement("div");
        block.className = "danmuProgressSeason running";
        var title = document.createElement("div");
        title.className = "danmuProgressTitle";
        var name = document.createElement("span");
        name.textContent = item.Type === "Episode"
            ? "本地第 " + (value(target, "EpisodeNumber", "episodeNumber", "?") || "?") + " 集 · " + item.Name
            : item.Name;
        var state = document.createElement("span");
        title.append(name, state);
        var meta = document.createElement("div");
        meta.className = "danmuProgressMeta";
        var items = document.createElement("div");
        block.append(title, meta, items);
        dialog.body.append(summary, block);

        var background = document.createElement("button");
        background.className = "danmuSmartButton";
        background.textContent = "后台下载";
        background.addEventListener("click", function () {
            dialog.forceClose();
            notify("下载任务已转入服务器后台队列，关闭页面不会取消任务。", false);
        });
        var stop = document.createElement("button");
        stop.className = "danmuSmartButton danger";
        stop.textContent = "强制停止全部下载";
        dialog.footer.append(background, stop);

        function terminal(status) {
            return status === "completed" || status === "completed_with_warnings" ||
                status === "completed_with_errors" || status === "failed" ||
                status === "cancelled" || status === "not_found";
        }

        function update(current) {
            var status = value(current, "Status", "status", "failed");
            var succeeded = value(current, "Succeeded", "succeeded", 0);
            var skipped = value(current, "Skipped", "skipped", 0);
            var partial = value(current, "Partial", "partial", 0);
            var failed = value(current, "Failed", "failed", 0);
            state.textContent = status === "queued" ? "后台队列中" :
                (status === "running" ? "进行中" :
                    (status === "stopping" ? "正在停止" :
                        (status === "completed" ? "完成 ✓" :
                            (status === "cancelled" ? "已停止" :
                                (status === "completed_with_warnings" ? "完成（部分缺失）" : "完成（有失败）")))));
            summary.textContent = value(current, "Message", "message", "");
            meta.textContent = candidateLine(candidate) +
                (sourceEpisodeNumber ? "　来源第 " + sourceEpisodeNumber + " 集" : "") +
                "　成功 " + succeeded + " / 部分缺失 " + partial +
                " / 重复已跳过 " + skipped + " / 失败 " + failed;
            block.className = "danmuProgressSeason " +
                (status === "completed" ? "success" :
                    (status === "completed_with_warnings" ? "warning" :
                        (status === "cancelled" ? "cancelled" :
                            ((status === "queued" || status === "running" || status === "stopping") ? "running" : "failed"))));

            items.replaceChildren();
            (value(current, "Episodes", "episodes", []) || []).forEach(function (resultItem) {
                var resultStatus = value(resultItem, "Status", "status", "pending");
                var row = document.createElement("div");
                row.className = "danmuEpisodeProgress " + resultStatus;
                var number = document.createElement("span");
                number.textContent = item.Type === "Movie"
                    ? "电影"
                    : "第 " + (value(resultItem, "EpisodeNumber", "episodeNumber", "?") || "?") + " 集";
                var itemName = document.createElement("span");
                itemName.textContent = value(resultItem, "EpisodeName", "episodeName", item.Name);
                var resultText = document.createElement("span");
                var message = value(resultItem, "Message", "message", "");
                resultText.textContent = resultStatus === "success" ? "✓ 下载成功" :
                    (resultStatus === "running" ? "● 正在下载" :
                        (resultStatus === "queued" ? "● 等待重试" :
                            (resultStatus === "partial" ? "⚠ " + (message || "部分弹幕缺失") :
                                (resultStatus === "skipped" ? "↷ " + (message || "已跳过") :
                                    (resultStatus === "cancelled" ? "■ 已强制停止" :
                                        (resultStatus === "failed" ? "✕ " + (message || "下载失败") : "等待中"))))));
                var retry = document.createElement("button");
                retry.className = "danmuEpisodeRetry";
                retry.textContent = "重试";
                retry.title = item.Type === "Movie" ? "强制重新下载电影弹幕" : "强制重新下载本集弹幕";
                var resultItemId = value(resultItem, "ItemId", "itemId", "");
                retry.disabled = !taskId || !terminal(status) || !resultItemId;
                retry.addEventListener("click", async function () {
                    retry.disabled = true;
                    retry.textContent = "提交中…";
                    try {
                        task = await api(resultItemId, "RetryTrackedEpisode", { taskId: taskId });
                        update(task);
                        monitorTask();
                    } catch (error) {
                        retry.disabled = false;
                        retry.textContent = "重试";
                        notify((item.Type === "Movie" ? "电影" : "单集") + "重试提交失败：" + (error.message || error), true);
                    }
                });
                row.append(number, itemName, resultText, retry);
                items.appendChild(row);
            });

            if (terminal(status)) {
                background.style.display = "none";
                stop.style.display = "none";
                dialog.closable = true;
            } else {
                background.style.display = "";
                stop.style.display = "";
                dialog.closable = false;
            }
        }

        async function monitorTask() {
            if (monitoring || !taskId) return;
            monitoring = true;
            try {
                while (!terminal(value(task, "Status", "status", "failed")) && dialog.overlay.isConnected) {
                    await wait(1000);
                    if (!dialog.overlay.isConnected) break;
                    task = await api(item.Id, "GetDownloadProgress", { taskId: taskId });
                    update(task);
                }
            } finally {
                monitoring = false;
            }
        }

        stop.addEventListener("click", async function () {
            stop.disabled = true;
            stop.textContent = "正在停止…";
            try {
                var result = await api(item.Id, "StopAllTrackedDownloads");
                notify(value(result, "Message", "message", "已提交停止请求"), false);
                // The provider may ignore cancellation. Closing must never wait for it.
                dialog.closable = true;
                background.style.display = "none";
                stop.style.display = "none";
            } catch (error) {
                stop.disabled = false;
                stop.textContent = "强制停止全部下载";
                notify("停止下载失败：" + (error.message || error), true);
            }
        });

        update(task);
        monitorTask();
    }

    function renderInitialSearchFailure(dialog, item, status, error) {
        if (!dialog || !dialog.overlay || !dialog.overlay.isConnected) return;
        dialog.androidBackLocked = false;
        dialog.body.replaceChildren();
        dialog.footer.replaceChildren();
        var message = document.createElement("p");
        message.className = "danmuMuted";
        message.textContent = status === "cancelled"
            ? "已取消搜索，可在准备好后重试。"
            : "获取匹配候选失败：" + (error && (error.message || error) || "未知错误");
        var retry = document.createElement("button");
        retry.className = "danmuSmartButton primary";
        retry.textContent = "重试搜索";
        retry.addEventListener("click", function () { runSmartDownload(item, dialog); });
        dialog.body.appendChild(message);
        dialog.footer.appendChild(retry);
        if (status !== "cancelled") notify(message.textContent, true);
    }

    async function runSmartDownload(item, dialog) {
        var preview = await runDialogSearch(
            dialog, item.Id, "provider-search", {},
            item.Type === "Series" ? "正在逐季请求服务器匹配结果，请稍候…" : "正在请求服务器匹配结果，请稍候…",
            function (status, error) { renderInitialSearchFailure(dialog, item, status, error); });
        if (!preview) return;
        var seasons = value(preview, "Seasons", "seasons", []) || [];

        if (item.Type === "Series") {
            renderSeriesPicker(dialog, item, seasons, {}, {});
            return;
        }

        if (item.Type === "Movie" || item.Type === "Episode") {
            var target = value(preview, "Target", "target", null);
            if (!target) {
                renderInitialSearchFailure(dialog, item, "error",
                    new Error(value(preview, "Message", "message", "服务器没有返回媒体信息")));
                return;
            }
            renderItemCandidatePicker(dialog, item, target, "");
            return;
        }

        var season = seasons[0];
        if (!season) {
            renderInitialSearchFailure(dialog, item, "error",
                new Error(value(preview, "Message", "message", "服务器没有返回季度信息")));
            return;
        }
        renderCandidatePicker(dialog, item, season, "");
    }

    function findMenuInsertionAnchor(menu) {
        var selectors = [
            '[data-id="scan"]',
            '[data-id="scanmedialibraryfiles"]',
            '[data-id="refreshmetadata"]',
            '[data-id="identify"]',
            '[data-id="edit"]'
        ];
        for (var index = 0; index < selectors.length; index++) {
            var anchor = menu.querySelector(selectors[index]);
            if (anchor) return anchor;
        }
        return null;
    }

    function runButtonWorkflow(menu, item) {
        closeMenu(menu);
        var dialog = openDialog(item.Type === "Series" ? "整部剧弹幕智能匹配" :
            (item.Type === "Season" ? "本季弹幕智能匹配" :
                (item.Type === "Episode" ? "本集弹幕智能匹配" : "电影弹幕智能匹配")));
        runSmartDownload(item, dialog).catch(function (error) {
            dialog.close();
            console.error("[Danmu Smart Match] 执行失败", error);
            notify("智能匹配或下载提交失败：" + (error.message || error), true);
        });
    }

    var buttonWorkflow = runButtonWorkflow;

    function makeButton(menu, item) {
        var template = findMenuInsertionAnchor(menu) || menu.querySelector(".actionSheetMenuItem");
        if (!template) {
            return null;
        }
        var button = template.cloneNode(true);
        var label = actionLabel(item.Type);
        var icon = button.querySelector(".actionsheetMenuItemIcon");
        var text = button.querySelector(".actionSheetItemText");
        button.dataset.id = BUTTON_ID;
        button.dataset.action = "none";
        button.removeAttribute("data-index");
        button.setAttribute("aria-label", label);
        button.title = label;
        if (icon) icon.textContent = "download";
        if (text) text.textContent = label;

        button.addEventListener("click", function (event) {
            event.preventDefault();
            event.stopPropagation();
            event.stopImmediatePropagation();
            if (button.dataset.running === "1") return;
            button.dataset.running = "1";
            buttonWorkflow(menu, item);
        }, true);
        return button;
    }

    async function injectButton() {
        window.clearTimeout(retryTimer);
        var context = pendingContext;
        if (!context || Date.now() > context.expires) return;
        var menus = Array.from(document.querySelectorAll(".actionSheet.opened"));
        var menu = context.id
            ? menus.find(function (candidate) { return getMenuItemId(candidate) === context.id; })
            : (menus.length === 1 ? menus[0] : null);
        if (!menu && context.id && menus.length === 1 && !getMenuItemId(menus[0])) menu = menus[0];
        if (!menu || menu.querySelector('[data-id="' + BUTTON_ID + '"]')) return;
        if (menu.dataset.danmuBulkResolving === "1") return;
        menu.dataset.danmuBulkResolving = "1";
        try {
            var menuItemId = getMenuItemId(menu);
            var resolvedItemId = resolveMenuContextId(context.id, menuItemId);
            if (!resolvedItemId) {
                menu.dataset.danmuBulkResolving = "done";
                return;
            }
            var item = await ApiClient.getItem(ApiClient.getCurrentUserId(), resolvedItemId);
            if (pendingContext !== context || context.generation !== contextGeneration) return;
            if (!item || !isSupportedItemType(item.Type)) {
                menu.dataset.danmuBulkResolving = "done";
                return;
            }
            if (!menu.isConnected || !menu.classList.contains("opened")) return;
            var menuItemId = getMenuItemId(menu);
            if (menuItemId && menuItemId !== resolvedItemId) return;
            var button = makeButton(menu, item);
            if (!button) return;
            var insertionAnchor = findMenuInsertionAnchor(menu);
            if (insertionAnchor) insertionAnchor.before(button);
            else (menu.querySelector(".actionsheetScrollSlider") || menu).appendChild(button);
            menu.dataset.danmuBulkResolving = "done";
        } catch (error) {
            delete menu.dataset.danmuBulkResolving;
            console.error("[Danmu Smart Match] 无法读取当前媒体信息", error);
        }
    }

    function scheduleInjection() {
        window.clearTimeout(retryTimer);
        retryTimer = window.setTimeout(injectButton, 30);
    }

    document.addEventListener("click", function (event) {
        var moreButton = event.target.closest(
            ".btnMoreCommands,[data-action='menu'],.cardOverlayButton-br,.listItemButton[data-action='menu']");
        if (!moreButton) return;
        var isDetailButton = Boolean(moreButton.closest(".page:not(.hide) .mainDetailButtons"));
        var id = isDetailButton ? getCurrentItemId() : getTriggerItemId(moreButton);
        if (!id && isDetailButton) return;
        setPendingContext(id);
        scheduleInjection();
    }, true);

    function captureLongPressContext(event) {
        var id = getGestureItemId(event.target);
        if (id) setPendingContext(id);
    }

    // Android Emby/CustomJSS long-press paths do not necessarily emit a click on
    // the more button. Capture the card before Emby creates its action sheet.
    document.addEventListener("pointerdown", captureLongPressContext, true);
    document.addEventListener("touchstart", captureLongPressContext, true);
    document.addEventListener("contextmenu", captureLongPressContext, true);

    new MutationObserver(function () {
        var menus = Array.from(document.querySelectorAll(".actionSheet.opened"));
        if (menus.length === 1) {
            var menuItemId = getMenuItemId(menus[0]);
            var contextId = openedActionSheetContextId(
                pendingContext, menuItemId, menus.length, Date.now());
            if (contextId && (!pendingContext || pendingContext.id !== contextId ||
                Date.now() > pendingContext.expires)) {
                setPendingContext(contextId);
            }
        }
        if (pendingContext && Date.now() <= pendingContext.expires) scheduleInjection();
    }).observe(document.body, { childList: true, subtree: true });

    window.__embyDanmuSmartMatchTest = {
        plausibleItemId: plausibleItemId,
        getTriggerItemId: getTriggerItemId,
        manualSearchDefault: manualSearchDefault,
        isSupportedItemType: isSupportedItemType,
        actionLabel: actionLabel,
        findMenuInsertionAnchor: findMenuInsertionAnchor,
        getGestureItemId: getGestureItemId,
        openedActionSheetContextId: openedActionSheetContextId,
        setPendingContext: setPendingContext,
        resolveMenuContextId: resolveMenuContextId,
        rematchParameters: rematchParameters,
        initializeKeywordIntent: initializeKeywordIntent,
        keywordRematchParameters: keywordRematchParameters,
        normalizeDecisionCode: normalizeDecisionCode,
        matchOriginLabel: matchOriginLabel,
        decisionReasonLabel: decisionReasonLabel,
        backendDecisionLine: backendDecisionLine,
        hasBackendMatch: hasBackendMatch,
        hasCompositePlan: hasCompositePlan,
        compositeVirtualGroups: compositeVirtualGroups,
        compositeRequestSelections: compositeRequestSelections,
        compositeHasDownloadableMappings: compositeHasDownloadableMappings,
        removeCompositeSelection: removeCompositeSelection,
        compositeExcludedItemIds: compositeExcludedItemIds,
        compositeDraftSeasonState: compositeDraftSeasonState,
        excludeCompositeRun: excludeCompositeRun,
        restoreCompositeRun: restoreCompositeRun,
        selectionLocalEpisodeItemIds: selectionLocalEpisodeItemIds,
        filterCompositeSelectionsByItemIds: filterCompositeSelectionsByItemIds,
        compositePlanCoversItemIds: compositePlanCoversItemIds,
        compositeDraftParameters: compositeDraftParameters,
        temporaryRangeKeyword: temporaryRangeKeyword,
        temporaryRangeSearchParameters: temporaryRangeSearchParameters,
        runDialogSearch: runDialogSearch,
        cancelDialogSearch: cancelDialogSearch,
        isCurrentSearch: isCurrentSearch,
        runSmartDownload: runSmartDownload,
        openDialog: openDialog,
        setBusy: setBusy,
        activeDialogCount: function () { return activeDialogs.length; },
        injectButton: injectButton,
        renderSeriesPicker: renderSeriesPicker,
        renderSeriesSeasonPicker: renderSeriesSeasonPicker,
        renderCompositeSeasonSummary: renderCompositeSeasonSummary,
        renderCompositeGroupPicker: renderCompositeGroupPicker,
        renderItemCandidatePicker: renderItemCandidatePicker,
        resolveSelectedCandidateDetail: resolveSelectedCandidateDetail,
        renderEpisodeSourcePicker: renderEpisodeSourcePicker,
        renderSingleTargetProgress: renderSingleTargetProgress,
        setButtonWorkflow: function (workflow) {
            buttonWorkflow = workflow || runButtonWorkflow;
        }
    };
    console.info("[Danmu Smart Match] 电视剧/季/集/电影智能匹配菜单已启用");
}());
