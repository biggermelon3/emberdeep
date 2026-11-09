using System.Collections.Generic;
using UnityEngine;
namespace PhysicalAssembly
{
    public sealed class VehicleWeapon : MonoBehaviour,IAssemblyControlReceiver
    {
        public Transform Muzzle;
        public float ShotsPerSecond=5,ProjectileSpeed=4;
        float nextShot;
        readonly List<ToyProjectile> pool=new();
        public void ApplyControl(in AssemblyControlFrame frame,float dt)
        {
            if(!frame.fireHeld || !frame.controller || Time.time<nextShot) return;
            nextShot=Time.time+1/ShotsPerSecond;
            ToyProjectile shot=null; foreach(var p in pool) if(!p.gameObject.activeSelf) { shot=p; break; }
            if(!shot)
            {
                if(pool.Count>=12) return;
                var go=GameObject.CreatePrimitive(PrimitiveType.Sphere); go.name="Toy projectile"; go.transform.localScale=Vector3.one*.025f;
                go.GetComponent<Renderer>().sharedMaterial=PartFactory.Material(new Color(1,.7f,.12f));
                var rb=go.AddComponent<Rigidbody>(); rb.mass=.015f; rb.useGravity=false; rb.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
                shot=go.AddComponent<ToyProjectile>(); pool.Add(shot); go.SetActive(false);
            }
            shot.Launch(Muzzle.position,Muzzle.forward*ProjectileSpeed,frame.controller.World,frame.controller.InstanceId);
        }
        void OnDestroy() { foreach(var p in pool) if(p) Destroy(p.gameObject); }
    }
}
