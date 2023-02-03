//   Copyright 2017 Luca De Petrillo
//
//   Licensed under the Apache License, Version 2.0 (the "License");
//   you may not use this file except in compliance with the License.
//   You may obtain a copy of the License at
//
//       http://www.apache.org/licenses/LICENSE-2.0
//
//   Unless required by applicable law or agreed to in writing, software
//   distributed under the License is distributed on an "AS IS" BASIS,
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//   See the License for the specific language governing permissions and
//   limitations under the License.

using RimWorld;
using RimWorldRealFoW.SectionLayers;
using RimWorldRealFoW.ShadowCasters;
using RimWorldRealFoW.ThingComps;
using RimWorldRealFoW.ThingComps.ThingSubComps;
using RimWorldRealFoW.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using Multiplayer.API;
using UnityEngine;
using Verse;

namespace RimWorldRealFoW {
	public class MapComponentSeenFog : MapComponent {
		private short[][] factionsShownCells = null; // MP: per faction
		private Dictionary<int, bool[]> knownCells = null; // MP: per player faction

		public int[] playerVisibilityChangeTick = null;

		public bool[] viewBlockerCells = null;

		private IntVec3[] idxToCellCache;

		private List<CompHideFromPlayer>[] compHideFromPlayerGrid;
		private byte[] compHideFromPlayerGridCount;

		public List<CompAffectVision>[] compAffectVisionGrid;

		private Designation[] mineDesignationGrid;

		private int maxFactionLoadId;

		private int mapCellLength;
		public int mapSizeX;
		public int mapSizeZ;
		private FogGrid fogGrid;

		private ThingGrid thingGrid;

		public bool initialized = false;

		public List<CompFieldOfViewWatcher> fowWatchers;

		private Section[] sections = null;
		int sectionsSizeX;
		int sectionsSizeY;

		int currentGameTick = 0;

		public MapComponentSeenFog(Map map) : base(map) {
			mapCellLength = map.cellIndices.NumGridCells;
			mapSizeX = map.Size.x;
			mapSizeZ = map.Size.z;

			fogGrid = map.fogGrid;
			thingGrid = map.thingGrid;

			fowWatchers = new List<CompFieldOfViewWatcher>(1000);

			maxFactionLoadId = 0;
			foreach (Faction faction in Find.World.factionManager.AllFactionsListForReading) {
				maxFactionLoadId = Math.Max(maxFactionLoadId, faction.loadID);
			}
			factionsShownCells = new short[maxFactionLoadId + 1][];

			knownCells = new();
			viewBlockerCells = new bool[mapCellLength];
			playerVisibilityChangeTick = new int[mapCellLength];

			mineDesignationGrid = new Designation[mapCellLength];

			idxToCellCache = new IntVec3[mapCellLength];
			compHideFromPlayerGrid = new List<CompHideFromPlayer>[mapCellLength];
			compHideFromPlayerGridCount = new byte[mapCellLength];
			compAffectVisionGrid = new List<CompAffectVision>[mapCellLength];
			for (int i = 0; i < mapCellLength; i++) {
				idxToCellCache[i] = CellIndicesUtility.IndexToCell(i, mapSizeX);

				compHideFromPlayerGrid[i] = new List<CompHideFromPlayer>(16);
				compHideFromPlayerGridCount[i] = 0;
				compAffectVisionGrid[i] = new List<CompAffectVision>(16);

				playerVisibilityChangeTick[i] = 0;
			}
		}

		public override void MapComponentTick() {
#if InternalProfile
			ProfilingUtils.startProfiling("0-calibration");
			ProfilingUtils.stopProfiling("0-calibration");
#endif

			currentGameTick = Find.TickManager.TicksGame;

			if (!initialized) {
				initialized = true;

				init();
			}
		}

		public short[] getFactionShownCells(Faction faction) {
			if (faction == null) {
				return null;
			}

			if (maxFactionLoadId < faction.loadID) {
				// Increase the jagged array.
				maxFactionLoadId = faction.loadID + 1;
				Array.Resize(ref factionsShownCells, maxFactionLoadId + 1);
			}

			// Lazy init faction shown grids (some mods could create dummy factions not used, causing a huge amount of memory waste).
			if (factionsShownCells[faction.loadID] == null) {
				factionsShownCells[faction.loadID] = new short[mapCellLength];
			}

			return factionsShownCells[faction.loadID];
		}

		public bool isShown(Faction faction, IntVec3 cell) {
			return isShown(faction, cell.x, cell.z);
		}
		
		public bool isShown(Faction faction, int idx) {
			return getFactionShownCells(faction)[idx] != 0;
		}
		
		public bool isShown(Faction faction, int x, int z) {
			return getFactionShownCells(faction)[(z * mapSizeX) + x] != 0;
		}
		
		public bool[] getFactionKnownCells(Faction faction) {
			if (faction == null) {
				return null;
			}

			if (knownCells.TryGetValue(faction.loadID, out bool[] arr))
				return arr;

			arr = knownCells[faction.loadID] = new bool[mapCellLength];
			return arr;
		}

		public ref bool isKnown(Faction faction, IntVec3 cell) {
			return ref isKnown(faction, cell.x, cell.z);
		}
		
		public ref bool isKnown(Faction faction, int x, int z) {
			return ref getFactionKnownCells(faction)[(z * mapSizeX) + x];
		}
		
		public ref bool isKnown(Faction faction, int idx) {
			return ref getFactionKnownCells(faction)[idx];
		}
		
		public ref bool isKnownToRealPlayerFaction(int idx) {
			return ref getFactionKnownCells(MpWrapper.RealPlayerFaction)[idx];
		}

		public void registerCompHideFromPlayerPosition(CompHideFromPlayer comp, int x, int z) {
			if (x >= 0 && z >= 0 && x < mapSizeX && z < mapSizeZ) {
				int idx = (z * mapSizeX) + x;
				compHideFromPlayerGrid[idx].Add(comp);
				compHideFromPlayerGridCount[idx]++;

			}
		}
		public void deregisterCompHideFromPlayerPosition(CompHideFromPlayer comp, int x, int z) {
			if (x >= 0 && z >= 0 && x < mapSizeX && z < mapSizeZ) {
				int idx = (z * mapSizeX) + x;
				compHideFromPlayerGrid[idx].Remove(comp);
				compHideFromPlayerGridCount[idx]--;
			}
		}

		public void registerCompAffectVisionPosition(CompAffectVision comp, int x, int z) {
			if (x >= 0 && z >= 0 && x < mapSizeX  && z < mapSizeZ) {
				compAffectVisionGrid[(z * mapSizeX) + x].Add(comp);
			}
		}
		public void deregisterCompAffectVisionPosition(CompAffectVision comp, int x, int z) {
			if (x >= 0 && z >= 0 && x < mapSizeX && z < mapSizeZ) {
				compAffectVisionGrid[(z * mapSizeX) + x].Remove(comp);
			}
		}

		public void registerMineDesignation(Designation des) {
			IntVec3 targetCell = des.target.Cell;
			mineDesignationGrid[(targetCell.z * mapSizeX) + targetCell.x] = des;
		}

		public void deregisterMineDesignation(Designation des) {
			IntVec3 targetCell = des.target.Cell;
			mineDesignationGrid[(targetCell.z * mapSizeX) + targetCell.x] = null;
		}
		
		private void init() {
			// Retrieve map sections and store in a linear array.
			Section[,] mapDrawerSections = map.mapDrawer.sections;
			sectionsSizeX = mapDrawerSections.GetLength(0);
			sectionsSizeY = mapDrawerSections.GetLength(1);

			sections = new Section[sectionsSizeX * sectionsSizeY];
			for (int y = 0; y < sectionsSizeY; y++) {
				for (int x = 0; x < sectionsSizeX; x++) {
					sections[y * sectionsSizeX + x] = mapDrawerSections[x, y];
				}
			}

			// Initialize mining designators (add notifications intercepted by detours aren't fired on load).
			List<Designation> designations = map.designationManager.AllDesignations;
			for (int i = 0; i < designations.Count; i++) {
				Designation des = designations[i];
				if (des.def == DesignationDefOf.Mine && !des.target.HasThing) {
					registerMineDesignation(des);
				}
			}

			// Reveal the starting position if home map and no pawns (landing).
			// MP: Disabled for now
			if (map.IsPlayerHome && map.mapPawns.ColonistsSpawnedCount == 0 && false)
			{
				var playerKnown = getFactionKnownCells(Faction.OfPlayer);
				
				IntVec3 playerStartSpot = MapGenerator.PlayerStartSpot;
				int baseViewRange = Mathf.RoundToInt(DefDatabase<RealFoWModDefaultsDef>.GetNamed(RealFoWModDefaultsDef.DEFAULT_DEF_NAME).baseViewRange);
				ShadowCaster.computeFieldOfViewWithShadowCasting(playerStartSpot.x, playerStartSpot.z, baseViewRange,
					viewBlockerCells, map.Size.x, map.Size.z, 
					false, null, null, null, // Directly updating known cells. No need to call incrementSeen.
					playerKnown, 0, 0, mapSizeX, 
					null, 0, 0, 0, 0, 0);

				for (int i = 0; i < mapCellLength; i++) {
					if (playerKnown[i]) {
						IntVec3 cell = CellIndicesUtility.IndexToCell(i, mapSizeX);
						foreach (Thing t in map.thingGrid.ThingsListAtFast(cell)) {
							CompMainComponent compMain = (CompMainComponent) t.TryGetComp(CompMainComponent.COMP_DEF);
							if (compMain != null && compMain.compHideFromPlayer != null) {
								compMain.compHideFromPlayer.forceSeen();
							}
						}
					}
				}
			}

			// Update all thing FoV
			foreach (Thing thing in map.listerThings.AllThings) {
				if (thing.Spawned) {
					CompMainComponent compMain = (CompMainComponent) thing.TryGetComp(CompMainComponent.COMP_DEF);
					if (compMain != null) {
						if (compMain.compComponentsPositionTracker != null) {
							compMain.compComponentsPositionTracker.updatePosition();
						}
						if (compMain.compFieldOfViewWatcher != null) {
							compMain.compFieldOfViewWatcher.updateFoV();
						}
					}
				}
			}

			// Redraw everything.
			// MP: push/pop is desync fix, consider a better solution
			Rand.PushState();
			map.mapDrawer.RegenerateEverythingNow();
			Rand.PopState();
		}

		// Called from RegenerateEverythingNow prefix patch
		public void UpdateThingVisibility() {
			foreach (Thing thing in map.listerThings.AllThings) {
				if (thing.Spawned) {
					CompMainComponent compMain = (CompMainComponent) thing.TryGetComp(CompMainComponent.COMP_DEF);
					compMain?.compHideFromPlayer?.updateVisibility(true);
				}
			}
		}

		public override void ExposeData() {
			base.ExposeData();

			if (Scribe.mode == LoadSaveMode.Saving)
			{
				var savingDict = knownCells.ToDictionary(kv => kv.Key, kv => new BoolArrayExposer()
				{
					size = mapCellLength,
					array = kv.Value
				});
				
				Scribe_Collections.Look(ref savingDict, "revealedCellsPerFaction");
			}
			else if (Scribe.mode == LoadSaveMode.LoadingVars)
			{
				var dict = new Dictionary<int, BoolArrayExposer>();
				Scribe_Collections.Look(ref dict, "revealedCellsPerFaction");

				knownCells = (dict ?? new Dictionary<int, BoolArrayExposer>()).
					ToDictionary(kv => kv.Key, kv => kv.Value.array);
			}
		}

		public void revealCell(Faction faction, int idx) {
			if (!isKnown(faction, idx)) {
				ref IntVec3 cell = ref idxToCellCache[idx];

				isKnown(faction, idx) = true;
				
				Designation designation = mineDesignationGrid[idx];
				if (designation != null && cell.GetFirstMineable(map) == null) {
					designation.Delete();
				}

				if (initialized) {
					setMapMeshDirtyFlag(idx);

					// Refresh overlays
					map.fertilityGrid.Drawer.SetDirty();
					map.roofGrid.Drawer.SetDirty();
					map.terrainGrid.Drawer.SetDirty();
				}

				if (compHideFromPlayerGridCount[idx] != 0) {
					List<CompHideFromPlayer> comps = compHideFromPlayerGrid[idx];
					int compCount = comps.Count;
					for (int i = 0; i < compCount; i++) {
						comps[i].updateVisibility(true);
					}
				}
			}
		}
		
		public void unrevealCell(Faction faction, int idx) {
			if (isKnown(faction, idx) && !isShown(faction, idx)) {
				isKnown(faction, idx) = false;
				
				if (initialized) {
					setMapMeshDirtyFlag(idx);

					// Refresh overlays
					map.fertilityGrid.Drawer.SetDirty();
					map.roofGrid.Drawer.SetDirty();
					map.terrainGrid.Drawer.SetDirty();
				}

				if (compHideFromPlayerGridCount[idx] != 0) {
					List<CompHideFromPlayer> comps = compHideFromPlayerGrid[idx];
					int compCount = comps.Count;
					for (int i = 0; i < compCount; i++) {
						comps[i].updateVisibility(true);
					}
				}
			}
		}

		public void incrementSeen(Faction faction, short[] factionShownCells, int idx) {
			if ((++factionShownCells[idx] == 1) && faction.def.isPlayer) {
				ref IntVec3 cell = ref idxToCellCache[idx];

				if (!isKnown(faction, idx)) {
					isKnown(faction, idx) = true;

					// Refresh overlays
					if (initialized) {
						map.fertilityGrid.Drawer.SetDirty();
						map.roofGrid.Drawer.SetDirty();
						map.terrainGrid.Drawer.SetDirty();
					}

					Designation designation = mineDesignationGrid[idx];
					if (designation != null && cell.GetFirstMineable(map) == null) {
						designation.Delete();
					}
				}

				if (initialized) {
					setMapMeshDirtyFlag(idx);
				}

				if (compHideFromPlayerGridCount[idx] != 0) {
					List<CompHideFromPlayer> comps = compHideFromPlayerGrid[idx];
					int compCount = comps.Count;
					for (int i = 0; i < compCount; i++) {
						comps[i].updateVisibility(true);
					}
				}
			}
		}

		public void decrementSeen(Faction faction, short[] factionShownCells, int idx) {
			if ((--factionShownCells[idx] == 0) && faction.def.isPlayer) {
				playerVisibilityChangeTick[idx] = currentGameTick;

				if (initialized) {
					setMapMeshDirtyFlag(idx);
				}

				if (compHideFromPlayerGridCount[idx] != 0) {
					List<CompHideFromPlayer> comps = compHideFromPlayerGrid[idx];
					int compCount = comps.Count;
					for (int i = 0; i < compCount; i++) {
						comps[i].updateVisibility(true);
					}
				}
			}
		}

		private void setMapMeshDirtyFlag(int idx) {
			int x = idx % mapSizeX;
			int z = idx / mapSizeX;

			int sectionX = x / 17;
			int sectionY = z / 17;


			// Update visibility change tick for this cell and proximal ones.
			int minProxX = Math.Max(0, x - 1);
			int maxProxZ = Math.Min(z + 2, mapSizeZ);
			int horizontalCellsCount = Math.Min(x + 2, mapSizeX) - minProxX;

			int startHorizontalIdx;
			for (int proxZ = Math.Max(0, z - 1); proxZ < maxProxZ; proxZ++) {
				startHorizontalIdx = (proxZ * mapSizeX) + minProxX;
				for (int horizontalPos = 0; horizontalPos < horizontalCellsCount; horizontalPos++) {
					playerVisibilityChangeTick[startHorizontalIdx + horizontalPos] = currentGameTick;
				}
			}

			
			// Update current section.
			sections[sectionY * sectionsSizeX + sectionX].dirtyFlags |= SectionLayer_FoVLayer.mapMeshFlag;

			
			// Update neighbours sections if needed.
			int relSecX = x % 17;
			int relSecZ = z % 17;
			if (relSecX == 0) {
				if (sectionX != 0) {
					sections[sectionY * sectionsSizeX + sectionX].dirtyFlags |= SectionLayer_FoVLayer.mapMeshFlag;
					if (relSecZ == 0) {
						if (sectionY != 0) {
							sections[(sectionY - 1) * sectionsSizeX + (sectionX - 1)].dirtyFlags |= SectionLayer_FoVLayer.mapMeshFlag;
						}
					} else if (relSecZ == 16) {
						if (sectionY < sectionsSizeY) {
							sections[(sectionY + 1) * sectionsSizeX + (sectionX - 1)].dirtyFlags |= SectionLayer_FoVLayer.mapMeshFlag;
						}
					}
				}
			} else if (relSecX == 16) {
				if (sectionX < sectionsSizeX) {
					sections[sectionY * sectionsSizeX + (sectionX + 1)].dirtyFlags |= SectionLayer_FoVLayer.mapMeshFlag;
					if (relSecZ == 0) {
						if (sectionY != 0) {
							sections[(sectionY - 1) * sectionsSizeX + (sectionX + 1)].dirtyFlags |= SectionLayer_FoVLayer.mapMeshFlag;
						}
					} else if (relSecZ == 16) {
						if (sectionY < sectionsSizeY) {
							sections[(sectionY + 1) * sectionsSizeX + (sectionX + 1)].dirtyFlags |= SectionLayer_FoVLayer.mapMeshFlag;
						}
					}
				}
			}

			if (relSecZ == 0) {
				if (sectionY != 0) {
					sections[(sectionY - 1) * sectionsSizeX + sectionX].dirtyFlags |= SectionLayer_FoVLayer.mapMeshFlag;
				}
			} else if (relSecZ == 16) {
				if (sectionY < sectionsSizeY) {
					sections[(sectionY + 1) * sectionsSizeX + sectionX].dirtyFlags |= SectionLayer_FoVLayer.mapMeshFlag;
				}
			}
		}
	}
}
