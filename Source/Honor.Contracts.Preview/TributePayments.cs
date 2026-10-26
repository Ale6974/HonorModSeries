using Honor.Contracts.Rules;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace Honor.Contracts.Preview;

public sealed class TributePiece : IExposable
{
    public string SourceId;
    public Pawn OriginalCarrier;
    public Thing Held;
    public int Count;
    public bool Returned;
    public void ExposeData()
    {
        Scribe_Values.Look(ref SourceId, "sourceId"); Scribe_References.Look(ref OriginalCarrier, "carrier");
        Scribe_References.Look(ref Held, "held"); Scribe_Values.Look(ref Count, "count"); Scribe_Values.Look(ref Returned, "returned");
    }
}

public sealed class TributeTransfer : IExposable, IThingHolder
{
    public string ReceiptId;
    public Caravan Caravan;
    public Settlement Receiver;
    public ThingOwner<Thing> Escrow;
    public List<TributePiece> Pieces = [];
    public bool Uncertain;
    public IThingHolder ParentHolder => null;
    public TributeTransfer() => Escrow = new ThingOwner<Thing>(this);
    public ThingOwner GetDirectlyHeldThings() => Escrow;
    public void GetChildHolders(List<IThingHolder> outChildren) => ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, Escrow);
    public void ExposeData()
    {
        Scribe_Values.Look(ref ReceiptId, "receipt"); Scribe_References.Look(ref Caravan, "caravan"); Scribe_References.Look(ref Receiver, "receiver");
        Scribe_Values.Look(ref Uncertain, "uncertain"); Scribe_Deep.Look(ref Escrow, "escrow", this);
        Scribe_Collections.Look(ref Pieces, "pieces", LookMode.Deep);
        if (Scribe.mode == LoadSaveMode.PostLoadInit) { Escrow ??= new(this); Pieces ??= []; }
    }
}

public sealed record QuotedTributeItem(Thing Thing, Pawn Carrier, decimal Price);

/// <summary>Manual direct delivery from a player's caravan, with saved escrow and receipt state.</summary>
public sealed class TributePaymentLedger : GameComponent
{
    private string snapshot;
    private List<TributeTransfer> transfers = [];
    public TributeLedger Ledger { get; private set; } = new();
    internal void CommitCharterRelease(TributeLedger prepared) => Ledger = prepared;
    public bool RestoreFailed { get; private set; }
    public TributePaymentLedger(Game game) { }
    private LiveCharterLedger Charters => Current.Game.GetComponent<LiveCharterLedger>();
    public override void FinalizeInit()
    {
        try
        {
            if (Charters.RestoreFailed || Ledger.LastTick > Find.TickManager.TicksGame || Ledger.Accounts.Any(a => {
                var charter = Charters.Book.Charter(a.CharterId);
                return charter.Terms != a.Terms || !Charters.Book.Entity(a.Terms.Subject).Player ||
                    charter.Phase is not (RecordedCharterPhase.Active or RecordedCharterPhase.Released) ||
                    !charter.PlayerSignedTick.HasValue || !charter.NpcSignedTick.HasValue ||
                    Math.Max(charter.PlayerSignedTick.Value, charter.NpcSignedTick.Value) > a.StartedTick ||
                    a.ClosedTick.HasValue && (charter.Phase != RecordedCharterPhase.Released ||
                        a.ClosedTick != Math.Max(charter.PlayerReleaseTick.Value, charter.NpcReleaseTick.Value));
            })) throw new ArgumentException("Payment account does not match charter.");
        }
        catch (ArgumentException) { RestoreFailed = true; Log.Error("[Honor.Contracts.Preview] Payment/charter mismatch; data retained."); }
    }
    public override void ExposeData()
    {
        if (Scribe.mode == LoadSaveMode.Saving && !RestoreFailed) snapshot = Ledger.Export();
        Scribe_Values.Look(ref snapshot, "honorTributePaymentsV1");
        Scribe_Collections.Look(ref transfers, "honorTributeTransfers", LookMode.Deep);
        if (Scribe.mode != LoadSaveMode.PostLoadInit) return;
        transfers ??= [];
        try
        {
            if (snapshot != null) Ledger = TributeLedger.Restore(snapshot);
            if (transfers.Any(t => t == null || t.Pieces.Any(p => p == null)) ||
                transfers.Select(t => t.ReceiptId).Distinct(StringComparer.Ordinal).Count() != transfers.Count ||
                transfers.Any(t => Ledger.Receipts.All(r => r.Id != t.ReceiptId)) ||
                Ledger.Receipts.Any(r => r.Phase == TributeReceiptPhase.Prepared && transfers.All(t => t.ReceiptId != r.Id)))
                throw new ArgumentException("Invalid saved transfer registry.");
        }
        catch (Exception e) when (e is ArgumentException or System.Text.Json.JsonException or InvalidOperationException or OverflowException)
        { RestoreFailed = true; Log.Error("[Honor.Contracts.Preview] Payment restore rejected; data and escrow retained. " + e.GetType().Name); }
    }
    public bool HasPending(string charterId) => RestoreFailed || Ledger.Receipts.Any(r => r.CharterId == charterId && r.Phase == TributeReceiptPhase.Prepared);
    public bool Enable(string charterId)
    {
        try
        {
            RequireReady(charterId); Ledger.Enable(Charters.Book, charterId, Find.TickManager.TicksGame); return true;
        }
        catch (ArgumentException) { Reject(); return false; }
    }
    private void RequireReady(string charterId)
    {
        if (Current.Game.GetComponent<PoliticalTransitionLedger>().BlocksChanges) throw new ArgumentException("Complete or resolve political transition first.");
        if (RestoreFailed || Charters.RestoreFailed) throw new ArgumentException("Ledger unavailable.");
        Charters.Book.ValidateCurrent(charterId, Charters.Observe());
        if (!Charters.Book.Entity(Charters.Book.Charter(charterId).Terms.Subject).Player) throw new ArgumentException("Only outgoing player tribute is implemented.");
    }
    public void Synchronize(string charterId)
    {
        if (!RestoreFailed && !Charters.RestoreFailed && Ledger.Accounts.Any(a => a.CharterId == charterId))
        {
            try { Ledger.SynchronizeClosure(Charters.Book, charterId, Find.TickManager.TicksGame); }
            catch (ArgumentException)
            {
                snapshot = Ledger.Export(); // Keep receipts written since the last save; closure validation is atomic.
                RestoreFailed = true; Log.Error("[Honor.Contracts.Preview] Invalid tribute closure; current payment history retained.");
            }
        }
    }
    public static Pawn Negotiator(Caravan caravan) => caravan.PawnsListForReading.Where(p => caravan.IsOwner(p) && p.Faction == Faction.OfPlayer &&
        p.RaceProps.Humanlike && !p.Dead && !p.Downed && !p.InMentalState && p.skills?.GetSkill(SkillDefOf.Social).TotallyDisabled == false)
        .OrderByDescending(p => p.GetStatValue(StatDefOf.TradePriceImprovement)).FirstOrDefault();
    private void ValidateDestination(string charterId, Caravan caravan, Settlement receiver)
    {
        RequireReady(charterId);
        var all = Find.WorldObjects.AllWorldObjects;
        if (caravan == null || receiver == null || !all.Contains(caravan) || !all.Contains(receiver) || caravan.Faction != Faction.OfPlayer ||
            caravan.Tile != receiver.Tile || receiver.Faction != Charters.CounterpartyFaction(charterId) || receiver.Faction.HostileTo(Faction.OfPlayer) ||
            !receiver.CanTradeNow || receiver.TraderKind == null || receiver.TradeCurrency != TradeCurrency.Silver ||
            receiver.TraderKind.TitleRequiredToTrade != null || Negotiator(caravan) == null ||
            Charters.Book.Charter(charterId).Terms.Transport != TributeTransport.SubjectDelivery)
            throw new ArgumentException("Direct tribute requires a capable caravan at a supported counterparty settlement.");
    }
    public QuotedTributeItem[] Quote(string charterId, Caravan caravan, Settlement receiver)
    {
        ValidateDestination(charterId, caravan, receiver); var negotiator = Negotiator(caravan);
        // Do not overwrite the game's global TradeSession. Mirror Tradeable's inputs to the installed pricing API.
        var items = new List<QuotedTributeItem>();
        foreach (var carrier in caravan.PawnsListForReading.Where(p => p.inventory != null && p.Faction == Faction.OfPlayer))
        foreach (var thing in carrier.inventory.innerContainer)
        {
            if (thing.def.category != ThingCategory.Item || thing is MinifiedThing || thing is Corpse || thing is IThingHolder ||
                thing is ThingWithComps withComps && withComps.AllComps.Any(c => c is IThingHolder) || !TradeUtility.PlayerSellableNow(thing, receiver) ||
                thing.def != ThingDefOf.Silver && !receiver.TraderKind.WillTrade(thing.def)) continue;
            float price;
            if (thing.def == ThingDefOf.Silver) price = thing.MarketValue;
            else
            {
                float improvement = negotiator.GetStatValue(StatDefOf.TradePriceImprovement);
                float drugs = thing.def.IsNonMedicalDrug ? negotiator.GetStatValue(StatDefOf.DrugSellPriceImprovement) : 0;
                float produce = ModsConfig.IdeologyActive && (thing.def.IsLeather || thing.def.IsMeat || thing.def.IsWool)
                    ? negotiator.GetStatValue(StatDefOf.AnimalProductsSellImprovement) : 0;
                price = TradeUtility.GetPricePlayerSell(thing, receiver.TraderKind.PriceTypeFor(thing.def, TradeAction.PlayerSells).PriceMultiplier(),
                    1, improvement, receiver.TradePriceImprovementOffsetForPlayer, drugs, produce, TradeCurrency.Silver);
                float buy = TradeUtility.GetPricePlayerBuy(thing, receiver.TraderKind.PriceTypeFor(thing.def, TradeAction.PlayerBuys).PriceMultiplier(),
                    1, improvement, receiver.TradePriceImprovementOffsetForPlayer);
                price = Math.Min(price, buy);
            }
            if (RuntimeCompat.IsFinite(price) && price > 0 && price <= 10000000) items.Add(new(thing, carrier, (decimal)price));
        }
        return items.OrderBy(i => i.Thing.def.defName, StringComparer.Ordinal).ThenBy(i => i.Thing.thingIDNumber).Take(100).ToArray();
    }
    public bool Deliver(string charterId, int period, Caravan caravan, Settlement receiver, IReadOnlyList<(QuotedTributeItem Item, int Count)> selected)
    {
        TributeTransfer transfer = null;
        try
        {
            Synchronize(charterId); var fresh = Quote(charterId, caravan, receiver);
            if (selected == null || selected.Count == 0 || selected.Any(s => s.Count <= 0 || s.Count > s.Item.Thing.stackCount ||
                !fresh.Any(f => f.Thing == s.Item.Thing && f.Carrier == s.Item.Carrier && f.Price == s.Item.Price)))
                throw new ArgumentException("Stale goods or quote; no items transferred.");
            string id = Guid.NewGuid().ToString("N");
            var lines = selected.Select(s => new TributeLine(s.Item.Thing.GetUniqueLoadID(), s.Count, s.Item.Price)).ToArray();
            Ledger.Prepare(id, charterId, period, lines, Find.TickManager.TicksGame);
            transfer = new() { ReceiptId = id, Caravan = caravan, Receiver = receiver }; transfers.Add(transfer);
            foreach (var selection in selected)
            {
                var item = selection.Item;
                var piece = new TributePiece { SourceId = item.Thing.GetUniqueLoadID(), OriginalCarrier = item.Carrier };
                transfer.Pieces.Add(piece);
                piece.Count = item.Carrier.inventory.innerContainer.TryTransferToContainer(item.Thing, transfer.Escrow, selection.Count, out Thing moved, false);
                piece.Held = moved;
                if (piece.Count != selection.Count) throw new InvalidOperationException("Could not stage the full selected stack.");
            }
            // Stock generation is the vanilla settlement mechanism; no tribute stock or silver is invented here.
            _ = receiver.Goods.ToArray(); var stock = receiver.trader.GetDirectlyHeldThings();
            foreach (var piece in transfer.Pieces)
            {
                int moved = transfer.Escrow.TryTransferToContainer(piece.Held, stock, piece.Count, out Thing deposited, false);
                if (moved != piece.Count) throw new InvalidOperationException("Receiver refused a prepared stack.");
                piece.Held = deposited;
            }
            FinalizeVerified(transfer);
            Messages.Message("HT_Delivered".Translate(), MessageTypeDefOf.PositiveEvent); return true;
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException)
        {
            if (transfer != null)
            {
                // A thrown third-party hook can make a mutation's result uncertain. Preserve the reservation
                // instead of fabricating an accounting rollback or allowing another payment of the same bill.
                if (Ledger.Receipt(transfer.ReceiptId).Phase == TributeReceiptPhase.Committed) return true;
                transfer.Uncertain = true;
                Log.Warning("[Honor.Contracts.Preview] Tribute transfer needs recovery: " + e.GetType().Name);
            }
            Reject(); return false;
        }
    }
    private void FinalizeVerified(TributeTransfer transfer)
    {
        var receipt = Ledger.Receipt(transfer.ReceiptId);
        RequireReady(receipt.CharterId);
        if (transfer.Receiver == null || !Find.WorldObjects.AllWorldObjects.Contains(transfer.Receiver) ||
            transfer.Receiver.Faction != Charters.CounterpartyFaction(receipt.CharterId))
            throw new ArgumentException("Receiver no longer represents the counterparty.");
        var stock = transfer.Receiver?.trader?.GetDirectlyHeldThings();
        if (stock == null || transfer.Escrow.Count != 0 || transfer.Pieces.Count != receipt.Lines.Count ||
            transfer.Pieces.Any(p => p.Held == null || p.Held.Destroyed || p.Held.holdingOwner != stock || p.Held.stackCount != p.Count || p.Returned) ||
            transfer.Pieces.Select(p => p.Held).Distinct().Count() != transfer.Pieces.Count)
            throw new ArgumentException("Full delivery cannot be proven.");
        var proof = transfer.Pieces.Select(p => new TributeLine(p.SourceId, p.Count, receipt.Lines.Single(l => l.SourceId == p.SourceId).UnitValue));
        Ledger.Commit(receipt.Id, proof, Find.TickManager.TicksGame); transfer.Uncertain = false;
        transfers.Remove(transfer); // Empty escrow and old stock references need not stay in the save.
    }
    public void Recover(string receiptId)
    {
        if (RestoreFailed) { Reject(); return; }
        var transfer = transfers.SingleOrDefault(t => t.ReceiptId == receiptId);
        if (transfer == null || Ledger.Receipt(receiptId).Phase != TributeReceiptPhase.Prepared) { Reject(); return; }
        try
        {
            RequireReady(Ledger.Receipt(receiptId).CharterId);
            FinalizeVerified(transfer);
            Messages.Message("HT_Delivered".Translate(), MessageTypeDefOf.PositiveEvent); return;
        }
        catch (ArgumentException) { /* Otherwise return only known physical pieces, preserving any uncertain reservation. */ }
        foreach (var piece in transfer.Pieces.Where(p => !p.Returned && p.Held != null && !p.Held.Destroyed))
        {
            var carrier = piece.OriginalCarrier;
            if (carrier == null || carrier.Dead || carrier.Destroyed || carrier.Faction != Faction.OfPlayer)
                carrier = transfer.Caravan?.PawnsListForReading.FirstOrDefault(p => !p.Dead && p.Faction == Faction.OfPlayer && p.inventory != null);
            if (carrier?.inventory == null || piece.Held.stackCount != piece.Count) continue;
            var owner = piece.Held.holdingOwner;
            if (owner == null || owner != transfer.Escrow && owner != transfer.Receiver?.trader?.GetDirectlyHeldThings()) continue;
            if (owner.TryTransferToContainer(piece.Held, carrier.inventory.innerContainer, piece.Count, out Thing returned, false) == piece.Count)
            { piece.Held = returned; piece.Returned = true; }
        }
        var expected = Ledger.Receipt(receiptId).Lines;
        bool knownReturn = transfer.Pieces.Count == expected.Count && expected.All(l => transfer.Pieces.Count(p =>
            p.SourceId == l.SourceId && p.Count == l.Count && p.Returned) == 1) && transfer.Pieces.Select(p => p.Held).Distinct().Count() == expected.Count;
        if (knownReturn && transfer.Escrow.Count == 0)
        { Ledger.CancelAfterVerifiedReturn(receiptId, true, Find.TickManager.TicksGame); transfer.Uncertain = false; transfers.Remove(transfer); }
        Messages.Message("HT_Recovery".Translate(), MessageTypeDefOf.NeutralEvent);
    }
    private static void Reject() => Messages.Message("HT_Unavailable".Translate(), MessageTypeDefOf.RejectInput);
}
