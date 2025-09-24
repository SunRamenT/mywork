// ITaskMiniGame.cs
using System;

public interface ITaskMiniGame
{
    // タスクが完了したことを通知するイベント (bool: true=成功, false=失敗)
    event Action<bool> OnTaskCompleted;
    
    // タスクを開始するメソッド
    void StartTask(TaskMachine machine);
}