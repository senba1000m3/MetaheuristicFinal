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

    public char[,] GenerateMap(int size)
    {
        int maxAttempts = 2000;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            char[,] map = TryGenerateSingleMap(size);
            
            if (map != null)
            {
                return map;
            }
        }

        return GenerateFallbackMap(size);
    }

    private char[,] TryGenerateSingleMap(int size)
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
        // 為了保證有解，先只生成 1 組。若要多組可改成 loop
        List<Point> pickups = new List<Point>();
        List<Point> dropoffs = new List<Point>();
        
        // 嘗試放置 P，確保周圍至少有一格是空的
        if (!TryPlaceObjectSmart(map, size, PICKUP, pickups)) return null;
        // 嘗試放置 D，確保周圍至少有一格是空的
        if (!TryPlaceObjectSmart(map, size, DROPOFF, dropoffs)) return null;

        // 5. 隨機生成內部障礙物 (O)
        // 減少障礙物數量以確保路徑暢通 (例如固定 5 個，或 5%)
        int obstacleCount = 5; 
        for (int i = 0; i < obstacleCount; i++)
        {
            TryPlaceObject(map, size, OBSTACLE);
        }

        // 6. 驗證並生成路徑
        // 複製一份地圖來畫路徑
        char[,] solvedMap = (char[,])map.Clone();
        
        if (FindAndMarkPath(solvedMap, size, startPos, endPos, pickups, dropoffs))
        {
            return solvedMap; // 成功！回傳畫好 X 的地圖
        }

        return null; // 此配置無解，回傳 null 讓外層迴圈重試
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
        Point currentPos = start;
        List<Point> currentPickups = new List<Point>(pickups);
        List<Point> currentDropoffs = new List<Point>(dropoffs);

        // 1. 去最近的 P (的鄰居)
        while (currentPickups.Count > 0)
        {
            Point targetP = currentPickups[0]; // 簡化：直接取第一個
            List<Point> path = GetPathToAdjacent(map, size, currentPos, targetP);
              if (path == null || path.Count == 0) return false;
              MarkPath(map, path);
              currentPos = path.LastOrDefault(); // 更新目前位置
              if (currentPos.Equals(default(Point))) return false;
              currentPickups.RemoveAt(0);

              // 2. 去最近的 D (的鄰居)
              Point targetD = currentDropoffs[0];
              List<Point> path2 = GetPathToAdjacent(map, size, currentPos, targetD);
              if (path2 == null || path2.Count == 0) return false;
              MarkPath(map, path2);
              currentPos = path2.LastOrDefault();
              if (currentPos.Equals(default(Point))) return false;
              currentDropoffs.RemoveAt(0);
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
}