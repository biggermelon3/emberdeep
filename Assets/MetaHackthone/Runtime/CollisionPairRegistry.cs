using System.Collections.Generic;
using UnityEngine;
namespace PhysicalAssembly
{
    public sealed class CollisionPairRegistry
    {
        sealed class Claim { public Collider a, b; public readonly HashSet<string> owners = new(); }
        readonly Dictionary<(int, int), Claim> claims = new();
        public void Reapply()
        {
            foreach(var c in claims.Values) if(c.a && c.b && c.owners.Count>0) Physics.IgnoreCollision(c.a,c.b,true);
        }
        public void Acquire(Collider a, Collider b, string owner)
        {
            if (!a || !b || a == b) return;
            int x = a.GetInstanceID(), y = b.GetInstanceID(); var key = x < y ? (x,y) : (y,x);
            if (!claims.TryGetValue(key, out var c)) claims[key] = c = new Claim { a = a, b = b };
            c.owners.Add(owner); Physics.IgnoreCollision(a, b, true);
        }
        public void Release(string owner)
        {
            var dead = new List<(int,int)>();
            foreach (var entry in claims)
            {
                var c = entry.Value; c.owners.Remove(owner);
                if (c.owners.Count != 0 && c.a && c.b) continue;
                if (c.a && c.b) Physics.IgnoreCollision(c.a, c.b, false);
                dead.Add(entry.Key);
            }
            foreach (var key in dead) claims.Remove(key);
        }
    }
}
