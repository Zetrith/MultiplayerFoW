﻿using UnityEngine;
using Verse;

namespace RimWorldRealFoW {
	public static class _GenMapUI {
		public static void DrawThingLabel(Thing thing, string text, Color textColor) {
			if (thing.isVisible()) {
				GenMapUI.DrawThingLabel(GenMapUI.LabelDrawPosFor(thing, -0.4f), text, textColor);
			}
		}
	}
}