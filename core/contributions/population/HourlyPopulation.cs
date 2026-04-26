using System;
using System.Diagnostics;
using freetrain.world;
using freetrain.world.structs;
using freetrain.framework.plugin;

namespace freetrain.contributions.population
{
	/// <summary>
	/// Population depends on hour of the day
	/// </summary>
	[Serializable]
	public class HourlyPopulation : Population
	{
		public HourlyPopulation( int basep, int[] weekdayHourTable, int[] weekendHourTable,
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

		public override int residents { get { return population; } }

		public override int calcPopulation( Time currentTime ) {
			if( currentTime.isWeekend || currentTime.isHoliday )
				return population * weekendHourTable[currentTime.hour] / 100;
			else
				return population * weekdayHourTable[currentTime.hour] / 100;
		}

		public override int calcEntering( Time currentTime ) {
			if( currentTime.isWeekend || currentTime.isHoliday )
				return population * weekendEnterTable[currentTime.hour] / 100;
			else
				return population * weekdayEnterTable[currentTime.hour] / 100;
		}
	}
}
