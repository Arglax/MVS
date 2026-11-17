namespace Skua.Core.Interfaces;
public interface IScriptLite
{
bool CharacterSelectScreen { get; set; }
bool CustomDropsUI { get; set; }
bool QuestLogTurnIns { get; set; }
bool BattlePets { get; set; }
bool StaticPlayerArt { get; set; }
bool ChatFilter { get; set; }
bool ChatUI { get; set; }
bool AurasUI { get; set; }
bool ColorSets { get; set; }
bool DraggableDrops { get; set; }
bool DisableChatScrolling { get; set; }
bool DisableDamageNumbers { get; set; }
bool DisableSoundFx { get; set; }
bool DisableQuestPopup { get; set; }
bool DisableQuestTracker { get; set; }
bool QuestPinner { get; set; }
bool QuestProgressNotifications { get; set; }
bool VisualSkillCooldowns { get; set; }
bool HideGroundItems { get; set; }
bool HideHealingBubbles { get; set; }
bool DisableAuraAnimations { get; set; }
bool HidePlayerNames { get; set; }
bool Debugger { get; set; }
bool DebugPacket { get; set; }
bool DisableDamageStrobe { get; set; }
bool DisableMonsterAnimation { get; set; }
bool DisableRedWarning { get; set; }
bool DisableSelfAnimation { get; set; }
bool DisableSkillAnimation { get; set; }
bool DisableWeaponAnimation { get; set; }
bool QuantityWarnings { get; set; }
bool FreezeMonsterPosition { get; set; }
bool HidePlayers { get; set; }
bool HideUI { get; set; }
bool InvisibleMonsters { get; set; }
bool ReacceptQuest { get; set; }
bool ShowNameTags { get; set; }
bool ShowShadows { get; set; }
bool HideGuildNamesOnly { get; set; }
bool HideYourNameOnly { get; set; }
bool ShowYourGroundItemOnly { get; set; }
bool ShowYourAuraAnimationOnly { get; set; }
bool ShowMonsterType { get; set; }
bool SmoothBackground { get; set; }
bool UntargetDead { get; set; }
bool UntargetSelf { get; set; }
T? Get<T>(string optionName);
void Set<T>(string optionName, T value);
}
