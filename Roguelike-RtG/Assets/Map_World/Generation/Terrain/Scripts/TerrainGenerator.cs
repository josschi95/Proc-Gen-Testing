using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DelaunayVoronoi;
using JS.World.Map.Features;
using JS.Math;
using Unity.VisualScripting;

//Special thaks to http://entropicparticles.com/6-days-of-creation

// Features To Add:
//      Rain Shadows
//      Ocean Currents
//

namespace JS.World.Map.Generation
{
    /// <summary>
    /// Generates world altitude, climate, and biomes
    /// </summary>
    public class TerrainGenerator : MonoBehaviour 
    {
        public int mapSize { get; private set; }

        [SerializeField] private WorldGenerator worldGenerator;
        [SerializeField] private Erosion erosion;

        [Space]

        [SerializeField] private BiomeHelper biomeHelper;

        [Header("Perlin Noise")]
        [SerializeField] private float noiseScale;
        [Tooltip("The number of iterations of Perlin Noise over an area")]
        [SerializeField] private int octaves = 4;
        [Range(0, 1)]
        [Tooltip("Controls decrease in amplitude of subsequent octaves")]
        [SerializeField] private float persistence = 0.5f;
        [Tooltip("Controls increase in frequency of octaves")]
        [SerializeField] private float lacunarity = 2f;
        [SerializeField] private Vector2 offset;

        private List<WorldTile> _water;
        private List<WorldTile> _land;

        public void SetInitialValues(int size)
        {
            _water = new List<WorldTile>();
            _land = new List<WorldTile>();

            mapSize = size;
            Features.TerrainData.MapSize = mapSize;
        }

        #region - Altitude -
        /// <summary>
        /// Randomly places tectonic plates and raises the altitude of surrouning nodes
        /// </summary>
        public void PlaceTectonicPlates(int plateCount, int minPlateSize, int maxPlateSize)
        {
            PlateTectonicsNew(maxPlateSize);


            //place n tectonic points and increase the altitude of surrounding nodes within range r by  a flat-top gaussian
            //tectonic points will also result in mountains, volcanoes? Fault lines?
            //place fault lines using Voronoi polygons, this is where volcanoes and mountains will be added
            float[,] heightMap = new float[mapSize, mapSize];
            int border = Mathf.RoundToInt(minPlateSize * 0.5f);
            var plates = new List<WorldTile>();
            //Alternatively I could make this a while loop and just continue if I get a point that's too close
            for (int points = 0; points < plateCount; points++)
            {
                
                int nodeX, nodeY; //select a random point on the map
                if (points < plateCount / 2) //First half will favor the center of the map
                {
                    nodeX = worldGenerator.PRNG.Next(border * 2, mapSize - border * 2);
                    nodeY = worldGenerator.PRNG.Next(border * 2, mapSize - border * 2);
                }
                else
                {
                    nodeX = worldGenerator.PRNG.Next(border, mapSize - border);
                    nodeY = worldGenerator.PRNG.Next(border, mapSize - border);
                }

                WorldTile tectonicNode = WorldMap.GetNode(nodeX, nodeY);
                tectonicNode.isTectonicPoint = true;
                plates.Add(tectonicNode);
                float range = worldGenerator.PRNG.Next(minPlateSize, maxPlateSize);

                //Grab all nodes within range
                var nodesInRange = WorldMap.GetNodesInRange_Circle(tectonicNode, (int)range);

                //Calculate their base height based on distance from the tectonic point
                for (int i = 0; i < nodesInRange.Count; i++)
                {
                    //nodesInRange[i].isTectonicPoint = true; //Mark as tectonic node, mainly for visual referencing

                    //The relative distance from this node to the tectonic node
                    float x = GridMath.GetStraightDist(tectonicNode.x, tectonicNode.y, nodesInRange[i].x, nodesInRange[i].y) / range;

                    float n = 6; //affects the width of the flat-top on the gaussian
                    float y = 0.5f * Mathf.Exp(-10 * Mathf.Pow(x, n)); //flat-top gaussian
                    
                    //Only set it to be y if the point is lower, so plates don't sink each other
                    if (heightMap[nodesInRange[i].x, nodesInRange[i].y] < y)
                        heightMap[nodesInRange[i].x, nodesInRange[i].y] = y;
                }
            }
            FindPlateBoundaries(plates);
            Features.TerrainData.HeightMap = heightMap;
        }

        private int poissonRadius = 50;

        List<Vector2Int> allowed_movements = new List<Vector2Int>
        {
            Vector2Int.left,
            Vector2Int.up,
            Vector2Int.right,
            Vector2Int.down,
            new Vector2Int(-1, 1),
            new Vector2Int(1, 1),
            new Vector2Int(1, -1),
            new Vector2Int(1, 1),
        };

        public void PlateTectonicsNew(int maxPlateSize)
        {
            // nah this is shit
            // another method would be to do this but take 10 steps at a time, or like 5-15 steps using bresenham
            // also add some check so it doesn't immediately double back on itself
            int iterations = 100;
            for (int i = 0; i < iterations; i++)
            {
                int x = 0;
                int y = 0;
                if (Random.value < 0.5f)
                {
                    if (Random.value < 0.5) y = mapSize - 1;

                    x = Random.Range(0, mapSize);
                }
                else
                {
                    if (Random.value < 0.5) x = mapSize - 1;

                    y = Random.Range(0, mapSize);
                }

                while (true)
                {
                    var direction = allowed_movements[Random.Range(0, allowed_movements.Count)];

                    x += direction.x;
                    y += direction.y;

                    var node = WorldMap.GetNode(x, y);
                    if (node == null) break;
                    if (node.PlateID == 0) break;

                    node.PlateID = 0;
                }
            }
            return;

            // Randomly place down points - poisson
            var size = new Vector2(WorldMap.Width, WorldMap.Height);
            var poissonPoints = Poisson.GeneratePoints(WorldMap.Seed, poissonRadius, size);
            List<WorldTile> tiles = new List<WorldTile>();
            foreach(var tile in poissonPoints)
            {
                var node = WorldMap.GetNode(tile);
                if (node != null) tiles.Add(node);
            }

            MathsUtil.ShuffleList(tiles, worldGenerator.PRNG);
            int toRemove = Mathf.RoundToInt(tiles.Count / 4);

            for (int i = toRemove - 1; i >= 0; i--) tiles.RemoveAt(i);

            // generate voronoi diagram
            var plateBorders = new HashSet<WorldTile>();
            var delaunay = new DelaunayTriangulator();
            var voronoi = new Voronoi();

            Debug.Assert(delaunay != null, "Delaunay is null!");

            var points = delaunay.ConvertToPoints(tiles, mapSize); // This has caused an error x4
            var triangulation = delaunay.BowyerWatson(points);
            var voronoiEdges = voronoi.GenerateEdgesFromDelaunay(triangulation);

            foreach (var edge in voronoiEdges)
            {
                var x0 = Mathf.RoundToInt(Mathf.Clamp((float)edge.Point1.X, 0, mapSize));
                var y0 = Mathf.RoundToInt(Mathf.Clamp((float)edge.Point1.Y, 0, mapSize));
                var x1 = Mathf.RoundToInt(Mathf.Clamp((float)edge.Point2.X, 0, mapSize));
                var y1 = Mathf.RoundToInt(Mathf.Clamp((float)edge.Point2.Y, 0, mapSize));

                var bresenham = Bresenham.PlotLine(x0, y0, x1, y1);
                foreach (var p in bresenham)
                {
                    int x = p.x;
                    int y = p.y;
                    while (x < 0) x++;
                    while (y < 0) y++;
                    while (x >= mapSize) x--;
                    while(y >= mapSize) y--;

                    var newNode = WorldMap.GetNode(x, y);
                    if (newNode != null && !plateBorders.Contains(newNode))
                    {
                        plateBorders.Add(newNode);
                        newNode.PlateID = 0;
                    }
                }
            }

            for (int i = 0; i < tiles.Count; i++)
            {
                FloodFillPlate(i+1, tiles[i], maxPlateSize);
            }
            
            // each area becomes a plate, allow a few to fuse to form larger plates

            // allow the points to grow, stopping when they contact a 'claimed' node

            // give each plate a direction
            // plates pushing against each other create mountains and volcanoes
            // plates pulling away from each other create rifts
            // plates moving past each other create strike-slip faults

            // the amount of landmass assigned to each plate is directly relational to its size
            // the locaction and shape of that landmass should roughly resemble the shape of the plate
            // it's location within the plate should be dependent on the movement direction of the plate


        }

        private List<WorldTile> FloodFillPlate(int id, WorldTile startNode, int maxSize)
        {
            var tiles = new List<WorldTile>();
            int[,] mapFlags = new int[mapSize, mapSize];

            Queue<WorldTile> queue = new Queue<WorldTile>();
            queue.Enqueue(startNode);

            while (queue.Count > 0)// && tiles.Count <= 750)
            {
                var node = queue.Dequeue();
                tiles.Add(node);
                node.PlateID = id;

                for (int i = 0; i < node.neighbors_adj.Count; i++)
                {
                    var neighbor = node.neighbors_adj[i];

                    // Chance to steal a node
                    if (neighbor.PlateID == 0) continue;

                    if (neighbor.PlateID >= 0 && worldGenerator.PRNG.Next(0, 100) > 25) continue;

                    if (mapFlags[neighbor.x, neighbor.y] == 0)
                    {
                        mapFlags[neighbor.x, neighbor.y] = 1;
                        queue.Enqueue(neighbor);
                    }
                }
            }
            return tiles;
        }

        /// <summary>
        /// Creates a Voronoi diagram given the tectonic points and calculates plate boundaries.
        /// </summary>
        private void FindPlateBoundaries(List<WorldTile> nodes)
        {
            var plateBorders = new HashSet<WorldTile>();
            var delaunay = new DelaunayTriangulator();
            var voronoi = new Voronoi();

            var points = delaunay.ConvertToPoints(nodes, mapSize);
            var triangulation = delaunay.BowyerWatson(points);
            var voronoiEdges = voronoi.GenerateEdgesFromDelaunay(triangulation);

            foreach (var edge in voronoiEdges)
            {
                var x0 = Mathf.RoundToInt(Mathf.Clamp((float)edge.Point1.X, 0, mapSize));
                var y0 = Mathf.RoundToInt(Mathf.Clamp((float)edge.Point1.Y, 0, mapSize));
                var x1 = Mathf.RoundToInt(Mathf.Clamp((float)edge.Point2.X, 0, mapSize));
                var y1 = Mathf.RoundToInt(Mathf.Clamp((float)edge.Point2.Y, 0, mapSize));

                var bresenham = Bresenham.PlotLine(x0, y0, x1, y1);
                foreach(var p in bresenham)
                {
                    int x = p.x;
                    int y = p.y;
                    while (x < 0) x++;
                    while (y < 0) y++;
                    while (x >= mapSize) x--;
                    while (y >= mapSize) y--;

                    var newNode = WorldMap.GetNode(x, y);
                    if (newNode != null && !plateBorders.Contains(newNode))
                        plateBorders.Add(newNode);
                }
            }
            Features.TerrainData.PlateBorders = plateBorders;
        }

        /// <summary>
        /// Generates a height map using randomly placed tectonic plates and Perlin Noise
        /// </summary>
        public void GenerateHeightMap()
        {
            float[,] heightMap = Features.TerrainData.HeightMap;

            float[,] perlinMap = PerlinNoise.GenerateHeightMap(mapSize, WorldMap.Seed, noiseScale, octaves, persistence, lacunarity, offset);
            float[,] falloffMap = FalloffGenerator.GenerateFalloffMap(mapSize);
            for (int x = 0; x < heightMap.GetLength(0); x++)
            {
                for (int y = 0; y < heightMap.GetLength(1); y++)
                {
                    perlinMap[x, y] -= 0.25f;
                    heightMap[x, y] += perlinMap[x, y];
                    heightMap[x, y] -= falloffMap[x, y];

                    heightMap[x, y] = Mathf.Clamp(heightMap[x, y], 0, 1);
                }
            }

            Features.TerrainData.HeightMap = heightMap;
        }

        /// <summary>
        /// Simulates erosion on the height map using a Raindrop algorithm.
        /// </summary>
        public void ErodeLandMasses(int iterations)
        {
            float[,] heightMap = Features.TerrainData.HeightMap;
            float[,] initial = Features.TerrainData.HeightMap;
            heightMap = erosion.Erode(heightMap, iterations, WorldMap.Seed);
            Features.TerrainData.HeightMap = heightMap;

            for (int x = 0; x < mapSize; x++)
            {
                for (int y = 0; y < mapSize; y++)
                {
                    float diff = initial[x, y] - heightMap[x, y];
                    //if (diff != 0) Debug.Log("Difference: [" + x + "," + y + "] = " + diff);
                }
            }
        }

        public void SetNodeAltitudeValues()
        {
            float[,] heightMap = Features.TerrainData.HeightMap;

            //This is doing nothing and is EXTREMELY FLAWED only taking into account altitude
            //float[,] airPressureMap = AirPressureData.GetAirPressureMap(heightMap);

            //Pass height and air pressure values to nodes
            for (int x = 0; x < mapSize; x++)
            {
                for (int y = 0; y < mapSize; y++)
                {
                    WorldTile node = WorldMap.GetNode(x, y);
                    bool isLand = heightMap[x, y] >= WorldParameters.SEA_LEVEL;

                    node.SetAltitude(heightMap[x, y], isLand);
                    if (isLand) _land.Add(node);
                    else _water.Add(node);

                    //node.airPressure = airPressureMap[x, y];
                }
            }
            Features.TerrainData.HeightMap = heightMap;
        }
        #endregion

        #region - Terrain Feature Identification -
        /// <summary>
        /// Identifies and registers Lakes.
        /// </summary>   
        public IEnumerator IdentifyBodiesOfWater()
        {
            var lakes = new List<Lake>();
            while(_water.Count > 0)
            {
                var body = FloodFillRegion(_water[0], false);
                bool trueLake = LakeIsLandlocked(body);

                if (trueLake)
                {
                    Lake newLake = new Lake(lakes.Count);
                    newLake.GridNodes = new GridCoordinates[body.Count];
                    lakes.Add(newLake);

                    for (int i = 0; i < body.Count; i++)
                    {
                        newLake.GridNodes[i] = new GridCoordinates(body[i].x, body[i].y);
                        body[i].SetBiome(biomeHelper.Lake);
                    }
                }

                for (int i = 0; i < body.Count; i++)
                {
                    _water.Remove(body[i]);
                }

                yield return null;
            }
            Features.TerrainData.Lakes = lakes.ToArray();
        }

        private bool LakeIsLandlocked(List<WorldTile> tiles)
        {
            if (tiles.Count >= 1000)
            {
                //UnityEngine.Debug.Log("Lake size is greater than 1,000. " + Nodes.Count);
                //return false;
            }

            for (int i = 0; i < tiles.Count; i++)
            {
                if (tiles[i].x == 0 || tiles[i].x == mapSize - 1) return false;
                if (tiles[i].y == 0 || tiles[i].y == mapSize - 1) return false;
            }
            return true;
        }

        /// <summary>
        /// Identifies and registers Islands.
        /// </summary>
        public IEnumerator IdentifyLandMasses()
        {
            var land = new List<LandMass>();
            while (_land.Count > 0)
            {
                var body = FloodFillRegion(_land[0], true);
                LandMass newLand = new LandMass(land.Count);

                if (body.Count < mapSize / 2) newLand.Size = LandSize.Islet;
                else if (body.Count < mapSize) newLand.Size = LandSize.Island;
                else newLand.Size = LandSize.Continent;

                var coords = new GridCoordinates[body.Count];
                for (int i = 0; i < body.Count; i++)
                {
                    coords[i] = new GridCoordinates(body[i].x, body[i].y);
                    _land.Remove(body[i]);
                }
                newLand.GridNodes = coords;

                land.Add(newLand);
                yield return null;
            }
            Features.TerrainData.LandMasses = land.ToArray();
        }

        /// <summary>
        /// Flood Fill Algorithm to find all neighbor land/water tiles
        /// </summary>
        private List<WorldTile> FloodFillRegion(WorldTile startNode, bool isLand)
        {
            var tiles = new List<WorldTile>();

            if (startNode.IsLand != isLand) throw new UnityException("Start Node does not align with given parameters!");

            int[,] mapFlags = new int[mapSize, mapSize];

            Queue<WorldTile> queue = new Queue<WorldTile>();
            queue.Enqueue(startNode);

            while(queue.Count > 0)
            {
                var node = queue.Dequeue();
                tiles.Add(node);

                for (int i = 0; i < node.neighbors_adj.Count; i++)
                {
                    var neighbor = node.neighbors_adj[i];
                    if (mapFlags[neighbor.x, neighbor.y] == 0 && neighbor.IsLand == isLand)
                    {
                        mapFlags[neighbor.x, neighbor.y] = 1;
                        queue.Enqueue(neighbor);
                    }
                }
            }
            return tiles;
        }

        public void IdentifyCoasts()
        {
            bool[,] coasts = new bool[mapSize, mapSize];

            for (int x = 0; x < mapSize; x++)
            {
                for (int y = 0; y < mapSize; y++)
                {
                    WorldTile node = WorldMap.GetNode(x, y);
                    if (!node.IsLand) continue;
                    for (int i = 0; i < node.neighbors_all.Count; i++)
                    {
                        if (!node.neighbors_all[i].IsLand && node.Rivers.Count == 0)
                        {
                            coasts[x, y] = true;
                        }
                    }
                }
            }
            Features.TerrainData.Coasts = coasts;
        }

        /// <summary>
        /// Identifies and registers Mountains.
        /// </summary>
        public void IdentifyMountains()
        {
            for (int x = 0; x < mapSize; x++)
            {
                for (int y = 0; y < mapSize; y++)
                {
                    WorldTile node = WorldMap.GetNode(x, y);
                    if (Features.TerrainData.HeightMap[node.x, node.y] >= WorldParameters.MOUNTAIN_HEIGHT)
                    {
                        node.SetBiome(biomeHelper.Mountain);
                        node.CheckNeighborMountains();
                    }
                }
            }
            var ranges = TerrainHelper.FindMountainRanges();
            Features.TerrainData.Mountains = ranges.ToArray();
        }
        #endregion

        #region - Climate -
        public void GenerateHeatMap(int northLatitude, int southLatitude)
        {
            //Create Heat Map
            float[,] heatMap = ClimateMath.GenerateHeatMap(Features.TerrainData.HeightMap, worldGenerator.PRNG);

            //Pass heat values to nodes
            for (int x = 0; x < mapSize; x++)
            {
                for (int y = 0; y < mapSize; y++)
                {
                    WorldTile node = WorldMap.GetNode(x, y);
                    node.SetTemperatureValues(heatMap[x, y], ClimateMath.GetHeatIndex(heatMap[x, y]));
                }
            }

            Features.TerrainData.HeatMap = heatMap;
        }

        private void GenerateWindMap()
        {

        }

        public void GeneratePrecipitationMap()
        {
            var heightMap = Features.TerrainData.HeightMap;
            //Create Wind Map
            Compass[,] windMap = ClimateMath.GenerateWindMap(heightMap);
            //Other factors that need to be taken into account
                //Coriolis effect
                //Convergence zones

            //Pass prevailing wind direction to nodes
            for (int x = 0; x < mapSize; x++)
            {
                for (int y = 0; y < mapSize; y++)
                {
                    WorldTile node = WorldMap.GetNode(x, y);
                    node.windDirection = windMap[x, y];
                }
            }

            var airPressureMap = ClimateMath.GetAirPressureMap(heightMap);

            // how much water vapor a node can hold before humidity = 100%
            var waterCapacityMap = new float[mapSize, mapSize];
            for (int x = 0; x < mapSize; x++)
            {
                for (int y = 0; y < mapSize; y++)
                {
                    WorldTile node = WorldMap.GetNode(x, y);

                    // Clausius-Clapeyron equation kinda, Temperature needs to be C
                    float c = Temperature.FloatToCelsius(Features.TerrainData.HeatMap[x, y]);
                    float pow = (17.67f * c) / (c + 243.5f);
                    float capacity = 6.112f * Mathf.Exp(pow);

                    // Modify value based on air pressure derived from altitude
                    // Lower air pressure = less capacity
                    capacity *= airPressureMap[x, y];

                    waterCapacityMap[x, y] = capacity;

                    node.airPressure = airPressureMap[x, y];
                    node.WaterCapacity = capacity;
                }
            }

            Debug.LogWarning("Pick up work here.");
            /* Precipitation Generation Rework
               1) Start with a base precipitation value for each node - based on latitude and proximity to oceans
                    - use my damped cosine method for initial value
                    - find nearest ocean tile and get bonus based on distance
                    - save time by skipping any node that is not land (lakes and rivers can copy surrounding tiles)

               2) Adjust values based on altitude, Adjust base values based on altitude. Higher altitudes receive
                    more rain due to orographic lift, up to a certain threshold
                    - create humidity map, set values of all water nodes to 100
                    - calculate the water capacity for each node - how much water vapor it can hold (before humidity = 100)
                        - primary factor is temperature - warm air holds more water than cold air
                        - secondary factor is air pressure - higher pressure = greater capacity
                        - Clausius-Clapeyron equation
                        - Relative Humidity (RH) = e / es 
                            - e = vapor pressure (air pressure?)
                            - es = 6.112 * 2.71828^(17.67*T / T + 243.5)
                            - T = degrees Celsius
                    - run multiple iterations where the prevailing winds move humidity from one tile to the next
                    - when humidity of a node exceeds 100, it 'rains'
                        - the precipitation for that node is increased by value - 100, 

                
               3) Modify values based on prevailing wind direction and proximity to mountain ranges. Areas on 
                    windward side receive more rainfall, those on leeward side experience a rain shadow effect
               4) Consider the influence of temperature on precipitation. Warmer areas may experience more 
                    convective rainfall, while colder areas may have more snowfall.
               5) Incorporate random variation to add realism and variability to the rainfall patterns
              
                Altitude: Higher altitudes tend to receive more rainfall due to orographic lift, where air is 
                forced upward by mountains, leading to cooling and condensation.

                Prevailing Winds: Winds play a significant role in distributing moisture across the landscape. 
                Windward sides of mountains usually receive more rainfall, while the leeward sides can be in a 
                rain shadow, receiving less precipitation.

                Temperature: Warm air can hold more moisture than cold air. Therefore, areas with higher 
                temperatures may experience more evaporation, leading to more rainfall in certain circumstances, 
                such as the formation of thunderstorms.

                Ocean Currents: Ocean currents influence the moisture content of the air. Areas near warm ocean 
                currents tend to have more moisture in the air, leading to higher precipitation rates.

                Latitude: Generally, areas near the equator receive more rainfall due to the convergence of 
                trade winds and the Intertropical Convergence Zone (ITCZ). However, this can vary based on other 
                factors like wind patterns and ocean currents.

                Local Geography: Features such as valleys, lakes, and forests can also affect local precipitation 
                patterns by influencing airflow and moisture retention.   
             */

            //Create Initial Moisture Map - !!!FLAWED!!!
            //This is entirely onteologic at the moment
            float[,] moistureMap = DampedCosine.GetMoistureMap(heightMap, WorldParameters.SEA_LEVEL, worldGenerator.PRNG);

            //Apply effects of prevailing winds to generate rain shadows
            CreateRainShadows(Features.TerrainData.Mountains, windMap);

            //Pass precipitation values to nodes
            for (int x = 0; x < mapSize; x++)
            {
                for (int y = 0; y < mapSize; y++)
                {
                    WorldTile node = WorldMap.GetNode(x, y);
                    node.SetPrecipitationValues(moistureMap[x, y], ClimateMath.GetPrecipitationZone(moistureMap[x, y]));
                }
            }

            //pass to terrain data
            float[,] adjustedMoistureMap = new float[mapSize, mapSize];
            for (int x = 0; x < mapSize; x++)
            {
                for (int y = 0; y < mapSize; y++)
                {
                    WorldTile node = WorldMap.GetNode(x, y);
                    adjustedMoistureMap[x, y] = node.precipitationValue;
                }
            }
            Features.TerrainData.MoistureMap = adjustedMoistureMap;
        }

        /// <summary>
        /// Removes moisture from leeward side of mountains and moves to windward sides
        /// </summary>
        private void CreateRainShadows(MountainRange[] mountains, Compass[,] windMap)
        {
            for (int i = 0; i < mountains.Length; i++)
            {
                //get dominant prevailing wind direction

                //need to figure out windward side and leeward side

                //from there, the rain shadow should extend out from the center 


            }
        }
        #endregion

        #region - Biomes -
        public void GenerateBiomes()
        {
            for (int x = 0; x < mapSize; x++)
            {
                for (int y = 0; y < mapSize; y++)
                {
                    WorldTile node = WorldMap.GetNode(x, y);
                    if (node.IsLand) node.SetBiome(biomeHelper.GetWhittakerTableBiome(node));
                    else if (!node.hasBiome) node.SetBiome(biomeHelper.GetOceanBiome(node)); //exclude lakes
                }
            }

            OverrideMountainBiomes();
            ConsolidateBiomes(3);
            SaveBiomeMap();
            FindBiomeGroups();
        }

        private void ConsolidateBiomes(int iterations)
        {
            for (int count = 0; count < iterations; count++)
            {
                for (int x = 0; x < mapSize; x++)
                {
                    for (int y = 0; y < mapSize; y++)
                    {
                        CheckBiomeNeighbors(WorldMap.GetNode(x, y));
                    }
                }
            }
        }

        //Check if this tile is surrounded by a biome of different types
        private void CheckBiomeNeighbors(WorldTile tile)
        {
            if (!tile.IsLand) return;
            int differentNeighbors = 0; //number of neighbors with a different biome
            int neighborBiome = 0; //the biome this tile will switch to

            for (int i = 0; i < tile.neighbors_adj.Count; i++)
            {
                if (!tile.neighbors_adj[i].hasBiome) continue;
                if (!tile.neighbors_adj[i].IsLand) continue; //don't adjust to water biomes
                if (tile.neighbors_adj[i].BiomeID == tile.BiomeID) continue;
                neighborBiome = tile.neighbors_adj[i].BiomeID;
                differentNeighbors++;
            }

            if (differentNeighbors >= 3) AdjustNodeBiome(tile, neighborBiome);
        }

        private void AdjustNodeBiome(WorldTile tile, int biomeID)
        {
            var newBiome = biomeHelper.GetBiome(biomeID);

            tile.SetBiome(newBiome);
            var newTemp = Mathf.Clamp(tile.heatValue,
                Temperature.CelsiusToFarenheit(newBiome.MinAvgTemp) / 100f,
                Temperature.CelsiusToFarenheit(newBiome.MaxAvgTemp) / 100f);
            var newZone = ClimateMath.GetHeatIndex(newTemp);
            tile.SetTemperatureValues(newTemp, newZone);

            var newPrecip = Mathf.Clamp(tile.precipitationValue,
                newBiome.MinPrecipitation / 400f,
                newBiome.MaxPrecipitation / 400f);
            var precipZone = ClimateMath.GetPrecipitationZone(newPrecip);
            tile.SetPrecipitationValues(newPrecip, precipZone);
        }

        private void SaveBiomeMap()
        {
            int[,] biomeMap = new int[mapSize, mapSize];
            for (int x = 0; x < mapSize; x++)
            {
                for (int y = 0; y < mapSize; y++)
                {
                    WorldTile node = WorldMap.GetNode(x, y);
                    if (node.hasBiome) biomeMap[x, y] = node.BiomeID;
                }
            }
            Features.TerrainData.BiomeMap = biomeMap;
        }

        private void FindBiomeGroups()
        {
            var biomes = new List<BiomeGroup>();
            for (int x = 0; x < mapSize; x++)
            {
                for (int y = 0; y < mapSize; y++)
                {
                    WorldTile node = WorldMap.GetNode(x, y);
                    if (node.BiomeGroup != null && !biomes.Contains(node.BiomeGroup))
                    {
                        node.BiomeGroup.ID = biomes.Count;
                        biomes.Add(node.BiomeGroup);
                        //Debug.Log("New biome group found! " + node.BiomeGroup.biome + " (" + node.BiomeGroup.ID + ")");
                    }
                }
            }
            Features.TerrainData.BiomeGroups = biomes.ToArray();
        }

        private void OverrideMountainBiomes()
        {
            foreach(var range in Features.TerrainData.Mountains)
            {
                for (int i = 0; i < range.Nodes.Count; i++)
                {
                    range.Nodes[i].SetBiome(biomeHelper.Mountain);
                }
            }
        }
        #endregion

        #region - Resources -
        /// <summary>
        /// Generates maps for precious metals and gemstones
        /// </summary>
        public void GenerateOreDeposits(float coal, float copper, float iron, float silver, float gold, float gems, float mithril, float adamantine)
        {
            float[,] coalMap = PerlinNoise.GenerateHeightMap(mapSize, worldGenerator.PRNG.Next(), noiseScale, octaves, persistence, lacunarity, offset);
            float[,] copperMap = PerlinNoise.GenerateHeightMap(mapSize, worldGenerator.PRNG.Next(), noiseScale, octaves, persistence, lacunarity, offset);
            float[,] ironMap = PerlinNoise.GenerateHeightMap(mapSize, worldGenerator.PRNG.Next(), noiseScale, octaves, persistence, lacunarity, offset);
            float[,] silverMap = PerlinNoise.GenerateHeightMap(mapSize, worldGenerator.PRNG.Next(), noiseScale, octaves, persistence, lacunarity, offset);
            float[,] goldMap = PerlinNoise.GenerateHeightMap(mapSize, worldGenerator.PRNG.Next(), noiseScale, octaves, persistence, lacunarity, offset);
            float[,] mithrilMap = PerlinNoise.GenerateHeightMap(mapSize, worldGenerator.PRNG.Next(), noiseScale, octaves, persistence, lacunarity, offset);
            float[,] adamantineMap = PerlinNoise.GenerateHeightMap(mapSize, worldGenerator.PRNG.Next(), noiseScale, octaves, persistence, lacunarity, offset);
            float[,] gemstoneMap = PerlinNoise.GenerateHeightMap(mapSize, worldGenerator.PRNG.Next(), noiseScale, octaves, persistence, lacunarity, offset);
            float[,] saltMap = PerlinNoise.GenerateHeightMap(mapSize, worldGenerator.PRNG.Next(), noiseScale, octaves, persistence, lacunarity, offset);


            for (int x = 0; x < mapSize; x++)
            {
                for (int y = 0; y < mapSize; y++)
                {
                    if (Features.TerrainData.HeightMap[x, y] < WorldParameters.SEA_LEVEL)
                    {
                        coalMap[x, y] = 0;
                        copperMap[x, y] = 0;
                        ironMap[x, y] = 0;
                        silverMap[x, y] = 0;
                        goldMap[x, y] = 0;
                        gemstoneMap[x, y] = 0;
                        mithrilMap[x, y] = 0;
                        adamantineMap[x, y] = 0;
                        saltMap[x, y] = 0;
                        continue;
                    }

                    if (coalMap[x, y] < 1 - coal) coalMap[x, y] = 0;
                    if (copperMap[x, y] < 1 - copper) copperMap[x, y] = 0;
                    if (ironMap[x, y] < 1 - iron) ironMap[x, y] = 0;
                    if (silverMap[x, y] < 1 - silver) silverMap[x, y] = 0;
                    if (goldMap[x, y] < 1 - gold) goldMap[x, y] = 0;
                    if (gemstoneMap[x, y] < 1 - gems) gemstoneMap[x, y] = 0;
                    if (mithrilMap[x, y] < 1 - mithril) mithrilMap[x, y] = 0;
                    if (adamantineMap[x, y] < 1 - adamantine) adamantineMap[x, y] = 0;
                }
            }

            Features.TerrainData.CoalMap = coalMap;
            Features.TerrainData.CopperMap = copperMap;
            Features.TerrainData.IronMap = ironMap;
            Features.TerrainData.SilverMap = silverMap;
            Features.TerrainData.GoldMap = goldMap;
            Features.TerrainData.MithrilMap = mithrilMap;
            Features.TerrainData.AdmanatineMap = adamantineMap;
            Features.TerrainData.GemstoneMap = gemstoneMap;
        }
        #endregion

        private void OnValidate()
        {
            if (lacunarity < 1) lacunarity = 1;
            if (octaves < 0) octaves = 0;
        }
    }
}


public enum Compass { North, NorthEast, East, SouthEast, South, SouthWest, West, NorthWest }