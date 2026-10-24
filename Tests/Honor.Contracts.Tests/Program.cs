using Honor.Contracts.Rules;

int assertions = 0;
void Check(bool condition, string name)
{
    assertions++;
    if (!condition) throw new Exception(name);
}
void Reject(Action action, string name)
{
    bool rejected = false;
    try { action(); } catch (ArgumentException) { rejected = true; }
    Check(rejected, name);
}
PartyRef F(string id) => new(PartyKind.Faction, id);
ObligationClause Protection() => new("protect", ObligationKind.Protection, ContractSide.Liege, 0, 5,
    maximumRequests: 2, responseTicks: 10);
ObligationClause Tribute() => new("tribute", ObligationKind.Tribute, ContractSide.Subject, 25, 5, "Silver", 100);
ContractTerms Terms(ContractKind kind = ContractKind.FactionVassalage, string liege = "A", string subject = "B", string settlement = "town1")
{
    PartyKind scope = kind == ContractKind.PersonalVassalage ? PartyKind.Pawn : PartyKind.Faction;
    return new(kind, new(scope, liege), new(scope, subject), 100, new[] { Tribute(), Protection() },
        kind == ContractKind.SettlementCharter ? settlement : null,
        kind == ContractKind.SettlementCharter ? new(true, false, true) : null);
}
ContractLedger Active(ContractTerms terms = null, long start = 0)
{
    var contract = new ContractLedger("test", terms ?? Terms());
    contract.Sign(ContractSide.Liege, start);
    contract.Sign(ContractSide.Subject, start);
    return contract;
}

foreach (var kind in Enum.GetValues<ContractKind>())
{
    var contract = new ContractLedger("consent", Terms(kind));
    Check(!contract.RequestVerifiedDuty("protect", "raid", 0), "unsigned contract grants no protection duty");
    Check(contract.Sign(ContractSide.Liege, 1), "first signature");
    Check(contract.Phase == ContractPhase.Proposed && contract.Obligations.Count == 0, "one party cannot force acceptance");
    Check(!contract.Sign(ContractSide.Liege, 10), "duplicate signature");
    Check(contract.Sign(ContractSide.Subject, 20), "second signature");
    Check(contract.StartedTick == 20 && contract.EndTick == 120 && contract.Obligations.Count == 4, "duration begins on mutual acceptance");
    Check(!contract.Sign(ContractSide.Subject, 25) && contract.EndTick == 120, "resigning cannot extend duration");
    Check(!contract.RecordVerifiedFulfillment("period/tribute/1", "early", 44), "no advance tribute farming");
    Check(contract.RecordVerifiedFulfillment("period/tribute/1", "delivery1", 45), "verified delivery at due tick");
    Check(!contract.RecordVerifiedFulfillment("period/tribute/1", "delivery2", 46), "one duty cannot be paid twice");
    Check(!contract.RecordVerifiedFulfillment("period/tribute/2", "delivery1", 70), "receipt cannot pay a second duty");
    Check(contract.RecordVerifiedFulfillment("period/tribute/2", "delivery2", 75), "payment exactly on grace boundary");
    Check(contract.RecordVerifiedFulfillment("period/tribute/3", "delivery3", 95), "third delivery");
    Check(contract.RecordVerifiedFulfillment("period/tribute/4", "delivery4", 125), "final delivery during trailing grace");
    Check(contract.Phase == ContractPhase.Expired && contract.BreachingSide == null, "peaceful expiry carries no culprit");
    Check(!contract.Repudiate(ContractSide.Subject, 126), "expired contract cannot be broken retroactively");
}

// A sparse game tick or reload must not erase missed periods or grant a free fresh deadline.
var overdue = Active();
overdue.Advance(1000);
Check(overdue.Obligations.Count(o => o.Phase == ObligationPhase.Overdue) == 4, "all missed deliveries remain recorded");
Check(overdue.Phase == ContractPhase.Disputed && overdue.BreachingSide == null, "lateness alone does not prove dishonor");
Check(!overdue.RecordVerifiedFulfillment("period/tribute/1", "late", 1000), "late receipt cannot silently cure dispute");
Check(overdue.ResolveMissedDuty("period/tribute/1", false, "disaster", 1000), "excuse first disaster");
Check(overdue.Phase == ContractPhase.Disputed, "other missed duties still need resolution");
Check(!overdue.ResolveMissedDuty("period/tribute/2", false, "disaster", 1000), "finding cannot be reused for another duty");
Check(overdue.ResolveMissedDuty("period/tribute/2", true, "refusal", 1000), "verified deliberate refusal");
Check(overdue.Phase == ContractPhase.Breached && overdue.BreachingSide == ContractSide.Subject, "subject breach attributed");
Check(!overdue.ResolveMissedDuty("period/tribute/2", true, "again", 1000), "breach cannot resolve twice");
Check(overdue.Obligations.All(o => o.Phase is ObligationPhase.Discharged or ObligationPhase.Excused), "termination closes remaining duties");

var protectedSubject = Active();
Check(protectedSubject.RequestVerifiedDuty("protect", "raid-1", 0), "verified attack creates protection duty");
Check(!protectedSubject.RequestVerifiedDuty("protect", "raid-2", 0), "one concurrent protection duty per clause");
Check(protectedSubject.RecordVerifiedFulfillment("request/protect/1", "quest-1", 1), "assistance can complete before response deadline");
Check(!protectedSubject.RequestVerifiedDuty("protect", "raid-1", 1), "same trigger cannot create a second reward");
Check(protectedSubject.RequestVerifiedDuty("protect", "raid-2", 2), "second genuine trigger within quota");
protectedSubject.Advance(18);
Check(protectedSubject.ResolveMissedDuty("request/protect/2", true, "abandonment", 18), "liege protection refusal can breach");
Check(protectedSubject.BreachingSide == ContractSide.Liege, "liege is accountable too");

var quota = Active();
for (int i = 1; i <= 2; i++)
{
    Check(quota.RequestVerifiedDuty("protect", $"raid-{i}", i), "finite genuine protection trigger");
    Check(quota.RecordVerifiedFulfillment($"request/protect/{i}", $"quest-{i}", i), "finite genuine completion");
}
Check(!quota.RequestVerifiedDuty("protect", "raid-3", 3), "agreed quota cannot be exceeded");

foreach (var side in Enum.GetValues<ContractSide>())
{
    var broken = Active();
    Check(broken.Repudiate(side, 1) && broken.BreachingSide == side, "either party can deliberately repudiate");
    Check(!broken.Repudiate(side, 2), "repudiation idempotence");
}
var released = Active();
Check(released.ConsentToRelease(ContractSide.Liege, 1) && released.Phase == ContractPhase.Active, "release needs both parties");
Check(!released.ConsentToRelease(ContractSide.Liege, 2), "one side cannot sign twice");
Check(released.ConsentToRelease(ContractSide.Subject, 3) && released.Phase == ContractPhase.Released, "mutual release");
Check(released.BreachingSide == null && released.Obligations.All(o => o.Phase == ObligationPhase.Discharged), "release settles outstanding duties by consent");

var serviceTerms = new ContractTerms(ContractKind.PersonalVassalage, new(PartyKind.Pawn, "lord"), new(PartyKind.Pawn, "vassal"),
    100, new[] { Protection(), new ObligationClause("service", ObligationKind.Service, ContractSide.Subject,
        0, 5, maximumRequests: 1, responseTicks: 10) });
var service = Active(serviceTerms);
Check(service.RequestVerifiedDuty("service", "mission", 99), "last-day request stays bounded by agreed response window");
service.Advance(100);
Check(service.Phase == ContractPhase.Active, "term end does not erase pending service");
Check(!service.RequestVerifiedDuty("protect", "post-expiry-raid", 100), "no new duties after term");
Check(service.RecordVerifiedFulfillment("request/service/1", "mission-complete", 109), "trailing response can complete");
Check(service.Phase == ContractPhase.Expired, "expires after final response");

// Boundary sweep: exact grace tick is permitted; the next tick needs adjudication.
for (int start = 0; start < 100; start++)
{
    var timely = Active(start: start);
    Check(timely.RecordVerifiedFulfillment("period/tribute/1", "paid", start + 30), "inclusive grace boundary");
    var late = Active(start: start);
    Check(!late.RecordVerifiedFulfillment("period/tribute/1", "late", start + 31), "one tick past grace");
    Check(late.Phase == ContractPhase.Disputed && late.BreachingSide == null, "boundary has no automatic guilt");
}

var ab = Active(Terms(liege: "A", subject: "B"));
var bc = Active(Terms(liege: "B", subject: "C"));
Check(BindingRules.Check(Terms(liege: "C", subject: "A"), new[] { ab, bc }) == BindingConflict.Cycle, "indirect vassal cycle");
Check(BindingRules.Check(Terms(liege: "C", subject: "B"), new[] { ab }) == BindingConflict.SubjectAlreadyBound, "no competing lieges");
Check(BindingRules.Check(Terms(liege: "B", subject: "C"), new[] { ab }) == BindingConflict.None, "acyclic subordinate vassalage");
Check(BindingRules.Check(Terms(ContractKind.PersonalVassalage, "C", "B"), new[] { ab }) == BindingConflict.None, "pawn and faction scopes independent");
var town = Active(Terms(ContractKind.SettlementCharter));
Check(BindingRules.Check(Terms(ContractKind.SettlementCharter, "C", "B"), new[] { town }) == BindingConflict.SettlementAlreadyBound, "one charter per settlement");
Check(BindingRules.Check(Terms(ContractKind.SettlementCharter, "C", "B", "town2"), new[] { town }) == BindingConflict.None, "charter does not bind whole faction");
ab.ConsentToRelease(ContractSide.Liege, 0); ab.ConsentToRelease(ContractSide.Subject, 0);
Check(BindingRules.Check(Terms(liege: "C", subject: "B"), new[] { ab }) == BindingConflict.None, "released binding no longer blocks negotiation");

Reject(() => new PartyRef((PartyKind)999, "bad"), "unknown scope");
Reject(() => new PartyRef(PartyKind.Pawn, " "), "empty identity");
Reject(() => Terms(liege: "same", subject: "same"), "self vassalage");
Reject(() => new ContractTerms(ContractKind.FactionVassalage, F("A"), new(PartyKind.Pawn, "B"), 100,
    new[] { Protection() }), "mixed parties");
Reject(() => new ContractTerms(ContractKind.SettlementCharter, F("A"), F("B"), 100,
    new[] { Protection() }), "charter requires a specific settlement and rights");
Reject(() => new ContractTerms(ContractKind.FactionVassalage, F("A"), F("B"), 100,
    new[] { Tribute() }), "no protection-free extraction contract");
Reject(() => new ContractTerms(ContractKind.FactionVassalage, F("A"), F("B"), 100,
    new[] { Protection(), Protection() }), "duplicate clause IDs");
Reject(() => new ContractTerms(ContractKind.FactionVassalage, F("A"), F("B"), 10000,
    new[] { Tribute(), Protection() }), "bounded periodic backlog");
Reject(() => new ObligationClause("tax", ObligationKind.Tribute, ContractSide.Subject, 0, 0, "Silver", 100), "invalid period");
Reject(() => new ObligationClause("tax", ObligationKind.Tribute, ContractSide.Subject, 1, 0, "Silver", -10), "negative tribute");
Reject(() => new ObligationClause("protection", ObligationKind.Protection, ContractSide.Subject, 0, 0,
    maximumRequests: 1, responseTicks: 10), "protection is owed by liege");
Reject(() => quota.Advance(0), "time cannot move backward");
Reject(() => Active().Sign((ContractSide)999, 0), "unknown side cannot sign");
var source = new List<ObligationClause> { Tribute(), Protection() };
var snapshot = new ContractTerms(ContractKind.FactionVassalage, F("A"), F("B"), 100, source);
source.Clear();
Check(snapshot.Clauses.Count == 2, "proposal freezes source clause list");
var overflow = new ContractLedger("overflow", Terms());
bool overflowRejected = false;
try { overflow.Sign(ContractSide.Liege, long.MaxValue - 10); } catch (OverflowException) { overflowRejected = true; }
Check(overflowRejected && overflow.Phase == ContractPhase.Proposed && overflow.Obligations.Count == 0, "overflow cannot partially activate");

assertions += MilitaryTests.Run();
assertions += CityTests.Run();
assertions += StabilityTests.Run();
assertions += RescuePledgeTests.Run();
assertions += PoliticalWorldTests.Run();
assertions += CharterBookTests.Run();
assertions += TributeLedgerTests.Run();
assertions += UnionAdmissionsTests.Run();
assertions += PlayerAllegianceTests.Run();
assertions += TerritoryTransferTests.Run();
assertions += FollowerTransfersTests.Run();
assertions += AllegianceTransitionsTests.Run();
Console.WriteLine($"PASS: {assertions} contract assertions (three scopes, consent and conquest origins, deadlines, reciprocal duties, evidence reuse guards, quotas, binding cycles, validation).");
