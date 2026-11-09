using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PhysicalAssembly;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FireRescue.Tests
{
    // Real paged shop, real display builders and scene buttons; injected hand
    // samples exercise arbitration, not headset readability or performance.
    public sealed class EmberdeepShopPreviewPlayTests
    {
        FireRescueGame game;
        [UnitySetUp] public IEnumerator Load()
        {
            EmberdeepSession.NewExpedition();yield return SceneManager.LoadSceneAsync(EmberdeepSession.FireScene);
            yield return null;yield return null;
            game=Object.FindFirstObjectByType<FireRescueGame>();Assert.NotNull(game);game.enabled=false;
            game.SendMessage("OnApplicationFocus",true);game.SendMessage("OnApplicationPause",false);
            Assert.IsTrue(game.TakeEquipment(1));Clear();game.OpenShop();Assert.IsTrue(game.ShopOpen);
            game.MenuInput.ViewCamera=game.Player.ViewCamera;
        }
        void Clear()
        {foreach(var tree in game.Run.Trees)if(tree.State==TreeState.Burning)game.Run.ApplyWater(tree.Position,100,.1f);}
        WorkshopButton Button(string command)=>game.GetComponentsInChildren<WorkshopButton>(true).Single(x=>x.Command==command);
        void Look(WorkshopButton button)
        {
            Physics.SyncTransforms();var camera=game.Player.ViewCamera;
            camera.transform.rotation=Quaternion.LookRotation(button.GetComponent<Collider>().bounds.center-camera.transform.position,Vector3.up);
            game.MenuInput.ViewCamera=camera;
        }
        bool Point(int hand,WorkshopButton button,float pinch)
        {
            // Deliberately near the well: eligible shop UI must retain the
            // physical-plus-ray priority fixed in the previous delivery.
            var position=game.HydrantStation.Lever.position;
            var physical=new EmberdeepRefillInteraction.Hand(true,new Pose(position,Quaternion.identity),pinch,position);
            var ray=new EmberdeepHandRay.Sample(true,new Ray(position,button.GetComponent<Collider>().bounds.center-position),pinch);
            return game.ProcessHandInterfaceSamples(hand==0?physical:default,hand==1?physical:default,
                hand==0?ray:default,hand==1?ray:default,true,.02f);
        }
        void Click(int hand,string command)
        {
            var button=Button(command);Look(button);for(int i=0;i<8;i++)Assert.IsFalse(Point(hand,button,.04f));
            Assert.AreSame(button,game.MenuInput.HoveredButton);Assert.IsTrue(Point(hand,button,.01f),command);
        }
        sealed class State
        {
            readonly int coins;readonly FireEquipmentId equipped;readonly WaterSourceKind source;
            readonly float supplied,recovered,consumed,charge;readonly bool valve;
            readonly float[] tanks=new float[4];readonly bool[] owned=new bool[4];readonly int[] levels=new int[4];
            public State(FireRun run)
            {
                coins=run.Coins;equipped=run.Equipped;source=run.Supply.Kind;valve=run.Supply.ValveOpen;
                supplied=run.SuppliedWater;recovered=run.RecoveredWater;consumed=run.ConsumedWater;charge=run.Charge01;
                for(int i=0;i<4;i++){tanks[i]=run.WaterFor((FireEquipmentId)i);owned[i]=run.Owns((FireEquipmentId)i);levels[i]=run.UpgradeFor((FireEquipmentId)i);}
            }
            public void Unchanged(FireRun run)
            {
                Assert.AreEqual(coins,run.Coins);Assert.AreEqual(equipped,run.Equipped);Assert.AreEqual(source,run.Supply.Kind);Assert.AreEqual(valve,run.Supply.ValveOpen);
                Assert.AreEqual(supplied,run.SuppliedWater);Assert.AreEqual(recovered,run.RecoveredWater);Assert.AreEqual(consumed,run.ConsumedWater);Assert.AreEqual(charge,run.Charge01);
                for(int i=0;i<4;i++){Assert.AreEqual(tanks[i],run.WaterFor((FireEquipmentId)i));Assert.AreEqual(owned[i],run.Owns((FireEquipmentId)i));Assert.AreEqual(levels[i],run.UpgradeFor((FireEquipmentId)i));}
            }
        }
        void AssertSafePreview()
        {
            var preview=game.ShopPreview;Assert.NotNull(preview);Assert.NotNull(preview.PreviewRoot);
            Assert.IsTrue(preview.PreviewRoot.gameObject.activeInHierarchy);
            Assert.Greater(preview.PreviewRoot.GetComponentsInChildren<MeshRenderer>().Length,0);
            foreach(var component in preview.PreviewRoot.GetComponentsInChildren<Component>(true))
                Assert.IsTrue(component is Transform || component is MeshFilter || component is MeshRenderer,
                    "Display-only preview cannot include physics, audio, particles, interactables or gameplay: "+component.GetType().Name);
            var bounds=preview.ModelBounds;var frame=EmberdeepShopPreview.FrameSize;
            Assert.Greater(bounds.size.sqrMagnitude,.00001f);
            Assert.LessOrEqual(bounds.size.x,frame.x+.001f);Assert.LessOrEqual(bounds.size.y,frame.y+.001f);Assert.LessOrEqual(bounds.size.z,frame.z+.001f);
            Assert.Less(bounds.center.magnitude,.002f,"Model must fit the actual preview frame, including after reopening");
        }

        [UnityTest] public IEnumerator EveryEquipmentAndTuningPageMatchesItsModelPriceAndPurchaseTarget()
        {
            var state=new State(game.Run);
            for(int i=0;i<4;i++)
            {
                var id=(FireEquipmentId)i;game.SelectEquipment(i);
                Assert.AreEqual(EmberdeepShopPage.Equipment,game.ShopPage);Assert.AreEqual("tool:"+id,game.ShopItemId);
                Assert.AreEqual(game.ShopItemId,game.ShopPreview.ItemId);Assert.AreEqual(id,game.ShopPreview.DisplayedEquipment);
                Assert.IsFalse(game.ShopPreview.IsFallback,"Every shipped tool has a production silhouette");
                Assert.IsNull(game.ShopPreview.DisplayedStationTier);Assert.AreEqual(FireEquipment.Get(id).Price,game.ShopPrice);
                Assert.AreEqual(game.Run.CanBuy(id),game.ShopCanBuy);AssertSafePreview();state.Unchanged(game.Run);
                game.ShowShopPage(EmberdeepShopPage.Tuning);
                Assert.AreEqual("tune:"+id+":"+(game.Run.UpgradeFor(id)+1),game.ShopItemId);
                Assert.AreEqual(game.ShopItemId,game.ShopPreview.ItemId);Assert.AreEqual(id,game.ShopPreview.DisplayedEquipment);
                Assert.AreEqual(game.Run.UpgradeCostFor(id),game.ShopPrice);Assert.IsNotEmpty(game.ShopComparison);
                StringAssert.Contains($"TANK {game.Run.CapacityFor(id):0} > {game.Run.CapacityFor(id)+25:0}",game.ShopComparison);
                StringAssert.Contains("No new mesh",string.Join("\n",game.GetComponentsInChildren<TextMesh>().Select(x=>x.text)));
                if(!game.Run.Owns(id))Assert.IsFalse(game.ShopCanBuy);
                AssertSafePreview();state.Unchanged(game.Run);
            }
            yield return null;
        }

        [UnityTest] public IEnumerator StationComparisonShowsActualCurrentAndResultModelsBeforeDeliberatePurchase()
        {
            game.ShowShopPage(EmberdeepShopPage.Station);var state=new State(game.Run);
            Assert.AreEqual("station:RotaryHydrant",game.ShopItemId);Assert.AreEqual(100,game.ShopPrice);
            Assert.AreEqual(WaterStationTier.RotaryHydrant,game.ShopPreview.DisplayedStationTier);Assert.IsTrue(game.ShopCanBuy);AssertSafePreview();
            game.ShowStationComparison(true);Assert.IsTrue(game.ShopShowingCurrentStation);
            Assert.AreEqual("station:RotaryHydrant",game.ShopItemId);Assert.AreEqual(WaterStationTier.LeverWell,game.ShopPreview.DisplayedStationTier);
            Assert.IsFalse(game.ShopCanBuy);Assert.IsFalse(game.BuyShopSelection());state.Unchanged(game.Run);AssertSafePreview();
            game.ShowStationComparison(false);Assert.AreEqual(WaterStationTier.RotaryHydrant,game.ShopPreview.DisplayedStationTier);
            Assert.IsTrue(game.BuyShopSelection());Assert.AreEqual(WaterStationTier.RotaryHydrant,game.Run.StationTier);
            Assert.Zero(game.Run.Coins);Assert.AreEqual("station:PushButton",game.ShopItemId);Assert.AreEqual(200,game.ShopPrice);
            Assert.AreEqual(WaterStationTier.PushButton,game.ShopPreview.DisplayedStationTier);Assert.IsFalse(game.ShopCanBuy);
            Assert.IsFalse(game.BuyShopSelection());Assert.Zero(game.Run.Coins);AssertSafePreview();yield return null;
        }

        [UnityTest] public IEnumerator RapidPagingAndRepeatedRefreshKeepOneActiveModelWithoutTransactions()
        {
            var state=new State(game.Run);var oldRoots=new List<Transform>();
            for(int i=0;i<24;i++)
            {
                var old=game.ShopPreview.PreviewRoot;game.SelectEquipment(i%4);game.ShowShopPage(i%3==0?EmberdeepShopPage.Tuning:EmberdeepShopPage.Equipment);
                if(old!=game.ShopPreview.PreviewRoot){Assert.IsFalse(old.gameObject.activeSelf);oldRoots.Add(old);}
                AssertSafePreview();state.Unchanged(game.Run);
            }
            var stable=game.ShopPreview.PreviewRoot;int generation=game.ShopPreview.Generation;
            for(int i=0;i<40;i++)game.RefreshShopPresentation();
            Assert.AreSame(stable,game.ShopPreview.PreviewRoot);Assert.AreEqual(generation,game.ShopPreview.Generation);
            Assert.AreEqual(1,game.ShopPreview.transform.Cast<Transform>().Count(x=>x.gameObject.activeSelf));
            yield return null;
            foreach(var old in oldRoots)Assert.IsTrue(!old,"Retired preview model must be destroyed after the frame");
            Assert.AreEqual(1,game.ShopPreview.transform.childCount);state.Unchanged(game.Run);
        }

        [UnityTest] public IEnumerator MissingPreviewUsesNamedFallbackWithoutChangingPriceOrDisablingValidPurchase()
        {
            game.SelectEquipment(1);var state=new State(game.Run);string id=game.ShopItemId;int price=game.ShopPrice;bool canBuy=game.ShopCanBuy;
            game.ShopPreview.ForceFallback=true;game.RefreshShopPresentation();
            Assert.IsTrue(game.ShopPreview.IsFallback);Assert.AreEqual(id,game.ShopItemId);Assert.AreEqual(id,game.ShopPreview.ItemId);
            Assert.AreEqual(price,game.ShopPrice);Assert.AreEqual(canBuy,game.ShopCanBuy);AssertSafePreview();
            var text=string.Join("\n",game.GetComponentsInChildren<TextMesh>().Select(x=>x.text));
            StringAssert.Contains("PREVIEW UNAVAILABLE",text);StringAssert.Contains(FireEquipment.Get(FireEquipmentId.PulsePistol).Name,text);
            state.Unchanged(game.Run);game.ShopPreview.ForceFallback=false;game.RefreshShopPresentation();
            Assert.IsFalse(game.ShopPreview.IsFallback);Assert.AreEqual(FireEquipmentId.PulsePistol,game.ShopPreview.DisplayedEquipment);AssertSafePreview();yield return null;
        }

        [UnityTest] public IEnumerator CancelReopenAndTrackingLossPreserveResourcesAndDoNotAccumulateModels()
        {
            var state=new State(game.Run);
            for(int i=0;i<5;i++)
            {
                game.SelectEquipment(i%4);game.CloseShop();game.OpenShop();Assert.IsTrue(game.ShopOpen);AssertSafePreview();state.Unchanged(game.Run);
                var button=Button("Next gear");Look(button);for(int j=0;j<8;j++)Assert.IsFalse(Point(0,button,.04f));
                game.ProcessHandInterfaceSamples(default,default,default,default,true,.02f);
                Assert.IsFalse(game.MenuInput.OwnsInput);
                for(int j=0;j<8;j++)Assert.IsFalse(Point(0,button,.01f));state.Unchanged(game.Run);
                yield return null;Assert.AreEqual(1,game.ShopPreview.transform.childCount);
            }
        }

        [UnityTest] public IEnumerator LeftHandPreviewThenBuyIsSeparateAndCannotRepeatThroughHeldPinch()=>PurchaseWithHand(0);
        [UnityTest] public IEnumerator RightHandPreviewThenBuyIsSeparateAndCannotRepeatThroughHeldPinch()=>PurchaseWithHand(1);
        IEnumerator PurchaseWithHand(int hand)
        {
            var state=new State(game.Run);Click(hand,"Next gear");Assert.AreEqual(FireEquipmentId.PulsePistol,game.SelectedEquipment);
            Assert.AreEqual("tool:PulsePistol",game.ShopItemId);Assert.AreEqual(FireEquipmentId.PulsePistol,game.ShopPreview.DisplayedEquipment);state.Unchanged(game.Run);
            var buy=Button("Buy gear");Look(buy);for(int i=0;i<12;i++)Assert.IsFalse(Point(hand,buy,.01f));state.Unchanged(game.Run);
            int coins=game.Run.Coins;float water=game.Run.Water;Click(hand,"Buy gear");
            Assert.IsTrue(game.Run.Owns(FireEquipmentId.PulsePistol));Assert.AreEqual(coins-100,game.Run.Coins);
            Assert.Zero(game.Run.WaterFor(FireEquipmentId.PulsePistol));Assert.AreEqual(water,game.Run.Water);Assert.IsFalse(game.ShopCanBuy);
            for(int i=0;i<12;i++)Assert.IsFalse(Point(hand,buy,.01f));Assert.AreEqual(coins-100,game.Run.Coins);
            Assert.IsFalse(game.RefillInteraction.OwnsControl);Assert.IsFalse(game.Spraying);Assert.IsFalse(game.Run.Supply.Locked);yield return null;
        }

        [UnityTest] public IEnumerator TuningPreviewIsNotPurchaseAndOnePinchCannotBuyTheFollowingLevel()
        {
            game.CloseShop();for(int i=0;i<2;i++){game.StartNext();Clear();}game.OpenShop();
            game.SelectEquipment(0);int coins=game.Run.Coins;float water=game.Run.Water;
            Click(1,"Tune gear");Assert.AreEqual(EmberdeepShopPage.Tuning,game.ShopPage);Assert.Zero(game.Run.UpgradeFor(FireEquipmentId.Extinguisher));
            Assert.AreEqual(coins,game.Run.Coins);Assert.IsNotEmpty(game.ShopComparison);Click(1,"Buy gear");
            Assert.AreEqual(1,game.Run.UpgradeFor(FireEquipmentId.Extinguisher));Assert.AreEqual(coins-75,game.Run.Coins);
            Assert.AreEqual(water,game.Run.Water);Assert.IsTrue(game.ShopCanBuy,"Fixture must afford a second level so input protection is actually tested");
            var buy=Button("Buy gear");for(int i=0;i<12;i++)Assert.IsFalse(Point(1,buy,.01f));
            Assert.AreEqual(1,game.Run.UpgradeFor(FireEquipmentId.Extinguisher));Assert.AreEqual(coins-75,game.Run.Coins);yield return null;
        }

        static Bounds TextBoundsIn(Transform panel,TextMesh text)
        {
            var renderer=text.GetComponent<Renderer>();var local=renderer.localBounds;
            var matrix=panel.worldToLocalMatrix*renderer.transform.localToWorldMatrix;
            var result=new Bounds(matrix.MultiplyPoint3x4(local.center),Vector3.zero);
            for(int corner=0;corner<8;corner++)
            {
                var sign=new Vector3((corner&1)==0?-1:1,(corner&2)==0?-1:1,(corner&4)==0?-1:1);
                result.Encapsulate(matrix.MultiplyPoint3x4(local.center+Vector3.Scale(local.extents,sign)));
            }
            return result;
        }

        [UnityTest] public IEnumerator ItemFactsRemainSeparatedAndFitPanelAcrossPagesAndWorldYaw()
        {
            const System.Reflection.BindingFlags fields=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            var panel=(Transform)typeof(FireRescueGame).GetField("shop",fields).GetValue(game);
            var names=new[]{"shopName","shopDescription","shopStats","shopState"};
            var labels=names.Select(name=>(TextMesh)typeof(FireRescueGame).GetField(name,fields).GetValue(game)).ToArray();
            var hud=panel.parent;var camera=game.Player.ViewCamera.transform;
            var origin=camera.position;var offset=hud.position-origin;var orientation=hud.rotation;var head=camera.rotation;
            void Check(string context)
            {
                var bounds=new Bounds[labels.Length];
                for(int i=0;i<labels.Length;i++)
                {
                    Assert.IsTrue(labels[i].gameObject.activeInHierarchy,context+" / "+names[i]);
                    Assert.IsNotEmpty(labels[i].text,context+" / "+names[i]);
                    bounds[i]=TextBoundsIn(panel,labels[i]);
                    Assert.Greater(bounds[i].size.y,.0001f,context+" / text mesh must have rendered bounds");
                    Assert.LessOrEqual(bounds[i].size.x,1.75f+.001f,context+" / "+names[i]+" exceeds the item facts column");
                    if(i>0)Assert.GreaterOrEqual(bounds[i-1].min.y,bounds[i].max.y-.0001f,
                        context+" / "+names[i-1]+" overlaps "+names[i]+" (actual rendered text, not anchor spacing)");
                }
            }
            foreach(float yaw in new[]{0f,90f})
            {
                var rotation=Quaternion.AngleAxis(yaw,Vector3.up);
                hud.SetPositionAndRotation(origin+rotation*offset,rotation*orientation);camera.rotation=rotation*head;
                for(int item=0;item<4;item++)
                {
                    game.SelectEquipment(item);yield return null;
                    Check($"yaw {yaw}, equipment {(FireEquipmentId)item}");
                    game.ShowShopPage(EmberdeepShopPage.Tuning);yield return null;
                    Check($"yaw {yaw}, tuning {(FireEquipmentId)item}");
                }
                // Isolate text layouts for both offered station upgrades without
                // spending currency; economic transitions have separate tests.
                foreach(var tier in new[]{WaterStationTier.LeverWell,WaterStationTier.RotaryHydrant})
                {
                    typeof(FireRun).GetProperty(nameof(FireRun.StationTier)).SetValue(game.Run,tier);
                    game.ShowShopPage(EmberdeepShopPage.Station);yield return null;
                    Check($"yaw {yaw}, station result after {tier}");
                    game.ShowStationComparison(true);yield return null;
                    Check($"yaw {yaw}, current station {tier}");
                }
            }
        }
    }
}
