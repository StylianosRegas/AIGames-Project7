using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A* pathfinding on the tile grid.
/// Works with the TileMap singleton to check walkability.
///
/// Usage:
///   List<Vector2Int> path = AStar.FindPath(startTile, targetTile);
///
/// Returns null if no path exists.
/// Returns an empty list if start == target.
/// </summary>
public static class AStar
{
    private class Node
    {
        public Vector2Int Position;
        public Node Parent;
        public float G; // cost from start
        public float H; // heuristic to goal
        public float F => G + H;

        public Node(Vector2Int pos, Node parent, float g, float h)
        {
            Position = pos;
            Parent   = parent;
            G        = g;
            H        = h;
        }
    }

    private static readonly Vector2Int[] FourDirections =
    {
        Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
    };

    /// <summary>
    /// Find the shortest path from startTile to targetTile.
    /// Calls TileMap.Instance.IsWalkable() for obstacle checking.
    /// </summary>
    public static List<Vector2Int> FindPath(Vector2Int startTile, Vector2Int targetTile)
    {
        if (startTile == targetTile) return new List<Vector2Int>();

        var openSet   = new List<Node>();
        var closedSet = new HashSet<Vector2Int>();

        openSet.Add(new Node(startTile, null, 0f, Heuristic(startTile, targetTile)));

        int iterations = 0;
        const int MaxIterations = 2000; // safety guard against infinite loops

        while (openSet.Count > 0 && iterations++ < MaxIterations)
        {
            // Pick node with lowest F
            Node current = GetLowestF(openSet);

            if (current.Position == targetTile)
                return ReconstructPath(current);

            openSet.Remove(current);
            closedSet.Add(current.Position);

            foreach (Vector2Int dir in FourDirections)
            {
                Vector2Int neighborPos = current.Position + dir;

                if (closedSet.Contains(neighborPos)) continue;
                if (!IsWalkable(neighborPos))         continue;

                float newG = current.G + 1f;
                Node existing = openSet.Find(n => n.Position == neighborPos);

                if (existing == null)
                {
                    openSet.Add(new Node(neighborPos, current, newG, Heuristic(neighborPos, targetTile)));
                }
                else if (newG < existing.G)
                {
                    existing.G      = newG;
                    existing.Parent = current;
                }
            }
        }

        // No path found
        return null;
    }

    private static float Heuristic(Vector2Int a, Vector2Int b)
    {
        // Manhattan distance (4-directional grid)
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }

    private static Node GetLowestF(List<Node> openSet)
    {
        Node best = openSet[0];
        foreach (var n in openSet)
            if (n.F < best.F) best = n;
        return best;
    }

    private static List<Vector2Int> ReconstructPath(Node node)
    {
        var path = new List<Vector2Int>();
        while (node != null)
        {
            path.Add(node.Position);
            node = node.Parent;
        }
        path.Reverse();
        return path;
    }

    private static bool IsWalkable(Vector2Int tile)
    {
        if (TileMap.Instance == null)
        {
            Debug.LogWarning("[AStar] TileMap.Instance is null.");
            return false;
        }
        return TileMap.Instance.IsWalkable(tile);
    }
}
