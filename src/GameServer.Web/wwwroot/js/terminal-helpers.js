(function () {
    window.TerminalHelpers = {
        fitAll: function () {
            if (window.XtermBlazor && window.XtermBlazor._terminals) {
                window.XtermBlazor._terminals.forEach(function (termObj) {
                    try {
                        var fitAddon = termObj.addons && termObj.addons.get('addon-fit');
                        if (fitAddon && typeof fitAddon.fit === 'function') {
                            fitAddon.fit();
                        }
                    } catch (err) {
                        // Suppress resize errors if element is detached/hidden
                    }
                });
            }
        },
        initAutoFit: function (containerEl) {
            if (!containerEl) return;
            try {
                var observer = new ResizeObserver(function () {
                    window.requestAnimationFrame(function () {
                        window.TerminalHelpers.fitAll();
                    });
                });
                observer.observe(containerEl);
            } catch (e) {
                // Fallback to window resize
            }
        }
    };

    window.addEventListener('resize', function () {
        window.requestAnimationFrame(function () {
            window.TerminalHelpers.fitAll();
        });
    });
})();
