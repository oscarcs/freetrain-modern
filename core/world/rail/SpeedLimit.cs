using System;
using System.Collections;
using System.Diagnostics;
using System.Drawing;
using freetrain.framework;
using freetrain.DirectXWrapper;


namespace freetrain.world.rail
{
    /// <summary>
    /// 速度制限　
    /// </summary>
    [Serializable]
    public class SpeedLimit
    {
        /// <summary>
        /// 
        /// </summary>
        private static SpeedLimit theInstance = null;

        /// <summary>
        /// 
        /// </summary>
        private static void onNewWorld(object sender, EventArgs a)
        {
            //SpeedLimitの永続化に関して
            theInstance = (SpeedLimit)World.world.otherObjects["{2B4B87D9-1856-4558-BD07-8C5D5467D109}"];
            if (theInstance == null)
            {
                World.world.otherObjects["{2B4B87D9-1856-4558-BD07-8C5D5467D109}"] = theInstance =
                    new SpeedLimit();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public static void init()
        {
            World.onNewWorld += new EventHandler(onNewWorld);
        }

        /// <summary>
        /// インスタンスを取得
        /// </summary>
        public static SpeedLimit getInstance()
        {
            return theInstance;
        }

        /// <summary>
        /// 低速区間
        /// </summary>
        ArrayList lowSpeedLimit;

        /// <summary>
        /// 低速区間
        /// </summary>
        public ArrayList LowSpeedLimit
        {
            get { return lowSpeedLimit; }
        }

        /// <summary>
        /// 中速区間
        /// </summary>
        ArrayList mediumSpeedLimit;

        /// <summary>
        /// 中速区間
        /// </summary>
        public ArrayList MediumSpeedLimit
        {
            get { return mediumSpeedLimit; }
        }

        /// <summary>
        /// 高速区間
        /// </summary>
        ArrayList fastSpeedLimit;

        /// <summary>
        /// 高速区間
        /// </summary>
        public ArrayList FastSpeedLimit
        {
            get { return fastSpeedLimit; }
        }


        /// <summary>
        /// コンストラクタ
        /// </summary>
        private SpeedLimit()
        {
            lowSpeedLimit = new ArrayList();
            mediumSpeedLimit = new ArrayList();
            fastSpeedLimit = new ArrayList();
        }



        /// <summary>
        /// リストに低速区間を追加する
        /// </summary>
        public void addLowSpeedLimit(ArrayList locs)
        {
            lowSpeedLimit.AddRange(locs);
        }

        /// <summary>
        /// リストに中速区間を追加する
        /// </summary>
        public void addMediumSpeedLimit(ArrayList locs)
        {
            mediumSpeedLimit.AddRange(locs);
        }

        /// <summary>
        /// リストに高速区間を追加する
        /// </summary>
        public void addFastSpeedLimit(ArrayList locs)
        {
            fastSpeedLimit.AddRange(locs);
        }

        /// <summary>
        /// 速度制限（低速）のLocationを追加する。
        /// ただし、すでに追加してあるロケーションは追加しない。
        /// </summary>
        public void addLowSpeedLimit(Hashtable route)
        {
            // キーにLocationが入ってるのでそれを利用
            foreach (Location loc in route.Keys)
            {
                //同じものがあったら入れない。
                if (!lowSpeedLimit.Contains(loc))
                {
                    lowSpeedLimit.Add(loc);
                }
            }
        }

        /// <summary>
        /// 速度制限（中速）のLocationを追加する。
        /// ただし、すでに追加してあるロケーションは追加しない。
        /// </summary>
        public void addMediumSpeedLimit(Hashtable route)
        {
            // キーにLocationが入ってるのでそれを利用
            foreach (Location loc in route.Keys)
            {
                //同じものがあったら入れない。
                if (!mediumSpeedLimit.Contains(loc))
                {
                    mediumSpeedLimit.Add(loc);
                }
            }
        }

        /// <summary>
        /// 速度制限（高速）のLocationを追加する。
        /// ただし、すでに追加してあるロケーションは追加しない。
        /// </summary>
        public void addFastSpeedLimit(Hashtable route)
        {
            // キーにLocationが入ってるのでそれを利用
            foreach (Location loc in route.Keys)
            {
                //同じものがあったら入れない。
                if (!fastSpeedLimit.Contains(loc))
                {
                    fastSpeedLimit.Add(loc);
                }
            }
        }

        /// <summary>
        /// 低速区間を削除する。
        /// </summary>
        public void removeLowSpeedLimit(ArrayList locs)
        {
            foreach(Location loc in locs)
            {
                lowSpeedLimit.Remove(loc);
            }
        }

        /// <summary>
        /// 中速区間を削除する。
        /// </summary>
        public void removeMediumSpeedLimit(ArrayList locs)
        {
            foreach (Location loc in locs)
            {
                mediumSpeedLimit.Remove(loc);
            }
        }

        /// <summary>
        /// 高速区間を削除する。
        /// </summary>
        public void removeFastSpeedLimit(ArrayList locs)
        {
            foreach (Location loc in locs)
            {
                fastSpeedLimit.Remove(loc);
            }
        }

        /// <summary>
        /// 低速区間を削除する。
        /// </summary>
        public void removeLowSpeedLimit(Hashtable route)
        {
            // キーにLocationが入ってるのでそれを利用
            foreach (Location loc in route.Keys)
            {
                lowSpeedLimit.Remove(loc);
            }
        }

        /// <summary>
        /// 中速区間を削除する。
        /// </summary>
        public void removeMediumSpeedLimit(Hashtable route)
        {
            // キーにLocationが入ってるのでそれを利用
            foreach (Location loc in route.Keys)
            {
                mediumSpeedLimit.Remove(loc);
            }
        }

        /// <summary>
        /// 高速区間を削除する。
        /// </summary>
        public void removeFastSpeedLimit(Hashtable route)
        {
            // キーにLocationが入ってるのでそれを利用
            foreach (Location loc in route.Keys)
            {
                fastSpeedLimit.Remove(loc);
            }
        }


        /// <summary>
        /// ロケーションが既に設定されている速度制限に含まれるか
        /// </summary>
        public bool Contains(Hashtable route)
        {
            foreach (Location loc in route.Keys)
            {
                if (lowSpeedLimit.Contains(loc) || mediumSpeedLimit.Contains(loc)
                    || fastSpeedLimit.Contains(loc))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// ロケーションが既に設定されている速度制限に含まれるか
        /// </summary>
        public bool Contains(ArrayList list)
        {
            foreach (Location loc in list)
            {
                if (lowSpeedLimit.Contains(loc) || mediumSpeedLimit.Contains(loc)
                    || fastSpeedLimit.Contains(loc))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
