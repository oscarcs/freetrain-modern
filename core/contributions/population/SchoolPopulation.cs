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
	/// school structures (such as high school, junior high school, etc.)
	/// </summary>
	[Serializable]
	public class SchoolPopulation : Population
	{
		public SchoolPopulation( int baseP ) :
          this(baseP,weekdayDistribution,weekendDistribution, weekdayEntering,weekendEntering) {}

		public SchoolPopulation( XmlElement e )
			: this( int.Parse( XmlUtil.selectSingleNode(e,"base").InnerText) ) {}

		private static readonly int[] weekdayDistribution = new int[]{
			 0,  0,  0,  0,  0,  0,	//  0:00- 5:00
			 0,  0,  0,  0,  0,  0,	//  6:00-11:00
			 20, 20, 10, 30, 60,100,	// 12:00-17:00
			 70, 40, 20,  0,  0,  0,	// 18:00-23:00
		};

		private static readonly int[] weekendDistribution = new int[]{
			 0,  0,  0,  0,  0,  0,	//  0:00- 5:00
			 0,  0,  0,  0,  0,  5,	//  6:00-11:00
			10,  5,  5,  5, 10, 10,	//  12:00- 17:00
			 5,  0,  0,  0,  0,  0,	//  18:00-23:00
		};

		private static readonly int[] weekdayEntering = new int[]{
			 0,  0,  0,  0,  0,  0,	//  0:00- 5:00
			 20, 60,100, 20, 5, 5,	//  6:00-11:00
			  5,  5,  5,  5, 5, 5,	// 12:00-17:00
			  5,  5,  5,  0, 0, 0,	// 18:00-23:00
		};

		private static readonly int[] weekendEntering = new int[]{
			 0,  0,  0,  0,  0,  0,	//  0:00- 5:00
			 0,  5, 10, 20, 10,  5,	//  6:00-11:00
			 5,  5,  5,  5,  5,  0,	//  12:00- 17:00
			 0,  0,  0,  0,  0,  0,	//  18:00-23:00
		};

		public SchoolPopulation( int basep, int[] weekdayHourTable, int[] weekendHourTable,
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

		public override int residents { get { return 1; } }  // no one lives in School

		public override int calcPopulation( Time currentTime ) {
			if( currentTime.isWeekend || currentTime.isHoliday || currentTime.isVacation )
				return population * weekendHourTable[currentTime.hour] / 100;
			else
				return population * weekdayHourTable[currentTime.hour] / 100;
		}

		public override int calcEntering( Time currentTime ) {
			if( currentTime.isWeekend || currentTime.isHoliday || currentTime.isVacation )
				return population * weekendEnterTable[currentTime.hour] / 100;
			else
				return population * weekdayEnterTable[currentTime.hour] / 100;
		}

	}

}
