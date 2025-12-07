using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class NormalAgent : IPlayerAgent{

    public override PlayerModel Simulate(char[,] maze)
    {
        // Normal: Balanced
        float errorRate = 0.10f; // 10% chance to make a wrong turn
        float backtrackChance = 0.0f;
        int patienceSteps = 120;
        float timePerStep = 1.0f;

        return RunSimulation(maze, errorRate, backtrackChance, patienceSteps, timePerStep);
    }
}
