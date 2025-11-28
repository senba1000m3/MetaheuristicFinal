using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class BeginnerAgent : IPlayerAgent{

    public PlayerModel Simulate(char[,] maze){
        var start = MazeSimUtils.Find(maze, 'S');
        var end = MazeSimUtils.Find(maze, 'E');
        Debug.Log($"[BeginnerAgent] Start: ({start.x},{start.y}), End: ({end.x},{end.y})");


        int x = start.x;
        int y = start.y;

        int backtracks = 0;
        int resets = 0;
        int nearSolves = 0;
        int steps = 0;

        int stuckCounter = 0;
        float time = 0f;

        Stack<(int, int)> path = new Stack<(int, int)>();
        List<(int,int)> visitedSeq = new List<(int,int)>();
        visitedSeq.Add((x,y));

        // safety limit
        int maxSteps = 5000;

        while(true){

            if(x == end.x && y == end.y) break;

            var neighbors = MazeSimUtils.GetNeighbors(maze, x, y);

            if(neighbors.Count == 0){
                // dead end => 50% reset / 50% backtrack
                if(Random.value < 0.5f){
                    resets++;
                    x = start.x;
                    y = start.y;
                    path.Clear();
                    visitedSeq.Add((x,y));
                } else {
                    if(path.Count > 0){
                        var last = path.Pop();
                        x = last.Item1;
                        y = last.Item2;
                        backtracks++;
                        visitedSeq.Add((x,y));
                    } else {
                        // can't backtrack, reset
                        resets++;
                        x = start.x;
                        y = start.y;
                        visitedSeq.Add((x,y));
                    }
                }
                stuckCounter = 0;
                continue;
            }

            // 80% random, 20% greedy towards end
            (int, int) next;
            if(Random.value < 0.8f){
                next = neighbors[Random.Range(0, neighbors.Count)];
            } else {
                next = neighbors
                    .OrderBy(n => MazeSimUtils.Manhattan(n.x, n.y, end.x, end.y))
                    .First();
            }

            path.Push((x, y));
            x = next.Item1;
            y = next.Item2;
            visitedSeq.Add((x,y));

            steps++;
            time += 1f;
            stuckCounter++;

            if(MazeSimUtils.Manhattan(x, y, end.x, end.y) <= 2){
                nearSolves++;
            }

            // 卡太久 → reset
            if(stuckCounter > 30){
                resets++;
                x = start.x;
                y = start.y;
                path.Clear();
                visitedSeq.Add((x,y));
                stuckCounter = 0;
            }

            if(steps > maxSteps) break; // 防無限迴圈
        }

        backtracks += MazeSimUtils.CountBacktracksFromVisited(visitedSeq);

        return new PlayerModel(
            attempts: 1,
            backtracks: backtracks,
            nearSolves: nearSolves,
            resets: resets,
            timeTaken: time
        );
    }
}
