using UnityEngine;

namespace FireRescue
{
    // A fingertip poke on a small physical target (the rim's pause token, LoopSpec §11b). One poke is an index
    // tip crossing down onto the face inside its radius. The hand must leave RearmDistance before it can poke
    // again, a fist never pokes (the bell and pump are struck with fists nearby), and lost tracking disarms.
    // Headset test 2026-11-22 (poke failed, pinch worked):
    //  - A fist is the four-finger closure AND a folded index. A pointing hand folds the other three fingers, so its
    //    closure alone reads ~.6-.75 and was taken for a fist; a pinch keeps them open, which is why only pinch worked.
    //  - The crossing is swept: hand tracking updates slower than the frame and a quick tip can step from above the
    //    face to well under it in one sample. Any step that crosses the contact plane counts, down to PressDepth under
    //    the face, and the lateral test uses the point where the step meets the plane.
    //  - A wider zone (Radius) and a larger coin (EmberdeepRimHud.TokenDiameter 4.4 cm).
    public sealed class EmberdeepFingerPoke
    {
        public const float Radius=.032f;        // m, lateral reach around the centre (the token is 4.4 cm across)
        public const float ContactHeight=.008f; // m above the face where the tip joint meets it (finger pad)
        public const float ApproachHeight=.07f; // m above the face: a tip higher than this is not approaching
        public const float PressDepth=.05f;     // m under the face: a step that ends deeper is a reach past, not a poke
        public const float RearmDistance=.055f; // m from the centre before the next poke
        public const float FistClosure=.55f;    // four-finger closure at or above, with a folded index: a fist
        public const float FoldedIndex=.55f;    // index curl at or above: folded (a poking index reads ~0-.35)
        public struct Tip
        {
            public bool Valid;public Vector3 Point;public float Closure,IndexCurl;
            // indexCurl NaN: unknown, the four-finger closure decides alone.
            public Tip(bool valid,Vector3 point,float closure,float indexCurl=float.NaN){Valid=valid;Point=point;Closure=closure;IndexCurl=indexCurl;}
            public bool Usable=>Valid && float.IsFinite(Point.x) && float.IsFinite(Point.y) && float.IsFinite(Point.z) && float.IsFinite(Closure);
            public bool Fist=>Closure>=FistClosure && (float.IsNaN(IndexCurl) || IndexCurl>=FoldedIndex);
        }
        readonly bool[] armed=new bool[2],tracked=new bool[2];
        readonly float[] height=new float[2];
        readonly Vector3[] last=new Vector3[2];
        public int PokeHand {get;private set;}=-1;
        public bool Poked=>PokeHand>=0;
        // A tip over the face and near enough to poke: the token can lift to show it is live.
        public bool Hovering {get;private set;}
        public bool Armed(int hand)=>hand>=0 && hand<2 && armed[hand];
        public void Reset(){for(int i=0;i<2;i++){armed[i]=tracked[i]=false;height[i]=0;}PokeHand=-1;Hovering=false;}
        public bool Sample(Tip left,Tip right,Vector3 center,Vector3 normal)
        {
            PokeHand=-1;Hovering=false;
            if(!float.IsFinite(center.sqrMagnitude) || !float.IsFinite(normal.sqrMagnitude) || normal.sqrMagnitude<.5f){Reset();return false;}
            normal.Normalize();
            for(int i=0;i<2;i++)
            {
                var tip=i==0?left:right;
                if(!tip.Usable){armed[i]=tracked[i]=false;continue;}
                var offset=tip.Point-center;float h=Vector3.Dot(offset,normal);float lateral=(offset-normal*h).magnitude;
                // Arming needs a real exit: a tip that first appears inside the token (tracking pop) is not armed.
                if(offset.magnitude>=RearmDistance)armed[i]=true;
                bool fist=tip.Fist;
                bool crossed=tracked[i] && height[i]>ContactHeight && h<=ContactHeight && h>-PressDepth;
                if(crossed)
                {
                    // Where this step met the contact plane.
                    float t=Mathf.Clamp01((height[i]-ContactHeight)/Mathf.Max(height[i]-h,1e-5f));
                    var hit=Vector3.Lerp(last[i],tip.Point,t)-center;lateral=(hit-normal*Vector3.Dot(hit,normal)).magnitude;
                }
                if(PokeHand<0 && armed[i] && !fist && lateral<=Radius && crossed){PokeHand=i;armed[i]=false;}
                Hovering|=armed[i] && !fist && lateral<=Radius*1.5f && h>ContactHeight && h<ApproachHeight;
                height[i]=h;last[i]=tip.Point;tracked[i]=true;
            }
            return PokeHand>=0;
        }
    }
}
