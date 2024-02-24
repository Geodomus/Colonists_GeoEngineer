using HarmonyLib;
using UnityEngine;

namespace GeoEngineer
{
    public class GeoEngineer
    {
        [StaticConstructorOnStartup]
        public static int Init()
        {
            Harmony.DEBUG = true;

            var harmony = new Harmony("GeoEngineer v2");

            FileLog.Log("testing harmony logging");

            harmony.PatchAll();
            SerialiseFactory.AddCustom(typeof(EntityDataComponentInfo), 200,
                typeof(BuildingProductionGeoEngineerDataInfo));
            SerialiseFactory.AddCustom(typeof(EntityDataComponent), 200,
                typeof(BuildingProductionGeoEngineerData));

            Debug.Log("#GeoEngineerV2# Init Complete!");

            return 2;
        }
    }
}