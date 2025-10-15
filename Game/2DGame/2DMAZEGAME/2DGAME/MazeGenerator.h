// MazeGenerator.h

#pragma once
#include <vector>
#include <random>

// UnityのVector2Intの代わりとなる構造体
struct Vector2Int {
    int x, y;
};

// 迷路生成ロジックをまとめた名前空間
namespace MazeGenerator
{
    std::vector<std::vector<int>> Generate(int width, int height, Vector2Int start, Vector2Int goal);
}