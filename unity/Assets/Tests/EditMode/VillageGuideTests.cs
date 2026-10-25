using System.Linq;
using NUnit.Framework;
using ProjectPenguin.Domain.Island;
using ProjectPenguin.Domain.Village;

namespace ProjectPenguin.Tests.EditMode
{
    /// <summary>置く前の案内 (つながる相手・施設群の不足 1 棟・次にやること)。村の状態を変えないことも確かめる。</summary>
    public sealed class VillageGuideTests
    {
        static int IdAt(VillageGame g,int x,int y)=>g.CreateView().Nodes.Single(n=>n.X==x&&n.Y==y).Id;

        [Test] public void PreviewUsesTheSameRuleAsRoutes()
        {
            var g=new VillageGame(true);g.Place(FacilityKind.Fishery,0,0);g.Place(FacilityKind.Warehouse,20,20);
            var fishery=IdAt(g,0,0);
            var near=g.PreviewLinks(FacilityKind.Preparation,4,0);
            Assert.That(near.Sources,Is.EqualTo(new[]{fishery}),"漁場から 8 マス以内ならお魚が届く");
            Assert.That(near.Reach,Is.EqualTo(8));
            var far=g.PreviewLinks(FacilityKind.Preparation,10,0);
            Assert.That(far.Connected,Is.False,"10 マス離れると届かない");
            Assert.That(g.PreviewLinks(FacilityKind.Warehouse,6,0).Reach,Is.Zero,"レシピの無い施設は送り出さない");

            g.Place(FacilityKind.Preparation,4,0);
            var routes=g.CreateView().Routes;
            Assert.That(routes.Any(r=>r.From==fishery&&r.To==IdAt(g,4,0)),Is.True,"置いたあとも同じつながり");
            Assert.That(g.ReachOfNode(fishery),Is.EqualTo(8));
        }

        [Test] public void SorterNearbyExtendsReachInPreview()
        {
            var g=new VillageGame(true);g.Place(FacilityKind.Fishery,0,0);g.Place(FacilityKind.Sorter,3,0);
            Assert.That(g.PreviewLinks(FacilityKind.Preparation,11,0).Sources.Count,Is.EqualTo(1),"仕分け所の近くの漁場は 12 マス届く");
        }

        [Test] public void PreviewDoesNotChangeTheVillage()
        {
            var g=new VillageGame(true);g.Place(FacilityKind.Fishery,0,0);
            var before=g.Export();
            g.PreviewLinks(FacilityKind.Preparation,4,0);g.GroupHints();
            Assert.That(g.Export().Nodes.Count,Is.EqualTo(before.Nodes.Count));
        }

        [Test] public void HintFindsTheSingleMissingCell()
        {
            var g=new VillageGame(true);g.Place(FacilityKind.Fishery,0,0);g.Place(FacilityKind.Fishery,2,0);
            var hints=g.GroupHints();
            Assert.That(hints.Any(h=>h.Name=="ちくわ3連ライン"&&h.Kind==FacilityKind.Fishery&&h.X==4&&h.Y==0),Is.True);
            Assert.That(hints.Any(h=>h.Name=="かまぼこ黄金ライン"),Is.False,"漁場-漁場の並びは かまぼこ (漁場-塩田-漁場) の 2/3 ではない");

            var placed=new VillageGame(true);placed.Place(FacilityKind.Fishery,0,0);placed.Place(FacilityKind.Fishery,2,0);placed.Place(FacilityKind.Fishery,4,0);
            Assert.That(placed.CreateView().Synergies.Count,Is.EqualTo(1));
            Assert.That(placed.GroupHints().Any(h=>h.Name=="ちくわ3連ライン"),Is.False,"成立済みの施設は案内に使わない");
        }

        [Test] public void HintSkipsOccupiedCells()
        {
            var g=new VillageGame(true);g.Place(FacilityKind.Fishery,0,0);g.Place(FacilityKind.Fishery,2,0);g.Place(FacilityKind.Preparation,4,0);
            Assert.That(g.GroupHints().Any(h=>h.X==4&&h.Y==0),Is.False);
        }

        [Test] public void NextStepPrefersTheMostUrgentProblem()
        {
            Assert.That(VillageInsight.NextStep(null,null).Tone,Is.EqualTo(VillageNextStepTone.Info));
            var empty=new VillageGame(true);
            var first=VillageInsight.NextStep(empty.CreateView(),empty.GroupHints());
            Assert.That(first.Build,Is.EqualTo(FacilityKind.Fishery));

            var g=new VillageGame(true);g.Place(FacilityKind.Fishery,0,0);g.Place(FacilityKind.Fishery,2,0);
            var hint=VillageInsight.NextStep(g.CreateView(),g.GroupHints());
            Assert.That(hint.Build,Is.EqualTo(FacilityKind.Fishery),"あと 1 棟の施設群を案内する");
            StringAssert.Contains("ちくわ3連ライン",hint.Text);
        }

        [Test] public void NextStepAsksForAConsumerWhenOutputHasNowhereToGo()
        {
            var g=new VillageGame(true);g.Place(FacilityKind.Fishery,0,0);
            g.AdvanceTo(120000);
            var step=VillageInsight.NextStep(g.CreateView(),System.Array.Empty<VillageGroupHint>());
            Assert.That(step.Build,Is.EqualTo(FacilityKind.Preparation),"お魚を使うさばき場を案内する");
            Assert.That(step.Tone,Is.EqualTo(VillageNextStepTone.Warn));
            Assert.That(step.NodeIds,Is.EqualTo(new[]{IdAt(g,0,0)}));
        }

        [Test] public void NextStepReportsGasShortageFirst()
        {
            var g=new VillageGame(true);g.Place(FacilityKind.Fishery,0,0);
            var s=g.Export();s.Unlimited=false;s.Gas=0;g=VillageGame.Restore(s);
            g.AdvanceTo(5000);
            var step=VillageInsight.NextStep(g.CreateView(),g.GroupHints());
            Assert.That(step.Tone,Is.EqualTo(VillageNextStepTone.Bad));
            StringAssert.Contains("歩く",step.Hint);
        }
    }
}
