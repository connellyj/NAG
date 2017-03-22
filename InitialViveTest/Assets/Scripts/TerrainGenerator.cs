using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Valve.VR.InteractionSystem;

public class TerrainGenerator : MonoBehaviour
{
    public float gridSize;
    public float unloadDistance;
    public float minDisplayDistance;
    public int maxWorldDimension;
    public float tileHeight;
    public float tileWidth;
    public float maxHeight;
    public int heightMapResolution;
    public Transform playerTransform;

    private float[,][,] heights;
    private Zone[,] zones;
    private bool isLoading = false;
    private bool isUnloading = false;

    private class Zone
    {
        public bool loaded = false;
        public Terrain terrain;
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
        modelTerrainData.size = new Vector3(tileWidth, maxHeight, tileHeight);
        modelTerrainData.heightmapResolution = heightMapResolution;
        int height = modelTerrainData.heightmapHeight;
        int width = modelTerrainData.heightmapWidth;
        heights = new float[maxWorldDimension, maxWorldDimension][,];
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
                        heights[i, j][y, x] = Mathf.PerlinNoise(i * height + y, j * width + x);
                    }
                }
            }
        }
        string toPrint = "";
        for(int i = 0; i < height; i++)
        {
            for(int j = 0; j < width; j++)
            {
                toPrint += heights[0, 0][i, j] + "  ";
            }
            toPrint += "\n";
        }
        Debug.Log(toPrint);
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
        
        int baseX, baseY;
        GetHeightBases(x, y, out baseX, out baseY);
        TerrainData terrainData = new TerrainData();
        terrainData.size = new Vector3(tileWidth, maxHeight, tileHeight);
        terrainData.heightmapResolution = heightMapResolution;
        terrainData.SetHeights(baseX, baseY, heights[y, x]);
        Terrain terrain = Terrain.CreateTerrainGameObject(terrainData).GetComponent<Terrain>();
        terrain.gameObject.AddComponent<TeleportAreaTerrain>();

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
        Destroy(zone.terrain);
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

    void GetHeightBases(int x, int y, out int baseX, out int baseY)
    {
        if (x + 1 < maxWorldDimension && zones[y, x + 1].loaded)
        {
            baseY = zones[y, x + 1].yBase;
            int width = zones[y, x + 1].terrain.terrainData.heightmapWidth;
            baseX = zones[y, x + 1].xBase - width;
        }
        else if ( x - 1 >= 0 && zones[y, x - 1].loaded)
        {
            baseY = zones[y, x - 1].yBase;
            int width = zones[y, x - 1].terrain.terrainData.heightmapWidth;
            baseX = zones[y, x - 1].xBase + width;
        }
        else if ( y + 1 < maxWorldDimension && zones[y + 1, x].loaded)
        {
            baseX = zones[y + 1, x].xBase;
            int height = zones[y + 1, x].terrain.terrainData.heightmapHeight;
            baseY = zones[y + 1, x].yBase - height;
        }
        else if (y - 1 >= 0 && zones[y - 1, x].loaded)
        {
            baseX = zones[y - 1, x].xBase;
            int height = zones[y - 1, x].terrain.terrainData.heightmapHeight;
            baseY = zones[y - 1, x].yBase + height;
        }
        else
        {
            baseY = 0;
            baseX = 0;
        }
    }
}
