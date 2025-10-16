// GameEvents.cs
using System;

public static class GameEvents
{
    // --- スコア用イベント ---
    public static event Action OnHalfDayPassed;
    public static void TriggerHalfDayPassed() => OnHalfDayPassed?.Invoke();

    // --- 善悪値・カオス値用イベント ---
    public static event Action<StatusManager> OnTargetDefeatedWithInfo;
    public static void TriggerTargetDefeatedWithInfo(StatusManager defeatedStatus) => OnTargetDefeatedWithInfo?.Invoke(defeatedStatus);

    public static event Action OnGoodDeedPerformed;
    public static void TriggerGoodDeedPerformed() => OnGoodDeedPerformed?.Invoke();

    public static event Action<float> OnChaosValueChange;
    public static void TriggerChaosValueChange(float amount) => OnChaosValueChange?.Invoke(amount);
    public static event Action OnGameClear;
    public static void TriggerGameClear() => OnGameClear?.Invoke();
}