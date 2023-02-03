using HarmonyLib;
using RimWorld;
using RimWorldRealFoW.Utils;
using Verse;

namespace RimWorldRealFoW.Detours {
	public static class _FertilityGrid {

		public static void CellBoolDrawerGetBoolInt_Postfix(int index, ref FertilityGrid __instance, ref bool __result) {
			if (__result) {
				Map map = __instance.map;
				MapComponentSeenFog mapCmq = map.getMapComponentSeenFog();
				if (mapCmq != null) {
					__result = mapCmq.isKnownToRealPlayerFaction(index);
				}
			}
		}
	}
}
