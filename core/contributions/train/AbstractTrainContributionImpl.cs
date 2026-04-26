using System;
using System.Xml;
using freetrain.framework;
using freetrain.framework.plugin;

namespace freetrain.contributions.train
{
	/// <summary>
	/// Common base implementation of TrainContribution
	/// </summary>
	[Serializable]
	public abstract class AbstractTrainContributionImpl : TrainContribution
	{
		/// <summary>
		/// Parses a train contribution from a DOM node.
		/// </summary>
		/// <exception cref="XmlException">If the parsing fails</exception>
		protected AbstractTrainContributionImpl( XmlElement e ) : base(e.Attributes["id"].Value) {
			_companyName = XmlUtil.selectSingleNode(e,"company").InnerText;
			_typeName = XmlUtil.selectSingleNode(e,"type").InnerText;
			_nickName = XmlUtil.selectSingleNode(e,"name").InnerText;
			_description= XmlUtil.selectSingleNode(e,"description").InnerText;
			_author= XmlUtil.selectSingleNode(e,"author").InnerText;

			_price = int.Parse( XmlUtil.selectSingleNode(e,"price").InnerText );
			_fare = int.Parse( XmlUtil.selectSingleNode(e,"fare").InnerText );
			_amenity = int.Parse( XmlUtil.selectSingleNode(e,"amenity").InnerText );
			// TODO: pictures

            try{
                string triprangeStr = XmlUtil.selectSingleNode(e,"triprange").InnerText.ToLower();
			    switch(triprangeStr) {
			        case "short":
				        _triprange = 0; break;
			        case "middle":
				        _triprange = 1; break;
			        case "long":
				        _triprange = 2; break;
			        default:
				        _triprange = 0; break;
			    }
            }catch( Exception ) {
					_triprange = 0;
			
            }


			string speedStr = XmlUtil.selectSingleNode(e,"speed").InnerText.ToLower();
			switch(speedStr) {
			case "slow":
				_minutesPerVoxel = 4; break;
			case "medium":
				_minutesPerVoxel = 3; break;
			case "fast":
				_minutesPerVoxel = 2; break;
			case "superb":
				_minutesPerVoxel = 1; break;
			default:
				try {
					_minutesPerVoxel = int.Parse(speedStr);
				} catch( Exception ) {
					throw new XmlException("unknown speed:"+speedStr,null);
				}
				break;
			}
		}

		public override string name { get { return _typeName+" "+_nickName; } }

		private readonly string _nickName;
		public override string nickName { get { return _nickName; } }

		private readonly string _typeName;
		public override string typeName { get { return _typeName; } }

		private readonly string _author;
		public override string author { get { return _author; } }

		private readonly string _companyName;
		public override string companyName { get { return _companyName; } }

		private readonly string _description;
		public override string description { get { return _description; } }

		private readonly int _price;
		public override int price( int length ) { return _price*length; }

		private readonly int _minutesPerVoxel;
		public override int minutesPerVoxel { get { return _minutesPerVoxel; } }

		private readonly int _fare = 400;
		public override int fare { get { return _fare; } }

        private readonly int _amenity = 100;
		public override int amenity {
          get {
            if( _amenity < 50 ) return 50;
            else if ( _amenity > 200 ) return 200;
            return _amenity;
          }
        }

        private readonly int _triprange;
		public override int triprange { get { return _triprange; } }

    }
}
