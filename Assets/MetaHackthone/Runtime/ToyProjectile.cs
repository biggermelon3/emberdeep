using System.Collections.Generic;
using UnityEngine;
namespace PhysicalAssembly
{
    public sealed class ToyProjectile : MonoBehaviour
    {
        float expires; bool consumed; readonly List<Collider> ignored=new();
        public void Launch(Vector3 position,Vector3 velocity,AssemblyWorld world,string controller)
        {
            transform.position=position; gameObject.SetActive(true); consumed=false; expires=Time.time+2;
            var collider=GetComponent<Collider>();
            foreach(var part in world.GetConnectedParts(controller)) foreach(var body in part.Bodies) foreach(var other in body.Colliders)
            { Physics.IgnoreCollision(collider,other,true); ignored.Add(other); }
            GetComponent<Rigidbody>().linearVelocity=velocity;
        }
        void Update() { if(Time.time>=expires) Recycle(); }
        void OnCollisionEnter(Collision collision)
        {
            if(consumed) return; consumed=true;
            var target=collision.collider.GetComponent<ToyTarget>(); if(target) target.Hit(); Recycle();
        }
        void Recycle()
        {
            var collider=GetComponent<Collider>(); foreach(var other in ignored) if(other) Physics.IgnoreCollision(collider,other,false);
            ignored.Clear(); gameObject.SetActive(false);
        }
    }
}
