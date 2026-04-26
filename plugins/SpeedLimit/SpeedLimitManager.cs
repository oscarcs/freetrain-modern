using System;
using System.Collections;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using freetrain.DirectXWrapper;
using freetrain.framework.graphics;
using freetrain.world;



namespace freetrain.world.rail.speedlimit
{
    public class SpeedLimitManager
    {

        /// <summary>
        /// 低速区間図示用アイコン
        /// </summary>
        public static readonly Surface lowSpeedIcon
            = PictureManager.get("{CFC7C99B-4EDE-493B-9DD1-268633902901}").surface;

        /// <summary>
        /// 中速区間図示用アイコン
        /// </summary>
        public static readonly Surface mediumSpeedIcon
            = PictureManager.get("{CFC7C99B-4EDE-493B-9DD1-268633902902}").surface;


        /// <summary>
        /// 高速区間図示用アイコン
        /// </summary>
        public static readonly Surface fastSpeedIcon
            = PictureManager.get("{CFC7C99B-4EDE-493B-9DD1-268633902903}").surface;


        /// <summary>
        /// 鉄道上にない速度制限を削除。
        /// </summary>
        public static void removeNotTrafficVoxel()
        {
            SpeedLimit sl = SpeedLimit.getInstance();
            //低速
            foreach (Location loc in getRemoveLocations(sl.LowSpeedLimit))
            {
                sl.LowSpeedLimit.Remove(loc);
            }
            //中速
            foreach (Location loc in getRemoveLocations(sl.MediumSpeedLimit))
            {
                sl.MediumSpeedLimit.Remove(loc);
            }
            //高速
            foreach (Location loc in getRemoveLocations(sl.FastSpeedLimit))
            {
                sl.FastSpeedLimit.Remove(loc);
            }
        }

        /// <summary>
        /// 鉄道上にない削除対象となる速度制限のロケーションを返す
        /// </summary>
        private static ArrayList getRemoveLocations(ArrayList list)
        {
            ArrayList tmp = new ArrayList();
            foreach (Location loc in list)
            {
                TrafficVoxel vox = TrafficVoxel.get(loc);
                if (vox == null)
                {
                    tmp.Add(loc);
                }
            }
            return tmp;
        }



        /// <summary>
        /// TrafficVoxelでないものを含んでいるか。
        /// 含んでいればtrue、含んでなければfalseを返す。
        /// </summary>
        public static bool isNotOnlyTrafficVoxel(ArrayList route)
        {
            foreach (Location loc in route)
            {
                TrafficVoxel vox = TrafficVoxel.get(loc);
                if (vox == null) { return true; }
            }
            return false;
        }


        /// <summary>
        /// TrafficVoxelでないものを含んでいるか。
        /// 含んでいればtrue、含んでなければfalseを返す。
        /// </summary>
        public static bool isNotOnlyTrafficVoxel(Hashtable route)
        {
            foreach (Location loc in route.Keys)
            {
                TrafficVoxel vox = TrafficVoxel.get(loc);
                if (vox == null) { return true; }
            }
            return false;
        }


    }
}
