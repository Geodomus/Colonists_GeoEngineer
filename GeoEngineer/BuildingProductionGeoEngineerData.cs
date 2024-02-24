using System.Collections.Generic;
using UnityEngine;

namespace GeoEngineer
{
    public class BuildingProductionGeoEngineerData : BuildingProductionData
    {
        private readonly List<FloorTileLayer> _selectedRouteLayers = new List<FloorTileLayer>();
        private List<FloorTileLayerRef> _ppSelectedRouteLayerFloorPositions = new List<FloorTileLayerRef>();
        private const int CurrentTreeHeightLeewayNeeded = 0;

        private HashSet<TerrainId> _zzzCurrentValidTerrainHash =
            new HashSet<TerrainId>(FloorTileLayerTerrainTypeComparer.instance);

        private HashSet<TerrainCategory> _currentValidTerrainCategoryHash =
            new HashSet<TerrainCategory>(TerrainCategoryComparer.instance);

        private FloorTileLayer _currentWorkerTargetLayer;
        private FloorTileLayerRef _ppCurrentWorkerTargetLayerRef;
        private Vector3 _nextSubPosition;

        public BuildingProductionGeoEngineerData() => this.typeId = (TypeID)200;

        public override BuildingProductionDataInfo GetInfo() => this.building.GetInfo()
            .GetAgeCompInfo<BuildingProductionGeoEngineerDataInfo>(this.building.age,
                (TypeID)200);

        public override BuildingProductionDataInfo GetUpgradeInfo() => this.building
            .GetInfo().GetAgeCompInfo<BuildingProductionGeoEngineerDataInfo>(this.building.age.ToNextAge(),
                (TypeID)200);

        public override void OnCreation(Entity cEntity)
        {
            base.OnCreation(cEntity);
            var validTerrainCategoryHash = (GetInfo() as BuildingProductionGeoEngineerDataInfo)?.ValidTerrainCategoryHash;
            if (validTerrainCategoryHash != null)
                foreach (var entry in validTerrainCategoryHash)
                {
                    _currentValidTerrainCategoryHash.Add(entry);
                }
        }

        public override void OnPostDeserialise(Entity dEntity)
        {
            base.OnPostDeserialise(dEntity);
            this._selectedRouteLayers.Clear();
            foreach (var t in this._ppSelectedRouteLayerFloorPositions)
                this._selectedRouteLayers.Add(
                    Game.instance.floorData.GetFloorTileLayerByRef(t));

            if (this._ppCurrentWorkerTargetLayerRef != null)
                this._currentWorkerTargetLayer = this._ppCurrentWorkerTargetLayerRef.GetLayer();
        }

        public override void OnDestroy()
        {
            this.OnLose();
            base.OnDestroy();
        }

        public override void OnLose()
        {
            base.OnLose();
            if (!(this._currentWorkerTargetLayer != null))
                return;
            this._currentWorkerTargetLayer.Unreserve();
            this._currentWorkerTargetLayer = null;
        }

        protected override bool TryWorkerUnitHeadToProduction(Unit unit)
        {
            this._selectedRouteLayers.Clear();
            if (this.building.priorityLayersData.priorityLayers.Count > 0)
            {
                int count = this.building.priorityLayersData.priorityLayers.Count;
                while (count-- > 0)
                {
                    FloorTileLayer priorityLayer = this.building.priorityLayersData.priorityLayers[count];
                    if (priorityLayer.CanReserveForPlantingTree(this._currentValidTerrainCategoryHash,
                            this.building.ownerData.owner, CurrentTreeHeightLeewayNeeded))
                    {
                        FloorTileLayerPathfinder.CalculateFloorTileLayerRoute(this._selectedRouteLayers,
                            this.building.occupierData.mainBaseLayer,
                            this.building.priorityLayersData.priorityLayers[count]);
                        if (this._selectedRouteLayers.Count > 0)
                        {
                            this._currentWorkerTargetLayer = priorityLayer;
                            break;
                        }
                    }
                }
            }
            if (this._selectedRouteLayers.Count <= 0)
                return false;
            Item targetItem =
                Game.instance.CreateItem(ItemID.SAPLING, this.building.ownerData.owner, this.building, unit);
            this._selectedRouteLayers[this._selectedRouteLayers.Count - 1]
                .ReserveForFutureOccupier(EntityInfo.Type.FLORA, 200, unit);
            unit.SetTargetItem(targetItem);
            unit.PickUpItem(targetItem, true);
            (unit as ForesterUnit)?.ToggleCarrySapling(true);
            return true;
        }

        protected override void SendUnitToWork(Unit unit)
        {
            FloorTileLayerSubLocation layerSubLocation = ReusableData.GetLayerSubLocation();
            layerSubLocation.layer = this._selectedRouteLayers[this._selectedRouteLayers.Count - 1];
            unit.HeadToWork(this._selectedRouteLayers, layerSubLocation);
            this._selectedRouteLayers.Clear();
        }

        public override bool OnWorkerUnitReachWork(Unit unit)
        {
            (unit as ForesterUnit)?.ToggleCarrySapling(false);
            return base.OnWorkerUnitReachWork(unit);
        }

        protected override float GetProcessTime(Unit unit = null)
        {
            double processTime = base.GetProcessTime(unit);
            float num = 0.75f;
            if (this._currentWorkerTargetLayer != null &&
                (this._currentWorkerTargetLayer.terrainCategory1 == TerrainCategory.Snow ||
                 this._currentWorkerTargetLayer.terrainCategory1 == TerrainCategory.Snow))
                num = Game.instance.codex.terrainInfo.snowTreeGrowthTimeFactor;
            return (float)processTime * num;
        }

        public override void OnCompleteWorkerProcessing(Unit unit)
        {
            Game.instance.AddCustomLog(new DebugGameCustomLogCommand.Entry()
            {
                type = DebugGameCustomLogCommand.EntryType.ForestrCompleteWorking,
                uid = this.building.uid
            });
            if (unit.locomotorData.currentLayer.CanPlantTree(this._currentValidTerrainCategoryHash,
                    this.building.ownerData.owner, CurrentTreeHeightLeewayNeeded 
                )&& unit.locomotorData.currentLayer.IsReservedBy(unit))
            {
                unit.locomotorData.currentLayer.SetTerrain(TerrainId.Grass);
            }

            if (this._currentWorkerTargetLayer != null)
            {
                this._currentWorkerTargetLayer.Unreserve();
                this._currentWorkerTargetLayer = null;
            }

            Game.instance.ConsumeItem(this.building.ownerData.owner, unit.carryingItem);
            unit.UnsetCarryingItem();
            this.OnCompleteBuildingProcessing();
            foreach (Building building1 in this.building.ownerData.owner.buildingsDict[(BuildingID) 11])
                building1.ForceWorkerIdleCheck();
            unit.ReturnToIdle(Unit.State.WORKER_FROM_PROCESSING);
        }

        protected override void CancelProduction()
        {
            base.CancelProduction();
            if (!(this._currentWorkerTargetLayer != null))
                return;
            this._currentWorkerTargetLayer.Unreserve();
            this._currentWorkerTargetLayer = null;
        }

        public override int Serialise(byte[] buffer, int offset)
        {
            List<FloorTileLayerRef> objects1 = new List<FloorTileLayerRef>();

            offset = DataEncoder.EncodeList(objects1, buffer, offset);
            List<FloorTileLayerRef> objects2 = new List<FloorTileLayerRef>();
            int count2 = this._selectedRouteLayers.Count;
            for (int index = 0; index < count2; ++index)
            {
                var layerRef = new FloorTileLayerRef(_selectedRouteLayers[index].coord, _selectedRouteLayers[index].id);
                objects2.Add(layerRef);
            }
            offset = DataEncoder.EncodeList(objects2, buffer, offset);
            offset = DataEncoder.Encode(this._zzzCurrentValidTerrainHash, buffer, offset);
            offset = DataEncoder.EncodeFloorTileLayer(this._currentWorkerTargetLayer, buffer, offset);
            offset = DataEncoder.Encode(this._nextSubPosition, buffer, offset);
            offset = DataEncoder.Encode(this._currentValidTerrainCategoryHash, buffer, offset);
            return base.Serialise(buffer, offset);
        }

        public override int Deserialise(byte[] buffer, int offset)
        {
            this._ppSelectedRouteLayerFloorPositions = new List<FloorTileLayerRef>();
            offset = DataEncoder.DecodeList(out this._ppSelectedRouteLayerFloorPositions, buffer,
                offset);
            if (Game.instance.currentBundleVersion >= 493)
            {
                offset = DataEncoder.Decode(out this._zzzCurrentValidTerrainHash, buffer, offset);
            }

            offset = DataEncoder.DecodeFloorTileLayer(out this._ppCurrentWorkerTargetLayerRef, buffer, offset);
            if (Game.instance.currentBundleVersion >= 993)
                offset = DataEncoder.Decode(out this._nextSubPosition, buffer, offset);
            if (Game.instance.currentBundleVersion >= 1120)
                offset = DataEncoder.Decode(out this._currentValidTerrainCategoryHash, buffer, offset);
            return base.Deserialise(buffer, offset);
        }

        public override bool IsValidPriorityLayer(FloorTileLayer layer)
        {
            return layer.IsUnoccupied() && layer.IsLandTerrainCategory(this._currentValidTerrainCategoryHash) &&
                   layer.parentTile.owner == this.building.ownerData.owner && !layer.HasAnyLandRoutes();
        }
    }
}