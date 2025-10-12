// MazeTile.cs (新規作成)
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class MazeTile : MonoBehaviour
{
    public Vector2Int gridPosition;
    private MazeMiniGame _miniGame;

    public void Initialize(MazeMiniGame miniGame, Vector2Int position)
    {
        _miniGame = miniGame;
        gridPosition = position;
        GetComponent<Button>().onClick.AddListener(OnTileClicked);
    }

    private void OnTileClicked()
    {
        _miniGame.HandleTileClick(gridPosition);
    }
}