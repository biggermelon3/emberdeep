using UnityEngine;

namespace FireRescue
{
    public sealed partial class FireRescueGame
    {
        float shopLessonWait;
        bool shopLessonPresented;
        Transform shopLessonRing;
        void TickShopLesson(float dt)
        {
            if(!Run.NeedsMagicUpgrade || shopLessonPresented || ShopOpen)return;
            shopLessonWait+=dt;
            if(shopLessonWait<1.8f)return;
            shopLessonPresented=true;OpenShop();
        }
        void RefreshShopLesson()
        {
            if(!shopBuy)return;
            bool guide=Run.NeedsMagicUpgrade && ShopOpen;
            if(!shopLessonRing)
            {
                shopLessonRing=new GameObject("First upgrade guide").transform;shopLessonRing.SetParent(shop,false);
                var text=Text(shopLessonRing,"1. BUY YOUR FIRST MAGIC UPGRADE",new Vector3(0,-.96f,-.04f),.034f,new Color(1,.83f,.43f));
                var fit=text.gameObject.AddComponent<EmberdeepTextBounds>();fit.Width=2.6f;fit.Height=.07f;
            }
            shopLessonRing.gameObject.SetActive(guide);
            if(guide)
            {
                shopPageText.text="FIRST UPGRADE / MORE PALM WATER";
                shopBuyText.text=$"UPGRADE $ {ShopPrice}";
                shopDescription.text="More water for your next rescue.\nAim at the gold button and pinch.";
            }
            var theme=shopBuy.GetComponent<EmberdeepForestUi>();if(theme)theme.Guided=guide;
        }
    }
}
