(function () {
    window.TerminalHelpers = {
        fitAll: function () {
            if (window.XtermBlazor && window.XtermBlazor._terminals) {
                window.XtermBlazor._terminals.forEach(function (termObj) {
                    try {
                        var fitAddon = termObj.addons && termObj.addons.get('addon-fit');
                        if (fitAddon && typeof fitAddon.fit === 'function') {
                            fitAddon.fit();
                        } else if (termObj.terminal) {
                            var el = termObj.terminal.element;
                            var parent = el ? el.parentElement : null;
                            if (parent && parent.clientWidth > 0 && parent.clientHeight > 0) {
                                var cellWidth = (termObj.terminal._core && termObj.terminal._core._renderService && termObj.terminal._core._renderService.dimensions && termObj.terminal._core._renderService.dimensions.actualCellWidth) || 9;
                                var cellHeight = (termObj.terminal._core && termObj.terminal._core._renderService && termObj.terminal._core._renderService.dimensions && termObj.terminal._core._renderService.dimensions.actualCellHeight) || 17;
                                var padding = 16;
                                var cols = Math.max(10, Math.floor((parent.clientWidth - padding) / cellWidth));
                                var rows = Math.max(5, Math.floor((parent.clientHeight - padding) / cellHeight));
                                if (termObj.terminal.cols !== cols || termObj.terminal.rows !== rows) {
                                    termObj.terminal.resize(cols, rows);
                                }
                            }
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

                var intersectionObserver = new IntersectionObserver(function (entries) {
                    entries.forEach(function (entry) {
                        if (entry.isIntersecting) {
                            setTimeout(function () {
                                window.TerminalHelpers.fitAll();
                            }, 50);
                            setTimeout(function () {
                                window.TerminalHelpers.fitAll();
                            }, 200);
                        }
                    });
                });
                intersectionObserver.observe(containerEl);
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

    document.addEventListener('click', function (e) {
        if (e.target && e.target.closest && (e.target.closest('.rz-tabview-item') || e.target.closest('.rz-tabview-nav') || e.target.closest('.console-container') || e.target.closest('.terminal-container') || e.target.closest('.terminal-wrapper') || e.target.closest('.xterm-wrapper'))) {
            setTimeout(function () {
                window.TerminalHelpers.fitAll();
            }, 50);
            setTimeout(function () {
                window.TerminalHelpers.fitAll();
            }, 250);
        }
    });
})();
