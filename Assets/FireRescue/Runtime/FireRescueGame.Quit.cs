using UnityEngine;

namespace FireRescue
{
    public sealed partial class FireRescueGame
    {
        Transform quitPanel;
        public bool QuitConfirmationOpen {get;private set;}
        public bool QuitRequested {get;private set;}
        void BuildQuitPanel()
        {
            Button("Quit app",new Vector3(0,-.82f,0),RequestQuit,out var quit);quit.text="QUIT APP";
            quitPanel=new GameObject("Quit confirmation modal").transform;quitPanel.SetParent(hud,false);
            quitPanel.localPosition=Vector3.back*.08f;
            Shape("Quit confirmation backdrop",PrimitiveType.Cube,quitPanel,Vector3.zero,new Vector3(2.95f,1.2f,.05f),PanelMaterial);
            Text(quitPanel,"Quit Emberdeep?",new Vector3(0,.30f,-.04f),.065f,Color.white);
            Text(quitPanel,"This showcase does not save after closing.",new Vector3(0,.10f,-.04f),.035f,new Color(.7f,.85f,.9f));
            Button("Cancel quit",new Vector3(-.52f,-.25f,-.04f),CancelQuit,out var cancel,quitPanel);cancel.text="CANCEL";
            Button("Confirm quit",new Vector3(.52f,-.25f,-.04f),ConfirmQuit,out var confirm,quitPanel);confirm.text="YES, QUIT";
            quitPanel.gameObject.SetActive(false);
        }
        public void RequestQuit()
        {
            if(QuitRequested || QuitConfirmationOpen)return;
            QuitConfirmationOpen=true;ResetInput(true,true);toolManipulation.Park();ClearInterfaceReservation();
            dashboard.gameObject.SetActive(false);shop.gameObject.SetActive(false);quitPanel.gameObject.SetActive(true);
        }
        public void CancelQuit()
        {
            if(!QuitConfirmationOpen || QuitRequested)return;
            QuitConfirmationOpen=false;quitPanel.gameObject.SetActive(false);
            dashboard.gameObject.SetActive(!ShopOpen);shop.gameObject.SetActive(ShopOpen);handMenu?.ResetInput();ClearInterfaceReservation();
        }
        public void ConfirmQuit()
        {
            if(!QuitConfirmationOpen || QuitRequested)return;
            QuitRequested=true;ResetInput();Application.Quit();
        }
    }
}
