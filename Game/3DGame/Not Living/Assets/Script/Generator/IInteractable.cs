// IInteractable.cs
public interface IInteractable
{
    // プレイヤーが検知範囲に入った時に呼ばれる
    void OnPlayerEnterRange();

    // プレイヤーが検知範囲から出た時に呼ばれる
    void OnPlayerExitRange();

    // プレイヤーがインタラクションキーを押した時に呼ばれる
    void OnInteract(PlayerController playerController);
}