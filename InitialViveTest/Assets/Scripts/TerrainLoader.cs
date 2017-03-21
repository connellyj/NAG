using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class TerrainLoader : MonoBehaviour {

    public Transform playerTransform;
    public int zoneDimension;
    public float minDisplayDistance;
    public float gridSize;
    public float unloadDistance;

    private Zone[][] zones;
    private bool isLoading = false;
    private bool isUnloading = false;

    public class Zone
    {
        public bool loaded = false;
        public bool isLoadable = true;
        public GameObject root;
        public Terrain terrain;
    }

    void Awake()
    {
        zones = new Zone[zoneDimension][];
        for(int i = 0; i < zoneDimension; i++)
        {
            zones[i] = new Zone[zoneDimension];
            for(int j = 0; j < zoneDimension; j++)
            {
                zones[i][j] = new Zone();
            }
        }
    }

    void Update()
    {
        for (int y = 0; y < zoneDimension; y++)
        {
            for (int x = 0; x < zoneDimension; x++)
            {
                Zone zone = zones[y][x];
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

    void GetClosestZone(Vector3 position, float closestDistance, out int closestX, out int closestY)
    {
        closestX = -1;
        closestY = -1;
        closestDistance = closestDistance * closestDistance;
        for (int y = 0; y < zoneDimension; y++)
        {
            for (int x = 0; x < zoneDimension; x++)
            {
                Zone zone = zones[y][x];
                if (!zone.loaded && zone.isLoadable)
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

        Zone zone = zones[y][x];

        // Zone cant be loaded
        if (!zone.isLoadable)
            yield break;

        isLoading = true;
        string levelName = GetPrefix(x, y);

        // Load Level
        AsyncOperation async = SceneManager.LoadSceneAsync(levelName, LoadSceneMode.Additive);
        yield return async;

        // Necessary to prevent overwriting of another load level additive following immediately
        // yield return 0;

        // Find the root game object containing the level data
        zone.root = GameObject.Find("/" + levelName);
        if (zone.root != null)
        {
            Transform terrain = zone.root.transform.Find("Terrain");
            if (terrain)
                zone.terrain = terrain.GetComponent(typeof(Terrain)) as Terrain;

            zone.loaded = true;
        }
        else
        {
            Debug.LogError(levelName + " could not be found after loading level");
        }

        // Hookup neighboring terrains so there are no seams in the LOD
        for (int yi = Mathf.Max(y - 1, 0); yi < Mathf.Min(y + 2, zoneDimension); yi++)
        {
            for (int xi = Mathf.Max(x - 1, 0); xi < Mathf.Min(x + 2, zoneDimension); xi++)
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

        Zone zone = zones[y][x];
        zone.loaded = false;

        if (zone.root)
            Destroy(zone.root);
        else
            Debug.LogError("Root for zone has already been unloaded:" + GetPrefix(x, y));

        zone.terrain = null;
        zone.root = null;

        yield return 0;

        isUnloading = false;
    }

    private static string GetPrefix(string prefix, string postfix, int x, int y)
    {
        return string.Format("{0}{1}-{2}{3}", prefix, x, y, postfix);
    }

    private static string GetPrefix(int x, int y)
    {
        return GetPrefix("Map_", "", x, y);
    }

    Terrain GetLoadedTerrain(int x, int y)
    {
        if ((x >= 0 && x < zoneDimension) && (y >= 0 && y < zoneDimension))
        {
            Zone zone = zones[y][x];
            if (zone.loaded)
                return zone.terrain;
        }

        return null;
    }
}
