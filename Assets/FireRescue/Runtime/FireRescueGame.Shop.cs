using UnityEngine;
using PhysicalAssembly;

namespace FireRescue
{
    public enum EmberdeepShopPage { Equipment, Tuning, Station }

    public sealed partial class FireRescueGame
    {
        public EmberdeepShopPage ShopPage { get; private set; }
        public EmberdeepShopPreview ShopPreview { get; private set; }
        public string ShopItemId { get; private set; }
        public int ShopPrice { get; private set; }
        public bool ShopCanBuy { get; private set; }
        public string ShopComparison=>shopStats?shopStats.text:"";
        public bool ShopShowingCurrentStation { get; private set; }
        WorkshopButton shopPrevious,shopNext,shopCompare;
        TextMesh shopPageText,shopPreviewNote,shopCompareText;

        public void ShowShopPage(EmberdeepShopPage page)
        {
            ShopPage=page;ShopShowingCurrentStation=false;
            handMenu?.ResetInput();RefreshShop();
        }
        public void ShowStationComparison(bool current)
        {
            if(ShopPage!=EmberdeepShopPage.Station)return;
            ShopShowingCurrentStation=current;handMenu?.ResetInput();RefreshShop();
        }
        public void RefreshShopPresentation()=>RefreshShop();
        public bool BuyShopSelection()
        {
            if(!ShopOpen || !ShopCanBuy || !ShopPreview || !ShopPreview.PreviewRoot
                || ShopShowingCurrentStation || ShopPreview.ItemId!=ShopItemId)return false;
            bool bought;
            if(ShopPage==EmberdeepShopPage.Equipment)bought=BuySelectedEquipment();
            else if(ShopPage==EmberdeepShopPage.Tuning)
            {
                int cost=Run.UpgradeCostFor(SelectedEquipment);
                if(!Run.InSupply || Run.Supply.Locked || !Run.Owns(SelectedEquipment) || cost<=0 || Run.Coins<cost
                    || cost!=ShopPrice || ShopItemId!=$"tune:{SelectedEquipment}:{Run.UpgradeFor(SelectedEquipment)+1}")return false;
                var equipped=Run.Equipped;
                if(!Run.Equip(SelectedEquipment))return false;
                try{bought=BuyUpgrade();}finally{Run.Equip(equipped);}
            }
            else bought=BuyStationUpgrade();
            RefreshShop();return bought;
        }
        void PageShopEquipment(int step)
        {
            var page=ShopPage;
            SelectEquipment((int)SelectedEquipment+step);
            if(page==EmberdeepShopPage.Tuning)ShowShopPage(page);
        }
        void BuildShop()
        {
            shop=new GameObject("Equipment shop").transform;shop.SetParent(hud,false);
            Shape("Shop panel",PrimitiveType.Cube,shop,new Vector3(0,-.2f,.02f),new Vector3(2.85f,1.43f,.035f),PanelMaterial);
            Text(shop,"F I R E H O U S E   /   G E A R",new Vector3(0,.40f,-.025f),.042f,new Color(1,.75f,.35f));
            // One portrait window sits beside the item facts, clear of every ray
            // target. No display mesh has a collider or owns hand input.
            Shape("Preview recess",PrimitiveType.Cube,shop,new Vector3(-.91f,.015f,-.015f),new Vector3(.9f,.68f,.02f),PanelMaterial);
            var frame=new GameObject("Shop display frame").transform;frame.SetParent(shop,false);frame.localPosition=new Vector3(-.91f,.015f,-.34f);
            ShopPreview=frame.gameObject.AddComponent<EmberdeepShopPreview>();
            shopPreviewNote=Text(shop,"DISPLAY ONLY",new Vector3(-.91f,-.29f,-.03f),.022f,new Color(.55f,.84f,.86f));
            shopPageText=Text(shop,"",new Vector3(.4f,.29f,-.03f),.026f,new Color(.45f,.84f,.89f));
            shopName=Text(shop,"",new Vector3(.4f,.19f,-.03f),.039f,Color.white);
            shopDescription=Text(shop,"",new Vector3(.4f,.055f,-.03f),.026f,new Color(.72f,.9f,.94f));
            shopStats=Text(shop,"",new Vector3(.4f,-.10f,-.03f),.027f,Color.white);
            shopState=Text(shop,"",new Vector3(.4f,-.30f,-.03f),.027f,new Color(1,.8f,.4f));
            shopPrevious=Button("Previous gear",new Vector3(-.88f,-.42f,0),()=>PageShopEquipment(-1),out var previous,shop);previous.text="< PREVIOUS";
            shopBuy=Button("Buy gear",new Vector3(0,-.42f,0),()=>BuyShopSelection(),out shopBuyText,shop);
            shopNext=Button("Next gear",new Vector3(.88f,-.42f,0),()=>PageShopEquipment(1),out var next,shop);next.text="NEXT >";
            shopUpgrade=Button("Tune gear",new Vector3(-.88f,-.61f,0),()=>ShowShopPage(ShopPage==EmberdeepShopPage.Tuning?EmberdeepShopPage.Equipment:EmberdeepShopPage.Tuning),out shopUpgradeText,shop);
            stationUpgrade=Button("Upgrade water station",new Vector3(0,-.61f,0),()=>ShowShopPage(ShopPage==EmberdeepShopPage.Station?EmberdeepShopPage.Equipment:EmberdeepShopPage.Station),out stationUpgradeText,shop);
            Button("Leave shop",new Vector3(.88f,-.61f,0),CloseShop,out var back,shop);back.text="BACK / PICK UP";
            shopCompare=Button("Compare station",new Vector3(0,-.80f,0),()=>ShowStationComparison(!ShopShowingCurrentStation),out shopCompareText,shop);
            shopCompare.transform.localScale=new Vector3(1.52f,.14f,.055f);
            shop.gameObject.SetActive(false);
        }
        void RefreshShop()
        {
            if(!shopName)return;
            var item=FireEquipment.Get(SelectedEquipment);bool owned=Run.Owns(SelectedEquipment);
            int level=Run.UpgradeFor(SelectedEquipment);
            int ownedCount=0;for(int n=0;n<4;n++)if(Run.Owns((FireEquipmentId)n))ownedCount++;
            rackControls.gameObject.SetActive(!ShopOpen && ownedCount>1);previousOwned.Interactable=nextOwned.Interactable=ownedCount>1;
            shopCompare.gameObject.SetActive(ShopPage==EmberdeepShopPage.Station);shopCompareText.gameObject.SetActive(ShopPage==EmberdeepShopPage.Station);
            shopPrevious.Interactable=shopNext.Interactable=ShopPage!=EmberdeepShopPage.Station;
            shopUpgrade.Interactable=stationUpgrade.Interactable=true;
            shopUpgradeText.text=ShopPage==EmberdeepShopPage.Tuning?"< TOOLS":"VIEW TUNING";
            stationUpgradeText.text=ShopPage==EmberdeepShopPage.Station?"< TOOLS":"VIEW WATER";
            if(ShopPage==EmberdeepShopPage.Station)
            {
                var current=Run.StationTier;var target=current==WaterStationTier.PushButton?current:(WaterStationTier)((int)current+1);
                ShopItemId="station:"+target;ShopPrice=Run.StationUpgradeCost;
                ShopCanBuy=Run.CanUpgradeStation && !ShopShowingCurrentStation;
                var shown=ShopShowingCurrentStation?current:target;
                shopPageText.text=ShopShowingCurrentStation?"CURRENT STATION / COMPARISON":"NEXT WATER STATION";
                shopName.text=StationName(shown);
                shopDescription.text=StationAction(shown);
                shopStats.text=current==target?"ALL STATION TIERS OWNED":StationName(current)+"\n  >  "+StationName(target);
                shopState.text=$"CASH $ {Run.Coins}  |  "+(ShopPrice==0?"FULLY UPGRADED":$"PRICE $ {ShopPrice}");
                shopBuyText.text=ShopShowingCurrentStation?"CURRENT":ShopPrice==0?"MAX TIER":$"BUY $ {ShopPrice}";
                shopCompareText.text=ShopShowingCurrentStation?"SHOW RESULT >":"< COMPARE CURRENT";
                shopCompare.Interactable=current!=target;
                ShopPreview.ShowStation(ShopItemId+(ShopShowingCurrentStation?"/current":""),shown,FoliageMaterial,PanelMaterial,WaterMaterial);
            }
            else
            {
                bool tuning=ShopPage==EmberdeepShopPage.Tuning;
                ShopItemId=tuning?$"tune:{SelectedEquipment}:{level+1}":"tool:"+SelectedEquipment;
                ShopPrice=tuning?Run.UpgradeCostFor(SelectedEquipment):item.Price;
                ShopCanBuy=tuning?owned && Run.InSupply && !Run.Supply.Locked && ShopPrice>0 && Run.Coins>=ShopPrice:Run.CanBuy(SelectedEquipment);
                shopPageText.text=tuning?"NUMERIC TUNING / SAME MODEL":$"TOOL {(int)SelectedEquipment+1} / 4";
                shopName.text=item.Name;
                shopDescription.text=tuning?"No new mesh / same firing mode":ShortControls(SelectedEquipment);
                shopStats.text=tuning?TuningComparison(item,level,ShopPrice>0):$"TANK {Run.CapacityFor(SelectedEquipment):0}  |  REACH {(item.Range+level*2.5f)*.08f:0.00}m\nTUNING +{level}  |  {DeliveryName(item.Delivery)}";
                string state=tuning?(!owned?"OWN TOOL FIRST":ShopPrice==0?"MAX TUNING":$"PRICE $ {ShopPrice}"):
                    owned?"OWNED":Run.ClearedIncidents<item.Unlock?$"CLEAR INCIDENT {item.Unlock}":$"PRICE $ {item.Price}";
                shopState.text=$"CASH $ {Run.Coins}  |  {state}";
                shopBuyText.text=tuning?(ShopPrice==0?"MAX TUNING":$"TUNE $ {ShopPrice}"):owned?"OWNED":$"BUY $ {ShopPrice}";
                ShopPreview.ShowTool(ShopItemId,SelectedEquipment,FoliageMaterial,PanelMaterial,WaterMaterial);
            }
            shopBuy.Interactable=ShopCanBuy;
            shopPreviewNote.text=ShopPreview.IsFallback?"PREVIEW UNAVAILABLE":"DISPLAY ONLY";
            if(ShopPreview.IsFallback)shopDescription.text="Preview unavailable\n"+shopDescription.text;
            FitShopText(shopName,1.72f,.039f);FitShopText(shopDescription,1.72f,.026f);FitShopText(shopStats,1.75f,.027f);FitShopText(shopState,1.75f,.027f);
            if(rackLabel){rackLabel.gameObject.SetActive(!grip.Held && !ShopOpen);rackLabel.text=owned?"PINCH GRIP TO TAKE":"SELECT IN SHOP";rackLabel.transform.localPosition=new Vector3(0,-.27f,-.34f);if(Player)rackLabel.transform.rotation=Player.ViewCamera.transform.rotation;}
        }
        static string StationName(WaterStationTier tier)=>tier==WaterStationTier.LeverWell?"LEVER WELL":tier==WaterStationTier.RotaryHydrant?"ROTARY HYDRANT":"PUSH BUTTON";
        static string StationAction(WaterStationTier tier)=>tier==WaterStationTier.LeverWell?"Pinch lever: down + return\n12 water per complete stroke":tier==WaterStationTier.RotaryHydrant?"Turn the gold wheel 45 degrees\nContinuous fill; turn back to stop":"Press the gold button to fill\nPress again to stop";
        static string DeliveryName(FireDelivery delivery)=>delivery==FireDelivery.Foam?"FOAM SWEEP":delivery==FireDelivery.Pulse?"PULSE BURSTS":delivery==FireDelivery.Fan?"WIDE FAN":"CHARGED SPLASH";
        static string ShortControls(FireEquipmentId id)=>id==FireEquipmentId.Extinguisher?"Hold pinch: short foam sweep\nSlows fire spread":id==FireEquipmentId.PulsePistol?"Tap: accurate water pulse\nHold: repeat bursts":id==FireEquipmentId.RainFan?"Hold: sweep five water streams\nWide cover; uses more water":"Hold: charge / release: splash\nOffhand pump adds charge";
        static string TuningComparison(FireEquipment item,int level,bool more)
        {
            int next=more?level+1:level;
            return $"TANK {item.Capacity+level*25:0} > {item.Capacity+next*25:0}   POWER {item.Power+level*27:0} > {item.Power+next*27:0}\n"
                +$"REACH {(item.Range+level*2.5f)*.08f:0.00} > {(item.Range+next*2.5f)*.08f:0.00}m\n"
                +$"WIDTH {(item.Width+level*.16f)*.08f:0.000} > {(item.Width+next*.16f)*.08f:0.000}m\n"
                +$"SPEED {item.Speed+level*.2f:0.0} > {item.Speed+next*.2f:0.0}m/s";
        }
        static void FitShopText(TextMesh label,float maxWidth,float size)
        {
            label.characterSize=size*20/64;
            float width=label.GetComponent<Renderer>().localBounds.size.x;
            if(width>maxWidth)label.characterSize*=maxWidth/width;
        }
    }
}
