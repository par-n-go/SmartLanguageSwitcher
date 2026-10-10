namespace SmartLanguageSwitcher;
internal sealed class LayoutHistory
{
    public nint Current { get; private set; }
    public nint Previous { get; private set; }
    private List<nint> cycle = [];
    private int index;
    public bool Active { get; private set; }
    public LayoutHistory(nint current, nint last, nint previous)
    {
        Current = current;
        Previous = last != 0 && last != current ? last : previous != current ? previous : 0;
    }
    public void Observe(nint actual)
    {
        if (actual == 0 || actual == Current) return;
        Previous = Current; Current = actual;
    }
    public nint Step(nint actual, IReadOnlyList<nint> installed)
    {
        if (!Active)
        {
            Observe(actual);
            var prior = installed.Contains(Previous) && Previous != actual ? Previous : installed.FirstOrDefault(x => x != actual);
            if (actual == 0 || prior == 0) return 0;
            cycle = [prior, .. installed.Where(x => x != actual && x != prior).Distinct(), actual];
            index = 0; Active = true;
        }
        else index = (index + 1) % cycle.Count;
        return cycle[index];
    }
    public void Finish(nint confirmed) { Active = false; Observe(confirmed); }
}
