using RimWorldRealFoW.Utils;
using Verse;

namespace RimWorldRealFoW.Detours;

public static class _OverlayDrawer
{
    // MP: Don't render thing overlays (f.e. forbidden icons)
    public static bool DrawOverlay_Prefix(Thing t)
    {
        return t.fowIsVisible();
    }
}