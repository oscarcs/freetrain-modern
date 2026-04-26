// 2010.05.11 riorio add Yesterday/Last month/Last year
using System;
using System.Runtime.Serialization;

namespace freetrain.world.accounting
{
	[Serializable]
	public abstract class TransactionSummary {
		public abstract long sales { get; }
		public abstract long expenditures { get; }
		public long balance { get { return sales-expenditures; } }
	}

	[Serializable]
	public abstract class TransactionAgoSummary {
        public virtual long sales(int i) { return 0; }
        public virtual long expenditures(int i) { return 0; }
		public long balance( int i ) { return sales(i)-expenditures(i); }
	}

	/// <summary>
	/// Records the summary of past transactions.
	/// </summary>
	[Serializable]
	public class TransactionHistory
	{
		[Serializable]
		private class Recorder {
			private long _dayTotal;
			private long _monthTotal;
			private long _yearTotal;
			private long _yesterdayTotal;
			private long _lastmonthTotal;
			private long _lastyearTotal;
            private long[] _dayAgoTotal = new long[32];
            private long[] _monthAgoTotal = new long[13];
			private readonly Clock clock = World.world.clock;

			internal Recorder() {
				// align the clock to 0:00am
				clock.registerRepeated(
					new ClockHandler(onClock),
					TimeLength.untilTomorrow(),
					TimeLength.ONEDAY );
			}

			internal long dayTotal   { get { return _dayTotal; } }
			internal long monthTotal { get { return _dayTotal+_monthTotal; } }
			internal long yearTotal  { get { return monthTotal+_yearTotal; } }
			internal long yesterdayTotal   { get { return _yesterdayTotal; } }
			internal long lastmonthTotal { get { return _lastmonthTotal; } }
			internal long lastyearTotal  { get { return _lastyearTotal; } }
			internal long[] dayAgoTotal   { get { return _dayAgoTotal; } }
			internal long[] monthAgoTotal { get { return _monthAgoTotal; } }

			internal void add( long delta ) {
				_dayTotal += delta;
			}

			public void onClock() {
				_monthTotal += _dayTotal;
                _yesterdayTotal = _dayTotal;

              for( int i=30; i>=0; i--)
                    _dayAgoTotal[ i+1 ] = _dayAgoTotal[ i ];

                _dayAgoTotal[ 0 ] = _dayTotal;
                _dayTotal = 0;
              
              if( clock.day==1 ) {
					_yearTotal += _monthTotal;
					_lastmonthTotal = _monthTotal;

                    for( int j=11; j>=0; j--)
                          _monthAgoTotal[ j+1 ] = _monthAgoTotal[ j ];

                    _monthAgoTotal[ 0 ] = _monthTotal;
					_monthTotal = 0;
					if( clock.month==4 ) {
					_lastyearTotal = _yearTotal;
						_yearTotal = 0;
					}
				}
			}
            [OnDeserialized()] 
            private void OnDeserializedMethod(StreamingContext context) 
            {
                if (_dayAgoTotal == null) _dayAgoTotal = new long[32];
                if (_monthAgoTotal == null) _monthAgoTotal = new long[13];
            }
		}

		// used to record sales and expenditures
		private readonly Recorder sales = new Recorder();
		private readonly Recorder expenditures = new Recorder();
		
		// expose those information to outside
		public TransactionSummary day;
		public TransactionSummary month;
		public TransactionSummary year;
		public TransactionSummary yesterday;
		public TransactionSummary lastmonth;
		public TransactionSummary lastyear;
		public TransactionAgoSummary dayAgo;
		public TransactionAgoSummary monthAgo;

		/// <summary>
		/// Record transactions of the given genre.
		/// </summary>
        public TransactionHistory() {
			day		= new DayTransactionSummary(this);
			month	= new MonthTransactionSummary(this);
			year	= new YearTransactionSummary(this);
			yesterday	= new YesterdayTransactionSummary(this);
			lastmonth	= new LastMonthTransactionSummary(this);
			lastyear	= new LastYearTransactionSummary(this);
			dayAgo		= new DayAgoTransactionSummary(this);
			monthAgo	= new MonthAgoTransactionSummary(this);
		}

		internal void earn( long delta ) {
			sales.add(delta);
		}

		internal void spend( long delta ) {
			expenditures.add(delta);
		}

		[Serializable]
		private class DayTransactionSummary : TransactionSummary {
			private readonly TransactionHistory history;

			internal DayTransactionSummary( TransactionHistory _history ) {
				this.history = _history;
			}

			public override long sales { get { return history.sales.dayTotal; } }
			public override long expenditures { get { return history.expenditures.dayTotal; } }
		}

		[Serializable]
		private class MonthTransactionSummary : TransactionSummary {
			private readonly TransactionHistory history;

			internal MonthTransactionSummary( TransactionHistory _history ) {
				this.history = _history;
			}

			public override long sales { get { return history.sales.monthTotal; } }
			public override long expenditures { get { return history.expenditures.monthTotal; } }
		}

		[Serializable]
		private class YearTransactionSummary : TransactionSummary {
			private readonly TransactionHistory history;

			internal YearTransactionSummary( TransactionHistory _history ) {
				this.history = _history;
			}

			public override long sales { get { return history.sales.yearTotal; } }
			public override long expenditures { get { return history.expenditures.yearTotal; } }
		}

		[Serializable]
		private class YesterdayTransactionSummary : TransactionSummary {
			private readonly TransactionHistory history;

			internal YesterdayTransactionSummary( TransactionHistory _history ) {
				this.history = _history;
			}

			public override long sales { get { return history.sales.yesterdayTotal; } }
			public override long expenditures { get { return history.expenditures.yesterdayTotal; } }
		}

		[Serializable]
		private class LastMonthTransactionSummary : TransactionSummary {
			private readonly TransactionHistory history;

			internal LastMonthTransactionSummary( TransactionHistory _history ) {
				this.history = _history;
			}

			public override long sales { get { return history.sales.lastmonthTotal; } }
			public override long expenditures { get { return history.expenditures.lastmonthTotal; } }
		}

		[Serializable]
		private class LastYearTransactionSummary : TransactionSummary {
			private readonly TransactionHistory history;

			internal LastYearTransactionSummary( TransactionHistory _history ) {
				this.history = _history;
			}

			public override long sales { get { return history.sales.lastyearTotal; } }
			public override long expenditures { get { return history.expenditures.lastyearTotal; } }
		}

		[Serializable]
		private class DayAgoTransactionSummary : TransactionAgoSummary {
			private readonly TransactionHistory history;

			internal DayAgoTransactionSummary( TransactionHistory _history ) {
				this.history = _history;
			}

			public override long sales( int i ) { return history.sales.dayAgoTotal[ i ]; }
			public override long expenditures( int i ) { return history.expenditures.dayAgoTotal[ i ]; }
		}

		[Serializable]
		private class MonthAgoTransactionSummary : TransactionAgoSummary {
			private readonly TransactionHistory history;

			internal MonthAgoTransactionSummary( TransactionHistory _history ) {
				this.history = _history;
			}

			public override long sales( int i ) { return history.sales.monthAgoTotal[ i ]; }
			public override long expenditures( int i ) { return history.expenditures.monthAgoTotal[ i ]; }
		}


      
        [OnDeserialized()] 
        private void OnDeserializedMethod(StreamingContext context) 
          { 
            　　//ここで追加されたオブジェクトを生成する
                if( yesterday == null )
                    yesterday	= new YesterdayTransactionSummary(this);
                if( lastmonth == null )
                    lastmonth	= new LastMonthTransactionSummary(this);
                if( lastyear == null )
                    lastyear	= new LastYearTransactionSummary(this);
                if( dayAgo == null )
                    dayAgo		= new DayAgoTransactionSummary(this);
                if( monthAgo == null )
			        monthAgo	= new MonthAgoTransactionSummary(this);
          }
    }
}
