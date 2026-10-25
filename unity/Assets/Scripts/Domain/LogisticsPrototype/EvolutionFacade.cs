using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectPenguin.Domain.LogisticsPrototype
{
    public sealed class EvolutionFacade
    {
        private readonly EvolutionState _state;
        public EvolutionFacade(int boardSize=24)
        {
            if(boardSize<6||boardSize>48) throw new ArgumentOutOfRangeException(nameof(boardSize));
            _state=new EvolutionState(boardSize);
        }
        public EvolutionView CreateView() => new EvolutionView(_state.AtMs,_state.Size,
            _state.Workshops.Select(n=>new WorkshopView(n.Id,n.Kind,n.X,n.Y,n.Stock,n.Kind==WorkshopKind.Warehouse?0:(_state.AtMs%EvolutionFlow.CycleMs)/(double)EvolutionFlow.CycleMs)),
            _state.Formations,_state.Routes,_state.Cargo);
        public EvolutionResult Place(WorkshopKind kind,int x,int y)
        {
            if(!Enum.IsDefined(typeof(WorkshopKind),kind)) return Fail("未対応の施設です");
            var n=new WorkshopState{Id=_state.NextId,Kind=kind,X=x,Y=y};
            if(!Fits(n,x,y)) return Fail("敷地の外、または別の施設と重なっています");
            _state.NextId++;_state.Workshops.Add(n);return Changed(EvolutionEventKind.Placed,n.Id);
        }
        public EvolutionResult Move(int id,int x,int y)
        {
            var n=_state.Workshops.FirstOrDefault(w=>w.Id==id);
            if(n==null) return Fail("施設がありません");
            if(_state.Cargo.Any(c=>c.To==id)) return Fail("入庫予定の倉庫は移動できません。線を外し到着を待ってください");
            if(!Fits(n,x,y)) return Fail("敷地の外、または別の施設と重なっています");
            n.X=x;n.Y=y;return Changed(EvolutionEventKind.Moved,id);
        }
        public EvolutionResult Remove(int id)
        {
            var n=_state.Workshops.FirstOrDefault(w=>w.Id==id);
            if(n==null) return Fail("施設がありません");
            if(_state.Cargo.Any(c=>c.From==id||c.To==id)||n.Stock.Values.Sum()>0) return Fail("配送中または在庫あり。先に倉庫へ届けてください");
            _state.Workshops.Remove(n);_state.Routes.RemoveAll(r=>r.From==id||r.To==id);return Changed(EvolutionEventKind.Removed,id);
        }
        public EvolutionResult Connect(int from,int to)
        {
            var a=_state.Workshops.FirstOrDefault(n=>n.Id==from);var b=_state.Workshops.FirstOrDefault(n=>n.Id==to);
            if(a==null||b==null||a.Kind==WorkshopKind.Warehouse||b.Kind!=WorkshopKind.Warehouse||_state.Routes.Any(r=>r.From==from&&r.To==to)) return Fail("生産施設 → 倉庫のみ接続できます。重複不可");
            _state.Routes.Add(new RouteView(from,to));return Ok(from,new[]{new EvolutionEvent(_state.AtMs,EvolutionEventKind.Connected,from)});
        }
        public EvolutionResult DisconnectAll()
        {
            _state.Routes.Clear();return Ok(0,new[]{new EvolutionEvent(_state.AtMs,EvolutionEventKind.Disconnected,0)});
        }
        public EvolutionResult AdvanceTo(long milliseconds)
        {
            if(milliseconds<_state.AtMs||milliseconds-_state.AtMs>120000) return Fail("時刻は単調増加、1回の進行は120秒以内です");
            var events=new List<EvolutionEvent>();
            while(_state.StepAt+EvolutionFlow.StepMs<=milliseconds) EvolutionFlow.Step(_state,_state.StepAt+EvolutionFlow.StepMs,events);
            _state.AtMs=milliseconds;return Ok(0,events);
        }
        private bool Fits(WorkshopState n,int x,int y) => x>=0&&y>=0&&x<=_state.Size-n.Size&&y<=_state.Size-n.Size
            &&!_state.Workshops.Any(o=>o.Id!=n.Id&&x<o.X+o.Size&&x+n.Size>o.X&&y<o.Y+o.Size&&y+n.Size>o.Y);
        private EvolutionResult Changed(EvolutionEventKind kind,int id)
        {
            var events=new List<EvolutionEvent>{new EvolutionEvent(_state.AtMs,kind,id)};
            var next=FormationRules.Find(_state.Workshops);
            foreach(var old in _state.Formations.Where(s=>!next.Any(n=>n.Key==s.Key))) events.Add(new EvolutionEvent(_state.AtMs,EvolutionEventKind.Dissolved,old.CoreId,old.Product));
            foreach(var formed in next.Where(s=>!_state.Formations.Any(n=>n.Key==s.Key))) events.Add(new EvolutionEvent(_state.AtMs,EvolutionEventKind.Formed,formed.CoreId,formed.Product));
            _state.Formations.Clear();_state.Formations.AddRange(next);return Ok(id,events);
        }
        private EvolutionResult Fail(string reason) => new EvolutionResult(false,reason,0,Array.Empty<EvolutionEvent>());
        private EvolutionResult Ok(int id,IEnumerable<EvolutionEvent> events) => new EvolutionResult(true,"",id,events);
    }
}
