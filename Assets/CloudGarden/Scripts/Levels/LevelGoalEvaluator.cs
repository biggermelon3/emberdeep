namespace CloudGarden
{
    public static class LevelGoalEvaluator
    {
        public static readonly string[] Titles = { "A CLOUD IN YOUR HANDS", "THE FIRST RAINDROP", "A CLOUD IS BORN", "TWO LITTLE GARDENS", "A SPARK OF LIFE", "A BREATHING WORLD", "YOUR CLOUD GARDEN" };
        public static readonly string[] Hints = {
            "Hold and shape your cloud. Let it go above the garden.",
            "Bring rain to the left garden. Watch a tiny seed wake up.",
            "Fill the pond. Gather rising mist, then grow a cloud seed.",
            "Help both gardens bloom. Stretch your cloud or move between them.",
            "Rain into the pond. Send two sparks to the brass tower to power the pump.",
            "Return water to the sky and grow two more seeds. Recycle a cloud when the sky is full.",
            "Keep the weather gentle. This little world is yours to care for."
        };

        public static bool Complete(int level, GardenModel model, bool shapedAndPlaced, int cycleBaseline)
        {
            switch (level)
            {
                case 0: return shapedAndPlaced;
                case 1: return model.plants[0].growth >= .55f;
                case 2: return model.seedsCreated >= 1;
                case 3: return model.plants[0].growth >= .75f && model.plants[1].growth >= .75f;
                case 4: return model.energyDelivered >= 1 && model.pumpedWater >= .18f;
                case 5: return model.cycles - cycleBaseline >= 2;
                default: return false;
            }
        }
    }
}
