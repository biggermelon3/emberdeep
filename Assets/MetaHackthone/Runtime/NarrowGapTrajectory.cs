using System.Collections.Generic;
using UnityEngine;

namespace PhysicalAssembly
{
    public sealed partial class NarrowGapCourse
    {
        public Collider PadCollider;
        public LineRenderer Guide { get; private set; }
        Vector3 visualPosition, colliderPosition;
        Quaternion visualRotation, colliderRotation;
        bool aimVisualsReady;
        readonly Vector3[] trajectoryPoints=new Vector3[101];
        readonly float[] arcLengths=new float[101];
        readonly RaycastHit[] trajectoryHits=new RaycastHit[128];
        public void InitializeAimVisuals()
        {
            if(aimVisualsReady || !PadVisual || !Trajectory) return;
            if(!PadCollider) PadCollider=transform.Find("Jump pad collider").GetComponent<Collider>();
            visualPosition=PadVisual.localPosition; visualRotation=PadVisual.localRotation;
            colliderPosition=PadCollider.transform.localPosition; colliderRotation=PadCollider.transform.localRotation;
            Guide=Trajectory.GetComponent<LineRenderer>();
            if(!Guide) Guide=Trajectory.gameObject.AddComponent<LineRenderer>();
            Guide.useWorldSpace=true; Guide.widthMultiplier=.004f; Guide.positionCount=0;
            Guide.numCornerVertices=2; Guide.numCapVertices=2;
            if(!Guide.sharedMaterial) Guide.sharedMaterial=new Material(Shader.Find("PhysicalAssembly/Trajectory"));
            var cream=new Color(1,.90f,.66f);
            Guide.colorGradient=new Gradient { colorKeys=new[]{new GradientColorKey(cream,0),new GradientColorKey(cream,1)},
                alphaKeys=new[]{new GradientAlphaKey(1,0),new GradientAlphaKey(1,.5f),new GradientAlphaKey(0,1)} };
            aimVisualsReady=true; HideTrajectory();
        }
        public void RotatePad(Vector3 pivot,Quaternion rotation)
        {
            InitializeAimVisuals();
            PadVisual.SetPositionAndRotation(pivot+rotation*(PadVisual.parent.TransformPoint(visualPosition)-pivot),rotation*PadVisual.parent.rotation*visualRotation);
            PadCollider.transform.SetPositionAndRotation(pivot+rotation*(PadCollider.transform.parent.TransformPoint(colliderPosition)-pivot),rotation*PadCollider.transform.parent.rotation*colliderRotation);
        }
        public void RestorePad()
        {
            if(!aimVisualsReady) return;
            PadVisual.SetLocalPositionAndRotation(visualPosition,visualRotation);
            PadCollider.transform.SetLocalPositionAndRotation(colliderPosition,colliderRotation);
        }
        public void HideTrajectory() { if(Trajectory) Trajectory.gameObject.SetActive(false); }
        public void ShowTrajectory(Vector3 origin,Vector3 velocity,HashSet<Collider> load)
        {
            InitializeAimVisuals();
            trajectoryPoints[0]=origin; arcLengths[0]=0; int count=1;
            for(int i=1;i<trajectoryPoints.Length;i++)
            {
                float t=i*.02f;
                var point=origin+velocity*t+Physics.gravity*(.5f*t*t);
                var from=trajectoryPoints[count-1]; var delta=point-from; float distance=delta.magnitude;
                int hits=Physics.RaycastNonAlloc(from,delta.normalized,trajectoryHits,distance,~0,QueryTriggerInteraction.Ignore);
                float nearest=distance; bool blocked=hits==trajectoryHits.Length;
                for(int h=0;h<hits;h++)
                {
                    var hit=trajectoryHits[h];
                    if(hit.collider==PadCollider || load.Contains(hit.collider) || !hit.collider.transform.IsChildOf(transform)) continue;
                    if(hit.distance<=nearest) { nearest=hit.distance; blocked=true; }
                }
                point=from+delta.normalized*nearest;
                trajectoryPoints[count]=point; arcLengths[count]=arcLengths[count-1]+nearest; count++;
                if(blocked) break;
            }
            // Resample uniformly by displayed arc length. LineRenderer gradients are
            // interpolated per vertex, so this keeps the halfway fade spatially exact.
            Guide.positionCount=101;
            float length=arcLengths[count-1]; int segment=1;
            for(int i=0;i<101;i++)
            {
                float d=length*i/100f;
                while(segment<count-1 && arcLengths[segment]<d) segment++;
                float span=arcLengths[segment]-arcLengths[segment-1];
                Guide.SetPosition(i,Vector3.Lerp(trajectoryPoints[segment-1],trajectoryPoints[segment],span>0?(d-arcLengths[segment-1])/span:0));
            }
            Trajectory.gameObject.SetActive(true);
        }
    }
}
