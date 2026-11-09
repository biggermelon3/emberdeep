using System.Collections.Generic;
using UnityEngine;

namespace PhysicalAssembly
{
    public sealed class ChallengeGestureFeedback : MonoBehaviour
    {
        IBuildModeTarget challenge;
        readonly Transform[,] rings = new Transform[2, 3];
        readonly ParticleSystem[,] sparks = new ParticleSystem[2, 3];
        readonly float[] seen = { -10, -10 };
        readonly AudioSource[] voices = new AudioSource[4];
        readonly Queue<int> notes = new();
        Material material; Mesh mesh; AudioClip piano;
        int owner = -1, voice;
        Fingers lit;
        float expansion = -1, nextNote, slowTime, fpsTime;
        int frames;
        bool wasArmed;
        public bool MeshOnly { get; private set; }
        public void Initialize(IBuildModeTarget controller)
        {
            challenge = controller; challenge.ModeGesture.Step += Step;
            piano = Resources.Load<AudioClip>("ChallengePiano");
            material = PartFactory.Material(new Color(.1f, 1f, .35f));
            mesh = RingMesh();
            for (int hand = 0; hand < 2; hand++) for (int f = 0; f < 3; f++)
            {
                var go = new GameObject($"Mode ring {hand} {f}"); go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh; go.AddComponent<MeshRenderer>().sharedMaterial = material;
                rings[hand, f] = go.transform;
                var fx = new GameObject("Ring sparkle"); fx.transform.SetParent(go.transform, false);
                var ps = fx.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = ps.main; main.playOnAwake = false; main.loop = true; main.startLifetime = .18f; main.startSpeed = .012f; main.startSize = .002f; main.maxParticles = 8; main.startColor = new Color(.2f, 1, .4f);
                var emission = ps.emission; emission.rateOverTime = 12;
                var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Circle; shape.radius = .011f;
                fx.GetComponent<ParticleSystemRenderer>().sharedMaterial = material; sparks[hand, f] = ps;
                go.SetActive(false);
            }
            for (int i = 0; i < voices.Length; i++) { voices[i] = gameObject.AddComponent<AudioSource>(); voices[i].playOnAwake = false; voices[i].spatialBlend = 0; voices[i].volume = .3f; }
        }
        void Step(int hand, int index)
        {
            owner = hand;
            bool closing = challenge.GestureDirection == ModeGestureDirection.Close;
            int cycle = closing ? challenge.BuildCyclesUsed : challenge.BuildCyclesUsed + 1;
            int root = 72 - 2 * (cycle - 1);
            int[] offsets = closing ? new[] { 7, 4, 0, -2 } : new[] { 0, 4, 7 };
            notes.Enqueue(root + offsets[Mathf.Min(index, offsets.Length - 1)]);
            lit = closing ? BuildModeGesture.Three & ~challenge.ModeGesture.Completed : challenge.ModeGesture.Completed;
            if (index == 0 && closing) expansion = Time.unscaledTime;
        }
        public void ModeChanged(bool building)
        {
            lit = challenge.GestureDirection == ModeGestureDirection.Open ? Fingers.None : BuildModeGesture.Three; expansion = Time.unscaledTime;
        }
        public void SetHandPose(int hand, Pose thumb, Pose index, Pose middle)
        {
            seen[hand] = Time.unscaledTime;
            if(!rings[hand,0]) return;
            rings[hand, 0].SetPositionAndRotation(thumb.position, thumb.rotation);
            rings[hand, 1].SetPositionAndRotation(index.position, index.rotation);
            rings[hand, 2].SetPositionAndRotation(middle.position, middle.rotation);
        }
        public void Clear() { owner = -1; lit = Fingers.None; notes.Clear(); expansion = -1; }
        void Update()
        {
            if (challenge==null || !rings[0,0]) return;
            bool armed = challenge.ModeGesture.Armed;
            if (wasArmed && !armed && expansion < Time.unscaledTime - .1f) lit = challenge.GestureDirection == ModeGestureDirection.Close ? BuildModeGesture.Three : Fingers.None;
            wasArmed = armed;
            float t = Time.unscaledTime - expansion;
            for (int h = 0; h < 2; h++) for (int f = 0; f < 3; f++)
            {
                bool visible = h == owner && Time.unscaledTime - seen[h] < .35f && ((lit & (Fingers)(1 << f)) != 0 || t >= 0 && t < .35f);
                rings[h, f].gameObject.SetActive(visible);
                rings[h, f].localScale = Vector3.one * (t >= 0 && t < .35f ? 1 + 1.5f * Mathf.Sin(t / .35f * Mathf.PI) : 1);
                if (visible && !MeshOnly) { if (!sparks[h, f].isPlaying) sparks[h, f].Play(); }
                else if (sparks[h, f].isPlaying) sparks[h, f].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            if (notes.Count > 0 && Time.unscaledTime >= nextNote)
            {
                var source = voices[voice++ % voices.Length]; source.pitch = Mathf.Pow(2, (notes.Dequeue() - 60) / 12f);
                if (piano) source.PlayOneShot(piano); nextNote = Time.unscaledTime + .10f;
            }
            ObserveFrame(Time.unscaledDeltaTime,owner>=0 && lit!=Fingers.None && Time.unscaledTime-seen[owner]<.35f);
        }
        public void ObserveFrame(float seconds,bool effectsActive)
        {
            if (!effectsActive || seconds<=0) { frames = 0; fpsTime = slowTime = 0; return; }
            fpsTime += seconds; frames++;
            if (fpsTime >= .5f)
            {
                slowTime = frames / fpsTime < 80 ? slowTime + fpsTime : 0;
                if (slowTime >= 2 && !MeshOnly) { MeshOnly = true; Debug.Log("NARROW_GAP_RING_MESH_FALLBACK"); }
                fpsTime = 0; frames = 0;
            }
        }
        static Mesh RingMesh()
        {
            const int count=24,tube=6; var v=new Vector3[count*tube]; var tris=new int[count*tube*6];
            for(int i=0;i<count;i++) for(int j=0;j<tube;j++)
            {
                float a=i*Mathf.PI*2/count,b=j*Mathf.PI*2/tube,r=.0105f+Mathf.Cos(b)*.0015f;
                int k=i*tube+j,n=((i+1)%count)*tube+j,q=i*tube+(j+1)%tube,s=((i+1)%count)*tube+(j+1)%tube;
                v[k]=new Vector3(Mathf.Cos(a)*r,Mathf.Sin(a)*r,Mathf.Sin(b)*.0015f);
                int t=k*6; tris[t]=k; tris[t+1]=n; tris[t+2]=q; tris[t+3]=n; tris[t+4]=s; tris[t+5]=q;
            }
            var mesh = new Mesh { name = "Mode finger ring" }; mesh.vertices = v; mesh.triangles = tris; mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }
        void OnDestroy()
        {
            if (challenge!=null) challenge.ModeGesture.Step -= Step;
            if (material) Destroy(material); if (mesh) Destroy(mesh);
        }
    }
}
