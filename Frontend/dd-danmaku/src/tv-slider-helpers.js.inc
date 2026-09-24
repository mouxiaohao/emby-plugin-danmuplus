    function normalizeDdSliderDirection(eventOrDirection) {
        const key = typeof eventOrDirection === 'string'
            ? eventOrDirection
            : eventOrDirection && (eventOrDirection.key || eventOrDirection.code);
        const code = eventOrDirection && typeof eventOrDirection !== 'string'
            ? (eventOrDirection.keyCode || eventOrDirection.which)
            : 0;
        if (key === 'ArrowLeft' || key === 'Left' || key === 'left' || code === 37) { return -1; }
        if (key === 'ArrowRight' || key === 'Right' || key === 'right' || code === 39) { return 1; }
        return 0;
    }

    function ddSliderDecimalPlaces(value) {
        const text = String(value).toLowerCase();
        const exponentIndex = text.indexOf('e');
        if (exponentIndex >= 0) {
            const exponent = parseInt(text.slice(exponentIndex + 1), 10) || 0;
            const baseDecimals = (text.slice(0, exponentIndex).split('.')[1] || '').length;
            return Math.max(0, baseDecimals - exponent);
        }
        return (text.split('.')[1] || '').length;
    }

    function calculateDdSliderStep(slider, direction) {
        const minAttribute = slider.getAttribute('min');
        const maxAttribute = slider.getAttribute('max');
        const stepAttribute = slider.getAttribute('step');
        let min = minAttribute === null ? 0 : parseFloat(minAttribute);
        let max = maxAttribute === null ? 100 : parseFloat(maxAttribute);
        let step = stepAttribute === null || stepAttribute === 'any' ? 1 : parseFloat(stepAttribute);
        if (!Number.isFinite(min)) { min = 0; }
        if (!Number.isFinite(max)) { max = 100; }
        if (max < min) { max = min; }
        if (!Number.isFinite(step) || step <= 0) { step = 1; }
        let current = parseFloat(slider.value);
        if (!Number.isFinite(current)) { current = min; }
        current = Math.min(max, Math.max(min, current));

        const precision = Math.min(9, Math.max(
            ddSliderDecimalPlaces(min),
            ddSliderDecimalPlaces(max),
            ddSliderDecimalPlaces(step),
            ddSliderDecimalPlaces(current)
        ));
        const scale = Math.pow(10, precision);
        const minInt = Math.round(min * scale);
        const maxInt = Math.round(max * scale);
        const stepInt = Math.max(1, Math.round(step * scale));
        const currentInt = Math.round(current * scale);
        const currentIndex = Math.round((currentInt - minInt) / stepInt);
        let nextInt = minInt + (currentIndex + direction) * stepInt;
        nextInt = Math.min(maxInt, Math.max(minInt, nextInt));
        const next = Number((nextInt / scale).toFixed(precision));
        return {
            changed: Math.abs(next - current) > 1 / (scale * 2),
            value: next,
        };
    }

    function installDdSliderDirectionHandler(slider) {
        if (slider.__ddDanmakuDirectionHandlerInstalled) { return slider; }
        slider.__ddDanmakuDirectionHandlerInstalled = true;

        const handleDirection = (event, suppliedDirection, originalEvent) => {
            const direction = suppliedDirection || normalizeDdSliderDirection(event);
            const orient = slider.getAttribute('orient') || 'horizontal';
            if (!direction || orient !== 'horizontal') { return false; }
            if (slider.disabled || slider.hidden || slider.tabIndex < 0
                || slider.getAttribute('aria-disabled') === 'true') { return false; }

            if (event && typeof event.preventDefault === 'function') { event.preventDefault(); }
            if (event && typeof event.stopPropagation === 'function') { event.stopPropagation(); }
            const identity = originalEvent
                || (event && event.detail && event.detail.originalEvent)
                || event;
            if (identity && slider.__ddDanmakuLastDirectionEvent === identity) { return true; }
            slider.__ddDanmakuLastDirectionEvent = identity;

            const result = calculateDdSliderStep(slider, direction);
            if (!result.changed) { return true; }
            const hadFocus = document.activeElement === slider;
            slider.value = String(result.value);
            slider.dispatchEvent(new Event('input', { bubbles: true }));
            slider.dispatchEvent(new Event('change', { bubbles: true }));
            if (hadFocus && document.activeElement !== slider && typeof slider.focus === 'function') {
                try { slider.focus({ preventScroll: true }); } catch (error) { slider.focus(); }
            }
            return true;
        };

        slider.addEventListener('keydown', event => handleDirection(event));
        slider.addEventListener('emby-direction', event => {
            const detail = event.detail || {};
            handleDirection(event, normalizeDdSliderDirection(detail.direction), detail.originalEvent);
        });
        slider.ddDanmakuAdjustDirection = (direction, originalEvent) => handleDirection(
            originalEvent || null,
            normalizeDdSliderDirection(direction),
            originalEvent
        );
        return slider;
    }
