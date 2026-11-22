(function () {
    "use strict";
    const base = location.href.replace(/[?#].*$/, "").replace(/[^/]*$/, "");
    const statusEl = () => document.getElementById("status");
    let socket = null;
    let player = null;
    let backlog = [];
    const audioContexts = [];
    const muteReasons = new Set();
    let mutedVolume = null;
    let audioSuspended = false;
    function syncAudioSuspend(muted) {
        if (muted) {
            audioSuspended = true;
            for (const ctx of audioContexts) {
                try {
                    if (ctx.state === "running") ctx.suspend().catch(() => { });
                } catch (e) { }
            }
        } else if (audioSuspended) {
            audioSuspended = false;
            for (const ctx of audioContexts) {
                try {
                    if (ctx.state === "suspended") ctx.resume().catch(() => { });
                } catch (e) { }
            }
        }
    }
    function wrapAudioContext(name) {
        const Native = window[name];
        if (typeof Native !== "function") return;
        const wrapped = new Proxy(Native, {
            construct(target, args, newTarget) {
                const ctx = Reflect.construct(target, args, newTarget === wrapped ? target : newTarget);
                audioContexts.push(ctx);
                if (muteReasons.size > 0) syncAudioSuspend(true);
                return ctx;
            }
        });
        window[name] = wrapped;
    }
    wrapAudioContext("AudioContext");
    wrapAudioContext("webkitAudioContext");
    const nativeFetch = window.fetch.bind(window);
    function proxied(url) {
        let u;
        try { u = new URL(url, location.href); } catch (e) { return null; }
        if (u.origin === location.origin) return null;
        if (u.protocol !== "http:" && u.protocol !== "https:") return null;
        return base + "p/" + u.protocol.slice(0, -1) + "/" + u.host + u.pathname + u.search;
    }
    window.fetch = async function (input, init) {
        try {
            const asked = typeof input === "string" ? input : input instanceof URL ? String(input) : input && input.url;
            if (asked && /\/gamefiles\/maps\//.test(asked) && !window.__mvsMapAskedAt) window.__mvsMapAskedAt = performance.now();
        } catch (e) { }
        if (typeof input === "string" || input instanceof URL) {
            return nativeFetch(proxied(String(input)) || input, init);
        }
        if (input instanceof Request) {
            const target = proxied(input.url);
            if (target) {
                const opts = { method: input.method, headers: input.headers, credentials: "omit", cache: input.cache, redirect: "follow" };
                if (input.method !== "GET" && input.method !== "HEAD") opts.body = await input.arrayBuffer();
                return nativeFetch(target, Object.assign(opts, init || {}));
            }
        }
        return nativeFetch(input, init);
    };
    const nativeDpr = window.devicePixelRatio || 1;
    let gfx = { dpr: 0, quality: "", filter: "natural", fps: false, auto: true, heat: true, smooth: 0 };
    let screenRatioCap = 0;
    const colourFilters = {
        natural: "",
        vivid: "saturate(1.3) contrast(1.08)",
        cinematic: "contrast(1.15) saturate(1.12) brightness(0.95)",
        bright: "brightness(1.12) contrast(1.05)"
    };
    function setPixelRatio(ratio, scale) {
        let want = ratio > 0 ? ratio : ratio < 0 ? Math.min(nativeDpr * -ratio, 3.5) : nativeDpr;
        if (ratio === 0 && screenRatioCap > 0) want = Math.min(want, screenRatioCap);
        if (scale && scale < 1) want = Math.max(1, want * scale);
        want = Math.round(want * 100) / 100;
        if (window.devicePixelRatio === want) return;
        Object.defineProperty(window, "devicePixelRatio", { get: () => want, configurable: true });
        const el = player;
        if (!el) return;
        el.style.width = "calc(100% - 1px)";
        requestAnimationFrame(() => requestAnimationFrame(() => { el.style.width = ""; }));
    }
    let touchRowH = 0;
    function applyTouchUi() {
        if (touchRowH <= 0) return;
        try { player.ruffle().callExternalInterface("mvsTouchUi", true); } catch (e) { log("touch ui: " + e); }
    }
    function applyQuality() {
        const q = governedQuality();
        if (!player) return;
        holdGameQuality(gfx.quality || (gfx.auto && gov.level > 0) ? q : "");
        if (!q) return;
        try { player.ruffle().callExternalInterface("setGameObject", "stage.quality", q); } catch (e) { }
    }
    let heldPref = null;
    function holdGameQuality(q) {
        try {
            if (q) {
                if (heldPref === null) {
                    let cur = null;
                    try { cur = JSON.parse(player.ruffle().callExternalInterface("getGameObject", "userPreference.data.quality")); } catch (e) { cur = null; }
                    if (cur !== "AUTO") return;
                    heldPref = cur;
                }
                player.ruffle().callExternalInterface("setGameObject", "userPreference.data.quality", ["LOW", "MEDIUM", "HIGH"].indexOf(q) >= 0 ? q : "HIGH");
            } else if (heldPref !== null) {
                player.ruffle().callExternalInterface("setGameObject", "userPreference.data.quality", heldPref);
                heldPref = null;
            }
        } catch (e) { }
    }
    window.addEventListener("pagehide", () => holdGameQuality(""));
    function mvsOptStored(key) {
        try { return localStorage.getItem("mvs.opt." + key) !== "off"; } catch (e) { return true; }
    }
    window.__mvsOpts = {
        mvsImproveFps: mvsOptStored("mvsImproveFps"), mvsLongDash: mvsOptStored("mvsLongDash"),
        mvsBigUi: mvsOptStored("mvsBigUi"), mvsSidebar: mvsOptStored("mvsSidebar"),
        mvsSwipe: mvsOptStored("mvsSwipe"), mvsAuras: mvsOptStored("mvsAuras"), mvsDrops: mvsOptStored("mvsDrops")
    };
    window.__mvsSwipeScroll = window.__mvsOpts.mvsSwipe;
    function onMvsOption(key, value) {
        const on = String(value) !== "off";
        try { localStorage.setItem("mvs.opt." + key, on ? "on" : "off"); } catch (e) { }
        const was = window.__mvsOpts[key];
        window.__mvsOpts[key] = on;
        if (key === "mvsSwipe") window.__mvsSwipeScroll = on;
        if (key === "mvsImproveFps") {
            window.__mvsFreezeOrphans = on;
            if (!on && gov.level !== 0) setLevel(0, "Improve FPS off");
            applySmooth();
        }
        if ((key === "mvsLongDash" || key === "mvsBigUi") && was !== undefined && was !== on) log(key + " " + (on ? "on" : "off") + ": takes effect the next time the game starts");
        try { window.dispatchEvent(new CustomEvent("mvsoption", { detail: { key, on } })); } catch (e) { }
    }
    const aaLadder = ["8X8", "HIGH", "MEDIUM", "LOW"];
    const govLevels = [
        { smooth: true, crowd: false, aa: 0, res: 1 }, { smooth: true, crowd: true, aa: 0, res: 1 },
        { smooth: false, crowd: true, aa: 0, res: 1 }, { smooth: false, crowd: true, aa: 1, res: 1 },
        { smooth: false, crowd: true, aa: 2, res: 1 }, { smooth: false, crowd: true, aa: 2, res: 0.8 },
        { smooth: false, crowd: true, aa: 3, res: 0.8 }, { smooth: false, crowd: true, aa: 3, res: 0.65 }
    ];
    window.__mvsFreezeOrphans = window.__mvsOpts.mvsImproveFps;
    const gov = {
        level: 0, floor: 0, low: 0, good: 0, settle: 0, gameRate: 30, rateAt: 0,
        upWait: 30, failedUpAt: 0, heat: "ok", lastFps: 0, gameQuality: "",
        trial: null, gpuBlockUntil: 0, recent: [],
        throttled: false, smoothPoor: 0, smoothPausedUntil: 0
    };
    function governedQuality() {
        const own = gfx.quality || gov.gameQuality;
        if (!gfx.auto || gov.level === 0) return own;
        let base = aaLadder.indexOf(own || "HIGH");
        if (base < 0) base = 1;
        return aaLadder[Math.min(aaLadder.length - 1, base + govLevels[gov.level].aa)];
    }
    function governedScale() {
        return gfx.auto ? govLevels[gov.level].res : 1;
    }
    function governedSmooth() {
        if (!(gfx.smooth > 0) || window.__mvsNoDraw || !window.__mvsOpts.mvsImproveFps) return 0;
        if (!gfx.auto) return gfx.smooth;
        if (window.__mvsNoDraw || !govLevels[gov.level].smooth || gov.throttled || Date.now() < gov.smoothPausedUntil) return 0;
        return gfx.smooth;
    }
    function checkSmooth(fps, drawn) {
        if (!gfx.auto || !(gfx.smooth > 0)) return;
        if (Number(window.__mvsInterp) > 0 && drawn >= 0) {
            gov.smoothPoor = drawn - fps < 15 ? gov.smoothPoor + 1 : 0;
            if (gov.smoothPoor >= 5) {
                gov.smoothPoor = 0;
                gov.smoothPausedUntil = Date.now() + 60000;
                log("governor: in-betweening paused for a minute (" + drawn + " pictures for " + fps + " game frames)");
            }
        } else {
            gov.smoothPoor = 0;
        }
        applySmooth();
    }
    function applySmooth() {
        window.__mvsInterp = governedSmooth();
    }
    let heldCrowd = null;
    function governedCrowd() {
        return !!(gfx.auto && govLevels[gov.level] && govLevels[gov.level].crowd);
    }
    function readCrowd() {
        try {
            const raw = player.ruffle().callExternalInterface("getGameObject", "litePreference.data.sCrowd");
            return raw == null || raw === "null" ? null : JSON.parse(raw);
        } catch (e) { return null; }
    }
    function applyCrowd() {
        try {
            if (!player) return;
            if (governedCrowd()) {
                if (heldCrowd === null) heldCrowd = readCrowd() || "auto";
                player.ruffle().callExternalInterface("setGameObject", "litePreference.data.sCrowd", "on");
            } else if (heldCrowd !== null) {
                player.ruffle().callExternalInterface("setGameObject", "litePreference.data.sCrowd", heldCrowd);
                heldCrowd = null;
            }
        } catch (e) { }
    }
    function defaultCrowd() {
        try {
            if (player && readCrowd() === null) player.ruffle().callExternalInterface("setGameObject", "litePreference.data.sCrowd", "auto");
        } catch (e) { }
    }
    window.addEventListener("pagehide", () => { gov.level = 0; applyCrowd(); });
    function defaultUntargetSelf() {
        try {
            if (!player || localStorage.getItem("mvs.untargetSelfSet")) return;
            const game = (...a) => player.ruffle().callExternalInterface(...a);
            game("setGameObject", "litePreference.data.bUntargetSelf", true);
            game("callGameFunction", "litePreference.flush");
            localStorage.setItem("mvs.untargetSelfSet", "1");
        } catch (e) { }
    }
    function levelKey(level) {
        const saved = gov.level;
        gov.level = level;
        const key = governedSmooth() + "|" + governedQuality() + "|" + governedScale() + "|" + governedCrowd();
        gov.level = saved;
        return key;
    }
    function setLevel(level, why) {
        level = Math.max(gov.floor, Math.min(govLevels.length - 1, level));
        if (level === gov.level) return;
        const now = levelKey(gov.level), step = level > gov.level ? 1 : -1;
        while (level > gov.floor && level < govLevels.length - 1 && levelKey(level) === now) level += step;
        const up = level < gov.level;
        if (gov.level === 0 && !gfx.quality) {
            try { gov.gameQuality = String(JSON.parse(player.ruffle().callExternalInterface("getGameObject", "stage.quality"))).toUpperCase(); }
            catch (e) { gov.gameQuality = ""; }
        }
        gov.level = level;
        gov.low = 0; gov.good = 0; gov.settle = 3;
        setPixelRatio(gfx.dpr, governedScale());
        applyQuality();
        applySmooth();
        applyCrowd();
        log("governor: level " + level + " (" + why + "; " + gov.lastFps + " fps of " + gov.gameRate +
            ", aa " + (governedQuality() || "game") + ", ratio " + window.devicePixelRatio + ", in-between " + governedSmooth() + ")");
        if (up) gov.failedUpAt = Date.now();
    }
    function isGpuStep(level) {
        return level >= 3;
    }
    function recentFps() {
        const r = gov.recent;
        return r.length ? r.reduce((a, b) => a + b, 0) / r.length : gov.lastFps;
    }
    function readGameRate() {
        try {
            const v = Number(JSON.parse(player.ruffle().callExternalInterface("getGameObject", "stage.frameRate")));
            if (v >= 10 && v <= 120) gov.gameRate = v;
        } catch (e) { }
    }
    function governorTick(fps) {
        gov.lastFps = fps;
        if (!gfx.auto || !player || document.hidden || window.__mvsNoDraw || player.style.visibility === "hidden") return;
        if (!window.__mvsOpts.mvsImproveFps) return;
        if (!gov.rateAt || fps > gov.gameRate + 5 || Date.now() - gov.rateAt > 60000) { gov.rateAt = Date.now(); readGameRate(); }
        if (gov.settle > 0) { gov.settle--; return; }
        gov.recent.push(fps);
        if (gov.recent.length > 4) gov.recent.shift();
        if (gov.trial) {
            gov.trial.samples.push(fps);
            if (gov.trial.samples.length >= 4) {
                const t = gov.trial, avg = t.samples.reduce((a, b) => a + b, 0) / t.samples.length;
                gov.trial = null;
                if (t.gpu && avg < t.before + 2 && t.from >= gov.floor) {
                    gov.gpuBlockUntil = Date.now() + 120000;
                    setLevel(t.from, "lower graphics gained nothing (" + avg.toFixed(1) + " vs " + t.before.toFixed(1) + " fps)");
                    return;
                }
            }
        }
        const want = Math.min(gov.gameRate, 30);
        if (fps < want * 0.85) {
            gov.good = 0;
            if (++gov.low >= 3) {
                if (Date.now() - gov.failedUpAt < 20000) gov.upWait = Math.min(gov.upWait * 2, 600);
                const from = gov.level;
                if (isGpuStep(from + 1) && Date.now() < gov.gpuBlockUntil) { gov.low = 0; return; }
                const before = recentFps();
                setLevel(from + 1, "slow");
                if (gov.level !== from) gov.trial = { from, before, gpu: isGpuStep(gov.level), samples: [] };
            }
        } else {
            gov.low = 0;
            if (fps >= want * 0.95 && gov.level > gov.floor && ++gov.good >= gov.upWait) setLevel(gov.level - 1, "room to spare");
        }
    }
    let lastThermal = null;
    function setThermal(msg) {
        lastThermal = msg;
        const status = Number(msg.status), headroom = Number(msg.headroom), battery = Number(msg.battery);
        const was = gov.heat;
        const hot = status >= 3 || battery >= 47 || (was === "hot" && battery >= 45);
        const warm = status >= 2 || battery >= 45 || (was !== "ok" && battery >= 43.5);
        const heat = hot ? "hot" : warm ? "warm" : "ok";
        if (heat !== gov.heat) log("thermal: " + heat + " (status " + status + ", headroom " + headroom + ", battery " + battery + " C)");
        gov.heat = heat;
        if (headroom >= 0) {
            const was = gov.throttled;
            gov.throttled = was ? headroom >= 0.9 : headroom >= 1;
            if (was !== gov.throttled) {
                log("governor: phone " + (gov.throttled ? "throttling" : "no longer throttling") + " (headroom " + headroom + "), in-betweening " + (gov.throttled ? "paused" : "back"));
                applySmooth();
            }
        }
        gov.floor = gfx.auto && heat === "hot" ? 5 : 0;
        if (gov.floor > 0) gov.trial = null;
        if (gov.level < gov.floor) setLevel(gov.floor, "phone " + heat);
        if (window.MvsOverlays && window.MvsOverlays.heat)
            window.MvsOverlays.heat(gfx.heat ? { heat, battery, status, headroom, lowered: gfx.auto && gov.floor > 0 } : { heat: "ok" });
    }
    function setTargetFps(fps) {
        fps = Math.max(0, Math.round(Number(fps) || 0));
        gov.targetRate = fps;
        const rate = fps > 0 ? fps : 30;
        if (player) {
            try {
                player.ruffle().callExternalInterface("setGameObject", "stage.frameRate", rate);
            } catch (e) {
                log("setTargetFps: " + e);
            }
        }
        if (fps > 0 && fps <= 30) {
            window.__mvsInterp = 0;
        } else {
            applySmooth();
        }
        log("frame pacing: target set to " + (fps > 0 ? fps + " fps" : "unlimited"));
    }
    let fpsHooked = false, drawCalls = 0, framesDrawn = 0, fpsEl = null, fpsInterval = 0;
    function hookDraws() {
        if (fpsHooked) return;
        fpsHooked = true;
        for (const ctx of [window.WebGLRenderingContext, window.WebGL2RenderingContext]) {
            if (!ctx) continue;
            for (const name of ["drawElements", "drawArrays"]) {
                const original = ctx.prototype[name];
                ctx.prototype[name] = function () { drawCalls++; return original.apply(this, arguments); };
            }
        }
    }
    function startFrameCounter() {
        if (fpsInterval) return;
        if (!(Number(window.__mvsFrames) >= 0)) hookDraws();
        const tick = () => {
            if (drawCalls > 0) { framesDrawn++; drawCalls = 0; }
            requestAnimationFrame(tick);
        };
        requestAnimationFrame(tick);
        let lastGameFrames = -1, lastDrawn = -1;
        fpsInterval = setInterval(() => {
            let drawn = framesDrawn;
            framesDrawn = 0;
            const pictures = Number(window.__mvsDrawn);
            if (pictures >= 0) {
                if (lastDrawn >= 0) drawn = pictures - lastDrawn;
                lastDrawn = pictures;
            }
            const total = Number(window.__mvsFrames);
            const fps = total >= 0 && lastGameFrames >= 0 ? total - lastGameFrames : drawn;
            if (total >= 0) lastGameFrames = total;
            if (fpsEl && !window.__mvsNoDraw) {
                fpsEl.textContent = fps + " fps" + (gfx.dev
                    ? (governedSmooth() && drawn > fps ? " \u00B7 " + drawn + " drawn" : "") + (gfx.auto && gov.level > 0 ? " \u2193" + gov.level : "")
                    : "");
                fpsEl.style.color = fps >= gov.gameRate * 0.95 ? "#7CFC7C" : fps >= gov.gameRate * 0.7 ? "#FFD34D" : "#FF6B6B";
            }
            try { governorTick(fps); } catch (e) { log("governor: " + e); }
            try { perfTick(fps); } catch (e) { }
            try { mapTick(); } catch (e) { }
            try { if (!document.hidden) checkSmooth(fps, drawn); } catch (e) { log("smooth: " + e); }
        }, 1000);
    }
    const perf = { loaded: false, n: 0, sum: 0, min: 0, max: 0 };
    function perfTick(fps) {
        if (!perf.loaded || document.hidden || appInBackground || window.__mvsNoDraw) return;
        perf.n++;
        perf.sum += fps;
        if (fps > 0 && (perf.min === 0 || fps < perf.min)) perf.min = fps;
        if (fps > perf.max) perf.max = fps;
        if (perf.n < 60) return;
        const wasm = /wasm (\d+) MB/.exec(String(window.__mvsMem || ""));
        send({ t: "ext", n: "mvsPerf", a: [(perf.sum / perf.n).toFixed(2) + "|" + perf.min + "|" + perf.max + "|" + perf.n + "|" + (wasm ? wasm[1] : 0)] });
        perf.n = 0; perf.sum = 0; perf.min = 0; perf.max = 0;
    }
    function mapTick() {
        const asked = window.__mvsMapAskedAt;
        if (!asked || !player) return;
        const ms = Math.round(performance.now() - asked);
        if (ms > 10 * 60000) { window.__mvsMapAskedAt = 0; return; }
        if (String(player.ruffle().callExternalInterface("getGameObject", "world.mapLoadInProgress")) !== "false") return;
        window.__mvsMapAskedAt = 0;
        send({ t: "ext", n: "mvsMapLoad", a: [ms] });
    }
    window.__mvsPerfNow = () => (gov.lastFps || 0) + "|" + ((/wasm (\d+) MB/.exec(String(window.__mvsMem || "")) || [0, 0])[1]);
    function setFpsMeter(on) {
        startFrameCounter();
        if (on) hookDraws();
        if (!on) {
            if (fpsEl) { fpsEl.remove(); fpsEl = null; }
            return;
        }
        if (fpsEl) return;
        fpsEl = document.createElement("div");
        fpsEl.style.cssText = "position:fixed;left:6px;top:6px;z-index:10;padding:2px 6px;border-radius:4px;" +
            "font:bold 13px monospace;color:#7CFC7C;background:rgba(0,0,0,0.55);pointer-events:none";
        fpsEl.textContent = "-- fps";
        document.body.appendChild(fpsEl);
    }
    function applyGraphics() {
        if (!gfx.auto) { gov.level = 0; gov.floor = 0; }
        setPixelRatio(gfx.dpr, governedScale());
        applySmooth();
        if (player) player.style.filter = colourFilters[gfx.filter] || "";
        applyQuality();
        setFpsMeter(!!gfx.fps);
    }
    function setGraphics(msg) {
        gfx = {
            dpr: Number(msg.dpr) || 0,
            quality: String(msg.quality || ""),
            filter: String(msg.filter || "natural"),
            fps: !!msg.fps,
            auto: msg.auto !== false,
            smooth: Math.max(0, Number(msg.smooth) || 0),
            dev: !!msg.dev,
            heat: msg.heat !== false
        };
        try { applyGraphics(); } catch (e) { log("graphics: " + e); }
        if (lastThermal) { try { setThermal(lastThermal); } catch (e) { } }
    }
    function send(msg) {
        const text = JSON.stringify(msg);
        if (socket && socket.readyState === 1) socket.send(text);
        else if (backlog.length < 500) backlog.push(text);
    }
    function log(message) {
        console.log("[skua]", message);
        send({ t: "log", m: String(message) });
    }
    function setStatus(text) {
        const el = statusEl();
        if (!el) return;
        el.textContent = text || "";
        el.style.display = text ? "block" : "none";
    }
    window.__skuaExt = function (name, args) {
        if (name === "loaded") {
            if (!perf.loaded) send({ t: "ext", n: "mvsGameLoad", a: [Math.round(performance.now())] });
            perf.loaded = true;
        }
        if (name === "loaded") { setStatus(""); setTimeout(applyQuality, 0); setTimeout(applyTouchUi, 0); setTimeout(defaultCrowd, 0); setTimeout(defaultUntargetSelf, 0); }
        if (name === "mvsMenu") { try { window.MvsOverlays.menu(args); } catch (e) { log("menu: " + e); } }
        if (name === "mvsDrops") { try { window.MvsOverlays.dropsList(args); } catch (e) { log("drops: " + e); } }
        if (name === "mvsOption") {
            try {
                let raw = args;
                while (Array.isArray(raw)) raw = raw[0];
                const m = /^(\w+):(on|off)$/.exec(String(raw));
                if (m) onMvsOption(m[1], m[2]);
            } catch (e) { log("option: " + e); }
        }
        send({ t: "ext", n: String(name), a: Array.from(args || []) });
        return undefined;
    };
    let keyboardWanted = false;
    function focusKeyboard(p) {
        setTimeout(() => {
            if (!keyboardWanted) return;
            if (!p.isVirtualKeyboardFocused()) p.virtualKeyboard.focus({ preventScroll: true });
            send({ t: "keyboard" });
        }, 0);
    }
    window.__skuaKeyboard = {
        openVirtualKeyboard(p) {
            keyboardWanted = true;
            if (!p.__skuaKeyboard) {
                p.__skuaKeyboard = true;
                p.container.addEventListener("mousedown", e => {
                    if (keyboardWanted && p.isVirtualKeyboardFocused()) e.preventDefault();
                });
                p.container.addEventListener("focus", () => focusKeyboard(p));
            }
            focusKeyboard(p);
        },
        closeVirtualKeyboard(p) {
            keyboardWanted = false;
            if (p.isVirtualKeyboardFocused()) p.container.focus({ preventScroll: true });
        }
    };
    let streamer = null;
    let streamerShown = "";
    let streamerTimer = 0;
    function applyStreamer(on) {
        const r = player && player.ruffle();
        if (!r || r.callExternalInterface("isNull", "world.myAvatar.pMC.pname") !== "false") return;
        const get = path => { try { return JSON.parse(r.callExternalInterface("getGameObject", path)); } catch (e) { return null; } };
        const set = (path, text) => {
            const current = get(path);
            if (typeof current === "string" && current !== text) r.callExternalInterface("setGameObject", path, text);
        };
        const real = String(get("world.myAvatar.objData.strUsername") || "");
        if (!real) return;
        const guild = get("world.myAvatar.objData.guild.Name");
        const name = on ? streamer.name : real;
        const tag = get("world.myAvatar.pMC.pname.ti.text");
        set("world.myAvatar.pMC.pname.ti.text", (String(tag).startsWith("<AFK> ") ? "<AFK> " : "") + name);
        set("world.myAvatar.pMC.pname.tg.text", on ? "< " + streamer.guild + " >" : guild ? "< " + guild + " >" : "");
        set("world.rootClass.ui.mcPortrait.strName.text", name.toUpperCase());
        const shown = on ? real : streamerShown;
        if (shown && get("world.rootClass.ui.mcPortraitTarget.strName.text") === shown.toUpperCase())
            set("world.rootClass.ui.mcPortraitTarget.strName.text", name.toUpperCase());
        if (!on) {
            try {
                const newChat = get("litePreference.data.bChatUI") === true;
                r.callExternalInterface("setGameObject", "ui.nc.visible", newChat);
                r.callExternalInterface("setGameObject", "ui.mcInterface.t1.visible", !newChat);
                r.callExternalInterface("setGameObject", "ui.mcInterface.te.visible", false);
            } catch (e) { }
        }
    }
    function setStreamer(msg) {
        const next = msg.on ? { name: String(msg.name), guild: String(msg.guild) } : null;
        if (JSON.stringify(next) === JSON.stringify(streamer)) return;
        streamer = next;
        clearInterval(streamerTimer);
        streamerTimer = 0;
        const beat = () => {
            const r = player && player.ruffle();
            if (!r) return;
            r.callExternalInterface("setGameObject", "litePreference.data.mvsStreamer",
                streamer ? streamer.name + "	" + streamer.guild + "	" + Date.now() : "");
        };
        try {
            if (streamer) {
                streamerShown = streamer.name;
                beat();
                streamerTimer = setInterval(() => { try { beat(); } catch (e) { } }, 1000);
            } else {
                beat();
                applyStreamer(false);
                setTimeout(() => { try { if (!streamer) applyStreamer(false); } catch (e) { } }, 80);
            }
        } catch (e) { log("streamer: " + e); }
    }
    function setMuted(reason, on) {
        try {
            if (on) muteReasons.add(reason); else muteReasons.delete(reason);
            const muted = muteReasons.size > 0;
            syncAudioSuspend(muted);
            const r = player && player.ruffle();
            if (!r) return;
            if (muted) {
                if (mutedVolume === null) { mutedVolume = r.volume; r.volume = 0; }
            } else if (mutedVolume !== null) {
                r.volume = mutedVolume;
                mutedVolume = null;
            }
        } catch (e) { log("mute: " + e); }
    }
    let appInBackground = false;
    window.__mvsUpdateNoDraw = function () {
        window.__mvsNoDraw = !!(window.__mvsBotOnly || appInBackground);
        applySmooth();
    };
    function handle(msg) {
        switch (msg.t) {
            case "call": {
                let reply;
                try {
                    if (!player) throw new Error("player not ready");
                    const v = player.ruffle().callExternalInterface(msg.f, ...(msg.a || []));
                    reply = { t: "ret", id: msg.id, v: v === undefined ? null : v };
                } catch (e) {
                    reply = { t: "ret", id: msg.id, e: String((e && e.message) || e) };
                }
                send(reply);
                break;
            }
            case "batch": {
                let reply;
                try {
                    if (!player) throw new Error("player not ready");
                    const r = player.ruffle();
                    const out = (msg.c || []).map(c => {
                        try {
                            const v = r.callExternalInterface(c.f, ...(c.a || []));
                            return { v: v === undefined ? null : v };
                        } catch (e) {
                            return { e: String((e && e.message) || e) };
                        }
                    });
                    reply = { t: "ret", id: msg.id, v: out };
                } catch (e) {
                    reply = { t: "ret", id: msg.id, e: String((e && e.message) || e) };
                }
                send(reply);
                break;
            }
            case "botOnly":
                try { window.MvsOverlays.botOnly(!!msg.v, false); } catch (e) { log("botOnly: " + e); }
                window.__mvsUpdateNoDraw();
                break;
            case "background":
                appInBackground = !!msg.v;
                window.__mvsUpdateNoDraw();
                setMuted("app", appInBackground);
                if (!!window.__mvsBackground !== appInBackground) {
                    window.__mvsBackground = appInBackground;
                    try { document.dispatchEvent(new Event("visibilitychange")); } catch (e) { log("background: " + e); }
                }
                break;
            case "reload":
                location.reload();
                break;
            case "quick":
                try { window.__quick.toggle(); } catch (e) { log("quick: " + e); }
                break;
            case "gameopts":
                try { window.__gameOpts.toggle(); } catch (e) { log("gameopts: " + e); }
                break;
            case "quality":
                try { player.ruffle().config = Object.assign({}, player.ruffle().config, { quality: msg.v }); } catch (e) { log(e); }
                break;
            case "visible":
                document.getElementById("player").style.visibility = msg.v ? "visible" : "hidden";
                break;
            case "streamer":
                setStreamer(msg);
                break;
            case "graphics":
                setGraphics(msg);
                break;
            case "drops":
                try { window.MvsOverlays.toggleDrops(); } catch (e) { log("drops: " + e); }
                break;
            case "thermal":
                try { setThermal(msg); } catch (e) { log("thermal: " + e); }
                break;
            case "targetFps":
                try { setTargetFps(msg.fps); } catch (e) { log("targetFps: " + e); }
                break;
            case "scriptStatus":
                try { window.MvsOverlays.scriptStatus(msg); } catch (e) { log("scriptStatus: " + e); }
                break;
        }
    }
    function connect() {
        socket = new WebSocket(base.replace(/^http/, "ws") + "ws/ctl");
        socket.onopen = () => {
            const pending = backlog;
            backlog = [];
            pending.forEach(t => socket.send(t));
        };
        socket.onmessage = e => {
            try { handle(JSON.parse(e.data)); } catch (err) { log("bad message: " + err); }
        };
        socket.onclose = () => setTimeout(connect, 1000);
    }
    async function boot() {
        if (window.MvsOverlays) window.MvsOverlays.init(() => player);
        connect();
        let cfg = { socketProxy: [], quality: "medium", frameRate: 0 };
        try {
            cfg = await (await nativeFetch(base + "config.json")).json();
        } catch (e) {
            log("config load failed: " + e);
        }
        touchRowH = Number(cfg.touchRowH) || 0;
        const consts = {};
        if (window.__mvsOpts.mvsLongDash) consts["MoveInput.DASH_PX"] = 540;
        if (touchRowH > 0 && window.__mvsOpts.mvsBigUi) {
            consts["LPFFrameInvAQ.ROW_H"] = touchRowH;
        }
        window.__mvsAs3Consts = consts;
        window.__arglaxNoBatch = true;
        const keep = String(cfg.engineMemory || "fork").toLowerCase();
        const kept = name => keep === "fork" || keep.split(",").indexOf(name) >= 0;
        window.__mvsUpstreamLibraries = !kept("libs");
        window.__mvsStrongDicts = !kept("dicts");
        window.__mvsStrongHandlers = !kept("handlers");
        log("engine memory features kept on: " + (["libs", "dicts", "handlers"].filter(kept).join(", ") || "none (stock Ruffle)"));
        window.addEventListener("error", e => {
            log("page error: " + e.message);
            if (/RuntimeError|panicked/.test(String(e.message))) {
                let last = 0;
                try { last = Number(sessionStorage.getItem("mvsCrashReload")) || 0; } catch (err) { }
                if (Date.now() - last > 180000) {
                    try { sessionStorage.setItem("mvsCrashReload", String(Date.now())); } catch (err) { }
                    log("game engine crashed (last memory report: " + (window.__mvsMem || "none") + "); reloading the game");
                    send({ t: "ext", n: "mvsEngineCrash", a: [window.__mvsPerfNow() + "\n" + String(e.message).replace(/\s+/g, " ").slice(0, 400) + "\n" + String(e.error && e.error.stack || "").slice(0, 6000)] });
                    setTimeout(() => location.reload(), 1500);
                }
            }
        });
        setInterval(() => { if (window.__mvsMem) log("memory: " + window.__mvsMem); }, 60000);
        window.addEventListener("unhandledrejection", e => log("unhandled rejection: " + (e.reason && e.reason.message || e.reason)));
        document.addEventListener("visibilitychange", () => setMuted("page", document.hidden));
        screenRatioCap = cfg.maxPixelRatio > 0 ? cfg.maxPixelRatio : 0;
        if (cfg.maxPixelRatio > 0 && window.devicePixelRatio > cfg.maxPixelRatio) {
            const cap = cfg.maxPixelRatio;
            Object.defineProperty(window, "devicePixelRatio", { get: () => cap, configurable: true });
        }
        setStatus("Loading game client...");
        const ruffle = window.RufflePlayer.newest();
        player = ruffle.createPlayer();
        document.getElementById("player").appendChild(player);
        try { applyGraphics(); } catch (e) { log("graphics: " + e); }
        const options = {
            url: base + "skua.swf",
            autoplay: "on",
            unmuteOverlay: "hidden",
            splashScreen: false,
            contextMenu: "off",
            letterbox: "on",
            scale: "showAll",
            logLevel: "warn",
            allowScriptAccess: true,
            allowNetworking: "all",
            openUrlMode: "allow",
            warnOnUnsupportedContent: false,
            showSwfDownload: false,
            maxExecutionDuration: 30,
            backgroundExecutionMode: "mainThread",
            quality: cfg.quality || "medium",
            socketProxy: cfg.socketProxy || []
        };
        if (cfg.frameRate > 0) options.frameRate = cfg.frameRate;
        if (cfg.preferredRenderer) options.preferredRenderer = cfg.preferredRenderer;
        try {
            await player.ruffle().load(options);
            if (gov.targetRate > 0) setTargetFps(gov.targetRate);
        } catch (e) {
            log("start failed: " + e);
            setStatus("The game couldn't start. Close MVS and open it again.");
            log("load failed: " + e);
        }
    }
    window.SkuaBridge = { boot, log };
})();
