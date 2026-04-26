using System;
using System.Windows.Forms;
using System.Diagnostics;
using freetrain.world.accounting;

namespace freetrain.world.soccerstadium
{
	/// <summary>
	/// A game of soccer and its record.
	/// </summary>
	[Serializable]
	internal class Game
	{
		/// <summary> opponent. </summary>
		public readonly OpponentTeam opponent;
		
		/// <summary> Date of the game </summary>
		public readonly Time date;
		
		/// <summary> Stadium where the game will be held. </summary>
		public readonly StadiumStructure stadium;

		/// <summary> Score of the game. </summary>
		private string _score;
		public string score { get { return _score; } }

		private int _audience;
		public int audience { get { return _audience; } }

		private int _returingAudience = 10;
		public int returningAudience { get { return _returingAudience; } }

      
		/// <summary>
		/// Schedule a new game.
		/// </summary>
		/// <param name="opponent">the opponent team</param>
		/// <param name="time">the date/time of the game</param>
		public Game( StadiumStructure _stadium, OpponentTeam _opponent, TimeLength _time ) {
			this.stadium = _stadium;
			this.opponent = _opponent;
			this.date = World.world.clock + _time;

			World.world.clock.registerOneShot( new ClockHandler(onGameDate), _time );
		}

		public Game( StadiumStructure _stadium, TimeLength _time )
			: this( _stadium, OpponentTeam.drawRandom(), _time ) {}

		public void onGameDate() {
          
			// the date of the game.
			// play the game!
            int capacity = Math.Max(10000, stadium.capacity);
            if( stadium.maches==0 )	// TODO: incorporate popularity
                _audience = Const.rnd.Next((capacity-9000)*3 / 8)+ (capacity-9000)*5 / 8 + stadium.popularity*50+this.opponent.popularity*40;
            else if( stadium.maches == stadium.draws ){
                  _audience = Const.rnd.Next((capacity-7000) / 2)+(capacity-7000)/2 +stadium.popularity*50+this.opponent.popularity*20;
              }
            else
              _audience = Const.rnd.Next((capacity-7000))*stadium.wins/(stadium.maches-stadium.draws)+(capacity-7000)/3
                +stadium.popularity*50+this.opponent.popularity*20;
            _audience = Math.Min( capacity, _audience );
          
            stadium._population.enters = _audience / 3;

            stadium.maches++;
            int winflag = 0;
            _score = stadium.playGame(this, ref winflag);

            if( winflag == 2 ){
                stadium.wins++;
            }
            if( winflag == 1 ){
                stadium.draws++;
            }
          
			AccountManager.theInstance.spend( capacity *1000, Const.GENRE ); // constant cost at 1 game
			AccountManager.theInstance.earn( _audience *2000, Const.GENRE ); // audience charge

            Debug.Print("stadium _audience  "+ _audience);
          
          // erase the record after four weeks.
			World.world.clock.registerOneShot( new ClockHandler(onTimeout),
				TimeLength.fromDays(Const.DAYS_RECORD_EFFECTIVE) );

            World.world.clock.registerOneShot( new ClockHandler(onStartAudienceReturn),
				TimeLength.fromHours( 1 ));

            World.world.clock.registerOneShot( new ClockHandler(onEndAudienceReturn),
				TimeLength.fromHours( 3 ));

            // MessageBox.Show("サッカースタジアム試合開催","試合開催");
        }

		public void onTimeout() {
            stadium.timeoutGame(this);
		}

		public void onStartAudienceReturn() {
			_returingAudience = _audience / 3;
            Debug.Print("stadium _returingAudience " + _returingAudience );
            stadium._population.populs = _returingAudience;
            stadium._population.enters = 10;
		}

		public void onEndAudienceReturn() {
			_returingAudience = 10;
            Debug.Print("stadium _returingAudience " + _returingAudience );
            stadium._population.populs = _returingAudience;
		}

      
		/// <summary>
		/// Creates a list item for the property dialog.
		/// </summary>
		public ListViewItem createListItem() {
			// TODO: should be moved to StadiumPropertyDialog?
			
			ListViewItem lvi = new ListViewItem( date.month+"/"+date.day );
			lvi.SubItems.Add(opponent.name);

			if( score!=null ) {
				// past games
				lvi.SubItems.Add(score);
				lvi.SubItems.Add(audience.ToString());
			}

			return lvi;
		}

          public void removeregister(){
			World.world.clock.unregister( new ClockHandler(onGameDate) );
			World.world.clock.unregister( new ClockHandler(onTimeout) );
          } 
	}
}
