using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Valve.VR.InteractionSystem;

public class TerrainGenerator : MonoBehaviour
{
    public int maxWorldDimension;
    public float maxHeight;
    public int heightMapResolution;
    public float perlinSampleSize;
    public Transform playerTransform;

    private float[,][,] heights;
    private Zone[,] zones;
    private bool isLoading = false;
    private bool isUnloading = false;
    private float gridSize;
    private float unloadDistance;
    private float minDisplayDistance;

    private class Zone
    {
        public bool loaded = false;
        public Terrain terrain;
        public GameObject root;
        public int xBase;
        public int yBase;
    }

    void Awake()
    {
        zones = new Zone[maxWorldDimension, maxWorldDimension];
        for (int i = 0; i < maxWorldDimension; i++)
        {
            for (int j = 0; j < maxWorldDimension; j++)
            {
                zones[i, j] = new Zone();
            }
        }
        TerrainData modelTerrainData = new TerrainData();
        modelTerrainData.heightmapResolution = heightMapResolution;
        modelTerrainData.size = new Vector3(modelTerrainData.size.x, maxHeight, modelTerrainData.size.z);
        int height = modelTerrainData.heightmapHeight;
        int width = modelTerrainData.heightmapWidth;
        heights = new float[maxWorldDimension, maxWorldDimension][,];
        gridSize = modelTerrainData.size.x;
        unloadDistance = gridSize * 1.8f;
        minDisplayDistance = gridSize * 0.7f;
        int xIterations = 0;
        int yIterations = 0;
        for(int i = 0; i < maxWorldDimension; i++)
        {
            for(int j = 0; j < maxWorldDimension; j++)
            {
                // Randomize Perlin sample?
                heights[i, j] = new float[height, width];
                for(int y = 0; y < height; y++)
                {
                    for(int x = 0; x < width; x++)
                    {
                        float perlinY = ((i * height + y - yIterations) * (1.0f / (height * maxWorldDimension))) * perlinSampleSize;
                        float perlinX = ((j * width + x - xIterations) * (1.0f / (width * maxWorldDimension))) * perlinSampleSize;
                        heights[i, j][y, x] = Mathf.PerlinNoise(perlinY, perlinX);
                    }
                }
                xIterations++;
            }
            xIterations = 0;
            yIterations++;
        }
    }

    void Update()
    {
        for (int y = 0; y < maxWorldDimension; y++)
        {
            for (int x = 0; x < maxWorldDimension; x++)
            {
                Zone zone = zones[y, x];
                if (zone.loaded)
                {
                    float sqrDistance = GetSqrDistance(playerTransform.position, x, y);
                    if (sqrDistance > unloadDistance * unloadDistance)
                    {
                        StartCoroutine(UnloadZone(x, y));
                        return;
                    }
                }
            }
        }
        int closestX, closestY;
        GetClosestZone(playerTransform.position, minDisplayDistance, out closestX, out closestY);

        if (closestX != -1)
            StartCoroutine(LoadZone(closestX, closestY));
    }

    List<Zone> GetAdjacentZones(int x, int y)
    {
        List<Zone> adjZones = new List<Zone>();
        for(int i = Mathf.Max(0, y); i < Mathf.Min(maxWorldDimension - Mathf.Max(0, y), 5); i++)
        {
            for(int j = Mathf.Max(0, x); j < Mathf.Min(maxWorldDimension - Mathf.Max(0, x), 5); j++)
            {
                Zone z = zones[y, x];
                if (z != null) adjZones.Add(z);
            }
        }
        return adjZones;
    }

    void GetClosestZone(Vector3 position, float closestDistance, out int closestX, out int closestY)
    {
        closestX = -1;
        closestY = -1;
        closestDistance = closestDistance * closestDistance;
        for (int y = 0; y < maxWorldDimension; y++)
        {
            for (int x = 0; x < maxWorldDimension; x++)
            {
                Zone zone = zones[y, x];
                if (!zone.loaded)
                {
                    float sqrDistance = GetSqrDistance(position, x, y);
                    if (sqrDistance < closestDistance)
                    {
                        closestDistance = sqrDistance;
                        closestX = x;
                        closestY = y;
                    }
                }
            }
        }
    }

    float GetSqrDistance(Vector3 position, int x, int y)
    {
        float minx = x * gridSize;
        float maxx = (x + 1) * gridSize;
        float miny = y * gridSize;
        float maxy = (y + 1) * gridSize;

        float xDistance = 0.0F;
        float yDistance = 0.0F;

        if (position.x < minx)
            xDistance = Mathf.Abs(minx - position.x);
        else if (position.x > maxx)
            xDistance = Mathf.Abs(maxx - position.x);

        if (position.z < miny)
            yDistance = Mathf.Abs(miny - position.z);
        else if (position.z > maxy)
            yDistance = Mathf.Abs(maxy - position.z);

        return xDistance * xDistance + yDistance * yDistance;
    }

    IEnumerator LoadZone(int x, int y)
    {
        if (isLoading)
        {
            Debug.LogError("Already loading zone");
            yield break;
        }

        Zone zone = zones[y, x];
        zone.loaded = true;

        isLoading = true;
        
        TerrainData terrainData = new TerrainData();
        terrainData.heightmapResolution = heightMapResolution;
        terrainData.size = new Vector3(terrainData.size.x, maxHeight, terrainData.size.z);
        terrainData.SetHeights(0, 0, heights[y, x]);
        GameObject root = Terrain.CreateTerrainGameObject(terrainData);
        Terrain terrain = root.GetComponent<Terrain>();
        terrain.gameObject.AddComponent<TeleportAreaTerrain>();
        terrain.transform.position = new Vector3(terrainData.size.x * x, 0.0f, terrainData.size.z * y);
        zone.terrain = terrain;
        zone.root = root;

        // Hookup neighboring terrains so there are no seams in the LOD
        for (int yi = Mathf.Max(y - 1, 0); yi < Mathf.Min(y + 2, maxWorldDimension); yi++)
        {
            for (int xi = Mathf.Max(x - 1, 0); xi < Mathf.Min(x + 2, maxWorldDimension); xi++)
            {
                Terrain curTerrain = GetLoadedTerrain(xi, yi);
                if (curTerrain != null)
                {
                    Terrain left = GetLoadedTerrain(xi - 1, yi);
                    Terrain right = GetLoadedTerrain(xi + 1, yi);
                    Terrain top = GetLoadedTerrain(xi, yi + 1);
                    Terrain bottom = GetLoadedTerrain(xi, yi - 1);
                    curTerrain.SetNeighbors(left, top, right, bottom);
                }
            }
        }

        isLoading = false;
    }

    IEnumerator UnloadZone(int x, int y)
    {
        isUnloading = true;

        Zone zone = zones[y, x];
        zone.loaded = false;
        Destroy(zone.root);
        zone.terrain = null;
   
        yield return 0;

        isUnloading = false;
    }

    Terrain GetLoadedTerrain(int x, int y)
    {
        if ((x >= 0 && x < maxWorldDimension) && (y >= 0 && y < maxWorldDimension))
        {
            Zone zone = zones[y, x];
            if (zone.loaded)
                return zone.terrain;
        }

        return null;
    }
}
