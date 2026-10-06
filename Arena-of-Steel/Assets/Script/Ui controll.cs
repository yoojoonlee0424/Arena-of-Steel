using UnityEngine;
using UnityEngine.SceneManagement;

public class Uicontroll : MonoBehaviour
{
    public GameObject Panel;

    private PlayerInput defaultInput;

 




    private void Awake()
    {
        defaultInput = new PlayerInput();

        defaultInput.OnFoot.Escape.performed += e => Menu();



        defaultInput.Enable();



        Panel.SetActive(false);
    }

    public void Menu()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        Panel.SetActive(true);
    }

    public void GameOn()
    {

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        Panel.SetActive(false);
    }


    public void Restart()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void Quitgame()
    {
        Application.Quit();
    }








}
