using System.Linq;
using NUnit.Framework;
using ProjectPenguin.Data;
using ProjectPenguin.Domain.Village;
using ProjectPenguin.Presentation.World.ProductionVillage;
using ProjectPenguin.Systems;

namespace ProjectPenguin.Tests.EditMode
{
    public sealed class VillageMoveTests
    {
        private sealed class Store:IProductionVillageStore { public ProductionVillageSave Load()=>null;public void Save(ProductionVillageSave s){} }
        [Test] public void PreviewCancelAndConfirmAreSeparateCommands()
        {
            var s=new ProductionVillageSessionCore(new Store(),new Store(),ProductionVillageStateTests.Game,true);s.Open(10000);
            var p=new VillagePlacementController(s);p.MoveSelect();p.Select(1);p.Preview(16,16);
            Assert.That(s.View.Nodes[0].X,Is.EqualTo(2));p.CancelPreview();Assert.That(s.View.Nodes[0].X,Is.EqualTo(2));
            p.Select(1);p.Preview(16,16);Assert.That(p.Confirm(),Is.True);Assert.That(s.View.Nodes[0].X,Is.EqualTo(16));Assert.That(p.Mode,Is.EqualTo(VillagePlacementMode.MoveSelect));
        }
        [Test] public void ConfirmRechecksDeliveryThatStartedAfterPreview()
        {
            var s=new ProductionVillageSessionCore(new Store(),new Store(),ProductionVillageStateTests.Game,true);s.Open(10000);
            var p=new VillagePlacementController(s);p.MoveSelect();p.Select(2);p.Preview(16,16);Assert.That(p.Reason,Is.Empty);
            s.Tick(4200);Assert.That(s.View.Cargo.Any(c=>c.To==2),Is.True);
            Assert.That(p.Confirm(),Is.False);Assert.That(s.View.Nodes.Single(n=>n.Id==2).X,Is.EqualTo(5));Assert.That(p.Reason,Does.Contain("到着"));
        }
    }
}
