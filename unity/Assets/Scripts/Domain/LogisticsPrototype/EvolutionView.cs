using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ProjectPenguin.Domain.LogisticsPrototype
{
    public enum WorkshopKind { Fishery, Saltworks, Warehouse }
    public enum WorkshopGood { Fish, Salt, Chikuwa, Kamaboko, Himono }
    public enum EvolutionEventKind { Placed, Moved, Removed, Formed, Dissolved, Produced, Dispatched, Arrived, Connected, Disconnected }

    public sealed class WorkshopView
    {
        public int Id { get; }
        public WorkshopKind Kind { get; }
        public int X { get; }
        public int Y { get; }
        public int Footprint => Kind == WorkshopKind.Warehouse ? 1 : 2;
        public IReadOnlyDictionary<WorkshopGood, int> Stock { get; }
        public double Progress { get; }
        public bool OutputBlocked => Kind!=WorkshopKind.Warehouse&&Held(Kind==WorkshopKind.Fishery?WorkshopGood.Fish:WorkshopGood.Salt)>=6;
        public WorkshopView(int id, WorkshopKind kind, int x, int y, IDictionary<WorkshopGood,int> stock, double progress)
        {
            Id=id; Kind=kind; X=x; Y=y; Progress=progress;
            Stock=new ReadOnlyDictionary<WorkshopGood,int>(new Dictionary<WorkshopGood,int>(stock));
        }
        public int Held(WorkshopGood good) => Stock.TryGetValue(good,out var n) ? n : 0;
        public int Total => Stock.Values.Sum();
    }
    public sealed class FormationView
    {
        public string Key { get; }
        public WorkshopGood Product { get; }
        public int CoreId { get; }
        public IReadOnlyList<int> Members { get; }
        public int X { get; }
        public int Y { get; }
        public int Width { get; }
        public int Height { get; }
        public FormationView(WorkshopGood product,int coreId,IEnumerable<int> members,int x,int y,int width,int height)
        {
            Product=product; CoreId=coreId; Members=Array.AsReadOnly(members.OrderBy(i=>i).ToArray());
            X=x;Y=y;Width=width;Height=height;Key=product+":"+string.Join(",",Members);
        }
    }
    public sealed class RouteView
    {
        public int From { get; }
        public int To { get; }
        public RouteView(int from,int to) { From=from;To=to; }
    }
    public sealed class CargoView
    {
        public long Id { get; }
        public int From { get; }
        public int To { get; }
        public WorkshopGood Good { get; }
        public long DepartAt { get; }
        public long ArriveAt { get; }
        public int FromX { get; }
        public int FromY { get; }
        public int ToX { get; }
        public int ToY { get; }
        public CargoView(long id,int from,int to,WorkshopGood good,long departAt,long arriveAt,int fromX,int fromY,int toX,int toY)
        { Id=id;From=from;To=to;Good=good;DepartAt=departAt;ArriveAt=arriveAt;FromX=fromX;FromY=fromY;ToX=toX;ToY=toY; }
    }
    public sealed class EvolutionView
    {
        public long AtMs { get; }
        public int BoardSize { get; }
        public IReadOnlyList<WorkshopView> Workshops { get; }
        public IReadOnlyList<FormationView> Formations { get; }
        public IReadOnlyList<RouteView> Routes { get; }
        public IReadOnlyList<CargoView> Cargo { get; }
        public EvolutionView(long atMs,int size,IEnumerable<WorkshopView> workshops,IEnumerable<FormationView> formations,IEnumerable<RouteView> routes,IEnumerable<CargoView> cargo)
        {
            AtMs=atMs;BoardSize=size;Workshops=Array.AsReadOnly(workshops.ToArray());
            Formations=Array.AsReadOnly(formations.ToArray());Routes=Array.AsReadOnly(routes.ToArray());Cargo=Array.AsReadOnly(cargo.ToArray());
        }
    }
    public sealed class EvolutionEvent
    {
        public long AtMs { get; }
        public EvolutionEventKind Kind { get; }
        public int SubjectId { get; }
        public WorkshopGood? Good { get; }
        public EvolutionEvent(long at,EvolutionEventKind kind,int subject,WorkshopGood? good=null) { AtMs=at;Kind=kind;SubjectId=subject;Good=good; }
    }
    public sealed class EvolutionResult
    {
        public bool Success { get; }
        public string Reason { get; }
        public int SubjectId { get; }
        public IReadOnlyList<EvolutionEvent> Events { get; }
        public EvolutionResult(bool success,string reason,int subject,IEnumerable<EvolutionEvent> events)
        { Success=success;Reason=reason;SubjectId=subject;Events=Array.AsReadOnly(events.ToArray()); }
    }
}
