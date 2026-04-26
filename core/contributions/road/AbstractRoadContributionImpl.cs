// 2009.05.10 Yz 同一ボクセル内に列車がある場合、道路を撤去不可能に変更
using System;
using System.Diagnostics;
using System.Drawing;
using System.Xml;
using freetrain.framework;
using freetrain.framework.graphics;
using freetrain.framework.plugin;
using freetrain.world;
using freetrain.world.rail;
using freetrain.world.road;
using freetrain.world.accounting;

namespace freetrain.contributions.road
{
	/// <summary>
	/// Usual implementation of RoadContribution.
	/// 
	/// Provided just for code sharing.
	/// </summary>
	[Serializable]
	public abstract class AbstractRoadContributionImpl : RoadContribution
	{
		private readonly string _name;
		private readonly string description;
		
		protected AbstractRoadContributionImpl( XmlElement e ) : base(e) 
		{
			_name = XmlUtil.selectSingleNode(e,"name").InnerText;
			description = XmlUtil.selectSingleNode(e,"description").InnerText;
		}

		protected internal abstract Sprite getSprite( byte dirs );

		public override bool canBeBuilt( Location from, Location to ) 
		{
			if( from==to )	return false;

			Direction d = from.getDirectionTo(to);

			Location here = from;

			while(true) 
			{
				if( World.world[here]!=null ) 
				{
					TrafficVoxel v = TrafficVoxel.get(here);
					if(v==null)				return false;	// occupied
					if(v.road!=null) 
					{
						if( !v.road.canAttach(d) && here!=to )	return false;
						if( !v.road.canAttach(d.opposite) && here!=from )	return false;
					}
					if(v.car!=null)			return false;	// car is in place
					// TODO: check v.railRoad
				}

				if( here==to )	return true;
				here = here.toward(to);
			}
		}

		public override void build( Location from, Location to ) 
		{
			Debug.Assert( canBeBuilt(from,to) );

			Direction d = from.getDirectionTo(to);

            long cost = 0;
            cost = calcCostOfBuildRoad( from, to );
            if( this.style.Type == MajorRoadType.railballast )
                AccountGenre.RAIL_SERVICE.spend(cost);	// charge the cost
            else
                AccountGenre.ROAD_SERVICE.spend(cost);
          
			Location here = from;
			while(true) 
			{
				Road r = Road.get(here);
				if( r==null ) 
				{
					RoadPattern p = RoadPattern.getStraight(d);
					if( here==from )	p = RoadPattern.get((byte)(1<<(d.index/2)));
					if( here==to   )	p = RoadPattern.get((byte)(1<<(d.opposite.index/2)));

					create( TrafficVoxel.getOrCreate(here), p );
				} 
				else 
				{
					if( here!=from )	r.attach( d.opposite );
					if( here!=to   )	r.attach( d );
				}

				if( here==to )	return;
				here = here.toward(to);
			}
		}

		/// <summary>
		/// Creates a new road with a given pattern.
		/// </summary>
		protected virtual Road create( TrafficVoxel voxel, RoadPattern pattern ) 
		{
			return new RoadImpl( this, voxel, pattern );
		}


		public override void remove( Location here, Location to ) 
		{
			TrafficVoxel v = null;

            if( here==to )	return;

			Direction d = here.getDirectionTo(to);

            long cost = calcCostOfRemoving( here, to );
			AccountGenre.ROAD_SERVICE.spend(cost);	// charge the cost

			while(true) 
			{
				Road r = Road.get(here);
				if( r!=null ) {
                    v = World.world[here] as TrafficVoxel;                      // 現在対象の位置のボクセルを取得
                    if (v != null) {                                            // ボクセルがトラフィックボクセルの場合
                        if (v.car != null) {                                    // 該当ボクセルに車が存在する場合
                            if (v.car is Train.TrainCar) {                      // 車が列車の場合
                            } else {
                                r.detach( d, d.opposite );
                            }
                        } else {
                            r.detach( d, d.opposite );
                        }
                    } else {
                        r.detach( d, d.opposite );
                    }
                }

                if(here==to)	return;
				here = here.toward(to);
			}
		}

		public override string name 
		{
			get 
			{
				return _name;
			}
		}

		public override string oneLineDescription 
		{
			get 
			{
				return description;
			}
		}


		public override Bitmap previewBitmap 
		{
			get 
			{
				using( PreviewDrawer drawer = new PreviewDrawer(new Size(100,100),new Size(10,1),0) ) 
				{
					int x,y;
					for( int i=0; i<28; i++ )
					{
						if(i<=9)
						{
							x = 0;
							y = i;
						}
						else
						{
							x = i-9;
							y = 9;
						}
						while(y>=0&&x<10)
						{
							if(previewPattern[PreviewPatternIdx,x,y]>0)
								drawer.draw(getSprite(previewPattern[PreviewPatternIdx,x,y]),9-x,y-5);
							x++;
							y--;
						}
					}
					return drawer.createBitmap();
				}
			}
        }

        /// <summary>
		/// Compute the cost of build roads.
		/// </summary>
        public static long calcCostOfBuildRoad( Location from, Location to ) {
            if( from == to ) return 0;
			Direction d = from.getDirectionTo(to);
            long cost = 0;
          
			Location here = from;
			while(true) 
			{
				Road r = Road.get(here);
				if( r==null ) 
				{
					cost += calcRoadCost( here );
				} 

				if( here==to )	return cost;
				here = here.toward(to);
			}
            
        }

        /// <summary>
		/// Compute the cost of build one voxel road.
		/// </summary>
		private static long calcRoadCost( Location loc ) {
			int waterLevel = World.world.waterLevel;
			int glevel = World.world.getGroundLevel(loc);
			int height = loc.z - glevel;
            int multiplier = ( height < 0 )? height * -2 : height + 1;
            

			if( glevel<=loc.z && loc.z<=waterLevel && glevel<waterLevel )
				return 0;	// underwater or on water.

			Voxel v = World.world[loc];
			if(v==null)		return ROAD_CONSTRUCTION_UNIT_COST*multiplier;
			
			// TODO: incorrect compuattion
            if(v.entity.isSilentlyReclaimable)
				// we can reclaim this voxel and build a road.
				return ROAD_CONSTRUCTION_UNIT_COST*multiplier + v.entity.entityValue;

			return ROAD_CONSTRUCTION_UNIT_COST*multiplier;
		}

		/// <summary>
		/// Compute the cost of removing roads.
		/// </summary>
		public static long calcCostOfRemoving( Location here, Location there ) {

			if(here==there)	return 0;

			World world = World.world;
			Direction d = here.getDirectionTo(there);
			long cost = 0;

			TrafficVoxel v = null;

			while(true) 
			{
				Road r = Road.get(here);
				if( r!=null ) {
                    v = World.world[here] as TrafficVoxel;                      // 現在対象の位置のボクセルを取得
                    if (v != null) {                                            // ボクセルがトラフィックボクセルの場合
                        if (v.car != null) {                                    // 該当ボクセルに車が存在する場合
                            if (v.car is Train.TrainCar) {                      // 車が列車の場合
                            } else {
                                cost++;
                            }
                        } else {
                            cost++;
                        }
                    } else {
                        cost++;
                    }
                }

                if(here==there)	break;
				here = here.toward(there);
			}
			return cost*ROAD_DESTRUCTION_UNIT_COST*(Math.Abs(here.z-world.waterLevel)+1);
		}

      
      
        private const int ROAD_DESTRUCTION_UNIT_COST  = 100000;
		private const int ROAD_CONSTRUCTION_UNIT_COST = 300000;

		/// <summary>
		/// Road implementation
		/// </summary>
		[Serializable]
			internal class RoadImpl : Road 
		{
			internal protected RoadImpl( AbstractRoadContributionImpl contrib, TrafficVoxel tv, RoadPattern pattern ) :
				base(tv,pattern,contrib.style) 
			{

				this.contribution = contrib;
			}
			
			private readonly AbstractRoadContributionImpl contribution;

			public override void drawBefore( DrawContext display, Point pt ) 
			{
				contribution.getSprite(pattern.dirs).draw( display.surface, pt );
			}

			public override bool attach( Direction d ) 
			{
				byte dirs = pattern.dirs;
				dirs |= (byte)(1<<(d.index/2));
				voxel.road = new RoadImpl( contribution, voxel, RoadPattern.get(dirs) );
				return true;
			}

			public override void detach( Direction d1, Direction d2 ) 
			{
				byte dirs = pattern.dirs;
				dirs &= (byte)~(1<<(d1.index/2));
				dirs &= (byte)~(1<<(d2.index/2));

				if( dirs==0 )
					// destroy this road
					voxel.road = null;
				else 
				{
					voxel.road = new RoadImpl( contribution, voxel, RoadPattern.get(dirs) );
				}

				World.world.onVoxelUpdated(location);
			}

			public override bool canAttach( Direction d ) 
			{
				return true;
			}
		}

    }
}
