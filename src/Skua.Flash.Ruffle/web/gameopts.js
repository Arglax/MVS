(function () {
    "use strict";
    const APP = window.__mvaOpts ? "mva" : "mvs";
    const GROUPS = [
        ["Controls", ["Mobile Controls", "Mobile UI Size", "Auto-Untarget Self", "Auto-Untarget Dead Targets"]],
        ["Smoother game", ["Max FPS", "Crowd Mode", "Hide Players", "Disable Monster Animations", "Disable Skill Animations",
            "Show Your Skill Animations Only", "Disable Weapon Animations", "Hide Ground Items"]],
        ["Screen", ["Disable Quest Popup", "Disable Damage Numbers", "Visual Skill CDs", "Show Monster Type"]]
    ];
    const CYCLES = new Set(["Mobile Controls", "Mobile UI Size", "Max FPS", "Crowd Mode"]);
    function call(name, ...args) {
        try {
            const p = document.querySelector("ruffle-player");
            return p && p.ruffle ? p.ruffle().callExternalInterface(name, ...args) : null;
        } catch (e) { return null; }
    }
    function parse(raw) {
        try { const v = raw ? JSON.parse(raw) : null; return v && v.rows ? v : null; } catch (e) { return null; }
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
    let root = null, data = null, tipTimer = 0;
    function css() {
        if (document.getElementById("gopt-css")) return;
        const s = document.createElement("style");
        s.id = "gopt-css";
        s.textContent = `
.gopt { position: fixed; z-index: 60; display: flex; flex-direction: column; box-sizing: border-box;
  border: 2px solid #C9A042; border-radius: 10px; background: linear-gradient(180deg, rgba(36,25,13,.96), rgba(18,13,8,.95));
  box-shadow: 0 0 0 1px #2A1A06, inset 0 0 0 1px rgba(255,224,140,.4), inset 0 1px 0 rgba(255,240,190,.3), 0 6px 24px rgba(0,0,0,.7);
  color: #EDE3CC; font: 13px/1.25 system-ui, sans-serif; user-select: none; -webkit-user-select: none; touch-action: pan-y; }
.gopt .hd { flex: none; display: flex; align-items: center; padding: 6px 8px 6px 12px; border-bottom: 1px solid rgba(201,160,66,.6);
  background: linear-gradient(180deg, rgba(92,60,18,.6), rgba(40,26,8,.2)); border-radius: 8px 8px 0 0; }
.gopt .hd b { flex: 1; color: #F2CF68; font-size: 15px; letter-spacing: .03em; text-shadow: 0 1px 0 #000; }
.gopt .x { width: 34px; height: 30px; border: 1px solid #8A6A2E; border-radius: 7px; background: linear-gradient(#7A1E14, #4A0F09);
  color: #F6E2B0; font: 700 16px system-ui, sans-serif; padding: 0; }
.gopt .bd { flex: 1; overflow-y: auto; padding: 6px 10px 10px; overscroll-behavior: contain; }
.gopt h4 { margin: 8px 2px 5px; color: #C9A447; font-size: 11px; letter-spacing: .12em; text-transform: uppercase; }
.gopt .grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(150px, 1fr)); gap: 6px; }
.gopt .t { display: flex; flex-direction: column; justify-content: center; gap: 4px; min-height: 52px; padding: 6px 8px; text-align: left;
  border: 1px solid #6E5424; border-radius: 8px; background: rgba(255,255,255,.05); color: #EDE3CC; font: 600 12px/1.2 system-ui, sans-serif; }
.gopt .t .v { align-self: flex-start; padding: 1px 9px; border-radius: 9px; font-size: 11px; font-weight: 700; background: #3A2C18; color: #A89C84; }
.gopt .t.on { border-color: #C9A042; background: rgba(201,160,66,.16); }
.gopt .t.on .v { background: linear-gradient(#E8C15A, #B8913A); color: #1A1206; }
.gopt .t.cy .v { background: linear-gradient(#7A1E14, #4A0F09); color: #F6E2B0; }
.gopt .t:active { transform: scale(.97); }
.gopt .tip { flex: none; min-height: 18px; padding: 6px 12px 8px; border-top: 1px solid rgba(201,160,66,.35); color: #CDBF9F; font-size: 11.5px; white-space: pre-line; }
.gopt .none { padding: 16px 4px; color: #A89C84; }`;
        document.head.appendChild(s);
    }
    function place() {
        if (!root) return;
        const b = gameBox();
        const w = Math.min(b.w - 16, 640), h = Math.min(b.h - 16, Math.max(260, b.h * 0.9));
        root.style.width = w + "px";
        root.style.height = h + "px";
        root.style.left = Math.round(b.x + (b.w - w) / 2) + "px";
        root.style.top = Math.round(b.y + (b.h - h) / 2) + "px";
    }
    function value(name) {
        if (CYCLES.has(name)) {
            const label = data.cycles && data.cycles[name];
            return label ? String(label).replace(/^[^:]*:\s*/, "") : null;
        }
        const row = data.rows.find(r => r.n === name);
        return row ? !!row.on : null;
    }
    function render() {
        const bd = root.querySelector(".bd");
        if (!data) { bd.innerHTML = `<div class="none">The game's options show up once you are in the game.</div>`; return; }
        let html = "";
        for (const [title, names] of GROUPS) {
            const tiles = names.map(n => {
                const v = value(n);
                if (v === null) return "";
                if (CYCLES.has(n)) return `<button class="t cy" data-n="${esc(n)}">${esc(n)}<span class="v">${esc(v)}</span></button>`;
                return `<button class="t${v ? " on" : ""}" data-n="${esc(n)}">${esc(n)}<span class="v">${v ? "On" : "Off"}</span></button>`;
            }).join("");
            if (tiles) html += `<h4>${esc(title)}</h4><div class="grid">${tiles}</div>`;
        }
        bd.innerHTML = html || `<div class="none">This game version lists none of these options.</div>`;
    }
    function describe(name) {
        const row = data && data.rows.find(r => r.n === name);
        const tip = root.querySelector(".tip");
        tip.textContent = row && row.d ? name + ": " + row.d : name + (CYCLES.has(name) ? ": tap to step through its values." : "");
    }
    function open() {
        css();
        data = parse(call(APP + "GameOptions"));
        if (!root) {
            root = document.createElement("div");
            root.className = "gopt";
            root.innerHTML = `<div class="hd"><b>Game options</b><button class="x" title="Close">✕</button></div>` +
                `<div class="bd"></div><div class="tip">Tap to switch. Hold to read what it does.</div>`;
            root.querySelector(".x").addEventListener("click", close);
            const bd = root.querySelector(".bd");
            let held = false;
            bd.addEventListener("pointerdown", e => {
                const t = e.target.closest(".t");
                if (!t) return;
                held = false;
                clearTimeout(tipTimer);
                tipTimer = setTimeout(() => { held = true; describe(t.dataset.n); }, 450);
            });
            bd.addEventListener("pointerup", () => clearTimeout(tipTimer));
            bd.addEventListener("pointercancel", () => clearTimeout(tipTimer));
            bd.addEventListener("click", e => {
                const t = e.target.closest(".t");
                if (!t || held) { held = false; return; }
                const fresh = parse(call(APP + "GameOption", t.dataset.n));
                if (fresh) data = fresh;
                render();
                describe(t.dataset.n);
            });
            document.body.appendChild(root);
            window.addEventListener("resize", place);
        }
        root.style.display = "";
        render();
        place();
    }
    function close() { if (root) root.style.display = "none"; }
    window.__gameOpts = { open, close, toggle: () => (root && root.style.display !== "none" ? close() : open()) };
})();
