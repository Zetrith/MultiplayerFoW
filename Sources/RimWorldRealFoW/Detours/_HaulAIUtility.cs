﻿using RimWorldRealFoW.Utils;
using Verse;
using Verse.AI;

namespace RimWorldRealFoW.Detours {
	public static class _HaulAIUtility {
		public static bool HaulToStorageJob_Prefix(Pawn p, Thing t, Job __result) {
			// MP: fowIsVisible for simulation
			// MP: disabled for now
			
			// if (p.Faction != null && p.Faction.IsPlayer && !t.fowIsVisible()) {
			// 	__result = null;
			// 	return false;
			// }

			return true;
		}
	}
}
