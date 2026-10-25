using UnityEngine;
using UnityEngine.UI;

// Goes on the Player prefab. Fills the Slider (in the player's own canvas) to match HP.
public class HealthBar : MonoBehaviour
{
    PlayerController player;
    Slider slider;

    void Start()
    {
        player = GetComponent<PlayerController>();
        slider = GetComponentInChildren<Slider>(true);
    }

    void Update()
    {
        slider.gameObject.SetActive(player.IsAlive);                  // hide when dead
        slider.value = (float)player.health.Value / player.MaxHP;     // 1 = full, 0 = empty
    }
}