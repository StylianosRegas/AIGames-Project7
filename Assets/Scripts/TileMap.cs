using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// TileMap — singleton that stores the walkability grid for the level.
///
/// A* calls TileMap.Instance.IsWalkable(tile) during pathfinding.
///
/// Setup (two options):
///
///   OPTION A — Auto-build from scene colliders (recommended for quick setup):
///     1. Create an empty GameObject named "TileMap" and attach this script.
///     2. Set GridWidth, GridHeight, and WorldOrigin in the Inspector.
///     3. Tag all wall/obstacle GameObjects with the "Wall" tag.
///     4. Call BuildFromScene() at Start (set AutoBuildOnStart = true).
///
///   OPTION B — Manual assignment:
///     1. Set MapData directly as a 2D bool array in a custom subclass or
///        populate WalkableTiles manually.
/// </summary>
public class TileMap : MonoBehaviour
{
    public static TileMap Instance { get; private set; }

    [Header("Grid Settings")]
    [Tooltip("Total number of tiles horizontally")]
    public int GridWidth  = 30;
    [Tooltip("Total number of tiles vertically")]
    public int GridHeight = 20;
    [Tooltip("World position of tile (0, 0)")]
    public Vector2 WorldOrigin = Vector2.zero;

    [Header("Auto Build")]
    [Tooltip("If true, scans scene for Wall-tagged colliders at Start")]
    public bool AutoBuildOnStart = true;

    // Internal walkability map: true = walkable, false = blocked
    private bool[,] _walkable;

    // Public set for editor tools or manual override
    public bool[,] MapData => _walkable;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        _walkable = new bool[GridWidth, GridHeight];
        // Default all tiles to walkable
        for (int x = 0; x < GridWidth; x++)
            for (int y = 0; y < GridHeight; y++)
                _walkable[x, y] = true;
    }

    void Start()
    {
        if (AutoBuildOnStart) BuildFromScene();
    }

    /// <summary>
    /// Scan all "Wall" tagged objects and mark their tiles as non-walkable.
    /// Uses a point overlap check at the center of each tile.
    /// </summary>
    public void BuildFromScene()
    {
        int wallLayer = LayerMask.GetMask("Default"); // adjust if walls are on a specific layer
        for (int x = 0; x < GridWidth; x++)
        {
            for (int y = 0; y < GridHeight; y++)
            {
                Vector2 worldPos = TileToWorld(new Vector2Int(x, y));
                Collider2D hit = Physics2D.OverlapPoint(worldPos);
                _walkable[x, y] = (hit == null || !hit.CompareTag("Wall"));
            }
        }
        Debug.Log("[TileMap] Walkability grid built from scene.");
    }

    /// <summary>
    /// Check if the given tile coordinates are walkable.
    /// Returns false for out-of-bounds tiles.
    /// </summary>
    public bool IsWalkable(Vector2Int tile)
    {
        if (tile.x < 0 || tile.x >= GridWidth || tile.y < 0 || tile.y >= GridHeight)
            return false;
        return _walkable[tile.x, tile.y];
    }

    /// <summary>
    /// Manually set a tile's walkability (useful for dynamic obstacles).
    /// </summary>
    public void SetWalkable(Vector2Int tile, bool walkable)
    {
        if (tile.x < 0 || tile.x >= GridWidth || tile.y < 0 || tile.y >= GridHeight) return;
        _walkable[tile.x, tile.y] = walkable;
    }

    // ── Coordinate Conversion ────────────────────────────────────────────

    public Vector2 TileToWorld(Vector2Int tile)
    {
        return new Vector2(WorldOrigin.x + tile.x, WorldOrigin.y + tile.y);
    }

    public Vector2Int WorldToTile(Vector2 worldPos)
    {
        return new Vector2Int(
            Mathf.RoundToInt(worldPos.x - WorldOrigin.x),
            Mathf.RoundToInt(worldPos.y - WorldOrigin.y));
    }

    // Draw grid in Scene view for debugging
    void OnDrawGizmosSelected()
    {
        if (_walkable == null) return;
        for (int x = 0; x < GridWidth; x++)
        {
            for (int y = 0; y < GridHeight; y++)
            {
                Gizmos.color = _walkable[x, y] ? new Color(0, 1, 0, 0.1f) : new Color(1, 0, 0, 0.3f);
                Vector2 center = TileToWorld(new Vector2Int(x, y));
                Gizmos.DrawCube(new Vector3(center.x, center.y, 0), Vector3.one * 0.9f);
            }
        }
    }
}
