using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public abstract class IPlayerAgent : MonoBehaviour
{
    public abstract PlayerModel Simulate(char[,] maze);
}
 