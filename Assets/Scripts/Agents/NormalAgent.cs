using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class NormalAgent : IPlayerAgent{

    public PlayerModel Simulate(char[,] maze){
        var start = MazeSimUtils.Find(maze, 'S');
        var end = MazeSimUtils.Find(maze, 'E');
        Debug.Log($"[BeginnerAgent] Start: ({start.x},{start.y}), End: ({end.x},{end.y})");


        int x = start.x;
        int y = start.y;

        int backtracks = 0;
        int resets = 0;
        int nearSolves = 0;
        float time = 0f;

        List<(int,int)> path = new List<(int,int)>();
        path.Add((x,y));

        int maxSteps = 2000;
        int noProgress = 0;
        int lastDist = (int)MazeSimUtils.Manhattan(x, y, end.x, end.y);

        while(true){
            if(x == end.x && y == end.y) break;

            var neighbors = MazeSimUtils.GetNeighbors(maze, x, y);

            if(neighbors.Count == 0){
                // backtrack if possible
                if(path.Count > 1){
                    path.RemoveAt(path.Count - 1);
                    var last = path[path.Count - 1];
                    x = last.Item1;
                    y = last.Item2;
                    backtracks++;
                } else {
                    resets++;
                    x = start.x;
                    y = start.y;
                    path.Clear();
                    path.Add((x,y));
                }
                time += 1f;
                continue;
            }

            // 90% choose neighbor that reduces manhattan (greedy), but with small noise
            (int, int) next;
            if(Random.value < 0.9f){
                next = neighbors
                    .OrderBy(n => MazeSimUtils.Manhattan(n.x, n.y, end.x, end.y))
                    .First();
            } else {
                next = neighbors[Random.Range(0, neighbors.Count)];
            }

            path.Add((next.Item1, next.Item2));
            x = next.Item1;
            y = next.Item2;

            int dist = (int)MazeSimUtils.Manhattan(x, y, end.x, end.y);
            time += 1f;

            if(dist < lastDist){
                noProgress = 0;
            } else {
                noProgress++;
            }
            lastDist = dist;

            if(dist <= 2) nearSolves++;

            // if stuck for long, force small backtrack
            if(noProgress > 40){
                if(path.Count > 1){
                    path.RemoveAt(path.Count - 1);
                    var last = path[path.Count - 1];
                    x = last.Item1;
                    y = last.Item2;
                    backtracks++;
                } else {
                    resets++;
                    x = start.x;
                    y = start.y;
                    path.Clear();
                    path.Add((x,y));
                }
                noProgress = 0;
            }

            if(time > maxSteps) break;
        }

        backtracks += MazeSimUtils.CountBacktracksFromVisited(path);

        return new PlayerModel(
            attempts: 1,
            backtracks: backtracks,
            nearSolves: nearSolves,
            resets: resets,
            timeTaken: time
        );
    }
}
