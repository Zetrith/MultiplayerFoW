using Verse;

namespace RimWorldRealFoW;

// MP: A helper to make DataExposeUtility.BoolArray work with Scribe_Collections
public class BoolArrayExposer : IExposable
{
    public int size;
    public bool[] array;
    
    public void ExposeData()
    {
        Scribe_Values.Look(ref size, "size");
        DataExposeUtility.BoolArray(ref array, size, "revealedCells");
    }
}