using UnityEngine;
namespace PhysicalAssembly
{
    public sealed class ToyTarget : MonoBehaviour
    {
        public WorkshopSession Session;
        float restore; Color original;
        void Start() { original=GetComponent<Renderer>().material.color; }
        public void Hit() { Session.AddScore(); GetComponent<Renderer>().material.color=Color.white; restore=Time.time+.15f; }
        void Update() { if(restore>0 && Time.time>restore) { GetComponent<Renderer>().material.color=original; restore=0; } }
    }
}
