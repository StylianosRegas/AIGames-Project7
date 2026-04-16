using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// GameManager — singleton that owns game state (win / lose) and global events.
///
/// Setup:
///   - Create an empty GameObject named "GameManager" in the scene.
///   - Attach this script.
///   - Assign the GameOverPanel and WinPanel UI GameObjects.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("UI Panels")]
    public GameObject GameOverPanel;
    public GameObject WinPanel;
    public GameObject TreasurePickedUpBanner; // Optional brief banner

    public bool GameIsOver { get; private set; } = false;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        if (GameOverPanel)  GameOverPanel.SetActive(false);
        if (WinPanel)       WinPanel.SetActive(false);
        if (TreasurePickedUpBanner) TreasurePickedUpBanner.SetActive(false);

        Time.timeScale = 1f;
    }

    public void TriggerGameOver()
    {
        if (GameIsOver) return;
        GameIsOver = true;
        Time.timeScale = 0f;
        if (GameOverPanel) GameOverPanel.SetActive(true);
        Debug.Log("[GameManager] GAME OVER — guard caught the player.");
    }

    public void TriggerWin()
    {
        if (GameIsOver) return;
        GameIsOver = true;
        Time.timeScale = 0f;
        if (WinPanel) WinPanel.SetActive(true);
        Debug.Log("[GameManager] WIN — player escaped with the treasure!");
    }

    public void OnTreasurePickedUp()
    {
        Debug.Log("[GameManager] Treasure picked up! Reach the exit.");
        if (TreasurePickedUpBanner)
        {
            TreasurePickedUpBanner.SetActive(true);
            Invoke(nameof(HideBanner), 2f);
        }
    }

    private void HideBanner()
    {
        if (TreasurePickedUpBanner) TreasurePickedUpBanner.SetActive(false);
    }

    // ── UI Button callbacks ──────────────────────────────────────────────

    public void RestartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void QuitGame()
    {
        Application.Quit();
    }
}
