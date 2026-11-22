using UnityEngine;

namespace LOGIYGames.SurvivalBuilding
{
    [CreateAssetMenu(menuName = "Building/Messages")]
    public class BuildMessages : ScriptableObject
    {
        [TextArea] public string WrongSurface = "Сюда нельзя поставить этот тип";
        [TextArea] public string SlotOccupied = "Здесь уже что-то стоит";
        [TextArea] public string NoSupport = "Нужна опора (пол или стена)";
        [TextArea] public string CrossesOtherGrid = "Пересекается с другой постройкой";
        [TextArea] public string BlockedByWorld = "Мешает объект";
        [TextArea] public string NoMaterialVariant = "Нет версии из этого материала";
        [TextArea] public string CantileverTooFar = "Слишком далеко от опоры";
        [TextArea] public string CannotDestroy = "Нельзя удалить: на этой части есть зависимые";
        [TextArea] public string CannotDestroyStairOnFloor = "Нельзя удалить: на этом полу стоит лестница";
        [TextArea] public string CannotDestroyWallOnFloor = "Нельзя удалить: на этом полу стоит стена";
        [TextArea] public string CannotDestroyFloorOnWall = "Нельзя удалить: на этой стене стоит пол";
        [TextArea] public string CannotDestroyFloorOnStair = "Нельзя удалить: над этой лестницей стоит пол";
        [TextArea] public string CannotDestroyOrphanFloor = "Нельзя удалить: соседний пол потеряет опору";
        public string ForDestroy(DestroyFailReason r) => r switch
        {
            DestroyFailReason.HasStairOnFloor => CannotDestroyStairOnFloor,
            DestroyFailReason.HasWallOnFloor => CannotDestroyWallOnFloor,
            DestroyFailReason.HasFloorOnWall => CannotDestroyFloorOnWall,
            DestroyFailReason.HasFloorOnStair => CannotDestroyFloorOnStair,
            DestroyFailReason.WouldOrphanAdjacentFloor => CannotDestroyOrphanFloor,
            _ => null,
        };
        public string For(PlacementFailReason r) => r switch
        {
            PlacementFailReason.WrongSurface => WrongSurface,
            PlacementFailReason.SlotOccupied => SlotOccupied,
            PlacementFailReason.NoSupport => NoSupport,
            PlacementFailReason.CrossesOtherGrid => CrossesOtherGrid,
            PlacementFailReason.BlockedByWorld => BlockedByWorld,
            PlacementFailReason.NoMaterialVariant => NoMaterialVariant,
            PlacementFailReason.CantileverTooFar => CantileverTooFar,
            _ => null,
        };
        public string For(PieceDefinition def, PlacementFailReason r)
        {
            if (r == PlacementFailReason.WrongSurface
                && def != null
                && !string.IsNullOrEmpty(def.SurfaceRequirementText))
                return def.SurfaceRequirementText;
            return For(r);
        }
    }
}