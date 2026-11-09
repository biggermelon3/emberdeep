using System.Collections.Generic;
using UnityEngine;
namespace PhysicalAssembly {
 public sealed class SandboxGrabOutline:MonoBehaviour {
  public WorkshopBootstrap Workshop;
  sealed class Entry {public readonly List<Renderer> renderers=new();public bool held;public float release=-1;}
  readonly Dictionary<AssemblyPart,Entry> entries=new();readonly HashSet<AssemblyPart> held=new();Material material;MaterialPropertyBlock properties;
  void Awake(){properties=new MaterialPropertyBlock();}
  void LateUpdate(){
   if(Workshop && Workshop.Downhill && Workshop.Downhill.Course.SandboxPolish) {
    var polish=GetComponent<SandboxPartHighlights>(); if(!polish) polish=gameObject.AddComponent<SandboxPartHighlights>(); polish.Level=Workshop.Downhill; return;
   }
   if(!Workshop || !Workshop.Downhill || !Workshop.Downhill.IsBuilding){Clear();return;}
   held.Clear();for(int h=0;h<3;h++)foreach(var p in Workshop.World.HeldParts(h))held.Add(p);
   foreach(var p in held)if(!entries.ContainsKey(p)){
    if(!material)material=new Material(Resources.Load<Shader>("GrabOutline"));
    var e=new Entry();entries.Add(p,e);
    foreach(var renderer in p.VisualRenderers){var f=renderer.GetComponent<MeshFilter>();if(!f || !f.sharedMesh)continue;
     var go=new GameObject("Grab outline");go.transform.SetParent(renderer.transform,false);go.AddComponent<MeshFilter>().sharedMesh=f.sharedMesh;
     var visual=go.AddComponent<MeshRenderer>();var mats=new Material[f.sharedMesh.subMeshCount];for(int i=0;i<mats.Length;i++)mats[i]=material;visual.sharedMaterials=mats;visual.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;e.renderers.Add(visual);
    }
   }
   foreach(var pair in entries){var e=pair.Value;bool active=pair.Key && held.Contains(pair.Key);if(e.held&&!active)e.release=Time.unscaledTime;e.held=active;
    float t=active?0:Time.unscaledTime-e.release;var c=Color.Lerp(new Color(1,.4f,0),Color.green,Mathf.Clamp01(t/.12f));c.a=active?1:1-Mathf.Clamp01((t-.12f)/.25f);
    properties.SetColor("_Color",c);properties.SetFloat("_Width",.002f);
    foreach(var visual in e.renderers)if(visual){visual.enabled=active || e.release>=0&&t<.37f;visual.SetPropertyBlock(properties);}
   }
  }
  void Clear(){foreach(var e in entries.Values)foreach(var v in e.renderers)if(v)Destroy(v.gameObject);entries.Clear();}
  void OnDisable(){Clear();}void OnDestroy(){if(material)Destroy(material);}
 }
}
