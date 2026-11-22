using System;
using System.Collections.Generic;
using UnityEngine;

namespace LOGIYGames.SurvivalBuilding
{
    /// <summary>Stable logical identity of one construction piece inside a grid.</summary>
    public readonly struct PieceKey : IEquatable<PieceKey>
    {
        public readonly int PieceId;
        public readonly Vector3Int Anchor;
        public readonly int Variant;

        /// <summary>Creates a piece key.</summary>
        /// <param name="pieceId">Stable identifier from the piece definition.</param>
        /// <param name="anchor">Grid cell used as the piece's anchor.</param>
        /// <param name="variant">Orientation or side variant.</param>
        public PieceKey(int pieceId, Vector3Int anchor, int variant)
        {
            PieceId = pieceId;
            Anchor = anchor;
            Variant = variant;
        }

        /// <inheritdoc/>
        public bool Equals(PieceKey other)
            => PieceId == other.PieceId && Anchor == other.Anchor && Variant == other.Variant;

        /// <inheritdoc/>
        public override bool Equals(object obj) => obj is PieceKey other && Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            unchecked
            {
                var hash = PieceId;
                hash = (hash * 397) ^ Anchor.GetHashCode();
                hash = (hash * 397) ^ Variant;
                return hash;
            }
        }

        /// <summary>Checks equality between two piece keys.</summary>
        public static bool operator ==(PieceKey left, PieceKey right) => left.Equals(right);

        /// <summary>Checks inequality between two piece keys.</summary>
        public static bool operator !=(PieceKey left, PieceKey right) => !left.Equals(right);
    }

    /// <summary>Logical category of a reservable construction slot.</summary>
    public enum SlotKind
    {
        Cell,
        Edge,
        Stair
    }

    /// <summary>
    /// Canonical key for a grid slot. Opposite references to the same edge resolve to one slot.
    /// </summary>
    public readonly struct Slot : IEquatable<Slot>
    {
        public readonly SlotKind Kind;
        public readonly Vector3Int Cell;
        public readonly int Side;

        private Slot(SlotKind kind, Vector3Int cell, int side)
        {
            Kind = kind;
            Cell = cell;
            Side = side;
        }

        /// <summary>Creates the slot occupied by a floor cell.</summary>
        public static Slot CellSlot(Vector3Int cell) => new Slot(SlotKind.Cell, cell, 0);

        /// <summary>Creates the stair slot associated with a cell.</summary>
        public static Slot StairSlot(Vector3Int cell) => new Slot(SlotKind.Stair, cell, 0);

        /// <summary>
        /// Creates a canonical edge slot. Adjacent cells referring to the same edge produce equal values.
        /// </summary>
        /// <param name="anchor">Cell from which the edge is referenced.</param>
        /// <param name="side">Side index in the range 0-3; values are normalized modulo four.</param>
        public static Slot Edge(Vector3Int anchor, int side)
        {
            side &= 3;
            var adjacent = anchor + BuildGrid.SideOffset(side);
            return Compare(anchor, adjacent) < 0
                ? new Slot(SlotKind.Edge, anchor, side)
                : new Slot(SlotKind.Edge, adjacent, (side + 2) & 3);
        }

        /// <inheritdoc/>
        public bool Equals(Slot other)
            => Kind == other.Kind && Cell == other.Cell && Side == other.Side;

        /// <inheritdoc/>
        public override bool Equals(object obj) => obj is Slot other && Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            unchecked
            {
                var hash = (int)Kind;
                hash = (hash * 397) ^ Cell.GetHashCode();
                hash = (hash * 397) ^ Side;
                return hash;
            }
        }

        /// <summary>Checks whether two slots refer to the same logical grid location.</summary>
        public static bool operator ==(Slot left, Slot right) => left.Equals(right);

        /// <summary>Checks whether two slots refer to different logical grid locations.</summary>
        public static bool operator !=(Slot left, Slot right) => !left.Equals(right);

        private static int Compare(Vector3Int left, Vector3Int right)
        {
            if (left.x != right.x) return left.x.CompareTo(right.x);
            if (left.y != right.y) return left.y.CompareTo(right.y);
            return left.z.CompareTo(right.z);
        }
    }

    /// <summary>World-space dimensions shared by newly created building grids.</summary>
    [Serializable]
    public class GridSettings
    {
        [Min(0.25f)] public float Cell = 2f;
        [Min(0.25f)] public float Level = 2f;
    }

    /// <summary>
    /// Stores the coordinate system, occupancy map and support graph for one connected building.
    /// </summary>
    public class BuildGrid : MonoBehaviour
    {
        /// <summary>Unique horizontal columns occupied by registered pieces.</summary>
        public readonly HashSet<Vector2Int> Columns = new();

        public Vector3 Origin;
        public Quaternion Rotation;
        public float Cell = 2f;
        public float Level = 2f;

        public Vector3Int MinCell = new(int.MaxValue, int.MaxValue, int.MaxValue);
        public Vector3Int MaxCell = new(int.MinValue, int.MinValue, int.MinValue);

        /// <summary>Indicates whether the grid currently contains at least one piece.</summary>
        public bool HasCells => Occupied.Count > 0;

        /// <summary>Maps each piece key to its runtime component.</summary>
        public readonly Dictionary<PieceKey, BuildPiece> Occupied = new();

        /// <summary>Maps each logical slot to the piece key that occupies it.</summary>
        public readonly Dictionary<Slot, PieceKey> SlotIndex = new();

        /// <summary>Converts a grid cell coordinate to its world-space center.</summary>
        /// <param name="cell">Grid coordinate.</param>
        /// <returns>World-space cell center.</returns>
        public Vector3 CellCenter(Vector3Int cell)
            => Origin + Rotation * new Vector3(cell.x * Cell, cell.y * Level, cell.z * Cell);

        /// <summary>Gets the world-space center point on the requested cell edge.</summary>
        /// <param name="cell">Grid coordinate containing the edge.</param>
        /// <param name="side">Edge direction, normalized modulo four.</param>
        /// <returns>World-space edge center.</returns>
        public Vector3 EdgePoint(Vector3Int cell, int side)
            => CellCenter(cell) + Rotation * (SideDir(side) * (Cell * 0.5f));

        /// <summary>Converts a world-space point to the nearest grid cell.</summary>
        /// <param name="worldPosition">World-space position.</param>
        /// <returns>Nearest integer grid coordinate.</returns>
        public Vector3Int WorldToCell(Vector3 worldPosition)
        {
            var local = Quaternion.Inverse(Rotation) * (worldPosition - Origin);
            var safeCell = Mathf.Max(Cell, 0.0001f);
            var safeLevel = Mathf.Max(Level, 0.0001f);

            return new Vector3Int(
                Mathf.RoundToInt(local.x / safeCell),
                Mathf.RoundToInt(local.y / safeLevel),
                Mathf.RoundToInt(local.z / safeCell));
        }

        /// <summary>Finds the horizontal grid side closest to a world-space direction.</summary>
        /// <param name="worldDirection">Direction in world space.</param>
        /// <returns>Side index from 0 to 3.</returns>
        public int SideFromWorld(Vector3 worldDirection)
        {
            var local = Quaternion.Inverse(Rotation) * worldDirection;
            local.y = 0f;

            return Mathf.Abs(local.x) > Mathf.Abs(local.z)
                ? (local.x > 0f ? 1 : 3)
                : (local.z > 0f ? 0 : 2);
        }

        /// <summary>Gets the local-space unit direction for a grid side.</summary>
        /// <param name="side">Side index, normalized modulo four.</param>
        public static Vector3 SideDir(int side) => (side & 3) switch
        {
            0 => Vector3.forward,
            1 => Vector3.right,
            2 => Vector3.back,
            _ => Vector3.left
        };

        /// <summary>Gets the cell-coordinate offset for a grid side.</summary>
        /// <param name="side">Side index, normalized modulo four.</param>
        public static Vector3Int SideOffset(int side) => (side & 3) switch
        {
            0 => new Vector3Int(0, 0, 1),
            1 => new Vector3Int(1, 0, 0),
            2 => new Vector3Int(0, 0, -1),
            _ => new Vector3Int(-1, 0, 0)
        };

        /// <summary>
        /// Registers a piece and all its occupied slots. The operation validates the entire registration
        /// before mutating the indexes, preventing partial index updates.
        /// </summary>
        /// <param name="key">Logical key of the piece.</param>
        /// <param name="piece">Runtime piece component.</param>
        /// <param name="slots">All slots occupied by the piece.</param>
        /// <exception cref="ArgumentNullException">A required argument is missing.</exception>
        /// <exception cref="InvalidOperationException">The key or any requested slot is already occupied.</exception>
        public void Register(PieceKey key, BuildPiece piece, IEnumerable<Slot> slots)
        {
            if (piece == null) throw new ArgumentNullException(nameof(piece));
            if (slots == null) throw new ArgumentNullException(nameof(slots));
            if (piece.Definition == null) throw new ArgumentException("The piece has no definition.", nameof(piece));
            if (Occupied.ContainsKey(key)) throw new InvalidOperationException($"Piece key '{key.GetHashCode()}' is already registered.");

            var slotList = new List<Slot>(slots);
            var uniqueSlots = new HashSet<Slot>();
            foreach (var slot in slotList)
            {
                if (!uniqueSlots.Add(slot))
                    throw new InvalidOperationException("A piece cannot register the same slot more than once.");
                if (SlotIndex.ContainsKey(slot))
                    throw new InvalidOperationException($"Slot '{slot.GetHashCode()}' is already occupied.");
            }

            Occupied.Add(key, piece);
            foreach (var slot in slotList)
                SlotIndex.Add(slot, key);

            foreach (var column in piece.Definition.GetOccupiedColumns(key))
                Columns.Add(column);

            MinCell = Vector3Int.Min(MinCell, key.Anchor);
            MaxCell = Vector3Int.Max(MaxCell, key.Anchor);
        }

        /// <summary>Unregisters a piece by key and removes all slot-index entries owned by it.</summary>
        /// <param name="key">Key of the piece to unregister.</param>
        public void Unregister(PieceKey key)
        {
            Occupied.Remove(key);

            var ownedSlots = new List<Slot>();
            foreach (var pair in SlotIndex)
            {
                if (pair.Value == key)
                    ownedSlots.Add(pair.Key);
            }

            foreach (var slot in ownedSlots)
                SlotIndex.Remove(slot);

            RebuildColumns();
            RebuildBounds();
        }

        /// <summary>Unregisters a piece using its known slot list.</summary>
        /// <param name="key">Key of the piece to unregister.</param>
        /// <param name="slots">Slots previously registered for the piece.</param>
        public void Unregister(PieceKey key, IEnumerable<Slot> slots)
        {
            if (slots == null)
            {
                Unregister(key);
                return;
            }

            Occupied.Remove(key);

            foreach (var slot in slots)
            {
                // Do not remove another piece's index entry if the grid was already inconsistent.
                if (SlotIndex.TryGetValue(slot, out var owner) && owner == key)
                    SlotIndex.Remove(slot);
            }

            RebuildColumns();
            RebuildBounds();
        }

        /// <summary>Checks whether a slot is currently occupied.</summary>
        /// <param name="slot">Slot to inspect.</param>
        public bool IsSlotOccupied(Slot slot) => SlotIndex.ContainsKey(slot);

        /// <summary>Tries to resolve a slot to its registered runtime piece.</summary>
        /// <param name="slot">Slot to inspect.</param>
        /// <param name="piece">Resolved piece, or <see langword="null"/> when lookup fails.</param>
        /// <returns><see langword="true"/> when the slot maps to a live registered piece.</returns>
        public bool TryGetPieceAtSlot(Slot slot, out BuildPiece piece)
        {
            piece = null;
            return SlotIndex.TryGetValue(slot, out var key)
                && Occupied.TryGetValue(key, out piece)
                && piece != null;
        }

        /// <summary>
        /// Determines whether all remaining pieces would still be connected to a ground-supported piece
        /// after removing the supplied key.
        /// </summary>
        /// <param name="removedKey">Piece key hypothetically removed from this grid.</param>
        /// <returns><see langword="true"/> when no remaining piece would become unsupported.</returns>
        public bool WouldRemainSupported(PieceKey removedKey)
        {
            if (Occupied.Count == 0)
                return true;

            if (Occupied.Count == 1 && Occupied.ContainsKey(removedKey))
                return true;

            var visited = new HashSet<PieceKey>();
            var queue = new Queue<PieceKey>();

            foreach (var pair in Occupied)
            {
                if (pair.Key == removedKey || pair.Value == null || pair.Value.Definition == null)
                    continue;

                if (pair.Value.Definition.IsSupportAnchor(pair.Key, this))
                {
                    if (visited.Add(pair.Key))
                        queue.Enqueue(pair.Key);
                }
            }

            while (queue.Count > 0)
            {
                var currentKey = queue.Dequeue();
                if (!Occupied.TryGetValue(currentKey, out var current) || current == null)
                    continue;

                foreach (var dependent in current.Definition.GetDependents(currentKey, this))
                {
                    if (dependent == null || dependent.Key == removedKey)
                        continue;

                    if (visited.Add(dependent.Key))
                        queue.Enqueue(dependent.Key);
                }
            }

            foreach (var pair in Occupied)
            {
                if (pair.Key != removedKey && !visited.Contains(pair.Key))
                    return false;
            }

            return true;
        }

        /// <summary>Measures the shortest distance from a world point to the grid's axis-aligned cell bounds.</summary>
        /// <param name="worldPosition">World-space position to measure from.</param>
        /// <returns>Distance in world units, or <see cref="float.MaxValue"/> for an empty grid.</returns>
        public float DistanceToBounds(Vector3 worldPosition)
        {
            if (!HasCells)
                return float.MaxValue;

            var local = Quaternion.Inverse(Rotation) * (worldPosition - Origin);
            var safeCell = Mathf.Max(Cell, 0.0001f);
            var safeLevel = Mathf.Max(Level, 0.0001f);

            var cellX = local.x / safeCell;
            var cellY = local.y / safeLevel;
            var cellZ = local.z / safeCell;

            var dx = (cellX - Mathf.Clamp(cellX, MinCell.x, MaxCell.x)) * safeCell;
            var dy = (cellY - Mathf.Clamp(cellY, MinCell.y, MaxCell.y)) * safeLevel;
            var dz = (cellZ - Mathf.Clamp(cellZ, MinCell.z, MaxCell.z)) * safeCell;

            return Mathf.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        /// <summary>Rebuilds the set of horizontal columns from the current occupancy map.</summary>
        private void RebuildColumns()
        {
            Columns.Clear();
            foreach (var pair in Occupied)
            {
                if (pair.Value == null || pair.Value.Definition == null)
                    continue;

                foreach (var column in pair.Value.Definition.GetOccupiedColumns(pair.Key))
                    Columns.Add(column);
            }
        }

        /// <summary>Recalculates the minimum and maximum occupied anchor coordinates.</summary>
        private void RebuildBounds()
        {
            if (Occupied.Count == 0)
            {
                MinCell = new Vector3Int(int.MaxValue, int.MaxValue, int.MaxValue);
                MaxCell = new Vector3Int(int.MinValue, int.MinValue, int.MinValue);
                return;
            }

            var min = new Vector3Int(int.MaxValue, int.MaxValue, int.MaxValue);
            var max = new Vector3Int(int.MinValue, int.MinValue, int.MinValue);

            foreach (var key in Occupied.Keys)
            {
                min = Vector3Int.Min(min, key.Anchor);
                max = Vector3Int.Max(max, key.Anchor);
            }

            MinCell = min;
            MaxCell = max;
        }
    }

    /// <summary>Material-specific prefab references for a piece definition.</summary>
    [Serializable]
    public class MaterialVariant
    {
        public MaterialDefinition Material;
        public GameObject Prefab;
        public GameObject GhostPrefab;
    }

    /// <summary>All data available to a piece while it evaluates a placement attempt.</summary>
    public struct PlacementContext
    {
        public RaycastHit Hit;
        public BuildPiece HitPiece;
        public BuildMarker HitMarker;
        public BuildGrid Grid;
        public Vector3 ObserverPosition;
        public MaterialDefinition Material;
        public BuildInputState Input;
        public float FirstYaw;
        public float FloorThickness;
    }

    /// <summary>Complete preview and commit data produced by a piece definition.</summary>
    public struct PlacementResult
    {
        public bool Show;
        public bool Valid;
        public PlacementFailReason FailReason;
        public PieceKey Key;
        public Vector3 Position;
        public Quaternion Rotation;
        public BuildGrid TargetGrid;
        public GameObject Prefab;
        public GameObject GhostPrefab;
    }

    /// <summary>Reasons why a placed piece cannot be removed safely.</summary>
    public enum DestroyFailReason
    {
        None,
        HasStairOnFloor,
        HasWallOnFloor,
        HasFloorOnWall,
        HasFloorOnStair,
        WouldOrphanAdjacentFloor,
    }

    /// <summary>Reasons why a placement attempt is invalid.</summary>
    public enum PlacementFailReason
    {
        None,
        WrongSurface,
        SlotOccupied,
        NoSupport,
        CrossesOtherGrid,
        BlockedByWorld,
        NoMaterialVariant,
        CantileverTooFar,
    }

    /// <summary>Candidate position offered by a placement marker.</summary>
    public struct MarkerSpot
    {
        public PieceDefinition Piece;
        public BuildGrid Grid;
        public PieceKey Key;
        public Vector3 Position;
        public Quaternion Rotation;
    }

    /// <summary>Stores per-definition rotation input while the planner is active.</summary>
    public class BuildInputState
    {
        public readonly Dictionary<PieceDefinition, int> Rotations = new();
        public float FirstYawAccum;

        /// <summary>Gets the normalized quarter-turn rotation for a definition.</summary>
        /// <param name="piece">Definition whose rotation is requested.</param>
        /// <returns>Rotation step from zero to three.</returns>
        public int GetRot(PieceDefinition piece)
            => piece != null && Rotations.TryGetValue(piece, out var value) ? value : 0;

        /// <summary>Stores a normalized quarter-turn rotation for a definition.</summary>
        /// <param name="piece">Definition whose rotation is updated.</param>
        /// <param name="value">Rotation step; normalized modulo four.</param>
        public void SetRot(PieceDefinition piece, int value)
        {
            if (piece == null)
                return;

            Rotations[piece] = value & 3;
        }
    }
}
