using UnityEngine;

namespace FireRescue
{
    // Display geometry only: gameplay builders are used in an inactive staging
    // object, then only transforms, shared meshes/materials and tint are copied.
    public sealed class EmberdeepShopPreview : MonoBehaviour
    {
        public string ItemId { get; private set; }
        public Transform PreviewRoot { get; private set; }
        public bool IsFallback { get; private set; }
        public bool ForceFallback { get; set; }
        public Bounds ModelBounds { get; private set; }
        public WaterStationTier? DisplayedStationTier { get; private set; }
        public FireEquipmentId? DisplayedEquipment { get; private set; }
        public int Generation { get; private set; }
        public static readonly Vector3 FrameSize=new Vector3(.79f,.61f,.60f);
        string key;
        MaterialPropertyBlock properties;
        readonly System.Collections.Generic.List<Object> ownedDetails=new();
        void ReleaseDetails(){foreach(var item in ownedDetails)if(item)Dispose(item);ownedDetails.Clear();}
        void OnDestroy()=>ReleaseDetails();

        public void ShowTool(string itemId,FireEquipmentId id,Material body,Material dark,Material accent)
            =>Show(itemId,id,null,body,dark,accent);
        public void ShowStation(string itemId,WaterStationTier tier,Material body,Material dark,Material accent)
            =>Show(itemId,null,tier,body,dark,accent);

        public void ShowMagic(string itemId)
        {
            if(key==itemId && PreviewRoot)return;
            ReleaseDetails();
            key=ItemId=itemId;Generation++;DisplayedEquipment=null;DisplayedStationTier=null;IsFallback=false;
            if(PreviewRoot){PreviewRoot.gameObject.SetActive(false);Dispose(PreviewRoot.gameObject);}
            PreviewRoot=new GameObject("PreviewPivot / "+itemId).transform;PreviewRoot.SetParent(transform,false);
            var demo=PreviewRoot.gameObject.AddComponent<EmberdeepInteractionDemo>();demo.Show(EmberdeepInteractionDemo.Action.Goddess);
            demo.SetCaptionVisible(false);
            PreviewRoot.localScale=Vector3.one*1.6f;
        }
        // A spell has no tool to display: the preview shows its crystal, charged and in its own
        // colour, beside the looping gesture demonstration.
        public void ShowSpell(string itemId,FireEquipmentId id,Color tint)
        {
            string next="spell/"+itemId;
            if(key==next && PreviewRoot)return;
            ReleaseDetails();
            key=next;ItemId=itemId;Generation++;DisplayedEquipment=id;DisplayedStationTier=null;IsFallback=false;
            if(PreviewRoot){PreviewRoot.gameObject.SetActive(false);Dispose(PreviewRoot.gameObject);}
            PreviewRoot=new GameObject("PreviewPivot / "+itemId).transform;PreviewRoot.SetParent(transform,false);
            var lesson=new GameObject("Gesture demonstration").transform;lesson.SetParent(PreviewRoot,false);
            lesson.localPosition=new Vector3(-.06f,-.035f,0);lesson.localScale=Vector3.one*1.55f;
            var demo=lesson.gameObject.AddComponent<EmberdeepInteractionDemo>();
            demo.Show(id==FireEquipmentId.PulsePistol?EmberdeepInteractionDemo.Action.Bolt:id==FireEquipmentId.RainFan?EmberdeepInteractionDemo.Action.Fan:
                id==FireEquipmentId.PressureCannon?EmberdeepInteractionDemo.Action.Orb:EmberdeepInteractionDemo.Action.Goddess);
            demo.SetCaptionVisible(false);
            var gem=new GameObject("Spell crystal").AddComponent<EmberdeepManaCrystal>();gem.transform.SetParent(PreviewRoot,false);
            // The setting's face looks out of the panel; the stone stands upright.
            gem.transform.localPosition=new Vector3(.175f,-.075f,-.03f);
            gem.transform.localRotation=Quaternion.Euler(0,0,-14)*Quaternion.LookRotation(Vector3.up,Vector3.forward);
            gem.transform.localScale=Vector3.one*1.9f;
            if(id!=FireEquipmentId.Extinguisher)gem.SetTint(tint);
            gem.SetFill(1);
            PreviewRoot.localScale=Vector3.one*1.6f;
            ModelBounds=BoundsIn(transform);
        }
        void Show(string itemId,FireEquipmentId? equipment,WaterStationTier? station,Material body,Material dark,Material accent)
        {
            properties??=new MaterialPropertyBlock();
            string next=itemId+"/"+equipment+"/"+station+"/"+ForceFallback;
            if(key==next && PreviewRoot)return;
            ReleaseDetails();
            key=next;ItemId=itemId;DisplayedEquipment=equipment;DisplayedStationTier=station;Generation++;
            if(PreviewRoot){PreviewRoot.gameObject.SetActive(false);Dispose(PreviewRoot.gameObject);}
            PreviewRoot=new GameObject("PreviewPivot / "+itemId).transform;PreviewRoot.SetParent(transform,false);
            var geometry=new GameObject("Display geometry").transform;geometry.SetParent(PreviewRoot,false);
            GameObject staging=null;
            IsFallback=ForceFallback;
            try
            {
                if(!ForceFallback)
                {
                    staging=new GameObject("Shop preview staging"){hideFlags=HideFlags.HideAndDontSave};
                    if(equipment.HasValue)
                    {
                        // Awake initializes this builder's property block before
                        // it becomes inactive. Build has no ticking behavior.
                        var view=staging.AddComponent<FireEquipmentView>();staging.SetActive(false);
                        view.Build(equipment.Value,body,dark,accent);
                    }
                    else
                    {
                        var view=staging.AddComponent<EmberdeepWaterStationView>();staging.SetActive(false);
                        view.Build(body,dark,accent,false);view.SetTier(station.Value);view.StopOutlet();
                    }
                    CopyChildren(staging.transform,geometry);
                    IsFallback=geometry.GetComponentsInChildren<MeshRenderer>(true).Length==0;
                }
            }
            catch(System.Exception)
            {
                // Missing preview content is recoverable; preserve item name,
                // price and confirmation UI and use an explicit neutral model.
                IsFallback=true;
            }
            finally{if(staging){staging.SetActive(false);Dispose(staging);}}
            if(IsFallback)
            {
                geometry.gameObject.SetActive(false);Dispose(geometry.gameObject);
                geometry=new GameObject("Preview unavailable / "+itemId).transform;geometry.SetParent(PreviewRoot,false);
                var shape=GameObject.CreatePrimitive(PrimitiveType.Capsule);shape.SetActive(false);
                var filter=geometry.gameObject.AddComponent<MeshFilter>();filter.sharedMesh=shape.GetComponent<MeshFilter>().sharedMesh;
                var renderer=geometry.gameObject.AddComponent<MeshRenderer>();renderer.sharedMaterial=body?body:dark;
                properties.Clear();properties.SetColor("_BaseColor",new Color(.58f,.65f,.66f));renderer.SetPropertyBlock(properties);
                geometry.localScale=new Vector3(.12f,.1f,.12f);Dispose(shape);
            }
            geometry.localRotation=station.HasValue?Quaternion.Euler(-12,28,0):Quaternion.Euler(-12,145,-8);
            if(station.HasValue && !IsFallback)Highlight(geometry,station==WaterStationTier.RotaryHydrant?"Valve":"Button");
            Fit(PreviewRoot);
        }

        void CopyChildren(Transform source,Transform target)
        {
            foreach(Transform child in source)
            {
                if(!child.gameObject.activeSelf)continue;
                // Instruction text/plates belong to the real station. Preview
                // instructions are legible panel text, not scaled world labels.
                string lower=child.name.ToLowerInvariant();
                if(lower.Contains("tutorial") || lower.Contains("instruction"))continue;
                var copy=new GameObject(child.name).transform;copy.SetParent(target,false);
                copy.localPosition=child.localPosition;copy.localRotation=child.localRotation;copy.localScale=child.localScale;
                var mesh=child.GetComponent<MeshFilter>();var original=child.GetComponent<MeshRenderer>();
                if(mesh && mesh.sharedMesh && original && original.enabled)
                {
                    var displayMesh=mesh.sharedMesh;var displayMaterials=original.sharedMaterials;
                    // Staging destroys its generated details after this frame. The
                    // passive preview owns independent copies, with no behavior component.
                    if(child.GetComponent<EmberdeepCraftDetails>())
                    {
                        displayMesh=Instantiate(displayMesh);ownedDetails.Add(displayMesh);
                        for(int i=0;i<displayMaterials.Length;i++)
                        {displayMaterials[i]=Instantiate(displayMaterials[i]);ownedDetails.Add(displayMaterials[i]);}
                    }
                    copy.gameObject.AddComponent<MeshFilter>().sharedMesh=displayMesh;
                    var renderer=copy.gameObject.AddComponent<MeshRenderer>();renderer.sharedMaterials=displayMaterials;
                    original.GetPropertyBlock(properties);renderer.SetPropertyBlock(properties);
                    renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
                }
                CopyChildren(child,copy);
            }
        }
        void Highlight(Transform root,string anchor)
        {
            foreach(var item in root.GetComponentsInChildren<Transform>(true))
            {
                if(item.name!=anchor)continue;
                foreach(var renderer in item.GetComponentsInChildren<MeshRenderer>(true))
                {
                    renderer.GetPropertyBlock(properties);properties.SetColor("_BaseColor",new Color(1,.73f,.25f));renderer.SetPropertyBlock(properties);
                }
            }
        }
        void Fit(Transform root)
        {
            var bounds=BoundsIn(root);float factor=Mathf.Min(FrameSize.x/Mathf.Max(.001f,bounds.size.x),
                FrameSize.y/Mathf.Max(.001f,bounds.size.y),FrameSize.z/Mathf.Max(.001f,bounds.size.z));
            root.localScale=Vector3.one*factor;root.localPosition=-bounds.center*factor;
            // Report final bounds relative to the fixed preview frame, useful to
            // both validation and authored replacement models.
            ModelBounds=BoundsIn(transform);
        }
        static Bounds BoundsIn(Transform space)
        {
            var bounds=new Bounds();bool first=true;
            foreach(var filter in space.GetComponentsInChildren<MeshFilter>(true))
            {
                if(!filter.sharedMesh || !LocallyActive(filter.transform,space))continue;
                var b=filter.sharedMesh.bounds;var matrix=space.worldToLocalMatrix*filter.transform.localToWorldMatrix;
                for(int corner=0;corner<8;corner++)
                {
                    var point=matrix.MultiplyPoint3x4(b.center+Vector3.Scale(b.extents,new Vector3((corner&1)==0?-1:1,(corner&2)==0?-1:1,(corner&4)==0?-1:1)));
                    if(first){bounds=new Bounds(point,Vector3.zero);first=false;}else bounds.Encapsulate(point);
                }
            }
            return bounds;
        }
        static bool LocallyActive(Transform item,Transform space)
        {
            for(var at=item;at && at!=space;at=at.parent)if(!at.gameObject.activeSelf)return false;
            return true;
        }
        static void Dispose(Object item){if(Application.isPlaying)Destroy(item);else DestroyImmediate(item);}
    }
}
