using UnityEngine;
using System.Collections;
using Valve.VR.InteractionSystem;

public class DrawRaycast : MonoBehaviour
{
    private LineRenderer lr;
    private GameObject sphere;
    private Player player;

    Transform reference
    {
        get
        {
            var top = SteamVR_Render.Top();
            return (top != null) ? top.origin : null;
        }
    }

    void Start()
    {
        lr = gameObject.AddComponent<LineRenderer>();
        lr.material.color = Color.cyan;
        sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(sphere.GetComponent<Collider>());
        sphere.GetComponent<Renderer>().material.color = Color.cyan;
        sphere.SetActive(false);
        player = Player.instance;
    }

    void Update()
    {
        if (player && player.leftController != null && player.leftController.GetHairTrigger()) DoClick();
        if (player && player.leftController != null && player.leftController.GetHairTriggerUp()) DoUnClick();
    }

    void DoUnClick()
    {
        // First get the current Transform of the the reference space (i.e. the Play Area, e.g. CameraRig prefab)
        var t = reference;
        if (t == null)
            return;
        lr.enabled = false;
        sphere.SetActive(false);
    }

    void DoClick()
    {
        // First get the current Transform of the the reference space (i.e. the Play Area, e.g. CameraRig prefab)
        var t = reference;
        if (t == null)
            return;

        // Get the current Y position of the reference space
        float refY = t.position.y;
        
        // create a Ray from the origin of the controller in the direction that the controller is pointing
        Ray ray = new Ray(player.leftHand.transform.position, player.leftHand.transform.forward);

        // Set defaults
        bool hasGroundTarget = false;
        float dist = 0f;

        RaycastHit hitInfo;
        TerrainCollider tc = Terrain.activeTerrain.GetComponent<TerrainCollider>();
        hasGroundTarget = tc.Raycast(ray, out hitInfo, 1000f);
        dist = hitInfo.distance;
        if (hasGroundTarget) drawLine(player.leftHand.transform.position, hitInfo.point);
        else
        {
            lr.enabled = false;
            sphere.SetActive(false);
        }
    }

    private void drawLine(Vector3 start, Vector3 end)
    {
        lr.enabled = true;
        sphere.SetActive(true);
        lr.startWidth = 0.05f;
        lr.endWidth = 0.1f;
        lr.SetPosition(0, start);
        lr.SetPosition(1, end);

        sphere.transform.position = end;

        float sizeOnScreen = 10;
        Vector3 a = Camera.main.WorldToScreenPoint(sphere.transform.position);
        Vector3 b = new Vector3(a.x, a.y + sizeOnScreen, a.z);

        Vector3 aa = Camera.main.ScreenToWorldPoint(a);
        Vector3 bb = Camera.main.ScreenToWorldPoint(b);

        sphere.transform.localScale = Vector3.one * (aa - bb).magnitude;
    }
}

