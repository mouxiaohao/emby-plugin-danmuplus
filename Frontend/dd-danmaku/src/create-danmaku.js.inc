    let danmakuCreationSequence = 0;

    async function createDanmaku(comments) {
        if (!comments) { return; }
        const creationSequence = ++danmakuCreationSequence;
        const currentDanmaku = window.ede.danmaku;
        const commentsParsed = danmakuParser(comments);
        const filteredComments = danmakuFilter(commentsParsed);
        console.log('[加载]弹幕成功: ' + filteredComments.length);

        const _media = document.querySelector(mediaQueryStr);
        if (!_media) {
            // This path is used only by quickDebug outside active playback.
            if (!currentDanmaku) {
                window.ede.danmaku = { comments: filteredComments, };
                window.ede.commentsOriginal = comments;
                window.ede.commentsParsed = commentsParsed;
            }
            buildCurrentDanmakuInfo(currentDanmakuInfoContainerId);
            throw new Error('用户已退出视频播放');
        }
        if (!isVersionOld) { _media.style.position = 'absolute'; }
        const wrapperTop = 0;
        const wrapper = document.createElement('div');
        wrapper.id = eleIds.danmakuWrapper + '-pending-' + creationSequence;
        wrapper.style.position = 'fixed';
        wrapper.style.width = '100%';
        wrapper.style.height = `calc(${lsGetItem(lsKeys.heightPercent.id)}% - ${wrapperTop}px)`;
        wrapper.style.backgroundColor = lsGetItem(lsKeys.debugShowDanmakuWrapper.id) ? styles.colors.highlight : '';
        wrapper.style.top = wrapperTop + 'px';
        wrapper.style.pointerEvents = 'none';
        const _container = await waitForElement(mediaContainerQueryStr);
        _container.prepend(wrapper);

        const requestedFixedSpeed = lsGetItem(lsKeys.fixedSpeed.id) === true;
        let nextDanmaku = null;
        let nextObserver = null;
        let DanmakuConstructor = null;
        let effectiveFixedSpeed = false;
        try {
            DanmakuConstructor = await resolveDanmakuConstructor();
            effectiveFixedSpeed = requestedFixedSpeed && hasFixedSpeedCapability(DanmakuConstructor);
            const _speed = 144 * lsGetItem(lsKeys.speed.id);
            nextDanmaku = new DanmakuConstructor({
                container: wrapper,
                media: _media,
                comments: filteredComments,
                engine: lsGetItem(lsKeys.engine.id),
                speed: _speed,
                fixedSpeed: effectiveFixedSpeed,
            });
            lsGetItem(lsKeys.switch.id) ? nextDanmaku.show() : nextDanmaku.hide();
            nextObserver = new ResizeObserver(() => {
                if (window.ede.danmaku === nextDanmaku) {
                    console.log('Resizing');
                    nextDanmaku.resize();
                    if (lsGetItem(lsKeys.osdLineChartEnable.id)) {
                        buildProgressBarChart(20);
                    }
                }
            });
            nextObserver.observe(_container);
        } catch (error) {
            if (nextObserver) { nextObserver.disconnect(); }
            if (nextDanmaku && typeof nextDanmaku.destroy === 'function') {
                try { nextDanmaku.destroy(); } catch (destroyError) { console.warn(destroyError); }
            }
            wrapper.remove();
            throw error;
        }

        // Resolve the active runtime only at commit time. Concurrent reloads may
        // have committed while this candidate was awaiting its engine module.
        const committedDanmaku = window.ede.danmaku;
        const committedObserver = window.ede.ob;
        const committedWrapper = getById(eleIds.danmakuWrapper);
        if (committedObserver) { committedObserver.disconnect(); }
        if (committedDanmaku && typeof committedDanmaku.destroy === 'function') {
            try { committedDanmaku.destroy(); } catch (destroyError) { console.warn(destroyError); }
        }
        if (committedWrapper && committedWrapper !== wrapper) { committedWrapper.remove(); }
        wrapper.id = eleIds.danmakuWrapper;
        window.ede.commentsOriginal = comments;
        window.ede.commentsParsed = commentsParsed;
        window.ede.danmaku = nextDanmaku;
        window.ede.ob = nextObserver;
        try {
            setFixedSpeedDiagnostic(
                requestedFixedSpeed,
                effectiveFixedSpeed,
                DanmakuConstructor,
                requestedFixedSpeed && !effectiveFixedSpeed ? 'engine-capability-missing' : ''
            );
        } catch (error) {
            console.warn('[弹幕速度] 实例已切换，但状态提示刷新失败', error);
        }

        // The hidden Emby adapter can be paused independently of its HTML media flag.
        if (_media.id) {
            try {
                require(['playbackManager'], (playbackManager) => {
                    try {
                        if (playbackManager.getCurrentPlayer()
                            && playbackManager.getPlayerState().PlayState.IsPaused
                        ) {
                            _media.dispatchEvent(new Event('pause'));
                        }
                    } catch (error) {
                        console.warn('[弹幕速度] 隐藏媒体暂停状态同步失败', error);
                    }
                });
            } catch (error) {
                console.warn('[弹幕速度] playbackManager 加载失败', error);
            }
        }
        try {
            buildCurrentDanmakuInfo(currentDanmakuInfoContainerId);
            appendvideoOsdDanmakuInfo(filteredComments.length);
            if (lsGetItem(lsKeys.osdLineChartEnable.id)) {
                buildProgressBarChart(20);
            }
        } catch (error) {
            console.warn('[弹幕速度] 实例已切换，但弹幕状态面板刷新失败', error);
        }
    }
