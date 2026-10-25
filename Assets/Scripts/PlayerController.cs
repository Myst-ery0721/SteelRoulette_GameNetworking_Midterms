using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

// Goes on the Player prefab: movement, attacking, health, weapon and buffs.
public class PlayerController : NetworkBehaviour
{
    public static List<PlayerController> All = new List<PlayerController>();

    public float baseSpeed = 5f;
    public GameObject crown;   // a child object shown only on the Monarch

    // STATE (NetworkVariables, Write: Server)
    public NetworkVariable<int> health = new NetworkVariable<int>(100);
    public NetworkVariable<int> currentWeaponId = new NetworkVariable<int>(0);
    public NetworkVariable<int> bonusDamage = new NetworkVariable<int>(0);
    public NetworkVariable<int> bonusMaxHP = new NetworkVariable<int>(0);
    public NetworkVariable<int> bonusSpeed = new NetworkVariable<int>(0);

    public int PlayerNumber => (int)OwnerClientId + 1;
    public bool IsAlive => health.Value > 0;
    public int MaxHP => 100 + bonusMaxHP.Value;
    public bool IsMonarch => GameManager.Instance.monarchId.Value == (long)OwnerClientId;
    public WeaponData CurrentWeapon => GameManager.Instance.weapons[currentWeaponId.Value];

    static Color[] colors = { Color.red, Color.blue, Color.green, Color.yellow };

    // How wide a swing is: 0.7 = 90 degrees in front of you (matches the aim cone)
    public const float SwingWidth = 0.7f;
    public float LastSwingTime = -10f;   // used by AimCone to flash

    Rigidbody2D rb;
    float nextAttackTime;

    public override void OnNetworkSpawn()
    {
        All.Add(this);

        // Use the child named "Crown" (so you don't have to fill the slot)
        var crownChild = transform.Find("Crown");
        crown = crownChild != null ? crownChild.gameObject : null;

        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0;          // top-down: no falling
        rb.freezeRotation = true;     // no tipping over
        transform.rotation = Quaternion.identity;
        GetComponent<SpriteRenderer>().color = colors[OwnerClientId % 4];
        if (!IsOwner) rb.bodyType = RigidbodyType2D.Kinematic;
        if (IsOwner) TeleportToSpawn();
    }

    public override void OnNetworkDespawn()
    {
        All.Remove(this);
    }

    void Update()
    {
        // Hide dead players, show the crown on the Monarch
        GetComponent<SpriteRenderer>().enabled = IsAlive;
        GetComponent<Collider2D>().enabled = IsAlive;
        if (crown != null) crown.SetActive(IsAlive && IsMonarch);

        if (!IsOwner) return;

        bool canAct = GameManager.Instance.isMatchActive.Value && IsAlive;

        // Move with WASD
        Vector2 input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")).normalized;
        float speed = baseSpeed * (1f + bonusSpeed.Value / 100f);
#if UNITY_6000_0_OR_NEWER
        rb.linearVelocity = canAct ? input * speed : Vector2.zero;
#else
        rb.velocity = canAct ? input * speed : Vector2.zero;
#endif

        // Attack toward the mouse with left click
        if (canAct && Input.GetMouseButtonDown(0) && Time.time >= nextAttackTime)
        {
            nextAttackTime = Time.time + CurrentWeapon.cooldown;
            LastSwingTime = Time.time;
            Vector2 mouse = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            AttackServerRpc((mouse - (Vector2)transform.position).normalized);
        }
    }

    // EVENT: Player -> Server
    [ServerRpc]
    void AttackServerRpc(Vector2 aim)
    {
        if (!GameManager.Instance.isMatchActive.Value || !IsAlive) return;

        int damage = CurrentWeapon.damage + bonusDamage.Value;

        foreach (var target in All.ToArray())
        {
            if (target == this || !target.IsAlive) continue;

            Vector2 toTarget = target.transform.position - transform.position;
            bool inRange = toTarget.magnitude <= CurrentWeapon.range;
            bool inFront = Vector2.Dot(aim, toTarget.normalized) > SwingWidth;

            if (inRange && inFront) GameManager.Instance.DamagePlayer(target, this, damage);
        }
    }

    public void TeleportToSpawn()
    {
        var spawns = GameManager.Instance.spawnPoints;
        Vector3 pos = spawns[(int)(OwnerClientId % (ulong)spawns.Length)].position;
        GetComponent<NetworkTransform>().Teleport(pos, transform.rotation, transform.localScale);
    }
}