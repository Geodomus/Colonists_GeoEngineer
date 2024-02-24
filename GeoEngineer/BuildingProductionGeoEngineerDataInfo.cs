using System.Collections.Generic;

namespace GeoEngineer
{
    public class BuildingProductionGeoEngineerDataInfo : BuildingProductionDataInfo
    {
        public BuildingProductionGeoEngineerDataInfo() => this.typeId = (EntityDataComponent.TypeID)200;
        public readonly HashSet<TerrainCategory> ValidTerrainCategoryHash = new HashSet<TerrainCategory>(TerrainCategoryComparer.instance);
    }
}