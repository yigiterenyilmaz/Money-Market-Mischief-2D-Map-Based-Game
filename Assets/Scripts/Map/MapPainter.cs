using UnityEngine;
    using System.Collections.Generic;

    [RequireComponent(typeof(MapDecorPlacer))]
    public class MapPainter : MonoBehaviour
    {
        [Header("References")]
        public MapGenerator mapGenerator;
        public SpriteRenderer mapRenderer;
        public BiomePaintSettings settings;

        [Header("Water Depth")]
        [Range(5, 60)] public int waterDepthRange = 30;
        [Range(2, 6)]  public int waterDepthSteps = 4;

        [Header("Region Transitions")]
        [Tooltip("Tile width of the transition band between two different regions")]
        [Range(1, 80)] public int transitionWidth = 30;

        [Header("Beaches")]
        [Tooltip("Chance (0-1) that a coastline segment gets a beach")]
        [Range(0f, 1f)] public float beachChance = 0.5f;
        [Tooltip("How many tiles inland the beach extends")]
        [Range(1, 40)] public int beachWidth = 10;

        private MapDecorPlacer decorPlacer;
        private Texture2D mapTexture;
        private float[,] waterDistMap;
        private float[,] borderDist;
        private int[,]   nearestOther;
        private float[,] beachDistMap;
        private int[,]   shoreDistField;

        void Awake() { decorPlacer = GetComponent<MapDecorPlacer>(); }
        void OnEnable()  { if (mapGenerator != null) mapGenerator.OnMapGenerated += Paint; }
        void OnDisable() { if (mapGenerator != null) mapGenerator.OnMapGenerated -= Paint; }

        public void Paint()
        {
            if (settings == null) { Debug.LogError("MapPainter: BiomePaintSettings not assigned."); return; }

            int w = mapGenerator.width;
            int h = mapGenerator.height;

            BuildWaterDistanceField(w, h);
            BuildBorderDistanceField(w, h);
            BuildBeachMap(w, h);

            if (mapTexture != null) Destroy(mapTexture);
            mapTexture = new Texture2D(w, h, TextureFormat.RGBA32, false);
            mapTexture.filterMode = FilterMode.Point;

            float seed = Random.Range(0f, 9999f);
            Color[] pixels = new Color[w * h];

            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    Color c = mapGenerator.IsLand(x, y)
                        ? PaintLandWithTransition(x, y, seed)
                        : PaintWater(x, y, seed, w, h);

                    float fog = mapGenerator.GetFog(x, y);
                    if (fog > 0f) c = Color.Lerp(c, settings.fogColor, fog);

                    pixels[x + y * w] = c;
                }
            }

            mapTexture.SetPixels(pixels);
            mapTexture.Apply();
            ApplyToRenderer(mapTexture);
            decorPlacer.Repaint(mapGenerator, settings, mapTexture);
        }

        // -------------------------------------------------------------------------
        // SHORE DISTANCE FIELD
        // -------------------------------------------------------------------------

        void BuildShoreDistField(int w, int h)
        {
            shoreDistField = new int[w, h];
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                    shoreDistField[x, y] = int.MaxValue;

            int[] dx4 = { 1, -1, 0, 0 };
            int[] dy4 = { 0, 0, 1, -1 };

            Queue<Vector2Int> queue = new Queue<Vector2Int>();

            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                {
                    if (!mapGenerator.IsLand(x, y)) continue;
                    for (int i = 0; i < 4; i++)
                    {
                        int nx = x + dx4[i], ny = y + dy4[i];
                        if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;
                        if (!mapGenerator.IsLand(nx, ny))
                        {
                            shoreDistField[x, y] = 0;
                            queue.Enqueue(new Vector2Int(x, y));
                            break;
                        }
                    }
                }

            while (queue.Count > 0)
            {
                var pos = queue.Dequeue();
                int d = shoreDistField[pos.x, pos.y];
                for (int i = 0; i < 4; i++)
                {
                    int nx = pos.x + dx4[i], ny = pos.y + dy4[i];
                    if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;
                    if (!mapGenerator.IsLand(nx, ny)) continue;
                    if (shoreDistField[nx, ny] <= d + 1) continue;
                    shoreDistField[nx, ny] = d + 1;
                    queue.Enqueue(new Vector2Int(nx, ny));
                }
            }
        }

        // -------------------------------------------------------------------------
        // BEACH MAP
        // -------------------------------------------------------------------------

        void BuildBeachMap(int w, int h)
        {
            BuildShoreDistField(w, h);

            beachDistMap = new float[w, h];
            float beachSeed = 7777f;

            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                {
                    beachDistMap[x, y] = -1f;
                    if (!mapGenerator.IsLand(x, y)) continue;
                    // No beaches on sea rocks
                    if (mapGenerator.IsSeaRock(x, y)) continue;

                    int sd = shoreDistField[x, y];
                    if (sd == int.MaxValue || sd > beachWidth) continue;

                    float selector = Mathf.PerlinNoise(x * 0.015f + beachSeed, y * 0.015f + beachSeed);

                    float inlandFade = 1f - ((float)sd / beachWidth);
                    selector *= inlandFade;

                    if (selector > (1f - beachChance))
                        beachDistMap[x, y] = (float)sd / beachWidth;
                }
        }

        // -------------------------------------------------------------------------
        // BORDER DISTANCE FIELD
        // -------------------------------------------------------------------------

        void BuildBorderDistanceField(int w, int h)
        {
            borderDist   = new float[w, h];
            nearestOther = new int[w, h];

            int[,] dist        = new int[w, h];
            int[,] sourceOther = new int[w, h];

            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                {
                    dist[x, y]        = int.MaxValue;
                    sourceOther[x, y] = 0;
                }

            int[] dx4 = { 1, -1, 0, 0 };
            int[] dy4 = { 0, 0, 1, -1 };

            Queue<Vector2Int> queue = new Queue<Vector2Int>();

            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                {
                    if (!mapGenerator.IsLand(x, y)) continue;
                    int myBiome = mapGenerator.GetBiome(x, y);
                    // Sea rocks (biome 5) don't participate in transitions
                    if (myBiome == 5) continue;

                    for (int i = 0; i < 4; i++)
                    {
                        int nx = x + dx4[i], ny = y + dy4[i];
                        if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;
                        if (!mapGenerator.IsLand(nx, ny)) continue;

                        int neighborBiome = mapGenerator.GetBiome(nx, ny);
                        // Ignore sea rock neighbors — no blending with them
                        if (neighborBiome == 5) continue;
                        if (neighborBiome != myBiome)
                        {
                            if (dist[x, y] > 0)
                            {
                                dist[x, y]        = 0;
                                sourceOther[x, y] = neighborBiome;
                                queue.Enqueue(new Vector2Int(x, y));
                            }
                            break;
                        }
                    }
                }

            while (queue.Count > 0)
            {
                Vector2Int pos = queue.Dequeue();
                int d       = dist[pos.x, pos.y];
                int other   = sourceOther[pos.x, pos.y];
                int myBiome = mapGenerator.GetBiome(pos.x, pos.y);

                for (int i = 0; i < 4; i++)
                {
                    int nx = pos.x + dx4[i], ny = pos.y + dy4[i];
                    if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;
                    if (!mapGenerator.IsLand(nx, ny)) continue;
                    if (mapGenerator.GetBiome(nx, ny) != myBiome) continue;
                    if (dist[nx, ny] <= d + 1) continue;

                    dist[nx, ny]        = d + 1;
                    sourceOther[nx, ny] = other;
                    queue.Enqueue(new Vector2Int(nx, ny));
                }
            }

            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                {
                    borderDist[x, y]   = dist[x, y] == int.MaxValue
                                         ? 1f
                                         : Mathf.Clamp01((float)dist[x, y] / transitionWidth);
                    nearestOther[x, y] = sourceOther[x, y];
                }
        }

        // -------------------------------------------------------------------------
        // LAND PAINTING WITH TRANSITION
        // -------------------------------------------------------------------------

        Color PaintLandWithTransition(int x, int y, float seed)
        {
            int   myBiome    = mapGenerator.GetBiome(x, y);
            float d          = borderDist[x, y];
            int   otherBiome = nearestOther[x, y];

            // Sea rocks: no beach, no biome transition — just paint rock
            if (myBiome == 5)
                return PaintSeaRock(x, y, seed);

            // Beach overrides everything — paint beach and blend into region inland
            float beachD = beachDistMap[x, y];
            if (beachD >= 0f)
            {
                Color beachColor  = PaintBeach(x, y, seed);
                float blendT      = Mathf.SmoothStep(0f, 1f, beachD);
                Color regionColor = GetBiomeColor(myBiome, x, y, seed);
                return Color.Lerp(beachColor, regionColor, blendT);
            }

            if (d >= 1f || otherBiome == 0)
                return GetBiomeColor(myBiome, x, y, seed);

            if (myBiome > otherBiome)
                return GetBiomeColor(myBiome, x, y, seed);

            float warp  = Perlin(x, y, seed + 3000f, 0.05f) * 0.4f - 0.2f;
            float t     = Mathf.Clamp01(d + warp);
            float chaos = Perlin(x, y, seed + 4000f, 0.09f) * 0.3f - 0.15f;
            t = Mathf.Clamp01(t + chaos);
            t = Mathf.SmoothStep(0f, 1f, t);

            Color myColor    = GetBiomeColor(myBiome,    x, y, seed);
            Color otherColor = GetBiomeColor(otherBiome, x, y, seed);

            return Color.Lerp(otherColor, myColor, t);
        }

        Color GetBiomeColor(int biome, int x, int y, float seed)
        {
            switch (biome)
            {
                case 1: return PaintUrban(x, y, seed);
                case 2: return PaintCities(x, y, seed);
                case 3: return PaintIndustrial(x, y, seed);
                case 4: return PaintAgricultural(x, y, seed);
                case 5: return PaintSeaRock(x, y, seed);
                default: return settings.waterDeep;
            }
        }

        // -------------------------------------------------------------------------
        // BIOME PAINT METHODS
        // -------------------------------------------------------------------------

        Color PaintBeach(int x, int y, float seed)
        {
            float n1 = Mathf.PerlinNoise(x * 0.025f + seed + 2000f, y * 0.025f + seed + 2000f);
            float n2 = Mathf.PerlinNoise(x * 0.06f  + seed + 2040f, y * 0.06f  + seed + 2040f) * 0.5f;
            float n3 = Mathf.PerlinNoise(x * 0.13f  + seed + 2080f, y * 0.13f  + seed + 2080f) * 0.25f;
            float n  = (n1 + n2 + n3) / 1.75f;

            if (n < 0.38f) return settings.beachDark;
            if (n < 0.46f) return Color.Lerp(settings.beachDark, settings.beachLight, 0.33f);
            if (n < 0.56f) return settings.beachLight;
            if (n < 0.64f) return Color.Lerp(settings.beachDark, settings.beachLight, 0.66f);
            return settings.beachDark;
        }

        Color PaintUrban(int x, int y, float seed)
        {
            float n1 = Mathf.PerlinNoise(x * 0.018f + seed,       y * 0.018f + seed);
            float n2 = Mathf.PerlinNoise(x * 0.045f + seed + 40f, y * 0.045f + seed + 40f) * 0.5f;
            float n3 = Mathf.PerlinNoise(x * 0.11f  + seed + 80f, y * 0.11f  + seed + 80f) * 0.25f;
            float n  = (n1 + n2 + n3) / 1.75f;

            if (n < 0.35f) return settings.urbanDark;
            if (n < 0.42f) return Color.Lerp(settings.urbanDark, settings.urbanLight, 0.33f);
            if (n < 0.52f) return settings.urbanLight;
            if (n < 0.60f) return Color.Lerp(settings.urbanDark, settings.urbanLight, 0.66f);
            return settings.urbanDark;
        }

        Color PaintAgricultural(int x, int y, float seed)
        {
            float n1 = Mathf.PerlinNoise(x * 0.022f + seed + 100f, y * 0.022f + seed + 100f);
            float n2 = Mathf.PerlinNoise(x * 0.055f + seed + 140f, y * 0.055f + seed + 140f) * 0.5f;
            float n3 = Mathf.PerlinNoise(x * 0.13f  + seed + 180f, y * 0.13f  + seed + 180f) * 0.25f;
            float n  = (n1 + n2 + n3) / 1.75f;

            if (n < 0.32f) return settings.agriculturalDark;
            if (n < 0.40f) return Color.Lerp(settings.agriculturalDark, settings.agriculturalLight, 0.33f);
            if (n < 0.55f) return settings.agriculturalLight;
            if (n < 0.63f) return Color.Lerp(settings.agriculturalDark, settings.agriculturalLight, 0.66f);
            return settings.agriculturalDark;
        }

        Color PaintCities(int x, int y, float seed)
        {
            float n1 = Mathf.PerlinNoise(x * 0.014f + seed + 300f, y * 0.014f + seed + 300f);
            float n2 = Mathf.PerlinNoise(x * 0.038f + seed + 340f, y * 0.038f + seed + 340f) * 0.5f;
            float n3 = Mathf.PerlinNoise(x * 0.09f  + seed + 380f, y * 0.09f  + seed + 380f) * 0.25f;
            float n  = (n1 + n2 + n3) / 1.75f;

            if (n < 0.38f) return settings.citiesDark;
            if (n < 0.46f) return Color.Lerp(settings.citiesDark, settings.citiesLight, 0.33f);
            if (n < 0.56f) return settings.citiesLight;
            if (n < 0.64f) return Color.Lerp(settings.citiesDark, settings.citiesLight, 0.66f);
            return settings.citiesDark;
        }

        Color PaintIndustrial(int x, int y, float seed)
        {
            float n1 = Mathf.PerlinNoise(x * 0.020f + seed + 600f, y * 0.020f + seed + 600f);
            float n2 = Mathf.PerlinNoise(x * 0.055f + seed + 640f, y * 0.055f + seed + 640f) * 0.5f;
            float n3 = Mathf.PerlinNoise(x * 0.14f  + seed + 680f, y * 0.14f  + seed + 680f) * 0.25f;
            float n  = (n1 + n2 + n3) / 1.75f;

            float crack = Mathf.PerlinNoise(x * 0.06f + seed + 700f, y * 0.06f + seed + 700f);
            if (crack > 0.76f) return settings.industrialCrack;

            if (n < 0.36f) return settings.industrialDark;
            if (n < 0.44f) return Color.Lerp(settings.industrialDark, settings.industrialLight, 0.33f);
            if (n < 0.54f) return settings.industrialLight;
            if (n < 0.62f) return Color.Lerp(settings.industrialDark, settings.industrialLight, 0.66f);
            return settings.industrialDark;
        }

        Color PaintSeaRock(int x, int y, float seed)
        {
            float n1 = Mathf.PerlinNoise(x * 0.025f + seed + 800f, y * 0.025f + seed + 800f);
            float n2 = Mathf.PerlinNoise(x * 0.065f + seed + 840f, y * 0.065f + seed + 840f) * 0.5f;
            float n3 = Mathf.PerlinNoise(x * 0.15f  + seed + 880f, y * 0.15f  + seed + 880f) * 0.25f;
            float n  = (n1 + n2 + n3) / 1.75f;

            // Crevice noise — dark cracks across the rock surface
            float crack = Mathf.PerlinNoise(x * 0.08f + seed + 900f, y * 0.08f + seed + 900f);
            if (crack > 0.74f) return settings.seaRockCrack;

            if (n < 0.36f) return settings.seaRockDark;
            if (n < 0.44f) return Color.Lerp(settings.seaRockDark, settings.seaRockLight, 0.33f);
            if (n < 0.54f) return settings.seaRockLight;
            if (n < 0.62f) return Color.Lerp(settings.seaRockDark, settings.seaRockLight, 0.66f);
            return settings.seaRockDark;
        }

        // -------------------------------------------------------------------------
        // WATER
        // -------------------------------------------------------------------------

        void BuildWaterDistanceField(int w, int h)
        {
            waterDistMap = new float[w, h];
            int[,] dist  = new int[w, h];

            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                    dist[x, y] = mapGenerator.IsLand(x, y) ? 0 : -1;

            Queue<Vector2Int> queue = new Queue<Vector2Int>();
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                    if (mapGenerator.IsLand(x, y))
                        queue.Enqueue(new Vector2Int(x, y));

            int[] dx4 = { 1, -1, 0, 0 };
            int[] dy4 = { 0, 0, 1, -1 };

            while (queue.Count > 0)
            {
                Vector2Int pos = queue.Dequeue();
                int d = dist[pos.x, pos.y];
                for (int i = 0; i < 4; i++)
                {
                    int nx = pos.x + dx4[i], ny = pos.y + dy4[i];
                    if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;
                    if (dist[nx, ny] != -1) continue;
                    dist[nx, ny] = d + 1;
                    queue.Enqueue(new Vector2Int(nx, ny));
                }
            }

            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                    waterDistMap[x, y] = Mathf.Clamp01((float)dist[x, y] / waterDepthRange);
        }

        Color PaintWater(int x, int y, float seed, int w, int h)
        {
            float coastDist  = waterDistMap[x, y];
            float cx         = (x - w * 0.5f) / (w * 0.5f);
            float cy         = (y - h * 0.5f) / (h * 0.5f);
            float radialDist = Mathf.Clamp01(Mathf.Sqrt(cx * cx + cy * cy));
            float depth      = Mathf.Max(coastDist, radialDist);
            float jitter     = Perlin(x, y, seed + 1200f, 0.04f) * 0.18f - 0.09f;
            depth = Mathf.Clamp01(depth + jitter);
            float stepped = Mathf.Floor(depth * waterDepthSteps) / (waterDepthSteps - 1);
            return Color.Lerp(settings.waterShallow, settings.waterDeep, Mathf.Clamp01(stepped));
        }

        // -------------------------------------------------------------------------
        // HELPERS
        // -------------------------------------------------------------------------

        static float Perlin(int x, int y, float seed, float scale)
            => Mathf.PerlinNoise(x * scale + seed, y * scale + seed);

        void ApplyToRenderer(Texture2D tex)
        {
            if (mapRenderer == null) return;
            if (mapRenderer.sprite != null) Destroy(mapRenderer.sprite);
            Sprite sprite = Sprite.Create(tex,
                new Rect(0, 0, mapGenerator.width, mapGenerator.height),
                new Vector2(0.5f, 0.5f), 100f);
            mapRenderer.sprite = sprite;
            mapRenderer.enabled = true;
        }
    }