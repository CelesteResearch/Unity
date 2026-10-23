mergeInto(LibraryManager.library, {
    $CelesteTouch: {
        root: null,
        directions: [2, 10, 8, 9, 1, 5, 4, 6],
        pointers: null,
        pending: null,
        dashDirections: null,
        enabled: false,
        listeners: [],
        direction: function (x, y) {
            if (x * x + y * y < 0.04) return 0;
            var sector = (Math.round((Math.atan2(y, x) * 4) / Math.PI) + 8) % 8;
            return CelesteTouch.directions[sector];
        },
        listen: function (target, event, callback, options) {
            target.addEventListener(event, callback, options);
            CelesteTouch.listeners.push([target, event, callback, options]);
        },
        release: function () {
            CelesteTouch.pointers.forEach(function (pointer, id) {
                if (pointer.element.hasPointerCapture(id)) pointer.element.releasePointerCapture(id);
            });
            CelesteTouch.pointers.clear();
            CelesteTouch.pending.clear();
            CelesteTouch.dashDirections.clear();
            if (CelesteTouch.root)
                CelesteTouch.root.querySelectorAll("[data-pressed]").forEach(function (element) {
                    element.removeAttribute("data-pressed");
                });
        },
        mask: function () {
            var result = 0;
            CelesteTouch.pointers.forEach(function (pointer) {
                result |= pointer.bits;
            });
            return result;
        },
        read: function () {
            var value = CelesteTouch.mask();
            CelesteTouch.pending.forEach(function (bits) {
                value |= bits;
            });
            var captured = false;
            CelesteTouch.dashDirections.forEach(function (direction) {
                if (!captured) {
                    value = (value & ~15) | direction;
                    captured = true;
                }
            });
            CelesteTouch.pending.clear();
            CelesteTouch.dashDirections.clear();
            return (CelesteTouch.enabled ? 256 : 0) | value;
        },
        updateAppearance: function () {
            if (!CelesteTouch.root) return;
            CelesteTouch.root.querySelectorAll("[data-control]").forEach(function (element) {
                var pressed = false;
                var direction = 0;
                CelesteTouch.pointers.forEach(function (pointer) {
                    if (pointer.element === element && pointer.bits) {
                        pressed = true;
                        direction |= pointer.bits;
                    }
                });
                if (element.getAttribute("data-control") === "pad")
                    element.setAttribute("data-direction", direction & 15);
                if (pressed) element.setAttribute("data-pressed", "");
                else element.removeAttribute("data-pressed");
            });
        },
        install: function () {
            if (CelesteTouch.root) return;
            CelesteTouch.pointers = new Map();
            CelesteTouch.pending = new Map();
            CelesteTouch.dashDirections = new Map();
            var viewport = document.getElementById("celeste-viewport");
            if (!viewport) throw new Error("Touch controls require the Celeste viewport.");
            var root = document.createElement("div");
            root.id = "celeste-touch";
            root.hidden = true;
            root.innerHTML =
                "<style>" +
                "#celeste-touch{position:absolute;inset:0;pointer-events:none;z-index:4;--pad:clamp(120px,30vmin,160px);--key:clamp(64px,17vmin,88px)}" +
                "#celeste-touch[hidden]{display:none}#celeste-touch [data-control]{position:absolute;box-sizing:border-box;pointer-events:auto;touch-action:none;user-select:none;-webkit-user-select:none;-webkit-tap-highlight-color:transparent;border:0;background:none;display:grid;place-items:center;padding:0;outline:none}" +
                "#celeste-touch img{width:100%;height:100%;object-fit:contain;pointer-events:none;user-select:none;-webkit-user-drag:none}" +
                '#celeste-touch [data-control]:not([data-control="pad"]){opacity:.8}#celeste-touch [data-control]:not([data-control="pad"])[data-pressed]{opacity:1;filter:brightness(1.15);transform:translateY(2px)}' +
                '#celeste-touch [data-control="pad"]{width:var(--pad);height:var(--pad);left:max(20px,env(safe-area-inset-left));bottom:max(20px,env(safe-area-inset-bottom))}' +
                '#celeste-touch [data-control="pad"],#celeste-touch [data-control="grab"]{border:2px solid #d9edf066;border-radius:50%;background:#111c2a20}' +
                '#celeste-touch [data-control="pad"][data-pressed],#celeste-touch [data-control="grab"][data-pressed]{border-color:#e8f8ffb3;background:#24354940}' +
                "#celeste-touch .direction{position:absolute;width:43%;height:43%;opacity:.6;pointer-events:none}" +
                "#celeste-touch .up{top:0;left:28.5%}#celeste-touch .down{bottom:0;left:28.5%}#celeste-touch .left{left:0;top:28.5%}#celeste-touch .right{right:0;top:28.5%}" +
                '#celeste-touch [data-direction="4"] .up,#celeste-touch [data-direction="5"] .up,#celeste-touch [data-direction="6"] .up,' +
                '#celeste-touch [data-direction="8"] .down,#celeste-touch [data-direction="9"] .down,#celeste-touch [data-direction="10"] .down,' +
                '#celeste-touch [data-direction="1"] .left,#celeste-touch [data-direction="5"] .left,#celeste-touch [data-direction="9"] .left,' +
                '#celeste-touch [data-direction="2"] .right,#celeste-touch [data-direction="6"] .right,#celeste-touch [data-direction="10"] .right{opacity:1;filter:brightness(1.15)}' +
                '#celeste-touch [data-control="jump"],#celeste-touch [data-control="dash"],#celeste-touch [data-control="grab"]{width:var(--key);height:var(--key)}' +
                '#celeste-touch [data-control="jump"]{right:max(20px,env(safe-area-inset-right));bottom:max(20px,env(safe-area-inset-bottom))}' +
                '#celeste-touch [data-control="dash"]{right:calc(max(20px,env(safe-area-inset-right)) + var(--key) + 12px);bottom:calc(max(20px,env(safe-area-inset-bottom)) + var(--key)*.7)}' +
                '#celeste-touch [data-control="grab"]{right:max(20px,env(safe-area-inset-right));bottom:calc(max(20px,env(safe-area-inset-bottom)) + var(--key) + 18px)}' +
                '#celeste-touch [data-control="pause"]{width:52px;height:52px;right:max(16px,env(safe-area-inset-right));top:max(16px,env(safe-area-inset-top))}' +
                "@media(max-height:320px){#celeste-touch{--pad:100px;--key:54px}}" +
                "</style>";
            var controls = [
                ["pad", 0, "Direction"],
                ["jump", 16, "Jump / Confirm"],
                ["dash", 32, "Dash / Back"],
                ["grab", 64, "Hold to grab"],
                ["pause", 128, "Pause"],
            ];
            controls.forEach(function (control) {
                var element = document.createElement("div");
                element.setAttribute("data-control", control[0]);
                element.setAttribute("role", "button");
                element.setAttribute("aria-label", control[2]);
                if (control[0] === "pad") {
                    element.innerHTML = ["up", "down", "left", "right"]
                        .map(function (direction) {
                            return (
                                '<span class="direction ' +
                                direction +
                                '"><img src="touch/' +
                                direction +
                                '.png" alt="" draggable="false"></span>'
                            );
                        })
                        .join("");
                } else {
                    element.innerHTML = '<img src="touch/' + control[0] + '.png" alt="" draggable="false">';
                }
                root.appendChild(element);
                function bits(event, bounds) {
                    return control[0] === "pad"
                        ? CelesteTouch.direction(
                              (event.clientX - bounds.left - bounds.width / 2) / (bounds.width / 2),
                              (event.clientY - bounds.top - bounds.height / 2) / (bounds.height / 2),
                          )
                        : control[1];
                }
                CelesteTouch.listen(element, "pointerdown", function (event) {
                    if (event.pointerType !== "touch" && event.pointerType !== "pen") return;
                    event.preventDefault();
                    event.stopPropagation();
                    var canvas = document.getElementById("unity-canvas");
                    if (canvas) canvas.focus({ preventScroll: true });
                    var bounds = element.getBoundingClientRect();
                    var pointer = { element: element, bounds: bounds, bits: bits(event, bounds) };
                    CelesteTouch.pointers.set(event.pointerId, pointer);
                    CelesteTouch.pending.set(event.pointerId, pointer.bits);
                    if (control[1] === 32) {
                        var direction = CelesteTouch.mask();
                        CelesteTouch.pending.forEach(function (bits) {
                            direction |= bits;
                        });
                        CelesteTouch.dashDirections.set(event.pointerId, direction & 15);
                    }
                    element.setPointerCapture(event.pointerId);
                    CelesteTouch.updateAppearance();
                });
                CelesteTouch.listen(element, "pointermove", function (event) {
                    var pointer = CelesteTouch.pointers.get(event.pointerId);
                    if (!pointer || pointer.element !== element) return;
                    event.preventDefault();
                    pointer.bits = bits(event, pointer.bounds);
                    CelesteTouch.pending.set(event.pointerId, pointer.bits);
                    CelesteTouch.updateAppearance();
                });
                function end(event) {
                    if (!CelesteTouch.pointers.has(event.pointerId)) return;
                    event.preventDefault();
                    CelesteTouch.pointers.delete(event.pointerId);
                    if (event.type !== "pointerup") {
                        CelesteTouch.pending.delete(event.pointerId);
                        CelesteTouch.dashDirections.delete(event.pointerId);
                    }
                    if (element.hasPointerCapture(event.pointerId)) element.releasePointerCapture(event.pointerId);
                    CelesteTouch.updateAppearance();
                }
                CelesteTouch.listen(element, "pointerup", end);
                CelesteTouch.listen(element, "pointercancel", end);
                CelesteTouch.listen(element, "lostpointercapture", end);
                CelesteTouch.listen(element, "contextmenu", function (event) {
                    event.preventDefault();
                });
            });
            viewport.appendChild(root);
            CelesteTouch.root = root;
            var coarse = window.matchMedia("(pointer: coarse)");
            function touchPrimary() {
                return navigator.maxTouchPoints > 0 && coarse.matches;
            }
            function reveal() {
                CelesteTouch.enabled = true;
                root.hidden = false;
            }
            if (touchPrimary()) reveal();
            CelesteTouch.listen(coarse, "change", function () {
                CelesteTouch.release();
                CelesteTouch.enabled = touchPrimary();
                root.hidden = !CelesteTouch.enabled;
            });
            CelesteTouch.listen(
                viewport,
                "pointerdown",
                function (event) {
                    if (event.pointerType === "touch") reveal();
                },
                { capture: true },
            );
            CelesteTouch.listen(window, "blur", CelesteTouch.release);
            CelesteTouch.listen(document, "visibilitychange", function () {
                if (document.hidden) CelesteTouch.release();
            });
            CelesteTouch.listen(window, "resize", CelesteTouch.release);
            CelesteTouch.listen(document, "fullscreenchange", CelesteTouch.release);
        },
        dispose: function () {
            CelesteTouch.release();
            CelesteTouch.listeners.forEach(function (item) {
                item[0].removeEventListener(item[1], item[2], item[3]);
            });
            CelesteTouch.listeners.length = 0;
            if (CelesteTouch.root) CelesteTouch.root.remove();
            CelesteTouch.root = null;
            CelesteTouch.enabled = false;
        },
    },
    CelesteWebTouchInstall__deps: ["$CelesteTouch"],
    CelesteWebTouchInstall: function () {
        CelesteTouch.install();
    },
    CelesteWebTouchRead__deps: ["$CelesteTouch"],
    CelesteWebTouchRead: function () {
        return CelesteTouch.read();
    },
    CelesteWebTouchDispose__deps: ["$CelesteTouch"],
    CelesteWebTouchDispose: function () {
        CelesteTouch.dispose();
    },
});
