using RimWorld;
using RimWorldRealFoW.Utils;
using Verse;

namespace RimWorldRealFoW;

// MP: Add debug tools
public static class DebugTools
{
    private const string FowCategory = "Multiplayer FoW";
    
    [DebugAction(category = FowCategory, actionType = DebugActionType.ToolMap)]
    private static void FowFlashViewBlockers()
    {
        foreach (var cell in CellRect.CenteredOn(UI.MouseCell(), 10))
            if (Find.CurrentMap.getMapComponentSeenFog().viewBlockerCells[(cell.z * Find.CurrentMap.Size.x) + cell.x])
                Find.CurrentMap.debugDrawer.FlashCell(cell);
    }
    
    [DebugAction(name = "Clear all known cells (cur faction)", category = FowCategory, actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap)]
    private static void ClearAllKnown()
    {
        for (int i = 0; i < Find.CurrentMap.Size.x * Find.CurrentMap.Size.z; i++)
            Find.CurrentMap.getMapComponentSeenFog().unrevealCell(Faction.OfPlayer, i);
    }
    
    [DebugAction(name = "Make all cells known (cur faction)", category = FowCategory, actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap)]
    private static void MakeAllKnown()
    {
        for (int i = 0; i < Find.CurrentMap.Size.x * Find.CurrentMap.Size.z; i++)
            Find.CurrentMap.getMapComponentSeenFog().revealCell(Faction.OfPlayer, i);
    }
}