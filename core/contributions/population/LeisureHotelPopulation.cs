using System;
using System.Xml;
using System.Diagnostics;
using freetrain.world;
using freetrain.world.structs;
using freetrain.framework.plugin;

namespace freetrain.contributions.population
{
	/// <summary>
	/// HourlyPopulation with a typical distribution for
	/// residential structures (such as houses, apartments, etc.)
	/// </summary>
	[Serializable]
	public class LeisureHotelPopulation : Population
	{
		public LeisureHotelPopulation( int baseP ) : 
          this(baseP,weekdayDistribution,weekendDistribution, weekdayEntering,weekendEntering) {}

		public LeisureHotelPopulation( XmlElement e )
			: this( int.Parse( XmlUtil.selectSingleNode(e,"base").InnerText) ) {}

		private static readonly int[] weekdayDistribution = new int[]{
			  0,  0,  0,  0,  0,  0,	//  0:00- 5:00
			  5, 20, 30, 50, 30, 20,	//  6:00-11:00
			 10,  5,  5,  5,  5,  5,	// 12:00-17:00
			  5,  5,  5,  5,  0,  0,	// 18:00-23:00
		};

		private static readonly int[] weekendDistribution = new int[]{
			  0,  0,  0,  0,  0,  0,	//  0:00- 5:00
			 10, 40, 60,100, 80, 20,	//  6:00-11:00
			 10, 20, 20,  5,  5, 10,	// 12:00-17:00
			 10,  5,  5,  5,  0,  0,	// 18:00-23:00
		};

		private static readonly int[] weekdayEntering = new int[]{
			  0,  0,  0,  0,  0,  0,	//  0:00- 5:00
			  5, 10, 10, 10, 10, 10,	//  6:00-11:00
			 10, 10, 10, 20, 25, 30,	// 12:00-17:00
			 40, 40, 30, 15, 10,  5,	// 18:00-23:00
		};

		private static readonly int[] weekendEntering = new int[]{
			  0,  0,  0,  0,  0,  0,	//  0:00- 5:00
			  5, 10, 10, 10, 20, 20,	//  6:00-11:00
			 20, 20, 30, 50, 60,100,	// 12:00-17:00
			 80, 80, 60, 40, 20, 10,	// 18:00-23:00
		};

        public LeisureHotelPopulation( int basep, int[] weekdayHourTable, int[] weekendHourTable,
                                       int[] weekdayEnterTable, int[] weekendEnterTable) {

			this.population = basep;
			this.weekdayHourTable = weekdayHourTable;
			this.weekendHourTable = weekendHourTable;
			this.weekdayEnterTable = weekdayEnterTable;
			this.weekendEnterTable = weekendEnterTable;
		}

		/// <summary>
		/// Ration of each hour in percentage
		/// </summary>
		private readonly int[] weekdayHourTable;
		private readonly int[] weekendHourTable;
		private readonly int[] weekdayEnterTable;
		private readonly int[] weekendEnterTable;
		private readonly int population;

		public override int residents { get { return 10; } }

		public override int calcPopulation( Time currentTime ) {
			if( currentTime.isWeekend || currentTime.isHoliday )
				return population * weekendHourTable[currentTime.hour] / 100;
			else if( currentTime.isVacation )
				return population * weekendHourTable[currentTime.hour] / 125;
            else 
				return population * weekdayHourTable[currentTime.hour] / 100;
		}


		public override int calcEntering( Time currentTime ) {
			if( currentTime.isWeekend || currentTime.isHoliday )
				return population * weekendEnterTable[currentTime.hour] / 100;
			else if( currentTime.isVacation )
				return population * weekendEnterTable[currentTime.hour] / 125;
            else 
				return population * weekdayEnterTable[currentTime.hour] / 100;
		}

	}
}
