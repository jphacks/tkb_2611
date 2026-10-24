using System.Collections.Generic;
namespace ProjectPenguin.Domain.HealthFactory
{
    public sealed class HealthFactoryResult
    {
        public bool Success {get;} public string Reason {get;} public long Delta {get;}
        public IReadOnlyList<HealthFactoryEvent> Events {get;}
        internal HealthFactoryResult(bool success,string reason,long delta=0,List<HealthFactoryEvent> events=null)
        {Success=success;Reason=reason;Delta=delta;Events=(events??new List<HealthFactoryEvent>()).AsReadOnly();}
    }
    public sealed class HealthFactoryEvent
    {
        public long AtMs {get;} public string Kind {get;} public int Target {get;}
        internal HealthFactoryEvent(long atMs,string kind,int target){AtMs=atMs;Kind=kind;Target=target;}
    }
    public sealed class HealthFactoryQuote
    {
        public bool Reachable {get; internal set;} public string Reason {get; internal set;}
        public long FutureGas {get; internal set;} public long GasShortfall {get; internal set;} public long AdditionalSteps {get; internal set;}
    }
    public sealed class HealthFactoryNodeView
    {
        public int Id {get; internal set;} public int X {get; internal set;} public int Y {get; internal set;}
    }
    public sealed class HealthFactoryDeliveryView
    {
        public int Route {get; internal set;} public int FromX {get; internal set;} public int FromY {get; internal set;}
        public int ToX {get; internal set;} public int ToY {get; internal set;}
        public long DepartAt {get; internal set;} public long ArriveAt {get; internal set;} public int PaidGas {get; internal set;}
    }
    public sealed class HealthFactoryView
    {
        public long Gas {get; internal set;} public long Spent {get; internal set;} public long AtMs {get; internal set;}
        public int Raw {get; internal set;} public int Input {get; internal set;} public int Output {get; internal set;} public int Stored {get; internal set;}
        public bool Processing {get; internal set;} public long ProcessStart {get; internal set;} public long ProcessEnd {get; internal set;}
        public long Water {get; internal set;} public long Electricity {get; internal set;}
        public int BoardSize {get; internal set;} public int InputCapacity {get; internal set;} public int Slots {get; internal set;} public int ProcessingDurationMs {get; internal set;}
        public int MaxSteps {get; internal set;} public int StepDay {get; internal set;} public long GrantedGas {get; internal set;} public int StepMultiplier {get; internal set;}
        public bool PassiveFixture {get; internal set;} public bool AssignedFixture {get; internal set;}
        public bool BufferBuilt {get; internal set;} public bool Upgraded {get; internal set;}
        public string StopReason {get; internal set;}
        public IReadOnlyList<bool> Routes {get; internal set;} public IReadOnlyList<int> RouteCosts {get; internal set;}
        public IReadOnlyList<HealthFactoryNodeView> Nodes {get; internal set;}
        public IReadOnlyList<HealthFactoryDeliveryView> Deliveries {get; internal set;}
    }
}
