using System;
using System.Xml;
using System.Drawing;
using freetrain.contributions.common;
using freetrain.framework.plugin;
using freetrain.framework.graphics;
using freetrain.controllers;
using freetrain.contributions.population;

namespace freetrain.contributions.rail
{
	/// <summary>
	/// Contribution that adds <c>TrafficVoxel.Accessory</c>
	/// </summary>
	[Serializable]
	public abstract class RailAccessoryContribution : Contribution, IEntityBuilder
	{
		private readonly string _name;
        private long _price;

		public RailAccessoryContribution( XmlElement e ) : base(e) {
			_name = XmlUtil.selectSingleNode(e,"name").InnerText;
            XmlAttribute a = e.Attributes["price"];
            if( a == null ) _price = 0;
            else _price = long.Parse( XmlUtil.selectSingleNode(e,"price").InnerText );
		}


		// TODO: do we need a method like
		// void create( Location loc ) ?

		#region IEntityBuilder メンバ
		public virtual string name { get { return _name; } }

		public virtual Population population { get { return null; } }

		public virtual long price {	get {return _price;}	}
		public virtual double pricePerArea {	get {return 0;}	}

		public bool computerCannotBuild { get{ return false; } }

		public bool playerCannotBuild {	get{ return true; }	}

		public abstract freetrain.framework.graphics.PreviewDrawer createPreview(System.Drawing.Size pixelSize);

		public abstract freetrain.controllers.ModalController createBuilder(freetrain.controllers.IControllerSite site);

		public abstract freetrain.controllers.ModalController createRemover(freetrain.controllers.IControllerSite site);

		#endregion
	}
}
