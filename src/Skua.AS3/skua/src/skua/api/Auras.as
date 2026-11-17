package skua.api {
import flash.events.TimerEvent;
import flash.utils.Timer;
import flash.utils.getTimer;
import skua.Main;
public class Auras {
    private static var _cleanupTimer:Timer = null;
    private static var _initTimer:Timer = null;
    private static var _initialized:Boolean = false;
    public function Auras() {
        super();
    }
    public static function initialize():void {
        if (_initialized) {
            return;
        }
        _initTimer = new Timer(500);
        _initTimer.addEventListener(TimerEvent.TIMER, checkGameReady);
        _initTimer.start();
    }
    private static function checkGameReady(event:TimerEvent):void {
        try {
            var world:* = Main.instance.game.world;
            if (world && world.uoTree && world.monTree) {
                _initTimer.stop();
                _initTimer.removeEventListener(TimerEvent.TIMER, checkGameReady);
                _initTimer = null;
                _initialized = true;
                Main.instance.external.debug("World load check passed, initializing Auras API.");
                startAuraAutoCleanup(5);
                watchAuras();
            }
        } catch (e:Error) {
        }
    }
    public static function GetAuraSnapshots(subject:String):String {
        var world:* = Main.instance.game.world;
        var leaf:* = subject == "Self"
            ? world.uoTree[Main.instance.game.sfc.myUserName.toLowerCase()]
            : (world.myAvatar.target != null ? world.myAvatar.target.dataLeaf : null);
        var snapshots:Array = [];
        if (leaf == null || leaf.hudAuras == null) {
            return JSON.stringify(snapshots);
        }
        var now:int = getTimer();
        for (var name:String in leaf.hudAuras) {
            var aura:* = leaf.hudAuras[name];
            if (aura == null) {
                continue;
            }
            snapshots.push({
                nam: name,
                n: int(aura.n),
                dur: Number(aura.dur),
                remaining: Number(aura.end) > 0 ? Math.max(0, (Number(aura.end) - now) / 1000) : 0,
                persist: aura.persist == true,
                icon: aura.icon != null ? String(aura.icon) : "",
                desc: world.auraDescs != null && world.auraDescs[name] != null ? String(world.auraDescs[name]) : "",
                seq: int(aura.seq)
            });
        }
        snapshots.sortOn("seq", Array.NUMERIC);
        return JSON.stringify(snapshots);
    }
    public static function getSubjectAuras(subject:String):Array {
        if (subject == 'Self')
        {
            var userObj:* = Main.instance.game.world.uoTree[Main.instance.game.sfc.myUserName.toLowerCase()];
            return rebuildAuraArray(userObj.auras)
        }
        else
        {
            var monID:int = 0;
            if (Main.instance.game.world.myAvatar.target != null) {
                monID = Main.instance.game.world.myAvatar.target.dataLeaf.MonMapID;
            }
            var monObj:* = Main.instance.game.world.monTree[monID];
            return rebuildAuraArray(monObj.auras)
        }
    }
    public static function  rebuildAuraArray(auras:Object):Array {
        var rebuiltAuras:Array = [];
        if (!auras) {
            return rebuiltAuras;
        }
        for (var i:int = 0; i < auras.length; i++) {
            var aura:Object = auras[i];
            if (!aura) {
                continue;
            }
            if (!aura.hasOwnProperty("nam") || !aura.nam) {
                continue;
            }
            if (aura.e == 1) {
                continue;
            }
            var rebuiltAura:Object = {};
            var hasVal:Boolean = false;
            for (var key:String in aura) {
                if (key == "cLeaf") {
                    rebuiltAura[key] = "cycle_";
                } else if (key == "val") {
                    var rawVal:* = aura[key];
                    if (rawVal == null || isNaN(rawVal)) {
                        rebuiltAura[key] = 1;
                    } else {
                        rebuiltAura[key] = rawVal;
                    }
                    hasVal = true;
                } else {
                    rebuiltAura[key] = aura[key];
                }
            }
            if (!hasVal) {
                rebuiltAura.val = 1;
            }
            rebuiltAuras.push(rebuiltAura);
        }
        return rebuiltAuras;
    }
    public static function rebuilduoTree(playerName:String):Object {
        var plrUser:String = playerName.toLowerCase();
        var userObj:* = Main.instance.game.world.uoTree[plrUser];
        if (!userObj) {
            return {};
        }
        var rebuiltObj:Object = {};
        for (var prop:String in userObj) {
            if (prop == "auras") {
                rebuiltObj[prop] = rebuildAuraArray(userObj.auras);
            } else {
                rebuiltObj[prop] = userObj[prop];
            }
        }
        return rebuiltObj;
    }
    public static function rebuildmonTree(monID:int):Object {
        var monObj:* = Main.instance.game.world.monTree[monID];
        if (!monObj) {
            return {};
        }
        var rebuiltObj:Object = {};
        for (var prop:String in monObj) {
            if (prop == "auras") {
                rebuiltObj[prop] = rebuildAuraArray(monObj.auras);
            } else {
                rebuiltObj[prop] = monObj[prop];
            }
        }
        return rebuiltObj;
    }
    public static function HasAnyActiveAura(subject:String, auraNames:String):String {
        var auraList:Array = auraNames.split(',');
        var auras:Object = null;
        try {
            auras = getSubjectAuras(subject);
        } catch (e:Error) {
            return "false";
        }
        var auraCount:int = auras.length;
        var auraListCount:int = auraList.length;
        for (var i:int = 0; i < auraCount; i++) {
            var aura:Object = auras[i];
            var auraNameLower:String = aura.nam.toLowerCase();
            for (var j:int = 0; j < auraListCount; j++) {
                if (auraNameLower == auraList[j].toLowerCase().replace(/^\s+|\s+$/g, "")) {
                    return "true";
                }
            }
        }
        return "false";
    }
    public static function GetAurasValue(subject:String, auraName:String):Number {
        var aura:Object = null;
        var auras:Array = null;
        try {
            auras = getSubjectAuras(subject);
        } catch (e:Error) {
            return 442;
        }
        var lowerAuraName:String = auraName.toLowerCase();
        for (var i:int = 0; i < auras.length; i++) {
            aura = auras[i];
            if (aura.nam.toLowerCase() == lowerAuraName) {
                return aura.val;
            }
        }
        return 444;
    }
    public static function GetPlayerAura(playerName:String):String {
        var plrUser:String = playerName.toLowerCase();
        try {
            var userObj:* = Main.instance.game.world.uoTree[plrUser];
            if (!userObj) {
                return '[]';
            }
            return JSON.stringify(rebuildAuraArray(userObj.auras))
        }
        catch (e:Error) {
        }
        return '[]';
    }
    public static function GetMonsterAuraByName(monsterName:String):String {
        try {
            var monID:int = 0;
            var lowerMonsterName:String = monsterName.toLowerCase();
            for each (var monster:* in Main.instance.game.world.monsters)
            {
                if (monster && monster.objData.strMonName.toLowerCase() == lowerMonsterName) {
                    monID = monster.objData.MonMapID
                }
            }
            var monObj:* = Main.instance.game.world.monTree[monID];
            if (!monObj) {
                return 'Error: Couldn\'t get Monster Object Tree';
            }
            return JSON.stringify(rebuildAuraArray(monObj.auras));
        }
        catch (e:Error) {
        }
        return '[]';
    }
    public static function GetMonsterAuraByID(monID:int):String {
        try {
            var monObj:* = Main.instance.game.world.monTree[monID];
            if (!monObj) {
                return 'Error: Couldn\'t get Monster Object Tree';
            }
            return JSON.stringify(rebuildAuraArray(monObj.auras));
        }
        catch (e:Error) {
        }
        return '[]';
    }
    private static function startAuraAutoCleanup(intervalSeconds:int = 5):void {
        if (_cleanupTimer != null) {
            return;
        }
        _cleanupTimer = new Timer(intervalSeconds * 1000);
        _cleanupTimer.addEventListener(TimerEvent.TIMER, onCleanupTimer);
        _cleanupTimer.start();
    }
    public static function stopAuraAutoCleanup():void {
        if (_cleanupTimer == null) {
            return;
        }
        _cleanupTimer.stop();
        _cleanupTimer.removeEventListener(TimerEvent.TIMER, onCleanupTimer);
        _cleanupTimer = null;
    }
    private static function onCleanupTimer(event:TimerEvent):void {
        var world:* = Main.instance.game.world;
        if (!world) {
            return;
        }
        var cleanedCount:int = 0;
        for (var playerName:String in world.uoTree) {
            var userObj:* = world.uoTree[playerName];
            if (userObj && userObj.auras is Array) {
                cleanedCount += cleanExpiredAuras(userObj.auras);
            }
        }
        for (var monID:String in world.monTree) {
            var monObj:* = world.monTree[monID];
            if (monObj && monObj.auras is Array) {
                cleanedCount += cleanExpiredAuras(monObj.auras);
            }
        }
    }
    private static function cleanExpiredAuras(auras:Array):int {
        var removedCount:int = 0;
        for (var i:int = auras.length - 1; i >= 0; i--) {
            var aura:Object = auras[i];
            if (!aura || !aura.hasOwnProperty("nam") || !aura.nam || aura.e == 1) {
                auras.splice(i, 1);
                removedCount++;
            }
        }
        return removedCount;
    }
    private static var _watchSfc:* = null;
    private static var _watchCasters:Object = {};
    private static var _watchLog:Array = [];
    private static function watchAuras():void {
        try {
            var sfc:* = Main.instance.game != null ? Main.instance.game.sfc : null;
            if (sfc == null || sfc == _watchSfc) {
                return;
            }
            if (_watchSfc != null) {
                _watchSfc.removeEventListener("onExtensionResponse", onAuraPacket);
            }
            _watchSfc = sfc;
            sfc.addEventListener("onExtensionResponse", onAuraPacket);
        } catch (e:Error) {
        }
    }
    private static function onAuraPacket(evt:*):void {
        try {
            var o:* = evt.params.dataObj;
            if (o == null || typeof o != "object" || o.cmd == null) {
                return;
            }
            if (o.cmd == "moveToArea") {
                _watchCasters = {};
                quickVisited(o);
                return;
            }
            var list:Array = null;
            if (o.cmd == "ct" && o.a is Array) {
                list = o.a;
            } else if (String(o.cmd).indexOf("aura") == 0) {
                list = [o];
            }
            if (list == null) {
                return;
            }
            var byAnim:Object = {};
            if (o.anims is Array) {
                for each (var an:* in o.anims) {
                    if (an != null && an.cInf != null && an.tInf != null) {
                        for each (var ti:String in String(an.tInf).split(",")) {
                            byAnim[ti] = String(an.cInf);
                        }
                    }
                }
            }
            var now:int = getTimer();
            for each (var e:* in list) {
                if (e == null || e.tInf == null || !(e.auras is Array)) {
                    continue;
                }
                var cmd:String = String(e.cmd);
                var tinf:String = String(e.tInf);
                var c:String = e.cInf != null ? String(e.cInf) : (byAnim[tinf] != null ? String(byAnim[tinf]) : "");
                var src:String = e.cInf != null ? "packet" : (c != "" ? "skill" : "none");
                var names:Array = [];
                for each (var a:* in e.auras) {
                    if (a == null || a.nam == null) {
                        continue;
                    }
                    names.push(String(a.nam));
                    if (cmd.indexOf("aura+") == 0 && c != "") {
                        _watchCasters[tinf + "|" + String(a.nam)] = {c: c, s: src, t: now};
                    } else if (cmd.indexOf("aura+") == 0 || cmd.indexOf("aura-") == 0) {
                        delete _watchCasters[tinf + "|" + String(a.nam)];
                    }
                }
                _watchLog.push({t: now, cmd: cmd, c: c, tg: tinf, s: src, n: names.join(",")});
                if (_watchLog.length > 60) {
                    _watchLog.shift();
                }
            }
        } catch (err:Error) {
        }
    }
    public static function auraCasters():String {
        watchAuras();
        return JSON.stringify({now: getTimer(), casters: _watchCasters, log: _watchLog.slice(-30)});
    }
    private static const QUICK_ACTIONS:Object = {
       "Area List":1, "Player HP Bar":1, "Hide Players":1, "Hide Monsters":1, "Hide UI":1,
       "Stats Overview":1, "Battle Analyzer":1, "Battle Analyzer Toggle":1,
       "Friends List":1, "Friendships UI":1, "Character Panel":1, "Outfits":1, "Options":1,
       "Quest Log":1, "Inventory":1, "Bank":1,
       "Target Random Monster":1, "Cancel Target":1, "Rest":1, "Jump":1, "Decline All Drops":1,
       "World Camera":1
    };
    private static const QUICK_MAX_LOG:int = 40;
    private static var _quickLog:Array = [];
    public static function quickRun(name:String):Boolean {
        try {
            var game:* = Main.instance.game;
            if (game == null || game.world == null || game.world.myAvatar == null) {
                return false;
            }
            watchAuras();
            if (name == "Chat") {
                game.chatF.openMsgEntry();
                return true;
            }
            if (QUICK_ACTIONS[name] == null) {
                return false;
            }
            game.runBind(name);
            return true;
        } catch (e:Error) {
        }
        return false;
    }
    private static function quickCleanMap(map:String):String {
        var m:String = map == null ? "" : String(map).toLowerCase().replace(/^\s+|\s+$/g, "");
        return /^[a-z0-9_]{2,40}(-[0-9]{1,6})?$/.test(m) ? m : "";
    }
    public static function quickTravel(map:String):String {
        try {
            var m:String = quickCleanMap(map);
            if (m == "") {
                return "bad";
            }
            var game:* = Main.instance.game;
            if (game == null || game.world == null || game.world.myAvatar == null) {
                return "nogame";
            }
            watchAuras();
            game.world.gotoTown(m, "Enter", "Spawn");
            return "ok";
        } catch (e:Error) {
        }
        return "nogame";
    }
    public static function quickVisited(o:*):void {
        try {
            var m:String = o.strMapName != null ? String(o.strMapName) : (o.areaName != null ? String(o.areaName) : "");
            m = m.toLowerCase().replace(/-[0-9]+$/, "");
            if (quickCleanMap(m) == "") {
                return;
            }
            var i:int = _quickLog.indexOf(m);
            if (i >= 0) {
                _quickLog.splice(i, 1);
            }
            _quickLog.unshift(m);
            if (_quickLog.length > QUICK_MAX_LOG) {
                _quickLog.length = QUICK_MAX_LOG;
            }
        } catch (e:Error) {
        }
    }
    public static function quickState():String {
        var map:String = "";
        try {
            watchAuras();
            var game:* = Main.instance.game;
            if (game != null && game.world != null && game.world.myAvatar != null && game.world.strMapName != null) {
                map = String(game.world.strMapName).toLowerCase();
            }
        } catch (e:Error) {
        }
        return JSON.stringify({map: map, log: _quickLog});
    }
}
}
