using UnityEngine;
using System.Collections.Generic;

public class MapGenerator {
    // 生成指定長度和轉角數的主路徑（簡易隨機生成，保證不重疊）
    List<(int x,int y)> GeneratePath(int size, int pathLen, int cornersTarget){
        List<(int x,int y)> path = new List<(int,int)>();
        int x = 1, y = size/2;
        path.Add((x,y));
        int dir = 0; // 0右 1下 2左 3上
        int[] dx = {1,0,-1,0};
        int[] dy = {0,1,0,-1};
        int corners = 0;
        System.Random rnd = new System.Random();
        HashSet<(int,int)> used = new HashSet<(int,int)>();
        used.Add((x,y));
        for(int i=1;i<pathLen;i++){
            // 隨機決定是否轉彎
            if(corners < cornersTarget && rnd.NextDouble()<0.3){
                dir = (dir + rnd.Next(1,4))%4;
                corners++;
            }
            int nx = x+dx[dir], ny = y+dy[dir];
            if(nx<1||nx>=size-1||ny<1||ny>=size-1||used.Contains((nx,ny))){
                // 換方向
                dir = (dir+1)%4;
                nx = x+dx[dir]; ny = y+dy[dir];
            }
            if(nx<1||nx>=size-1||ny<1||ny>=size-1||used.Contains((nx,ny))){
                // 找不到路，提前結束
                break;
            }
            x=nx; y=ny;
            path.Add((x,y));
            used.Add((x,y));
        }
        return path;
    }

    public char[,] GenerateMap(int size, Dictionary<string, float> targetMetrics){
        char[,] map = new char[size, size];
        for(int y = 0; y < size; y++){
            for(int x = 0; x < size; x++){
                map[y, x] = 'O';
            }
        }
        // 1. 生成主路徑
        int pathLen = Mathf.Clamp(Mathf.RoundToInt(targetMetrics["PathLength"]), 8, size*size-2*size);
        int cornersTarget = Mathf.Clamp(Mathf.RoundToInt(targetMetrics["Corners"]), 0, pathLen/2);
        List<(int x,int y)> path = GeneratePath(size, pathLen, cornersTarget);
        // 2. 標記主路徑
        for(int i=0;i<path.Count;i++){
            var p = path[i];
            map[p.y, p.x] = 'X';
        }
        // 起點終點設在最外圈牆壁
        PlaceStartAndEnd(map, size);
        // 3. 分配 Pickups/Dropoffs 在路徑上
        int pickups = Mathf.Clamp(Mathf.RoundToInt(targetMetrics["Pickups"]), 1, path.Count/2);
        int dropoffs = pickups;
        System.Random rnd = new System.Random();
        HashSet<int> usedIdx = new HashSet<int>();
        int placedP = 0, placedD = 0;
        int maxTries = 1000, tries = 0;
        int minIdx = 1;
        int maxIdx = path.Count - 2;
        if (maxIdx > minIdx) {
            while(placedP < pickups && tries < maxTries){
                int idx = rnd.Next(minIdx, maxIdx);
                if(!usedIdx.Contains(idx)){
                    var p = path[idx];
                    map[p.y, p.x] = 'P';
                    usedIdx.Add(idx);
                    placedP++;
                }
                tries++;
            }
            tries = 0;
            while(placedD < dropoffs && tries < maxTries){
                int idx = rnd.Next(minIdx, maxIdx);
                if(!usedIdx.Contains(idx)){
                    var p = path[idx];
                    map[p.y, p.x] = 'D';
                    usedIdx.Add(idx);
                    placedD++;
                }
                tries++;
            }
        }
        // 4. 分配空白格
        int emptyTarget = Mathf.Clamp(Mathf.RoundToInt(targetMetrics["EmptySpace"]), 0, size*size-path.Count);
        int emptyPlaced = 0;
        tries = 0;
        while(emptyPlaced < emptyTarget && tries < maxTries){
            int x = rnd.Next(1, size-1);
            int y = rnd.Next(1, size-1);
            if(map[y, x] == 'O'){
                map[y, x] = '#';
                emptyPlaced++;
            }
            tries++;
        }
        // 檢查地圖合法性
        if (!CheckMap(map)) {
            Debug.LogWarning("Map check failed after GenerateMap. Consider using GenerateValidMap for guaranteed validity.");
        }
        return map;
    }

    private void DFS(char[,] map, int x, int y, int size){
        int[] dx = { 0, 0, 2, -2 };
        int[] dy = { 2, -2, 0, 0 };

        List<int> dirs = new List<int> { 0, 1, 2, 3 };
        Shuffle(dirs);

        foreach(int dir in dirs){
            int nx = x + dx[dir];
            int ny = y + dy[dir];

            // 只檢查內部格子，不管最外圍
            if(nx > 0 && nx < size - 1 && ny > 0 && ny < size - 1 && map[ny, nx] == 'O'){
                map[ny, nx] = 'X';
                map[y + dy[dir]/2, x + dx[dir]/2] = 'X';
                DFS(map, nx, ny, size);
            }
        }
    }

    private void Shuffle(List<int> list){
        for(int i = 0; i < list.Count; i++){
            int rand = Random.Range(i, list.Count);
            int tmp = list[i];
            list[i] = list[rand];
            list[rand] = tmp;
        }
    }

    private void PlaceStartAndEnd(char[,] map, int size){
        // 起點設在左邊邊界的路徑
        bool foundStart = false;
        for(int y = 0; y < size; y++){
            if(map[y, 0] == 'X'){
                map[y, 0] = 'S';
                foundStart = true;
                break;
            }
        }
        if(!foundStart){
            map[1, 0] = 'S';
        }
        // 終點設在右邊邊界的路徑
        bool foundEnd = false;
        for(int y = size-1; y >= 0; y--){
            if(map[y, size-1] == 'X'){
                map[y, size-1] = 'E';
                foundEnd = true;
                break;
            }
        }
        if(!foundEnd){
            map[size-2, size-1] = 'E';
        }
    }

    private void PlaceSpecialPoints(char[,] map, int size, int count, char symbol){
        int placed = 0;
        while(placed < count){
            int x = Random.Range(1, size - 1);
            int y = Random.Range(1, size - 1);

            if(map[y, x] == 'X'){ // 只能放在路徑
                map[y, x] = symbol;
                placed++;
            }
        }
    }

    // 檢查地圖是否符合所有路徑規則
    public bool CheckMap(char[,] map)
    {
        int rows = map.GetLength(0), cols = map.GetLength(1);
        (int x, int y) start = (-1, -1), end = (-1, -1);
        List<(int x, int y)> pickups = new List<(int, int)>();
        List<(int x, int y)> dropoffs = new List<(int, int)>();

        // 掃描地圖，記錄 S/E/P/D 位置
        for (int y = 0; y < rows; y++)
            for (int x = 0; x < cols; x++)
            {
                if (map[y, x] == 'S') start = (x, y);
                if (map[y, x] == 'E') end = (x, y);
                if (map[y, x] == 'P') pickups.Add((x, y));
                if (map[y, x] == 'D') dropoffs.Add((x, y));
            }
        if (start == (-1, -1) || end == (-1, -1)) return false;

        // BFS 檢查所有可走格是否連通且路徑不斷裂
        Queue<(int x, int y, int cargo, HashSet<(int,int)> pSet, HashSet<(int,int)> dSet)> q = new Queue<(int, int, int, HashSet<(int,int)>, HashSet<(int,int)>)>();
        bool[,,] visited = new bool[rows, cols, pickups.Count+2];
        q.Enqueue((start.x, start.y, 0, new HashSet<(int,int)>(), new HashSet<(int,int)>()));
        visited[start.y, start.x, 0] = true;
        int[] dx = { 1, -1, 0, 0 }, dy = { 0, 0, 1, -1 };
        while (q.Count > 0)
        {
            var (x, y, cargo, pSet, dSet) = q.Dequeue();
            // 撿貨
            if (map[y, x] == 'P' && !pSet.Contains((x, y))) {
                pSet = new HashSet<(int,int)>(pSet); pSet.Add((x, y));
                cargo++;
            }
            // 放貨
            if (map[y, x] == 'D' && !dSet.Contains((x, y)) && cargo > 0) {
                dSet = new HashSet<(int,int)>(dSet); dSet.Add((x, y));
                cargo--;
            }
            // 到終點前必須經過所有 P/D
            if (map[y, x] == 'E') {
                if (pSet.Count == pickups.Count && dSet.Count == dropoffs.Count && cargo == 0)
                    return true;
                continue;
            }
            for (int d = 0; d < 4; d++)
            {
                int nx = x + dx[d], ny = y + dy[d];
                if (nx >= 0 && nx < cols && ny >= 0 && ny < rows &&
                    !visited[ny, nx, cargo] && "XSPD#E".Contains(map[ny, nx]))
                {
                    visited[ny, nx, cargo] = true;
                    q.Enqueue((nx, ny, cargo, pSet, dSet));
                }
            }
        }
        return false;
    }

    // 生成地圖直到符合規則
    public char[,] GenerateValidMap(int size, Dictionary<string, float> targetMetrics)
    {
        char[,] map;
        int tries = 0;
        do {
            map = GenerateMap(size, targetMetrics);
            tries++;
        } while (!CheckMap(map) && tries < 100);
        return map;
    }
}
