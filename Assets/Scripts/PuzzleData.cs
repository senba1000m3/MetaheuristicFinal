using UnityEngine;

public class PuzzleData {
    public int size;
    public char[,] charMap;
    public TileType[,] tileMap;

    public PuzzleData(int size){
        this.size = size;
        charMap = new char[size, size];
        tileMap = new TileType[size, size];
    }
}
