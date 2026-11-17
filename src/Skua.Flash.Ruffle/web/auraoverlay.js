(() => {
    "use strict";
    if (window.__mvsAuraOverlay) return;
    window.__mvsAuraOverlay = true;
    const POLL_MS = 250, DRAW_MS = 100, DESC_MS = 3000, ICON = 40;
    const store = {
        get(k, d) { try { const v = localStorage.getItem("mvs." + k); return v === null ? d : JSON.parse(v); } catch (e) { return d; } },
        set(k, v) { try { localStorage.setItem("mvs." + k, JSON.stringify(v)); } catch (e) { } }
    };
    const SPECIAL = [
        { re: /decay|heal(ing)?\s*block|no\s*heal|anti-?heal|cauteri[sz]e/i, kind: "noheal", tag: "NO HEAL",
          note: "Healing is blocked or turned into damage while this lasts. Hold healing skills and potions until it ends." },
        { re: /^focus$|taunt/i, kind: "enrage", tag: "TAUNT",
          note: "Forced to attack whoever taunted it (Scroll of Enrage: 6 seconds). Taunts don't work on players." },
        { re: /^reckless$/i, kind: "enrage", tag: "RECKLESS",
          note: "Attacks recklessly: all its outgoing damage is 15% lower (Scroll of Enrage: 10 seconds)." },
        { re: /enrage/i, kind: "enrage", tag: "ENRAGE",
          note: "Enraged: expect bigger hits until it ends." }
    ];
    const PILL = { buff: "#E8B931", hot: "#4CC26A", dot: "#E5484D", debuff: "#8A6CFF", scroll: "#F08A24", neutral: "#8E8E8E" };
    const GROUPS = ["enrage", "scrolls", "potions", "debuffs", "other"];
    const GROUP_NAMES = { enrage: "Scroll of Enrage", scrolls: "Scrolls", potions: "Potions", debuffs: "Debuffs", other: "Everything else" };
    const FILTERS = [["all", "All"], ["buff", "Buffs"], ["debuff", "Debuffs"], ["sp", "Scrolls & potions"], ["q", "Quick"]];
    const POTION_RE = /tonic|elixir|potion|draught|brew|philter|felicitous|tincture|serum/i;
    function clauseKind(c) {
        if (/convert\w*\s+heal\w*\s+(in)?to\s+damage|\bheal\w*\b.*\bas\s+damage\b/i.test(c)) return "debuff";
        if (/\bdots?\b|damage(?:\s+\w+){0,2}\s+over time|\bburn|\bbleed|\bpoison|\bdeals?\b.*\bevery\b/i.test(c)) return "dot";
        if (/\bhots?\b|\bregen|heal\w*(?:\s+\w+){0,2}\s+over time|\bheals?\b.*\b(every|over)\b/i.test(c)) return "hot";
        if (/\bstun|heal(ing)?[\s-]*block|\bdecay\b|\bcannot\b|can't|\bblocked\b|\bslow/i.test(c)) return "debuff";
        const m = c.match(/([+\-−])\s*\d/);
        if (!m) return "neutral";
        let good = m[1] === "+";
        if (/\bcosts?\b|cooldown|consumption|\btaken\b|incoming damage/i.test(c)) good = !good;
        return good ? "buff" : "debuff";
    }
    const clauseCache = new Map();
    function clauses(text) {
        const key = String(text || "");
        let v = clauseCache.get(key);
        if (!v) {
            const isStat = t => /\d/.test(t) && t.length <= 40;
            v = [];
            for (const part of key.split(";").map(s => s.trim()).filter(Boolean)) {
                const bits = part.split(",").map(s => s.trim()).filter(Boolean);
                for (const t of (bits.length > 1 && bits.every(isStat) ? bits : [part])) v.push({ t, k: clauseKind(t) });
            }
            if (clauseCache.size > 500) clauseCache.clear();
            clauseCache.set(key, v);
        }
        return v;
    }
    function norm(s) { return String(s || "").toLowerCase().replace(/[^a-z0-9 ]/g, "").replace(/\s+/g, " ").trim(); }
    function lev(a, b) {
        const d = [];
        for (let i = 0; i <= a.length; i++) d[i] = [i];
        for (let j = 1; j <= b.length; j++) d[0][j] = j;
        for (let i = 1; i <= a.length; i++) for (let j = 1; j <= b.length; j++) {
            d[i][j] = Math.min(d[i - 1][j] + 1, d[i][j - 1] + 1, d[i - 1][j - 1] + (a[i - 1] === b[j - 1] ? 0 : 1));
        }
        return d[a.length][b.length];
    }
    function matchScore(q, name) {
        const n = norm(name);
        if (!q || !n) return 0;
        if (n === q) return 1000;
        if (n.startsWith(q)) return 800 - (n.length - q.length);
        const words = n.split(" ");
        if (words.some(w => w.startsWith(q))) return 700;
        if (n.includes(q)) return 600;
        let i = 0, first = -1, last = -1;
        for (let p = 0; p < n.length && i < q.length; p++) {
            if (n[p] === q[i]) { if (first < 0) first = p; last = p; i++; }
        }
        if (i === q.length) return 500 - ((last - first + 1) - q.length);
        const dist = Math.min(lev(q, n), ...words.map(w => lev(q, w)));
        if (dist <= Math.max(1, Math.floor(q.length / 3))) return 400 - 50 * dist;
        return 0;
    }
    function knownNames() {
        const out = new Map();
        const add = n => { const k = String(n).toLowerCase(); if (n && !out.has(k)) out.set(k, String(n)); };
        if (wiki && wiki.auras) Object.keys(wiki.auras).forEach(add);
        if (wiki && wiki.fx) for (const c of Object.values(wiki.fx)) Object.keys(c).forEach(add);
        Object.keys(seen).forEach(add);
        return [...out.values()];
    }
    function bestMatches(text, limit) {
        const q = norm(text);
        if (!q) return [];
        return knownNames().map(n => ({ n, s: matchScore(q, n) })).filter(x => x.s > 0)
            .sort((a, b) => b.s - a.s || a.n.length - b.n.length).slice(0, limit);
    }
    function getPins() { return String(store.get("auraPins", "")).split(";").map(s => s.trim()).filter(Boolean); }
    function setPins(p) { store.set("auraPins", p.join(";")); }
    function getOrder() {
        const o = String(store.get("auraOrder", GROUPS.join(","))).split(",").filter(k => GROUPS.includes(k));
        for (const g of GROUPS) if (!o.includes(g)) o.push(g);
        return o;
    }
    function pinIndex(a, pins, text) {
        const nn = norm(a.name), lname = a.name.toLowerCase(), lt = String(text || "").toLowerCase();
        for (let i = 0; i < pins.length; i++) if (norm(pins[i]) === nn) return i;
        for (let i = 0; i < pins.length; i++) {
            const p = pins[i].toLowerCase();
            if (lname.includes(p) || lt.includes(p)) return i;
        }
        return -1;
    }
    let seen = store.get("auraSeen", {});
    let descs = {}, lastDesc = 0, lastDescRead = 0;
    const descMiss = new Set();
    function paused() {
        return document.hidden || !!window.__mvsNoDraw || !!window.__mvsBackground;
    }
    let clockOffset = null;
    const offSamples = new Map();
    const open = new Set();
    const DETAIL_DEFAULT = 3, DETAIL_MIN = 2;
    function maxDetail() {
        const n = Math.round(Number(store.get("auraMaxDetail", DETAIL_DEFAULT)));
        return Number.isFinite(n) ? Math.max(DETAIL_MIN, n) : DETAIL_DEFAULT;
    }
    const moreOpen = { me: false, t: false };
    const icons = {};
    let iconBudget = 0;
    let wiki = null, wikiLoading = false;
    const wikiCache = {};
    let state = { mine: [], theirs: [], target: null, me: "", cls: "" };
    function call(name, ...args) {
        try {
            const p = document.querySelector("ruffle-player");
            return p && p.ruffle ? p.ruffle().callExternalInterface(name, ...args) : null;
        } catch (e) {
            return null;
        }
    }
    function read(path) {
        const v = call("getGameObject", path);
        if (v == null) return null;
        try { return JSON.parse(v); } catch (e) { return null; }
    }
    function esc(t) { return String(t == null ? "" : t).replace(/[&<>"]/g, c => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;" }[c])); }
    function loadWiki() {
        if (!wiki && window.__mvsAuraData) wiki = window.__mvsAuraData;
        if (wiki || wikiLoading) return;
        wikiLoading = true;
        fetch("auradata.json").then(r => r.ok ? r.json() : null).then(j => { wiki = j || { classes: {} }; })
            .catch(() => { wiki = { classes: {} }; });
    }
    function normClass(n) { return String(n || "").toLowerCase().replace(/\([^)]*\)/g, "").replace(/\s+/g, " ").trim(); }
    function limitIn(text) {
        const m = text.match(/up\s+to\s+(\d+)\s*(?:times|stacks)/i) || text.match(/max(?:imum)?(?:\s+of)?\s+(\d+)\s+stacks?/i)
            || text.match(/(\d+)\s+stacks?\s+max/i) || text.match(/stacks?\s+up\s+to\s+(\d+)/i);
        return m && +m[1] > 1 ? +m[1] : 0;
    }
    function wikiInfo(aura, cls) {
        const key = cls + "|" + aura;
        if (key in wikiCache) return wikiCache[key];
        if (!wiki) return null;
        const skills = wiki.classes[normClass(cls)] || [];
        let base = aura.replace(/^[IVXLC0-9]+\s*-\s*/, "");
        const reversed = /\(reversed\)/i.test(base);
        const core = base.replace(/\s*\(reversed\)\s*/i, "").replace(/^the\s+/i, "").trim();
        const coreRe = new RegExp("^(?:the\\s+)?" + core.replace(/[.*+?^${}()|[\]\\]/g, "\\$&") + "\\s*:\\s*(.+)$", "i");
        let text = "", from = "";
        for (const s of skills) {
            if (/reversed/i.test(s.n) !== reversed) continue;
            for (const line of String(s.d).split("\n")) {
                const m = line.trim().match(coreRe);
                if (m) { text = m[1]; from = s.n; break; }
            }
            if (text) break;
        }
        let all = "";
        if (core.length > 2) {
            const nameRe = new RegExp("(?<![\\w])" + base.replace(/[.*+?^${}()|[\]\\]/g, "\\$&") + "(?![\\w])", "i");
            const hits = [];
            for (const s of skills) {
                for (const sent of String(s.d).replace(/\n/g, " ").split(/(?<=[.!?])\s+/)) {
                    if (nameRe.test(sent)) hits.push({ s: s.n, t: sent.trim() });
                }
            }
            all = hits.map(h => h.t).join(" ");
            const says = h => (/\b(appl|grant|summon|gain|increas|reduc|decreas|restor|heal)/i.test(h.t) ? 0 : 1);
            hits.sort((a, b) => says(a) - says(b));
            if (!text && hits.length) { text = hits.slice(0, 2).map(h => h.t).join(" "); from = hits[0].s; }
        }
        const max = limitIn(all || text);
        const info = text ? { text, from, max } : null;
        wikiCache[key] = info;
        return info;
    }
    let enhIndex = null;
    function enhInfo(name, who) {
        if (!wiki || !wiki.auras) return null;
        if (!enhIndex) {
            enhIndex = {};
            for (const k of Object.keys(wiki.auras)) enhIndex[k.toLowerCase().replace(/[‘’]/g, "'")] = wiki.auras[k];
        }
        const e = enhIndex[String(name).toLowerCase().replace(/[‘’]/g, "'")];
        if (!e) return null;
        if (!Array.isArray(e)) return e;
        const on = who === "t" ? "target" : "you";
        return e.find(x => x.on === on) || e[0];
    }
    function limitFromDesc(desc) {
        if (!desc) return 0;
        return limitIn(String(desc).replace(/<[^>]*>/g, " "));
    }
    function special(name) {
        for (const s of SPECIAL) if (s.re.test(name)) return s;
        return null;
    }
    function iconUrl(key) {
        if (!key) return "";
        if (key in icons) return icons[key];
        if (iconBudget <= 0) return "";
        iconBudget--;
        const r = call("mvsAuraIcon", key, ICON);
        let url = "";
        try {
            const v = typeof r === "string" ? r : "";
            const i = v.indexOf(":");
            if (i > 0) {
                const size = +v.slice(0, i);
                const bin = atob(v.slice(i + 1));
                const c = document.createElement("canvas");
                c.width = c.height = size;
                const ctx = c.getContext("2d");
                const img = ctx.createImageData(size, size);
                for (let p = 0, q = 0; p + 3 < bin.length; p += 4, q += 4) {
                    img.data[q] = bin.charCodeAt(p + 1);
                    img.data[q + 1] = bin.charCodeAt(p + 2);
                    img.data[q + 2] = bin.charCodeAt(p + 3);
                    img.data[q + 3] = bin.charCodeAt(p);
                }
                ctx.putImageData(img, 0, 0);
                url = c.toDataURL();
            }
        } catch (e) {
            url = "";
        }
        icons[key] = url;
        return url;
    }
    function css() {
        if (document.getElementById("mvs-aura-style")) return;
        const s = document.createElement("style");
        s.id = "mvs-aura-style";
        s.textContent = `
.mvs-aw { position: fixed; z-index: 31; width: 17em; font: calc(11px * var(--s, .72))/1.25 system-ui, sans-serif; color: #EDE3CC;
  background: rgba(22, 17, 12, 0.84); border: 1px solid #8A6A2E; border-radius: .8em; box-shadow: 0 2px 10px rgba(0,0,0,.5);
  user-select: none; -webkit-user-select: none; touch-action: none; max-height: 94vh; overflow: hidden;
  contain: layout paint; will-change: transform; }
.mvs-aw .hd { display: flex; align-items: center; gap: .3em; padding: .25em .25em .25em .3em; }
.mvs-aw .grip { flex: none; width: 1.4em; text-align: center; color: #C9A447; font-size: 1.35em; line-height: 1; cursor: grab;
  opacity: .85; }
.mvs-aw .tt { flex: 1; min-width: 0; font-weight: 700; color: #E8C15A; font-size: 1.05em; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
.mvs-aw .bt { flex: none; width: 2.1em; height: 1.9em; border: 0; border-radius: .45em; background: rgba(255,255,255,.08);
  color: #E8C15A; font: 700 1.1em system-ui, sans-serif; padding: 0; }
.mvs-aw .bd { box-sizing: border-box; padding: 0 .45em .45em; overflow-y: auto; max-height: calc(94vh - 3em); touch-action: pan-y; overscroll-behavior: contain; }
.mvs-aw.min .bd { display: none; }
.mvs-aw .sh { color: #C8B89A; font-weight: 700; font-size: .9em; letter-spacing: .04em; text-transform: uppercase; margin: .2em .2em .25em;
  white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
.mvs-aw .row { position: relative; margin-bottom: .25em; padding: .25em .4em .35em .25em; border-radius: .5em; background: rgba(0,0,0,.38);
  border: 1px solid rgba(138,106,46,.5); overflow: hidden; }
.mvs-aw .top { display: flex; align-items: center; gap: .4em; }
.mvs-aw .ic { flex: none; width: 2em; height: 2em; border-radius: .35em; background: #2A2016 center/contain no-repeat;
  display: flex; align-items: center; justify-content: center; color: #E8DCC0; font: 700 1em system-ui, sans-serif; }
.mvs-aw .nm { flex: 1; min-width: 0; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; color: #E6DCC6; }
.mvs-aw .st { flex: none; font: 700 1.15em system-ui, sans-serif; color: #FFF; }
.mvs-aw .st small { font-size: .8em; color: #9A8F7A; font-weight: 600; }
.mvs-aw .tm { flex: none; min-width: 3em; text-align: right; font: 600 1em monospace; color: #C8B89A; }
.mvs-aw .tag { flex: none; padding: 0 .35em; border-radius: .35em; font-size: .8em; font-weight: 800; }
.mvs-aw .sum { color: #A89C84; font-size: .9em; margin: .15em 0 0 2.4em; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
.mvs-aw .sum .by { color: #E8C15A; font-weight: 700; }
.mvs-aw .row.noheal .sum .by, .mvs-aw .row.enrage .sum .by { color: #FFF; }
.mvs-aw .bar { position: absolute; left: 0; bottom: 0; width: 100%; height: .2em; background: #40A8C8;
  transform-origin: 0 50%; will-change: transform; }
.mvs-aw .bar.low { background: #FF9A3C; }
.mvs-aw .row.noheal { border-color: #FF5A4E; background: rgba(70,10,8,.6); }
.mvs-aw .row.noheal .tag { background: #FF5A4E; color: #1A0604; }
.mvs-aw .row.enrage { border-color: #FF9A3C; background: rgba(70,32,6,.55); }
.mvs-aw .row.enrage .tag { background: #FF9A3C; color: #1A0E04; }
.mvs-aw .row.scroll { border-color: #C77DFF; background: rgba(48,14,70,.62); }
.mvs-aw .row.scroll .tag { background: #C77DFF; color: #1A0826; }
/* Glows pulse by opacity on their own layer (compositor only): animating box-shadow repainted the
   row over the game canvas every frame. */
.mvs-aw .row.noheal::after, .mvs-aw .row.enrage::after, .mvs-aw .row.max::after { content: ""; position: absolute; inset: 0;
  border-radius: inherit; pointer-events: none; will-change: opacity; animation: mvsAuraPulse 1s ease-in-out infinite alternate; }
.mvs-aw .row.noheal::after { box-shadow: inset 0 0 .7em rgba(255,90,78,1); }
.mvs-aw .row.enrage::after { box-shadow: inset 0 0 .7em rgba(255,154,60,1); }
.mvs-aw .row.max::after { box-shadow: inset 0 0 .7em rgba(64,224,255,.95); }
@keyframes mvsAuraPulse { from { opacity: .25; } to { opacity: 1; } }
@keyframes mvsAuraRed { from { box-shadow: 0 0 2px rgba(255,90,78,.5); } to { box-shadow: 0 0 12px rgba(255,90,78,1); } }
@keyframes mvsAuraOrange { from { box-shadow: 0 0 2px rgba(255,154,60,.5); } to { box-shadow: 0 0 12px rgba(255,154,60,1); } }
.mvs-aw .chip.scroll { border-color: #C77DFF; color: #DDB0FF; }
.mvs-aw .row.max { border-color: #40E0FF; }
.mvs-aw .row.max .st { color: #40E0FF; }
.mvs-aw .info { margin: .35em 0 0 .2em; padding-top: .25em; border-top: 1px solid rgba(138,106,46,.4); color: #C8B89A; font-size: .95em; white-space: normal; }
.mvs-aw .info b { color: #EDE3CC; font-weight: 600; }
.mvs-aw .info .wd { color: #E6DCC6; }
.mvs-aw .more, .mvs-aw .none { color: #8A806C; font-size: .9em; margin: 0 .2em .25em; }
.mvs-aw .chips { display: none; gap: .25em; flex-wrap: wrap; padding: 0 .45em .35em; }
.mvs-aw.min .chips { display: flex; }
.mvs-aw .keep { display: none; padding: .1em .45em 0; }
.mvs-aw.min .keep { display: block; }
.mvs-aw .keep:empty { display: none; }
.mvs-aw .ql { display: flex; align-items: baseline; gap: .5em; padding: .2em .35em; border-radius: .4em; background: rgba(0,0,0,.3); margin-bottom: .2em; }
.mvs-aw .qv { flex: none; min-width: 3.6em; font: 800 1.15em system-ui, sans-serif; text-align: right; }
.mvs-aw .qv.good { color: #4CC26A; }
.mvs-aw .qv.bad { color: #E5484D; }
.mvs-aw .qs { flex: 1; min-width: 0; color: #E6DCC6; }
.mvs-aw .qn { flex: none; color: #8A806C; font-size: .85em; }
.mvs-aw .qh { color: #C8B89A; font-weight: 700; font-size: .85em; text-transform: uppercase; letter-spacing: .04em; margin: .35em .2em .2em; }
.mvs-aw .qf { display: flex; flex-wrap: wrap; gap: .25em; }
.mvs-aw .chip { display: flex; align-items: center; gap: .25em; padding: 0 .45em 0 .15em; border-radius: .7em; background: rgba(0,0,0,.4);
  border: 1px solid rgba(138,106,46,.6); font-weight: 700; }
.mvs-aw .chip i { width: 1.3em; height: 1.3em; border-radius: .25em; background: center/contain no-repeat; }
.mvs-aw .chip.max { border-color: #40E0FF; color: #40E0FF; box-shadow: 0 0 6px rgba(64,224,255,.7); }
.mvs-aw .chip.noheal { border-color: #FF5A4E; color: #FF8A80; }
.mvs-aw .chip.enrage { border-color: #FF9A3C; color: #FFB870; }
@keyframes mvsAuraGlow { from { box-shadow: 0 0 3px rgba(64,224,255,.45); } to { box-shadow: 0 0 12px rgba(64,224,255,.95); } }
.mvs-ap { position: fixed; z-index: 31; width: 30px; height: 30px; border-radius: 50%; border: 1px solid #8A6A2E;
  background: rgba(22, 17, 12, 0.86); color: #E8C15A; font: 700 13px system-ui, sans-serif; padding: 0; box-shadow: 0 2px 8px rgba(0,0,0,.5); }
.mvs-ap.max { border-color: #40E0FF; color: #40E0FF; box-shadow: 0 0 8px rgba(64,224,255,.8); }
.mvs-ap.noheal { border-color: #FF5A4E; color: #FF5A4E; }
/* AQW-style gold frame: dark leather plate, gold rim with a lit inner edge, gold rule under the header. */
.mvs-aw { border: 2px solid #C9A042; border-radius: .55em; background: linear-gradient(180deg, rgba(36,25,13,.93), rgba(18,13,8,.9));
  box-shadow: 0 0 0 1px #2A1A06, inset 0 0 0 1px rgba(255,224,140,.4), inset 0 1px 0 rgba(255,240,190,.3), 0 3px 12px rgba(0,0,0,.6); }
.mvs-aw .hd { border-bottom: 1px solid rgba(201,160,66,.6); background: linear-gradient(180deg, rgba(92,60,18,.55), rgba(40,26,8,.15)); }
.mvs-ap { border-width: 2px; box-shadow: 0 0 0 1px #2A1A06, inset 0 0 0 1px rgba(255,224,140,.4), 0 2px 8px rgba(0,0,0,.55); }
.mvs-ap:not(.max):not(.noheal) { border-color: #C9A042; }
.mvs-aw { width: 22em; }
/* Applier under the aura name, so a long boss name never squeezes the aura's own. */
.mvs-aw .top .nb { flex: 1; min-width: 0; display: flex; flex-direction: column; }
.mvs-aw .top .nm { flex: none; }
.mvs-aw .top .by { white-space: nowrap; overflow: hidden; text-overflow: ellipsis; color: #E8B931; font-weight: 700; font-size: .8em; }
.mvs-aw .top .by.you { color: #9A8F7A; }
.mvs-aw .pls { display: flex; flex-wrap: wrap; gap: .2em; margin: .2em 0 0 2.4em; }
.mvs-aw .pl { padding: 0 .4em; border-radius: .6em; font-size: .85em; font-weight: 600; color: var(--c);
  border: 1px solid var(--c); background: color-mix(in srgb, var(--c) 18%, transparent); white-space: normal; max-width: 100%; }
.mvs-aw .row.scroll { border-color: #F08A24; background: rgba(70,32,6,.55); }
.mvs-aw .row.scroll .tag { background: #F08A24; color: #1A0E04; }
.mvs-aw .flt { display: flex; flex-wrap: wrap; gap: .25em; margin: 0 .2em .3em; }
.mvs-aw .fc { padding: .1em .5em; border-radius: .7em; border: 1px solid rgba(138,106,46,.6); color: #C8B89A; font-size: .85em; font-weight: 700; }
.mvs-aw .fc.on { background: #E8C15A; color: #1A1206; border-color: #E8C15A; }
.mvs-aw .cfg { padding: .3em .5em .5em; border-bottom: 1px solid rgba(138,106,46,.5); max-height: 60vh; overflow-y: auto; touch-action: pan-y; }
.mvs-aw .cfg .ch { color: #E8C15A; font-weight: 700; margin: .3em 0 .2em; }
.mvs-aw .cfg .pins { display: flex; flex-wrap: wrap; gap: .25em; }
.mvs-aw .cfg .pin { padding: .1em .2em .1em .5em; border-radius: .7em; background: rgba(232,185,49,.18); border: 1px solid #E8B931; }
.mvs-aw .cfg .pin b, .mvs-aw .cfg .og b { display: inline-block; padding: 0 .5em; color: #E8C15A; }
.mvs-aw .cfg .hint { color: #8A806C; }
.mvs-aw .cfg .q { width: 100%; box-sizing: border-box; margin-top: .35em; padding: .3em .4em; font: inherit; color: #EDE3CC;
  background: rgba(0,0,0,.5); border: 1px solid #8A6A2E; border-radius: .4em; user-select: text; -webkit-user-select: text; }
.mvs-aw .cfg .sg { padding: .3em .4em; border-bottom: 1px solid rgba(138,106,46,.3); }
.mvs-aw .cfg .og { display: flex; align-items: center; padding: .15em 0; }
.mvs-aw .cfg .og span { flex: 1; }
.mvs-aw .cfg .fd { min-height: 2.2em; }
.mvs-aw .cfg .fd b { border: 1px solid #8A6A2E; border-radius: .7em; padding: .15em .8em; color: #C8B89A; }
.mvs-aw .cfg .fd b.on { background: #E8C15A; color: #1A1206; border-color: #E8C15A; }
.mvs-aw .cfg .rst { display: inline-block; margin-top: .3em; color: #C8B89A; text-decoration: underline; }
.mvs-aw .cfg .og .mx { flex: none; min-width: 1.8em; text-align: center; font-weight: 800; color: #EDE3CC; }
.mvs-aw .cfg .og b[data-maxd] { border: 1px solid #8A6A2E; border-radius: .5em; padding: .1em .6em; }
.mvs-aw .fold { display: flex; align-items: center; gap: .35em; margin-bottom: .25em; padding: .25em .35em; border-radius: .5em;
  background: rgba(0,0,0,.3); border: 1px dashed rgba(201,160,66,.6); }
.mvs-aw .fold .fl { flex: none; color: #E8C15A; font-weight: 800; }
.mvs-aw .fold .fcs { flex: 1; min-width: 0; display: flex; flex-wrap: wrap; gap: .25em; }
.mvs-aw .fold .fx { flex: none; color: #C8B89A; font-size: .9em; }
.mvs-aw .fold.less { justify-content: center; color: #C8B89A; font-weight: 700; font-size: .9em; }`;
        document.head.appendChild(s);
    }
    let boxCache = null, boxAt = 0;
    function gameBox() {
        const now = Date.now();
        if (boxCache && now - boxAt < 500) return boxCache;
        boxAt = now;
        const p = document.querySelector("ruffle-player");
        const c = p && p.shadowRoot ? p.shadowRoot.querySelector("canvas") : null;
        const r = (c || p) ? (c || p).getBoundingClientRect() : null;
        if (!r || r.width < 50 || r.height < 50) boxCache = { x: 0, y: 0, w: window.innerWidth, h: window.innerHeight };
        else {
            const k = Math.min(r.width / 960, r.height / 550), w = 960 * k, h = 550 * k;
            boxCache = { x: Math.round(r.left + (r.width - w) / 2), y: Math.round(r.top + (r.height - h) / 2), w: Math.round(w), h: Math.round(h) };
        }
        return boxCache;
    }
    let freeDrag = store.get("auraFreeDrag", false);
    function clampTo(node, x, y) {
        const b = freeDrag ? { x: 0, y: 0, w: window.innerWidth, h: window.innerHeight } : gameBox();
        node.style.left = Math.round(Math.min(Math.max(b.x, x), Math.max(b.x, b.x + b.w - node.offsetWidth))) + "px";
        node.style.top = Math.round(Math.min(Math.max(b.y, y), Math.max(b.y, b.y + b.h - node.offsetHeight))) + "px";
    }
    function place(w) {
        if (w.el.style.display === "none") { w.replace = true; return; }
        w.replace = false;
        clampTo(w.el, w.want.x, w.want.y);
    }
    function placePill(w) {
        const b = gameBox(), pill = w.pill;
        if (w.side === "left") { pill.style.left = (b.x + 6) + "px"; pill.style.right = ""; }
        else { pill.style.right = Math.max(0, window.innerWidth - (b.x + b.w) + 6) + "px"; pill.style.left = ""; }
        pill.style.top = Math.round(b.y + w.pillShare * b.h) + "px";
    }
    let lastBoxKey = "";
    function reflow() {
        if (!wins) return;
        const b = gameBox(), key = b.x + "," + b.y + "," + b.w + "," + b.h;
        if (key === lastBoxKey) return;
        lastBoxKey = key;
        for (const w of Object.values(wins)) { place(w); placePill(w); }
    }
    window.addEventListener("resize", () => { boxAt = 0; reflow(); });
    function makeWin(key, title, defX, defY, pillY, pillGlyph, side) {
        css();
        const w = { key, el: document.createElement("div"), pill: document.createElement("button"), sig: "", rows: {} };
        const el = w.el;
        el.className = "mvs-aw";
        el.innerHTML = `<div class="hd"><span class="grip" title="Drag to move">⠿</span><span class="tt">${esc(title)}</span><button class="bt sz-b" title="Size">A</button><button class="bt sp-b" title="Split / join You and Target">⇆</button>` +
            `<button class="bt cfg-b" title="Pins and order">⚙</button>` +
            `<button class="bt min-b" title="Collapse">–</button><button class="bt hide-b" title="Hide">×</button></div>` +
            `<div class="cfg"></div><div class="keep"></div><div class="chips"></div><div class="bd"></div>`;
        el.querySelector(".cfg-b").addEventListener("click", e => { e.stopPropagation(); toggleCfg(w); });
        cfgEvents(w);
        if (store.get("auraMin." + key, false)) el.classList.add("min");
        el.querySelector(".sz-b").addEventListener("click", e => { e.stopPropagation(); cycleSize(); });
        el.querySelector(".sp-b").addEventListener("click", e => { e.stopPropagation(); setSplit(!store.get("auraSplit", true)); });
        el.querySelector(".min-b").addEventListener("click", e => { e.stopPropagation(); toggleMin(w); });
        el.querySelector(".hide-b").addEventListener("click", e => { e.stopPropagation(); store.set("auraHidden." + key, true); draw(); });
        el.querySelector(".keep").addEventListener("click", e => {
            const row = e.target.closest(".row");
            if (!row) return;
            if (open.has(row.dataset.k)) open.delete(row.dataset.k); else open.add(row.dataset.k);
            w.sig = "";
            draw();
        });
        el.querySelector(".bd").addEventListener("click", e => {
            const fc = e.target.closest(".fc");
            if (fc) {
                const [side, f] = fc.dataset.f.split(":");
                store.set("auraFilter." + side, f);
                for (const x of Object.values(wins)) x.sig = "";
                draw();
                return;
            }
            const fold = e.target.closest(".fold");
            if (fold) {
                const side = fold.dataset.more;
                moreOpen[side] = !moreOpen[side];
                for (const x of Object.values(wins)) x.sig = "";
                draw();
                return;
            }
            const row = e.target.closest(".row");
            if (!row) return;
            const k = row.dataset.k;
            if (open.has(k)) open.delete(k); else open.add(k);
            w.sig = "";
            draw();
        });
        drag(w);
        document.body.appendChild(el);
        const pos = store.get("auraPos." + key, null);
        w.want = pos ? { x: pos.x, y: pos.y } : { x: defX, y: defY };
        place(w);
        const pill = w.pill;
        pill.className = "mvs-ap";
        pill.textContent = pillGlyph;
        pill.title = title;
        pill.addEventListener("click", () => { store.set("auraHidden." + key, false); w.sig = ""; draw(); });
        w.side = side;
        w.pillShare = pillY;
        placePill(w);
        document.body.appendChild(pill);
        return w;
    }
    function toggleCfg(w) {
        w.cfgOpen = !w.cfgOpen;
        if (w.el.classList.contains("min") && w.cfgOpen) toggleMin(w);
        renderCfg(w);
    }
    function renderCfg(w) {
        const c = w.el.querySelector(".cfg");
        if (!w.cfgOpen) { c.innerHTML = ""; c.style.display = "none"; return; }
        c.style.display = "";
        const pins = getPins(), order = getOrder();
        const pinHtml = pins.length ? pins.map((p, i) => `<span class="pin">${esc(p)}<b data-unpin="${i}">×</b></span>`).join("")
            : `<span class="hint">Nothing pinned</span>`;
        const ordHtml = order.map((g, i) => `<div class="og"><span>${i + 1}. ${GROUP_NAMES[g]}</span>` +
            `<b data-mv="${i}:-1">▲</b><b data-mv="${i}:1">▼</b></div>`).join("");
        c.innerHTML = `<div class="ch">Pinned (always on top)</div><div class="pins">${pinHtml}</div>` +
            `<input class="q" type="text" placeholder="Search aura to pin…" autocomplete="off" spellcheck="false">` +
            `<div class="sug"></div><div class="ch">Order</div>${ordHtml}<span class="rst" data-reset="1">Reset order</span>` +
            `<div class="ch">Panels</div><div class="og fd" data-free="1"><span>Drag panels anywhere</span>` +
            `<b class="${freeDrag ? "on" : ""}">${freeDrag ? "On" : "Off"}</b></div>` +
            `<span class="hint">${freeDrag ? "Panels can sit over the side rails." : "Panels stay inside the game."}</span>` +
            `<div class="og"><span>Detailed auras per panel<br><span class="hint">The rest fold into one row of icons (tap it to open them). At least ${DETAIL_MIN}.</span></span>` +
            `<b data-maxd="-1">−</b><span class="mx">${maxDetail()}</span><b data-maxd="1">+</b></div>`;
    }
    function renderSug(w) {
        const q = w.el.querySelector(".cfg .q"), s = w.el.querySelector(".cfg .sug");
        if (!q || !s) return;
        const m = bestMatches(q.value, 6);
        s.innerHTML = q.value.trim() ? (m.length ? m.map(x => `<div class="sg" data-add="${esc(x.n)}">${esc(x.n)}</div>`).join("")
            : `<div class="sg" data-add="${esc(q.value.trim())}">Pin keyword “${esc(q.value.trim())}”</div>`) : "";
    }
    function addPin(name) {
        const p = getPins();
        if (name && !p.some(x => x.toLowerCase() === name.toLowerCase())) p.push(name);
        setPins(p);
        refreshAll();
    }
    function refreshAll() {
        for (const x of Object.values(wins || {})) { x.sig = ""; renderCfg(x); }
        draw();
    }
    function cfgEvents(w) {
        const c = w.el.querySelector(".cfg");
        c.style.display = "none";
        c.addEventListener("input", () => renderSug(w));
        c.addEventListener("keydown", e => {
            if (e.key !== "Enter" || !e.target.classList.contains("q")) return;
            const v = e.target.value.trim();
            if (!v) return;
            const m = bestMatches(v, 1);
            addPin(m.length ? m[0].n : v);
        });
        c.addEventListener("click", e => {
            const t = e.target;
            if (t.dataset.add) addPin(t.dataset.add);
            else if (t.closest("[data-free]")) {
                freeDrag = !freeDrag;
                store.set("auraFreeDrag", freeDrag);
                for (const x of Object.values(wins || {})) {
                    place(x);
                    if (!x.replace) { x.want = { x: x.el.offsetLeft, y: x.el.offsetTop }; store.set("auraPos." + x.key, x.want); }
                    renderCfg(x);
                }
            }
            else if (t.dataset.maxd) {
                store.set("auraMaxDetail", Math.max(DETAIL_MIN, maxDetail() + Number(t.dataset.maxd)));
                refreshAll();
            }
            else if (t.dataset.unpin != null) { const p = getPins(); p.splice(+t.dataset.unpin, 1); setPins(p); refreshAll(); }
            else if (t.dataset.mv) {
                const [i, d] = t.dataset.mv.split(":").map(Number), o = getOrder(), j = i + d;
                if (j >= 0 && j < o.length) { [o[i], o[j]] = [o[j], o[i]]; store.set("auraOrder", o.join(",")); refreshAll(); }
            } else if (t.dataset.reset) { store.set("auraOrder", GROUPS.join(",")); refreshAll(); }
        });
    }
    const SIZES = [0.6, 0.72, 0.85, 1];
    function applySize() {
        const k = store.get("auraSize", 0.72);
        for (const w of Object.values(wins || {})) {
            w.el.style.setProperty("--s", (k * 1.2).toFixed(3));
            place(w);
        }
    }
    function cycleSize() {
        const k = store.get("auraSize", 0.72);
        store.set("auraSize", SIZES[(SIZES.indexOf(k) + 1) % SIZES.length]);
        applySize();
    }
    function toggleMin(w) {
        w.el.classList.toggle("min");
        store.set("auraMin." + w.key, w.el.classList.contains("min"));
    }
    function drag(w) {
        const handle = w.el.querySelector(".hd");
        let start = null;
        handle.addEventListener("pointerdown", e => {
            if (e.target.closest(".bt")) return;
            start = { x: e.clientX, y: e.clientY, l: w.el.offsetLeft, t: w.el.offsetTop, moved: false };
            w.dragging = true;
            handle.setPointerCapture(e.pointerId);
        });
        handle.addEventListener("pointermove", e => {
            if (!start) return;
            const dx = e.clientX - start.x, dy = e.clientY - start.y;
            if (!start.moved && Math.abs(dx) + Math.abs(dy) < 6) return;
            start.moved = true;
            clampTo(w.el, start.l + dx, start.t + dy);
        });
        handle.addEventListener("pointerup", () => {
            if (!start) return;
            if (start.moved) { w.want = { x: w.el.offsetLeft, y: w.el.offsetTop }; store.set("auraPos." + w.key, w.want); }
            else toggleMin(w);
            start = null;
            w.dragging = false;
        });
        handle.addEventListener("pointercancel", () => { start = null; w.dragging = false; });
    }
    let wins = null;
    function windows() {
        if (wins) return wins;
        const B = gameBox();
        if (!store.get("auraLayout2", false)) {
            store.set("auraLayout2", true);
            store.set("auraSplit", true);
            for (const k of ["all", "me", "t"]) { try { localStorage.removeItem("mvs.auraPos." + k); } catch (e) { } }
        }
        wins = {
            all: makeWin("all", "Auras", B.x + 4, B.y + Math.round(B.h * 0.35), 0.35, "✦", "left"),
            me: makeWin("me", "You", B.x + 4, B.y + Math.round(B.h * 0.35), 0.35, "✦", "left"),
            t: makeWin("t", "Target", B.x + B.w - 150, B.y + Math.round(B.h * 0.1), 0.1, "◎", "right")
        };
        applySize();
        return wins;
    }
    function setSplit(on) {
        store.set("auraSplit", on);
        for (const w of Object.values(windows())) w.sig = "";
        draw();
    }
    const recasts = new Map();
    function castTime(who, a, nowMs) {
        const ts = Number(a.ts) || 0;
        const k = who + "|" + a.nam + "|" + a.casterType + "|" + a.casterId;
        let r = recasts.get(k);
        if (!r || r.ts !== ts) {
            r = { ts, dur: a.dur, val: a.val, at: ts, seen: nowMs };
            recasts.set(k, r);
        } else if (r.dur !== a.dur || r.val !== a.val) {
            r.dur = a.dur; r.val = a.val; r.at = nowMs;
        }
        r.seen = nowMs;
        if (recasts.size > 300) for (const [key, v] of recasts) if (nowMs - v.seen > 300000) recasts.delete(key);
        return Math.max(ts, r.at);
    }
    function auraList(leafPath, who) {
        const hud = read(leafPath + ".hudAuras");
        if (!hud || typeof hud !== "object") return null;
        const raw = read(leafPath + ".auras");
        const nowMs = Date.now();
        const byName = {};
        const casters = {};
        if (Array.isArray(raw)) {
            for (const a of raw) {
                if (!a || !a.nam) continue;
                a._ts = castTime(who, a, nowMs);
                const b = byName[a.nam];
                if (!b || a._ts > b._ts) byName[a.nam] = a;
                if (a.casterType === "m" || a.casterType === "p") {
                    const list = casters[a.nam] || (casters[a.nam] = []);
                    const k = a.casterType + ":" + a.casterId;
                    const at = list.find(c => c.k === k);
                    if (!at) list.push({ k, type: a.casterType, id: a.casterId, ts: a._ts });
                    else if (a._ts > at.ts) at.ts = a._ts;
                }
            }
            for (const n of Object.keys(casters)) casters[n].sort((x, y) => y.ts - x.ts);
        }
        for (const name of Object.keys(hud)) {
            const h = hud[name], a = byName[name];
            if (a && a.ts && a._ts === Number(a.ts) && h && h.end > 0 && h.dur > 0 && Math.abs(Number(a.dur) - h.dur) < 0.01) {
                offSamples.set(who + ":" + name + ":" + a.ts, { off: Number(a.ts) - (h.end - h.dur * 1000), at: nowMs });
            }
        }
        for (const [k, s] of offSamples) if (nowMs - s.at > 120000) offSamples.delete(k);
        while (offSamples.size > 25) offSamples.delete(offSamples.keys().next().value);
        if (offSamples.size) {
            const offs = [...offSamples.values()].map(s => s.off).sort((x, y) => x - y);
            clockOffset = offs[Math.floor((offs.length - 1) / 2)];
        }
        const out = [];
        for (const name of Object.keys(hud)) {
            const h = hud[name] || {}, a = byName[name] || {};
            const fromCast = a._ts && a.dur > 0 ? a._ts + a.dur * 1000 : null;
            const fromHud = h.end > 0 && clockOffset !== null ? h.end + clockOffset : null;
            const end = fromCast !== null && fromHud !== null ? Math.max(fromCast, fromHud) : (fromCast ?? fromHud);
            out.push({ key: who + ":" + name, who, name, n: Math.max(1, Number(h.n) || Number(a.val) || 1),
                dur: Number(h.dur) || Number(a.dur) || 0, end, icon: h.icon || a.icon || "",
                casterType: a.casterType, casterId: a.casterId, casters: casters[name] || [], seq: Number(h.seq) || 0 });
        }
        return out;
    }
    function enabled() {
        return !(window.__mvsOpts && window.__mvsOpts.mvsAuras === false);
    }
    const FULL_MS = 2000;
    const gate = { me: { v: null, at: 0 }, t: { v: null, at: 0, key: "" }, user: "" };
    function sideChanged(g, v, now) {
        if (v !== g.v || now - g.at > FULL_MS) { g.v = v; g.at = now; return true; }
        return false;
    }
    function poll() {
        if (!enabled() || paused()) return;
        const now = Date.now();
        const myId = read("world.myAvatar.dataLeaf.entID");
        if (myId == null) {
            state.me = ""; state.mine = []; state.theirs = []; state.target = null;
            gate.me.v = gate.t.v = null; gate.t.key = ""; gate.user = "";
            return;
        }
        if (myId !== state.myId || !state.me) {
            state.myId = myId;
            state.me = read("world.myAvatar.objData.strUsername") || "";
        }
        if (now - lastDesc > DESC_MS) {
            lastDesc = now;
            state.cls = read("world.myAvatar.objData.strClassName") || "";
            state.mobile = call("mvsMobileActive") === true;
            loadWiki();
        }
        if (sideChanged(gate.me, read("world.myAvatar.dataLeaf.hudAurasV"), now)) {
            const mine = auraList("world.myAvatar.dataLeaf", "me");
            if (mine) state.mine = mine;
        }
        const tMon = read("world.myAvatar.target.dataLeaf.MonMapID");
        const tEnt = tMon == null ? read("world.myAvatar.target.dataLeaf.entID") : null;
        if (tMon != null || tEnt != null) {
            const key = tMon != null ? "m:" + tMon : "p:" + tEnt;
            if (key !== gate.t.key) {
                gate.t.key = key; gate.t.v = null;
                state.target = {
                    name: read("world.myAvatar.target.objData.strMonName") || read("world.myAvatar.target.objData.strUsername") || "Target",
                    mapId: tMon, entId: tEnt
                };
            }
            if (sideChanged(gate.t, read("world.myAvatar.target.dataLeaf.hudAurasV"), now)) {
                const theirs = auraList("world.myAvatar.target.dataLeaf", "t");
                if (theirs) state.theirs = theirs;
            }
        } else {
            state.target = null;
            state.theirs = [];
            gate.t.key = ""; gate.t.v = null;
        }
        if (now - lastDescRead > DESC_MS && state.mine.concat(state.theirs).some(a => !(a.name in descs) && !descMiss.has(a.name))) {
            lastDescRead = now;
            const d = read("world.auraDescs");
            if (d && typeof d === "object") descs = d;
            for (const a of state.mine.concat(state.theirs)) if (!(a.name in descs)) descMiss.add(a.name);
        }
        sniffCasters(now);
        resolveCasters(now);
        let changed = false;
        for (const a of state.mine.concat(state.theirs)) {
            if (a.n > (seen[a.name] || 0)) { seen[a.name] = a.n; changed = true; }
        }
        if (changed) store.set("auraSeen", seen);
    }
    const casterInfo = {};
    let monsAt = 0;
    let sniff = null, sniffAt = 0;
    function sniffCasters(now) {
        if (!state.mine.concat(state.theirs).some(a => !a.casters || !a.casters.length)) return;
        if (now - sniffAt >= 1000) {
            sniffAt = now;
            try {
                const r = call("mvsAuraCasters");
                if (r) { sniff = JSON.parse(r); window.__auraPackets = sniff.log; }
            } catch (e) { }
        }
        if (!sniff || !sniff.casters) return;
        const tinfOf = who => who === "me" ? "p:" + state.myId
            : state.target ? (state.target.mapId != null ? "m:" + state.target.mapId : "p:" + state.target.entId) : "";
        for (const a of state.mine.concat(state.theirs)) {
            if (a.casters && a.casters.length) continue;
            const hit = sniff.casters[tinfOf(a.who) + "|" + a.name];
            if (!hit || !hit.c) continue;
            const type = hit.c.charAt(0), id = Number(hit.c.slice(2));
            a.casters = [{ k: type + ":" + id, type, id, ts: 0, via: hit.s }];
            a.casterType = type; a.casterId = id;
        }
    }
    function resolveCasters(now) {
        const want = new Set();
        for (const a of state.mine.concat(state.theirs)) for (const c of a.casters || []) want.add(c);
        let needMons = false;
        for (const c of want) {
            const info = casterInfo[c.k];
            if (info && now - info.at < 15000) continue;
            if (c.type === "p") {
                if (c.id === state.myId) { casterInfo[c.k] = { name: state.me, cls: state.cls, at: now, me: true }; continue; }
                const base = "world.avatars." + c.id;
                const name = read(base + ".objData.strUsername") || read(base + ".pnm");
                const cls = read(base + ".objData.strClassName");
                casterInfo[c.k] = { name: name || "", cls: cls || "", at: now };
            } else needMons = true;
        }
        if (needMons && now - monsAt > 3000) {
            monsAt = now;
            let mons = null;
            try { const r = call("availableMonsters"); mons = typeof r === "string" ? JSON.parse(r) : null; } catch (e) { mons = null; }
            if (Array.isArray(mons)) {
                for (const m of mons) {
                    if (m && m.MonMapID != null) casterInfo["m:" + m.MonMapID] = { name: m.strMonName || "", cls: "", at: now };
                }
            }
            for (const c of want) {
                if (c.type === "m" && !casterInfo[c.k] && state.target && c.id === state.target.mapId) {
                    casterInfo[c.k] = { name: state.target.name, cls: "", at: now };
                }
            }
        }
    }
    function casterName(c) {
        const info = casterInfo[c.k];
        if (c.type === "p" && c.id === state.myId) return "You";
        if (info && info.name) return info.name;
        return c.type === "m" ? "a monster" : "a player";
    }
    function sourceOf(a) {
        if (!a.casters || !a.casters.length) return "Not named: the aura packet had no caster, and no skill hit at the same moment";
        return a.casters.map(c => casterName(c) + (c.via === "skill" ? " (the skill that hit at the same moment)" : c.via === "packet" ? " (from the aura packet)" : "")).join(", ");
    }
    function byLine(a) {
        const cs = a.casters || [];
        if (!cs.length) return "";
        return casterName(cs[0]) + (cs.length > 1 ? " +" + (cs.length - 1) : "");
    }
    function casterClass(a) {
        const c = (a.casters || [])[0];
        if (!c || c.type !== "p") return "";
        if (c.id === state.myId) return state.cls;
        const info = casterInfo[c.k];
        return info ? info.cls : "";
    }
    function fxInfo(name, cls) {
        if (!wiki || !wiki.fx || !cls) return null;
        const f = wiki.fx[normClass(cls)];
        if (!f) return null;
        const base = String(name).replace(/^[IVXLC0-9]+\s*-\s*/, "").replace(/\s*\(reversed\)\s*/i, "").replace(/^the\s+/i, "").trim();
        if (f[base]) return f[base];
        const lower = base.toLowerCase();
        for (const k of Object.keys(f)) if (k.toLowerCase() === lower) return f[k];
        return null;
    }
    function procBuffOn(enh, name, list) {
        const src = String(enh.src || ""), i = src.indexOf(" (");
        const buff = (i > 0 ? src.slice(0, i) : src).trim().toLowerCase();
        return !!buff && buff !== String(name).toLowerCase() && list.some(x => x.who === "me" && String(x.name).toLowerCase() === buff);
    }
    const lastLimit = {};
    let agreed = null;
    function agreedLimit(name) {
        if (!wiki || !wiki.fx) return 0;
        if (!agreed) {
            agreed = {};
            for (const lines of Object.values(wiki.fx)) {
                for (const k of Object.keys(lines)) {
                    const m = lines[k].max, key = k.toLowerCase();
                    if (!(m > 1)) continue;
                    agreed[key] = key in agreed && agreed[key] !== m ? 0 : m;
                }
            }
        }
        const base = String(name).replace(/^[IVXLC0-9]+\s*-\s*/, "").replace(/\s*\(reversed\)\s*/i, "").replace(/^the\s+/i, "").trim();
        return agreed[base.toLowerCase()] || 0;
    }
    function decorate(list, now) {
        for (const a of list) {
            a.left = a.end != null ? (a.end - now) / 1000 : null;
            const mine = a.casterType === "p" && a.casterId === state.myId;
            const ccls = casterClass(a);
            a.wiki = ccls ? wikiInfo(a.name, ccls) : null;
            a.fx = ccls ? fxInfo(a.name, ccls) : null;
            a.fxCls = ccls;
            a.enh = mine || a.casterType !== "m" ? enhInfo(a.name, a.who) : null;
            if (a.who === "me" && mine && a.enh && a.enh.on === "you" && procBuffOn(a.enh, a.name, list)) { a.wiki = null; a.fx = null; }
            a.max = limitFromDesc(descs[a.name]) || (a.wiki ? a.wiki.max : 0) || (a.fx && a.fx.max > 1 ? a.fx.max : 0) || (a.enh ? a.enh.max : 0);
            if (a.max > 1) lastLimit[a.name] = a.max;
            else if (!a.max && !(a.casters && a.casters.length)) a.max = lastLimit[a.name] || agreedLimit(a.name);
            a.cnt = a.max > 1 ? Math.min(a.n, a.max) : a.n;
            const most = seen[a.name] || a.n;
            a.most = Math.max(a.max > 1 ? Math.min(most, a.max) : most, a.cnt);
            a.stacks = a.max > 1 || a.most > 1;
            a.full = a.max > 1 && a.cnt >= a.max;
            a.sp = special(a.name);
            if (a.sp && a.wiki && a.sp.kind !== "noheal") a.sp = null;
            if (!a.sp && !a.wiki && a.enh && /^Scroll of/i.test(a.enh.src || "")) {
                a.sp = { kind: "scroll", tag: "SCROLL", note: a.enh.src + ": " + a.enh.t };
            }
        }
        const pins = getPins(), order = getOrder();
        for (const a of list) {
            const text = summary(a);
            a.cl = clauses(text);
            const src = a.enh ? String(a.enh.src || "") : "";
            const enrage = (a.sp && a.sp.kind === "enrage") || /^Scroll of Enrage/i.test(src);
            const scroll = enrage || (a.sp && a.sp.kind === "scroll") || /^Scroll of/i.test(src);
            const bad = a.cl.filter(c => c.k === "debuff" || c.k === "dot").length;
            if (scroll) a.cat = "scroll";
            else if (POTION_RE.test(a.name)) a.cat = "potion";
            else if ((a.sp && a.sp.kind === "noheal") || (a.cl.length && bad * 2 > a.cl.length)
                || (a.who === "me" && a.casterType === "m")) a.cat = "debuff";
            else a.cat = "buff";
            a.group = enrage ? "enrage" : scroll ? "scrolls" : a.cat === "potion" ? "potions" : a.cat === "debuff" ? "debuffs" : "other";
            a.pin = pinIndex(a, pins, text);
        }
        const live = list.filter(a => a.left === null || a.left > -1);
        const rank = a => (a.sp && a.sp.kind === "noheal" ? -1 : a.sp ? 0 : a.full ? 1 : a.stacks ? 2 : 3);
        const tier = a => ((a.sp && a.sp.tag === "TAUNT") || (a.group === "enrage" && /^focus$/i.test(a.name)) ? -1 : a.pin >= 0 ? a.pin : pins.length + order.indexOf(a.group));
        live.sort((x, y) => tier(x) - tier(y) || rank(x) - rank(y) || y.seq - x.seq);
        return live;
    }
    function passes(a, f) {
        return f === "q" ? true : f === "buff" ? a.cat === "buff" : f === "debuff" ? a.cat === "debuff" : f === "sp" ? (a.cat === "scroll" || a.cat === "potion") : true;
    }
    function fmtTime(s) {
        if (s == null) return "∞";
        if (s < 0) s = 0;
        return s >= 60 ? Math.floor(s / 60) + "m" + String(Math.floor(s % 60)).padStart(2, "0") : s.toFixed(1) + "s";
    }
    function stackText(a) {
        if (!a.stacks || (a.cnt <= 1 && !(a.max > 1))) return "";
        if (a.max > 1) return `${a.cnt}<small>/${a.max}</small>`;
        return `×${a.cnt}`;
    }
    function summary(a) {
        if (a.fx) return a.fx.e;
        if (a.enh) return a.enh.t.replace(/;\s*/g, ", ");
        const d = descs[a.name];
        if (d) return String(d).replace(/<[^>]*>/g, " ").replace(/\s+/g, " ").trim();
        if (a.wiki) return a.wiki.text;
        return a.sp ? a.sp.note : "";
    }
    function rowHtml(a, compact) {
        const cls = ["row", a.sp ? a.sp.kind : "", a.full ? "max" : ""].filter(Boolean).join(" ");
        const url = iconUrl(a.icon);
        const icon = url ? `<span class="ic" style="background-image:url(${url})"></span>`
            : `<span class="ic">${esc(a.name.replace(/^[IVXLC0-9]+\s*-\s*/, "").charAt(0).toUpperCase())}</span>`;
        const sum = summary(a);
        const by = byLine(a);
        let info = "";
        if (open.has(a.key)) {
            const cs = a.casters || [];
            const lines = [`<b>${cs.length > 1 ? "Applied by (latest first)" : "Applied by"}:</b> ${esc(sourceOf(a))}`];
            if (a.stacks) {
                lines.push(`<b>Stacks:</b> ${a.cnt}${a.max > 1 ? " of " + a.max + (a.full ? " (max)" : "")
                    : " (limit not listed; most seen " + a.most + ")"}`);
            }
            lines.push(`<b>Duration:</b> ${a.dur > 0 ? a.dur + "s" : "until removed"}`);
            if (sum) lines.push(`<b>Gives:</b> <span class="wd">${esc(sum)}</span>`);
            const from = a.fx ? a.fx.from + " (" + a.fxCls + ")" : a.wiki && a.wiki.from ? a.wiki.from + " (" + a.fxCls + ")" : a.enh ? a.enh.src : "";
            if (from) lines.push(`<i>From ${esc(from)}, AQW Wiki</i>`);
            if (a.sp && sum !== a.sp.note) lines.push(esc(a.sp.note));
            info = `<div class="info">${lines.join("<br>")}</div>`;
        }
        const scroll = a.cat === "scroll";
        const pills = (scroll ? [`<span class="pl" style="--c:${PILL.scroll}">Scroll</span>`] : [])
            .concat(a.cl.slice(0, 6).map(c => `<span class="pl" style="--c:${scroll ? PILL.scroll : PILL[c.k]}">${esc(c.t)}</span>`)).join("");
        const byHtml = by ? `<span class="by${by === "You" ? " you" : ""}">${esc(by)}</span>` : "";
        return `<div class="${cls}" data-k="${esc(a.key)}"><div class="top">${icon}` +
            `<span class="nb"><span class="nm">${a.pin >= 0 ? "📌 " : ""}${esc(a.name)}</span>${byHtml}</span>` +
            (a.sp ? `<span class="tag">${a.sp.tag}</span>` : "") +
            `<span class="st">${stackText(a)}</span><span class="tm"></span></div>` +
            (pills && !compact && !open.has(a.key) ? `<div class="pls">${pills}</div>` : "") +
            `<div class="bar"></div>${info}</div>`;
    }
    function keepFull(a) {
        return !!(a.sp && (a.sp.kind === "enrage" || a.sp.kind === "noheal")) || a.group === "enrage"
            || /^(focus|reckless)$|enrage|decay|heal(ing)?\s*block|no\s*heal/i.test(a.name);
    }
    const STAT_RE = /^(.*?)([+\-−])\s*(\d+(?:\.\d+)?)\s*(%?)\s*(.*)$/;
    function statOf(t) {
        const m = STAT_RE.exec(String(t).trim());
        if (!m) return null;
        const tail = m[5].replace(/\bfor\s+\d+(\.\d+)?\s*s(ec\w*)?\b/ig, "").replace(/\bper\s+stack\b/ig, "").trim();
        const name = (m[1].trim() || tail).replace(/[,:;.]+$/, "").replace(/\s+/g, " ").trim();
        if (!name || name.length > 40) return null;
        const v = (m[2] === "+" ? 1 : -1) * Number(m[3]);
        return { stat: name.replace(/\b\w/g, c => c.toUpperCase()), key: name.toLowerCase(), v, pct: m[4] === "%",
            flip: /\bcosts?\b|cooldown|consumption|\btaken\b|incoming/i.test(name), stack: /\bstack/i.test(t) };
    }
    function quickHtml(list) {
        const sums = new Map(), flags = new Map();
        for (const a of list) {
            const heal = (a.sp && a.sp.kind === "noheal") || /decay|heal(ing)?\s*block|no\s*heal/i.test(a.name);
            if (heal) flags.set("noheal:" + a.name, [`No heal (${a.name})`, PILL.dot]);
            else if (/^focus$|taunt/i.test(a.name) || (a.sp && a.sp.tag === "TAUNT")) flags.set("enrage:" + a.name, [`Taunted (${a.name})`, PILL.scroll]);
            else if ((a.sp && a.sp.kind === "enrage") || a.group === "enrage") flags.set("enrage:" + a.name, [a.name, PILL.scroll]);
            const parts = [];
            for (const c of a.cl || []) {
                if (statOf(c.t)) { parts.push(c); continue; }
                const bits = c.t.split(/[.;]\s*|,\s*(?=[+\-−]?\d|[A-Z])/).map(t => t.trim()).filter(Boolean);
                const found = bits.filter(t => statOf(t));
                if (found.length) found.forEach(t => parts.push({ t, k: c.k })); else parts.push(c);
            }
            for (const c of parts) {
                const s = statOf(c.t);
                if (!s) {
                    if (c.k === "dot") flags.set("dot", ["Damage over time", PILL.dot]);
                    else if (c.k === "hot") flags.set("hot", ["Heal over time", PILL.hot]);
                    else if (c.k === "debuff" && !heal && c.t.length <= 30) flags.set("x:" + c.t.toLowerCase(), [c.t, PILL.debuff]);
                    continue;
                }
                const times = s.stack && a.cnt > 1 ? a.cnt : 1;
                const e = sums.get(s.key) || { stat: s.stat, v: 0, pct: s.pct, flip: s.flip, n: 0, stacks: 0, from: a.name };
                e.v += s.v * times;
                e.n++;
                if (times > 1) e.stacks = times;
                sums.set(s.key, e);
            }
        }
        const fmt = e => (e.v > 0 ? "+" : e.v < 0 ? "−" : "") + (+Math.abs(e.v).toFixed(1)) + (e.pct ? "%" : "");
        const note = e => e.n > 1 ? e.n + " auras" : e.stacks > 1 ? e.stacks + " stacks" : esc(e.from);
        const line = e => `<div class="ql"><span class="qv ${(e.flip ? e.v < 0 : e.v > 0) ? "good" : "bad"}">${fmt(e)}</span>` +
            `<span class="qs">${esc(e.stat)}</span><span class="qn">${note(e)}</span></div>`;
        const all = [...sums.values()].filter(e => e.v !== 0).sort((x, y) => Math.abs(y.v) - Math.abs(x.v));
        const gains = all.filter(e => e.flip ? e.v < 0 : e.v > 0), losses = all.filter(e => !(e.flip ? e.v < 0 : e.v > 0));
        let html = "";
        if (gains.length) html += `<div class="qh">Gains</div>` + gains.map(line).join("");
        if (losses.length) html += `<div class="qh">Losses</div>` + losses.map(line).join("");
        if (flags.size) html += `<div class="qh">Effects</div><div class="qf">` +
            [...flags.values()].map(([t, c]) => `<span class="pl" style="--c:${c}">${esc(t)}</span>`).join("") + `</div>`;
        return html || '<div class="none">No numbers to add up</div>';
    }
    function section(title, list, side) {
        const f = store.get("auraFilter." + side, "all");
        const shown = list.filter(a => passes(a, f));
        const chips = `<div class="flt">` + FILTERS.map(([k, n]) => `<span class="fc${k === f ? " on" : ""}" data-f="${side}:${k}">${n}</span>`).join("") + `</div>`;
        if (f === "q") return `<div class="sh">${esc(title)}</div>${chips}${list.length ? quickHtml(list) : '<div class="none">No auras</div>'}`;
        const n = maxDetail(), all = moreOpen[side] || shown.length <= n;
        const rows = (all ? shown : shown.slice(0, n)).map(a => rowHtml(a)).join("");
        const rest = all ? [] : shown.slice(n);
        const more = rest.length ? `<div class="fold" data-more="${side}"><span class="fl">+${rest.length}</span>` +
            `<span class="fcs">${rest.map(chip).join("")}</span><span class="fx">▾</span></div>`
            : moreOpen[side] && shown.length > n ? `<div class="fold less" data-more="${side}">Show less ▴</div>` : "";
        return `<div class="sh">${esc(title)}</div>${chips}${rows || '<div class="none">No auras</div>'}${more}`;
    }
    function chip(a) {
        const cls = ["chip", a.sp ? a.sp.kind : "", a.full ? "max" : ""].filter(Boolean).join(" ");
        const url = iconUrl(a.icon);
        const short = a.name.replace(/^[IVXLC0-9]+\s*-\s*/, "").split(/\s+/).map(w => w.charAt(0)).join("").slice(0, 3);
        return `<span class="${cls}" data-k="${esc(a.key)}">${url ? `<i style="background-image:url(${url})"></i>` : esc(short)}` +
            `${a.stacks ? " " + a.cnt : ""}<span class="tm"></span></span>`;
    }
    const HOLD_MS = 1500;
    function fitHeight(w) {
        const b = gameBox(), H = b.h, el = w.el;
        const right = el.offsetLeft + el.offsetWidth / 2 > b.x + b.w / 2;
        const limit = b.y + (state.mobile && right ? 0.5 : 0.86) * H;
        const bd = el.querySelector(".bd");
        const top0 = bd.offsetTop;
        const soft = Math.max(48, Math.round(Math.min(limit - el.offsetTop, 0.6 * H) - top0));
        const area = freeDrag ? { x: 0, y: 0, w: window.innerWidth, h: window.innerHeight } : b;
        const chrome = el.offsetHeight - bd.offsetHeight;
        const hard = Math.max(48, Math.floor(Math.min(area.h, 0.94 * window.innerHeight) - chrome));
        const em = parseFloat(getComputedStyle(bd).fontSize) || 8;
        const need = bd.scrollHeight;
        let room = Math.min(Math.max(soft, need), hard);
        if (need > room) {
            let cut = 0;
            for (const c of bd.children) {
                const bottom = c.offsetTop - top0 + c.offsetHeight;
                if (bottom + 0.2 * em <= room) cut = bottom; else break;
            }
            if (cut > 0) room = cut + 0.2 * em;
        }
        room = Math.max(48, Math.round(room));
        if (bd.style.maxHeight !== room + "px") bd.style.maxHeight = room + "px";
        if (w.want && !w.dragging) {
            const y = Math.round(Math.min(Math.max(area.y, w.want.y), Math.max(area.y, area.y + area.h - el.offsetHeight)));
            if (y !== el.offsetTop) clampTo(el, w.want.x, w.want.y);
        }
    }
    function paint(w, show, html, chips, list, keep) {
        const now = Date.now();
        if (show && w.forceUntil && now < w.forceUntil) {
            w.lastFull = now;
        } else if (show && list && list.length) w.lastFull = now;
        else if (show && w.lastFull && now - w.lastFull < HOLD_MS) { w.el.style.display = ""; return; }
        if (show && !(list && list.length) && !(w.forceUntil && now < w.forceUntil)) show = false;
        w.el.style.display = show ? "" : "none";
        if (!show) return;
        if (w.replace) place(w);
        keep = keep || "";
        const sig = html + chips + keep;
        if (sig !== w.sig) {
            w.sig = sig;
            w.el.querySelector(".bd").innerHTML = html;
            w.el.querySelector(".chips").innerHTML = chips;
            w.el.querySelector(".keep").innerHTML = keep;
        }
        fitHeight(w);
        const byKey = {};
        for (const a of list) byKey[a.key] = a;
        for (const node of w.el.querySelectorAll("[data-k]")) {
            const a = byKey[node.dataset.k];
            if (!a) continue;
            const tm = node.querySelector(".tm");
            const txt = node.classList.contains("chip") ? (a.left != null ? " " + Math.ceil(Math.max(0, a.left)) + "s" : "") : fmtTime(a.left);
            if (tm && tm.textContent !== txt) tm.textContent = txt;
            const bar = node.querySelector(".bar");
            if (bar) {
                const frac = a.left != null && a.dur > 0 ? Math.max(0, Math.min(1, a.left / a.dur)) : 0;
                const sx = "scaleX(" + frac.toFixed(3) + ")";
                if (bar.style.transform !== sx) bar.style.transform = sx;
                bar.classList.toggle("low", a.left != null && a.left < 3);
            }
        }
    }
    function draw() {
        reflow();
        if (paused()) return;
        const W = windows();
        if (!enabled()) {
            for (const w of Object.values(W)) { w.el.style.display = "none"; w.pill.style.display = "none"; }
            return;
        }
        iconBudget = 2;
        const now = Date.now();
        const mine = decorate(state.mine, now), theirs = decorate(state.theirs, now);
        const split = store.get("auraSplit", true);
        const hasMe = !!state.me;
        const tTitle = state.target ? "Target: " + state.target.name : "Target";
        const meHtml = section("You", mine, "me"), tHtml = section(tTitle, theirs, "t");
        const meChips = mine.filter(a => !keepFull(a)).map(chip).join(""), tChips = theirs.filter(a => !keepFull(a)).map(a => chip(a)).join("");
        const meKeep = mine.filter(keepFull).map(a => rowHtml(a, true)).join(""), tKeep = theirs.filter(keepFull).map(a => rowHtml(a, true)).join("");
        const anyMe = mine.length > 0, anyT = theirs.length > 0;
        const hid = k => store.get("auraHidden." + k, false);
        const pill = (w, show, list) => {
            w.pill.style.display = show ? "" : "none";
            w.pill.classList.toggle("max", list.some(a => a.full));
            w.pill.classList.toggle("noheal", list.some(a => a.sp && a.sp.kind === "noheal"));
        };
        if (!split) {
            const any = hasMe && (anyMe || anyT);
            paint(W.all, hasMe && !hid("all"), meHtml + (state.target ? tHtml : ""), meChips + (state.target ? tChips : ""), mine.concat(theirs), meKeep + (state.target ? tKeep : ""));
            pill(W.all, any && hid("all"), mine.concat(theirs));
            paint(W.me, false); pill(W.me, false, []);
            paint(W.t, false); pill(W.t, false, []);
        } else {
            paint(W.all, false); pill(W.all, false, []);
            paint(W.me, hasMe && !hid("me"), meHtml, meChips, mine, meKeep);
            pill(W.me, hasMe && anyMe && hid("me"), mine);
            paint(W.t, hasMe && !hid("t"), tHtml, tChips, theirs, tKeep);
            pill(W.t, hasMe && !!state.target && anyT && hid("t"), theirs);
        }
    }
    window.MvsAuras = {
        show(which) {
            const W = windows();
            const w = store.get("auraSplit", true) ? (which === "t" ? W.t : W.me) : W.all;
            store.set("auraHidden." + w.key, false);
            if (w.el.classList.contains("min")) toggleMin(w);
            w.forceUntil = Date.now() + 5000;
            w.sig = "";
            gate.me.at = gate.t.at = 0;
            poll();
            draw();
        }
    };
    const stats = window.__mvsAuraStats = { polls: 0, pollMs: 0, pollMax: 0, draws: 0, drawMs: 0, drawMax: 0, wait: POLL_MS, since: Date.now() };
    function loopPoll() {
        const t0 = performance.now();
        try { poll(); } catch (e) { }
        const d = performance.now() - t0;
        stats.polls++; stats.pollMs += d; stats.pollMax = Math.max(stats.pollMax, d);
        stats.wait = Math.min(1000, Math.max(POLL_MS, 20 * d));
        setTimeout(loopPoll, stats.wait);
    }
    function loopDraw() {
        const t0 = performance.now();
        try { draw(); } catch (e) { }
        const d = performance.now() - t0;
        stats.draws++; stats.drawMs += d; stats.drawMax = Math.max(stats.drawMax, d);
        setTimeout(loopDraw, Math.min(1000, Math.max(DRAW_MS, 20 * d)));
    }
    setTimeout(loopPoll, POLL_MS);
    setTimeout(loopDraw, DRAW_MS);
})();
