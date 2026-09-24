        const fixedSpeedControl = embyCheckbox(
            { id: lsKeys.fixedSpeed.id, label: lsKeys.fixedSpeed.name },
            lsGetItem(lsKeys.fixedSpeed.id),
            async (checked) => {
                const previousValue = lsGetItem(lsKeys.fixedSpeed.id);
                const previousDiagnostic = window.ede && window.ede.fixedSpeedDiagnostic
                    ? window.ede.fixedSpeedDiagnostic
                    : window.__ddDanmakuFixedSpeedDiagnostic;
                if (previousValue === checked) { return; }
                if (!lsCheckSet(lsKeys.fixedSpeed.id, checked)) { return; }
                try {
                    if (window.ede && window.ede.commentsOriginal
                        && document.querySelector(mediaQueryStr)) {
                        await createDanmaku(window.ede.commentsOriginal);
                    }
                    updateFixedSpeedSettingStatus();
                } catch (error) {
                    lsSetItem(lsKeys.fixedSpeed.id, previousValue);
                    const checkbox = fixedSpeedControl.querySelector('input');
                    if (checkbox) { checkbox.checked = previousValue; }
                    window.__ddDanmakuFixedSpeedDiagnostic = previousDiagnostic;
                    if (window.ede) { window.ede.fixedSpeedDiagnostic = previousDiagnostic; }
                    updateFixedSpeedSettingStatus();
                    console.error('[弹幕速度] 切换失败，已恢复原设置和实例', error);
                    embyToast({ text: '弹幕速度模式切换失败，已恢复原设置' });
                }
            }
        );
        getById(eleIds.danmakuFixedSpeedDiv, container).append(fixedSpeedControl);
        updateFixedSpeedSettingStatus();
