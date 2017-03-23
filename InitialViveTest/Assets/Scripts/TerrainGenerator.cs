using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Valve.VR.InteractionSystem;

public class TerrainGenerator : MonoBehaviour
{
    public int maxWorldDimension;
    public float maxHeight;
    public int heightMapResolution;
    public float perlinSampleSize;
    public Transform playerTransform;
    public Texture2D[] terrainTexture;

    private float[,][,] heights;
    private Zone[,] zones;
    private bool isLoading = false;
    private float gridSize;
    private float unloadDistance;
    private float minDisplayDistance;
    private int terrainMapHeight;
    private int terrainMapWidth;

    private class Zone
    {
        public bool loaded = false;
        public Terrain terrain;
        public GameObject root;
        public TerrainData terrainData;
    }

    private enum Direction { NORTH, SOUTH, EAST, WEST}

    void Awake()
    {
        CreateZones();
        InitConstants();
        InitHeightMaps();
        InitEdgeHeightMaps();
        InitAllTerrainData();
        Zone firstZone = zones[1, 1];
        float width = firstZone.terrainData.size.x;
        float height = firstZone.terrainData.size.z;
        float elevation = firstZone.terrainData.GetHeight(10, 10);
        playerTransform.position = new Vector3(width * 1.1f, elevation, height * 1.1f);
    }

    void Update()
    {
        CheckUnloadZones();
        CheckLoadZones();
    }

    private void GetClosestZone(Vector3 position, float closestDistance, out int closestX, out int closestY)
    {
        closestX = -1;
        closestY = -1;
        closestDistance = closestDistance * closestDistance;
        for (int y = 0; y < maxWorldDimension + 2; y++)
        {
            for (int x = 0; x < maxWorldDimension + 2; x++)
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

    private float GetSqrDistance(Vector3 position, int x, int y)
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

    private IEnumerator LoadZone(int x, int y)
    {
        if (isLoading)
        {
            Debug.LogError("Already loading zone");
            yield break;
        }
        isLoading = true;
        Zone zone = zones[y, x];
        zone.loaded = true;
        bool isEdge = (x == 0 || y == 0 || x == maxWorldDimension + 1 || y == maxWorldDimension + 1);
        CreateTerrain(zone, x, y, isEdge);
        ConnectNeighborTerrains(x, y);
        isLoading = false;
    }

    private IEnumerator UnloadZone(int x, int y)
    {
        Zone zone = zones[y, x];
        zone.loaded = false;
        Destroy(zone.root);
        zone.terrain = null;
        zone.root = null;
        yield return 0;
    }

    private Terrain GetLoadedTerrain(int x, int y)
    {
        if ((x >= 0 && x < maxWorldDimension + 2) && (y >= 0 && y < maxWorldDimension + 2))
        {
            Zone zone = zones[y, x];
            if (zone.loaded)
                return zone.terrain;
        }

        return null;
    }

    private float[,] initEdgeHeightMap(int terrainY, int terrainX, Direction edgeSide)
    {
        float[,] mountainHeights = new float[terrainMapHeight, terrainMapWidth];
        if(edgeSide == Direction.NORTH || edgeSide == Direction.EAST)
        {
            for (int y = 0; y < terrainMapHeight; y++)
            {
                for (int x = 0; x < terrainMapWidth; x++)
                {
                    if (edgeSide == Direction.NORTH)
                    {
                        if (y < terrainMapHeight / 32) mountainHeights[y, x] = PerlinSample(terrainX, terrainY, x, y);
                        else mountainHeights[y, x] = mountainHeights[y - 1, x] + 0.05f;
                    }
                    else if (edgeSide == Direction.EAST)
                    {
                        if (x < terrainMapWidth / 32) mountainHeights[y, x] = PerlinSample(terrainX, terrainY, x, y);
                        else mountainHeights[y, x] = mountainHeights[y, x - 1] + 0.05f;
                    }
                }
            }
        }else
        {
            for (int y = terrainMapHeight - 1; y >= 0; y--)
            {
                for (int x = terrainMapWidth - 1; x >= 0; x--)
                {
                    if (edgeSide == Direction.SOUTH)
                    {
                        if (y > (terrainMapHeight * 31) / 32) mountainHeights[y, x] = PerlinSample(terrainX, terrainY, x, y);
                        else mountainHeights[y, x] = mountainHeights[y + 1, x] + 0.05f;
                    }
                    else if (edgeSide == Direction.WEST)
                    {
                        if (x > (terrainMapWidth * 31) / 32) mountainHeights[y, x] = PerlinSample(terrainX, terrainY, x, y);
                        else mountainHeights[y, x] = mountainHeights[y, x + 1] + 0.05f;
                    }
                }
            }
        }
        return mountainHeights;
    }

    private float PerlinSample(int terrainX, int terrainY, int xCoord, int yCoord)
    {
        float perlinY = ((terrainY * terrainMapHeight + yCoord - terrainY) * (1.0f / (terrainMapHeight * (maxWorldDimension + 2)))) * perlinSampleSize;
        float perlinX = ((terrainX * terrainMapWidth + xCoord - terrainX) * (1.0f / (terrainMapWidth * (maxWorldDimension + 2)))) * perlinSampleSize;
        return Mathf.PerlinNoise(perlinY, perlinX);
    }

    private void InitEdgeHeightMaps()
    {
        for (int i = 0; i < maxWorldDimension + 2; i++)
        {
            float[,] mountain = initEdgeHeightMap(0, i, Direction.SOUTH);
            heights[0, i] = mountain;
            mountain = initEdgeHeightMap(maxWorldDimension + 1, i, Direction.NORTH);
            heights[maxWorldDimension + 1, i] = mountain;
            mountain = initEdgeHeightMap(i, 0, Direction.WEST);
            heights[i, 0] = mountain;
            mountain = initEdgeHeightMap(i, maxWorldDimension + 1, Direction.EAST);
            heights[i, maxWorldDimension + 1] = mountain;
        }
    }

    private void InitHeightMaps()
    {
        for (int i = 1; i < maxWorldDimension + 1; i++)
        {
            for (int j = 1; j < maxWorldDimension + 1; j++)
            {
                // Randomize Perlin sample?
                heights[i, j] = new float[terrainMapHeight, terrainMapWidth];
                for (int y = 0; y < terrainMapHeight; y++)
                {
                    for (int x = 0; x < terrainMapWidth; x++)
                    {
                        heights[i, j][y, x] = PerlinSample(j, i, x, y);
                    }
                }
            }
        }
    }

    private void CreateZones()
    {
        zones = new Zone[maxWorldDimension + 2, maxWorldDimension + 2];
        for (int i = 0; i < maxWorldDimension + 2; i++)
        {
            for (int j = 0; j < maxWorldDimension + 2; j++)
            {
                zones[i, j] = new Zone();
            }
        }
    }

    private void InitConstants()
    {
        TerrainData modelTerrainData = new TerrainData();
        modelTerrainData.heightmapResolution = heightMapResolution;
        modelTerrainData.size = new Vector3(modelTerrainData.size.x, maxHeight, modelTerrainData.size.z);
        terrainMapHeight = modelTerrainData.heightmapHeight;
        terrainMapWidth = modelTerrainData.heightmapWidth;
        heights = new float[maxWorldDimension + 2, maxWorldDimension + 2][,];
        gridSize = modelTerrainData.size.x;
        unloadDistance = gridSize * 1.8f;
        minDisplayDistance = gridSize * 0.9f;
    }

    private void CheckUnloadZones()
    {
        // Make more efficient?
        for (int y = 0; y < maxWorldDimension + 2; y++)
        {
            for (int x = 0; x < maxWorldDimension + 2; x++)
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
    }

    private void CheckLoadZones()
    {
        int closestX, closestY;
        GetClosestZone(playerTransform.position, minDisplayDistance, out closestX, out closestY);
        if (closestX != -1) StartCoroutine(LoadZone(closestX, closestY));
    }

    private float[,,] InitTextures(TerrainData terrainData)
    {
        // put textures in terrain data
        SplatPrototype[] tex = new SplatPrototype[4];
        for (int i = 0; i < 4; i++)
        {
            tex[i] = new SplatPrototype();
            tex[i].texture = terrainTexture[i];
            tex[i].tileSize = new Vector2(1, 1);
        }
        terrainData.splatPrototypes = tex;

        // Splatmap data is stored internally as a 3d array of floats, so declare a new empty array ready for your custom splatmap data:
        float[,,] splatmapData = new float[terrainData.alphamapWidth, terrainData.alphamapHeight, terrainData.alphamapLayers];

        for (int i = 0; i < terrainData.alphamapHeight; i++)
        {
            for (int j = 0; j < terrainData.alphamapWidth; j++)
            {
                // Normalise x/y coordinates to range 0-1 
                float y_01 = (float)i / (float)terrainData.alphamapHeight;
                float x_01 = (float)j / (float)terrainData.alphamapWidth;

                // Sample the height at this location (note GetHeight expects int coordinates corresponding to locations in the heightmap array)
                float height = terrainData.GetHeight(Mathf.RoundToInt(y_01 * terrainData.heightmapHeight), Mathf.RoundToInt(x_01 * terrainData.heightmapWidth));

                // Calculate the normal of the terrain (note this is in normalised coordinates relative to the overall terrain dimensions)
                Vector3 normal = terrainData.GetInterpolatedNormal(y_01, x_01);

                // Calculate the steepness of the terrain
                float steepness = terrainData.GetSteepness(y_01, x_01);

                // Setup an array to record the mix of texture weights at this point
                float[] splatWeights = new float[terrainData.alphamapLayers];

                // CHANGE THE RULES BELOW TO SET THE WEIGHTS OF EACH TEXTURE ON WHATEVER RULES YOU WANT

                // Texture[0] has constant influence
                splatWeights[0] = 0.1f;

                // Texture[1] is stronger at lower altitudes
                splatWeights[1] = Mathf.Clamp01(terrainData.heightmapHeight - height);

                // Texture[2] stronger on flatter terrain
                // Note "steepness" is unbounded, so we "normalise" it by dividing by the extent of heightmap height and scale factor
                // Subtract result from 1.0 to give greater weighting to flat surfaces
                splatWeights[2] = 1.0f - Mathf.Clamp01(steepness * steepness / (terrainData.heightmapHeight * 10.0f));

                // Texture[3] increases with height but only on surfaces facing positive Z axis 
                splatWeights[3] = height * Mathf.Clamp01(normal.z);

                // Sum of all textures weights must add to 1, so calculate normalization factor from sum of weights
                float z = splatWeights.Sum();

                // Loop through each terrain texture
                for (int k = 0; k < terrainData.alphamapLayers; k++)
                {

                    // Normalize so that sum of all texture weights = 1
                    splatWeights[k] /= z;

                    // Assign this point to the splatmap array
                    splatmapData[j, i, k] = splatWeights[k];
                }
            }
        }

        // Finally assign the new splatmap to the terrainData:
        return splatmapData;
    }

    private void InitAllTerrainData()
    {
        for(int y = 0; y < maxWorldDimension + 2; y++)
        {
            for (int x = 0; x < maxWorldDimension + 2; x++)
            {
                zones[y, x].terrainData = InitTerrainData(x, y);
            }
        }
    }

    private TerrainData InitTerrainData(int x, int y)
    {
        TerrainData terrainData = new TerrainData();
        terrainData.heightmapResolution = heightMapResolution;
        terrainData.size = new Vector3(terrainData.size.x, maxHeight, terrainData.size.z);
        terrainData.SetHeights(0, 0, heights[y, x]);
        terrainData.SetAlphamaps(0, 0, InitTextures(terrainData));
        return terrainData;
    }

    private void CreateTerrain(Zone zone, int x, int y, bool isEdge)
    {
        GameObject root = Terrain.CreateTerrainGameObject(zone.terrainData);
        Terrain terrain = root.GetComponent<Terrain>();
        if (!isEdge) terrain.gameObject.AddComponent<TeleportAreaTerrain>();
        terrain.transform.position = new Vector3(zone.terrainData.size.x * x, 0.0f, zone.terrainData.size.z * y);
        zone.terrain = terrain;
        zone.root = root;
    }

    private void ConnectNeighborTerrains(int x, int y)
    {
        for (int yi = Mathf.Max(y - 1, 0); yi < Mathf.Min(y + 2, maxWorldDimension + 2); yi++)
        {
            for (int xi = Mathf.Max(x - 1, 0); xi < Mathf.Min(x + 2, maxWorldDimension + 2); xi++)
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
    }
}
