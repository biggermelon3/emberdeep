using System;
using UnityEngine;

namespace PhysicalAssembly
{
    [SelectionBase]
    public sealed class AssemblyPart : MonoBehaviour
    {
        public string InstanceId = "";
        public PartKind Kind;
        public int GridVersion=1;
        public bool BuildingCore;
        [HideInInspector] public float GeometryScale=1f;
        [HideInInspector] public float DesignWheelRadius;
        public AssemblyBody MainBody;
        public AssemblyBody[] Bodies;
        public AssemblySocket[] Sockets;
        public Renderer[] VisualRenderers { get; private set; }
        public MeshFilter[] VisualMeshes { get; private set; }
        [NonSerialized] public AssemblyWorld World;
        public void Initialize(AssemblyWorld world)
        {
            if (string.IsNullOrEmpty(InstanceId)) InstanceId = Guid.NewGuid().ToString("N");
            Bodies = GetComponentsInChildren<AssemblyBody>();
            Sockets = GetComponentsInChildren<AssemblySocket>();
            foreach (var body in Bodies) { body.Part = this; body.Initialize(); }
            if (!MainBody && Bodies.Length > 0) MainBody = Bodies[0];
            VisualRenderers=GetComponentsInChildren<Renderer>(); VisualMeshes=GetComponentsInChildren<MeshFilter>();
            World = world; world.RegisterPart(this);
        }
        public AssemblyBody FindBody(string id) => Array.Find(Bodies, b => b && b.Id == id);
        public AssemblySocket FindSocket(string id) => Array.Find(Sockets, s => s && s.Id == id);
        void OnDestroy() { if (World) World.UnregisterPart(this); }
    }
}
