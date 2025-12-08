using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class MazeSimUtils {

    // 回傳可走的鄰格 (4向)
    public static List<(int x, int y)> GetNeighbors(char[,] maze, int x, int y){
        int rows = maze.GetLength(0);
        int cols = maze.GetLength(1);

        List<(int, int)> list = new List<(int, int)>();

        (int, int)[] dirs = {
            (1, 0), (-1, 0), (0, 1), (0, -1)
        };

        foreach(var d in dirs){
            int nx = x + d.Item1;
            int ny = y + d.Item2;
            if(nx >= 0 && nx < cols && ny >= 0 && ny < rows){
                if(IsWalkable(maze[ny, nx])){
                    list.Add((nx, ny));
                }
            }
        }
        return list;
    }

    public static bool IsWalkable(char c){
        return c == 'X' || c == 'S' || c == 'E' || c == '#' || c == '↑' || c == '↓' || c == '←' || c == '→';
    }

    public static (int x, int y) Find(char[,] maze, char target){
        int rows = maze.GetLength(0);
        int cols = maze.GetLength(1);
        for(int y = 0; y < rows; y++){
            for(int x = 0; x < cols; x++){
                if(maze[y, x] == target) return (x, y);
            }
        }
        return (-1, -1);
    }

    public static float Manhattan(int x1, int y1, int x2, int y2){
        return Mathf.Abs(x1 - x2) + Mathf.Abs(y1 - y2);
    }

    public static bool IsAdjacent((int x, int y) p1, (int x, int y) p2) {
        return Manhattan(p1.x, p1.y, p2.x, p2.y) == 1;
    }

    public static (int, int) GetClosestWalkableNeighbor(char[,] maze, (int x, int y) current, (int x, int y) target) {
        var neighbors = GetNeighbors(maze, target.x, target.y);
        if (neighbors.Count == 0) return (-1, -1);
        return neighbors.OrderBy(n => Manhattan(current.x, current.y, n.x, n.y)).First();
    }

    // BFS shortest path (returns list of coords from start to end, inclusive). null if no path.
    public static List<(int x, int y)> BFS(char[,] maze, (int x, int y) start, (int x, int y) goal){
        int rows = maze.GetLength(0);
        int cols = maze.GetLength(1);

        Queue<(int x, int y)> q = new Queue<(int x, int y)>();
        Dictionary<(int, int), (int, int)> parent = new Dictionary<(int, int), (int, int)>();

        q.Enqueue(start);
        parent[start] = (-1, -1);

        (int[] dx, int[] dy) = (new int[]{1,-1,0,0}, new int[]{0,0,1,-1});

        while(q.Count > 0){
            var cur = q.Dequeue();
            if(cur == goal) break;

            for(int i=0;i<4;i++){
                int nx = cur.x + dx[i];
                int ny = cur.y + dy[i];

                if(nx < 0 || ny < 0 || nx >= cols || ny >= rows) continue;
                if(!IsWalkable(maze[ny, nx])) continue;
                if(parent.ContainsKey((nx, ny))) continue;

                parent[(nx, ny)] = cur;
                q.Enqueue((nx, ny));
            }
        }

        if(!parent.ContainsKey(goal)) return null;

        List<(int, int)> path = new List<(int, int)>();
        var v = goal;
        while(v.x != -1){
            path.Add(v);
            v = parent[v];
        }
        path.Reverse();
        return path;
    }

    // pick random walkable neighbor or return current
    public static (int, int) GetRandomNeighbor(char[,] maze, int x, int y){
        var list = GetNeighbors(maze, x, y);
        if(list.Count == 0) return (x, y);
        return list[Random.Range(0, list.Count)];
    }

    // pick a wrong move (not the correct next step of shortest path at index)
    public static (int, int) GetRandomWrongMove(char[,] maze, int x, int y, List<(int,int)> shortestPath, int index){
        var correctNext = shortestPath[Mathf.Min(index + 1, shortestPath.Count - 1)];
        var neighbors = GetNeighbors(maze, x, y);
        var wrongs = neighbors.Where(n => n.x != correctNext.Item1 || n.y != correctNext.Item2).ToList();
        if(wrongs.Count == 0) return (x, y);
        return wrongs[Random.Range(0, wrongs.Count)];
    }

    // count backtracks from a visited sequence (list of coords)
    public static int CountBacktracksFromVisited(List<(int x,int y)> visitedSequence){
        int back = 0;
        HashSet<(int,int)> seen = new HashSet<(int,int)>();
        foreach(var p in visitedSequence){
            if(seen.Contains(p)) back++;
            else seen.Add(p);
        }
        return back;
    }

    // detect near solves from visited sequence relative to goal
    // nearSolve increment when agent reaches within 'thresholdSteps' of goal
    public static int CountNearSolves(List<(int x,int y)> visitedSequence, (int x,int y) goal, int thresholdSteps){
        int cnt = 0;
        for(int i=0;i<visitedSequence.Count;i++){
            var p = visitedSequence[i];
            float man = Manhattan(p.x, p.y, goal.x, goal.y);
            if(man <= thresholdSteps) cnt++;
        }
        return cnt;
    }
}
