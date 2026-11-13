using Honor.Contracts.Rules;
using RimWorld;
using Verse;
namespace Honor.Contracts.Preview;

/// <summary>Read native layers only. Never call SetTerrain, SetFoundation, SetTempTerrain or a color setter.</summary>
internal static class TerrainWorkEvidence
{
    private static int Target(TerrainDef def)=>def.isFoundation?1:def.temporary?2:0;
    private static string Terrain(TerrainDef def)
    {
        if(def==null)return null;
        if(def.GetType()!=typeof(TerrainDef)||DefDatabase<TerrainDef>.GetNamedSilentFail(def.defName)!=def)throw new ArgumentException("Original registered native terrain definition required.");
        return def.defName;
    }
    private static (WorkTerrainLayers Layers,string Under,string Roof) Read(Map map,int cell,int targetLayer)
    {
        if(!NativeTransferBodies.WorkObservationBodies||map==null||map.terrainGrid?.GetType()!=typeof(TerrainGrid)||map.roofGrid?.GetType()!=typeof(RoofGrid)||cell<0||cell>=map.cellIndices.NumGridCells)throw new ArgumentException("Original audited terrain map required.");
        if(ResidentActiveWork.Raw(map.terrainGrid,typeof(TerrainGrid),"map")!=map||ResidentActiveWork.Raw(map.roofGrid,typeof(RoofGrid),"map")!=map)throw new ArgumentException("Original native terrain/roof owners changed.");
        if(ResidentActiveWork.Raw(map.terrainGrid,typeof(TerrainGrid),"topGrid") is not TerrainDef[] top||ResidentActiveWork.Raw(map.terrainGrid,typeof(TerrainGrid),"foundationGrid") is not TerrainDef[] foundation||
            ResidentActiveWork.Raw(map.terrainGrid,typeof(TerrainGrid),"tempGrid") is not TerrainDef[] temporary||ResidentActiveWork.Raw(map.terrainGrid,typeof(TerrainGrid),"colorGrid") is not ColorDef[] colors||
            top.Length!=map.cellIndices.NumGridCells||foundation.Length!=top.Length||temporary.Length!=top.Length||colors.Length!=top.Length)throw new ArgumentException("Exact original native terrain arrays required.");
        ResidentActiveWork.Known(typeof(TerrainGrid),"TerrainAt");ResidentActiveWork.Known(typeof(TerrainGrid),"UnderTerrainAt");ResidentActiveWork.Known(typeof(RoofGrid),"RoofAt");
        var position=map.cellIndices.IndexToCell(cell);var roof=map.roofGrid.RoofAt(position);var color=colors[cell];
        if(roof!=null&&(roof.GetType()!=typeof(RoofDef)||DefDatabase<RoofDef>.GetNamedSilentFail(roof.defName)!=roof)||color!=null&&(color.GetType()!=typeof(ColorDef)||DefDatabase<ColorDef>.GetNamedSilentFail(color.defName)!=color))throw new ArgumentException("Registered native roof/color definitions required.");
        var layers=new WorkTerrainLayers(targetLayer,Terrain(map.terrainGrid.TerrainAt(position)),Terrain(top[cell]),Terrain(foundation[cell]),Terrain(temporary[cell]),color?.defName);
        WorkTerrainLayerRules.Check(layers);return(layers,Terrain(map.terrainGrid.UnderTerrainAt(position)),roof?.defName);
    }
    internal static WorkTerrainLayers Original(Frame frame)
    {
        if(frame.def.entityDefToBuild is not TerrainDef def)return null;
        _=Terrain(def);var read=Read(frame.Map,frame.Map.cellIndices.CellToIndex(frame.Position),Target(def));
        return def.isFoundation||def.temporary||def.bridge||read.Layers.Foundation!=null||read.Layers.Temporary!=null||read.Layers.Color!=null?read.Layers:null;
    }
    internal static WorkTerrainAcceptance Capture(Map map,int cell,string expected,WorkTerrainLayers original=null)
    {
        if(!NativeTransferBodies.WorkObservationBodies)throw new ArgumentException("Audited native terrain required.");
        var def=DefDatabase<TerrainDef>.GetNamedSilentFail(expected);if(def==null||Terrain(def)!=expected)throw new ArgumentException("Registered original target terrain required.");
        var read=Read(map,cell,Target(def));
        if(original==null)
        {
            // Old .112 mode deliberately remains plain, unpainted and unlayered.
            if(def.isFoundation||def.temporary||def.bridge||read.Layers.Foundation!=null||read.Layers.Temporary!=null||read.Layers.Color!=null||read.Layers.Visible!=expected)throw new ArgumentException("Old terrain scope grants no layered preservation authority.");
            return new(cell,expected,read.Under,read.Roof);
        }
        WorkTerrainLayerRules.Pair(original,read.Layers,expected);
        return new(cell,expected,read.Under,read.Roof){LayersV9=read.Layers};
    }
    internal static string Review(Map map,WorkTerrainAcceptance actual,WorkTerrainLayers original)
    {
        string Label<T>(string name) where T:Def=>name==null?"RE_WorkTerrainNone".Translate().ToString():DefDatabase<T>.GetNamedSilentFail(name)?.label??name;
        var cell=map.cellIndices.IndexToCell(actual.Cell);string place=cell.x+", "+cell.z;
        string Row(string phase,WorkTerrainLayers layers)=>"RE_WorkTerrainLayersReview".Translate(phase,place,
            (layers.TargetLayer==1?"RE_WorkTerrainLayerFoundation":layers.TargetLayer==2?"RE_WorkTerrainLayerTemporary":"RE_WorkTerrainLayerTop").Translate().ToString(),
            Label<TerrainDef>(actual.Def),Label<TerrainDef>(layers.Top),Label<TerrainDef>(layers.Foundation),Label<TerrainDef>(layers.Temporary),Label<ColorDef>(layers.Color),Label<TerrainDef>(layers.Visible));
        return Row("RE_WorkTerrainBefore".Translate(),original)+"\n"+Row("RE_WorkTerrainAfter".Translate(),actual.LayersV9);
    }
}
