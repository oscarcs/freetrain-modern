using System;
using System.Xml;
using freetrain.framework.plugin;

namespace freetrain.contributions.population
{
	/// <summary>
	/// HourlyPopulation with a typical distribution for
	/// shoppers to shops.
	/// </summary>
	[Serializable]
	public class ShopperPopulation : HourlyPopulation
	{
		public ShopperPopulation( int baseP ) :
          base(baseP,weekdayDistribution,weekendDistribution, weekdayEntering,weekendEntering) {}

		public ShopperPopulation( XmlElement e )
			: this( int.Parse( XmlUtil.selectSingleNode(e,"base").InnerText) ) {}

		private static readonly int[] weekdayDistribution = new int[]{
			  0,  0,  0,  0,  0,  0,	//  0:00- 5:00
			  0,  0,  0,  5, 20, 75,	//  6:00-11:00
			 45, 25, 15, 40, 70,100,	// 12:00-17:00
			 45, 20, 10,  5,  0,  0,	// 18:00-23:00
		};

		private static readonly int[] weekendDistribution = new int[]{
			  0,  0,  0,  0,  0,  0,	//  0:00- 5:00
			  0,  0,  0,  5, 20, 75,	//  6:00-11:00
			 45, 55, 75, 75, 70, 70,	// 12:00-17:00
			 70, 55, 40, 20,  5,  0,	// 18:00-23:00
		};

		private static readonly int[] weekdayEntering = new int[]{
			  0,  0,  0,  0,  0,  0,	//  0:00- 5:00
			  0,  0,  0,  5, 30, 50,	//  6:00-11:00
			 40, 30, 30, 40, 50,100,	// 12:00-17:00
			 50, 10, 10,  5,  0,  0,	// 18:00-23:00
		};

		private static readonly int[] weekendEntering = new int[]{
			  0,  0,  0,  0,  0,  0,	//  0:00- 5:00
			  0,  0,  0,  5, 40, 60,	//  6:00-11:00
			 50, 50, 40, 50, 60, 90,	// 12:00-17:00
			 60, 20, 10,  5,  0,  0,	// 18:00-23:00
		};
	}
}
