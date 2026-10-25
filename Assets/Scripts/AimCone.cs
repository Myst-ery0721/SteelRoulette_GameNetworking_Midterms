using UnityEngine;

// Goes on the Player prefab. Draws a see-through cone toward the mouse,
// as long as your weapon's range and as wide as your swing. Only you see it.
public class AimCone : MonoBehaviour
{
    public Color color = new Color(1f, 1f, 1f, 0.2f);       // normal
    public Color swingColor = new Color(1f, 0f, 0f, 0.6f);  // red flash when you swing
    public float flashTime = 0.15f;

    PlayerController player;
    GameObject cone;
    Mesh mesh;

    void Start()
    {
        player = GetComponent<PlayerController>();

        // Create the cone object
        cone = new GameObject("AimCone");
        mesh = new Mesh();
        cone.AddComponent<MeshFilter>().mesh = mesh;
        var r = cone.AddComponent<MeshRenderer>();
        r.sharedMaterial = GetComponent<SpriteRenderer>().sharedMaterial;
        r.sortingOrder = 5;
    }

    void LateUpdate()
    {
        bool show = player.IsOwner && player.IsAlive && GameManager.Instance.isMatchActive.Value;
        cone.SetActive(show);
        if (!show) return;

        cone.transform.position = player.Center;

        // Aim direction and cone size
        Vector2 mouse = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Vector2 dir = mouse - player.Center;
        float aim = Mathf.Atan2(dir.y, dir.x);
        float half = Mathf.Acos(PlayerController.SwingWidth);   // half the swing angle
        float range = player.CurrentWeapon.range;

        // Fan shape: the player's center + 11 points along the arc
        var points = new Vector3[12];
        var colors = new Color[12];
        var tris = new int[30];
        for (int i = 0; i <= 10; i++)
        {
            float a = aim - half + half * 2f * i / 10f;
            points[i + 1] = new Vector3(Mathf.Cos(a), Mathf.Sin(a)) * range;
        }
        bool swinging = Time.time - player.LastSwingTime < flashTime;
        for (int i = 0; i < 12; i++) colors[i] = swinging ? swingColor : color;
        for (int i = 0; i < 10; i++) { tris[i * 3] = 0; tris[i * 3 + 1] = i + 2; tris[i * 3 + 2] = i + 1; }

        mesh.Clear();
        mesh.vertices = points;
        mesh.colors = colors;
        mesh.triangles = tris;
    }

    void OnDestroy() { if (cone != null) Destroy(cone); }
}