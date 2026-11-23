using System.Collections.Generic;
using Step=FireRescue.EmberdeepCampTutorialFlow.Step;

namespace FireRescue
{
    // Which forest-spirit line plays when, in the camp tutorial (pure logic; EmberdeepCampPlay feeds it the camp each frame
    // and plays what it returns on EmberdeepForestSpirit).
    // - Each step queues its lines on entry; one line at a time, in order.
    // - A queued line the player has already done is dropped when it would start (the owner's rule: skip a line if the
    //   player already did the thing). A line already playing is never cut; the flow waits for it (Quiet).
    // - Reactions: the first spray at the campfire, the gem pumped full, the sheep catching fire.
    // - Reminders: after NagAfter s of silence with nothing done, at most one or two per step.
    public sealed class EmberdeepSpiritDirector
    {
        public struct Facts
        {
            public Step Step;
            public bool CampfireOut,Spraying,PumpMet,SheepLit,SheepBurning,SaplingBurning,Petted;
            public int PumpStrikes;
        }
        // Seconds: the spirit's arrival before the first word; quiet time before a reminder.
        public const float IntroDelay=2f,NagAfter=8,SecondNagAfter=10,SheepNagAfter=10;
        readonly List<EmberdeepSpiritLine> queue=new();
        readonly HashSet<EmberdeepSpiritLine> said=new();
        Step entered=Step.Off;
        float quietFor,sinceBegin;
        bool sprayedInStep;
        int strikesAtEntry;
        public bool QueueEmpty=>queue.Count==0;
        public int Queued=>queue.Count;
        public bool Said(EmberdeepSpiritLine line)=>said.Contains(line);
        public bool IsQueued(EmberdeepSpiritLine line)=>queue.Contains(line);
        public bool IntroDone=>said.Contains(EmberdeepSpiritLines.Intro2);
        public float QuietFor=>quietFor;

        public void Begin(){queue.Clear();said.Clear();entered=Step.Off;quietFor=sinceBegin=0;sprayedInStep=false;strikesAtEntry=0;}
        public void Stop(){queue.Clear();entered=Step.Off;}

        // One frame (dt 0 while the camp is not live: timers hold). speaking: a line is being said. Returns the line to
        // start now, or null.
        public EmberdeepSpiritLine Tick(float dt,in Facts f,bool speaking)
        {
            if(!(dt>=0) || float.IsInfinity(dt))dt=0;
            sinceBegin+=dt;quietFor=speaking?0:quietFor+dt;
            if(f.Step!=entered){entered=f.Step;Enter(f);}
            if(f.Step==Step.Campfire && f.Spraying)sprayedInStep=true;
            React(f);
            for(int i=queue.Count-1;i>=0;i--)if(Skip(queue[i],f))queue.RemoveAt(i);
            if(speaking || queue.Count==0 || (f.Step==Step.Intro && sinceBegin<IntroDelay))return null;
            var next=queue[0];queue.RemoveAt(0);said.Add(next);quietFor=0;return next;
        }
        void Enter(in Facts f)
        {
            switch(f.Step)
            {
                case Step.Intro:Queue(EmberdeepSpiritLines.Intro1);Queue(EmberdeepSpiritLines.Intro2);break;
                case Step.Campfire:sprayedInStep=false;Queue(EmberdeepSpiritLines.FireAim);Queue(EmberdeepSpiritLines.FirePress);break;
                case Step.Pump:strikesAtEntry=f.PumpStrikes;Queue(EmberdeepSpiritLines.PumpJoke);Queue(EmberdeepSpiritLines.PumpHow);break;
                case Step.Pet:Queue(EmberdeepSpiritLines.PetHow);break;
                case Step.Done:queue.Clear();Queue(EmberdeepSpiritLines.Outro);break;
            }
        }
        void React(in Facts f)
        {
            bool quiet=queue.Count==0;
            switch(f.Step)
            {
                case Step.Campfire:
                    if(f.Spraying)Once(EmberdeepSpiritLines.FireFirstSpray);
                    if(!f.CampfireOut && !f.Spraying && quiet)
                    {
                        if(!Said(EmberdeepSpiritLines.FireNag1)){if(quietFor>=NagAfter)Once(EmberdeepSpiritLines.FireNag1);}
                        else if(quietFor>=SecondNagAfter)Once(EmberdeepSpiritLines.FireNag2);
                    }
                    break;
                case Step.Pump:
                    if(f.PumpMet)Once(EmberdeepSpiritLines.PumpDone);
                    else if(quiet && StrikesInStep(f)==0 && quietFor>=NagAfter)Once(EmberdeepSpiritLines.PumpNag);
                    break;
                case Step.Sheep:
                    // The scare comes first, whatever was waiting.
                    if(f.SheepLit && !Said(EmberdeepSpiritLines.SheepFire) && !IsQueued(EmberdeepSpiritLines.SheepFire))queue.Insert(0,EmberdeepSpiritLines.SheepFire);
                    if(Said(EmberdeepSpiritLines.SheepFire) && (f.SheepBurning || f.SaplingBurning) && quiet && quietFor>=SheepNagAfter)
                        Once(EmberdeepSpiritLines.SheepNag);
                    break;
            }
        }
        int StrikesInStep(in Facts f)=>f.PumpStrikes-strikesAtEntry;
        // The player already did what the line asks.
        bool Skip(EmberdeepSpiritLine line,in Facts f)
        {
            if(line==EmberdeepSpiritLines.FireAim)return f.CampfireOut;
            if(line==EmberdeepSpiritLines.FirePress)return f.CampfireOut || sprayedInStep;
            if(line==EmberdeepSpiritLines.FireNag1 || line==EmberdeepSpiritLines.FireNag2)return f.CampfireOut || f.Step!=Step.Campfire;
            // PumpHow is never skipped: the camp pump stays hidden until it has been said (EmberdeepCampPlay.PumpHeld).
            if(line==EmberdeepSpiritLines.PumpNag)return f.PumpMet || StrikesInStep(f)>0 || f.Step!=Step.Pump;
            if(line==EmberdeepSpiritLines.SheepNag)return !(f.SheepBurning || f.SaplingBurning);
            if(line==EmberdeepSpiritLines.PetHow)return f.Petted;
            return false;
        }
        void Queue(EmberdeepSpiritLine line){if(!said.Contains(line) && !queue.Contains(line))queue.Add(line);}
        void Once(EmberdeepSpiritLine line)=>Queue(line);
    }
}
