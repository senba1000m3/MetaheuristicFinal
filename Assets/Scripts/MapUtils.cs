using System;
using System.Collections.Generic;
using System.Linq;

public static class MapUtils
{
    public const char EMPTY = '#';
    public const char PATH = 'X';
    public const char PATH_UP = '↑';
    public const char PATH_DOWN = '↓';
    public const char PATH_LEFT = '←';
    public const char PATH_RIGHT = '→';
    public const char PICKUP = 'P';
    public const char DROPOFF = 'D';
    public const char OBSTACLE = 'O';
    public const char START = 'S';
    public const char END = 'E';

    public struct Point
    {
        public int X, Y;
        public Point(int x, int y) { X = x; Y = y; }
        public override bool Equals(object obj) => obj is Point p && X == p.X && Y == p.Y;
        public override int GetHashCode() => (X, Y).GetHashCode();
    }

    public static bool BFSRepairMap(char[,] map, int size)
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
                if (c == PATH || c == PATH_UP || c == PATH_DOWN || c == PATH_LEFT || c == PATH_RIGHT)
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

    public static bool FindAndMarkPath(char[,] map, int size, Point start, Point end, List<Point> pickups, List<Point> dropoffs)
    {
        if (pickups == null || dropoffs == null) return false;
        
        int pairCount = Math.Min(pickups.Count, dropoffs.Count);
        if (pairCount == 0) return false;

        Point currentPos = start;
        List<Point> availablePickups = new List<Point>(pickups);
        List<Point> availableDropoffs = new List<Point>(dropoffs);

        int pairsProcessed = 0;
        while (pairsProcessed < pairCount)
        {
            if (availablePickups.Count == 0 || availableDropoffs.Count == 0) break;

            Point targetP = FindNearest(currentPos, availablePickups);
            List<Point> path = GetPathToAdjacent(map, size, currentPos, targetP);
            
            if (path == null || path.Count == 0) return false;
            
            MarkPath(map, path);
            currentPos = path.LastOrDefault(); 
            if (currentPos.Equals(default(Point))) return false;
            
            availablePickups.Remove(targetP);

            Point targetD = FindNearest(currentPos, availableDropoffs);
            List<Point> path2 = GetPathToAdjacent(map, size, currentPos, targetD);
            
            if (path2 == null || path2.Count == 0) return false;
            
            MarkPath(map, path2);
            currentPos = path2.LastOrDefault();
            if (currentPos.Equals(default(Point))) return false;
            
            availableDropoffs.Remove(targetD);
            pairsProcessed++;
        }

        List<Point> finalPath = GetPathToTarget(map, size, currentPos, end);
        if (finalPath == null) return false;

        MarkPath(map, finalPath);
        
        map[start.X, start.Y] = START;
        map[end.X, end.Y] = END;

        return true;
    }

    private static Point FindNearest(Point from, List<Point> candidates)
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

    private static void MarkPath(char[,] map, List<Point> path)
    {
        for (int i = 0; i < path.Count - 1; i++)
        {
            Point curr = path[i];
            Point next = path[i+1];

            if (map[curr.X, curr.Y] == EMPTY)
            {
                int dx = next.X - curr.X;
                int dy = next.Y - curr.Y;

                if (dx == 1) map[curr.X, curr.Y] = PATH_DOWN;
                else if (dx == -1) map[curr.X, curr.Y] = PATH_UP;
                else if (dy == 1) map[curr.X, curr.Y] = PATH_RIGHT;
                else if (dy == -1) map[curr.X, curr.Y] = PATH_LEFT;
                else map[curr.X, curr.Y] = PATH;
            }
        }
    }

    private static List<Point> GetPathToAdjacent(char[,] map, int size, Point start, Point target)
    {
        List<Point> validDestinations = new List<Point>();
        int[] dx = { 0, 0, 1, -1 };
        int[] dy = { 1, -1, 0, 0 };

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

    private static List<Point> GetPathToTarget(char[,] map, int size, Point start, Point target)
    {
        return BFS(map, size, start, new List<Point> { target });
    }

    private static List<Point> BFS(char[,] map, int size, Point start, List<Point> targets)
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

        List<Point> path = new List<Point>();
        Point curr = foundTarget.Value;
        while (!curr.Equals(start))
        {
            path.Add(curr);
            curr = parents[curr];
        }
        path.Add(start);
        path.Reverse();
        return path;
    }

    private static bool IsWalkable(char[,] map, int size, int x, int y)
    {
        if (x < 0 || x >= size || y < 0 || y >= size) return false;
        char c = map[x, y];
        return c == EMPTY || c == START || c == END;
    }
}
