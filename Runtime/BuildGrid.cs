using System;
using System.Collections.Generic;
using UnityEngine;

namespace LOGIYGames.SurvivalBuilding
{
    public readonly struct PieceKey : IEquatable<PieceKey>
    {
        public readonly int PieceId;
        public readonly UnityEngine.Vector3Int Anchor;
        public readonly int Variant;

        public PieceKey(int id, UnityEngine.Vector3Int anchor, int variant)
        {
            PieceId = id; Anchor = anchor; Variant = variant;
        }

        public bool Equals(PieceKey o) => PieceId == o.PieceId && Anchor == o.Anchor && Variant == o.Variant;
        public override bool Equals(object o) => o is PieceKey k && Equals(k);
        public override int GetHashCode() =>
            (PieceId * 397) ^ (Anchor.x * 73856093) ^ (Anchor.y * 83492791) ^ (Anchor.z * 19349663) ^ Variant;
    }

    public enum SlotKind { Cell, Edge, Stair }

    public readonly struct Slot : IEquatable<Slot>
    {
        public readonly SlotKind Kind;
        public readonly Vector3Int Cell;
        public readonly int Side;

        Slot(SlotKind k, Vector3Int c, int s) { Kind = k; Cell = c; Side = s; }

        public static Slot CellSlot(Vector3Int c) => new Slot(SlotKind.Cell, c, 0);

        public static Slot StairSlot(Vector3Int c) => new Slot(SlotKind.Stair, c, 0);

        public static Slot Edge(Vector3Int a, int side)
        {
            // канонизация: ребро (a, s) и (a+offset(s), (s+2)&3) — одно и то же.
            // Берём «меньшую» клетку.
            var b = a + BuildGrid.SideOffset(side);
            int cmp = Compare(a, b);
            return cmp < 0 ? new Slot(SlotKind.Edge, a, side)
                           : new Slot(SlotKind.Edge, b, (side + 2) & 3);
        }

        static int Compare(Vector3Int a, Vector3Int b)
        {
            if (a.x != b.x) return a.x.CompareTo(b.x);
            if (a.y != b.y) return a.y.CompareTo(b.y);
            return a.z.CompareTo(b.z);
        }

        public bool Equals(Slot o) => Kind == o.Kind && Cell == o.Cell && Side == o.Side;
        public override bool Equals(object o) => o is Slot s && Equals(s);
        public override int GetHashCode() =>
            ((int)Kind * 397) ^ (Cell.x * 73856093) ^ (Cell.y * 83492791) ^ (Cell.z * 19349663) ^ Side;
    }

    [System.Serializable]
    public class GridSettings
    {
        [Min(0.25f)] public float Cell = 2f;
        [Min(0.25f)] public float Level = 2f;
    }
    public class BuildGrid : MonoBehaviour
    {
        public readonly HashSet<Vector2Int> Columns = new();
        public Vector3 Origin;
        public Quaternion Rotation;
        public float Cell = 2f, Level = 2f;

        public Vector3Int MinCell = new Vector3Int(int.MaxValue, int.MaxValue, int.MaxValue);
        public Vector3Int MaxCell = new Vector3Int(int.MinValue, int.MinValue, int.MinValue);
        public bool HasCells => Occupied.Count > 0;

        public readonly Dictionary<PieceKey, BuildPiece> Occupied = new();
        public readonly Dictionary<Slot, PieceKey> SlotIndex = new();

        // ---- grid math ----

        public Vector3 CellCenter(Vector3Int c) =>
            Origin + Rotation * new Vector3(c.x * Cell, c.y * Level, c.z * Cell);

        public Vector3 EdgePoint(Vector3Int c, int side) =>
            CellCenter(c) + Rotation * (SideDir(side) * (Cell * 0.5f));

        public Vector3Int WorldToCell(Vector3 w)
        {
            Vector3 l = Quaternion.Inverse(Rotation) * (w - Origin);
            return new Vector3Int(
                Mathf.RoundToInt(l.x / Cell),
                Mathf.RoundToInt(l.y / Level),
                Mathf.RoundToInt(l.z / Cell));
        }
        public int SideFromWorld(Vector3 worldDir)
        {
            Vector3 l = Quaternion.Inverse(Rotation) * worldDir;
            l.y = 0f;
            return Mathf.Abs(l.x) > Mathf.Abs(l.z)
                ? (l.x > 0 ? 1 : 3)
                : (l.z > 0 ? 0 : 2);
        }
        public static Vector3 SideDir(int s) => s switch
        {
            0 => Vector3.forward,
            1 => Vector3.right,
            2 => Vector3.back,
            3 => Vector3.left
        };

        public static Vector3Int SideOffset(int s) => s switch
        {
            0 => new Vector3Int(0, 0, 1),
            1 => new Vector3Int(1, 0, 0),
            2 => new Vector3Int(0, 0, -1),
            _ => new Vector3Int(-1, 0, 0)
        };

        // ---- storage ----

        public void Register(PieceKey key, BuildPiece piece, IEnumerable<Slot> slots)
        {
            Occupied[key] = piece;
            foreach (var s in slots) SlotIndex[s] = key;
            foreach (var xz in piece.Definition.GetOccupiedColumns(key))
                Columns.Add(xz);
            MinCell = Vector3Int.Min(MinCell, key.Anchor);
            MaxCell = Vector3Int.Max(MaxCell, key.Anchor);
        }

        public void Unregister(PieceKey key, IEnumerable<Slot> slots)
        {
            Occupied.Remove(key);
            foreach (var s in slots) SlotIndex.Remove(s);

            // Пересчёт колонок и границ — просто и безопасно (удаление редкое)
            RebuildColumns();
            RebuildBounds();
        }
        void RebuildColumns()
        {
            Columns.Clear();
            foreach (var kvp in Occupied)
                foreach (var xz in kvp.Value.Definition.GetOccupiedColumns(kvp.Key))
                    Columns.Add(xz);
        }

        void RebuildBounds()
        {
            if (Occupied.Count == 0)
            {
                MinCell = new Vector3Int(int.MaxValue, int.MaxValue, int.MaxValue);
                MaxCell = new Vector3Int(int.MinValue, int.MinValue, int.MinValue);
                return;
            }

            var min = new Vector3Int(int.MaxValue, int.MaxValue, int.MaxValue);
            var max = new Vector3Int(int.MinValue, int.MinValue, int.MinValue);
            foreach (var k in Occupied.Keys)
            {
                min = Vector3Int.Min(min, k.Anchor);
                max = Vector3Int.Max(max, k.Anchor);
            }
            MinCell = min;
            MaxCell = max;
        }
        public bool IsSlotOccupied(Slot s) => SlotIndex.ContainsKey(s);

        public bool TryGetPieceAtSlot(Slot s, out BuildPiece piece)
        {
            piece = null;
            return SlotIndex.TryGetValue(s, out var k) && Occupied.TryGetValue(k, out piece);
        }
        public bool WouldRemainSupported(PieceKey removedKey)
        {
            if (Occupied.Count == 0) return true;
            if (Occupied.Count == 1 && Occupied.ContainsKey(removedKey)) return true;

            var visited = new HashSet<PieceKey>();
            var queue = new Queue<PieceKey>();

            // Якоря: полы на земле, кроме удаляемого
            foreach (var kvp in Occupied)
            {
                if (kvp.Key.Equals(removedKey)) continue;

                if (kvp.Value.Definition is FloorPiece fp && fp.IsOnGround(kvp.Key.Anchor, this))
                {
                    if (visited.Add(kvp.Key)) queue.Enqueue(kvp.Key);
                }
            }

            // BFS: от опорных деталей к зависимым
            while (queue.Count > 0)
            {
                var currKey = queue.Dequeue();
                var curr = Occupied[currKey];

                foreach (var dep in curr.Definition.GetDependents(currKey, this))
                {
                    if (dep == null) continue;
                    if (dep.Key.Equals(removedKey)) continue;
                    if (visited.Add(dep.Key)) queue.Enqueue(dep.Key);
                }
            }

            // Все оставшиеся должны быть достижимы
            foreach (var kvp in Occupied)
            {
                if (kvp.Key.Equals(removedKey)) continue;
                if (!visited.Contains(kvp.Key)) return false;
            }
            return true;
        }
        public float DistanceToBounds(Vector3 world)
        {
            if (Occupied.Count == 0) return float.MaxValue;
            if (!HasCells) return float.MaxValue;
            Vector3 l = Quaternion.Inverse(Rotation) * (world - Origin);
            float cx = l.x / Cell, cy = l.y / Level, cz = l.z / Cell;
            float dx = (cx - Mathf.Clamp(cx, MinCell.x, MaxCell.x)) * Cell;
            float dy = (cy - Mathf.Clamp(cy, MinCell.y, MaxCell.y)) * Level;
            float dz = (cz - Mathf.Clamp(cz, MinCell.z, MaxCell.z)) * Cell;
            return Mathf.Sqrt(dx * dx + dy * dy + dz * dz);
        }
    }

    [Serializable]
    public class MaterialVariant
    {
        public MaterialDefinition Material;
        public GameObject Prefab;
        public GameObject GhostPrefab;
    }

    public struct PlacementContext
    {
        public RaycastHit Hit;
        public BuildPiece HitPiece;
        public BuildMarker HitMarker;

        public BuildGrid Grid;      // null → новая сетка
        public Vector3 ObserverPosition;   // ← было Camera


        public MaterialDefinition Material;
        public BuildInputState Input;

        public float FirstYaw;
        public float FloorThickness;
    }

    public struct PlacementResult
    {
        public bool Show;
        public bool Valid;
        public PlacementFailReason FailReason;   // ← новое; None если Valid
        public PieceKey Key;
        public Vector3 Position;
        public Quaternion Rotation;
        public BuildGrid TargetGrid;   // null → создать новую

        public GameObject Prefab;
        public GameObject GhostPrefab;
    }
    public enum DestroyFailReason
    {
        None,
        HasStairOnFloor,   // на полу стоит лестница
        HasWallOnFloor,    // на ребре пола стоит стена
        HasFloorOnWall,    // на стене стоит пол
        HasFloorOnStair,   // над лестницей стоит пол
        WouldOrphanAdjacentFloor,   // ← новое
    }
    public enum PlacementFailReason
    {
        None,               // всё ок
        WrongSurface,       // луч попал не в тот тип (стена: не на пол; проём: не в стену)
        SlotOccupied,       // место уже занято
        NoSupport,          // нет опоры (пол: ни стены/пола под, стена: нет пола под)
        CrossesOtherGrid,   // пересекает куски чужой сетки
        BlockedByWorld,     // мешает объект из BlockerLayers
        NoMaterialVariant,  // для этого материала нет префаба
        CantileverTooFar,   // ← новое
    }

    public struct MarkerSpot
    {
        public PieceDefinition Piece;
        public BuildGrid Grid;
        public PieceKey Key;
        public Vector3 Position;
        public Quaternion Rotation;
    }

    public class BuildInputState
    {
        public readonly Dictionary<PieceDefinition, int> Rotations = new();
        public float FirstYawAccum;

        public int GetRot(PieceDefinition p) => Rotations.TryGetValue(p, out var v) ? v : 0;
        public void SetRot(PieceDefinition p, int v) => Rotations[p] = v;
    }
}