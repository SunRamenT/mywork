// MazeGenerator.cs (ランダムスタート/ゴール対応 最終版)
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class MazeGenerator
{
    // 迷路生成の途中経過を記録するためのヘルパー構造体
    private struct Trigger : IEquatable<Trigger>
    {
        public readonly int x, y, value, goingX, goingY, straight;
        public Trigger(int x, int y, int value, int goingX, int goingY, int straight)
        {
            this.x = x; this.y = y; this.value = value;
            this.goingX = goingX; this.goingY = goingY; this.straight = straight;
        }
        public bool Equals(Trigger other) => x == other.x && y == other.y;
        public override int GetHashCode() => x.GetHashCode() ^ y.GetHashCode();
    }

    // =============================
    // ★ ここだけ編集で迷路調整可 ★
    // =============================
    
    public static int width = 15;
    public static int height = 15;
    public static float seed = 0.5f;
    public static float branchBonus = 1f / 12f;
    public static float[,] valueSource = new float[,] 
    { 
        { 0f, 1.1f, 1.3f, 0f },  // 解答ルート長
        { 0f, 0.1f, 0.3f, 0f },  // 曲がり回数
        { 0f, 0.1f, 0.25f, 0f }  // 分岐数
    };
    public static int maxGenerateAttempts = 500;

    // =============================
    // ↓ ここから下は基本ロジック（編集不要）
    // =============================
    
    private static int[,] maze;
    private static readonly Vector2Int[] dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
    private static int filledCells;
    private static HashSet<Trigger> triggers;
    private static Vector2Int _start;
    private static Vector2Int _goal;

    public static int[,] Generate(Vector2Int start, Vector2Int goal)
    {
        _start = start;
        _goal = goal;

        int[,] bestMaze = null;
        float bestScore = float.MinValue;

        for (int attempt = 0; attempt < maxGenerateAttempts; attempt++)
        {
            maze = new int[width, height];
            filledCells = (width + 1) / 2 * (height + 1) / 2;
            triggers = new HashSet<Trigger>();

            WriteField(_start.x, _start.y, 1, 0, 0, 0);

            while (filledCells > 0)
            {
                Trigger bestTrigger = new Trigger();
                bool triggerFound = false;
                int tryCount = Mathf.Min(triggers.Count, 5);
                if (tryCount == 0) break;

                for(int i = 0; i < tryCount; i++)
                {
                    if (triggers.Count == 0) break;
                    Trigger randomTrigger = triggers.ElementAt(UnityEngine.Random.Range(0, triggers.Count));

                    bool canExtend = false;
                    foreach (var dir in dirs)
                    {
                        int nx = randomTrigger.x + dir.x * 2;
                        int ny = randomTrigger.y + dir.y * 2;
                        if (nx >= 0 && nx < width && ny >= 0 && ny < height && maze[nx, ny] == 0)
                        {
                            canExtend = true;
                            break;
                        }
                    }

                    if (canExtend)
                    {
                        if (!triggerFound || randomTrigger.x + randomTrigger.y < bestTrigger.x + bestTrigger.y)
                        {
                            bestTrigger = randomTrigger;
                            triggerFound = true;
                        }
                    }
                    else
                    {
                        triggers.Remove(randomTrigger);
                    }
                }
                
                if (!triggerFound) break;

                CarvePath(bestTrigger.x, bestTrigger.y, bestTrigger.value, bestTrigger.goingX, bestTrigger.goingY, bestTrigger.straight);
            }
            
            if (maze[_goal.x, _goal.y] == 0) continue;
            
            float score = EvaluateMaze();
            if (score > bestScore)
            {
                bestScore = score;
                bestMaze = (int[,])maze.Clone();
            }
            if (bestScore >= 0.8f) break;
        }
        
        if (bestMaze == null) 
        {
            Debug.LogWarning("有効な迷路の生成に失敗したため、最低限の保証された迷路を生成します。");
            maze = new int[width, height];
            for (int i = 0; i < width; i++) maze[i, 0] = 1;
            for (int i = 0; i < height; i++) maze[width - 1, i] = 1;
            bestMaze = maze;
        }
        return bestMaze;
    }

    private static void WriteField(int x, int y, int value, int goingX, int goingY, int straight)
    {
        if (maze[x, y] == 0)
        {
            maze[x, y] = value;
            filledCells--;
            triggers.Add(new Trigger(x, y, value, goingX, goingY, straight));
        }
        CarvePath(x, y, value, goingX, goingY, straight);
    }

    private static void CarvePath(int x, int y, int value, int goingX, int goingY, int straight)
    {
        List<Vector2Int> directions = new List<Vector2Int>(dirs);
        for (int i = 0; i < directions.Count; i++)
        {
            int rand = UnityEngine.Random.Range(i, directions.Count);
            Vector2Int tmp = directions[i];
            directions[i] = directions[rand];
            directions[rand] = tmp;
        }

        foreach (var dir in directions)
        {
            int nextX = x + dir.x * 2;
            int nextY = y + dir.y * 2;

            if (nextX < 0 || nextX >= width || nextY < 0 || nextY >= height) continue;
            if (maze[nextX, nextY] != 0) continue;

            if (dir.x == goingX && dir.y == goingY)
            {
                float goStraightProb = 1f / Mathf.Pow(3f, straight);
                if (UnityEngine.Random.value >= goStraightProb) continue;
            }
            if (UnityEngine.Random.value >= seed) continue;

            maze[x + dir.x, y + dir.y] = value + 1;
            int nextStraight = (dir.x != goingX || dir.y != goingY) ? 1 : straight + 1;
            
            WriteField(nextX, nextY, value + 2, dir.x, dir.y, nextStraight);
        }
    }
    
    private static float EvaluateMaze()
    {
        int pathLength = maze[_goal.x, _goal.y];
        
        float answerLengthNorm = pathLength / (float)(width + height - 1);
        float turnCountNorm = CountTurns() / (float)(width + height - 1);
        float choiceCountNorm = CountChoices() / (float)(width + height - 1);

        float[] processedScores = new float[3];
        float[] sourceValues = { answerLengthNorm, turnCountNorm, choiceCountNorm };

        for (int i = 0; i < 3; i++)
        {
            float source1 = sourceValues[i] - valueSource[i,1];
            if (source1 > 0f)
            {
                float source2 = source1 / (valueSource[i,2] - valueSource[i,1]);
                processedScores[i] = source2 / (source2 + 1f);
            }
            else
            {
                processedScores[i] = 0f;
            }
            valueSource[i,3] = processedScores[i];
        }

        float sideRoadsScore = CheckSideRoads();
        float finalScore = processedScores[0] * processedScores[1] * processedScores[2] * sideRoadsScore;

        return finalScore;
    }

    private static List<Vector2Int> GetAnswerPath()
    {
        List<Vector2Int> path = new List<Vector2Int>();
        int x = _goal.x;
        int y = _goal.y;

        for(int loopGuard = 0; loopGuard < width * height; loopGuard++)
        {
            path.Add(new Vector2Int(x, y));
            if (x == _start.x && y == _start.y) break;

            bool foundNextStep = false;
            foreach (var d in dirs)
            {
                int nextX = x + d.x;
                int nextY = y + d.y;
                if (nextX >= 0 && nextX < width && nextY >= 0 && nextY < height && maze[nextX, nextY] == maze[x, y] - 1)
                {
                    x = nextX; 
                    y = nextY;
                    foundNextStep = true;
                    break;
                }
            }
            
            if (!foundNextStep)
            {
                Debug.LogWarning("GetAnswerPathで道が途切れているため、処理を中断しました。");
                break;
            }
        }

        path.Reverse();
        return path;
    }

    private static int CountTurns()
    {
        int count = 0;
        List<Vector2Int> path = GetAnswerPath();
        if (path.Count < 3) return 0;

        for (int i = 0; i < path.Count - 2; i++)
        {
            Vector2Int p1 = path[i];
            Vector2Int p2 = path[i+1];
            Vector2Int p3 = path[i+2];

            if ((p2.x - p1.x) != (p3.x - p2.x) || (p2.y - p1.y) != (p3.y - p2.y))
            {
                count++;
            }
        }
        return count;
    }

    private static int CountChoices()
    {
        int choices = 0;
        List<Vector2Int> path = GetAnswerPath();
        for (int i = 0; i < path.Count; i++)
        {
            int x = path[i].x;
            int y = path[i].y;
            int openPaths = 0;
            foreach (var d in dirs)
            {
                int nx = x + d.x;
                int ny = y + d.y;
                if (nx >= 0 && nx < width && ny >= 0 && ny < height && maze[nx, ny] != 0)
                    openPaths++;
            }
            
            if (i == 0 || i == path.Count - 1) choices += openPaths - 1;
            else choices += openPaths - 2;
        }
        return choices;
    }

    private static float CheckSideRoads()
    {
        List<float> sideRoadScores = new List<float>();
        List<Vector2Int> path = GetAnswerPath();
        if (path.Count == 0) return 0f;
        
        for (int i = 0; i < path.Count; i++)
        {
            if (i % 2 != 0) continue;

            int x = path[i].x;
            int y = path[i].y;
            
            List<Vector2Int> exclude = new List<Vector2Int>();
            if (i > 0) exclude.Add(path[i-1] - path[i]);
            if (i < path.Count - 1) exclude.Add(path[i+1] - path[i]);

            float branchScore = CheckSideRoad(x, y, exclude);

            if (maze[_goal.x, _goal.y] > 0)
            {
                branchScore *= (maze[_goal.x, _goal.y] - maze[x,y]) / (float)maze[_goal.x, _goal.y];
            }

            if (branchScore > 0)
            {
                sideRoadScores.Add(branchScore);
            }
        }
        
        sideRoadScores.Sort((a, b) => b.CompareTo(a));

        int countToUse = Mathf.Min(sideRoadScores.Count, (width + height) / 6);
        countToUse = Mathf.Min(countToUse, 8);

        float finalScore = 1f;
        for (int i = 0; i < countToUse; i++)
        {
            finalScore *= sideRoadScores[i];
        }
        
        return finalScore;
    }

    private static float CheckSideRoad(int x, int y, List<Vector2Int> exclude)
    {
        List<float> choices = new List<float>();
        foreach (var d in dirs)
        {
            if (exclude.Exists(e => e.x == d.x && e.y == d.y)) continue;

            int nextWallX = x + d.x;
            int nextWallY = y + d.y;
            if (nextWallX < 0 || nextWallX >= width || nextWallY < 0 || nextWallY >= height || maze[nextWallX, nextWallY] == 0) continue;

            int nextPathX = x + d.x * 2;
            int nextPathY = y + d.y * 2;
            if (nextPathX < 0 || nextPathX >= width || nextPathY < 0 || nextPathY >= height) continue;

            List<Vector2Int> newExclude = new List<Vector2Int> { new Vector2Int(-d.x, -d.y) };
            float branchScore = CheckSideRoad(nextPathX, nextPathY, newExclude) + 2f;
            choices.Add(branchScore);
        }

        if (choices.Count == 0) return 0f;
        if (choices.Count == 1) return choices[0];

        float sum = 0;
        float product = 1;
        foreach (var c in choices)
        {
            sum += c;
            product *= c;
        }
        return sum * Mathf.Pow(product, branchBonus);
    }
    /// 迷路データから行き止まりの座標リストを検出して返す
    /// </summary>
    // MazeGenerator.cs の FindDeadEnds メソッドを置き換え

    /// <summary>
    /// 迷路データから行き止まりの座標リストを検出して返す
    /// </summary>
    public static List<Vector2Int> FindDeadEnds(int[,] mazeData, Vector2Int start, Vector2Int goal)
    {
        List<Vector2Int> deadEnds = new List<Vector2Int>();
        int w = mazeData.GetLength(0);
        int h = mazeData.GetLength(1);

        // 全ての「通路マス」（奇数座標）をチェック
        for (int x = 0; x < w; x += 2)
        {
            for (int y = 0; y < h; y += 2)
            {
                // 壁はスキップ
                if (mazeData[x, y] == 0) continue;
                
                // スタートとゴールは除外
                if ((x == start.x && y == start.y) || (x == goal.x && y == goal.y)) continue;
                
                int openPaths = 0;
                // 上下左右の隣接する「通路マス」への道が開いているかチェック
                foreach (var dir in dirs)
                {
                    // 間の壁の位置をチェック
                    int wallX = x + dir.x;
                    int wallY = y + dir.y;

                    if (wallX >= 0 && wallX < w && wallY >= 0 && wallY < h && mazeData[wallX, wallY] > 0)
                    {
                        openPaths++;
                    }
                }

                // 道が1方向にしか繋がっていないマス = 行き止まり
                if (openPaths == 1)
                {
                    deadEnds.Add(new Vector2Int(x, y));
                }
            }
        }
        return deadEnds;
    }
}