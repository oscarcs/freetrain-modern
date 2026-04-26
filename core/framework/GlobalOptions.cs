using System;
using System.Collections;
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
	/// Global Configuration.
	/// 
	/// This is an application-wide configuration, which will be used across
	/// all the games.
	/// 
	/// Use freetrain.framework.Core.options to access the instance.
	/// </summary>
	[XmlTypeAttribute(Namespace="http://www.kohsuke.org/freetrain/globalConfig")]
	[XmlRootAttribute(Namespace="http://www.kohsuke.org/freetrain/globalConfig", IsNullable=false)]
	public class GlobalOptions : PersistentOptions
	{
		public GlobalOptions() {}

		/// <summary>
		/// If true, show a message box for errors.
		/// If false, show a message into the status bar.
		/// </summary>
		public bool showErrorMessageBox = false;

		public DDSurfaceAllocation surfaceAlloc = DirectDraw.SurfeceAllocation;
		public DDSurfaceAllocation SurfaceAlloc
		{
			get{ return surfaceAlloc; }
			set{ DirectDraw.SurfeceAllocation = value; 
				 surfaceAlloc = value; }
		}
		
		public double[] devParams = new double[11];

		/// <summary>
		/// Length of the time (in seconds) 
		/// while a message is displayed.
		/// </summary>
		public int messageDisplayTime = 3;

		public bool enableSoundEffect = true;

		/// <summary>
		/// Debug option to draw the bounding box
		/// </summary>
		public bool drawBoundingBox = false;

		private bool _drawStationNames = true;

		public bool drawStationNames {
			get {
				return _drawStationNames;
			}
			set {
				if( _drawStationNames!=value && world.World.world!=null )
					world.World.world.onAllVoxelUpdated();	// redraw
				_drawStationNames = value;
			}
		}

        private string _fontstringStationNames = "ＭＳ Ｐゴシック, 10pt";
      
		public string fontstringStationNames {
			get {
				return _fontstringStationNames;
			}
			set {
				if( _fontstringStationNames!=value && world.World.world!=null )
					world.World.world.onAllVoxelUpdated();	// redraw
				_fontstringStationNames = value;
			}
		}

        private int _colorvalueStationNames = 0x00ffffff;
      
		public int colorvalueStationNames {
			get {
				return _colorvalueStationNames;
			}
			set {
				if( _colorvalueStationNames!=value && world.World.world!=null )
					world.World.world.onAllVoxelUpdated();	// redraw
				_colorvalueStationNames = value;
			}
		}

		private bool _drawTrainNames = false;

		public bool drawTrainNames {
			get {
				return _drawTrainNames;
			}
			set {
				if( _drawTrainNames!=value && world.World.world!=null )
					world.World.world.onAllVoxelUpdated();	// redraw
				_drawTrainNames = value;
			}
		}

        private string _fontstringTrainNames = "ＭＳ Ｐゴシック, 8pt";
      
		public string fontstringTrainNames {
			get {
				return _fontstringTrainNames;
			}
			set {
				if( _fontstringTrainNames!=value && world.World.world!=null )
					world.World.world.onAllVoxelUpdated();	// redraw
				_fontstringTrainNames = value;
			}
		}

        private int _colorvalueTrainNames = 0x00ffff00;
      
		public int colorvalueTrainNames {
			get {
				return _colorvalueTrainNames;
			}
			set {
				if( _colorvalueTrainNames!=value && world.World.world!=null )
					world.World.world.onAllVoxelUpdated();	// redraw
				_colorvalueTrainNames = value;
			}
		}
      
      /// 架線柱を描画するかしないか
		private bool _drawElectlicPoles = true;

		public bool drawElectlicPoles {
			get {
				return _drawElectlicPoles;
			}
			set {
				if( _drawElectlicPoles!=value && world.World.world!=null )
					world.World.world.onAllVoxelUpdated();	// redraw
				_drawElectlicPoles = value;
			}
		}
		
        /// 破産時にメッセージボックスを表示するかしないか
		private bool _bunkruptmessageflag = true;

		public bool bunkruptMessageFlag {
			get {
				return _bunkruptmessageflag;
			}
			set {
				_bunkruptmessageflag = value;
			}
		}

        /// 破産時にプラスする資金額

        private long _liquidplusatbunkrupt = 10000000000;

        public long liquidPlusAtBunkrupt {
          get {
            return _liquidplusatbunkrupt;
          }
          set {
            _liquidplusatbunkrupt = value;
          }
        }
      
		/// <summary>
		/// If false, draw trees.
		/// If true, speed up drawing by ignore drawing trees.
		/// </summary>
		private bool _hideTrees = false;

		public bool hideTrees 
		{
			get 
			{
				return _hideTrees;
			}
			set 
			{
				if( _hideTrees!=value && world.World.world!=null )
					world.World.world.onAllVoxelUpdated();	// redraw
				_hideTrees = value;
			}
		}

		public new GlobalOptions load() 
		{
			GlobalOptions opt = (GlobalOptions)base.load();
          
			DirectDraw.SurfeceAllocation = opt.SurfaceAlloc;

			return opt;
		}

		// Maintain backward-compatibility
		protected override string Stem { get { return ""; } }
	}
}
