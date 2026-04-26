using System;
using System.Diagnostics;
using System.Drawing;
using System.ComponentModel;
using System.Windows.Forms;
using freetrain.framework;	/* MainWindow */
using freetrain.contributions.land;
using freetrain.contributions.common;
using freetrain.util;	/* Keyboard */
using freetrain.views;
using freetrain.world;
using freetrain.world.land;
using freetrain.world.terrain;
using freetrain.DirectXWrapper;


namespace freetrain.controllers.terrain
{
	public class Mountain2Controller : ControllerHostForm
	{
		public static void create() {
			if(theInstance == null)
				theInstance = new Mountain2Controller();			
			theInstance.Show();
			theInstance.Activate();
		}

		private static Mountain2Controller theInstance;

		protected override void OnClosing(CancelEventArgs e) {
			base.OnClosing(e);
			theInstance = null;
		}

		protected Mountain2Controller() {
			InitializeComponent();
			updatePreview();
			this.currentController = new Logic(this);
		}
		
		public override void updatePreview() {}

		protected override void Dispose(bool disposing) {
			if(disposing && components != null) {
				components.Dispose();
			}
			base.Dispose(disposing);
		}

		private System.Windows.Forms.PictureBox preview;
		private System.ComponentModel.IContainer components = null;
		private System.Windows.Forms.RadioButton buttonUp;
		private System.Windows.Forms.RadioButton buttonDown;

		private void InitializeComponent()
		{
			this.buttonUp = new System.Windows.Forms.RadioButton();
			this.buttonDown = new System.Windows.Forms.RadioButton();
			this.preview = new System.Windows.Forms.PictureBox();
			this.SuspendLayout();
			// 
			// buttonUp
			// 
			this.buttonUp.Appearance = System.Windows.Forms.Appearance.Button;
			this.buttonUp.Checked = true;
			this.buttonUp.Location = new System.Drawing.Point(4, 96);
			this.buttonUp.Name = "buttonUp";
			this.buttonUp.Size = new System.Drawing.Size(56, 24);
			this.buttonUp.TabIndex = 2;
			this.buttonUp.TabStop = true;
			this.buttonUp.Text = "隆起";
			this.buttonUp.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
			// 
			// buttonDown
			// 
			this.buttonDown.Appearance = System.Windows.Forms.Appearance.Button;
			this.buttonDown.Location = new System.Drawing.Point(60, 96);
			this.buttonDown.Name = "buttonDown";
			this.buttonDown.Size = new System.Drawing.Size(56, 24);
			this.buttonDown.TabIndex = 4;
			this.buttonDown.Text = "掘削";
			this.buttonDown.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
			// 
			// preview
			// 
			this.preview.BackColor = System.Drawing.Color.White;
			this.preview.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
			this.preview.Location = new System.Drawing.Point(4, 8);
			this.preview.Name = "preview";
			this.preview.Size = new System.Drawing.Size(112, 80);
			this.preview.SizeMode = System.Windows.Forms.PictureBoxSizeMode.CenterImage;
			this.preview.TabIndex = 3;
			this.preview.TabStop = false;
			// 
			// Mountain2Controller
			// 
			this.AutoScaleBaseSize = new System.Drawing.Size(5, 12);
			this.ClientSize = new System.Drawing.Size(120, 123);
			this.Controls.AddRange(new System.Windows.Forms.Control[] {
																		  this.buttonUp,
																		  this.buttonDown,
																		  this.preview});
			this.Name = "Mountain2Controller";
			this.Text = "地形操作２";
			this.ResumeLayout(false);
		}
	
		private bool isUpper{get {return this.buttonUp.Checked; }}
	
		private class Logic : RectSelectorController, MapOverlay
		{
			protected readonly Mountain2Controller owner;
		
			internal Logic(Mountain2Controller owner) : base(owner.siteImpl) {
				this.owner = owner;
			}
		
			protected override void onRectSelected(Location loc1, Location loc2) {
				/* 異常地形を生成していないか検査 */
				if(Keyboard.isControlKeyPressed) {
					World w = World.world;
					for(int x = loc1.x; x <= loc2.x; x++) {
						for(int y = loc1.y; y <= loc2.y; y++) {
							Location loc = new Location(x, y, loc1.z);
							if(!isFourAdjacentCornersMatched(loc)) {
								fixIllegalVoxel(loc);
								continue;
							}
							if(w.isOutsideWorld(loc))
								continue;
						}	/* for */
					}	/* for */
					return;
				}

				if(owner.isUpper)
					upper(loc1, loc2);
				else
					lower(loc1, loc2);
				World.world.onVoxelUpdated(new Cube(loc1, loc2.x - loc1.x + 1,
					loc2.y - loc1.y + 1, 1));
			}
		
			protected override void onRectUpdated(Location loc1, Location loc2) {}

			public void drawBefore(QuarterViewDrawer view, DrawContextEx surface) {}
			
			public void drawVoxel(QuarterViewDrawer view, DrawContextEx canvas, Location loc, Point pt) {
				if(loc.z != anchor.z) return;
				if(anchor != UNPLACED && loc.inBetween(anchor, currentLoc))
					LandPropertyVoxel.sprite.drawAlpha(canvas.surface, pt);
			}
			
			public void drawAfter(QuarterViewDrawer view, DrawContextEx surface) {}
		}

		private static void fixIllegalVoxel(Location loc)
		{
			World w = World.world;
			Direction d = Direction.NORTHEAST;
			
			/* 不適な角を探して修正 */
			for(int i = 0; i < 4; i++) {
				if(!MountainVoxel.isCornerMatched(loc, d)) {
					Location l = new Location(loc.x,loc.y, w.getGroundLevel(loc.x, loc.y));
					Voxel v = w[l];
					MountainVoxel mv;

					if(v != null) {
						mv = v as MountainVoxel;
						if(mv == null) {
							/* 障害物あり。修正不可 */
							MainWindow.showError("[" + l.x + ":" + l.y + "] - 障害物があります");
							return;
						}
					}
					else {
						mv = new MountainVoxel(l, 0, 0, 0, 0);
					}
			
					/* 正しい高さを取得.
					 * ただし他の3ボクセル全て同じ値でなければ修正不可 */
					int h2 = MountainVoxel.getTotalHeight(l + d, d.opposite);
					int h3 = MountainVoxel.getTotalHeight(l + d.left, d.right90);
					int h4 = MountainVoxel.getTotalHeight(l + d.right, d.left90);
					int r = h2;
					if(r == -1) r = h3;
					if(h3 != -1 && h3 != r) { 
//						MainWindow.showError("[" + l.x + ":" + l.y + "] - 高さの不揃いな角があります");
						return;
					}
					if(r == -1) r = h4;
					if(h4 != -1 && h4 != r) {
//						MainWindow.showError("[" + l.x + ":" + l.y + "] - 高さの不揃いな角があります");
						return;
					}
			
					int setup_h;
					if(r % 4 == 0) {
						if((r / 4) == l.z)
							setup_h = 0;
						else
							setup_h = 4;
					}
					else {
						setup_h = r % 4;
					}
			
					mv.setHeight(d, setup_h);
			
					if(mv.isSaturated) {
						w.raiseGround(l);
						w.remove(l);
					} else
					if(mv.isFlattened) {
						w.remove(mv);
					}
				}
				d = d.right90;
			}	/* for */

			MainWindow.mainWindow.statusText = "[" + loc.x + ":" + loc.y + "] - 修正しました";
			return;
		}

		/* 指定ボクセルの4角について, 
		 * 高さ情報が(その角の接する他のボクセルについて)揃っているか
		 * チェックする */
		private static bool isFourAdjacentCornersMatched(Location loc) {
			Direction d = Direction.NORTH;
			loc += Direction.SOUTH;

			/* isCornerMatched()
			 *  - 指定ボクセルの指定角について,その高さ情報が
			 *    当該角に接する他の3ボクセルについても同じ値かチェックする */
			for(int i = 0; i < 4; i++) {
				if(!MountainVoxel.isCornerMatched(loc, d.left)) {
					return false;
				}

				loc += d;
				d = d.right90;
			}

			return true;
		}

		private class LookupTable {
			public int x, y;
			public Direction d;
			public LookupTable(int _x, int _y, Direction _d) {
				x = _x; y = _y; d = _d;
			}
			static public LookupTable[] CreateLookupTable() {
				return new LookupTable[] {
					new LookupTable(0, 0, Direction.get(3)),
					new LookupTable(0, 1, Direction.get(1)),
					new LookupTable(1, 0, Direction.get(5)),
					new LookupTable(1, 1, Direction.get(7)),
					new LookupTable(0, 0, null),
				};
			}
		}
		
		public static void upper(Location loc1, Location loc2)
		{
			if(loc2.x - loc1.x < 1 || loc2.y - loc1.y < 1) {
				return;
			}

			World w = World.world;
			if(loc1.z == w.size.z - 1) {
				return;
			}

			bool isShift = Keyboard.isShiftKeyPressed;
//Debug.WriteLine("isShift:" + isShift);
			LookupTable[] lookuptable = LookupTable.CreateLookupTable();

//Debug.WriteLine(loc1.x + ":" + loc1.y + "-" + loc2.x + ":" + loc2.y);
			/* 範囲に含まれる各ボクセルについてループ処理 */
			for(int x = loc1.x; x < loc2.x; x++) {
				for(int y = loc1.y; y < loc2.y; y++) {
//Debug.WriteLine(x + ":" + y);
					Location loc = new Location(x, y, loc1.z);

					if(!isFourAdjacentCornersMatched(loc)) 
						continue;

					if(w.isOutsideWorld(loc))
						continue;

					/* そのボクセルの南東角(視覚上で右角)を
					 * 共有する4ボクセルについてループ処理.
					 * 隆起可能かどうか判定する */
					bool nearRoof = (w.getGroundLevel(loc) == w.size.z - 1);
					bool isOk = true;
					for(int i = 0; isOk == true && lookuptable[i].d != null; i++) {
						Location loc3 = new Location(x + lookuptable[i].x,
						                          y + lookuptable[i].y,
						                          loc.z);

						if(w.isOutsideWorld(loc3))
							continue;

						if(w.getGroundLevel(loc3) != loc1.z) {
							isOk = false;
							break;
						}
	
						Voxel v = w[loc3];
						if(v == null)
							continue;	/* 障害物無し */
	
						if(v is MountainVoxel) {
							int h = ((MountainVoxel)v).getHeight(lookuptable[i].d);
							if(h == 4 || (nearRoof && h == 3)) {
								isOk = false;
								break;
							}
							continue;
						}

						isOk = false;	/* 障害物有り */
						break;
					} /* for */

//Debug.WriteLine("isOk:" + isOk + " / isShift:" + isShift);
					if(isOk) {
						int setup_level = 4;	/* 設定する高さ */

						if(isShift) {
							/* そのボクセルの各角について調べる
							 *  3つ以上の角が2未満 - 南東角の高さを2にする
							 *  2つ以上の角が2以上 - 南東角の高さを4にする
							 *  それ以外 - そのままにする
							 */
							bool less2_flg = false;
							bool more2_flg = false;
							Voxel target_vx = w[loc];
							MountainVoxel target_mv = target_vx as MountainVoxel;
							if(target_mv == null) {
								less2_flg = true;
							}
							else {
								int f = 0;
								for(int i = 0; lookuptable[i].d != null; i++) {
									if(target_mv.getHeight(lookuptable[i].d) >= 2) {
										f++;
									}
								}	/* for */
								more2_flg = (f >= 2) ? true : false;
								less2_flg = (f < 2) ? true : false;
							}
//Debug.WriteLine("setup_level:" + setup_level);
						/* 東または南ボクセルが以下のいずれかの場合, 南東角の高さを2にする
						 *  - 高さレベル(GroundLevel)が低い
						 *  - 障害物がある
						 *  - 当該ボクセルがx軸及びy軸のいずれかの端
						 */

							/* 東ボクセル */
							if(
								(w.getGroundLevel(loc.x + 2, loc.y) < loc1.z) ||
								(
									w[loc.x + 2, loc.y, loc.z] != null &&
									!(w[loc.x + 2, loc.y, loc.z] is MountainVoxel)
								) ||
								(loc.x == (loc2.x - 1))
							) {
								less2_flg = true;
							}
							else
							/* 南ボクセル */
							if(
								(w.getGroundLevel(loc.x, loc.y + 2) < loc1.z) ||
								(
									w[loc.x, loc.y + 2, loc.z] != null &&
									!(w[loc.x, loc.y + 2, loc.z] is MountainVoxel)
								) ||
								(loc.y == (loc2.y - 1))
							) {
								less2_flg = true;
							}

						setup_level = (less2_flg ? 2 : more2_flg ? 4 : 0);
//Debug.WriteLine("setup_level[2]:" + setup_level);
						}

						if(setup_level != 0) {
							/* そのボクセルの南東角(視覚上で右角)を
							 * 共有する4ボクセルについてループ処理 */
							for(int i = 0; lookuptable[i].d != null; i++) {
								Location loc3 = new Location(
							                             x + lookuptable[i].x,
							                             y + lookuptable[i].y,
							                             loc1.z);
								Voxel vx = w[loc3];
								if(vx is World.OutOfWorldVoxel)
									continue;

								MountainVoxel v = vx as MountainVoxel;
								if(v == null)
									v = new MountainVoxel(loc3, 0, 0, 0, 0);

								if(v.getHeight(lookuptable[i].d) != setup_level)
									v.setHeight(lookuptable[i].d, setup_level);

								if(v.isSaturated) {
									w.raiseGround(loc3);
									w.remove(loc3);
								}
							}	/* for */
						}
					}
				}	/* for */		
			}	/* for */
		}	/* upper() */

		public static void lower(Location loc1, Location loc2) {
			Debug.Assert(loc1.z == loc2.z);

			if(loc2.x - loc1.x < 1 || loc2.y - loc1.y < 1)
				return;
			if(loc1.z == 0)
				return;

			World w = World.world;
			LookupTable[] lookuptable = LookupTable.CreateLookupTable();

			/* 範囲に含まれる各ボクセルについてループ処理 */
			for(int x = loc1.x; x < loc2.x; x++) {
				for(int y = loc1.y; y < loc2.y; y++) {
					Location loc = new Location(x, y, loc1.z);

					if(!isFourAdjacentCornersMatched(loc))
						continue;

					if(w.isOutsideWorld(loc))
						continue;

					MountainVoxel mvBase = MountainVoxel.get(loc);
					if(mvBase != null)
						continue;
					else {
						int glevel = w.getGroundLevel(loc);
						if(glevel != loc1.z && glevel != loc1.z - 1)
							continue;

						loc.z--;
					}

					/* そのボクセルの南東角(視覚上で右角)を
					 * 共有する4ボクセルについてループ処理.
					 * 掘削可能かどうか判定する */
					bool isOk = true;
					for(int i = 0; isOk == true && lookuptable[i].d != null; i++) {
						Location loc3 = new Location(
						                          x + lookuptable[i].x,
						                          y + lookuptable[i].y,
						                          loc.z);

						/* 山ボクセルか？
						 * [NOTE]フラットレベルのボクセルは山ボクセルではない */
						if(MountainVoxel.get(loc3) != null)
							continue;

						/* そのボクセル、または1段上のボクセルに
						 * 障害物があるか？ */
						if(w[loc3.x, loc3.y, loc3.z + 1] != null) {
							isOk = false;
							break;
						}
						if(w[loc3.x, loc3.y, loc3.z] != null) {
							isOk = false;
							break;
						}
					}	/* for */

					if(isOk) {
						/* そのボクセルの南東角(視覚上で右角)を
						 * 共有する4ボクセルについてループ処理 */
						for(int i = 0; lookuptable[i].d != null; i++) {
							Location loc3 = new Location(
							                             x + lookuptable[i].x,
							                             y + lookuptable[i].y,
							                             loc.z);

							MountainVoxel mv = MountainVoxel.get(loc3);
							if(mv == null) {
								if(loc.z >= w.getGroundLevel(loc3)) 
									continue;
								w.lowerGround(loc3);
								mv = new MountainVoxel(loc3, 4, 4, 4, 4);
							}
							mv.setHeight(lookuptable[i].d, 0);
		
							if(mv.isFlattened)
								w.remove(mv);
						}	/* for */
					}
				}	/* for */
			}	/* for */
		}	/* lower() */
	}	/* class Mountain2Controller */

}	/* namespace freetrain.controllers.terrain */
