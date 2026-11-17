package skua {
import flash.external.ExternalInterface;
import skua.api.Auras;
import skua.api.Combat;
import skua.api.Inventory;
import skua.api.Monster;
import skua.api.Player;
import skua.api.Server;
import skua.api.Shop;
import skua.api.Skills;
import skua.api.World;
import skua.module.ClassRatingsUI;
import skua.module.DropsUI;
import skua.module.GameMenusUI;
import skua.module.Modules;
import skua.remote.RemoteRegistry;
public class Externalizer {
    public function Externalizer() {
        super();
    }
    public function init(root:Main):void {
        this.addCallback("loadClient", Main.loadGame);
        this.addCallback("setBackgroundValues", Main.setBackgroundValues);
        this.addCallback("isTrue", Main.isTrue);
        this.addCallback("isNull", Main.isNull);
        this.addCallback("getGameObject", Main.getGameObject);
        this.addCallback("getGameObjectS", Main.getGameObjectS);
        this.addCallback("getGameObjectKey", Main.getGameObjectKey);
        this.addCallback("setGameObject", Main.setGameObject);
        this.addCallback("setGameObjectKey", Main.setGameObjectKey);
        this.addCallback("getArrayObject", Main.getArrayObject);
        this.addCallback("setArrayObject", Main.setArrayObject);
        this.addCallback("callGameFunction", Main.callGameFunction);
        this.addCallback("callGameFunction0", Main.callGameFunction0);
        this.addCallback("selectArrayObjects", Main.selectArrayObjects);
        this.addCallback("connectToServer", Server.connectToServer);
        this.addCallback("clickServer", Server.clickServer);
        this.addCallback("loadPolicyFile", Server.loadPolicyFile);
        this.addCallback("loadPolicyFiles", Server.loadPolicyFiles);
        this.addCallback("killLoginModals", Server.killLoginModals);
        this.addCallback("resetConnectionState", Server.resetConnectionState);
        this.addCallback("getActiveModalMessage", Server.getActiveModalMessage);
        this.addCallback("isLoggedIn", Player.isLoggedIn);
        this.addCallback("isKicked", Player.isKicked);
        this.addCallback("walkTo", Player.walkTo);
        this.addCallback("untargetSelf", Player.untargetSelf);
        this.addCallback("attackPlayer", Player.attackPlayer);
        this.addCallback("getAvatar", Player.getAvatar);
        this.addCallback("getLoadouts", Player.getLoadouts);
        this.addCallback("gender", Player.Gender);
        this.addCallback("rejectExcept", Player.rejectExcept);
        this.addCallback("jumpCorrectRoom", World.jumpCorrectRoom);
        this.addCallback("disableDeathAd", World.disableDeathAd);
        this.addCallback("skipCutscenes", World.skipCutscenes);
        this.addCallback("hidePlayers", World.hidePlayers);
        this.addCallback("disableFX", World.disableFX);
        this.addCallback("killLag", World.killLag);
        this.addCallback("availableMonsters", Monster.availableMonstersInCell);
        this.addCallback("getMonsters", Monster.getMonsters);
        this.addCallback("getTargetMonster", Monster.getTargetMonster);
        this.addCallback("attackMonsterName", Monster.attackMonsterByName);
        this.addCallback("attackMonsterID", Monster.attackMonsterByID);
        this.addCallback("canUseSkill", Skills.canUseSkill);
        this.addCallback("useSkill", Skills.useSkill);
        this.addCallback("infiniteRange", Combat.infiniteRange);
        this.addCallback("magnetize", Combat.magnetize);
        this.addCallback("hasInventoryCategories", Inventory.hasInventoryCategories);
        this.addCallback("inventoryBagUsedSlots", Inventory.inventoryBagUsedSlots);
        this.addCallback("inventoryMiscSlots", Inventory.inventoryMiscSlots);
        this.addCallback("inventoryMiscUsedSlots", Inventory.inventoryMiscUsedSlots);
        this.addCallback("isFavoriteItem", Inventory.isFavoriteItem);
        this.addCallback("inventoryPool", Inventory.inventoryPool);
        this.addCallback("inventoryHasSpaceFor", Inventory.inventoryHasSpaceFor);
        this.addCallback("buyItemByName", Shop.buyItemByName);
        this.addCallback("buyItemByID", Shop.buyItemByID);
        this.addCallback("getShopItem", Shop.getShopItem);
        this.addCallback("getShopItemByID", Shop.getShopItemByID);
        this.addCallback("getUnmetPurchaseRequirements", Shop.getUnmetPurchaseRequirements);
        this.addCallback("getSubjectAuras", Auras.getSubjectAuras);
        this.addCallback("GetAuraSnapshots", Auras.GetAuraSnapshots);
        this.addCallback("HasAnyActiveAura", Auras.HasAnyActiveAura);
        this.addCallback("GetPlayerAura", Auras.GetPlayerAura);
        this.addCallback("GetMonsterAuraByName", Auras.GetMonsterAuraByName);
        this.addCallback("GetMonsterAuraByID", Auras.GetMonsterAuraByID);
        this.addCallback("rebuildAuraArray", Auras.rebuildAuraArray);
        this.addCallback("GetAurasValue", Auras.GetAurasValue);
        this.addCallback("rebuilduoTree", Auras.rebuilduoTree);
        this.addCallback("rebuildmonTree", Auras.rebuildmonTree);
        this.addCallback("mvsAuraCasters", Auras.auraCasters);
        this.addCallback("mvsQuick", Auras.quickRun);
        this.addCallback("mvsTravel", Auras.quickTravel);
        this.addCallback("mvsQuickState", Auras.quickState);
        this.addCallback("sendClientPacket", Main.sendClientPacket);
        this.addCallback("catchPackets", Main.catchPackets);
        this.addCallback("injectScript", Main.injectScript);
        this.addCallback("lnkCreate", RemoteRegistry.ext_create);
        this.addCallback("lnkDestroy", RemoteRegistry.ext_destroy);
        this.addCallback("lnkGetChild", RemoteRegistry.ext_getChild);
        this.addCallback("lnkDeleteChild", RemoteRegistry.ext_deleteChild);
        this.addCallback("lnkGetValue", RemoteRegistry.ext_getValue);
        this.addCallback("lnkSetValue", RemoteRegistry.ext_setValue);
        this.addCallback("lnkCall", RemoteRegistry.ext_call);
        this.addCallback("lnkGetArray", RemoteRegistry.ext_getArray);
        this.addCallback("lnkSetArray", RemoteRegistry.ext_setArray);
        this.addCallback("fcCreate", RemoteRegistry.ext_fcCreate);
        this.addCallback("fcPush", RemoteRegistry.ext_fcPushArgs);
        this.addCallback("fcPushDirect", RemoteRegistry.ext_fcPushArgsDirect);
        this.addCallback("fcClear", RemoteRegistry.ext_fcClearArgs);
        this.addCallback("fcCallFlash", RemoteRegistry.ext_fcCallFlash);
        this.addCallback("fcCall", RemoteRegistry.ext_fcCall);
        this.addCallback("modEnable", Modules.enable);
        this.addCallback("modDisable", Modules.disable);
        this.addCallback("mvsClassRatings", ClassRatingsUI.setRatings);
        this.addCallback("mvsRatingFocus", ClassRatingsUI.setFocus);
        this.addCallback("mvsMenuClick", GameMenusUI.menuClick);
        this.addCallback("mvsMenuClose", GameMenusUI.menuClose);
        this.addCallback("mvsGameButton", GameMenusUI.gameButton);
        this.addCallback("mvsDropsOn", DropsUI.dropsOn);
        this.addCallback("mvsDropAccept", DropsUI.dropAccept);
        this.addCallback("mvsDropDecline", DropsUI.dropDecline);
		this.debug("Externalizer::init done.");
        this.call("requestLoadGame");
    }
    public function addCallback(name:String, func:Function):void {
        ExternalInterface.addCallback(name, func);
    }
    public function call(name:String, ...rest):* {
        return ExternalInterface.call(name, rest);
    }
    public function debug(message:String):void {
        this.call("debug", message);
    }
}
}
