using System;
using System.Collections.Generic;
using System.Linq;

public class MapGenerator
{
    private const char EMPTY = '#';
    private const char PATH = 'X';
    private const char PICKUP = 'P';
    private const char DROPOFF = 'D';
    private const char OBSTACLE = 'O';
    private const char START = 'S';
    private const char END = 'E';

    private Random _random = new Random();

    // 座標結構
    private struct Point
    {
        public int X, Y;
        public Point(int x, int y) { X = x; Y = y; }
        public override bool Equals(object obj) => obj is Point p && X == p.X && Y == p.Y;
        public override int GetHashCode() => (X, Y).GetHashCode();
    }

    // Overload for backward compatibility
    public char[,] GenerateMap(int size)
    {
        // Default to 1 pickup and ~50% empty space
        return GenerateMap(size, 1, (int)(size * size * 0.5f));
    }

    public char[,] GenerateMap(int size, int pickupCount, int emptyCount)
    {
        int maxAttempts = 100; // Reduced from 2000 to prevent freezing

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            // Randomize pickup count in range [count-1, count+1]
            // Ensure at least 1
            int currentPickupCount = _random.Next(pickupCount - 1, pickupCount + 2);
            if (currentPickupCount < 1) currentPickupCount = 1;

            // If we are failing a lot, try reducing pickup count further
            if (attempt > 50 && currentPickupCount > 1) {
                currentPickupCount--;
            }

            char[,] map = TryGenerateSingleMap(size, currentPickupCount, emptyCount);
            
            if (map != null)
            {
                return map;
            }
        }

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
        
        for (int i = 0; i < pickupCount; i++)
        {
            if (!TryPlaceObjectSmart(map, size, PICKUP, pickups)) return null;
            if (!TryPlaceObjectSmart(map, size, DROPOFF, dropoffs)) return null;
        }

        // 5. 驗證並生成路徑 (先不放障礙物，確保有路)
        char[,] solvedMap = (char[,])map.Clone();
        
        if (!FindAndMarkPath(solvedMap, size, startPos, endPos, pickups, dropoffs))
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
    private bool TryPlaceObjectSmart(char[,] map, int size, char type, List<Point> tracker)
    {
        for (int i = 0; i < 50; i++) // 嘗試 50 次
        {
            int r = _random.Next(1, size - 1);
            int c = _random.Next(1, size - 1);

            if (map[r, c] == EMPTY)
            {
                // 檢查周圍是否有空位 (Empty)
                if (HasEmptyNeighbor(map, size, r, c))
                {
                    map[r, c] = type;
                    tracker.Add(new Point(r, c));
                    return true;
                }
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

    // 核心路徑邏輯：Start -> P鄰居 -> D鄰居 -> End
    private bool FindAndMarkPath(char[,] map, int size, Point start, Point end, List<Point> pickups, List<Point> dropoffs)
    {
        // === 防禦性檢查：防止 GA 傳入壞掉的數據 ===
        if (pickups == null || dropoffs == null) return false;
        
        // 如果數量不一致，或者根本沒有任務，視為無效地圖
        if (pickups.Count != dropoffs.Count || pickups.Count == 0) return false;
        // ==========================================

        Point currentPos = start;
        List<Point> availablePickups = new List<Point>(pickups);
        List<Point> availableDropoffs = new List<Point>(dropoffs);

        // 1. 去最近的 P (的鄰居)
        while (availablePickups.Count > 0)
        {
            // 安全檢查
            if (availableDropoffs.Count == 0) return false;

            // Find nearest pickup
            Point targetP = FindNearest(currentPos, availablePickups);
            List<Point> path = GetPathToAdjacent(map, size, currentPos, targetP);
            
            if (path == null || path.Count == 0) return false;
            
            MarkPath(map, path);
            currentPos = path.LastOrDefault(); 
            if (currentPos.Equals(default(Point))) return false;
            
            availablePickups.Remove(targetP);

            // 2. 去最近的 D (的鄰居)
            Point targetD = FindNearest(currentPos, availableDropoffs);
            List<Point> path2 = GetPathToAdjacent(map, size, currentPos, targetD);
            
            if (path2 == null || path2.Count == 0) return false;
            
            MarkPath(map, path2);
            currentPos = path2.LastOrDefault();
            if (currentPos.Equals(default(Point))) return false;
            
            availableDropoffs.Remove(targetD);
        }

        // 3. 去終點 (直接踩上去)
        List<Point> finalPath = GetPathToTarget(map, size, currentPos, end);
        if (finalPath == null) return false;

        MarkPath(map, finalPath);
        
        // 確保起點終點符號不被覆蓋
        map[start.X, start.Y] = START;
        map[end.X, end.Y] = END;

        return true;
    }

    private Point FindNearest(Point from, List<Point> candidates)
    {
        Point best = candidates[0];
        int minDist = int.MaxValue;

        foreach (var p in candidates)
        {
            int dist = Math.Abs(p.X - from.X) + Math.Abs(p.Y - from.Y);
            if (dist < minDist)
            {
                minDist = dist;
                best = p;
            }
        }
        return best;
    }

    private void MarkPath(char[,] map, List<Point> path)
    {
        foreach (var p in path)
        {
            // 只有 Empty 可以變成 Path，保留 S, E, P, D, O
            if (map[p.X, p.Y] == EMPTY)
            {
                map[p.X, p.Y] = PATH;
            }
        }
    }

    // BFS: 走到目標的"旁邊"
    private List<Point> GetPathToAdjacent(char[,] map, int size, Point start, Point target)
    {
        List<Point> validDestinations = new List<Point>();
        int[] dx = { 0, 0, 1, -1 };
        int[] dy = { 1, -1, 0, 0 };

        // 找出目標四周可站立的點
        for (int i = 0; i < 4; i++)
        {
            int nx = target.X + dx[i];
            int ny = target.Y + dy[i];
            if (IsWalkable(map, size, nx, ny))
            {
                validDestinations.Add(new Point(nx, ny));
            }
        }

        if (validDestinations.Count == 0) return null;
        return BFS(map, size, start, validDestinations);
    }

    // BFS: 直接走到目標 (用於終點)
    private List<Point> GetPathToTarget(char[,] map, int size, Point start, Point target)
    {
        return BFS(map, size, start, new List<Point> { target });
    }

    private List<Point> BFS(char[,] map, int size, Point start, List<Point> targets)
    {
        Queue<Point> queue = new Queue<Point>();
        Dictionary<Point, Point> parents = new Dictionary<Point, Point>();
        HashSet<Point> visited = new HashSet<Point>();

        queue.Enqueue(start);
        visited.Add(start);

        Point? foundTarget = null;

        while (queue.Count > 0)
        {
            Point current = queue.Dequeue();

            if (targets.Contains(current))
            {
                foundTarget = current;
                break;
            }

            int[] dx = { 0, 0, 1, -1 };
            int[] dy = { 1, -1, 0, 0 };

            for (int i = 0; i < 4; i++)
            {
                int nx = current.X + dx[i];
                int ny = current.Y + dy[i];
                Point next = new Point(nx, ny);

                // 判斷是否可以走：
                // 1. 在地圖內
                // 2. 是路(Empty, Start, End) 或是 目標點(targets)
                // 注意：已經生成的 PATH ('X') 這裡視為障礙物，避免路徑自我重疊（貪食蛇自殺）
                if (nx >= 0 && nx < size && ny >= 0 && ny < size)
                {
                    bool isTarget = targets.Contains(next);
                    bool isWalkable = map[nx, ny] == EMPTY || map[nx, ny] == START || map[nx, ny] == END;

                    if (!visited.Contains(next) && (isWalkable || isTarget))
                    {
                        visited.Add(next);
                        parents[next] = current;
                        queue.Enqueue(next);
                    }
                }
            }
        }

        if (foundTarget == null) return null;

        // 回溯路徑
        List<Point> path = new List<Point>();
        Point curr = foundTarget.Value;
        while (!curr.Equals(start))
        {
            path.Add(curr);
            curr = parents[curr];
        }
        path.Reverse();
        return path;
    }

    private bool IsWalkable(char[,] map, int size, int x, int y)
    {
        if (x < 0 || x >= size || y < 0 || y >= size) return false;
        char c = map[x, y];
        return c == EMPTY || c == START || c == END;
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

    public bool BFSRepairMap(char[,] map, int size)
    {

        Point start = new Point(-1, -1);
        Point end = new Point(-1, -1);
        List<Point> pickups = new List<Point>();
        List<Point> dropoffs = new List<Point>();

        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                char c = map[x, y];
                if (c == PATH)
                {
                    map[x, y] = EMPTY;
                }
                else if (c == START) start = new Point(x, y);
                else if (c == END) end = new Point(x, y);
                else if (c == PICKUP) pickups.Add(new Point(x, y));
                else if (c == DROPOFF) dropoffs.Add(new Point(x, y));
            }
        }

        if (start.X == -1 || end.X == -1) return false;
        if (pickups.Count == 0 || dropoffs.Count == 0) return false; 

        return FindAndMarkPath(map, size, start, end, pickups, dropoffs);
    }
}