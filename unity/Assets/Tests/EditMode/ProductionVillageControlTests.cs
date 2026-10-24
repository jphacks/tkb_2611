using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using ProjectPenguin.Data;
using ProjectPenguin.Domain.Village;
using ProjectPenguin.Systems;
using UnityEngine;

namespace ProjectPenguin.Tests.EditMode
{
    public sealed class ProductionVillageControlTests
    {
        private sealed class MemoryStore:IProductionVillageStore
        {
            public string Json;public int Writes;public bool Fail;
            public ProductionVillageSave Load()=>Json==null?null:JsonUtility.FromJson<ProductionVillageSave>(Json);
            public void Save(ProductionVillageSave s){if(Fail)throw new IOException();Json=JsonUtility.ToJson(s);Writes++;}
        }
        [Test] public void FailedSaveDoesNotPublishConstructionOrReward()
        {
            var store=new MemoryStore();var core=new ProductionVillageSessionCore(store,new MemoryStore(),ProductionVillageStateTests.Game,true);core.Open(10000);
            var balance=core.View.Gas;store.Fail=true;Assert.That(core.AcceptSteps(1,500,1,10000),Is.False);Assert.That(core.View.Gas,Is.EqualTo(balance));Assert.That(core.Records.Entries.Count,Is.Zero);
            Assert.That(core.Place(ProjectPenguin.Domain.Island.FacilityKind.Fishery,20,20,10000),Is.False);Assert.That(core.View.Nodes.Count,Is.EqualTo(3));
        }
        [Test] public void ExplicitDevelopmentSwitchKeepsProductSaveUntouched()
        {
            var store=new MemoryStore();var debug=new MemoryStore();var core=new ProductionVillageSessionCore(store,debug,ProductionVillageStateTests.Game,true);core.Open(10000);var original=store.Json;
            Assert.That(core.Resources(0,0,0,false,10000),Is.False);Assert.That(core.BeginDevelopment(10000),Is.True);
            Assert.That(core.Resources(7,8,9,false,10000),Is.True);Assert.That(core.View.Gas,Is.EqualTo(7));core.Forward(60,10000);core.Save(10000);core.ResetDevelopment(10000);
            Assert.That(store.Json,Is.EqualTo(original));Assert.That(debug.Writes,Is.GreaterThan(1));
            var restart=new ProductionVillageSessionCore(store,debug,()=>throw new Exception("initial settings must not overwrite save"),true);restart.Open(10000);Assert.That(restart.View.Gas,Is.EqualTo(100));
        }
        [Test] public void ReleaseEquivalentRefusesEveryDevelopmentEntry()
        {
            var core=new ProductionVillageSessionCore(new MemoryStore(),new MemoryStore(),ProductionVillageStateTests.Game,false);core.Open(10000);
            Assert.That(core.BeginDevelopment(10000),Is.False);Assert.That(core.Resources(9,9,9,false,10000),Is.False);Assert.That(core.Forward(1,10000),Is.False);Assert.That(core.Configure(true,10,100,10,false,10000),Is.False);Assert.That(core.ResetDevelopment(10000),Is.False);
        }
        [Test] public void NonFiniteValuesAndOverflowAreRejected()
        {
            var core=new ProductionVillageSessionCore(new MemoryStore(),new MemoryStore(),ProductionVillageStateTests.Game,true);core.Open(10000);core.BeginDevelopment(10000);
            Assert.That(core.Resources(double.NaN,0,0,false,10000),Is.False);Assert.That(core.Resources(double.PositiveInfinity,0,0,false,10000),Is.False);Assert.That(core.Resources(-1,0,0,false,10000),Is.False);Assert.That(core.Forward(double.MaxValue,10000),Is.False);
            Assert.That(core.Resources(double.MaxValue,0,0,false,10000),Is.False);
        }
        [Test] public void FirstSaveFailureDoesNotPublishUnsavedVillage()
        {var store=new MemoryStore{Fail=true};var c=new ProductionVillageSessionCore(store,new MemoryStore(),ProductionVillageStateTests.Game,true);c.Open(10000);Assert.That(c.View,Is.Null);Assert.That(c.Error,Is.Not.Empty);}
        [Test] public void ValidReloadUnblocksPreviouslyFailedStore()
        {
            var path=Path.Combine(Path.GetTempPath(),"penguin-village-test-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(path);
            try
            {
                var store=new ProductionVillageStore(path);var save=new ProductionVillageSave{State=ProductionVillageStateTests.Game().Export(),UtcMs=10000};store.Save(save);var bytes=File.ReadAllBytes(store.FilePath);
                File.WriteAllText(store.FilePath,"temporarily invalid");Assert.Catch(()=>store.Load());File.WriteAllBytes(store.FilePath,bytes);
                var recovered=store.Load();Assert.DoesNotThrow(()=>store.Save(recovered));
            }
            finally{Directory.Delete(path,true);}
        }
        [Test] public void ResumeFailureRetainsPendingIntervalUntilRetry()
        {
            var store=new MemoryStore();var now=ProductionVillageSession.UtcNow;
            var core=new ProductionVillageSessionCore(store,new MemoryStore(),ProductionVillageStateTests.Game,true);core.Open(now-1000);core.Tick(100);
            var owner=new GameObject("Village resume verification");owner.SetActive(false);
            try
            {
                var session=owner.AddComponent<ProductionVillageSession>();var flags=BindingFlags.Instance|BindingFlags.NonPublic;
                typeof(ProductionVillageSession).GetField("<Core>k__BackingField",flags).SetValue(session,core);
                var pending=typeof(ProductionVillageSession).GetField("_suspendedUtc",flags);pending.SetValue(session,now-1000);store.Fail=true;
                typeof(ProductionVillageSession).GetMethod("Resume",flags).Invoke(session,null);
                Assert.That((long)pending.GetValue(session),Is.GreaterThan(0));Assert.That(core.View.AtMs,Is.EqualTo(100));
                store.Fail=false;session.RetryLoad();Assert.That((long)pending.GetValue(session),Is.Zero);Assert.That(core.View.AtMs,Is.GreaterThanOrEqualTo(1100));
            }
            finally{UnityEngine.Object.DestroyImmediate(owner);}
        }
        [Test] public void ReopenAndRollbackUtcDoNotDoubleProduce()
        {
            var store=new MemoryStore();var core=new ProductionVillageSessionCore(store,new MemoryStore(),ProductionVillageStateTests.Game,true);core.Open(10000);core.Tick(4200);core.Save(14200);
            var next=new ProductionVillageSessionCore(store,new MemoryStore(),ProductionVillageStateTests.Game,true);next.Open(20000);var made=next.View.Produced;
            var third=new ProductionVillageSessionCore(store,new MemoryStore(),ProductionVillageStateTests.Game,true);third.Open(15000);Assert.That(third.View.Produced,Is.EqualTo(made));Assert.That(third.View.AtMs,Is.EqualTo(next.View.AtMs));
        }
        [Test] public void SqliteRoundTripAndCorruptionProtectOldAndProductDatabases()
        {
            var path=Path.Combine(Path.GetTempPath(),"penguin-village-test-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(path);
            try
            {
                var old=Path.Combine(path,"penguin.db");File.WriteAllText(old,"old protected");
                var store=new ProductionVillageStore(path);var g=ProductionVillageStateTests.Game();g.AdvanceTo(4200);
                store.Save(new ProductionVillageSave{State=g.Export(),UtcMs=10000});var loaded=store.Load();Assert.That(loaded.State.Cargo.Count,Is.GreaterThan(0));
                Assert.That(File.ReadAllText(old),Is.EqualTo("old protected"));
                File.WriteAllText(store.FilePath,"broken");Assert.Throws<SQLite.SQLiteException>(()=>store.Load());Assert.Throws<InvalidOperationException>(()=>store.Save(loaded));Assert.That(File.ReadAllText(store.FilePath),Is.EqualTo("broken"));
            }
            finally{Directory.Delete(path,true);}
        }
    }
}
