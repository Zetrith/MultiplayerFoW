using Multiplayer.API;
using RimWorld;

namespace RimWorldRealFoW;

// MP: Wrapper for Multiplayer API
public static class MpWrapper
{
    public static Faction RealPlayerFaction => MP.RealPlayerFaction ?? Faction.OfPlayer;
}