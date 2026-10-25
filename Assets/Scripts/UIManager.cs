using TMPro;
using Unity.Netcode;
using UnityEngine;

// Put this on your Canvas. Drag your own UI objects into the slots. That's it.
public class UIManager : MonoBehaviour
{
    [Header("Panels")]
    public GameObject connectPanel;
    public GameObject hudPanel;
    public GameObject startButton;

    [Header("Texts")]
    public TMP_Text joinCodeText;
    public TMP_Text roundText;
    public TMP_Text weaponText;
    public TMP_Text messageText;
    public TMP_Text player1Text;
    public TMP_Text player2Text;
    public TMP_Text player3Text;
    public TMP_Text player4Text;

    float hideMessageAt;

    void OnEnable() { GameManager.OnMessage += ShowMessage; }
    void OnDisable() { GameManager.OnMessage -= ShowMessage; }

    void ShowMessage(string text)
    {
        messageText.text = text;
        hideMessageAt = Time.time + 3f;
    }

    void Update()
    {
        bool connected = NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer;
        connectPanel.SetActive(!connected);
        hudPanel.SetActive(connected);
        if (!connected) return;

        var gm = GameManager.Instance;
        startButton.SetActive(gm.CanStartGame);

        // Lobby code: only before the first round, or after the game ends
        bool inLobby = gm.currentRound.Value == 0 || (!gm.isMatchActive.Value && gm.currentRound.Value >= gm.maxRounds);
        joinCodeText.gameObject.SetActive(inLobby);
        joinCodeText.text = "Lobby Code: " + GameManager.JoinCode;
        roundText.text = "Round " + gm.currentRound.Value + " / " + gm.maxRounds;

        var me = GameManager.LocalPlayer;
        if (me != null) weaponText.text = "Weapon: " + me.CurrentWeapon.displayName;

        TMP_Text[] playerTexts = { player1Text, player2Text, player3Text, player4Text };
        foreach (var p in PlayerController.All)
        {
            var t = playerTexts[p.OwnerClientId % 4];
            if (t == null) continue;   // fine to leave P3/P4 empty
            t.text = "Player " + p.PlayerNumber + (p.IsMonarch ? " (Monarch)" : "") + "\n" +
                     "HP: " + p.health.Value + " / " + p.MaxHP + "\n" +
                     "+" + p.bonusDamage.Value + " Dmg  +" + p.bonusMaxHP.Value + " HP  +" + p.bonusSpeed.Value + "% Spd";
        }

        // Clear the message after 3 seconds (keep the final winner on screen)
        bool gameOver = !gm.isMatchActive.Value && gm.currentRound.Value >= gm.maxRounds;
        if (hideMessageAt > 0 && Time.time > hideMessageAt && !gameOver)
        {
            hideMessageAt = 0;
            messageText.text = "";
        }
    }
}