using UnityEngine;

public abstract class IPlayerAgent : MonoBehaviour
{
    public abstract PlayerModel Simulate(char[,] maze);
}
 