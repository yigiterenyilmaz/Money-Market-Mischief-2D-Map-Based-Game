using System;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    //runtime
    private GameState currentState = GameState.Initializing;
    private float gameTime = 0f; //toplam oynanan süre (saniye)

    //events
    public static event Action<GameState, GameState> OnGameStateChanged; //oldState, newState
    public static event Action OnGameStarted;
    public static event Action OnGamePaused;
    public static event Action OnGameResumed;
    public static event Action OnGameEnded;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        //ileride harita üretimi burada tetiklenecek
        //şimdilik direkt oyunu başlat
        StartGame();
    }

    private void Update()
    {
        if (currentState == GameState.Playing)
        {
            gameTime += Time.deltaTime;
        }
    }

    private void OnEnable()
    {
        GameStatManager.OnGameOver += HandleGameOver;
    }

    private void OnDisable()
    {
        GameStatManager.OnGameOver -= HandleGameOver;
    }

    #region State Transitions

    public void StartGame()
    {
        SetState(GameState.Playing);
        Time.timeScale = 1f;
        OnGameStarted?.Invoke();
    }

    public void PauseGame()
    {
        if (currentState != GameState.Playing)
            return;

        SetState(GameState.Paused);
        Time.timeScale = 0f;
        OnGamePaused?.Invoke();
    }

    public void ResumeGame()
    {
        if (currentState != GameState.Paused)
            return;

        SetState(GameState.Playing);
        Time.timeScale = 1f;
        OnGameResumed?.Invoke();
    }

    public void EndGame()
    {
        if (currentState == GameState.GameOver)
            return;

        SetState(GameState.GameOver);
        Time.timeScale = 0f;
        OnGameEnded?.Invoke();
    }

    private void HandleGameOver()
    {
        EndGame();
    }

    #endregion

    #region State Management

    private void SetState(GameState newState)
    {
        if (currentState == newState)
            return;

        GameState oldState = currentState;
        currentState = newState;
        OnGameStateChanged?.Invoke(oldState, newState);
    }

    public GameState GetCurrentState()
    {
        return currentState;
    }

    public bool IsPlaying()
    {
        return currentState == GameState.Playing;
    }

    public bool IsPaused()
    {
        return currentState == GameState.Paused;
    }

    public bool IsGameOver()
    {
        return currentState == GameState.GameOver;
    }

    #endregion

    #region Getters

    public float GetGameTime()
    {
        return gameTime;
    }

    //dakika cinsinden oyun süresi
    public float GetGameTimeMinutes()
    {
        return gameTime / 60f;
    }

    #endregion
}
