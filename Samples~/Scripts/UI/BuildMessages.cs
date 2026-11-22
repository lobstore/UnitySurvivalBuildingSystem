using UnityEngine;

namespace LOGIYGames.SurvivalBuilding
{
    /// <summary>Localized display text for placement and destruction failure reasons.</summary>
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

        /// <summary>Resolves a localized message for a destruction failure.</summary>
        /// <param name="reason">Reason why removal is blocked.</param>
        /// <returns>Configured message, or <see langword="null"/> when no specialized message exists.</returns>
        public string ForDestroy(DestroyFailReason reason) => reason switch
        {
            DestroyFailReason.HasStairOnFloor => CannotDestroyStairOnFloor,
            DestroyFailReason.HasWallOnFloor => CannotDestroyWallOnFloor,
            DestroyFailReason.HasFloorOnWall => CannotDestroyFloorOnWall,
            DestroyFailReason.HasFloorOnStair => CannotDestroyFloorOnStair,
            DestroyFailReason.WouldOrphanAdjacentFloor => CannotDestroyOrphanFloor,
            _ => CannotDestroy,
        };

        /// <summary>Resolves a localized message for a placement failure.</summary>
        /// <param name="reason">Reason why the placement is invalid.</param>
        /// <returns>Configured message, or <see langword="null"/> when the reason is None.</returns>
        public string For(PlacementFailReason reason) => reason switch
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

        /// <summary>Resolves a placement failure, preferring custom surface text on the definition.</summary>
        /// <param name="definition">Piece definition that evaluated the placement.</param>
        /// <param name="reason">Reason why the placement is invalid.</param>
        /// <returns>Piece-specific surface text or the shared message for the failure reason.</returns>
        public string For(PieceDefinition definition, PlacementFailReason reason)
        {
            if (reason == PlacementFailReason.WrongSurface
                && definition != null
                && !string.IsNullOrEmpty(definition.SurfaceRequirementText))
            {
                return definition.SurfaceRequirementText;
            }

            return For(reason);
        }
    }
}
