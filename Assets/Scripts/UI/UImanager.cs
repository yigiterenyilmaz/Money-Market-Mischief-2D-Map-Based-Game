using UnityEngine;
using UnityEngine.SceneManagement;

public class UImanager : MonoBehaviour
{
    public static UImanager Instance { get; private set; }
    
    public GameObject pausePanel;
    public GameObject skillTreePanel;   
    public GameObject pauseButton;
    public GameObject skillTreeButton;
    public GameObject moneyBar;
    public MapController mainCamera;
    private bool enable=true;
    private int currentui = 0;
    //0=map 1=skill menu 2=pause
    
    
    
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }
    public void OnRestartPress()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void OnGameResumePress()
    {
        pausePanel.SetActive(false);
        skillTreePanel.SetActive(false);
        pauseButton.SetActive(true);
        skillTreeButton.SetActive(true);
        moneyBar.SetActive(true);
        mainCamera.enable = true;
        currentui = 0;
        GameManager.Instance.ResumeGame();
    }

    public void OnGameExitPress()
    {
        Application.Quit();
    }

    public void OnEnterPausePress()
    {
        pausePanel.SetActive(true);
        skillTreePanel.SetActive(false);
        pauseButton.SetActive(false);
        skillTreeButton.SetActive(false);
        moneyBar.SetActive(false);
        mainCamera.enable = false;
        currentui = 2;
        GameManager.Instance.PauseGame();
    }

    public void OnSkillTreeOpen()
    {
        pausePanel.SetActive(false);
        skillTreePanel.SetActive(true);
        pauseButton.SetActive(false);
        skillTreeButton.SetActive(false);
        moneyBar.SetActive(false);
        mainCamera.enable = false;
        currentui = 1;
    }

    public void OnSkillTreeClose()
    {
        if (enable)
        {
            pausePanel.SetActive(false);
            skillTreePanel.SetActive(false);
            pauseButton.SetActive(true);
            skillTreeButton.SetActive(true);
            moneyBar.SetActive(true);
            mainCamera.enable = true;
            currentui = 0;
        }

    }
    
    public void ToggleUI()
    {
        if (enable)
        {
            pauseButton.SetActive(false);
            skillTreeButton.SetActive(false);
            mainCamera.enable = false;
            enable = false;
        }
        else
        {
            if(currentui==0)OnGameResumePress();
            if(currentui==1)OnSkillTreeOpen();
            if(currentui==2)OnEnterPausePress();
            enable = true;
        }
    }
}