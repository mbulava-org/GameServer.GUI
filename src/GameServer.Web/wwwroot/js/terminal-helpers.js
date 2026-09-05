(function () {
    function getFitAddonInstance() {
        try {
            if (window.FitAddon) {
                var ctor = typeof window.FitAddon === 'function' ? window.FitAddon : window.FitAddon.FitAddon;
                if (typeof ctor === 'function') {
                    return new ctor();
                }
            }
        } catch (e) {
            console.warn('TerminalHelpers: Could not instantiate FitAddon:', e);
        }
        return null;
    }

    function ensureAddons(instance) {
        if (!instance || !instance._addonList) return;
        if (!instance._addonList.has('addon-fit')) {
            var fitAddon = getFitAddonInstance();
            if (fitAddon) {
                try {
                    instance.registerAddon('addon-fit', fitAddon);
                } catch (e) {
                    console.warn('TerminalHelpers: Could not register addon-fit:', e);
                }
            }
        }
    }

    function patchXtermBlazor(instance) {
        if (!instance || instance._resiliencePatched) return;

        ensureAddons(instance);

        if (typeof instance.registerTerminal === 'function') {
            var origRegisterTerminal = instance.registerTerminal.bind(instance);
            instance.registerTerminal = function (terminalId, element, options, addons) {
                ensureAddons(instance);

                if (element && element.style) {
                    element.style.width = '100%';
                    element.style.height = '100%';
                    element.style.minHeight = '0';
                    element.style.flex = '1';
                    element.style.display = 'flex';
                    element.style.flexDirection = 'column';
                }

                var safeAddons = [];
                if (Array.isArray(addons)) {
                    addons.forEach(function (addonName) {
                        if (instance._addonList && instance._addonList.has(addonName)) {
                            safeAddons.push(addonName);
                        } else {
                            if (addonName === 'addon-fit') {
                                ensureAddons(instance);
                                if (instance._addonList && instance._addonList.has(addonName)) {
                                    safeAddons.push(addonName);
                                    return;
                                }
                            }
                            console.warn('XtermBlazor: Addon "' + addonName + '" is not registered. Skipping to prevent circuit crash.');
                        }
                    });
                }

                try {
                    var res = origRegisterTerminal(terminalId, element, options, safeAddons);
                    setTimeout(function () {
                        window.TerminalHelpers.fitAll();
                    }, 50);
                    return res;
                } catch (err) {
                    console.error('TerminalHelpers: Handled error in XtermBlazor.registerTerminal:', err);
                }
            };
        }

        if (typeof instance.invokeAddonFunction === 'function') {
            var origInvoke = instance.invokeAddonFunction.bind(instance);
            instance.invokeAddonFunction = function (terminalId, addonName, functionName) {
                try {
                    var termObj = instance._terminals ? instance._terminals.get(terminalId) : null;
                    if (termObj && termObj.addons && termObj.addons.get(addonName)) {
                        return origInvoke.apply(instance, arguments);
                    } else {
                        console.warn('TerminalHelpers: Addon "' + addonName + '" not found on terminal ' + terminalId);
                        return null;
                    }
                } catch (err) {
                    console.warn('TerminalHelpers: Error in invokeAddonFunction:', err);
                    return null;
                }
            };
        }

        instance._resiliencePatched = true;
    }

    // Patch current instance if already loaded
    if (window.XtermBlazor) {
        patchXtermBlazor(window.XtermBlazor);
    }

    // Intercept future assignments to window.XtermBlazor
    var _xtermBlazor = window.XtermBlazor;
    try {
        Object.defineProperty(window, 'XtermBlazor', {
            configurable: true,
            enumerable: true,
            get: function () {
                return _xtermBlazor;
            },
            set: function (val) {
                _xtermBlazor = val;
                if (val) {
                    patchXtermBlazor(val);
                }
            }
        });
    } catch (e) {
        // Fallback if defineProperty fails
    }

    window.TerminalHelpers = {
        ensureAddons: function () {
            if (window.XtermBlazor) {
                patchXtermBlazor(window.XtermBlazor);
            }
        },
        fitAll: function () {
            if (window.XtermBlazor && window.XtermBlazor._terminals) {
                window.XtermBlazor._terminals.forEach(function (termObj) {
                    try {
                        var term = termObj.terminal;
                        if (!term) return;

                        var el = term.element;
                        var host = el ? el.parentElement : null;
                        var wrapper = el ? (el.closest('.terminal-wrapper, .xterm-wrapper') || host) : null;

                        if (host && host.style) {
                            if (host.style.height !== '100%') host.style.height = '100%';
                            if (host.style.width !== '100%') host.style.width = '100%';
                            if (host.style.flex !== '1') host.style.flex = '1';
                            if (host.style.display !== 'flex') host.style.display = 'flex';
                            if (host.style.flexDirection !== 'column') host.style.flexDirection = 'column';
                            if (host.style.minHeight !== '0') host.style.minHeight = '0';
                        }

                        var targetContainer = wrapper || host;
                        if (targetContainer && targetContainer.clientWidth > 0 && targetContainer.clientHeight > 0) {
                            var cellWidth = (term._core && term._core._renderService && term._core._renderService.dimensions && term._core._renderService.dimensions.actualCellWidth) || 9;
                            var cellHeight = (term._core && term._core._renderService && term._core._renderService.dimensions && term._core._renderService.dimensions.actualCellHeight) || 17;
                            
                            var paddingHor = 16;
                            var paddingVer = 16;

                            var availableWidth = Math.max(0, targetContainer.clientWidth - paddingHor);
                            var availableHeight = Math.max(0, targetContainer.clientHeight - paddingVer);

                            var cols = Math.max(10, Math.floor(availableWidth / cellWidth));
                            var rows = Math.max(5, Math.floor(availableHeight / cellHeight));

                            if (term.cols !== cols || term.rows !== rows) {
                                if (term._core && term._core._renderService) {
                                    term._core._renderService.clear();
                                }
                                term.resize(cols, rows);
                            }
                        } else {
                            var fitAddon = termObj.addons && termObj.addons.get('addon-fit');
                            if (fitAddon && typeof fitAddon.fit === 'function') {
                                fitAddon.fit();
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

    document.addEventListener('DOMContentLoaded', function () {
        if (window.XtermBlazor) {
            patchXtermBlazor(window.XtermBlazor);
        }
    });

    document.addEventListener('blazor.enhancedload', function () {
        if (window.XtermBlazor) {
            patchXtermBlazor(window.XtermBlazor);
        }
    });
})();
