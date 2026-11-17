(function () {
    "use strict";
    if (window.__quick) return;
    const APP = window.__mvaOpts ? "mva" : "mvs";
    const ACTIONS = [
        ["Show", [["Area List", "Users here", "U"], ["Player HP Bar", "HP bars", "V"], ["Hide Players", "Hide players"],
            ["Hide Monsters", "Hide monsters"], ["Hide UI", "Hide interface"], ["Stats Overview", "Stats"], ["Battle Analyzer", "Battle analyzer"]]],
        ["Fight", [["Target Random Monster", "Target a monster"], ["Cancel Target", "Untarget"], ["Rest", "Rest"], ["Jump", "Jump"],
            ["Decline All Drops", "Decline all drops"], ["Battle Analyzer Toggle", "Analyzer on / off"]]],
        ["Windows", [["Chat", "Chat keyboard"], ["Friends List", "Friends"], ["Friendships UI", "Friendships"], ["Character Panel", "Character"],
            ["Outfits", "Outfits"], ["Quest Log", "Quests"], ["Options", "Options"], ["World Camera", "World camera"]]]
    ];
    const PLACES = [["Battleon", "battleon"], ["Battleon Town", "battleontown"], ["Yulgar's Inn", "yulgar"], ["Forge", "forge"],
        ["Museum", "museum"], ["Swordhaven", "swordhaven"], ["Arcangrove", "arcangrove"], ["Shadowfall", "shadowfall"],
        ["The Lair", "lair"], ["Citadel", "citadel"], ["Doomwood", "doomwood"], ["Tercessuinotlim", "tercessuinotlim"]];
    const MAP_RE = /^[a-z0-9_]{2,40}(-[0-9]{1,6})?$/;
    const MAX_LOG = 40;
    const store = {
        get(k, d) { try { const v = localStorage.getItem(APP + "." + k); return v === null ? d : JSON.parse(v); } catch (e) { return d; } },
        set(k, v) { try { localStorage.setItem(APP + "." + k, JSON.stringify(v)); } catch (e) { } }
    };
    function call(name, ...args) {
        try {
            const p = document.querySelector("ruffle-player");
            return p && p.ruffle ? p.ruffle().callExternalInterface(APP + name, ...args) : null;
        } catch (e) { return null; }
    }
    function esc(t) { return String(t == null ? "" : t).replace(/[&<>"]/g, c => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;" }[c])); }
    function gameBox() {
        const p = document.querySelector("ruffle-player");
        const c = p && p.shadowRoot ? p.shadowRoot.querySelector("canvas") : null;
        const r = (c || p) ? (c || p).getBoundingClientRect() : null;
        if (!r || r.width < 50 || r.height < 50) return { x: 0, y: 0, w: window.innerWidth, h: window.innerHeight };
        const k = Math.min(r.width / 960, r.height / 550), w = 960 * k, h = 550 * k;
        return { x: r.left + (r.width - w) / 2, y: r.top + (r.height - h) / 2, w, h };
    }
    function getLog() { const l = store.get("travelLog", []); return Array.isArray(l) ? l.filter(m => MAP_RE.test(m)) : []; }
    let here = "";
    const dropped = new Set();
    function syncLog() {
        let st = null;
        try { const r = call("QuickState"); st = r ? JSON.parse(r) : null; } catch (e) { st = null; }
        here = st && st.map ? String(st.map) : "";
        const fresh = (st && Array.isArray(st.log) ? st.log.slice() : []).filter(m => !dropped.has(m));
        if (here && !fresh.includes(here) && !dropped.has(here)) fresh.unshift(here);
        const old = getLog();
        const out = [];
        for (const m of fresh.concat(old)) if (MAP_RE.test(m) && !out.includes(m)) out.push(m);
        out.length = Math.min(out.length, MAX_LOG);
        if (out.join() !== old.join()) store.set("travelLog", out);
        return out;
    }
    document.addEventListener("visibilitychange", () => { if (document.hidden) syncLog(); });
    let root = null, tab = store.get("quickTab", "actions"), note = "";
    function css() {
        if (document.getElementById("quick-css")) return;
        const s = document.createElement("style");
        s.id = "quick-css";
        s.textContent = `
.qk { position: fixed; z-index: 60; display: flex; flex-direction: column; box-sizing: border-box;
  border: 2px solid #C9A042; border-radius: 10px; background: linear-gradient(180deg, rgba(36,25,13,.96), rgba(18,13,8,.95));
  box-shadow: 0 0 0 1px #2A1A06, inset 0 0 0 1px rgba(255,224,140,.4), inset 0 1px 0 rgba(255,240,190,.3), 0 6px 24px rgba(0,0,0,.7);
  color: #EDE3CC; font: 13px/1.25 system-ui, sans-serif; user-select: none; -webkit-user-select: none; touch-action: pan-y; }
.qk .hd { flex: none; display: flex; align-items: center; gap: 6px; padding: 6px 8px; border-bottom: 1px solid rgba(201,160,66,.6);
  background: linear-gradient(180deg, rgba(92,60,18,.6), rgba(40,26,8,.2)); border-radius: 8px 8px 0 0; }
.qk .tb { min-height: 34px; padding: 0 14px; border: 1px solid #8A6A2E; border-radius: 8px; background: rgba(255,255,255,.05);
  color: #EDE3CC; font: 700 13px system-ui, sans-serif; }
.qk .tb.on { background: linear-gradient(#E8C15A, #B8913A); color: #1A1206; border-color: #E8C15A; }
.qk .sp { flex: 1; }
.qk .x { width: 34px; height: 34px; border: 1px solid #8A6A2E; border-radius: 7px; background: linear-gradient(#7A1E14, #4A0F09);
  color: #F6E2B0; font: 700 16px system-ui, sans-serif; padding: 0; }
.qk .bd { flex: 1; overflow-y: auto; padding: 6px 10px 10px; overscroll-behavior: contain; }
.qk h4 { margin: 8px 2px 5px; color: #C9A447; font-size: 11px; letter-spacing: .12em; text-transform: uppercase; }
.qk .grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(128px, 1fr)); gap: 6px; }
.qk .t { display: flex; align-items: center; justify-content: space-between; gap: 6px; min-height: 44px; padding: 6px 10px; text-align: left;
  border: 1px solid #6E5424; border-radius: 8px; background: rgba(255,255,255,.05); color: #EDE3CC; font: 600 12.5px/1.2 system-ui, sans-serif; }
.qk .t:active { transform: scale(.97); border-color: #C9A042; background: rgba(201,160,66,.16); }
.qk .t.here { border-color: #C9A042; color: #F2CF68; }
.qk .t kbd { flex: none; padding: 1px 6px; border-radius: 5px; background: #3A2C18; color: #C8B89A; font: 700 11px monospace; }
.qk .t .rm { flex: none; margin: -6px -10px -6px 0; padding: 0 12px; align-self: stretch; display: flex; align-items: center; color: #A89C84; font-size: 15px; }
.qk .t small { display: block; color: #A89C84; font-weight: 400; font-size: 11px; }
.qk .go { display: flex; gap: 6px; }
.qk .go input { flex: 1; min-width: 0; min-height: 40px; box-sizing: border-box; padding: 0 10px; border: 1px solid #8A6A2E; border-radius: 8px;
  background: rgba(0,0,0,.5); color: #EDE3CC; font: 14px system-ui, sans-serif; user-select: text; -webkit-user-select: text; }
.qk .go button { min-width: 64px; }
.qk .ft { flex: none; display: flex; align-items: center; gap: 8px; min-height: 20px; padding: 6px 10px 8px; border-top: 1px solid rgba(201,160,66,.35);
  color: #CDBF9F; font-size: 11.5px; }
.qk .ft span { flex: 1; }
.qk .ft button, .qk .lk { border: 0; background: none; color: #C8B89A; font: 600 11.5px system-ui, sans-serif; text-decoration: underline; padding: 6px 2px; }
.qk .none { padding: 6px 2px; color: #A89C84; }
.qk-fab { position: fixed; z-index: 32; width: 38px; height: 38px; border-radius: 50%; border: 2px solid #C9A042; padding: 0;
  background: rgba(22,17,12,.86); color: #F7C948; font: 400 18px/1 system-ui, sans-serif; touch-action: none;
  box-shadow: 0 0 0 1px #2A1A06, inset 0 0 0 1px rgba(255,224,140,.4), 0 2px 8px rgba(0,0,0,.55); }`;
        document.head.appendChild(s);
    }
    function place() {
        if (!root || root.style.display === "none") return;
        const b = gameBox();
        const w = Math.min(b.w - 16, 600), h = Math.min(b.h - 16, Math.max(260, b.h * 0.9));
        root.style.width = w + "px";
        root.style.height = h + "px";
        root.style.left = Math.round(b.x + (b.w - w) / 2) + "px";
        root.style.top = Math.round(b.y + (b.h - h) / 2) + "px";
    }
    function render() {
        for (const b of root.querySelectorAll(".tb")) b.classList.toggle("on", b.dataset.tab === tab);
        const bd = root.querySelector(".bd");
        let html = "";
        if (tab === "actions") {
            for (const [title, list] of ACTIONS) {
                html += `<h4>${title}</h4><div class="grid">` + list.map(([n, label, key]) =>
                    `<button class="t" data-act="${esc(n)}"><span>${esc(label)}</span>${key ? `<kbd>${key}</kbd>` : ""}</button>`).join("") + `</div>`;
            }
        } else {
            const log = syncLog();
            html += `<h4>Go to a map</h4><div class="go"><input type="text" placeholder="Map name, like /join" autocomplete="off" autocapitalize="off" spellcheck="false" maxlength="48">` +
                `<button class="tb" data-go="1">Go</button></div>`;
            html += `<h4>Places</h4><div class="grid">` + PLACES.map(([label, m]) =>
                `<button class="t${m === here ? " here" : ""}" data-map="${m}"><span>${esc(label)}</span></button>`).join("") + `</div>`;
            html += `<h4>Travel log</h4>` + (log.length ? `<div class="grid">` + log.map(m =>
                `<button class="t${m === here ? " here" : ""}" data-map="${esc(m)}"><span>${esc(m)}${m === here ? "<small>you are here</small>" : ""}</span><span class="rm" data-rm="${esc(m)}">×</span></button>`).join("") +
                `</div><button class="lk" data-clear="1">Clear the travel log</button>`
                : `<div class="none">Maps you visit show up here.</div>`);
        }
        bd.innerHTML = html;
        root.querySelector(".ft span").textContent = note || (tab === "actions" ? "The game's own shortcut actions." : "Tap a map to join it. The game allows one trip every 5 seconds.");
        root.querySelector(".ft button").textContent = "Floating button: " + (store.get("quickFab", false) ? "On" : "Off");
    }
    function travel(map) {
        const m = String(map || "").trim().toLowerCase().replace(/^\/?join\s+/, "");
        if (!MAP_RE.test(m)) { note = "That is not a map name."; render(); return; }
        const r = call("Travel", m);
        if (r === "ok") { note = ""; close(); return; }
        note = r === "bad" ? "That is not a map name." : "Travel works once you are in the game.";
        render();
    }
    function open(which) {
        css();
        if (which) tab = which;
        if (!root) {
            root = document.createElement("div");
            root.className = "qk";
            root.innerHTML = `<div class="hd"><button class="tb" data-tab="actions">Actions</button><button class="tb" data-tab="travel">Travel</button>` +
                `<span class="sp"></span><button class="x" title="Close">✕</button></div><div class="bd"></div>` +
                `<div class="ft"><span></span><button></button></div>`;
            root.querySelector(".x").addEventListener("click", close);
            root.querySelector(".ft button").addEventListener("click", () => { store.set("quickFab", !store.get("quickFab", false)); fab(); render(); });
            root.addEventListener("keydown", e => { if (e.key === "Enter" && e.target.tagName === "INPUT") travel(e.target.value); });
            root.addEventListener("click", e => {
                const t = e.target;
                const tb = t.closest("[data-tab]");
                if (tb) { tab = tb.dataset.tab; store.set("quickTab", tab); note = ""; render(); return; }
                if (t.dataset.rm) { dropped.add(t.dataset.rm); store.set("travelLog", getLog().filter(m => m !== t.dataset.rm)); render(); return; }
                if (t.dataset.clear) { for (const m of syncLog()) dropped.add(m); store.set("travelLog", []); render(); return; }
                if (t.closest("[data-go]")) { travel(root.querySelector(".go input").value); return; }
                const a = t.closest("[data-act]");
                if (a) {
                    if (call("Quick", a.dataset.act) === true) { note = ""; close(); }
                    else { note = "Actions work once you are in the game."; render(); }
                    return;
                }
                const m = t.closest("[data-map]");
                if (m) travel(m.dataset.map);
            });
            document.body.appendChild(root);
            window.addEventListener("resize", () => { place(); placeFab(); });
        }
        note = "";
        root.style.display = "";
        render();
        place();
    }
    function close() { if (root) root.style.display = "none"; }
    let fabEl = null;
    function placeFab(x, y) {
        if (!fabEl || fabEl.style.display === "none") return;
        const b = gameBox(), pos = store.get("quickFabPos", null);
        const fx = x != null ? x : pos ? b.x + pos.x * b.w : b.x + b.w - 46;
        const fy = y != null ? y : pos ? b.y + pos.y * b.h : b.y + b.h * 0.3;
        fabEl.style.left = Math.round(Math.min(Math.max(b.x, fx), b.x + b.w - 38)) + "px";
        fabEl.style.top = Math.round(Math.min(Math.max(b.y, fy), b.y + b.h - 38)) + "px";
    }
    function fab() {
        const on = store.get("quickFab", false);
        if (!on) { if (fabEl) fabEl.style.display = "none"; return; }
        css();
        if (!fabEl) {
            fabEl = document.createElement("button");
            fabEl.className = "qk-fab";
            fabEl.textContent = "⚡";
            fabEl.title = "Quick actions and travel";
            let start = null;
            fabEl.addEventListener("pointerdown", e => {
                start = { x: e.clientX, y: e.clientY, l: fabEl.offsetLeft, t: fabEl.offsetTop, moved: false };
                try { fabEl.setPointerCapture(e.pointerId); } catch (err) { }
            });
            fabEl.addEventListener("pointermove", e => {
                if (!start) return;
                const dx = e.clientX - start.x, dy = e.clientY - start.y;
                if (!start.moved && Math.abs(dx) + Math.abs(dy) < 8) return;
                start.moved = true;
                placeFab(start.l + dx, start.t + dy);
            });
            fabEl.addEventListener("pointerup", () => {
                if (!start) return;
                if (start.moved) {
                    const b = gameBox();
                    store.set("quickFabPos", { x: (fabEl.offsetLeft - b.x) / b.w, y: (fabEl.offsetTop - b.y) / b.h });
                } else if (root && root.style.display !== "none") close(); else open();
                start = null;
            });
            fabEl.addEventListener("pointercancel", () => { start = null; });
            document.body.appendChild(fabEl);
        }
        fabEl.style.display = "";
        placeFab();
    }
    window.addEventListener("mvaloaded", () => placeFab());
    if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", fab); else fab();
    window.__quick = { open, close, back: () => { const up = !!root && root.style.display !== "none"; close(); return up; }, toggle: () => (root && root.style.display !== "none" ? close() : open()) };
})();
