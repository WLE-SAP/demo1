using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 游戏中按 Esc 返回开始界面。
/// </summary>
public class ReturnToMenu : MonoBehaviour
{
    public string menuSceneName = "MainMenu";
    public KeyCode key = KeyCode.Escape;

    void Update()
    {
        if (Input.GetKeyDown(key)) SceneManager.LoadScene(menuSceneName);
    }
}
