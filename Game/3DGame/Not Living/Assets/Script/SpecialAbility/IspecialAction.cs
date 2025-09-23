// ISpecialAction.cs
public interface ISpecialAction
{
    void PerformAction(PlayerController playerController);
    float CooldownProgress { get; }
    
    // ▼▼▼ このプロパティを追加 ▼▼▼
    /// <summary>
    /// UIに表示するための能力名
    /// </summary>
    string AbilityName { get; }
}