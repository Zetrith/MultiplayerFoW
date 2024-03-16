using HarmonyLib;
using RimWorldRealFoW.ThingComps.ThingSubComps;
using Verse;

namespace RimWorldRealFoW.Patches;

[HarmonyPatch(typeof(Thing), nameof(Thing.SpawnSetup))]
public class ThingSpawnPatch
{
    static void Postfix(Thing __instance)
    {
        var t = __instance;
        
        if (t.def.building != null && 
            t.def.saveCompressible &&
            t.def.building.isNaturalRock)
        {
            CompViewBlockerWatcher.updateViewBlockerCells(t, t.Map, true);
        }
    }
}

[HarmonyPatch(typeof(Thing), nameof(Thing.DeSpawn))]
public class ThingDeSpawnPatch
{
    static void Prefix(Thing __instance)
    {
        var t = __instance;
        
        if (t.def.building != null && 
            t.def.saveCompressible &&
            t.def.building.isNaturalRock)
        {
            CompViewBlockerWatcher.updateViewBlockerCells(t, t.Map, false);
        }
    }
}