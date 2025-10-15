// MazeGenerator.cpp

#include "MazeGenerator.h"
#include <unordered_set>
#include <algorithm>
#include <windows.h> // OutputDebugStringAのため

// Vector2Int用のハッシュ関数（unordered_setで必要）
namespace std {
    template <>
    struct hash<Vector2Int> {
        std::size_t operator()(const Vector2Int& v) const {
            return std::hash<int>()(v.x) ^ (std::hash<int>()(v.y) << 1);
        }
    };
}
// Vector2Int用の比較演算子（unordered_setで必要）
bool operator==(const Vector2Int& lhs, const Vector2Int& rhs) {
    return lhs.x == rhs.x && lhs.y == rhs.y;
}


namespace MazeGenerator
{
    // C#版のプライベート変数を名前空間スコープの変数として定義
    namespace {
        int width;
        int height;
        std::vector<std::vector<int>> maze;
        const Vector2Int dirs[] = { {0, 1}, {0, -1}, {-1, 0}, {1, 0} }; // up, down, left, right
        int filledCells;

        // C#のRandomの代わり
        std::mt19937 rng(std::random_device{}());

        // 範囲内の整数乱数を生成するヘルパー関数
        int GetRandomInt(int min, int max) {
            std::uniform_int_distribution<int> dist(min, max);
            return dist(rng);
        }
    }

    void WriteField(int x, int y) {
        if (maze[y][x] == 0) {
            maze[y][x] = 1; // 1を道とする
            filledCells--;
        }
    }

    void CarvePath(int x, int y) {
        std::vector<Vector2Int> directions = { {0, 1}, {0, -1}, {-1, 0}, {1, 0} };
        std::shuffle(directions.begin(), directions.end(), rng);

        for (const auto& dir : directions) {
            int nextX = x + dir.x * 2;
            int nextY = y + dir.y * 2;

            if (nextX >= 0 && nextX < width && nextY >= 0 && nextY < height && maze[nextY][nextX] == 0) {
                // 壁を壊して道にする
                maze[y + dir.y][x + dir.x] = 1;
                maze[nextY][nextX] = 1;
                CarvePath(nextX, nextY);
            }
        }
    }

    std::vector<std::vector<int>> Generate(int w, int h, Vector2Int start, Vector2Int goal)
    {
        width = w;
        height = h;

        // 迷路を壁(0)で初期化
        maze.assign(height, std::vector<int>(width, 0));

        // 開始点から道を掘り始める（棒倒し法）
        maze[start.y][start.x] = 1;
        CarvePath(start.x, start.y);

        // ゴールが道でなければ、強制的に道にする（生成失敗時の保険）
        if (maze[goal.y][goal.x] == 0) {
            maze[goal.y][goal.x] = 1;
            // ゴールと隣接する道を繋げる簡単な処理
            if (goal.x > 1 && maze[goal.y][goal.x - 2] == 1) maze[goal.y][goal.x - 1] = 1;
            else if (goal.y > 1 && maze[goal.y - 2][goal.x] == 1) maze[goal.y - 1][goal.x] = 1;
            OutputDebugStringA("Warning: Maze goal was not reachable, forced a path.\n");
        }

        return maze;
    }
}