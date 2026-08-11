/*
 * Emby.CustomCssJS: 电视剧/季/集/电影智能匹配并一键下载弹幕
 * 适用于 Emby 4.9.x + 本方案配套的 Emby.Plugin.Danmu DLL
 */
(function () {
    "use strict";

    var INSTALL_FLAG = "__embyDanmuSmartMenuV9";
    var BUTTON_ID = "danmu-bulk-download";

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

    function api(itemId, option, parameters) {
        var query = Object.assign({ option: option }, parameters || {});
        return ApiClient.ajax({
            url: ApiClient.getUrl("plugin/danmu/" + encodeURIComponent(itemId), query),
            type: "GET",
            dataType: "json",
            timeout: 180000
        }).then(asJson);
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
            ".danmuProgressSeason{padding:.9rem;margin:.7rem 0;border:1px solid rgba(255,255,255,.14);border-radius:.45rem;background:rgba(255,255,255,.025)}",
            ".danmuProgressSeason.running{border-color:#00a4dc}.danmuProgressSeason.success{border-color:#2e7d32}.danmuProgressSeason.warning{border-color:#ffb300}.danmuProgressSeason.failed{border-color:#c62828}.danmuProgressSeason.cancelled{border-color:#777}",
            ".danmuProgressTitle{display:flex;justify-content:space-between;gap:1rem;font-weight:600}.danmuProgressMeta{opacity:.8;font-size:.9rem;margin:.3rem 0 .55rem}",
            ".danmuEpisodeProgress{display:grid;grid-template-columns:minmax(4.5rem,auto) minmax(8rem,1fr) minmax(6rem,1.3fr) auto;gap:.55rem;align-items:center;padding:.36rem .2rem;border-top:1px solid rgba(255,255,255,.08);font-size:.9rem}",
            ".danmuEpisodeProgress.running{color:#8dd7f2}.danmuEpisodeProgress.success{color:#81c784}.danmuEpisodeProgress.partial{color:#ffd54f}.danmuEpisodeProgress.skipped{color:#ffb74d}.danmuEpisodeProgress.failed{color:#ef9a9a}.danmuEpisodeProgress.cancelled{color:#aaa}.danmuEpisodeProgress.pending,.danmuEpisodeProgress.queued{opacity:.55}",
            ".danmuEpisodeRetry{border:1px solid rgba(255,255,255,.28);border-radius:.3rem;background:#444;color:#fff;padding:.28rem .55rem;cursor:pointer;white-space:nowrap}.danmuEpisodeRetry:hover{background:#666}.danmuEpisodeRetry:disabled{opacity:.45;cursor:default}",
            ".danmuProgressSummary{position:sticky;top:-1rem;z-index:2;margin:-1rem -1.2rem .8rem;padding:.8rem 1.2rem;background:#202020;border-bottom:1px solid rgba(255,255,255,.14)}",
            ".danmuMuted{opacity:.72}.danmuBusy{text-align:center;padding:2.2rem 1rem;font-size:1.05rem}",
            "@media(max-width:520px){.danmuSmartOverlay{padding:0}.danmuSmartCard{height:100%;max-height:none;border-radius:0}.danmuSmartSearch{flex-wrap:wrap}.danmuSmartSearch input{flex-basis:100%}.danmuEpisodeProgress{grid-template-columns:auto 1fr auto}.danmuEpisodeProgress>span:nth-child(3){grid-column:1/3}}"
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
        var dialog = {
            overlay: overlay,
            title: heading,
            body: body,
            footer: footer,
            closable: true,
            forceRefresh: false,
            close: function () {
                if (dialog.closable) overlay.remove();
            },
            forceClose: function () { overlay.remove(); }
        };
        close.addEventListener("click", dialog.close);
        overlay.addEventListener("click", function (event) {
            if (event.target === overlay) {
                dialog.close();
            }
        });
        return dialog;
    }

    function setBusy(dialog, message) {
        dialog.body.replaceChildren();
        dialog.footer.replaceChildren();
        var busy = document.createElement("div");
        busy.className = "danmuBusy";
        busy.textContent = message;
        dialog.body.appendChild(busy);
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
        return [
            value(season, "SeriesId", "seriesId", ""),
            value(season, "SeasonId", "seasonId", ""),
            value(season, "SeasonNumber", "seasonNumber", ""),
            value(season, "Year", "year", ""),
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

    function candidateLine(candidate) {
        var site = value(candidate, "SiteName", "siteName", value(candidate, "Site", "site", "未知网站"));
        var name = value(candidate, "Name", "name", "未命名项目");
        var year = value(candidate, "Year", "year", null);
        var episodes = value(candidate, "EpisodeSize", "episodeSize", 0);
        var score = Math.round(Number(value(candidate, "Score", "score", 0)) * 100);
        return site + "｜" + name + "｜" + (year || "年份未知") + "｜" + (episodes > 0 ? episodes + " 集" : "集数未知") + "｜评分 " + score;
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
            Name: "当前已绑定的项目",
            Reason: "当前选择"
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
            meta.textContent = candidate ? candidateLine(candidate) : "尚未选择";
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
        var close = document.createElement("button");
        close.className = "danmuSmartButton primary";
        close.textContent = "关闭";
        close.style.display = "none";
        close.addEventListener("click", dialog.close);
        dialog.footer.append(background, stop, close);

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
                close.style.display = "";
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
                taskParameters.site = value(candidate, "Site", "site", "");
                taskParameters.candidateId = value(candidate, "Id", "id", "");
                taskParameters.manual = (seasonWasManuallyBound(season) || selectionWasChanged(season, candidate)) ? "true" : "false";
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
            close.style.display = "none";
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
            close.style.display = "";
            dialog.closable = true;
        }

        await monitorTasks();
    }

    function renderSeriesPicker(dialog, item, seasons, selections, keywords) {
        selections = selections || {};
        keywords = keywords || {};
        if (dialog.title) dialog.title.textContent = "整部剧弹幕智能匹配";
        dialog.body.replaceChildren();
        dialog.footer.replaceChildren();

        seasons.forEach(function (season) {
            var key = seasonSelectionKey(season);
            if (!selections[key]) {
                var current = selectedCandidate(season);
                if (current) selections[key] = current;
            }
        });
        var message = document.createElement("p");
        message.textContent = "下面只显示每季的最终匹配状态。需要修改时点击对应季度的“手动匹配”，再进入候选选择页面。匹配失败的季度不会阻止其他季度下载。";
        dialog.body.appendChild(message);

        seasons.forEach(function (season, seasonIndex) {
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
            var state = document.createElement("div");
            state.className = "danmuSeasonSummaryState";
            state.textContent = selection ? "✓ 匹配成功" : "✕ 匹配失败";
            var detail = document.createElement("div");
            detail.className = "danmuSeasonSummaryDetail";
            detail.textContent = selection ? candidateLine(selection) :
                value(season, "Message", "message", "未找到可信匹配结果");
            main.append(title, state, detail);
            var manual = document.createElement("button");
            manual.className = "danmuSmartButton";
            manual.textContent = "手动匹配";
            manual.addEventListener("click", function () {
                renderSeriesSeasonPicker(dialog, item, seasons, seasonIndex, selections, keywords);
            });
            block.append(main, manual);
            dialog.body.appendChild(block);
        });

        appendForceRefreshOption(dialog);

        var cancel = document.createElement("button");
        cancel.className = "danmuSmartButton";
        cancel.textContent = "取消";
        cancel.addEventListener("click", dialog.close);
        var ok = document.createElement("button");
        ok.className = "danmuSmartButton primary";
        var matchedSeasons = seasons.filter(function (season) {
            return Boolean(selections[seasonSelectionKey(season)]);
        });
        ok.textContent = matchedSeasons.length === seasons.length ? "下载全部匹配季度" :
            "下载已匹配季度（" + matchedSeasons.length + "/" + seasons.length + "）";
        ok.disabled = matchedSeasons.length === 0;
        ok.addEventListener("click", function () {
            submitSeriesSelections(dialog, matchedSeasons, selections);
        });
        dialog.footer.append(cancel, ok);
    }

    function renderSeriesSeasonPicker(dialog, item, seasons, seasonIndex, selections, keywords) {
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
            value(season, "EpisodeCount", "episodeCount", 0) + " 集。";
        dialog.body.appendChild(summary);

        var search = document.createElement("div");
        search.className = "danmuSmartSearch";
        var input = document.createElement("input");
        input.type = "search";
        input.placeholder = "输入本季关键词重新搜索";
        input.value = Object.prototype.hasOwnProperty.call(keywords, selectionKey)
            ? keywords[selectionKey]
            : value(season, "SeriesName", "seriesName", item.SeriesName || item.Name || "");
        var searchButton = document.createElement("button");
        searchButton.className = "danmuSmartButton";
        searchButton.textContent = "重新搜索";
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
                "　类型：" + (value(candidate, "Category", "category", "未知") || "未知") +
                "　评分：" + Math.round(Number(value(candidate, "Score", "score", 0)) * 100);
            var reason = document.createElement("div");
            reason.className = "danmuCandidateReason";
            reason.textContent = value(candidate, "Reason", "reason", "需要人工确认");
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
            setBusy(dialog, "正在使用新关键词搜索本季候选…");
            try {
                var parameters = seasonRequestParameters(season);
                parameters.keyword = keyword;
                parameters.force = "true";
                var searchItemId = parameters.seriesId || item.Id;
                var refreshed = await api(searchItemId, "MatchPreview", parameters);
                var refreshedSeason = (value(refreshed, "Seasons", "seasons", []) || [])[0];
                if (!refreshedSeason) throw new Error("服务器没有返回本季候选");
                var oldKey = selectionKey;
                seasons[seasonIndex] = refreshedSeason;
                var newKey = seasonSelectionKey(refreshedSeason);
                if (newKey !== oldKey && selections[oldKey] && !selections[newKey]) {
                    selections[newKey] = selections[oldKey];
                    delete selections[oldKey];
                }
                keywords[newKey] = keyword;
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
        dialog.body.replaceChildren();
        dialog.footer.replaceChildren();

        var summary = document.createElement("p");
        summary.textContent = "库内信息：" + value(season, "SeriesName", "seriesName", "") + " / " + value(season, "SeasonName", "seasonName", "") + "，" + (value(season, "Year", "year", null) || "年份未知") + "，" + value(season, "EpisodeCount", "episodeCount", 0) + " 集。请选择正确项目，绑定会被保存供以后使用。";
        dialog.body.appendChild(summary);

        var search = document.createElement("div");
        search.className = "danmuSmartSearch";
        var input = document.createElement("input");
        input.type = "search";
        input.placeholder = "换一个关键词重新搜索，例如：唐诡奇潭";
        input.value = keyword || value(season, "SeriesName", "seriesName", item.SeriesName || item.Name || "");
        var searchButton = document.createElement("button");
        searchButton.className = "danmuSmartButton";
        searchButton.textContent = "重新搜索";
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
            if ((currentCandidate &&
                value(currentCandidate, "Id", "id", "") === value(candidate, "Id", "id", "") &&
                value(currentCandidate, "Site", "site", "") === value(candidate, "Site", "site", "")) ||
                (!currentCandidate && index === 0)) {
                radio.checked = true;
            }
            var main = document.createElement("div");
            main.className = "danmuCandidateMain";
            var title = document.createElement("div");
            title.className = "danmuCandidateTitle";
            title.textContent = value(candidate, "SiteName", "siteName", value(candidate, "Site", "site", "未知网站")) + " · " + value(candidate, "Name", "name", "未命名项目");
            var meta = document.createElement("div");
            meta.className = "danmuCandidateMeta";
            meta.textContent = "年份：" + (value(candidate, "Year", "year", null) || "未知") + "　集数：" + (value(candidate, "EpisodeSize", "episodeSize", 0) || "未知") + "　类型：" + (value(candidate, "Category", "category", "未知") || "未知") + "　综合评分：" + Math.round(Number(value(candidate, "Score", "score", 0)) * 100);
            var reason = document.createElement("div");
            reason.className = "danmuCandidateReason";
            reason.textContent = value(candidate, "Reason", "reason", "需要人工确认");
            main.append(title, meta, reason);
            row.append(radio, main);
            list.appendChild(row);
        });
        dialog.body.appendChild(list);

        appendForceRefreshOption(dialog);

        var cancel = document.createElement("button");
        cancel.className = "danmuSmartButton";
        cancel.textContent = "取消";
        cancel.addEventListener("click", dialog.close);
        var bind = document.createElement("button");
        bind.className = "danmuSmartButton primary";
        bind.textContent = "绑定并下载";
        bind.disabled = !candidates.length;
        dialog.footer.append(cancel, bind);

        searchButton.addEventListener("click", async function () {
            var newKeyword = input.value.trim();
            if (!newKeyword) {
                notify("请输入搜索关键词。", true);
                return;
            }
            setBusy(dialog, "正在使用新关键词搜索所有已启用网站…");
            try {
                var refreshed = await api(item.Id, "MatchPreview", { keyword: newKeyword, force: "true" });
                var refreshedSeason = (value(refreshed, "Seasons", "seasons", []) || [])[0];
                renderCandidatePicker(dialog, item, refreshedSeason || season, newKeyword);
            } catch (error) {
                renderCandidatePicker(dialog, item, season, newKeyword);
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

    function renderItemCandidatePicker(dialog, item, target, keyword) {
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
                "。请选择季度候选并确认要使用的来源集数。";
        } else {
            summary.textContent = "库内信息：" + value(target, "ItemName", "itemName", item.Name || "未命名电影") +
                "，" + (value(target, "Year", "year", null) || "年份未知") + "。请选择正确电影。";
        }
        dialog.body.appendChild(summary);

        var search = document.createElement("div");
        search.className = "danmuSmartSearch";
        var input = document.createElement("input");
        input.type = "search";
        input.placeholder = "输入关键词重新搜索";
        input.value = keyword || manualSearchDefault(item, target);
        var searchButton = document.createElement("button");
        searchButton.className = "danmuSmartButton";
        searchButton.textContent = "重新搜索";
        search.append(input, searchButton);
        dialog.body.appendChild(search);

        var list = document.createElement("div");
        if (!candidates.length) {
            var empty = document.createElement("p");
            empty.className = "danmuMuted";
            empty.textContent = value(target, "Message", "message", "没有候选结果，请更换关键词重试。");
            list.appendChild(empty);
        }
        function refreshEpisodeInputs() {
            Array.from(list.querySelectorAll(".danmuSourceEpisode")).forEach(function (control) {
                var radio = control.parentElement.querySelector('input[type="radio"]');
                control.classList.toggle("active", Boolean(radio && radio.checked));
            });
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
                value(selected, "Site", "site", "") === value(candidate, "Site", "site", "")) ||
                (!selected && index === 0);
            var main = document.createElement("div");
            main.className = "danmuCandidateMain";
            var title = document.createElement("div");
            title.className = "danmuCandidateTitle";
            title.textContent = value(candidate, "SiteName", "siteName", value(candidate, "Site", "site", "未知网站")) +
                " · " + value(candidate, "Name", "name", "未命名项目");
            var suggested = value(candidate, "SuggestedEpisodeNumber", "suggestedEpisodeNumber", null);
            var meta = document.createElement("div");
            meta.className = "danmuCandidateMeta";
            meta.textContent = "年份：" + (value(candidate, "Year", "year", null) || "未知") +
                "　集数：" + (value(candidate, "EpisodeSize", "episodeSize", 0) || "未知") +
                (isEpisode ? "　候选源集数：第 " + (suggested || "?") + " 集" : "") +
                "　类型：" + (value(candidate, "Category", "category", "未知") || "未知") +
                "　评分：" + Math.round(Number(value(candidate, "Score", "score", 0)) * 100);
            var reason = document.createElement("div");
            reason.className = "danmuCandidateReason";
            reason.textContent = value(candidate, "Reason", "reason", "需要人工确认");
            main.append(title, meta, reason);
            row.append(radio, main);
            if (isEpisode) {
                var sourceControl = document.createElement("span");
                sourceControl.className = "danmuSourceEpisode";
                var sourceLabel = document.createElement("span");
                sourceLabel.textContent = "来源集数";
                var sourceInput = document.createElement("input");
                sourceInput.type = "number";
                sourceInput.min = "1";
                sourceInput.step = "1";
                sourceInput.value = suggested || "";
                sourceInput.dataset.candidateIndex = String(index);
                sourceInput.addEventListener("click", function (event) { event.stopPropagation(); });
                sourceControl.append(sourceLabel, sourceInput);
                row.appendChild(sourceControl);
            }
            radio.addEventListener("change", refreshEpisodeInputs);
            list.appendChild(row);
        });
        dialog.body.appendChild(list);
        refreshEpisodeInputs();
        appendForceRefreshOption(dialog);

        searchButton.addEventListener("click", async function () {
            var newKeyword = input.value.trim();
            if (!newKeyword) {
                notify("请输入搜索关键词。", true);
                return;
            }
            setBusy(dialog, "正在使用新关键词搜索所有已启用网站…");
            try {
                var refreshed = await api(item.Id, "MatchPreview", { keyword: newKeyword, force: "true" });
                var refreshedTarget = value(refreshed, "Target", "target", null);
                if (!refreshedTarget) throw new Error("服务器没有返回媒体候选");
                renderItemCandidatePicker(dialog, item, refreshedTarget, newKeyword);
            } catch (error) {
                renderItemCandidatePicker(dialog, item, target, newKeyword);
                notify("重新搜索失败：" + (error.message || error), true);
            }
        });
        input.addEventListener("keydown", function (event) {
            if (event.key === "Enter") {
                event.preventDefault();
                searchButton.click();
            }
        });

        var cancel = document.createElement("button");
        cancel.className = "danmuSmartButton";
        cancel.textContent = "取消";
        cancel.addEventListener("click", dialog.close);
        var start = document.createElement("button");
        start.className = "danmuSmartButton primary";
        start.textContent = isEpisode ? "下载本集弹幕" : "绑定并下载电影弹幕";
        start.disabled = !candidates.length;
        start.addEventListener("click", function () {
            var checked = list.querySelector('input[name="danmuItemCandidateChoice"]:checked');
            if (!checked) {
                notify("请选择一个候选结果。", true);
                return;
            }
            var candidateIndex = Number(checked.value);
            var candidate = candidates[candidateIndex];
            var sourceEpisodeNumber = null;
            if (isEpisode) {
                var sourceInput = list.querySelector('.danmuSourceEpisode input[data-candidate-index="' + candidateIndex + '"]');
                sourceEpisodeNumber = Number(sourceInput && sourceInput.value);
                if (!Number.isInteger(sourceEpisodeNumber) || sourceEpisodeNumber <= 0) {
                    notify("来源集数必须是正整数。", true);
                    return;
                }
                var available = Number(value(candidate, "EpisodeSize", "episodeSize", 0));
                if (available > 0 && sourceEpisodeNumber > available) {
                    notify("该候选不存在第 " + sourceEpisodeNumber + " 集。", true);
                    return;
                }
            }
            var manual = !selected ||
                value(selected, "Id", "id", "") !== value(candidate, "Id", "id", "") ||
                value(selected, "Site", "site", "") !== value(candidate, "Site", "site", "");
            renderSingleTargetProgress(dialog, item, target, candidate, sourceEpisodeNumber, manual);
        });
        dialog.footer.append(cancel, start);
    }

    async function renderSingleTargetProgress(dialog, item, target, candidate, sourceEpisodeNumber, manual) {
        dialog.closable = false;
        setBusy(dialog, "正在提交下载任务…");
        var parameters = {
            site: value(candidate, "Site", "site", ""),
            candidateId: value(candidate, "Id", "id", ""),
            manual: manual ? "true" : "false",
            forceRefresh: dialog.forceRefresh ? "true" : "false"
        };
        if (sourceEpisodeNumber) parameters.sourceEpisodeNumber = sourceEpisodeNumber;
        var task = await api(item.Id, "StartTrackedDownload", parameters);
        var taskId = value(task, "TaskId", "taskId", "");
        var monitoring = false;

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
        var close = document.createElement("button");
        close.className = "danmuSmartButton primary";
        close.textContent = "关闭";
        close.style.display = "none";
        close.addEventListener("click", dialog.close);
        dialog.footer.append(background, stop, close);

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
                close.style.display = "";
                dialog.closable = true;
            } else {
                background.style.display = "";
                stop.style.display = "";
                close.style.display = "none";
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
                close.style.display = "";
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

    async function runSmartDownload(item, dialog) {
        setBusy(dialog, item.Type === "Series" ? "正在逐季搜索并综合评分，请稍候…" : "正在搜索所有已启用网站并综合评分，请稍候…");
        var preview = await api(item.Id, "MatchPreview");
        var seasons = value(preview, "Seasons", "seasons", []) || [];

        if (item.Type === "Series") {
            renderSeriesPicker(dialog, item, seasons, {}, {});
            return;
        }

        if (item.Type === "Movie" || item.Type === "Episode") {
            var target = value(preview, "Target", "target", null);
            if (!target) throw new Error(value(preview, "Message", "message", "服务器没有返回媒体信息"));
            renderItemCandidatePicker(dialog, item, target, "");
            return;
        }

        var season = seasons[0];
        if (!season) {
            throw new Error(value(preview, "Message", "message", "服务器没有返回季度信息"));
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
        injectButton: injectButton,
        renderSingleTargetProgress: renderSingleTargetProgress,
        setButtonWorkflow: function (workflow) {
            buttonWorkflow = workflow || runButtonWorkflow;
        }
    };
    console.info("[Danmu Smart Match] 电视剧/季/集/电影智能匹配菜单已启用");
}());

