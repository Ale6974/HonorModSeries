namespace Honor.Contracts.Rules;
public sealed record WorkFrameFilth(string Id,string Def,int Cell,string Proof);
/// <summary>Bounded ancillary inputs to a positively observed terrain-frame completion. No cleanup/replay authority.</summary>
public static class WorkFrameFilthRules
{
    private static bool Key(string value,int maximum)=>!string.IsNullOrWhiteSpace(value)&&value.Length<=maximum&&!value.Any(char.IsControl);
    public static void Check(WorkFrameFilth[] rows)
    {
        if(rows==null||rows.Length is <1 or >512||rows.Any(row=>row==null||!Key(row.Id,120)||!Key(row.Def,512)||row.Cell<0||!CityStockProof.Valid(row.Proof))||
            !rows.Select(row=>row.Id).SequenceEqual(rows.Select(row=>row.Id).Distinct().OrderBy(id=>id,StringComparer.Ordinal)))throw new ArgumentException("Complete ordered original filth scope required.");
    }
    public static void Pair(WorkFrameFilth[] original,WorkFrameFilth[] current)
    {
        if(original==null){if(current!=null)throw new ArgumentException("Old frame scope grants no ancillary input authority.");return;}
        Check(original);Check(current);
        if(!original.Select(row=>(row.Id,row.Def,row.Cell)).SequenceEqual(current.Select(row=>(row.Id,row.Def,row.Cell))))throw new ArgumentException("Same original filth identity, definition and construction cell required.");
    }
}
