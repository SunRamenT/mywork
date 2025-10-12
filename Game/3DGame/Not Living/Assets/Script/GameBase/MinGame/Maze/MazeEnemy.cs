// MazeEnemy.cs (AI修正版)
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class MazeEnemy : MonoBehaviour
{
    // --- 外部から設定 ---
    public int[,] mazeData;
    public Vector2Int currentPos;
    private float moveInterval = 1.0f;
    public Transform gridParent;
    
    // ▼▼▼ 追加: 確率を調整するための重み ▼▼▼
    [Tooltip("前進・横進を選ぶ重み。この値が大きいほど、引き返しにくくなります。")]
    public int forwardWeight = 3;

    // --- 内部用 ---
    private readonly Vector2Int[] dirs = { new Vector2Int(0, 1), new Vector2Int(0, -1), new Vector2Int(1, 0), new Vector2Int(-1, 0) };
    private int mazeWidth, mazeHeight;
    private Vector2Int _previousMoveDirection = Vector2Int.zero;

    public void Initialize(int[,] data, Vector2Int startPos, float interval, Transform parent)
    {
        mazeData = data;
        currentPos = startPos;
        moveInterval = interval;
        gridParent = parent;
        mazeWidth = mazeData.GetLength(0);
        mazeHeight = mazeData.GetLength(1);
        _previousMoveDirection = Vector2Int.zero;

        StartCoroutine(MoveRoutine());
    }

    private IEnumerator MoveRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(moveInterval);

            // --- 1. 移動可能な全ての方向をリストアップ ---
            yield return new WaitForSeconds(moveInterval);

            List<Vector2Int> possibleDirections = new List<Vector2Int>();
            foreach (var dir in dirs)
            {
                Vector2Int wallPos = currentPos + dir;
                Vector2Int targetPos = currentPos + dir * 1;

                if (targetPos.x >= 0 && targetPos.x < mazeWidth && targetPos.y >= 0 && targetPos.y < mazeHeight &&
                    mazeData[wallPos.x, wallPos.y] > 0 &&
                    targetPos != MazeMiniGame.shieldPosition) // ▼▼▼ 追加: 移動先がシールドではないかチェック
                {
                    possibleDirections.Add(dir);
                }
            }


            // --- 2. 移動方向を重み付けで決定 ---
            if (possibleDirections.Count > 0)
            {
                Vector2Int moveDir;

                if (possibleDirections.Count == 1)
                {
                    // 選択肢が1つ（行き止まり）なら、それを選ぶしかない
                    moveDir = possibleDirections[0];
                }
                else
                {
                    // 選択肢が複数ある場合、重み付けで選ぶ
                    List<Vector2Int> weightedDirections = new List<Vector2Int>();
                    Vector2Int reverseDir = -_previousMoveDirection;

                    foreach (var dir in possibleDirections)
                    {
                        if (dir == reverseDir)
                        {
                            // 引き返す方向のくじは1枚だけ入れる
                            weightedDirections.Add(dir);
                        }
                        else
                        {
                            // それ以外の方向（前進・横進）のくじは forwardWeight の数だけ入れる
                            for (int i = 0; i < forwardWeight; i++)
                            {
                                weightedDirections.Add(dir);
                            }
                        }
                    }
                    // 作成した「くじ箱」からランダムに1枚引く
                    moveDir = weightedDirections[Random.Range(0, weightedDirections.Count)];
                }

                // --- 3. 移動 ---
                _previousMoveDirection = moveDir;
                currentPos += moveDir * 1;

                // UI上の位置を更新
                Transform targetFloor = gridParent.Find($"Tile_{currentPos.x}_{currentPos.y}");
                if (targetFloor != null)
                {
                    transform.SetParent(targetFloor, false);
                    transform.localPosition = Vector3.zero;
                }
            }
        }
    }
}