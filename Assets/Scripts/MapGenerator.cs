using System;
using System.Collections.Generic;
using System.Linq;
using Point = MapUtils.Point;

public class MapGenerator
{
    private const char EMPTY = MapUtils.EMPTY;
    private const char PATH = MapUtils.PATH;
    private const char PATH_UP = MapUtils.PATH_UP;
    private const char PATH_DOWN = MapUtils.PATH_DOWN;
    private const char PATH_LEFT = MapUtils.PATH_LEFT;
    private const char PATH_RIGHT = MapUtils.PATH_RIGHT;
    private const char PICKUP = MapUtils.PICKUP;
    private const char DROPOFF = MapUtils.DROPOFF;
    private const char OBSTACLE = MapUtils.OBSTACLE;
    private const char START = MapUtils.START;
    private const char END = MapUtils.END;

    private Random _random = new Random();

    // Overload for backward compatibility
    public char[,] GenerateMap(int size)
    {
        // Default to 1 pickup and ~50% empty space
        return GenerateMap(size, 1, (int)(size * size * 0.5f));
    }

    public char[,] GenerateMap(int size, int pickupCount, int emptyCount)
    {
        int maxAttemptsTotal = 3000; // Total attempts allowed before giving up completely
        int attempts = 0;

        // Try to generate with the requested count. If it fails repeatedly, gradually reduce the target.
        // This ensures we get the highest possible pickup count that is solvable.
        // Modified: Reduce much slower, or try harder at the target count
        for (int currentTarget = pickupCount; currentTarget >= 1; currentTarget--)
        {
            // Try a reasonable number of times for each target level
            int attemptsForThisLevel = 50; // Increased from 20
            
            // If we are at the requested target, try harder
            if (currentTarget == pickupCount) attemptsForThisLevel = 200; // Increased from 50

            for (int i = 0; i < attemptsForThisLevel; i++)
            {
                attempts++;
                if (attempts > maxAttemptsTotal) break;

                // Randomize slightly around the current target [target-1, target+1]
                int pCount = _random.Next(currentTarget - 1, currentTarget + 2);
                if (pCount < 1) pCount = 1;
                
                // Optimization: If we already failed at higher counts, don't try to go back up above the current target
                // (unless it's the first iteration)
                if (currentTarget < pickupCount && pCount > currentTarget) pCount = currentTarget;

                char[,] map = TryGenerateSingleMap(size, pCount, emptyCount);
                
                if (map != null)
                {
                    UnityEngine.Debug.Log($"[MapGenerator] Map generated! Size: {size}, Pickups: {pCount} (Target: {pickupCount}), Empty: {emptyCount}");
                    return map;
                }
            }

            if (attempts > maxAttemptsTotal) break;
        }

        UnityEngine.Debug.LogWarning("[MapGenerator] Failed to generate valid map after many attempts. Returning fallback.");
        return GenerateFallbackMap(size);
    }

    private char[,] TryGenerateSingleMap(int size, int pickupCount, int emptyCount)
    {
        char[,] map = new char[size, size];

        // 1. 初始化全地圖為 Empty
        for (int i = 0; i < size; i++)
            for (int j = 0; j < size; j++)
                map[i, j] = EMPTY;

        // 2. 設置四周圍牆
        for (int i = 0; i < size; i++)
        {
            map[0, i] = OBSTACLE; map[size - 1, i] = OBSTACLE;
            map[i, 0] = OBSTACLE; map[i, size - 1] = OBSTACLE;
        }

        // 3. 設置起點 (S) 和 終點 (E)
        Point startPos = new Point(1 + _random.Next(size - 2), 0);
        Point endPos = new Point(1 + _random.Next(size - 2), size - 1);
        map[startPos.X, startPos.Y] = START;
        map[endPos.X, endPos.Y] = END;

        // 4. 隨機生成 Pickup (P) 和 Dropoff (D)
        List<Point> pickups = new List<Point>();
        List<Point> dropoffs = new List<Point>();
        
        // Keep track of all special points to avoid placing them too close to each other
        List<Point> allSpecialPoints = new List<Point> { startPos, endPos };

        for (int i = 0; i < pickupCount; i++)
        {
            if (!TryPlaceObjectSmart(map, size, PICKUP, pickups, allSpecialPoints)) return null;
            if (!TryPlaceObjectSmart(map, size, DROPOFF, dropoffs, allSpecialPoints)) return null;
        }

        // 5. 驗證並生成路徑 (先不放障礙物，確保有路)
        char[,] solvedMap = (char[,])map.Clone();
        
        if (!MapUtils.FindAndMarkPath(solvedMap, size, startPos, endPos, pickups, dropoffs))
        {
            return null;
        }

        // 6. 根據 emptyCount 填充障礙物
        // 計算目前剩下的 Empty
        List<Point> emptySpots = new List<Point>();
        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                if (solvedMap[x, y] == EMPTY)
                {
                    emptySpots.Add(new Point(x, y));
                }
            }
        }

        int currentEmpty = emptySpots.Count;
        if (currentEmpty < emptyCount) return null; // 路徑太長或 P/D 太多，無法滿足 emptyCount

        int obstaclesNeeded = currentEmpty - emptyCount;

        // 隨機選取位置放障礙物
        // Fisher-Yates Shuffle
        int n = emptySpots.Count;
        while (n > 1)
        {
            n--;
            int k = _random.Next(n + 1);
            Point value = emptySpots[k];
            emptySpots[k] = emptySpots[n];
            emptySpots[n] = value;
        }

        for (int i = 0; i < obstaclesNeeded; i++)
        {
            Point p = emptySpots[i];
            solvedMap[p.X, p.Y] = OBSTACLE;
        }

        return solvedMap;
    }

    // 放置物件，並檢查上下左右是否有空位 (避免生成死路)
    // 同時檢查是否與其他重要點位太近 (避免 Agent 走捷徑導致順序錯誤)
    private bool TryPlaceObjectSmart(char[,] map, int size, char type, List<Point> tracker, List<Point> allSpecialPoints)
    {
        // Phase 1: Strict placement (Try to avoid adjacency)
        for (int i = 0; i < 50; i++) 
        {
            int r = _random.Next(1, size - 1);
            int c = _random.Next(1, size - 1);
            Point p = new Point(r, c);

            if (map[r, c] == EMPTY)
            {
                if (!HasEmptyNeighbor(map, size, r, c)) continue;

                bool tooClose = false;
                foreach (var sp in allSpecialPoints)
                {
                    int dist = Math.Abs(sp.X - r) + Math.Abs(sp.Y - c);
                    if (dist < 2) 
                    {
                        tooClose = true;
                        break;
                    }
                }
                if (tooClose) continue;

                map[r, c] = type;
                tracker.Add(p);
                allSpecialPoints.Add(p);
                return true;
            }
        }

        // Phase 2: Relaxed placement (Allow adjacency if strict fails)
        // This ensures we can still generate maps with high pickup counts even if they are crowded
        for (int i = 0; i < 50; i++) 
        {
            int r = _random.Next(1, size - 1);
            int c = _random.Next(1, size - 1);
            Point p = new Point(r, c);

            if (map[r, c] == EMPTY)
            {
                if (!HasEmptyNeighbor(map, size, r, c)) continue;

                // Skip distance check
                
                map[r, c] = type;
                tracker.Add(p);
                allSpecialPoints.Add(p);
                return true;
            }
        }

        return false;
    }

    private void TryPlaceObject(char[,] map, int size, char type)
    {
        for (int i = 0; i < 20; i++)
        {
            int r = _random.Next(1, size - 1);
            int c = _random.Next(1, size - 1);
            if (map[r, c] == EMPTY)
            {
                map[r, c] = type;
                return;
            }
        }
    }

    private bool HasEmptyNeighbor(char[,] map, int size, int r, int c)
    {
        int[] dx = { 0, 0, 1, -1 };
        int[] dy = { 1, -1, 0, 0 };
        for(int k=0; k<4; k++)
        {
            int nx = r + dx[k];
            int ny = c + dy[k];
            if (nx >= 0 && nx < size && ny >= 0 && ny < size)
            {
                if (map[nx, ny] == EMPTY) return true;
            }
        }
        return false;
    }

    // 萬一失敗的保底地圖
    private char[,] GenerateFallbackMap(int size)
    {
        char[,] map = new char[size, size];
        for (int i = 0; i < size; i++)
            for (int j = 0; j < size; j++)
                map[i, j] = EMPTY;
        
        // 圍牆
        for (int i = 0; i < size; i++) {
            map[0, i] = OBSTACLE; map[size - 1, i] = OBSTACLE;
            map[i, 0] = OBSTACLE; map[i, size - 1] = OBSTACLE;
        }
        
        map[1, 0] = START;
        map[1, size - 1] = END;
        return map;
    }

}


