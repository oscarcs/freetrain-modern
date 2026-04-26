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
	public class ResidentialPopulation : HourlyPopulation
	{
		public ResidentialPopulation( int baseP ) : 
          base(baseP,weekdayDistribution,weekendDistribution, weekdayEntering,weekendEntering) {}

		public ResidentialPopulation( XmlElement e )
			: this( int.Parse( XmlUtil.selectSingleNode(e,"base").InnerText) ) {}

		private static readonly int[] weekdayDistribution = new int[]{
			 10,  5,  5,  5,  5,  5,	//  0:00- 5:00
			 40,100, 80, 80, 60, 40,	//  6:00-11:00
			 40, 30, 30, 40, 40, 30,	// 12:00-17:00
			 30, 25, 25, 20, 20, 15,	// 18:00-23:00
		};

		private static readonly int[] weekendDistribution = new int[]{
			 10,  5,  5,  5,  5,  5,	//  0:00- 5:00
			 20, 50, 40, 40, 50, 40,	//  6:00-11:00
			 40, 30, 30, 40, 40, 30,	// 12:00-17:00
			 30, 25, 25, 20, 20, 15,	// 18:00-23:00
		};

		private static readonly int[] weekdayEntering = new int[]{
			 20, 10,  5,  5,  5,  5,	//  0:00- 5:00
			  5, 10, 10, 10, 20, 20,	//  6:00-11:00
			 30, 30, 30, 40, 50, 60,	// 12:00-17:00
			 70, 90,100, 80, 60, 50,	// 18:00-23:00
		};

		private static readonly int[] weekendEntering = new int[]{
			 20, 10,  5,  5,  5,  5,	//  0:00- 5:00
			  5, 10, 10, 10, 20, 30,	//  6:00-11:00
			 40, 30, 30, 40, 50, 60,	// 12:00-17:00
			 70,100, 80, 60, 50, 40,	// 18:00-23:00
		};
	}
}
