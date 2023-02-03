using HarmonyLib;
using RimWorldRealFoW.Utils;
using Verse;

namespace RimWorldRealFoW.Patches;

[HarmonyPatch(typeof(MapDrawer), nameof(MapDrawer.RegenerateEverythingNow))]
public static class RegenEverythingPatch
{
    static void Prefix(MapDrawer __instance)
    {
        __instance.map.getMapComponentSeenFog().UpdateThingVisibility();
    }
}