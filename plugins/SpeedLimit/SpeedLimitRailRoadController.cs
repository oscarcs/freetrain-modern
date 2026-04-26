using System;
using System.Diagnostics;
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
using freetrain.controllers;
using freetrain.controllers.rail;
using freetrain.DirectXWrapper;


namespace freetrain.world.rail.speedlimit
{
	/// <summary>
	/// 速度制限ダイアログ
	/// </summary>
	public class SpeedLimitRailRoadController :  AbstractControllerImpl, MapOverlay
	{
		#region Singleton instance management
		/// <summary>
		/// Creates a new controller window, or active the existing one.
		/// </summary>
		public static void create() {
			if(theInstance==null)
				theInstance = new SpeedLimitRailRoadController();
			theInstance.Show();
			theInstance.Activate();
		}

		private System.Windows.Forms.ComboBox comboBoxSpeed;
		private System.Windows.Forms.Label labelSpeed;

		private static SpeedLimitRailRoadController theInstance;

		protected override void OnClosing(System.ComponentModel.CancelEventArgs e) {
			base.OnClosing(e);
			theInstance = null;
		}
		#endregion
		

		/// <summary>
		/// コンストラクタ
		/// </summary>
		private SpeedLimitRailRoadController() 
		{
			InitializeComponent();
			updatePreview();

			setSpeedList();
		}


		/// <summary>
		/// 
		/// </summary>
		public override void updatePreview()
		{
			using( PreviewDrawer drawer = new PreviewDrawer( picture.Size, new Size(1,10), 0 ) ) 
			{
				for( int i=0; i<10; i++ )
					drawer.draw( RailPattern.get( Direction.NORTH, Direction.SOUTH ), 0, i );
				if(picture.Image!=null) picture.Image.Dispose();
				picture.Image = drawer.createBitmap();
			}
		}


		/// <summary>
		/// 
		/// </summary>
		protected override void Dispose( bool disposing ) 
		{
			if( disposing && components != null)
					components.Dispose();
			base.Dispose( disposing );
		}


		/// <summary>
		/// ダイアログボックス内のメッセージを更新する。　
		/// Updates the message in the dialog box.
		/// </summary>
		private void updateDialog() {
			message.Text = anchor!=UNPLACED?
				"終点を選んでください":"始点を選んでください";
		}


		/// <summary>
		/// ユーザーによって最初に選ばれた地点　
		/// The first location selected by the user.
		/// </summary>
		private Location anchor = UNPLACED;


		/// <summary>
		/// 現在のマウスポジション。　anchor!=UNPLACED のときのみ使用する。
		/// Current mouse position. Used only when anchor!=UNPLACED
		/// </summary>
		private Location currentPos = UNPLACED;


		/// <summary>
		/// 位置をもたないことを表す特殊な値
		/// </summary>
		private static Location UNPLACED = freetrain.world.Location.UNPLACED;


		/// <summary>
		/// 施設ボタンが押されてるかどうか
		/// </summary>
		private bool isPlacing { get { return buttonPlace.Checked; } }





		#region Windows Form Designer generated code
		/// <summary>
		/// 施設ボタン
		/// </summary>
		private System.Windows.Forms.RadioButton buttonPlace;
		/// <summary>
		/// 撤去ボタン
		/// </summary>
		private System.Windows.Forms.RadioButton buttonRemove;
		private System.Windows.Forms.Label message;
		private System.Windows.Forms.PictureBox picture;
		private System.ComponentModel.Container components = null;

		private void InitializeComponent()
		{
            this.picture = new System.Windows.Forms.PictureBox();
            this.message = new System.Windows.Forms.Label();
            this.buttonPlace = new System.Windows.Forms.RadioButton();
            this.buttonRemove = new System.Windows.Forms.RadioButton();
            this.comboBoxSpeed = new System.Windows.Forms.ComboBox();
            this.labelSpeed = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.picture)).BeginInit();
            this.SuspendLayout();
            // 
            // picture
            // 
            this.picture.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.picture.Location = new System.Drawing.Point(8, 8);
            this.picture.Name = "picture";
            this.picture.Size = new System.Drawing.Size(96, 72);
            this.picture.TabIndex = 0;
            this.picture.TabStop = false;
            // 
            // message
            // 
            this.message.Location = new System.Drawing.Point(8, 88);
            this.message.Name = "message";
            this.message.Size = new System.Drawing.Size(96, 24);
            this.message.TabIndex = 1;
            this.message.Text = "マップの２点をクリックして設定";
            // 
            // buttonPlace
            // 
            this.buttonPlace.Appearance = System.Windows.Forms.Appearance.Button;
            this.buttonPlace.Checked = true;
            this.buttonPlace.Location = new System.Drawing.Point(8, 168);
            this.buttonPlace.Name = "buttonPlace";
            this.buttonPlace.Size = new System.Drawing.Size(48, 24);
            this.buttonPlace.TabIndex = 2;
            this.buttonPlace.TabStop = true;
            this.buttonPlace.Text = "追加";
            this.buttonPlace.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.buttonPlace.CheckedChanged += new System.EventHandler(this.modeChanged);
            // 
            // buttonRemove
            // 
            this.buttonRemove.Appearance = System.Windows.Forms.Appearance.Button;
            this.buttonRemove.Location = new System.Drawing.Point(56, 168);
            this.buttonRemove.Name = "buttonRemove";
            this.buttonRemove.Size = new System.Drawing.Size(48, 24);
            this.buttonRemove.TabIndex = 3;
            this.buttonRemove.Text = "削除";
            this.buttonRemove.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.buttonRemove.CheckedChanged += new System.EventHandler(this.modeChanged);
            // 
            // comboBoxSpeed
            // 
            this.comboBoxSpeed.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxSpeed.Location = new System.Drawing.Point(32, 128);
            this.comboBoxSpeed.Name = "comboBoxSpeed";
            this.comboBoxSpeed.Size = new System.Drawing.Size(72, 20);
            this.comboBoxSpeed.TabIndex = 5;
            // 
            // labelSpeed
            // 
            this.labelSpeed.Location = new System.Drawing.Point(6, 119);
            this.labelSpeed.Name = "labelSpeed";
            this.labelSpeed.Size = new System.Drawing.Size(24, 37);
            this.labelSpeed.TabIndex = 9;
            this.labelSpeed.Text = "速度";
            this.labelSpeed.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // SpeedLimitRailRoadController
            // 
            this.AutoScaleBaseSize = new System.Drawing.Size(5, 12);
            this.ClientSize = new System.Drawing.Size(112, 202);
            this.Controls.Add(this.labelSpeed);
            this.Controls.Add(this.comboBoxSpeed);
            this.Controls.Add(this.buttonPlace);
            this.Controls.Add(this.buttonRemove);
            this.Controls.Add(this.message);
            this.Controls.Add(this.picture);
            this.Name = "SpeedLimitRailRoadController";
            this.Text = "速度制限";
            this.Load += new System.EventHandler(this.BlockSystemRailRoadController_Load);
            this.Closed += new System.EventHandler(this.BlockSystemRailRoadController_Closed);
            ((System.ComponentModel.ISupportInitialize)(this.picture)).EndInit();
            this.ResumeLayout(false);

		}
		#endregion


		/// <summary>
		/// 
		/// </summary>
		public override void onClick( MapViewWindow source, Location loc, Point ab ) 
		{
			if(anchor==UNPLACED) 
			{
				anchor = loc;
				sameLevelDisambiguator = new SameLevelDisambiguator(anchor.z);
			} 
			else 
			{
				if(anchor!=loc) 
				{
					if(isPlacing)
					{
						//速度制限区間設定
						build( anchor, loc );

						//線路を消すため
						World.world.onAllVoxelUpdated();
					} 
					else
					{
						//速度制限区間削除
						remove( anchor, loc );

						//線路を消すため
						World.world.onAllVoxelUpdated();
					}
				}
				anchor = UNPLACED;
			}

			updateDialog();
		}

		/// <summary>
		/// 
		/// </summary>
		public override void onRightClick( MapViewWindow source, Location loc, Point ab ) 
		{
			// cancel the anchor
			if(anchor!=UNPLACED && currentPos!=UNPLACED)
			{
				World.world.onVoxelUpdated(Cube.createInclusive(anchor,currentPos));
			}
			anchor = UNPLACED;
			updateDialog();
			
		}


		/// <summary>
		/// 
		/// </summary>
		public override void onMouseMove( MapViewWindow view, Location loc, Point ab ) 
		{
			//線路を引く時
			if( anchor!=UNPLACED && isPlacing && currentPos!=loc ) {


				// update the screen
				if( currentPos!=UNPLACED ) {
					World.world.onVoxelUpdated(Cube.createInclusive(anchor,currentPos));
				}
				currentPos = loc;
				World.world.onVoxelUpdated(Cube.createInclusive(anchor,currentPos));
				
			}

			//撤去する時
			if( anchor!=UNPLACED && !isPlacing ) {
						
			}
		}


		/// <summary>
		/// 
		/// </summary>
		public override void onDetached() 
		{
			anchor = UNPLACED;
		}

		/// <summary>
		/// 
		/// </summary>
		public override LocationDisambiguator disambiguator 
		{
			get {
				// the 2nd selection must go to the same height as the anchor.
				if(anchor==UNPLACED)	return RailRoadDisambiguator.theInstance;
				else					return sameLevelDisambiguator;
			}
		}
		
		/// <summary>
		/// 
		/// </summary>
		private LocationDisambiguator sameLevelDisambiguator;


		/// <summary>
		/// 
		/// </summary>
		protected override void OnVisibleChanged(System.EventArgs e) 
		{
			updateDialog();
		}
		



		// "place" or "remove" button was clicked. reset the anchor
		/// <summary>
		/// 施設または撤去ボタンがクリックされた際、アンカーをリセットする。
		/// </summary>
		private void modeChanged(object sender, EventArgs e) 
		{
			anchor = UNPLACED;
			updateDialog();
		}


		/// <summary>
		/// 
		/// </summary>
		public void drawBefore( QuarterViewDrawer view, DrawContextEx canvas ) 
		{
			if( anchor!=UNPLACED && isPlacing ) 
			{
				canvas.tag = comupteRoute( anchor, currentPos );
				if( canvas.tag!=null )
					Debug.WriteLine( ((IDictionary)canvas.tag).Count );
			}
		}


		/// <summary>
		/// 
		/// </summary>
		public void drawVoxel( QuarterViewDrawer view, DrawContextEx canvas, Location loc, Point pt ) 
		{
			Surface display = canvas.surface; 

			//新しく設定する閉塞区間であれば描画する
			IDictionary dic = (IDictionary)canvas.tag;
			if( dic!=null ) {
				RailPattern rp = (RailPattern)dic[loc];
				if( rp!=null ) {
					for( int j=World.world.getGroundLevel(loc); j<loc.z; j++ )
						// 橋げたとか必要なもの
						BridgePierVoxel.defaultSprite.drawAlpha(
							canvas.surface,
							view.fromXYZToClient(loc.x,loc.y,j) );

					rp.drawAlpha( canvas.surface, pt ); 

					//アイコン表示
                    switch (this.comboBoxSpeed.Text)
                    {
                        case "低速":
                            display.blt(pt, SpeedLimitManager.lowSpeedIcon);
                            break;
                        case "中速":
                            display.blt(pt, SpeedLimitManager.mediumSpeedIcon);
                            break;
                        case "高速":
                            display.blt(pt, SpeedLimitManager.fastSpeedIcon);
                            break;
                    }
				}
			}

			//速度制限区間であればアイコン表示する
            SpeedLimit sl = SpeedLimit.getInstance();
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


		/// <summary>
		/// 
		/// </summary>
		public void drawAfter( QuarterViewDrawer view, DrawContextEx canvas ) 
		{
		}



		/// <summary>
		/// ２つの特定した地点間の速度制限区間を設定する。　
		/// </summary>
		private void build( Location here, Location there ) {

			Hashtable route = (Hashtable)comupteRoute(here, there);			
			if(route==null) {return;}
			if(SpeedLimitManager.isNotOnlyTrafficVoxel(route))
            {
				MainWindow.showError("設定できない場所です。");
				return;
			}
            SpeedLimit sp = SpeedLimit.getInstance();
            if (sp.Contains(route))
            {
                MainWindow.showError("既に設定されています。");
                return;
            }


			//速度制限区間を新規追加
            switch (this.comboBoxSpeed.Text)
            {
                case "低速":
                    sp.addLowSpeedLimit(route);
                    break;
                case "中速":
                    sp.addMediumSpeedLimit(route);
                    break;
                case "高速":
                    sp.addFastSpeedLimit(route);
                    break;
            }
            
		}


		/// <summary>
		/// ２つの特定した地点間の速度制限を削除する。　
		/// </summary>
		private void remove( Location here, Location there ) {
			
			Hashtable route = new Hashtable();
			route = (Hashtable)comupteRoute(here, there);
			
			if(route==null) {return;}

	
			
			//速度制限削除
            SpeedLimit sp = SpeedLimit.getInstance();
            sp.removeLowSpeedLimit(route);
            sp.removeMediumSpeedLimit(route);
            sp.removeFastSpeedLimit(route);
		}


		/// <summary>
		/// コンボボックスの速度のリストを設定
		/// </summary>
		private void setSpeedList() {
			ArrayList list = new ArrayList();
			list.Add("低速");
			list.Add("中速");
            list.Add("高速");
			this.comboBoxSpeed.DataSource = list;
		}


		/// <summary>
		/// ２地点間の速度制限のロケーションを計算
		/// 
		/// </summary>
		private static IDictionary comupteRoute( Location from, Location to ) {
			Hashtable route = new Hashtable();
			if(from==to) { return route; }

			Direction dir = null;

			for( Location loc = from; loc!=to; dir=loc.getDirectionTo(to).opposite, loc=loc.toward(to) ) {
				Direction dd = loc.getDirectionTo(to);
				route.Add( loc, RailPattern.get( dir!=null?dir:dd.opposite, dd ) );
			}

			route.Add( to, RailPattern.get( dir, dir.opposite ) );
			return route;
		}



		/// <summary>
		/// フォームを閉じる時
		/// </summary>
		private void BlockSystemRailRoadController_Closed(object sender, System.EventArgs e) {
			World.world.onAllVoxelUpdated();
		}

        /// <summary>
        /// フォームを開く時
        /// </summary>
        private void BlockSystemRailRoadController_Load(object sender, EventArgs e)
        {
            setSpeedList();
        }



	}

}
