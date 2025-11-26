using UnityEngine;

public class PlayerModel {
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

    public bool CanIncreaseDifficulty(int maxAttempts){
        return attempts <= maxAttempts;
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
        if(!CanIncreaseDifficulty(5)){
            return Mathf.Max(1, currentDifficulty - 1);
        }

        float Ss = CalculateSoftConstraintScore();

        if(Ss > 5){
            return Mathf.Min(10, currentDifficulty + 1);
        } else if(Ss < -5){
            return Mathf.Max(1, currentDifficulty - 1);
        }

        return currentDifficulty;
    }
}
