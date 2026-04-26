using System;
using System.Xml;
using freetrain.framework.plugin;

namespace freetrain.contributions.population
{
	/// <summary>
	/// HourlyPopulation with a typical distribution for
	/// agricultural lands
	/// </summary>
	[Serializable]
	public class AgriculturalPopulation : HourlyPopulation
	{
		public AgriculturalPopulation( int baseP ) :
          base(baseP,weekdayDistribution,weekendDistribution, weekdayEntering,weekendEntering) {}

		public AgriculturalPopulation( XmlElement e )
			: this( int.Parse( XmlUtil.selectSingleNode(e,"base").InnerText) ) {}

		// TODO: parameter calibration
		private static readonly int[] weekdayDistribution = new int[]{
			  5,  5,  5,  5,  5,  5,	//  0:00- 5:00
			  5,  5,  5,  5,  5,  5,	//  6:00-11:00
			  5,  5,  5,  5,  5,  5,	// 12:00-17:00
			  5,  5,  5,  5,  5,  5,	// 18:00-23:00
		};

		private static readonly int[] weekendDistribution = new int[]{
			  5,  5,  5,  5,  5,  5,	//  0:00- 5:00
			  5,  5,  5,  5,  5,  5,	//  6:00-11:00
			  5,  5,  5,  5,  5,  5,	// 12:00-17:00
			  5,  5,  5,  5,  5,  5,	// 18:00-23:00
		};

		private static readonly int[] weekdayEntering = new int[]{
			  5,  5,  5,  5,  5,  5,	//  0:00- 5:00
			  5,  5,  5,  5,  5,  5,	//  6:00-11:00
			  5,  5,  5,  5,  5,  5,	// 12:00-17:00
			  5,  5,  5,  5,  5,  5,	// 18:00-23:00
		};

		private static readonly int[] weekendEntering = new int[]{
			  5,  5,  5,  5,  5,  5,	//  0:00- 5:00
			  5,  5,  5,  5,  5,  5,	//  6:00-11:00
			  5,  5,  5,  5,  5,  5,	// 12:00-17:00
			  5,  5,  5,  5,  5,  5,	// 18:00-23:00
		};
	}
}
