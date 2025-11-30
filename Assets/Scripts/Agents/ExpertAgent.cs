using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ExpertAgent : IPlayerAgent{

    public override PlayerModel Simulate(char[,] maze){
        var start = MazeSimUtils.Find(maze, 'S');
        var end = MazeSimUtils.Find(maze, 'E');
        Debug.Log($"[ExpertAgent] Start: ({start.x},{start.y}), End: ({end.x},{end.y})");

        // try compute shortest path first (A* not necessary here, BFS ok)
        var shortest = MazeSimUtils.BFS(maze, (start.x, start.y), (end.x, end.y));
        if(shortest == null){
            // no path -> huge penalty
            return new PlayerModel(1, 50, 0, 5, 999f);
        }

        int backtracks = 0;
        int resets = 0;
        int nearSolves = 0;
        float time = 0f;

        // expert will mostly follow shortest path but small probability to make tiny mistake
        List<(int,int)> visited = new List<(int,int)>();
        int mistakeWindow = Mathf.Max(1, shortest.Count / 10); // occasional small mistake window
        for(int i=0;i<shortest.Count;i++){
            var step = shortest[i];
            visited.Add(step);
            // small chance to deviate for "human-like" behaviour
            if(Random.value < 0.02f && i % mistakeWindow == 0){
                // choose a neighbor that is not the optimal next if possible
                var wrong = MazeSimUtils.GetRandomWrongMove(maze, step.Item1, step.Item2, shortest, i);
                if(wrong.Item1 != step.Item1 || wrong.Item2 != step.Item2){
                    visited.Add(wrong);
                    backtracks++;
                    time += 1f;
                }
            }
            time += 0.5f; // expert moves faster (time unit smaller)
            if(MazeSimUtils.Manhattan(step.Item1, step.Item2, end.x, end.y) <= 2) nearSolves++;
            if(time > 5000f) break;
        }

        backtracks += MazeSimUtils.CountBacktracksFromVisited(visited);

        return new PlayerModel(
            attempts: 1,
            backtracks: backtracks,
            nearSolves: nearSolves,
            resets: resets,
            timeTaken: time
        );
    }
}
