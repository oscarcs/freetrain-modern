//2010.05.02 riorio added seatedcapacity
using System;
using System.Drawing;
using System.Xml;
using freetrain.framework.plugin;
using freetrain.world;
using freetrain.DirectXWrapper;

namespace freetrain.contributions.train
{
	/// <summary>
	/// Train car type.
	/// </summary>
	[Serializable]
	public abstract class TrainCarContribution : Contribution
	{
		public TrainCarContribution( XmlElement e ) : base(e) {
			_capacity = int.Parse(XmlUtil.selectSingleNode(e,"capacity").InnerText);
			_seatedcapacity = int.Parse(XmlUtil.selectSingleNode(e,"seatedcapacity").InnerText);
		}
		protected TrainCarContribution( string id, int cap ) : this( id, cap, cap ) {
		}
		protected TrainCarContribution( string id, int cap, int scap ) : base("trainCar",id) {
			_capacity = cap;
            _seatedcapacity = scap;
		}

		/// <summary>
		/// Number of passengers this car can hold.
		/// </summary>
		public int capacity { get { return _capacity; } }
		protected int _capacity;

		/// <summary>
		/// Number of passengers seated this car can hold.
		/// </summary>
		public int seatedcapacity { get { return _seatedcapacity; } }
		protected int _seatedcapacity;

		/// <summary>
		/// Draws a car to the specified position.
		/// </summary>
		/// <param name="angle">[0,16). Angle of the car. 2*direction.index</param>
		public abstract void draw( Surface display, Point pt, int angle );

		/// <summary>
		/// Dras a car on a slope.
		/// </summary>
		/// <param name="angle">one of 4 directions</param>
		/// <param name="isClimbing">true if the car is climbing</param>
		public abstract void drawSlope( Surface display, Point pt, Direction angle, bool isClimbing );

		// TODO: support cargos
	}
}
