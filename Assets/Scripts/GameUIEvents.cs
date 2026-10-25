using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

// Goes on the GameManager object. Sends the game's values to your own UI.
// In the Inspector: click +, drag in your TextMeshPro text, pick "string text" (Dynamic string).
// For panels/buttons: drag in the GameObject, pick "SetActive" (Dynamic bool).
public class GameUIEvents : MonoBehaviour
{
    public UnityEvent<bool> onConnected;      // HUD panel
    public UnityEvent<bool> onNotConnected;   // Connect panel
    public UnityEvent<bool> onCanStartGame;   // Start button

    public UnityEvent<string> onJoinCodeText;   // "Lobby Code: X7K2QP"
    public UnityEvent<string> onRoundText;
    public UnityEvent<string> onWeaponText;
    public UnityEvent<string> onMessage;
    public UnityEvent<string> onPlayer1Text;
    public UnityEvent<string> onPlayer2Text;
    public UnityEvent<string> onPlayer3Text;
    public UnityEvent<string> onPlayer4Text;

    float hideMessageAt;

    void OnEnable() { GameManager.OnMessage += ShowMessage; }
    void OnDisable() { GameManager.OnMessage -= ShowMessage; }

    void ShowMessage(string text)
    {
        onMessage.Invoke(text);
        hideMessageAt = Time.time + 3f;
    }

    void Update()
    {
        bool connected = NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer;
        onConnected.Invoke(connected);
        onNotConnected.Invoke(!connected);
        if (!connected) return;

        onJoinCodeText.Invoke("Lobby Code: " + GameManager.JoinCode);

        var gm = GameManager.Instance;
        onCanStartGame.Invoke(gm.CanStartGame);
        onRoundText.Invoke("Round " + gm.currentRound.Value + " / " + gm.maxRounds);

        var me = GameManager.LocalPlayer;
        if (me != null) onWeaponText.Invoke("Weapon: " + me.CurrentWeapon.displayName);

        UnityEvent<string>[] playerTexts = { onPlayer1Text, onPlayer2Text, onPlayer3Text, onPlayer4Text };
        foreach (var p in PlayerController.All)
        {
            playerTexts[p.OwnerClientId % 4].Invoke(
                "Player " + p.PlayerNumber + (p.IsMonarch ? " (Monarch)" : "") + "\n" +
                "HP: " + p.health.Value + " / " + p.MaxHP + "\n" +
                "+" + p.bonusDamage.Value + " Dmg  +" + p.bonusMaxHP.Value + " HP  +" + p.bonusSpeed.Value + "% Spd");
        }

        // Clear the message after 3 seconds (keep the final winner on screen)
        bool gameOver = !gm.isMatchActive.Value && gm.currentRound.Value >= gm.maxRounds;
        if (hideMessageAt > 0 && Time.time > hideMessageAt && !gameOver)
        {
            hideMessageAt = 0;
            onMessage.Invoke("");
        }
    }
}