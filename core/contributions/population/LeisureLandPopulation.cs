using System;
using System.Xml;
using System.Diagnostics;
using freetrain.world;
using freetrain.world.structs;
using freetrain.framework.plugin;

namespace freetrain.contributions.population
{
	/// <summary>
	/// LeisureLandPopulation
	/// </summary>
	[Serializable]
	public class LeisureLandPopulation : Population
	{
		public LeisureLandPopulation( int baseP ) :
      this(baseP,weekdayDistribution,weekendDistribution, weekdayEntering,weekendEntering) {}

		public LeisureLandPopulation( XmlElement e )
			: this( int.Parse( XmlUtil.selectSingleNode(e,"base").InnerText) ) {}

		private static readonly int[] weekdayDistribution = new int[]{
			  0,  0,  0,  0,  0,  0,	//  0:00- 5:00
			  0,  0,  0,  0,  5, 10,	//  6:00-11:00
			 10, 10, 20, 20, 30, 40,	// 12:00-17:00
			 30, 30, 30, 20,  5,  0,	// 18:00-23:00
		};

		private static readonly int[] weekendDistribution = new int[]{
			  0,  0,  0,  0,  0,  0,	//  0:00- 5:00
			  0,  0,  0,  0, 10, 20,	//  6:00-11:00
			 30, 30, 60, 60, 60, 80,	// 12:00-17:00
			100, 70, 60, 60, 15,  5,	// 18:00-23:00
		};

		private static readonly int[] weekdayEntering = new int[]{
			  0,  0,  0,  0,  0,  0,	//  0:00- 5:00
			  5, 10, 10, 20, 20, 20,	//  6:00-11:00
			 20, 20, 20, 20, 25, 30,	// 12:00-17:00
			 40, 30, 10,  5,  0,  0,	// 18:00-23:00
		};

		private static readonly int[] weekendEntering = new int[]{
			  0,  0,  0,  0,  0,  0,	//  0:00- 5:00
			  5, 20, 40, 80,100, 80,	//  6:00-11:00
			 70, 60, 50, 40, 40, 30,	// 12:00-17:00
			 30, 30, 10,  5,  0,  0,	// 18:00-23:00
		};

        public LeisureLandPopulation( int basep, int[] weekdayHourTable, int[] weekendHourTable,
                                      int[] weekdayEnterTable, int[] weekendEnterTable ) {


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
