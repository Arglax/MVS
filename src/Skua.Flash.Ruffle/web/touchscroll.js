(() => {
    "use strict";
    const STAGE_W = 960, STAGE_H = 550;
    const PX_PER_LINE = 10;
    const START_PX = 10;
    const SCROLLERS = /^(LPF\w+|monsterDrops|MailWindow|KeybindsPanel|keybinds|cEntry|\w*ScrollPane|TextArea|List|TileList|DataGrid|TouchSlider|Chat2?|HouseMenu|ItemList|QuestLayout|qRewardPrev|mcOption|QFrameMC|sBar)$/;
    const GAME_SCROLLS = /^(LPF\w+|MailWindow|monsterDrops|keybinds|KeybindsPanel|mcOption|QFrameMC|OFrameMC|QTrackerMC|FactionsMC|cellMenu)$/;
    const BAG_STRIP = /^Sprite>Sprite>Sprite>LPFFrameInvAQ>/;
    let touchOn = false, touchAt = 0;
    function gameTouchOn() {
        const now = Date.now();
        if (now - touchAt < 3000) return touchOn;
        touchAt = now;
        touchOn = askGameTouch();
        return touchOn;
    }
    window.__mvsGameTouchOn = gameTouchOn;
    window.addEventListener("mvsoption", () => { touchAt = 0; });
    function askGameTouch() {
        try {
            const r = document.querySelector("ruffle-player").ruffle();
            let v = null;
            try { v = r.callExternalInterface("mvsMobileActive"); } catch (e) { v = null; }
            if (v === true || v === false) return v;
            return r.callExternalInterface("getGameObject", "litePreference.data.sTouchMode") === "\"on\"";
        } catch (e) {
            return false;
        }
    }
    let swipe = null;
    let fling = null;
    function canvas() {
        const p = document.querySelector("ruffle-player");
        return p && p.shadowRoot ? p.shadowRoot.querySelector("canvas") : null;
    }
    function cssPerStage(c) {
        return Math.min(c.clientWidth / STAGE_W, c.clientHeight / STAGE_H) || 1;
    }
    function fromGame(e) {
        const p = document.querySelector("ruffle-player");
        return p != null && e.composedPath().includes(p);
    }
    function isScroller(chain) {
        return typeof chain === "string" && chain.split(">").some(name => SCROLLERS.test(name));
    }
    function send(c, type, x, y, pointerId, buttons) {
        const Ctor = type.startsWith("pointer") ? PointerEvent : MouseEvent;
        const ev = new Ctor(type, {
            bubbles: true, cancelable: true, composed: true,
            clientX: x, clientY: y, pointerId, pointerType: "touch", isPrimary: true,
            button: type === "pointermove" ? -1 : 0, buttons
        });
        const r = c.getBoundingClientRect();
        Object.defineProperty(ev, "offsetX", { value: x - r.left - c.clientLeft });
        Object.defineProperty(ev, "offsetY", { value: y - r.top - c.clientTop });
        c.dispatchEvent(ev);
    }
    function scrollBy(s, cssDy) {
        s.acc += cssDy / cssPerStage(s.c) / PX_PER_LINE;
        const lines = Math.trunc(s.acc);
        if (lines === 0) return;
        s.acc -= lines;
        s.c.dispatchEvent(new WheelEvent("wheel", {
            bubbles: true, cancelable: true, composed: true,
            clientX: s.x0, clientY: s.y0, deltaMode: WheelEvent.DOM_DELTA_LINE, deltaY: lines
        }));
    }
    function stopFling() {
        if (fling) cancelAnimationFrame(fling.raf);
        fling = null;
    }
    window.addEventListener("pointerdown", e => {
        stopFling();
        if (e.pointerType !== "touch" || !e.isPrimary || !fromGame(e)) {
            swipe = null;
            return;
        }
        const c = canvas();
        if (!c) {
            swipe = null;
            return;
        }
        if (e.isTrusted) {
            swipe = null;
            send(c, "pointermove", e.clientX, e.clientY, e.pointerId, 0);
        }
        if (window.__mvsSwipeScroll === false) {
            swipe = null;
            return;
        }
        window.__mvsPressChain = "";
        swipe = { id: e.pointerId, c, x0: e.clientX, y0: e.clientY, lastY: e.clientY, mode: "pending", acc: 0, track: [],
            gameTouch: gameTouchOn() };
    }, true);
    window.addEventListener("pointermove", e => {
        const s = swipe;
        if (!s || e.pointerId !== s.id) return;
        if (s.mode === "pending") {
            const dx = e.clientX - s.x0, dy = e.clientY - s.y0;
            if (Math.abs(dx) > START_PX && Math.abs(dx) > Math.abs(dy)) {
                s.mode = "pass";
            } else if (Math.abs(dy) > START_PX && Math.abs(dy) > 1.2 * Math.abs(dx)) {
                const chain = window.__mvsPressChain;
                const strip = typeof chain === "string" && BAG_STRIP.test(chain);
                const gameOwns = !strip && s.gameTouch && typeof chain === "string" && chain.split(">").some(n => GAME_SCROLLS.test(n));
                s.mode = !gameOwns && isScroller(chain) ? "scroll" : "pass";
                s.lastY = s.y0;
            }
        }
        if (s.mode !== "scroll") return;
        e.stopPropagation();
        e.preventDefault();
        scrollBy(s, s.lastY - e.clientY);
        s.lastY = e.clientY;
        s.track.push({ t: e.timeStamp, y: e.clientY });
        while (s.track.length > 2 && e.timeStamp - s.track[0].t > 100) s.track.shift();
    }, true);
    function end(e) {
        const s = swipe;
        if (!s || e.pointerId !== s.id) return;
        swipe = null;
        if (s.mode !== "scroll") return;
        e.stopPropagation();
        e.preventDefault();
        send(s.c, "pointermove", -10000, -10000, s.id, 1);
        send(s.c, "pointerup", -10000, -10000, s.id, 0);
        send(s.c, "pointermove", s.x0, s.y0, s.id, 0);
        if (e.type === "pointercancel" || s.track.length < 2) return;
        const a = s.track[0], b = s.track[s.track.length - 1];
        let v = Math.max(-4, Math.min(4, (a.y - b.y) / Math.max(8, b.t - a.t)));
        if (Math.abs(v) < 0.15) return;
        let last = performance.now();
        const step = now => {
            const dt = Math.min(50, now - last);
            last = now;
            scrollBy(s, v * dt);
            v *= Math.pow(0.95, dt / 16);
            if (Math.abs(v) < 0.03) { fling = null; return; }
            fling.raf = requestAnimationFrame(step);
        };
        fling = { raf: requestAnimationFrame(step) };
    }
    window.addEventListener("pointerup", end, true);
    window.addEventListener("pointercancel", end, true);
})();
