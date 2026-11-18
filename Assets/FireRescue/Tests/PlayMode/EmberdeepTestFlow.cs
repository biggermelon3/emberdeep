namespace FireRescue.Tests
{
    public static class EmberdeepTestFlow
    {
        // Existing focused tests skip only the real-time countdown. The dedicated
        // 024 scene test exercises the actual button, pause, countdown and replay.
        public static void StartPrepared(FireRun run)
        {
            if(run.Phase!=FirePhase.Preparing)return;
            run.BeginCountdown();for(int i=0;i<31;i++)run.TickPreparation(.1f);
        }
    }
}
