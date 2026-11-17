package skua.module {
import skua.Main;
public class DropsUI extends Module {
    private static var _dropsOn:Boolean = false;
    private static var _dropsKey:String = "";
    public function DropsUI() {
        super("DropsUI");
        enabled = true;
    }
    private static function dropStore(game:*):* {
        if (!_dropsOn || game == null || game.litePreference == null) return null;
        if (!game.litePreference.data.bCustomDrops) {
            game.litePreference.data.bCustomDrops = true;
            game.litePreference.flush();
        }
        if (game.cDropsUI == null) {
            var cls:Class = game.loaderInfo.applicationDomain.getDefinition("liteAssets.draw.customDrops") as Class;
            game.cDropsUI = new cls(game);
        }
        try {
            if (game.ui.mcPortrait.iconDrops.visible) game.ui.mcPortrait.iconDrops.visible = false;
        } catch (e:Error) {
        }
        return game.cDropsUI;
    }
    private static function findDrop(store:*, id:int):* {
        for each (var item:* in store.invTree) {
            if (item != null && int(item.ItemID) == id) return item;
        }
        return null;
    }
    override public function onFrame(game:*):void {
        var store:* = dropStore(game);
        if (store == null) return;
        var list:Array = [];
        for each (var item:* in store.invTree) {
            if (item == null) continue;
            var qty:* = store.itemCount[item.dID];
            list.push({
                "id": int(item.ItemID),
                "n": String(item.sName),
                "q": qty == null ? int(item.dQty) : int(qty),
                "t": String(item.sType),
                "ac": int(item.bCoins) == 1,
                "mem": int(item.bUpg) == 1,
                "r": int(item.iRty),
                "i": String(item.sIcon)
            });
        }
        var key:String = JSON.stringify(list);
        if (key == _dropsKey) return;
        _dropsKey = key;
        try {
            Main.instance.external.call("mvsDrops", key);
        } catch (e:Error) {
        }
    }
    public static function dropsOn(on:Boolean):void {
        _dropsOn = on;
        _dropsKey = "";
        if (on) return;
        try {
            var game:* = Main.instance.getGame();
            if (game.cDropsUI != null) game.ui.mcPortrait.iconDrops.visible = true;
        } catch (e:Error) {
        }
    }
    public static function dropAccept(id:int):String {
        try {
            var store:* = dropStore(Main.instance.getGame());
            var item:* = store == null ? null : findDrop(store, id);
            if (item == null) return "";
            return store.requestDrop(item, true) ? "ok" : "full";
        } catch (e:Error) {
        }
        return "";
    }
    public static function dropPreview(id:int):void {
        try {
            var store:* = dropStore(Main.instance.getGame());
            var item:* = store == null ? null : findDrop(store, id);
            if (item != null) store.previewDrop(item);
        } catch (e:Error) {
        }
    }
    public static function dropDecline(id:int):void {
        try {
            var store:* = dropStore(Main.instance.getGame());
            var item:* = store == null ? null : findDrop(store, id);
            if (item != null) store.onBtNo(item);
        } catch (e:Error) {
        }
    }
}
}
