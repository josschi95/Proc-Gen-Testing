namespace JS.WorldMap
{
    public class WorldSaveData
    {
        //NOTE//
        //A number of these values can be re-generated using the seed quickly enough that there's no need to actually save them. 
        //Seriously, I don't need a JSON with 5 sets of 40,000 item length arrays
        //However, the features (Mountains, Lakes, Rivers, Islands, etc.) take quite a bit more time and should definitely be saved

        //Time
        public int seconds;
        public int minutes;
        public int hours;
        public int days;
        public int months;
        public int years;

        public int seed;

        //Terrain Data
        public int mapWidth, mapHeight;
        public float[] heightMap;
        public float[] heatMap;
        public float[] moistureMap;
        //public float[] airPressureMap;
        public Compass[] windMap;

        //Resources
        public float[] CoalMap;
        public float[] CopperMap;
        public float[] IronMap;
        public float[] SilverMap;
        public float[] GoldMap;
        public float[] MithrilMap;
        public float[] AdmanatineMap;
        public float[] GemstoneMap;

        //Features
        public int[] biomeMap; //reference to the ID of each biome
        public BiomeGroup[] BiomeGroups;

        public MountainRange[] Mountains;
        public Lake[] Lakes;
        public River[] Rivers;
        public Island[] Islands;


        //Settlement Data
        public Settlement[] Settlements;
        public Road[] Roads;
        public Bridge[] Bridges;
    }
}