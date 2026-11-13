namespace Honor.Contracts.Rules;

/// <summary>Native layer names, not permission to construct or restore terrain. TargetLayer: top=0, foundation=1, temporary=2.</summary>
public sealed record WorkTerrainLayers(int TargetLayer,string Visible,string Top,string Foundation,string Temporary,string Color);
public static class WorkTerrainLayerRules
{
    private static bool Key(string value)=>!string.IsNullOrWhiteSpace(value)&&value.Length<=512;
    public static void Check(WorkTerrainLayers layers,string completedDefinition=null)
    {
        if(layers==null||layers.TargetLayer is <0 or >2||!Key(layers.Visible)||
            new[]{layers.Top,layers.Foundation,layers.Temporary,layers.Color}.Any(value=>value!=null&&!Key(value)))throw new ArgumentException("Exact bounded native terrain layers required.");
        string target=layers.TargetLayer==1?layers.Foundation:layers.TargetLayer==2?layers.Temporary:layers.Top;
        if(completedDefinition!=null&&target!=completedDefinition)throw new ArgumentException("The original intended native terrain layer must contain the actual completed definition.");
    }
    public static void Pair(WorkTerrainLayers original,WorkTerrainLayers actual,string definition)
    {
        if(original==null){if(actual!=null)throw new ArgumentException("Old terrain scope grants no layered terrain authority.");return;}
        Check(original);Check(actual,definition);
        if(original.TargetLayer!=actual.TargetLayer)throw new ArgumentException("Original intended terrain layer changed.");
    }
}
