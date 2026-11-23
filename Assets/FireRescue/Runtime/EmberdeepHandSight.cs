using Oculus.Interaction.Input;

namespace FireRescue
{
    // Whether a tracked hand is really in view NOW. Both rigs' hand chains run through the Interaction SDK's
    // LastKnownGoodHand (Quest: FromOVRHandDataSource -> LastKnownGoodHand -> Hand; PICO: FromUnityXRHandDataSource ->
    // LastKnownGoodHand -> HandFilter -> Hand). While the runtime keeps a lost hand "connected", that modifier goes on
    // reporting it as valid and tracked at its last good pose, pointer pose and pinch included (HandDataAsset.CopyPosesFrom),
    // so Hand.IsTrackedDataValid never drops and the hand hangs in the air (headset test 2, 2026-11-22). This asks the start
    // of the chain, what the runtime itself reports. Same cause and fix as Shape of Memory's MetaShadowPlayer.Seen
    // (/main/TimBranch/shadow-workshop cs:277), kept in Emberdeep code so ShadowWorkshop's provider is unchanged here.
    public static class EmberdeepHandSight
    {
        // The start of a hand's data chain: before LastKnownGoodHand, filters or synthesis.
        public static IDataSource<HandDataAsset> RawSource(IDataSource<HandDataAsset> source)
        {
            for(int depth=0;depth<16 && source!=null;depth++)
            {
                var modifier=source as DataModifier<HandDataAsset>;
                if(modifier==null || modifier.ModifyDataFromSource==null)break;
                source=modifier.ModifyDataFromSource;
            }
            return source;
        }
        // tracked: the runtime tracks the hand now; confident: it also reports high confidence (logs only). A hand that is
        // not a data chain (test fixtures) answers for itself.
        public static bool RawTracked(IHand hand,out bool confident)
        {
            confident=false;
            if(hand==null || !hand.IsConnected)return false;
            if(hand is IDataSource<HandDataAsset> chain)
            {
                var data=RawSource(chain)?.GetData();
                if(data==null)return false;
                confident=data.IsHighConfidence;
                return data.IsDataValid && data.IsTracked;
            }
            confident=hand.IsHighConfidence;
            return hand.IsTrackedDataValid;
        }
        public static bool RawTracked(IHand hand)=>RawTracked(hand,out _);
    }

    // A hand's sight with a short grace, so a dropped frame neither blinks a ray nor resets its pinch: Seen while the
    // chain gives a hand (present) and its source tracks it (raw); after the source stops, Seen holds LostAfter.
    public struct EmberdeepSightGate
    {
        public const float LostAfter=.2f;
        public bool Seen {get;private set;}
        public float LostFor {get;private set;}
        public bool Step(bool present,bool raw,float dt)
        {
            if(present && raw){Seen=true;LostFor=0;}
            else if(!present || !Seen){Seen=false;LostFor=0;}
            else
            {
                LostFor+=float.IsFinite(dt) && dt>0?dt:0;
                if(LostFor>=LostAfter){Seen=false;LostFor=0;}
            }
            return Seen;
        }
        public void Reset(){Seen=false;LostFor=0;}
    }
}
