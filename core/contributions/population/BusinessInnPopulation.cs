using System;
using System.Xml;
using freetrain.framework.plugin;

namespace freetrain.contributions.population
{
	/// <summary>
	/// HourlyPopulation with a typical distribution for
	/// residential structures (such as houses, apartments, etc.)
	/// </summary>
	[Serializable]
	public class BusinessInnPopulation : HourlyPopulation
	{
		public BusinessInnPopulation( int baseP ) : 
          base(baseP,weekdayDistribution,weekdayDistribution, weekdayEntering,weekdayEntering) {}

		public BusinessInnPopulation( XmlElement e )
			: this( int.Parse( XmlUtil.selectSingleNode(e,"base").InnerText) ) {}

		private static readonly int[] weekdayDistribution = new int[]{
			  5,  5,  5,  5,  5,  5,	//  0:00- 5:00
			 10, 40, 80,100, 50, 30,	//  6:00-11:00
			 10,  5,  5,  5,  5,  5,	// 12:00-17:00
			  5,  5,  5,  5,  5,  5,	// 18:00-23:00
		};
        // Business Inn is equal usage weekday/weekend

		private static readonly int[] weekdayEntering = new int[]{
			 10,  5,  5,  5,  5,  5,	//  0:00- 5:00
			 10, 10, 10, 10, 10, 10,	//  6:00-11:00
			 10, 10, 10, 20, 30, 60,	// 12:00-17:00
			 90, 90, 60, 30, 20, 10,	// 18:00-23:00
		};

    }
}
