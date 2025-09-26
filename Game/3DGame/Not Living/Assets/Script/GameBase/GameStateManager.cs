// GameStateManager.cs
using UnityEngine;
using System;

public class GameStateManager : MonoBehaviour
{
    public static GameStateManager Instance { get; private set; }

    public enum GameState
    {
        Gameplay,      // 通常のプレイ状態
        MiniGameActive, // ミニゲーム実行中
        Paused         // ▼▼▼ ポーズ中（設定画面など）を追加 ▼▼▼
    }

    public GameState CurrentState { get; private set; }
    public event Action<GameState> OnGameStateChanged;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void SetState(GameState newState)
    {
        if (CurrentState == newState) return;
        CurrentState = newState;
        OnGameStateChanged?.Invoke(newState);
    }
}