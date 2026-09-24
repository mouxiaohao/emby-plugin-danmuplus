    // ------ require start ------
    /* The generated standalone script always carries this controlled build. */
/*__CONTROLLED_DANMAKU_ENGINE__*/

    const controlledDanmakuConstructor = window.Danmaku;
    const bundledDefaultDanmakuPath = requireDanmakuPath;
    let customDanmakuPath = null;
    let customDanmakuPromise = null;

    function normalizeDanmakuConstructor(moduleValue) {
        if (typeof moduleValue === 'function') { return moduleValue; }
        if (moduleValue && typeof moduleValue.default === 'function') { return moduleValue.default; }
        return null;
    }

    function hasFixedSpeedCapability(DanmakuConstructor) {
        const capability = DanmakuConstructor && DanmakuConstructor.DD_DANMAKU_CAPABILITY;
        return !!(capability
            && capability.id === 'dd-danmaku-wall-clock-v1'
            && capability.engineVersion === '2.0.8'
            && capability.option === 'fixedSpeed');
    }

    function updateFixedSpeedSettingStatus() {
        const status = getById(eleIds.danmakuFixedSpeedStatus);
        if (!status) { return; }
        const diagnostic = window.ede && window.ede.fixedSpeedDiagnostic
            ? window.ede.fixedSpeedDiagnostic
            : window.__ddDanmakuFixedSpeedDiagnostic;
        const inactive = diagnostic && diagnostic.requested && !diagnostic.effective;
        status.style.display = inactive ? '' : 'none';
        status.textContent = inactive
            ? '当前自定义弹幕引擎不支持此模式，已保留设置并按原滚动方式运行。'
            : '';
    }

    function setFixedSpeedDiagnostic(requested, effective, DanmakuConstructor, reason) {
        const capability = DanmakuConstructor && DanmakuConstructor.DD_DANMAKU_CAPABILITY;
        const diagnostic = {
            requested: requested === true,
            effective: effective === true,
            capability: capability && capability.id ? capability.id : null,
            reason: reason || '',
        };
        window.__ddDanmakuFixedSpeedDiagnostic = diagnostic;
        if (window.ede) { window.ede.fixedSpeedDiagnostic = diagnostic; }
        if (diagnostic.requested && !diagnostic.effective) {
            console.warn('[弹幕速度] 已请求保持滚动速度，但当前引擎不兼容，已按原模式运行', diagnostic);
        }
        updateFixedSpeedSettingStatus();
    }

    async function resolveDanmakuConstructor() {
        const requestedPath = requireDanmakuPath;
        const hasExplicitCustomPath = !!requestedPath && requestedPath !== bundledDefaultDanmakuPath;
        if (!hasExplicitCustomPath) { return controlledDanmakuConstructor; }
        if (!customDanmakuPromise || customDanmakuPath !== requestedPath) {
            customDanmakuPath = requestedPath;
            customDanmakuPromise = Emby.importModule(requestedPath).then(moduleValue => {
                const constructor = normalizeDanmakuConstructor(moduleValue);
                if (!constructor) { throw new Error('自定义弹幕引擎没有导出构造函数'); }
                return constructor;
            }).catch(error => {
                customDanmakuPromise = null;
                throw error;
            });
        }
        return customDanmakuPromise;
    }
