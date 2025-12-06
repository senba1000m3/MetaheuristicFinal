using UnityEngine;
using System.Collections.Generic;

public class PlayerModel {
    public static Dictionary<string, float> MapDifficultyToTargets(int difficulty){
        return new Dictionary<string, float>{
            { "PathLength", Mathf.Lerp(8,50,(difficulty-1)/9f) },
            { "Corners", Mathf.Lerp(0,20,(difficulty-1)/9f) },
            { "EmptySpace", Mathf.Lerp(20,5,(difficulty-1)/9f) },
            { "Pickups", Mathf.Lerp(1,6,(difficulty-1)/9f) },
            { "OrthogonalPickups", Mathf.Lerp(0,2,(difficulty-1)/9f) }
        };
    }
    public int attempts;     // A
    public int backtracks;   // B
    public int nearSolves;   // N
    public int resets;       // R
    public float timeTaken;  // T (秒)

    public int Bthreshold = 3;
    public int Nthreshold = 2;
    public int Rthreshold = 2;
    public float WB = 1f;
    public float WN = 1f;
    public float WR = 1f;
    public float WT = 0.5f;

    public PlayerModel(int attempts, int backtracks, int nearSolves, int resets, float timeTaken){
        this.attempts = attempts;
        this.backtracks = backtracks;
        this.nearSolves = nearSolves;
        this.resets = resets;
        this.timeTaken = timeTaken;
    }

    public float CalculateSoftConstraintScore(){
        int B = backtracks - 1;
        float Ss = 0f;

        if(B < Bthreshold){
            Ss += Mathf.Abs(10 - B) * WB;
        } else {
            Ss -= B * WB;
        }

        if(nearSolves < Nthreshold){
            Ss += Mathf.Abs(5 - nearSolves) * WN;
        } else {
            Ss -= nearSolves * WN;
        }

        if(resets < Rthreshold){
            Ss += Mathf.Abs(5 - resets) * WR;
        } else {
            Ss -= resets * WR;
        }

        Ss -= timeTaken * WT;

        return Ss;
    }

    public int SuggestDifficulty(int currentDifficulty){
        int maxAttempts = 5;
        if (attempts > maxAttempts) {
            return Mathf.Max(1, currentDifficulty - 1);
        }

        // Soft Constraint: 根據分數調整
        float Ss = CalculateSoftConstraintScore();
        if (Ss > 5) {
            return Mathf.Min(10, currentDifficulty + 1);
        }
        else if (Ss < -5) {
            return Mathf.Max(1, currentDifficulty - 1);
        }
        return currentDifficulty;
    }
}
