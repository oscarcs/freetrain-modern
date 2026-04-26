// 2008.09.06 YZ Add forwarding and reverse & forwarding
// 2008.09.13 YZ Add forwarding and reverse & forwarding status
// 2008.11.03 YZ Modify _forwarding attribute to OptionalField
// 2010.05.02 riorio Add seatedcapacity
// 2010.05.07 riorio Add reimberse money when train sell
using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using freetrain.contributions.train;
using freetrain.framework;
using freetrain.framework.sound;
using freetrain.framework.graphics;
using freetrain.framework.plugin;
using freetrain.world.accounting;
using System.Runtime.Serialization;
using freetrain.DirectXWrapper;

namespace freetrain.world.rail
{
	public delegate void TrainHandler( Train train );

	/// <summary>
	/// Train
	/// </summary>
	[Serializable]
	public class Train : TrainItem, IDeserializationCallback
	{	
		/// <summary>
		/// Function object that computes the next state for the head car.
		/// </summary>
		[NonSerialized]
		private /*readonly*/ CalcNextTrainCarState calcNextTrainCarState;


		public Train( TrainGroup group, int length, TrainContribution _type, Time now)
			: this(group,string.Format("TR{0}",iota++),length,_type,SimpleTrainControllerImpl.theInstance) {

              this.boughtThisTrain = now;
		}
    
		/// <summary> Sequence number generator. </summary>
		private static int iota=1;


		/// <summary>
		/// Sound-effect of a ringing bell. Used when a train leaves a station.
		/// </summary>
		private static readonly SoundEffect thudSound = new RepeatableSoundEffectImpl(
			ResourceUtil.loadSystemSound("train.wav"),1,300);


		/// <summary>
		/// Creates a new train and assigns it to a group.
		/// </summary>
		public Train( TrainGroup group, string _name, int length, TrainContribution _type, TrainController _controller )
			: base(group,_name) {
			this.type = _type;
			this.controller = _controller;
            
			TrainCarContribution[] carTypes = type.create(length);

			cars = new TrainCar[length];
			for( int i=0; i<length; i++ )
				cars[i] = new TrainCar(this,carTypes[i],i);

			calcNextTrainCarState = new CalcNextTrainCarState(this);

        }


		public void OnDeserialization( object sender ) {
			calcNextTrainCarState = new CalcNextTrainCarState(this);
		}



		/// <summary> Type of this train. </summary>
		public readonly TrainContribution type;

        /// <summary> When I bought this train. </summary>
        private Time boughtThisTrain;

        public Time boughtTime {
          get {
            return boughtThisTrain;
          }
          set {
            boughtThisTrain = value;
          }
        } 
      

        public string displayName { get { return name; } }

		/// <summary>
		/// この編成を構成する車両
		/// </summary>
		private readonly TrainCar[] cars;

		/// <summary> Number of cars in this train. </summary>
		public int length { get { return cars.Length; } }

		/// <summary> The first car of this train. </summary>
		public TrainCar head { get { return cars[0]; } }

		/// <summary> The last car of this train. </summary>
		public TrainCar tail { get { return cars[cars.Length-1]; } }

		/// <summary> Return true if this train is placed on the map </summary>
		public bool isPlaced { get { return state!=State.Unplaced; } }

		/// <summary> Place a train to the specified location.</summary>
		/// <returns> false if it can't be done. </returns>
		public bool place( Location loc ) {
			Debug.Assert(!isPlaced);

			Direction[] ds = new Direction[length];
			Location[] locs = new Location[length];

			int idx = length;
			
			Direction d = null;
			do {
				idx--;
				
				RailRoad rr = RailRoad.get(loc);
				if(rr==null || rr.voxel.isOccupied) {
					// can't be placed here
					return false;
				}
				if(d==null)		d = rr.dir1;	// set the initial direction
				
				ds[idx]=d; locs[idx]=loc;

				// determine the next voxel
				cars[0].place( loc, d );
				d = rr.guide();
				loc += d;
				cars[0].remove();
			} while(idx!=0);

			// make sure we are not forming cycles
			for( int i=0; i<length-1; i++ )
				for( int j=i+1; j<length; j++ )
					if( locs[i]==locs[j] )
						return false;	// can't be placed
			
			// can be placed. place all
			for( int i=0; i<length; i++ )
				cars[i].place( locs[i], ds[i] );

			stopCallCount = 0;
			registerTimer();
			state = State.Moving;

			return true;
		}

		/// <summary>
		/// 配置済みの列車を撤去する
		/// </summary>
		public void remove() {
			Debug.Assert(isPlaced);
			
			foreach( TrainCar car in cars )
				car.remove();

			// make sure that we don't have any pending event
			World.world.clock.unregister( new ClockHandler(clockHandler) );
			state = State.Unplaced;
		}

		/// <summary> Sell this train. </summary>
		public void sell() {
			if(isPlaced)	remove();

			ownerGroup.items.remove(this);

          // TODO: reimberse money
            TimeLength usedTime;
            if (boughtThisTrain == null){}
            else{
              usedTime = World.world.clock - boughtThisTrain;

              long useYears = usedTime / TimeLength.fromDays( 365 );
              Debug.Print("useYears: {0}",useYears);
              long reimbersePrice = this.type.price( this.length ) / (useYears+2);
              AccountManager.theInstance.earn( reimbersePrice, AccountGenre.RAIL_SERVICE );
            }
			// disconnect all listeners.
			nonPersistentStateListeners = null;
			persistentStateListeners = null;
		}

		
		/// <summary> Possible states of a train. </summary>
		public enum State : byte {
			Unplaced,			// not placed
			Moving,				// moving normally
			StoppingAtStation,	// stopping at a station, waiting for the time to start
			StoppingAtSignal,	// stopping at a singal.
			EmergencyStopping,	// stopping because of a car ahead of this train
		}

		/// <summary> State of this train. Usually updated by the clock handler. </summary>
		private State __state = State.Unplaced;

		public State state {
			get {
				return __state;
			}
			set {
				if( __state==value )	return;

				__state = value;
				notifyListeners();
			}
		}

		private void notifyListeners() {
			if(persistentStateListeners!=null)
				persistentStateListeners(this);
			if(nonPersistentStateListeners!=null)
				nonPersistentStateListeners(this);
		}

		/// <summary>
		/// Returns the state in its display text.
		/// </summary>
		public string stateDisplayText {
			get {
/*
#region riorio_20100401_DELETED
#region YZ_20080913_ADDED
                if (_forwarding == true) {
                    return "回送中";
                }
#endregion
				switch(state) {
				case State.Unplaced:			return "未配置";
				case State.Moving:				return "進行中";
				case State.StoppingAtStation:	return "発車時間待";
				case State.StoppingAtSignal:	return "停止中";
				case State.EmergencyStopping:	return "緊急停止";
				default:	Debug.Fail("undefined state"); return null;
				}
#endregion
*/
#region riorio_20100401_ADDED
                string str = "";
                if( _forwarding == true) {
                    str = "回送";
                }
				switch(state) {
				case State.Unplaced:			str += "未配置";  break;
				case State.Moving:				str += "進行中";  break;
				case State.StoppingAtStation:	str += "発車時間待";  break;
				case State.StoppingAtSignal:	str += "停止中";  break;
				case State.EmergencyStopping:	str += "緊急停止";  break;
				default:	Debug.Fail("undefined state"); str += "未定義";  break;
				}
                return str;
            }
#endregion
		}

		/// <summary>
		/// Delegates that are invoked when the state of the train changes.
		/// </summary>
		public TrainHandler persistentStateListeners;

		[NonSerialized]
		public TrainHandler nonPersistentStateListeners;

        private void registerTimer() {
            registerTimer(TimeLength.fromMinutes(type.minutesPerVoxel));
        }

        /// <summary>
        /// 
        /// </summary>
		private void registerTimer(Location loc) {
            SpeedLimit sl = SpeedLimit.getInstance();
			//速度制限の分岐
            if(sl.LowSpeedLimit.Contains(loc))
            {
                registerTimer(TimeLength.fromMinutes(4));
            }
            else if (sl.MediumSpeedLimit.Contains(loc))
            {
                if (type.minutesPerVoxel == 4)
                {
                    registerTimer();
                }
                else
                {
                    registerTimer(TimeLength.fromMinutes(3));
                }
            }
            else if (sl.FastSpeedLimit.Contains(loc))
            {
                if (type.minutesPerVoxel == 4 || type.minutesPerVoxel == 3)
                {
                    registerTimer();
                }
                else
                {
                    registerTimer(TimeLength.fromMinutes(2));
                }
            }
            else
            {
                //速度制限なし
                registerTimer();
            }
		}
			
		private void registerTimer( TimeLength time ) {
			World.world.clock.registerOneShot( new ClockHandler(clockHandler), time );
		}

		/// <summary> Counter that remembers the # of consecutive times this train is told to stop. </summary>
		private int stopCallCount = 0;

		/// <summary>
		/// Clock event handler.
		/// </summary>
		public void clockHandler() {
			Debug.Assert(isPlaced);	// we should have unregistered the handler when the train was removed.

			CarState.Inside ins = head.state.asInside();
			if( ins!=null ) { // this car might need to stop
				TimeLength time = ins.voxel.railRoad.getStopTimeSpan(this,stopCallCount);
						Debug.WriteLine(string.Format("time.totalMinutes {0}",time.totalMinutes) );
				if(time.totalMinutes>0) {
					stopCallCount++;
					// a car needs to stop here
					registerTimer(time);	// resume after the specified time

					// TODO: see where this train is being stopped. do something if necessary
					state = State.StoppingAtStation;
					return;
				}

#region YZ_20080913_ADDED
                _forwarding = false;                                            // forwarding flag off
#endregion
#region YZ_20080906_MODIFIED
//				if(time.totalMinutes<0)
//					reverse();	// turn around
                if (time.totalMinutes == -999) {                                // if time = -999 is forwarding
                    _forwarding = true;                                         // forwarding flag on
						Debug.WriteLine(string.Format("_forwarding = true") );
                } else {
                    if (time.totalMinutes == -1000) {                           // if time = -1000 is reverse and forwarding
                        reverse();                                              // turn around
                        _forwarding = true;                                     // forwarding flag on
						Debug.WriteLine(string.Format("_forwarding = true") );
                    } else {
                        if (time.totalMinutes < 0) {                            // if minus value is reverse
                            reverse();                                          // turn around
                        }
                    }
                }
#endregion
            }

			// this car can now move
			stopCallCount = 0;
			State s = State.Moving;
			
			// determine the next head car state
			CarState next = calcNextTrainCarState[head.state];
			if(next!=null) {	// if it can move forward
				if(!isBlocked[next] )
					move(next);
				else
					// otherwise we can't move. emergency stop.
					s = State.EmergencyStopping;
			} else {
				// we can't go forward. turn around
				reverse();
				next = calcNextTrainCarState[head.state];

				if( next!=null && !isBlocked[next] )
					move(next);
				else
					s = State.EmergencyStopping;
			}
			state = s;	// update the state
			
            //変更
            if( next.isOutside )
              registerTimer();
            else if( ins==null )
              registerTimer();
            else
              registerTimer(ins.voxel.location);
		}

		private int moveCount = 0;	// used to compute the cost of a train

#region YZ_20080906_ADDED
        // Internal train forwarding flag
        private bool _forwarding;

        /// <summary>Train forwarding flag</summary>
        /// <value>true  is train forwarding</value>
        /// <value>false is train not forwarding</value>
        public bool forwarding {
            get { return _forwarding; }
            set { _forwarding = value; }
        }
#endregion

		/// <summary>
		/// 1voxel動かす
		/// </summary>
		public void move( CarState next ) {
			
			if( next.isOutside && next.asOutside().timeLeft==OUTSIDE_COUNTER_INITIAL_VALUE/2 ) {
				// unload the passengers and reload them
				unloadPassengers();

#region YZ_20080906_MODIFIED
//              loadPassengers(null,Math.Min(100,passengerCapacity));	// TODO: compute the value seriously
                if (_forwarding == true) {                                      // if train is forwarding
#region YZ_20080913_MODIFIED
//                  _forwarding = false;                                        // not load passengers and train forwarding flag off
#endregion
                } else {
                    loadPassengers(null,Math.Min(100, passengerCapacity));      // load passengers
                }
#endregion

            }

			for( int i=0; i<cars.Length; i++ )
				next = cars[i].moveTo(next);

			// moving a train costs money
			if( ((moveCount++)&3)==0 ) {
				// TODO: exact amount is still under debate
				AccountManager.theInstance.spend( (length*20+(passenger/20))*2000, AccountGenre.RAIL_SERVICE );
			}

			playSound(thudSound);
		}

		/// <summary>
		/// Plays a sound effect for this train if necessary.
		/// </summary>
		public void playSound(SoundEffect se) {
			CarState.Inside ins = head.state.asInside();
			if(ins!=null)
				se.play(ins.location);	// play the sound
		}

		/// <summary>
		/// Reverses the direction of the train.
		/// </summary>
		public void reverse() {
			// reverse the direction of each car.
			foreach( TrainCar car in cars )
				car.reverse();

			// swap the sequence
			for( int i=0; i<cars.Length/2; i++ ) {
				TrainCar t = cars[i];
				cars[i] = cars[ cars.Length-(i+1) ];
				cars[ cars.Length-(i+1) ] = t;
			}

			isReversed = !isReversed;
		}

		/// <summary>
		/// Returns true if the train is reversed.
		/// </summary>
		private bool isReversed;

		


		/// <summary>
		/// Number of passengers in this train.
		/// </summary>
		private int _passenger;
		public int passenger {
			get {
				return _passenger;
			}
		}

		/// <summary>
		/// State of the car when the current passengers were loaded.
		/// </summary>
		private CarState.Placed passengerSourceState;

		/// <summary>
		/// Maximum number of passengers this train can hold.
		/// </summary>
		public int passengerCapacity {
			get {
				int c = 0;
				foreach( TrainCar car in this.cars )
					c += car.type.capacity;
				return c;
			}
		}

		/// <summary>
		/// Maximum number of passengers seated this train can hold.
		/// </summary>
		public int passengerSeatedCapacity {
			get {
				int sc = 0;
                foreach( TrainCar car in this.cars ){
                    if( car.type.seatedcapacity>=0 && car.type.seatedcapacity <= car.type.capacity )
                        sc += car.type.seatedcapacity;
                    else
                        sc += car.type.capacity;
                }
				return sc;
			}
		}

		/// <summary>
		/// Maximum number of passengers packed this train can hold.
		/// </summary>
		public int passengerPackingCapacity {
			get {
				int ppc, psc;

                if (passengerSeatedCapacity > passengerCapacity) psc = passengerCapacity;
                else if (passengerSeatedCapacity < 0) psc = 0;
                else psc = passengerSeatedCapacity;
                // passengersSTANDING = passengerCapacity - passengerSeatedCapacity
                // passengerPackingCapacity = passengerSeatedCapacity + passengersSTANDING*2
                ppc = psc + (passengerCapacity - psc) * 2;
				return ppc;
			}
		}

		/// <summary>
		/// Unloads the passengers from this train.
		/// This method should be called only by the Station.unloadPassengers() method.
		/// </summary>
		/// <returns>number of unloaded passengers</returns>
		public int unloadPassengers() {
			int r = _passenger;
			_passenger = 0;
			if( passengerSourceState!=null ) {
				// compute the distance between the source state and the current state
				int dist = passengerSourceState.location.
					getDistanceTo( this.head.state.asPlaced().location );

				// record the sales
				AccountManager.theInstance.earn(
					r * type.fare * dist * 2,
					AccountGenre.RAIL_SERVICE );
				
				passengerSourceState = null;
			}

			notifyListeners();

			return r;
		}
		/// <summary>
		/// Loads the passengers from this train.
		/// This method should be called only by the Station.loadPassengers() method.
		/// </summary>
		public void loadPassengers(Station from, int n ) {
			Debug.Assert(_passenger==0);
			Debug.Assert(n <= passengerPackingCapacity);
			_passenger = n;
			// memorize the location where passengers are loaded
			passengerSourceState = this.head.state.asPlaced();
			notifyListeners();
		}


		
		private const int OUTSIDE_COUNTER_INITIAL_VALUE = 100;

		/// <summary>
		/// Determines the next car state by visiting the current state.
		/// This visitor is only applied against the head car.
		/// 
		/// The method returns null if it cannot proceed because there's no
		/// rail road in front of the head car.
		/// </summary>
		private class CalcNextTrainCarState : CarState.Visitor
		{
			private readonly Train owner;

			internal CalcNextTrainCarState( Train _owner ) {
				this.owner = _owner;
			}

			public CarState this[CarState s] { get {
				return (CarState)s.accept(this);
			}}

			public object onInside( CarState.Inside state ) {
				TrainCar head = owner.head;
				RailRoad rr = RailRoad.get(state.location);
				
				Direction go = rr.guide();	// angle to go
				Location newLoc = state.location + go;
				newLoc.z += rr.zdiff(state.direction);

				if( World.world.isBorderOfWorld(newLoc) ) {
					// go outside the world
					return new CarState.Outside( newLoc, go, OUTSIDE_COUNTER_INITIAL_VALUE );
				} else {
					if(isConnected(newLoc,go))
						// the rail needs to be connected
						return new CarState.Inside( newLoc, go );
					else
						return null;
				}
			}
			public object onUnplaced( CarState.Unplaced state ) {
				return state;	// remain unplaced
			}
			public object onOutside( CarState.Outside state ) {
				if( state.timeLeft!=0 )
					return new CarState.Outside( state.location, state.direction, state.timeLeft-1 );

				// time to get back to the world.
				CarState s = calcReturnPoint(state);
				if(s!=null)		return s;

				// there's no coming back. try again later
				return new CarState.Outside( state.location, state.direction, 10 );
			}
		}

		/// <summary>
		/// Determines where the train should re-appear into the world.
		/// </summary>
		/// <param name="state"></param>
		/// <returns></returns>
		private static CarState.Inside calcReturnPoint( CarState.Outside state ) {
			// where do we go back?
			// for now, go back to where it comes from.
			int idx = state.direction.isSharp?0:1;
			for( int i=0; i<5; i++ ) {
				// compute the location
				Location newLoc = state.location + returnPointMatrixes[idx,i]*((Distance)state.direction);
				// see if there's rail road
				if( isConnected( newLoc, state.direction.opposite ) )
					// OK
					return new CarState.Inside( newLoc, state.direction.opposite );
			}
			return null;	// non found
		}

		/// <summary>
		/// Returns true if a train car can proceed to the
		/// specified location by going the specified direction
		/// </summary>
		private static bool isConnected( Location loc, Direction d ) {
			RailRoad rr = RailRoad.get(loc);
			return rr!=null && rr.hasRail(d.opposite);
		}

		


		/// <summary>
		/// 45-degree rotational transformations from askew directions.
		/// (The same rotational transformations need to be doubled when
		/// applied against axis parallel directions)
		/// </summary>
		private readonly static Matrix L45 = new Matrix( 0.5f, 0.5f, -0.5f, 0.5f );
		private readonly static Matrix R45 = new Matrix( 0.5f, -0.5f, 0.5f, 0.5f );

		/// <summary>
		/// Determines the return location from outside the world.
		/// </summary>
		private readonly static Matrix[,] returnPointMatrixes = new Matrix[2,5] {
			// when the direction is parallel to X/Y axis
			{
				Matrix.L90,		// in the order of precedence
				Matrix.R90,
				Matrix.L90 - 2*Matrix.E,
				Matrix.R90 - 2*Matrix.E,
				Matrix.REVERSE
			},
			// when the direction is not parallel to X/Y axis
			{
				L45 - Matrix.E,
				R45 - Matrix.E,
				L45 - 2*Matrix.E,
				R45 - 2*Matrix.E,
				Matrix.REVERSE
			}
		};



		/// <summary>
		/// Reverse the direction of the visiting car state and return it.
		/// </summary>
		private static readonly ReverseCarState reverseCarState = new ReverseCarState();
		private class ReverseCarState : CarState.Visitor
		{
			public object onInside( CarState.Inside state ) {
				Direction d = state.voxel.railRoad.guide().opposite;
				return new CarState.Inside( state.location, d );
			}
			public object onUnplaced( CarState.Unplaced state ) {
				return state;	// remain unchanged
			}
			public object onOutside( CarState.Outside state ) {
				CarState.Inside s = calcReturnPoint(state);
				if(s==null)
					// all the usable RRs are completely removed.
					// that means this train is fully outside the world.
					// so it's OK to remain static.
					return state;

				return new CarState.Outside(
					s.location-s.direction, s.direction.opposite,
					OUTSIDE_COUNTER_INITIAL_VALUE - state.timeLeft );
			}
			public CarState this[CarState s] { get {
				return (CarState)s.accept(this);
			}}
		}
		
		private static readonly IsBlocked isBlocked = new IsBlocked();
		private class IsBlocked : CarState.Visitor
		{
			public object onInside( CarState.Inside state ) {
				return state.voxel.isOccupied;
			}
			public object onUnplaced( CarState.Unplaced state ) {
				return false;
			}
			public object onOutside( CarState.Outside state ) {
				return false;
			}
			public bool this[CarState s] { get {
				return (bool)s.accept(this);
			}}
		}






		/// <summary>
		/// 一両の電車
		/// </summary>
		[Serializable]
		public class TrainCar : Car
		{
			public TrainCar( Train parent, TrainCarContribution _type, int idx ) {
				this.parent = parent;
				this.type = _type;
				this.index = idx;
			}

			/// <summary> この電車を含む編成 </summary>
			public readonly Train parent;

			/// <summary> Type of this car. </summary>
			public readonly TrainCarContribution type;


			/// <summary> Previous train car, or null. </summary>
			public TrainCar previous {
				get {
					if( !parent.isReversed ) {
						if( index==0 )	return null;
						else			return parent.cars[index-1];
					} else {
						if( index==parent.cars.Length-1 )	return null;
						else
							return parent.cars[parent.cars.Length-index-2];
					}
				}
			}

			/// <summary>
			/// Index in the array. This car must be either at this
			/// position or "parent.cars.Length-index"
			/// </summary>
			private readonly int index;
			

			internal CarState moveTo( CarState newState ) {
				return base.setState(newState);
			}

			/// <summary>
			/// Reverses the direction of the car.
			/// </summary>
			public void reverse() {
				setState( reverseCarState[this.state] );
			}

			public override void draw( DrawContext dc, Point pt ) {
				Surface display = dc.surface;

				pt.Y -= 9;	// offset

				CarState.Inside s = state.asInside();
				Debug.Assert(s!=null);

				RailRoad rr = s.voxel.railRoad;
				if(rr is SlopeRailRoad) { // slope rail
					SlopeRailRoad srr = (SlopeRailRoad)rr;

					switch(srr.level) {// apply slope height
					case 0:	break;
					case 1:	pt.Y -= 4; break;
					case 2:	pt.Y += 8;	break;
					case 3: pt.Y += 4; break;
					}

					if( !parent.isReversed )
						type.drawSlope( display, pt, s.direction, s.direction==srr.climbDir );
					else
						type.drawSlope( display, pt, s.direction.opposite, s.direction!=srr.climbDir );
				} else { // level rail road
					int d1 = s.direction.index;
					int d2 = s.voxel.railRoad.guide().index;

					int angle;
					if(d1==d2) {
						angle = d1*2;
					} else {
						int diff = (d2-d1)&7;
						if(diff==7)	diff=-1;
						
						int dd = ( d2*2 + diff*3 ) &15;	// operation is on modulo 16.

						if( 2<dd && dd<10 )	pt.X +=3;
						else				pt.X -=3;

						if( 6<dd && dd<=14)	pt.Y +=2;
						else				pt.Y -=2;

						angle = (d1*2+diff)&15;
					}

					if( parent.isReversed )
						angle ^= 8;

					type.draw( display, pt, angle );
				}
			}
		}
	}
}
