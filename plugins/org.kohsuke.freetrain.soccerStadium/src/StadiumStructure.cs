// 2010.04.17 riorio fixed
using System;
using System.Collections;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using freetrain.contributions.structs;
using freetrain.framework;
using freetrain.framework.plugin;
using freetrain.world.accounting;
using freetrain.world.structs;
using freetrain.util;

namespace freetrain.world.soccerstadium
{
	/// <summary>
	/// Structure object for a soccer stadium.
	/// </summary>
	[Serializable]
	public class StadiumStructure : PThreeDimStructure
	{
//		/// <summary>
//		/// Stadium structure contribution.
//		/// </summary>
//		private readonly new StructureContributionImpl type;

		/// <summary>
		/// Strength of the team (0-100). A stronger team wins often.
		/// </summary>
		private int _strength = 30;

		/// <summary>
		/// Popularity of the team (0-100). A popular team attracts more people.
		/// </summary>
		private int _popularity = 30;
		
		/// <summary> Name of the stadium. </summary>
		public string stadiumName;

		/// <summary> Name of the team. </summary>
		public string teamName;

        private int _maches = 0;
        private int _wins = 0;
        private int _draws = 0;

		/// <summary>
		/// Upcoming games with the earlier ones at the head of the array.
		/// Should be treated as a read-only object from outside.
		/// </summary>
		public readonly IList futureGames = new ArrayList();

		/// <summary>
		/// Past games with the earlier ones at the head of the array.
		/// Should be treated as a read-only object from outside.
		/// </summary>
		public readonly IList pastGames = new ArrayList();

        /// SpecialStationListener
   		private readonly SpecialStationListenerImpl specialstationListener;
        public PopulationImpl _population;

        public int capacity;
      
		public override object queryInterface( Type aspect ) {
			// if type.population is null, we don't have any population
			if( aspect==typeof(rail.SpecialStationListener) )
				return specialstationListener;
			else
				return base.queryInterface(aspect);
		}


		/// <summary>
		/// Creates a new commercial structurewith its left-top corner at
		/// the specified location.
		/// </summary>
		/// <param name="_type">
		/// Type of the structure to be built.
		/// </param>
		public StadiumStructure( StructureContributionImpl _type, WorldLocator wloc  ) : base( _type, wloc ) {
			// this.type = _type;

            this.capacity = _type.capacity;
            this._population = new PopulationImpl();
            this.specialstationListener = new SpecialStationListenerImpl( this._population, wloc.location );
              
			// register once a month timer for the strength/popularity decay
			repeatHandler = World.world.clock.registerRepeated( new ClockHandler(onClock), TimeLength.fromDays(30) );

   			AccountManager.theInstance.spend( _type.price, Const.GENRE );

          
            Time now = World.world.clock;
            TimeLength untilSeasonOpen;
            Time weekend = now;

            World.world.clock.registerOneShot(new ClockHandler(onNewYear),
                                              TimeLength.fromDays((13 - now.month) * 30 + 5 ));
          
            if( now.month < 3 ){  // season off
                untilSeasonOpen = TimeLength.fromDays((3 - now.month) * 30);
                weekend = weekend + untilSeasonOpen;
            }

            if( weekend.dayOfWeek < 5 )  // search first weekend
                weekend = weekend + TimeLength.fromDays(5 - weekend.dayOfWeek);
			// schedule initial games
			// minutes to the next day midnight
			TimeLength b = TimeLength.fromMinutes(
				TimeLength.ONEDAY.totalMinutes
				- ( weekend.totalMinutes % TimeLength.ONEDAY.totalMinutes ));
            b += (weekend - now);
			// the first game is set to 14:00 that day. 
			b += TimeLength.fromHours(6);

			for( int i=0; i<Const.SCHEDULE_DAYS; i+=7 ) {
                if( Const.rnd.Next( 2 ) == 1 )
                     scheduleNewGame( b );
                else
  				     scheduleNewGame( b+TimeLength.fromHours(5) );	// schdule a night game

				b += TimeLength.fromDays(7);
			}
		}

		public override string name { get { return stadiumName; } }

        private ClockHandler repeatHandler = null;
      
		public int strength { get { return _strength; } }
		public int popularity { get { return _popularity; } }

		public int maches {
          get { return _maches; }
          set { _maches = value; }
        }
		public int wins {
          get { return _wins; }
          set { _wins = value; }
        }
		public int draws {
          get { return _draws; }
          set { _draws = value; }
        }

		// height-cut color
		protected override Color heightCutColor { get { return hcColor; } }
		private static Color hcColor = Color.FromArgb(51,115,179);

		public override bool isOwned { get { return true; } }
		public override bool isSilentlyReclaimable { get { return false; } }

		public override void remove() {
			base.remove();

            foreach (Game game in this.futureGames)
                game.removeregister();
            
			World.world.clock.unregister( repeatHandler );
			World.world.clock.unregister( new ClockHandler(onNewYear) );

            if( specialstationListener!=null )
                specialstationListener.onRemoved();
		}


		public override bool onClick() {
			using( StadiumPropertyDialog prop = new StadiumPropertyDialog(this) ) {
				prop.ShowDialog(MainWindow.mainWindow);
			}
			return true;
		}

        public override bool onRightClick()
        {
             return false;
        }

        public void onClock()
        {
			// both parameters decline streadily
			_popularity = Math.Max(0,_popularity-2);
			_strength   = Math.Max(0,_strength-2);
		}

        public void onNewYear(){
            _maches = 0;
            _wins = 0;
            _draws = 0;
          
            World.world.clock.registerOneShot( new ClockHandler(onNewYear), TimeLength.fromDays(365) );
        }
      
		public void reinforce() {
			_strength = Math.Min(100,_strength+10);
			// we will charge anyway even if we are the strongest.
			AccountManager.theInstance.spend( 10000000, Const.GENRE );
		}

		public void doPR() {
			_popularity = Math.Min(100,_popularity+10);
			// we will charge anyway even if we are the strongest.
			AccountManager.theInstance.spend( 10000000, Const.GENRE );
		}

		/// <summary>
		/// Schedule a new game in the future.
		/// </summary>
		private void scheduleNewGame( TimeLength timeToGame ) {
            futureGames.Add(new Game(this, timeToGame));
		}

	//
	// called by the Game object.
	//
		internal string playGame( Game onegame, ref int winFlag ) {
			Debug.Assert( futureGames[0]==onegame );
			futureGames.RemoveAt(0);

			string score;

			// decide the score.
			int s1 = (int)Math.Floor( 6.0*Math.Pow(Const.rnd.NextDouble(),1.4) );
			int s2 = (int)Math.Floor( 6.0*Math.Pow(Const.rnd.NextDouble(),1.4) );
			
			// make sure s1 >= s2
			if( s2 > s1 ) {
				int t=s1; s1=s2; s2=t;
			}

			if(s1==s2) {
				// draw game. no bonus
				score = s1+"-"+s2;
                winFlag = 1;
			} else {
				// decide who won. 25% - 75%
                if (Const.rnd.Next(100) < (this.strength - onegame.opponent.strength) / 5 + 50)
                {
					score = s1+"-"+s2;
					// won
					_popularity = Math.Min(100,_popularity+1);
                    winFlag = 2;
				} else {
					score = s2+"-"+s1;
					// lost
					_popularity = Math.Max(0,_popularity-1);
                    winFlag = 0;
				}
			}

			// move it to the "past games" list
			pastGames.Add(onegame);

			// schedule another game 4 week later
            if( World.world.clock.month==12 ){  // 1,2 are OFF season SKIP
              scheduleNewGame( TimeLength.fromDays(Const.SCHEDULE_DAYS * 3 + 7) );
            }
            else{ scheduleNewGame( TimeLength.fromDays(Const.SCHEDULE_DAYS) ); }
          
			return score;
		}

      
		/// <summary>
		/// Erase a game from the records.
		/// Should be called only from a Game object.
		/// </summary>
		internal void timeoutGame( Game game ) {
			Debug.Assert(pastGames[0]==game);
			pastGames.RemoveAt(0);
		}
	}
}
