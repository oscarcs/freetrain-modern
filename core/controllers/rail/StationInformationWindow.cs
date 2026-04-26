using System;
using System.Diagnostics;
using System.Drawing;
using System.Collections;
using System.ComponentModel;
using System.Windows.Forms;
using freetrain.world;
using freetrain.world.rail;

namespace freetrain.controllers.rail
{
	/// <summary>
	/// Property dialog of a station
	/// </summary>
	public class StationInformationWindow : Form
	{
		#region Windows Form Designer generated code

        private System.Windows.Forms.Label label1;
		private System.Windows.Forms.Label label2;
		private System.Windows.Forms.Label label3;
		private System.Windows.Forms.Label label_trains;
		private System.Windows.Forms.Label label_waiting;
		private System.Windows.Forms.Label label5;
		private System.Windows.Forms.Label label_unloaded;
        private Label label_loaded;
        private Label label6;
        private Label nameBox;
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
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.label_unloaded = new System.Windows.Forms.Label();
            this.label_trains = new System.Windows.Forms.Label();
            this.label_waiting = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.label_loaded = new System.Windows.Forms.Label();
            this.label6 = new System.Windows.Forms.Label();
            this.nameBox = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.Location = new System.Drawing.Point(16, 0);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(48, 16);
            this.label1.TabIndex = 1;
            this.label1.Text = "名前:";
            this.label1.TextAlign = System.Drawing.ContentAlignment.BottomRight;
            // 
            // label2
            // 
            this.label2.Location = new System.Drawing.Point(64, 72);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(128, 16);
            this.label2.TabIndex = 6;
            this.label2.Text = "降車客数（今日/昨日)：";
            this.label2.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // label3
            // 
            this.label3.Location = new System.Drawing.Point(64, 96);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(128, 16);
            this.label3.TabIndex = 6;
            this.label3.Text = "発着数（今日/昨日)：";
            this.label3.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // label_unloaded
            // 
            this.label_unloaded.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.label_unloaded.Location = new System.Drawing.Point(192, 72);
            this.label_unloaded.Name = "label_unloaded";
            this.label_unloaded.Size = new System.Drawing.Size(88, 16);
            this.label_unloaded.TabIndex = 7;
            this.label_unloaded.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // label_trains
            // 
            this.label_trains.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.label_trains.Location = new System.Drawing.Point(192, 96);
            this.label_trains.Name = "label_trains";
            this.label_trains.Size = new System.Drawing.Size(88, 16);
            this.label_trains.TabIndex = 7;
            this.label_trains.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // label_waiting
            // 
            this.label_waiting.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.label_waiting.Location = new System.Drawing.Point(192, 24);
            this.label_waiting.Name = "label_waiting";
            this.label_waiting.Size = new System.Drawing.Size(88, 16);
            this.label_waiting.TabIndex = 7;
            this.label_waiting.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // label5
            // 
            this.label5.Location = new System.Drawing.Point(18, 24);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(174, 16);
            this.label5.TabIndex = 6;
            this.label5.Text = "乗車待ち客数（実数/需要値)：";
            this.label5.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // label_loaded
            // 
            this.label_loaded.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.label_loaded.Location = new System.Drawing.Point(192, 48);
            this.label_loaded.Name = "label_loaded";
            this.label_loaded.Size = new System.Drawing.Size(88, 16);
            this.label_loaded.TabIndex = 9;
            this.label_loaded.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // label6
            // 
            this.label6.Location = new System.Drawing.Point(64, 48);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(128, 16);
            this.label6.TabIndex = 8;
            this.label6.Text = "乗車客数（今日/昨日)：";
            this.label6.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // nameBox
            // 
            this.nameBox.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.nameBox.Location = new System.Drawing.Point(70, 2);
            this.nameBox.Name = "nameBox";
            this.nameBox.Size = new System.Drawing.Size(210, 16);
            this.nameBox.TabIndex = 10;
            this.nameBox.Text = "駅名";
            this.nameBox.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // StationInformationDialog
            // 
            this.AutoScaleBaseSize = new System.Drawing.Size(5, 12);
            this.ClientSize = new System.Drawing.Size(296, 118);
            this.Controls.Add(this.nameBox);
            this.Controls.Add(this.label_loaded);
            this.Controls.Add(this.label6);
            this.Controls.Add(this.label_unloaded);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.label_trains);
            this.Controls.Add(this.label_waiting);
            this.Controls.Add(this.label5);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "StationInformationDialog";
            this.ShowInTaskbar = false;
            this.Text = "駅情報";
            this.TopMost = true;
            this.ResumeLayout(false);

		}
		#endregion

		public StationInformationWindow( Station st ) {
			this.station = st;

			InitializeComponent();
            onUpdate();

            station.onStationChange += new StationCounterListener(onUpdate);
          
		}

		/// <summary> Station object to which this dialog is opened for. </summary>
		private Station station;

		/// <summary>
		/// 使用されているリソースに後処理を実行します。
		/// </summary>
		protected override void Dispose( bool disposing ) {
            station.onStationChange -= new StationCounterListener(onUpdate);
			if( disposing && components != null)
				components.Dispose();
			base.Dispose( disposing );
		}

        private void onUpdate(){
			// initialize the dialog
			nameBox.Text = station.name;
			label_loaded.Text = string.Format("{0} / {1}",station.LoadedToday,station.LoadedYesterday);
            label_unloaded.Text = string.Format("{0} / {1}", station.UnloadedToday, station.UnloadedYesterday);
            label_trains.Text = string.Format("{0} / {1}", station.TrainsToday, station.TrainsYesterday);
			label_waiting.Text = string.Format("{0} / {1}",station.WaitingPassengers, station.population);
        }

	}
}
