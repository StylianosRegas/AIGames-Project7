using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// FogOfWar — covers tiles outside the player's line of sight with a dark overlay.
///
/// Uses a secondary "Fog" Tilemap layer drawn on top of the game world.
/// Tiles within the player's vision radius and with clear line-of-sight
/// are made transparent; all others remain opaque (dark).
///
/// Setup:
///   1. In your Tilemap hierarchy, add a second Tilemap called "FogLayer"
///      above the ground/wall layers (higher Order in Layer value).
///   2. Fill the FogLayer with a solid dark tile covering the full map.
///   3. Attach this script to the FogLayer Tilemap GameObject.
///   4. Assign PlayerTransform, FogTilemap, and WallLayer in the Inspector.
///   5. Assign a TileBase asset (any dark solid tile) to FogTile.
///
/// Alternative (simpler):
///   If Tilemap fog is too complex, use a single dark SpriteRenderer with
///   a circular cutout shader, or rely on Unity's 2D Lights for lighting.
/// </summary>
public class FogOfWar : MonoBehaviour
{
    [Header("References")]
    public Transform PlayerTransform;
    public Tilemap   FogTilemap;

    [Header("Vision")]
    [Tooltip("Tiles within this radius (in world units) are revealed")]
    public float VisionRadius = 7f;
    public LayerMask WallLayer;

    [Header("Fog Tile")]
    [Tooltip("The dark tile used to fill the fog layer")]
    public TileBase FogTile;

    [Header("Grid")]
    public int GridWidth  = 30;
    public int GridHeight = 20;

    private Vector2Int _lastPlayerTile = new Vector2Int(-999, -999);

    void Update()
    {
        if (PlayerTransform == null || FogTilemap == null) return;

        Vector2Int playerTile = new Vector2Int(
            Mathf.RoundToInt(PlayerTransform.position.x),
            Mathf.RoundToInt(PlayerTransform.position.y));

        // Only redraw when the player moves to a new tile
        if (playerTile == _lastPlayerTile) return;
        _lastPlayerTile = playerTile;

        RefreshFog(playerTile);
    }

    private void RefreshFog(Vector2Int playerTile)
    {
        int radius = Mathf.CeilToInt(VisionRadius);

        for (int x = 0; x < GridWidth; x++)
        {
            for (int y = 0; y < GridHeight; y++)
            {
                var tile = new Vector2Int(x, y);
                Vector3Int cell = new Vector3Int(x, y, 0);

                if (IsVisible(playerTile, tile, radius))
                    FogTilemap.SetTile(cell, null);        // clear fog
                else
                    FogTilemap.SetTile(cell, FogTile);     // place fog
            }
        }
    }

    private bool IsVisible(Vector2Int playerTile, Vector2Int targetTile, int radius)
    {
        float dist = Vector2.Distance(playerTile, targetTile);
        if (dist > radius) return false;

        // Raycast from player to tile center
        Vector2 from = new Vector2(playerTile.x, playerTile.y);
        Vector2 to   = new Vector2(targetTile.x, targetTile.y);
        Vector2 dir  = to - from;

        if (dir.sqrMagnitude < 0.001f) return true; // same tile

        RaycastHit2D hit = Physics2D.Raycast(from, dir.normalized, dir.magnitude, WallLayer);
        return hit.collider == null; // visible if no wall in the way
    }
}
