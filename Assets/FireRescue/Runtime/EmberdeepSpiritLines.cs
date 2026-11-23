using UnityEngine;

namespace FireRescue
{
    // Camp tutorial voice-over (owner's script v2, 2026-11-22): the forest spirit's lines. English audio voiced by the
    // owner, subtitles en + zh-Hans (each card has a Translations.tsv row; EmberdeepSpiritVoiceTests checks).
    // Clips: Resources/EmberdeepVoice/<Id, . -> _>.wav, cut from one take and processed by .agents/local/tools/vo-clean
    // (clean.py --preset spirit, split.py): +3 semitones, smaller-creature formants, open-air tail, each line -18 LUFS.
    // - Seconds: the clip's length.
    // - SpeechEnd: where the voice really stops (the rest of the clip is the reverb tail).
    // - Cards: subtitle cards, each with when its first word starts.
    // - PauseAfter: the silence before the next line may start.
    // Timing measured against the audio (owner's headset test 2026-11-22: cards ran ahead of the voice, and chained lines had
    // no gap). Each card start is the first word's time from word alignment against the script (.agents/local/tools/vo-clean:
    // sapi_words.ps1 + card_times.py: the offline Windows recognizer constrained to the line's own words), checked against
    // speech onsets. SpeechEnd is the last speech energy within 38 dB of the line's loud speech. +.02 s for the
    // pitch shifter's latency. The first cut had guessed both from pauses alone (intro.1's cards were 1.5 and 1.7 s early,
    // ends up to .35 s early).
    public sealed class EmberdeepSpiritLine
    {
        public readonly string Id;
        public readonly float Seconds,SpeechEnd,PauseAfter;
        public readonly (float At,string Text)[] Cards;
        public EmberdeepSpiritLine(string id,float seconds,float speechEnd,float pauseAfter,params (float At,string Text)[] cards)
        {Id=id;Seconds=seconds;SpeechEnd=speechEnd;PauseAfter=pauseAfter;Cards=cards;}
        public string Resource=>"EmberdeepVoice/"+Id.Replace('.','_');
        // The line counts as said just after its last word; the tail rings on into the pause that follows.
        public float DoneAt=>Mathf.Min(Seconds,SpeechEnd+EmberdeepSpiritLines.AfterWords);
        // The card on screen at t seconds into the line (the last card stays up to the end).
        public string CardAt(float t)
        {
            string card=null;
            foreach(var c in Cards)if(t>=c.At)card=c.Text;
            return card;
        }
        public override string ToString()=>Id;
    }

    public static class EmberdeepSpiritLines
    {
        // Silence between the last word and the next line's first: AfterWords + PauseAfter + the next clip's .12 s lead.
        // Default PauseAfter .8 (~1.1 s heard); 1.0-1.2 after a joke or a question, so it can land.
        public const float AfterWords=.15f,Pause=.8f,PauseLong=1.15f;
        // ---- Intro: the spirit has just come out of the tent ----------------------------------------------------------
        public static readonly EmberdeepSpiritLine Intro1=new("intro.1",14.21f,13.68f,PauseLong,
            (0,"Hmm. Ah, I see. So YOU'RE the new forest guardian they sent?"),
            (6.19f,"And your name is... You know what, I don't care."),
            (9.65f,"Let's get your orientation over with, so you can leave me in peace."));
        public static readonly EmberdeepSpiritLine Intro2=new("intro.2",6.89f,6.3f,Pause,
            (0,"See that blue gem on the back of your hand?"),
            (2.22f,"That's where your water lives. You'll need it to protect the forest."));
        // ---- Campfire: palm water --------------------------------------------------------------------------------------
        public static readonly EmberdeepSpiritLine FireAim=new("fire.aim",9.5f,8.67f,1f,
            (0,"Now, see that unattended, unauthorized campfire?"),
            (4.03f,"Open your hand and aim your palm at it."),
            (6.45f,"Like in that movie... Metal Man."));
        public static readonly EmberdeepSpiritLine FirePress=new("fire.press",5.24f,4.47f,Pause,
            (0,"Then press the gem with a finger from your other hand, and keep it there."));
        public static readonly EmberdeepSpiritLine FireFirstSpray=new("fire.first_spray",6.64f,5.99f,Pause,
            (0,"What's that? Too fiddly?"),
            (2.26f,"Well, it's either that, or you soak everything you wave at."));
        public static readonly EmberdeepSpiritLine FireNag1=new("fire.nag1",7.98f,7.37f,Pause,
            (0,"You gonna... you gonna put that fire out?"),
            (2.76f,"Palm at the fire, finger on the gem."),
            (4.73f,"Or are you too good for target practice?"));
        public static readonly EmberdeepSpiritLine FireNag2=new("fire.nag2",5.03f,4.21f,Pause,
            (0,"Palm. Fire. Finger. Gem."),
            (1.64f,"I'll wait. I'm a spirit, waiting is all I do."));
        // ---- Pump ------------------------------------------------------------------------------------------------------
        public static readonly EmberdeepSpiritLine PumpJoke=new("pump.joke",7.83f,7.16f,PauseLong,
            (0,"You didn't think water was unlimited, did you?"),
            (2.72f,"Or has living around humans made you take everything for granted?"));
        public static readonly EmberdeepSpiritLine PumpHow=new("pump.how",5.71f,4.92f,Pause,
            (0,"When you run low, make a fist and pound that pump next to you."),
            (3.97f,"Go on, top it up."));
        public static readonly EmberdeepSpiritLine PumpNag=new("pump.nag",5f,4.29f,Pause,
            (0,"The pump. Next to you. Make a fist and hit it."),
            (3.34f,"It won't hit itself."));
        public static readonly EmberdeepSpiritLine PumpDone=new("pump.done",3.19f,2.49f,Pause,
            (0,"There. Full. Try not to waste it."));
        // ---- Sheep -----------------------------------------------------------------------------------------------------
        public static readonly EmberdeepSpiritLine SheepFire=new("sheep.fire",7.59f,6.77f,Pause,
            (0,"Oh, you fluffy idiot. Trying to turn yourself into roast lamb?"),
            (3.35f,"Guardian, quick! Put out the sheep, and the tree, before it spreads."));
        public static readonly EmberdeepSpiritLine SheepNag=new("sheep.nag",4.92f,4.1f,Pause,
            (0,"Still burning. The sheep. And the tree."),
            (2.52f,"Palm, gem, water. You know this."));
        public static readonly EmberdeepSpiritLine PetHow=new("pet.how",6.51f,5.7f,Pause,
            (0,"Now go on, give it a pat."),
            (1.67f,"Teach it a lesson about how close it came to becoming someone's dinner today."));
        // ---- Done ------------------------------------------------------------------------------------------------------
        public static readonly EmberdeepSpiritLine Outro=new("outro",15.86f,15.05f,.5f,
            (0,"...and send the new guardian off with words of encouragement..."),
            (3.92f,"Right. That's your orientation. I'm sure you'll do fine."),
            (7.72f,"It's not like anyone cares about nature these days."),
            (11.25f,"Am I right, ladies and gentlemen?"),
            (14.07f,"...Okay. Get out of here."));

        public static readonly EmberdeepSpiritLine[] All=
            {Intro1,Intro2,FireAim,FirePress,FireFirstSpray,FireNag1,FireNag2,PumpJoke,PumpHow,PumpNag,PumpDone,SheepFire,SheepNag,PetHow,Outro};

        // ---- Plan phase (fireground; owner 2026-11-22) -------------------------------------------------------------------
        // One intro line per topic, said once when the Plan phase of the level that introduces it starts
        // (EmberdeepPlanSpirit, FireRescueGame.Spirit.cs). Text: the owner's script, reviewed (2026-11-22 20:40). Recorded
        // by phone (plan-take-1) and processed like the camp lines: spirit preset, -18 LUFS.
        // Card times come from word alignment (sapi_words.ps1, confidence .86 / .85), snapped to a speech onset up to
        // .35 s earlier, +.02 s for the pitch shifter. With no clip in Resources/EmberdeepVoice a line never plays
        // (ship-safe). Not in All: the camp's own tests walk All.
        public static readonly EmberdeepSpiritLine PlanTower=new("plan.tower",27.47f,26.55f,Pause,
            (0,"Water towers! What do they do? Where do they come from?"),
            (4.69f,"And why 'water tower', not 'tower water'? Hmm. Anyway."),
            (10.98f,"Put one in the middle of the trees,"),
            (12.58f,"and when fire comes near, it rains on everything around it."),
            (16.58f,"Set them down while time's frozen, before you ring the bell,"),
            (19.81f,"or even once the fire's going."),
            (22.11f,"But they're a one-time thing. Once it's rained, it's done. No refills."));
        public static readonly EmberdeepSpiritLine PlanWind=new("plan.wind",22.38f,21.46f,Pause,
            (0,"Wind. What a cruel creature, lurking in this naive little paradise."),
            (8.5f,"They lure you close, then rip your heart out and smash it to pieces."),
            (13.74f,"Anyway. Be careful of them. They help the fire spread."),
            (17.73f,"Watch the weathervane: the fire runs the way it points."));
        public static readonly EmberdeepSpiritLine[] Plan={PlanTower,PlanWind};
    }
}
