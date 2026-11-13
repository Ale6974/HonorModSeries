using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using HarmonyLib;
using Honor.Contracts.Rules;
using Verse;
using Verse.AI;
namespace Honor.Contracts.Preview;
internal static class GroundRestGivers
{
    private sealed class References : IEqualityComparer<ThinkNode>
    { public bool Equals(ThinkNode x, ThinkNode y) => ReferenceEquals(x,y); public int GetHashCode(ThinkNode x) => RuntimeHelpers.GetHashCode(x); }
    private static void Known(Type type, string name)
    { var m = AccessTools.Method(type,name); if (m == null || m.DeclaringType != type || Harmony.GetPatchInfo(m)?.Owners.Any() == true) throw new ArgumentException("Unreviewed native think-tree save boundary."); }
    internal static GroundRestGiver Capture(Job job)
    {
        if (!NativeTransferBodies.WorkObservationBodies || job?.GetType() != typeof(Job)) throw new ArgumentException("Reviewed original native job required.");
        var cache = AccessTools.Field(typeof(Job),"jobGiverKey");
        if (cache?.DeclaringType != typeof(Job) || cache.FieldType != typeof(int)) throw new ArgumentException("Unknown native giver cache schema.");
        int before = (int)cache.GetValue(job);
        if (job.jobGiver == null) { if (job.jobGiverThinkTree != null || before != -1) throw new ArgumentException("Incomplete native giver provenance."); return null; }
        var tree = job.jobGiverThinkTree;
        if (tree?.GetType() != typeof(ThinkTreeDef) || tree.thinkRoot == null || DefDatabase<ThinkTreeDef>.GetNamedSilentFail(tree.defName) != tree) throw new ArgumentException("Original registered think-tree definition required.");
        Known(typeof(ThinkTreeDef),"TryGetThinkNodeWithSaveKey"); Known(typeof(ThinkNode),"get_UniqueSaveKey"); Known(typeof(ThinkNode),"get_ChildrenRecursive");
        var keyField = AccessTools.Field(typeof(ThinkNode),"uniqueSaveKeyInt");
        if (keyField?.DeclaringType != typeof(ThinkNode) || keyField.FieldType != typeof(int)) throw new ArgumentException("Unknown native node-key schema.");
        var seen = new HashSet<ThinkNode>(new References()); var keys = new HashSet<int>();
        var stack = new Stack<(ThinkNode Node,int Depth,int Parent)>(); stack.Push((tree.thinkRoot,0,-1));
        var topology = new List<object>(); int foundKey = -1; string kind = null;
        while (stack.Count != 0)
        {
            var (node,depth,parent) = stack.Pop();
            if (node == null || depth > 64 || seen.Count >= 4096 || !seen.Add(node) || node.GetType().Assembly != typeof(ThinkNode).Assembly || node.subNodes?.GetType() != typeof(List<ThinkNode>) || node.subNodes.Count > 4096) throw new ArgumentException("Unknown/cyclic/oversized original native think tree.");
            int key = (int)keyField.GetValue(node); if (key == -1 || !keys.Add(key)) throw new ArgumentException("Native node save key missing/ambiguous.");
            int index = topology.Count; topology.Add(new { Parent = parent, Key = key, Type = node.GetType().FullName, Count = node.subNodes.Count });
            if (ReferenceEquals(node,job.jobGiver)) { foundKey = key; kind = node.GetType().FullName; }
            for (int i=node.subNodes.Count-1;i>=0;i--) stack.Push((node.subNodes[i],depth+1,index));
        }
        if (kind == null || before != -1 && before != foundKey) throw new ArgumentException("Original giver is absent or has a stale native saved key.");
        // Native Job.ExposeData derives this cache from the same registered node on every save. We never assign it.
        var result = new GroundRestGiver(tree.defName,foundKey,kind,RuntimeCompat.Hex(RuntimeCompat.Sha256(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(topology)))));
        GroundRest.RequireGiver(result); return result;
    }
}
