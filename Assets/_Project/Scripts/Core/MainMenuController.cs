// =================================================================================================
// Proyecto: The Pumpkin Ritual - La Hora de las Brujas (TP Programación 2 - UNICABA)
// Autora: Tamara Maciel
// Correo: tmaciel96@gmail.com
// Repositorio: https://github.com/tmaciel96/PumpkinPolyTP
// 
// Descripción del script:
// Controlador del menú principal del juego. Administra la navegación entre escenas ("00_MainMenu"
// y "01_GameLevel"), la visualización de paneles modales de controles/instrucciones, la liberación
// del cursor para interacción con la UI y la salida limpia del juego.
// =================================================================================================

using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    [Header("Configuración de Escena")]
    public string gameSceneName = "01_GameLevel";

    [Header("Paneles Opcionales")]
    public GameObject controlsPanel;
    public GameObject creditsPanel;

    void Start()
    {
        Time.timeScale = 1f;

        // Asegurar que el cursor esté visible y desbloqueado en el menú
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (controlsPanel != null) controlsPanel.SetActive(false);
        if (creditsPanel != null) creditsPanel.SetActive(false);
    }

    public void PlayGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(gameSceneName);
    }

    public void OpenControls()
    {
        if (controlsPanel != null) controlsPanel.SetActive(true);
        if (creditsPanel != null) creditsPanel.SetActive(false);
    }

    public void CloseControls()
    {
        if (controlsPanel != null) controlsPanel.SetActive(false);
    }

    public void OpenCredits()
    {
        if (creditsPanel != null) creditsPanel.SetActive(true);
        if (controlsPanel != null) controlsPanel.SetActive(false);
    }

    public void CloseCredits()
    {
        if (creditsPanel != null) creditsPanel.SetActive(false);
    }

    public void QuitGame()
    {
        Debug.Log("Saliendo del juego...");
        Application.Quit();
    }
}
