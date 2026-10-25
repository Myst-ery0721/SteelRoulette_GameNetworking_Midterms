using System.Collections;
using System.Linq;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using UnityEngine;

// Goes on the GameManager object (with a NetworkObject).
// Runs the rounds and decides the winner. Draws no UI.
public class GameManager : NetworkBehaviour
{
    public static GameManager Instance;

    public int maxRounds = 10;
    public WeaponData[] weapons;   // drag your Weapon assets here
    public BuffData[] buffs;       // drag your Buff assets here
    public Transform[] spawnPoints;  // drag 4 empty objects here (P1, P2, P3, P4)

    // STATE (NetworkVariables, Write: Server)
    public NetworkVariable<long> monarchId = new NetworkVariable<long>(-1);
    public NetworkVariable<int> currentRound = new NetworkVariable<int>(0);
    public NetworkVariable<bool> isMatchActive = new NetworkVariable<bool>(false);

    // Your UI listens to this for center-screen messages
    public static System.Action<string> OnMessage;

    // The lobby code (shown on screen by GameUIEvents)
    public static string JoinCode = "";

    string typedCode = "";
    bool waitingForNextRound;

    void Awake() { Instance = this; }

    // ---------- Buttons (hook these up in the Inspector) ----------

    // Host: asks Unity Relay for a lobby code, then starts the game
    public async void HostGame()
    {
        try
        {
            await SignIn();
            var allocation = await RelayService.Instance.CreateAllocationAsync(3);   // 3 friends + you = 4 players
            JoinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(
                allocation.RelayServer.IpV4, (ushort)allocation.RelayServer.Port,
                allocation.AllocationIdBytes, allocation.Key, allocation.ConnectionData);

            NetworkManager.Singleton.StartHost();
        }
        catch (System.Exception e)
        {
            Debug.LogError(e);
            OnMessage?.Invoke("Couldn't host. Check your internet and Unity Cloud setup.");
        }
    }

    // Join: uses the code typed in the Lobby Code box
    public async void JoinGame()
    {
        try
        {
            await SignIn();
            var allocation = await RelayService.Instance.JoinAllocationAsync(typedCode);
            JoinCode = typedCode;

            NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(
                allocation.RelayServer.IpV4, (ushort)allocation.RelayServer.Port,
                allocation.AllocationIdBytes, allocation.Key, allocation.ConnectionData,
                allocation.HostConnectionData);

            NetworkManager.Singleton.StartClient();
        }
        catch (System.Exception e)
        {
            Debug.LogError(e);
            OnMessage?.Invoke("Couldn't join. Check the lobby code.");
        }
    }

    // Hook to the Lobby Code input field (On Value Changed, Dynamic string)
    public void SetJoinCode(string code) => typedCode = code.Trim().ToUpper();

    // Signs in to Unity Services (a random profile, so 2 windows on 1 computer count as 2 players)
    async System.Threading.Tasks.Task SignIn()
    {
        if (UnityServices.State != ServicesInitializationState.Initialized)
        {
            var options = new InitializationOptions();
            options.SetProfile("player" + Random.Range(0, 100000));
            await UnityServices.InitializeAsync(options);
        }
        if (!AuthenticationService.Instance.IsSignedIn)
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
    }

    public void StartGame()
    {
        if (!IsServer) return;
        foreach (var p in PlayerController.All)
        {
            p.bonusDamage.Value = 0;
            p.bonusMaxHP.Value = 0;
            p.bonusSpeed.Value = 0;
        }
        monarchId.Value = -1;
        currentRound.Value = 1;
        StartRound();
    }

    public bool CanStartGame => IsServer && !isMatchActive.Value && !waitingForNextRound && PlayerController.All.Count >= 2;
    public static PlayerController LocalPlayer => PlayerController.All.FirstOrDefault(p => p.IsOwner);

    // ---------- Server logic ----------

    void StartRound()
    {
        foreach (var p in PlayerController.All)
        {
            p.health.Value = p.MaxHP;
            p.currentWeaponId.Value = Random.Range(0, weapons.Length);   // the randomized part
        }
        StartRoundClientRpc(currentRound.Value);
        isMatchActive.Value = true;
    }

    public void DamagePlayer(PlayerController victim, PlayerController killer, int damage)
    {
        victim.health.Value = Mathf.Max(0, victim.health.Value - damage);
        if (victim.health.Value > 0) return;

        // Killer steals the victim's buffs
        killer.bonusDamage.Value += victim.bonusDamage.Value;
        killer.bonusMaxHP.Value += victim.bonusMaxHP.Value;
        killer.bonusSpeed.Value += victim.bonusSpeed.Value;
        victim.bonusDamage.Value = 0;
        victim.bonusMaxHP.Value = 0;
        victim.bonusSpeed.Value = 0;
        PlayerDiedClientRpc(victim.PlayerNumber, killer.PlayerNumber);

        CheckForWinner();
    }

    void CheckForWinner()
    {
        var alive = PlayerController.All.Where(p => p.IsAlive).ToList();
        if (alive.Count != 1) return;                         // 1. TRIGGER: one player left

        isMatchActive.Value = false;                          // 2. STATE CHANGE
        var monarch = alive[0];
        monarchId.Value = (long)monarch.OwnerClientId;

        if (currentRound.Value >= maxRounds)
        {
            GameOverClientRpc(monarch.PlayerNumber);          // 3. BROADCAST
            return;
        }

        // Rounds 1-9: random buff, then the next round
        int buffId = Random.Range(0, buffs.Length);
        monarch.bonusDamage.Value += buffs[buffId].bonusDamage;
        monarch.bonusMaxHP.Value += buffs[buffId].bonusMaxHP;
        monarch.bonusSpeed.Value += buffs[buffId].bonusSpeed;
        RoundOverClientRpc(monarch.PlayerNumber, buffId);
        StartCoroutine(NextRound());
    }

    IEnumerator NextRound()
    {
        waitingForNextRound = true;
        yield return new WaitForSeconds(3f);
        waitingForNextRound = false;
        currentRound.Value++;
        StartRound();
    }

    // ---------- Events (Server -> Everyone) ----------

    [ClientRpc]
    void StartRoundClientRpc(int round)
    {
        LocalPlayer.TeleportToSpawn();
        OnMessage?.Invoke("Round " + round + "!");
    }

    [ClientRpc]
    void PlayerDiedClientRpc(int victim, int killer)
    {
        OnMessage?.Invoke("Player " + victim + " died! Player " + killer + " took their buffs!");
    }

    [ClientRpc]
    void RoundOverClientRpc(int winner, int buffId)
    {
        OnMessage?.Invoke("Player " + winner + " Wins! Got " + buffs[buffId].displayName);
    }

    [ClientRpc]
    void GameOverClientRpc(int winner)
    {
        OnMessage?.Invoke("Player " + winner + " is the Final Monarch!");   // 4. UI UPDATE
    }
}