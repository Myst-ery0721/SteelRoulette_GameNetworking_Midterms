using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

// Goes on the Player prefab: movement, attacking, health, weapon, buffs and animations.
// Animator needs: a Bool "IsMoving" and a Trigger "Attack".
public class PlayerController : NetworkBehaviour
{
    public static List<PlayerController> All = new List<PlayerController>();

    public float baseSpeed = 5f;
    public Vector2 centerOffset = new Vector2(0f, 0.5f);   // from the feet to the middle of the body

    // The middle of the player's body (used for the aim cone and hits)
    public Vector2 Center => (Vector2)transform.position + centerOffset;
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

    // Animation
    Animator animator;
    NetworkAnimator networkAnimator;   // set its Authority Mode to Owner
    SpriteRenderer sprite;
    Vector3 lastPosition;

    public override void OnNetworkSpawn()
    {
        All.Add(this);

        // Use the child named "Crown" (so you don't have to fill the slot)
        var crownChild = transform.Find("Crown");
        crown = crownChild != null ? crownChild.gameObject : null;

        animator = GetComponent<Animator>();
        networkAnimator = GetComponent<NetworkAnimator>();
        sprite = GetComponent<SpriteRenderer>();
        lastPosition = transform.position;

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

        // Face left or right (works for every player on every screen)
        float movedX = transform.position.x - lastPosition.x;
        lastPosition = transform.position;
        if (Mathf.Abs(movedX) > 0.001f) sprite.flipX = movedX < 0;

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

        // ANIMATION: Run while moving, Combat Ready while standing still
        animator.SetBool("IsMoving", canAct && input != Vector2.zero);

        // Attack toward the mouse with left click
        if (canAct && Input.GetMouseButtonDown(0) && Time.time >= nextAttackTime)
        {
            nextAttackTime = Time.time + CurrentWeapon.cooldown;
            LastSwingTime = Time.time;
            networkAnimator.SetTrigger("Attack");   // ANIMATION: swing (shows on every screen)
            Vector2 mouse = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            AttackServerRpc((mouse - Center).normalized);
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

            Vector2 toTarget = target.Center - Center;
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