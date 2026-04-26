using System;
using System.Collections;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Serialization;
using freetrain.util;
using freetrain.DirectXWrapper;

namespace freetrain.framework
{
	/// <summary>
	/// Font option Configuration.
	/// 
	/// This is an application-wide configuration, which will be used across
	/// all the games.
	/// 
	/// Use freetrain.framework.Core.fontoptions to access the instance.
	/// </summary>
	public class FontOptions
	{
		public FontOptions() {}

        private Font _fontStationNames = new Font("ＭＳ Ｐゴシック", 10);
        private Color _colorStationNames = Color.White;

        public Font fontStationNames{
            get {
              return _fontStationNames;
            }
            set {
				if( _fontStationNames!=value && world.World.world!=null )
					world.World.world.onAllVoxelUpdated();	// redraw
				_fontStationNames = value;
			}
        }
      
        public Color colorStationNames{
            get {
              return _colorStationNames;
            }
            set {
				if( _colorStationNames!=value && world.World.world!=null )
					world.World.world.onAllVoxelUpdated();	// redraw
				_colorStationNames = value;
			}
        }
      
        private Font _fontTrainNames = new Font("ＭＳ Ｐゴシック", 8);
        private Color _colorTrainNames = Color.Yellow;

        public Font fontTrainNames{
            get {
              return _fontTrainNames;
            }
            set {
				if( _fontTrainNames!=value && world.World.world!=null )
					world.World.world.onAllVoxelUpdated();	// redraw
				_fontTrainNames = value;
			}
        }
      
        public Color colorTrainNames{
            get {
              return _colorTrainNames;
            }
            set {
				if( _colorTrainNames!=value && world.World.world!=null )
					world.World.world.onAllVoxelUpdated();	// redraw
				_colorTrainNames = value;
			}
        }

        public FontOptions load( GlobalOptions opts )
          {
            FontOptions fontoptions = new FontOptions();

            TypeConverter converter;
            converter = TypeDescriptor.GetConverter(typeof(Font));
            fontoptions.fontStationNames = (Font)(converter.ConvertFromString( opts.fontstringStationNames ));
            fontoptions.fontTrainNames = (Font)(converter.ConvertFromString( opts.fontstringTrainNames ));
            fontoptions.colorStationNames = Color.FromArgb( opts.colorvalueStationNames );
            fontoptions.colorTrainNames = Color.FromArgb( opts.colorvalueTrainNames );

            return fontoptions;
          }

	}
}
