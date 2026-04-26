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
	public class HourlyWeeklyPopulation : Population
	{
		public HourlyWeeklyPopulation( int basep, int[] sundayHourTable, int[] mondayHourTable,
                                 int[] tuesdayHourTable, int[] wednesdayHourTable, int[] thursdayHourTable,
                                 int[] fridayHourTable, int[] saturdayHourTable,
                                 int[] sundayEnterTable, int[] mondayEnterTable, int[] tuesdayEnterTable,
                                 int[] wednesdayEnterTable, int[] thursdayEnterTable,
                                 int[] fridayEnterTable, int[] saturdayEnterTable ) {

			this.population = basep;
			this.sundayHourTable = sundayHourTable;
			this.mondayHourTable = mondayHourTable;
			this.tuesdayHourTable = tuesdayHourTable;
			this.wednesdayHourTable = wednesdayHourTable;
			this.thursdayHourTable = thursdayHourTable;
			this.fridayHourTable = fridayHourTable;
			this.saturdayHourTable = saturdayHourTable;
			this.sundayEnterTable = sundayEnterTable;
			this.mondayEnterTable = mondayEnterTable;
			this.tuesdayEnterTable = tuesdayEnterTable;
			this.wednesdayEnterTable = wednesdayEnterTable;
			this.thursdayEnterTable = thursdayEnterTable;
			this.fridayEnterTable = fridayEnterTable;
			this.saturdayEnterTable = saturdayEnterTable;
		}

		/// <summary>
		/// Ration of each hour in percentage
		/// </summary>
		private readonly int[] sundayHourTable;
		private readonly int[] mondayHourTable;
		private readonly int[] tuesdayHourTable;
		private readonly int[] wednesdayHourTable;
		private readonly int[] thursdayHourTable;
		private readonly int[] fridayHourTable;
		private readonly int[] saturdayHourTable;
		private readonly int[] sundayEnterTable;
		private readonly int[] mondayEnterTable;
		private readonly int[] tuesdayEnterTable;
		private readonly int[] wednesdayEnterTable;
		private readonly int[] thursdayEnterTable;
		private readonly int[] fridayEnterTable;
		private readonly int[] saturdayEnterTable;
		private readonly int population;

		public override int residents { get { return population; } }

        public override int calcPopulation(Time currentTime)
        {

            if( currentTime.isHoliday ) return population * sundayHourTable[currentTime.hour] / 100;
          
            switch (currentTime.dayOfWeek)
            {
                case 0: return population * sundayHourTable[currentTime.hour] / 100;
                case 1: return population * mondayHourTable[currentTime.hour] / 100;
                case 2: return population * tuesdayHourTable[currentTime.hour] / 100;
                case 3: return population * wednesdayHourTable[currentTime.hour] / 100;
                case 4: return population * thursdayHourTable[currentTime.hour] / 100;
                case 5: return population * fridayHourTable[currentTime.hour] / 100;
                case 6: return population * saturdayHourTable[currentTime.hour] / 100;
            }
            return 0;
        }


        public override int calcEntering(Time currentTime)
        {

            if( currentTime.isHoliday ) return population * sundayEnterTable[currentTime.hour] / 100;
          
            switch (currentTime.dayOfWeek)
            {
                case 0: return population * sundayEnterTable[currentTime.hour] / 100;
                case 1: return population * mondayEnterTable[currentTime.hour] / 100;
                case 2: return population * tuesdayEnterTable[currentTime.hour] / 100;
                case 3: return population * wednesdayEnterTable[currentTime.hour] / 100;
                case 4: return population * thursdayEnterTable[currentTime.hour] / 100;
                case 5: return population * fridayEnterTable[currentTime.hour] / 100;
                case 6: return population * saturdayEnterTable[currentTime.hour] / 100;
            }
            return 0;
        }
	}
}
