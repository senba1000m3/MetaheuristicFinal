using System.Collections.Generic;

public static class MazeUtils {

    // BFS 檢查 S → E 是否可通行
    public static bool IsMazeSolvable(char[,] map)
    {
        int rows = map.GetLength(0);
        int cols = map.GetLength(1);

        (int x, int y) start = (-1, -1);
        (int x, int y) end = (-1, -1);

        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < cols; x++)
            {
                if (map[y, x] == 'S') start = (x, y);
                if (map[y, x] == 'E') end = (x, y);
            }
        }

        if (start.x == -1 || end.x == -1) return false;

        Queue<(int x, int y)> queue = new Queue<(int, int)>();
        bool[,] visited = new bool[rows, cols];

        queue.Enqueue(start);
        visited[start.y, start.x] = true;

        int[] dx = { 0, 0, 1, -1 };
        int[] dy = { 1, -1, 0, 0 };

        while (queue.Count > 0)
        {
            var (cx, cy) = queue.Dequeue();
            if (cx == end.x && cy == end.y) return true;

            for (int i = 0; i < 4; i++)
            {
                int nx = cx + dx[i];
                int ny = cy + dy[i];

                if (nx < 0 || ny < 0 || nx >= cols || ny >= rows) continue;
                if (visited[ny, nx]) continue;
                if (map[ny, nx] == 'O') continue;

                visited[ny, nx] = true;
                queue.Enqueue((nx, ny));
            }
        }

        return false;
    }

    // 使用 BFS 填補路徑 (在 Crossover 後補 S → E)
    public static void FillPathBFS(char[,] map)
    {
        int rows = map.GetLength(0);
        int cols = map.GetLength(1);

        (int x, int y) start = (-1, -1);
        (int x, int y) end = (-1, -1);

        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < cols; x++)
            {
                if (map[y, x] == 'S') start = (x, y);
                if (map[y, x] == 'E') end = (x, y);
            }
        }

        if (start.x == -1 || end.x == -1) return;

        Queue<(int x, int y)> queue = new Queue<(int, int)>();
        Dictionary<(int, int), (int, int)> parent = new Dictionary<(int, int), (int, int)>();
        bool[,] visited = new bool[rows, cols];

        queue.Enqueue(start);
        visited[start.y, start.x] = true;
        parent[start] = start;

        int[] dx = { 0, 0, 1, -1 };
        int[] dy = { 1, -1, 0, 0 };

        bool found = false;

        while (queue.Count > 0)
        {
            var (cx, cy) = queue.Dequeue();
            if (cx == end.x && cy == end.y)
            {
                found = true;
                break;
            }

            for (int i = 0; i < 4; i++)
            {
                int nx = cx + dx[i];
                int ny = cy + dy[i];

                if (nx < 0 || ny < 0 || nx >= cols || ny >= rows) continue;
                if (visited[ny, nx]) continue;
                if (map[ny, nx] == 'O') continue;

                visited[ny, nx] = true;
                queue.Enqueue((nx, ny));
                parent[(nx, ny)] = (cx, cy);
            }
        }

        if (!found) return;

        // 回溯路徑並填補
        var curr = end;
        while (curr != start)
        {
            var p = parent[curr];
            if (map[curr.y, curr.x] == 'O') map[curr.y, curr.x] = 'X';
            curr = p;
        }
    }
}
