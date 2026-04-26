// 2008.09.06 YZ Add forwarding and reverse & forwarding option
// 2010.04.01 riorio Add TURNING reverse option , Original foobarbazer
using System;
using System.Collections;
using freetrain.world.rail;
using System.Diagnostics;

namespace freetrain.world.rail.tattc
{
	/// <summary>
	/// StationHandler that follows the detailed rules.
	/// </summary>
	[Serializable]
	internal class AdvancedStationHandler : StationHandler {
		public AdvancedStationHandler() {}


		internal readonly RuleCollection rules = new RuleCollection();

		[Serializable]
		internal class RuleCollection : CollectionBase {
			public void add( AdvStationRule rule ) {
				this.List.Add(rule);
			}
			public void remove( AdvStationRule rule ) {
				this.List.Remove(rule);
			}
			public void insert( int idx, AdvStationRule rule ) {
				this.List.Insert( idx, rule );
			}
			public void set( int idx, AdvStationRule rule ) {
				this.List[idx] = rule;
			}
		}

		internal TimeLength checkTurn(Train train, int forwarding) {

          if( forwarding==-1000 || forwarding==-1 ){ // -1000=反転回送, -1=反転
          // 先頭車
				Train.TrainCar headCar=train.head;
				CarState.Inside ss=headCar.state.asInside();
				if(ss==null) {
	              return TimeLength.fromMinutes( forwarding );
	            }
	
				// 先頭車の位置
				Location headLoc=ss.location;
	
                // 列車の車両(Car)の配列
                Train.TrainCar[] cars = new Train.TrainCar[train.length];
                int idx = train.length;

			    // 列車の進行方向. 上から下方向ならtrue
			    // CS0165対策で初期化
			    bool bToLower = false;

				// 一旦reverse()させて
				// 最後部までの各車両が存在する線路を調べる
				train.reverse();
				Train.TrainCar tailCar=headCar;
	
				while(tailCar != null) {
					cars[--idx] = tailCar;

					ss = tailCar.state.asInside();
					if(ss == null){
						return TimeLength.fromMinutes( forwarding );
					}
					RailRoad rr = RailRoad.get(ss.location);
					if(rr == null){
						return TimeLength.fromMinutes( forwarding );
					}

					// ポイントorカーブ
					// -- 再配置時に,再配置前と異なる方向に配置される恐れがある
					int d1 = -1, d2 = -1;
					for(int i = 0; i < 8; i++) {
						if(rr.hasRail(Direction.get(i))) {
							if(d1 == -1)
								d1 = i;
							else if(d2 == -1) {
								if(i - d1 != 4)	// ポイントorカーブ
								{
									return TimeLength.fromMinutes( forwarding );
								}
								d2=i;
							}
							else {
								return TimeLength.fromMinutes( forwarding );
							}
						}
					}	// for

                    // 1両だけの場合
				    if(train.length == 1) {
					    // 方向を調べる
					    //  - 進行方向が水平か垂直なら何もしない
					    Direction d = rr.guide();
					    if(d.isSharp == false) {
						    return TimeLength.fromMinutes(-1000);
				  	    }
					    // Train::reverse()をコールする前の
					    // 進行方向を調べる(下向きか上向きか)
					    bToLower = (d.index <= 2);
  					    break;
				    }

					// 次の車両へ
					tailCar = tailCar.previous;
				}	// while
	
				// 直線線路上に列車がある事を確認したが,
				// 水平や垂直方向なら何もしない
				Location tailLoc=ss.location;
                if( train.length > 1 ){
				    if(tailLoc.x-headLoc.x==tailLoc.y-headLoc.y)
	                    {
	                        return TimeLength.fromMinutes( forwarding );
	                    }
				     bToLower = ((tailLoc.y < headLoc.y) || (tailLoc.x > headLoc.x));
			    }
	
			    // Train::place()は以下の仕様がある.
			    //  1. 指定した位置に最後尾車両を配置
			    //  2. 直線線路上では, 上方に向かって編成が伸びる
			    // 再設置の際に,
			    // 最後尾と先頭車のどちらを指して
			    // 設置させるか判断
			    Location loc = bToLower ? headLoc : tailLoc;

                // 列車撤去, 再設置
				// 一旦列車を撤去
				//  Train::remove()をコールすると,
				//  このソースのgetStopTimeSpan()をコールバックしているハンドラが登録解除され,
				//  直後にコールするTrain::place()によって再登録となる.
				//  それが原因なのか一つの列車が逆組成折返を実施すると, 他の列車が硬直状態に陥る.
				//  そのため, Train::remove()をコールせずに車両撤去のコードのみを引用実装する.  
				foreach(Train.TrainCar car in cars) {
					car.remove();
				}	/* foreach */

				// 元々の進行方向が上から下の場合
				if(bToLower) {
					// 逆転
					//  - 関数冒頭で Train::reverse() をコールしたが,
					//    元の向きに戻す格好となる
					train.reverse();

					// cars配列にある並びは
					// 関数冒頭で Train::reverse() をコールしたあとに作成したものであり, 
					// 直上で再度 Train::reverse() をコールしたため, ここで
					// 正しい順序に並べなおす.
					for(int i = 0; i < cars.Length / 2; i++) {
						Train.TrainCar t = cars[i];
						cars[i] = cars[cars.Length - (i + 1)];
						cars[cars.Length - (i + 1)] = t;
					}	/* for */
				}

				// 再設置
				//  - 撤去と同様に, Train::place()から車両設置のコードのみを引用実装.
				//  - 進行方向がどちらであれ,
				//    車両設置は下(最後尾)から上(先頭)へ向かって行う.
				{
					int idx2 = train.length;
					Direction d = null;
					do {
						idx2--;
						RailRoad rr = RailRoad.get(loc);
						if(d == null) {
							d = rr.dir1;
						}
						cars[idx2].place(loc, d);
						d = rr.guide();
						loc += d;
					} while(idx2 != 0);
				}
            
				// 進行方向を戻さない（後でTimeLength.fromMinutes( forwarding )を返してreverseさせるため）
				if(bToLower) {
					train.reverse();
				}
				return TimeLength.fromMinutes( forwarding );
            }
            return TimeLength.fromMinutes( -1 );
        }

		private readonly TimeLength MIN_STOP_TIME = TimeLength.fromMinutes(10);

		internal override TimeLength getStopTimeSpan( Train train, int callCount ) {
			Clock clock = World.world.clock;

			if( callCount==0 ) {
				// decide whether to stop or pass
				foreach( AdvStationRule rule in rules ) {
					if( rule.action==StationAction.pass && rule.matches(clock) )
						return TimeLength.ZERO;	// pass
					if( rule.action==StationAction.stop && rule.matches(clock) )
						return MIN_STOP_TIME;	// force the train to stop at least this much
				}
				// by default, we stop.
				return MIN_STOP_TIME;

			} else {
				// TODO: do the efficient computation by using the getNextMatch method.

				// decide whether to go or sit still
				foreach( AdvStationRule rule in rules ) {
					if( rule.action==StationAction.go && rule.matches(clock) )
						return TimeLength.ZERO;	// go
					if(( rule.action==StationAction.reverse || rule.action==StationAction.reverse2)
                       && rule.matches(clock) )
                         {
						    if(rule.action==StationAction.reverse2) {
							    return checkTurn(train, -1);
						    }
  						  return TimeLength.fromMinutes(-1);	// turn around and go
                        }
					if( rule.action==StationAction.stop && rule.matches(clock) )
						break;		// can't go

#region YZ_20080906_ADDED
					if (rule.action == StationAction.forwarding && rule.matches(clock)) {
						return TimeLength.fromMinutes(-999);  // forwarding train
                    }
#region riorio_20100401_ADDED
					if ((rule.action == StationAction.rforwarding || rule.action==StationAction.rforwarding2)
                        && rule.matches(clock)) {
                        if( rule.action==StationAction.rforwarding2) {
                                return checkTurn(train, -1000);
						    }
						return TimeLength.fromMinutes(-1000); // reverse and forwarding train
                    }
#endregion
#endregion
				}

				// the unit of rules is 10 minutes. So wait until the next ten minutes break
				int next = (60-clock.minutes)%10;
				if(next==0)	next=10;
				return TimeLength.fromMinutes(next);
			}
		}
	}

	[Serializable]
#region YZ_20080906_MODIFIED
//	internal enum StationAction {
//		pass,	// pass the station
//		stop,	// sit still
//		go,		// go
//		reverse	// reverse
//	}
	internal enum StationAction {
		pass,       // pass the station
		stop,       // sit still
		go,         // go
		reverse,    // reverse
        forwarding, // forwarding
        rforwarding,// reverse forwarding
		reverse2,    // reverse and TURN
        rforwarding2 // reverse and TURN forwarding
	}
#endregion

	[Serializable]
	internal class AdvStationRule : TimeMask {
		internal StationAction action = StationAction.go;
	}
}
