using System;
using System.Drawing;
using System.Collections;
using System.ComponentModel;
using System.Windows.Forms;
using freetrain.framework;
using freetrain.framework.graphics;
using freetrain.util;
using freetrain.world;
using freetrain.world.rail;
using freetrain.views;
using freetrain.views.map;
using freetrain.DirectXWrapper;
using freetrain.controllers;


namespace freetrain.world.rail.speedlimit
{
	/// <summary>
	/// 速度制限 勾配用ダイアログ
	/// </summary>
	public class SpeedLimitSlopeController : AbstractControllerImpl, LocationDisambiguator, MapOverlay
	{
		#region Singleton instance management
		/// <summary>
		/// Creates a new controller window, or active the existing one.
		/// </summary>
		public static void create() {
			if(theInstance==null)
				theInstance = new SpeedLimitSlopeController();
			theInstance.Show();
			theInstance.Activate();
		}

		private System.Windows.Forms.ComboBox comboBoxSpeed;
		private System.Windows.Forms.Label labelSpeed;

		private static SpeedLimitSlopeController theInstance;

		protected override void OnClosing(System.ComponentModel.CancelEventArgs e) {
			base.OnClosing(e);
			theInstance = null;
		}
		#endregion



		public SpeedLimitSlopeController() 
		{
			// Windows フォーム デザイナ サポートに必要です。
			InitializeComponent();
			
			setSpeedList();

			pictureN.Tag = Direction.get(0);
			pictureE.Tag = Direction.get(2);
			pictureS.Tag = Direction.get(4);
			pictureW.Tag = Direction.get(6);

			update( pictureN, pictureN );	// select N first
			updatePreview();
		}

		public override void updatePreview()
		{
			
			PreviewDrawer drawer;

			// TODO: locations of the previews are uttely incorrect. fix them

			// direction N
			using(drawer = new PreviewDrawer( pictureN.ClientSize, new Size(2,4), 0 )) {
				drawer.draw( RailPattern.getSlope( Direction.NORTH, 3 ), 1, -1 );
				drawer.draw( RailPattern.getSlope( Direction.NORTH, 2 ), 1, 0 );
				drawer.draw( RailPattern.getSlope( Direction.NORTH, 1 ), 0, 2 );
				drawer.draw( RailPattern.getSlope( Direction.NORTH, 0 ), 0, 3 );
				if(pictureN.Image!=null) pictureN.Image.Dispose();
				pictureN.Image = drawer.createBitmap();
			}

			// direction S
			using(drawer = new PreviewDrawer( pictureS.ClientSize, new Size(2,4), 0 )) {
				drawer.draw( RailPattern.getSlope( Direction.SOUTH, 0 ), 0, 0 );
				drawer.draw( RailPattern.getSlope( Direction.SOUTH, 1 ), 0, 1 );
				drawer.draw( RailPattern.getSlope( Direction.SOUTH, 2 ), 1, 1 );
				drawer.draw( RailPattern.getSlope( Direction.SOUTH, 3 ), 1, 2 );
				if(pictureS.Image!=null) pictureS.Image.Dispose();
				pictureS.Image = drawer.createBitmap();
			}

			// direction E
			using(drawer = new PreviewDrawer( pictureE.ClientSize, new Size(4,2), 0 )) {
				drawer.draw( RailPattern.getSlope( Direction.EAST, 3 ),  3, 0 );
				drawer.draw( RailPattern.getSlope( Direction.EAST, 2 ),  2, 0 );
				drawer.draw( RailPattern.getSlope( Direction.EAST, 1 ),  0, 1 );
				drawer.draw( RailPattern.getSlope( Direction.EAST, 0 ), -1, 1 );
				if(pictureE.Image!=null) pictureE.Image.Dispose();
				pictureE.Image = drawer.createBitmap();
			}

			// direction W
			using(drawer = new PreviewDrawer( pictureW.ClientSize, new Size(4,2), 0 )) {
				drawer.draw( RailPattern.getSlope( Direction.WEST, 3 ), 1, 0 );
				drawer.draw( RailPattern.getSlope( Direction.WEST, 2 ), 2, 0 );
				drawer.draw( RailPattern.getSlope( Direction.WEST, 1 ), 2, 1 );
				drawer.draw( RailPattern.getSlope( Direction.WEST, 0 ), 3, 1 );
				if(pictureW.Image!=null) pictureW.Image.Dispose();
				pictureW.Image = drawer.createBitmap();
			}
		}

		protected override void Dispose( bool disposing ) {
			if( disposing && components != null)
					components.Dispose();
			base.Dispose( disposing );
		}

		#region Windows Form Designer generated code
		private System.Windows.Forms.PictureBox pictureN;
		private System.Windows.Forms.PictureBox pictureE;
		private System.Windows.Forms.PictureBox pictureS;
		private System.Windows.Forms.PictureBox pictureW;
		private System.Windows.Forms.RadioButton buttonPlace;
		private System.Windows.Forms.RadioButton buttonRemove;
		/// <summary>
		/// 必要なデザイナ変数です。
		/// </summary>
		private System.ComponentModel.Container components = null;

		/// <summary>
		/// デザイナ サポートに必要なメソッドです。このメソッドの内容を
		/// コード エディタで変更しないでください。
		/// </summary>
		private void InitializeComponent()
		{
            this.pictureN = new System.Windows.Forms.PictureBox();
            this.pictureE = new System.Windows.Forms.PictureBox();
            this.pictureS = new System.Windows.Forms.PictureBox();
            this.pictureW = new System.Windows.Forms.PictureBox();
            this.buttonPlace = new System.Windows.Forms.RadioButton();
            this.buttonRemove = new System.Windows.Forms.RadioButton();
            this.comboBoxSpeed = new System.Windows.Forms.ComboBox();
            this.labelSpeed = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.pictureN)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.pictureE)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.pictureS)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.pictureW)).BeginInit();
            this.SuspendLayout();
            // 
            // pictureN
            // 
            this.pictureN.Location = new System.Drawing.Point(8, 8);
            this.pictureN.Name = "pictureN";
            this.pictureN.Size = new System.Drawing.Size(96, 48);
            this.pictureN.SizeMode = System.Windows.Forms.PictureBoxSizeMode.CenterImage;
            this.pictureN.TabIndex = 0;
            this.pictureN.TabStop = false;
            this.pictureN.Click += new System.EventHandler(this.picture_Click);
            // 
            // pictureE
            // 
            this.pictureE.Location = new System.Drawing.Point(8, 64);
            this.pictureE.Name = "pictureE";
            this.pictureE.Size = new System.Drawing.Size(96, 48);
            this.pictureE.SizeMode = System.Windows.Forms.PictureBoxSizeMode.CenterImage;
            this.pictureE.TabIndex = 1;
            this.pictureE.TabStop = false;
            this.pictureE.Click += new System.EventHandler(this.picture_Click);
            // 
            // pictureS
            // 
            this.pictureS.Location = new System.Drawing.Point(8, 120);
            this.pictureS.Name = "pictureS";
            this.pictureS.Size = new System.Drawing.Size(96, 48);
            this.pictureS.SizeMode = System.Windows.Forms.PictureBoxSizeMode.CenterImage;
            this.pictureS.TabIndex = 2;
            this.pictureS.TabStop = false;
            this.pictureS.Click += new System.EventHandler(this.picture_Click);
            // 
            // pictureW
            // 
            this.pictureW.Location = new System.Drawing.Point(8, 176);
            this.pictureW.Name = "pictureW";
            this.pictureW.Size = new System.Drawing.Size(96, 48);
            this.pictureW.SizeMode = System.Windows.Forms.PictureBoxSizeMode.CenterImage;
            this.pictureW.TabIndex = 3;
            this.pictureW.TabStop = false;
            this.pictureW.Click += new System.EventHandler(this.picture_Click);
            // 
            // buttonPlace
            // 
            this.buttonPlace.Appearance = System.Windows.Forms.Appearance.Button;
            this.buttonPlace.Checked = true;
            this.buttonPlace.Location = new System.Drawing.Point(8, 288);
            this.buttonPlace.Name = "buttonPlace";
            this.buttonPlace.Size = new System.Drawing.Size(48, 24);
            this.buttonPlace.TabIndex = 4;
            this.buttonPlace.TabStop = true;
            this.buttonPlace.Text = "追加";
            this.buttonPlace.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // buttonRemove
            // 
            this.buttonRemove.Appearance = System.Windows.Forms.Appearance.Button;
            this.buttonRemove.Location = new System.Drawing.Point(56, 288);
            this.buttonRemove.Name = "buttonRemove";
            this.buttonRemove.Size = new System.Drawing.Size(48, 24);
            this.buttonRemove.TabIndex = 5;
            this.buttonRemove.Text = "削除";
            this.buttonRemove.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // comboBoxSpeed
            // 
            this.comboBoxSpeed.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxSpeed.Location = new System.Drawing.Point(32, 248);
            this.comboBoxSpeed.Name = "comboBoxSpeed";
            this.comboBoxSpeed.Size = new System.Drawing.Size(72, 20);
            this.comboBoxSpeed.TabIndex = 6;
            this.comboBoxSpeed.SelectedIndexChanged += new System.EventHandler(this.comboBoxID_SelectedIndexChanged);
            // 
            // labelSpeed
            // 
            this.labelSpeed.Location = new System.Drawing.Point(6, 239);
            this.labelSpeed.Name = "labelSpeed";
            this.labelSpeed.Size = new System.Drawing.Size(24, 37);
            this.labelSpeed.TabIndex = 9;
            this.labelSpeed.Text = "速度";
            this.labelSpeed.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // SpeedLimitSlopeController
            // 
            this.AutoScaleBaseSize = new System.Drawing.Size(5, 12);
            this.ClientSize = new System.Drawing.Size(112, 325);
            this.Controls.Add(this.labelSpeed);
            this.Controls.Add(this.comboBoxSpeed);
            this.Controls.Add(this.buttonRemove);
            this.Controls.Add(this.buttonPlace);
            this.Controls.Add(this.pictureW);
            this.Controls.Add(this.pictureS);
            this.Controls.Add(this.pictureE);
            this.Controls.Add(this.pictureN);
            this.Name = "SpeedLimitSlopeController";
            this.Text = "速度制限（勾配）";
            this.DoubleClick += new System.EventHandler(this.BlockSystemSlopeController_DoubleClick);
            ((System.ComponentModel.ISupportInitialize)(this.pictureN)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.pictureE)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.pictureS)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.pictureW)).EndInit();
            this.ResumeLayout(false);

		}
		#endregion

		/// <summary> Selected direction. </summary>
		private Direction direction;



		/// <summary>
		/// construction mode. Are we placing a new one, or removing an existing one?
		/// </summary>
		private bool isPlacing { get { return buttonPlace.Checked; } }

		private void picture_Click(object sender, System.EventArgs e) {
			update( pictureN, sender );
			update( pictureS, sender );
			update( pictureW, sender );
			update( pictureE, sender );
		}

		private void update( PictureBox picBox, object sender ) {
			if(picBox==sender) {
				picBox.BorderStyle = BorderStyle.Fixed3D;
				direction = (Direction)picBox.Tag;
			} else {
				picBox.BorderStyle = BorderStyle.None;
			}
		}


		/// <summary>
		/// 
		/// </summary>
		public override void onRightClick( MapViewWindow source, Location loc, Point ab ) {
		}

		/// <summary>
		/// 
		/// </summary>
		public override void onClick( MapViewWindow source, Location loc, Point ab ) {
			if(isPlacing) {
				//速度制限追加
				if(SpeedLimitManager.isNotOnlyTrafficVoxel(getSlopeLocation(loc,direction))){
					MainWindow.showError("設定できない場所です。");
					return;
				}
                SpeedLimit sp = SpeedLimit.getInstance();
                if (sp.Contains(getSlopeLocation(loc,direction)))
                {
                    MainWindow.showError("既に設定されています。");
                    return;
                }
                switch (this.comboBoxSpeed.Text)
                {
                    case "低速":
                        sp.addLowSpeedLimit(getSlopeLocation(loc, direction));
                        break;
                    case "中速":
                        sp.addMediumSpeedLimit(getSlopeLocation(loc, direction));
                        break;
                    case "高速":
                        sp.addFastSpeedLimit(getSlopeLocation(loc, direction));
                        break;
                }
			} 
			else {
				//速度制限削除
				SlopeRailRoad srr = SlopeRailRoad.get(loc);
				if(srr==null) {
					loc.z++;
					srr = SlopeRailRoad.get(loc);
				}
				if(srr!=null) {
					if(srr.level>=2) {
						loc.z--;
					}
					for(int i=0; i<srr.level; i++) {
						loc -= direction;
					}

					// removing
                    SpeedLimit sp = SpeedLimit.getInstance();
					sp.removeLowSpeedLimit(getSlopeLocation(loc,direction));
                    sp.removeMediumSpeedLimit(getSlopeLocation(loc, direction));
	                sp.removeFastSpeedLimit(getSlopeLocation(loc,direction));
					return;

				}
				MainWindow.showError("削除できません");
			}
		}

		/// <summary>
		/// スロープのロケーションを取得
		/// </summary>
		private ArrayList getSlopeLocation( Location _base, Direction dir ) {			
			ArrayList list = new ArrayList();
			for( int i=0; i<4; i++ ) {
				Location loc = new Location(_base.x, _base.y, _base.z+(i/2));
				list.Add(loc);
				_base += dir;
			}
			return list;
		}



		/// <summary>
		/// 
		/// </summary>
		public override LocationDisambiguator disambiguator { get { return this; } }

		
		/// <summary>
		/// LocationDisambiguator implementation.
		/// Use the base of the slope to disambiguate.
		/// </summary>
		/// <param name="loc"></param>
		/// <returns></returns>
		public bool isSelectable( Location loc ) {
			if(!isPlacing) {
				SlopeRailRoad rr = SlopeRailRoad.get(loc);
				if(rr!=null && rr.level<2)
						return true;
				
				loc.z++;
				rr = SlopeRailRoad.get(loc);
				if(rr!=null && rr.level>=2)
						return true;

				return false;
			} else {
				// it is always allowed to place it on or under ground 
				if( World.world.getGroundLevel(loc)>=loc.z )
					return true;

				// if the new rail road is at the edge of existing rail,
				// allow.
				RailRoad rr = RailRoad.get(loc+direction.opposite);
				if( rr!=null && rr.hasRail(direction))
					return true;

				for( int i=0; i<4; i++ )
					loc += direction;
				loc.z++;

				// run the same test to the other end
				rr = RailRoad.get(loc);
				if( rr!=null && rr.hasRail(direction.opposite))
					return true;

				return false;
			}
		}

		/// <summary>
		/// 
		/// </summary>
		private Location lastMouse;

		/// <summary>
		/// 
		/// </summary>
		public override void onMouseMove( MapViewWindow source, Location loc, Point ab ) 
		{
			if(lastMouse!=loc) {
				// update the image
				invalidateScreen();
				lastMouse = loc;
				invalidateScreen();
			}
		}

		/// <summary>
		/// 
		/// </summary>
		private void invalidateScreen() 
		{
			Location loc2 = lastMouse;
			loc2.x += direction.offsetX*3;
			loc2.y += direction.offsetY*3;
			loc2.z ++;

			World.world.onVoxelUpdated( Cube.createInclusive(lastMouse,loc2) );
		}

		/// <summary>
		/// 
		/// </summary>
		public void drawBefore( QuarterViewDrawer view, DrawContextEx surface ) {}

		/// <summary>
		/// 
		/// </summary>
		public void drawVoxel( QuarterViewDrawer view, DrawContextEx canvas, Location loc, Point pt ) {
	

            //速度制限区間であればアイコン表示する
            Surface display = canvas.surface;
            SpeedLimit sl = SpeedLimit.getInstance();
            if (sl != null)
            {
                if (sl.LowSpeedLimit.Contains(loc))
                {
                    display.blt(pt, SpeedLimitManager.lowSpeedIcon);
                }
                if (sl.MediumSpeedLimit.Contains(loc))
                {
                    display.blt(pt, SpeedLimitManager.mediumSpeedIcon);
                }
                if (sl.FastSpeedLimit.Contains(loc))
                {
                    display.blt(pt, SpeedLimitManager.fastSpeedIcon);
                }
            }
		}

		/// <summary>
		/// 
		/// </summary>
		public void drawAfter( QuarterViewDrawer view, DrawContextEx dc ) {
			if(!isPlacing)		return;
			Location loc = lastMouse;
			if(loc==world.Location.UNPLACED) return;
			if(!SlopeRailRoad.canCreateSlope(loc,direction))	return;

			Surface canvas = dc.surface;

			int Z = loc.z;
			for( int i=0; i<4; i++ ) {
				if(i==2)	loc.z++;

				for( int j=World.world.getGroundLevel(loc); j<Z; j++ )
					// TODO: ground level handling
					BridgePierVoxel.defaultSprite.drawAlpha(
						canvas, view.fromXYZToClient(loc.x,loc.y,j) );

				RailPattern.getSlope(direction,i).drawAlpha(
					canvas, view.fromXYZToClient(loc) );
				loc += direction;
			}

		}

		/// <summary>
		/// コンボボックスの速度制限のリストを設定
		/// </summary>
		private void setSpeedList() {
            ArrayList list = new ArrayList();
            list.Add("低速");
            list.Add("中速");
            list.Add("高速");
            this.comboBoxSpeed.DataSource = list;
		}

		/// <summary>
		/// 
		/// </summary>
		private void comboBoxID_SelectedIndexChanged(object sender, System.EventArgs e) {
		}


		/// <summary>
		/// 
		/// </summary>
		private void BlockSystemSlopeController_DoubleClick(object sender, System.EventArgs e) {
		}
	}
}
