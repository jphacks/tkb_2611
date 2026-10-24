using NUnit.Framework;
using System;
using System.Linq;
using ProjectPenguin.Domain.Island;
using ProjectPenguin.Domain.Village;

namespace ProjectPenguin.Tests.EditMode
{
    public sealed class ProductionVillageTests
    {
        private static VillageGame Game()
        { var g=new VillageGame(false); g.SetResources(100,1000,1000); return g; }
        [Test] public void UnsupportedEffectCannotConsumeConstructionResources()
        { var g=Game(); Assert.That(g.Place(FacilityKind.Research,0,0).Success,Is.False); Assert.That(g.CreateView().Electricity,Is.EqualTo(1000)); }
        [Test] public void PaidProductionFinishesWithoutGasAndMovePreservesProgress()
        {
            var g=Game();var id=g.Place(FacilityKind.Fishery,2,2).Id;g.AdvanceTo(100);
            g.SetResources(0,0,0); Assert.That(g.Move(id,7,7).Success,Is.True);
            g.AdvanceTo(4100);Assert.That(g.CreateView().Nodes.Single().Total,Is.EqualTo(1));
            Assert.That(g.CreateView().Nodes.Single().Id,Is.EqualTo(id));
        }
        [Test] public void PlacementOverflowCannotBypassBoardBounds()
        {var g=Game(); Assert.That(g.Place(FacilityKind.Fishery,int.MaxValue,0).Success,Is.False);}
        [Test]
        public void ProductionGameExistsOutsidePrototypeAssembly()
        {
            var type = typeof(ProjectPenguin.Domain.Island.IslandCatalog).Assembly.GetType("ProjectPenguin.Domain.Village.VillageGame");
            Assert.That(type, Is.Not.Null, "製品用の独立した村モデルが必要です");
        }
    }
}
