using System;
using System.Xml;
using freetrain.framework.plugin;

namespace freetrain.contributions.population
{
	/// <summary>
	/// HourlyPopulation with a typical distribution for
	/// restaurants.
	/// </summary>
	[Serializable]
	public class BarPopulation : HourlyWeeklyPopulation
	{
		public BarPopulation( int baseP ) : base(baseP,sundayDistribution,weekdayDistribution,
                                                 weekdayDistribution,weekdayDistribution,weekdayDistribution,
                                                 fridayDistribution,saturdayDistribution,
                                                 sundayEntering,weekdayEntering,
                                                 weekdayEntering,weekdayEntering,weekdayEntering,
                                                 fridayEntering,saturdayEntering) {}

		public BarPopulation( XmlElement e )
			: this( int.Parse( XmlUtil.selectSingleNode(e,"base").InnerText) ) {}

		private static readonly int[] weekdayDistribution = new int[]{
			  5,  0,  0,  0,  0,  0,	//  0:00- 5:00
			  5,  0,  0,  0,  0,  5,	//  6:00-11:00
			 20, 10,  0,  0,  0, 10,	// 12:00-17:00
			 15, 30, 40, 40, 50, 30,	// 18:00-23:00
		};

		private static readonly int[] sundayDistribution = new int[]{
			 30, 15,  5,  5,  5,  5,	//  0:00- 5:00
			 10,  0,  0,  0,  0,  5,	//  6:00-11:00
			 20, 10, 10, 10, 10, 30,	// 12:00-17:00
			 30, 25, 25, 20, 20, 10,	// 18:00-23:00
		};

		private static readonly int[] fridayDistribution = new int[]{
			  5,  0,  0,  0,  0,  0,	//  0:00- 5:00
			  5,  0,  0,  0,  0,  5,	//  6:00-11:00
			 20, 10,  0,  0,  0, 10,	// 12:00-17:00
			 15, 40, 80, 90,100, 90,	// 18:00-23:00
		};

		private static readonly int[] saturdayDistribution = new int[]{
			 50, 20, 10, 10, 10, 10,	//  0:00- 5:00
			 20,  5,  5,  0,  0, 10,	//  6:00-11:00
			 20, 10, 10, 10, 10, 40,	// 12:00-17:00
			 40, 50, 60, 80, 80, 70,	// 18:00-23:00
		};

		private static readonly int[] weekdayEntering = new int[]{
			  5,  0,  0,  0,  0,  0,	//  0:00- 5:00
			  0,  0,  0,  0,  0,  5,	//  6:00-11:00
			 20, 10,  0,  0, 10, 15,	// 12:00-17:00
			 30, 40, 40, 30, 30, 10,	// 18:00-23:00
		};

		private static readonly int[] sundayEntering = new int[]{
			 10,  5,  0,  0,  0,  0,	//  0:00- 5:00
			  0,  0,  0,  0,  0,  5,	//  6:00-11:00
			 20, 10, 10, 10, 30, 30,	// 12:00-17:00
			 25, 25, 20, 20, 20, 10,	// 18:00-23:00
		};

		private static readonly int[] fridayEntering = new int[]{
			  5,  0,  0,  0,  0,  0,	//  0:00- 5:00
			  0,  0,  0,  0,  0,  5,	//  6:00-11:00
			 20, 10,  0,  0, 10, 15,	// 12:00-17:00
			 40, 90,100,100, 90, 70,	// 18:00-23:00
		};

		private static readonly int[] saturdayEntering = new int[]{
			 20, 10,  0,  0,  0,  0,	//  0:00- 5:00
			  0,  0,  0,  0,  0, 10,	//  6:00-11:00
			 20, 10, 10, 10, 30, 50,	// 12:00-17:00
			 50, 80, 80, 70, 70, 60,	// 18:00-23:00
		};
    }
}
