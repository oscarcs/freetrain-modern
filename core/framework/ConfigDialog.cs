using System;
using System.Drawing;
using System.Collections;
using System.ComponentModel;
using System.Windows.Forms;
using freetrain.world;
using freetrain.DirectXWrapper;

namespace freetrain.framework
{
	/// <summary>
	/// ConfigDialog の概要の説明です。
	/// </summary>
	public class ConfigDialog : System.Windows.Forms.Form
	{
		private System.Windows.Forms.Button buttonOK;
		private System.Windows.Forms.Button buttonCancel;
		private System.Windows.Forms.GroupBox groupBox1;
		private System.Windows.Forms.RadioButton radioMsgBox;
		private System.Windows.Forms.RadioButton radioStatus;
		private System.Windows.Forms.Label label1;
		private System.Windows.Forms.TrackBar msgStatusLength;
		private System.Windows.Forms.CheckBox drawStationNames;
		private System.Windows.Forms.CheckBox showBoundingBox;
		private System.Windows.Forms.CheckBox hideTrees;
		private System.Windows.Forms.Label label2;
		private System.Windows.Forms.ToolTip toolTip;
		private System.Windows.Forms.ComboBox comboSurfaceAlloc;
        private TabControl optionTabControl;
        private TabPage tabPage1;
        private CheckBox drawElectlicPoles;
        private CheckBox drawTrainNames;
        private TabPage fontstabPage;
        private FontDialog fontDialogStation;
        private FontDialog fontDialogTrain;
        private Button fontselectTrain;
        private Button fontselectStation;
        private Label trainFontSample;
        private Label stationFontSample;

		private readonly GlobalOptions opts;
        private TabPage bunkruptTabPage;
        private Label label3;
        private CheckBox showMessageatBunkrupt;
        private RadioButton bunkruptplus3;
        private RadioButton bunkruptplus2;
        private RadioButton bunkruptplus1;
		private readonly FontOptions fntopt;
		
		public ConfigDialog() : this(Core.options, Core.fontoptions) {}

		public ConfigDialog( GlobalOptions opts, FontOptions fopt ) {
			this.opts = opts;
            this.fntopt = fopt;
			InitializeComponent();

            TypeConverter converter;
            converter = TypeDescriptor.GetConverter(typeof(Font));
            fopt.fontStationNames = (Font)(converter.ConvertFromString( opts.fontstringStationNames ));
            fopt.fontTrainNames = (Font)(converter.ConvertFromString( opts.fontstringTrainNames ));
            fopt.colorStationNames = Color.FromArgb( opts.colorvalueStationNames );
            fopt.colorTrainNames = Color.FromArgb( opts.colorvalueTrainNames );
          
            radioMsgBox.Checked = opts.showErrorMessageBox;
			radioStatus.Checked = !opts.showErrorMessageBox;
			msgStatusLength.Value = opts.messageDisplayTime;
			drawStationNames.Checked = opts.drawStationNames;
			drawTrainNames.Checked = opts.drawTrainNames;
            drawElectlicPoles.Checked = opts.drawElectlicPoles;
			showBoundingBox.Checked = opts.drawBoundingBox;
			hideTrees.Checked = opts.hideTrees;
            stationFontSample.ForeColor = fopt.colorStationNames;
            stationFontSample.Font = fopt.fontStationNames;
            trainFontSample.ForeColor = fopt.colorTrainNames;
            trainFontSample.Font = fopt.fontTrainNames;
            fontDialogStation.Color = fopt.colorStationNames;
            fontDialogStation.Font = fopt.fontStationNames;
            fontDialogTrain.Color = fopt.colorTrainNames;
            fontDialogTrain.Font = fopt.fontTrainNames;
			comboSurfaceAlloc.SelectedIndex = (int)opts.SurfaceAlloc;

            showMessageatBunkrupt.Checked = opts.bunkruptMessageFlag;

            if( opts.liquidPlusAtBunkrupt == 10000000000 ) bunkruptplus1.Checked = true;
            if( opts.liquidPlusAtBunkrupt == 100000000000 ) bunkruptplus2.Checked = true;
            if( opts.liquidPlusAtBunkrupt == 1000000000000 ) bunkruptplus3.Checked = true;
		}

		protected override void Dispose( bool disposing ) {
			if( disposing && components != null)
				components.Dispose();
			base.Dispose( disposing );
		}

		private System.ComponentModel.IContainer components;

		#region Windows Form Designer generated code

		/// <summary>
		/// デザイナ サポートに必要なメソッドです。このメソッドの内容を
		/// コード エディタで変更しないでください。
		/// </summary>
		private void InitializeComponent()
		{
            this.components = new System.ComponentModel.Container();
            this.buttonOK = new System.Windows.Forms.Button();
            this.buttonCancel = new System.Windows.Forms.Button();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.label1 = new System.Windows.Forms.Label();
            this.msgStatusLength = new System.Windows.Forms.TrackBar();
            this.radioStatus = new System.Windows.Forms.RadioButton();
            this.radioMsgBox = new System.Windows.Forms.RadioButton();
            this.drawStationNames = new System.Windows.Forms.CheckBox();
            this.showBoundingBox = new System.Windows.Forms.CheckBox();
            this.hideTrees = new System.Windows.Forms.CheckBox();
            this.comboSurfaceAlloc = new System.Windows.Forms.ComboBox();
            this.label2 = new System.Windows.Forms.Label();
            this.toolTip = new System.Windows.Forms.ToolTip(this.components);
            this.optionTabControl = new System.Windows.Forms.TabControl();
            this.tabPage1 = new System.Windows.Forms.TabPage();
            this.bunkruptTabPage = new System.Windows.Forms.TabPage();
            this.drawTrainNames = new System.Windows.Forms.CheckBox();
            this.drawElectlicPoles = new System.Windows.Forms.CheckBox();
            this.fontstabPage = new System.Windows.Forms.TabPage();
            this.trainFontSample = new System.Windows.Forms.Label();
            this.stationFontSample = new System.Windows.Forms.Label();
            this.fontselectTrain = new System.Windows.Forms.Button();
            this.fontselectStation = new System.Windows.Forms.Button();
            this.fontDialogStation = new System.Windows.Forms.FontDialog();
            this.fontDialogTrain = new System.Windows.Forms.FontDialog();
            this.showMessageatBunkrupt = new System.Windows.Forms.CheckBox();
            this.label3 = new System.Windows.Forms.Label();
            this.bunkruptplus1 = new System.Windows.Forms.RadioButton();
            this.bunkruptplus2 = new System.Windows.Forms.RadioButton();
            this.bunkruptplus3 = new System.Windows.Forms.RadioButton();
            this.groupBox1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.msgStatusLength)).BeginInit();
            this.optionTabControl.SuspendLayout();
            this.tabPage1.SuspendLayout();
            this.bunkruptTabPage.SuspendLayout();
            this.fontstabPage.SuspendLayout();
            this.SuspendLayout();
            // 
            // buttonOK
            // 
            this.buttonOK.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.buttonOK.Location = new System.Drawing.Point(247, 242);
            this.buttonOK.Name = "buttonOK";
            this.buttonOK.Size = new System.Drawing.Size(80, 24);
            this.buttonOK.TabIndex = 0;
            this.buttonOK.Text = "&OK";
            this.buttonOK.Click += new System.EventHandler(this.buttonOK_Click);
            // 
            // buttonCancel
            // 
            this.buttonCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.buttonCancel.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.buttonCancel.Location = new System.Drawing.Point(332, 242);
            this.buttonCancel.Name = "buttonCancel";
            this.buttonCancel.Size = new System.Drawing.Size(80, 24);
            this.buttonCancel.TabIndex = 1;
            this.buttonCancel.Text = "ｷｬﾝｾﾙ(&C)";
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.label1);
            this.groupBox1.Controls.Add(this.msgStatusLength);
            this.groupBox1.Controls.Add(this.radioStatus);
            this.groupBox1.Controls.Add(this.radioMsgBox);
            this.groupBox1.Location = new System.Drawing.Point(5, 5);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(392, 80);
            this.groupBox1.TabIndex = 2;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "エラーメッセージの表示";
            // 
            // label1
            // 
            this.label1.Location = new System.Drawing.Point(160, 16);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(96, 16);
            this.label1.TabIndex = 3;
            this.label1.Text = "表示時間：";
            // 
            // msgStatusLength
            // 
            this.msgStatusLength.Location = new System.Drawing.Point(160, 32);
            this.msgStatusLength.Minimum = 1;
            this.msgStatusLength.Name = "msgStatusLength";
            this.msgStatusLength.Size = new System.Drawing.Size(224, 45);
            this.msgStatusLength.TabIndex = 2;
            this.msgStatusLength.Value = 1;
            // 
            // radioStatus
            // 
            this.radioStatus.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.radioStatus.Location = new System.Drawing.Point(16, 48);
            this.radioStatus.Name = "radioStatus";
            this.radioStatus.Size = new System.Drawing.Size(144, 16);
            this.radioStatus.TabIndex = 1;
            this.radioStatus.Text = "ステータスバーに表示";
            this.radioStatus.CheckedChanged += new System.EventHandler(this.onRadioMsgStyle);
            // 
            // radioMsgBox
            // 
            this.radioMsgBox.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.radioMsgBox.Location = new System.Drawing.Point(16, 24);
            this.radioMsgBox.Name = "radioMsgBox";
            this.radioMsgBox.Size = new System.Drawing.Size(144, 16);
            this.radioMsgBox.TabIndex = 0;
            this.radioMsgBox.Text = "メッセージボックスを表示";
            this.radioMsgBox.CheckedChanged += new System.EventHandler(this.onRadioMsgStyle);
            // 
            // drawStationNames
            // 
            this.drawStationNames.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.drawStationNames.Location = new System.Drawing.Point(8, 90);
            this.drawStationNames.Name = "drawStationNames";
            this.drawStationNames.Size = new System.Drawing.Size(168, 16);
            this.drawStationNames.TabIndex = 3;
            this.drawStationNames.Text = "駅の名前を画面に表示";
            // 
            // showBoundingBox
            // 
            this.showBoundingBox.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.showBoundingBox.Location = new System.Drawing.Point(8, 110);
            this.showBoundingBox.Name = "showBoundingBox";
            this.showBoundingBox.Size = new System.Drawing.Size(168, 16);
            this.showBoundingBox.TabIndex = 4;
            this.showBoundingBox.Text = "描画範囲を表示(デバッグ)";
            // 
            // hideTrees
            // 
            this.hideTrees.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.hideTrees.Location = new System.Drawing.Point(8, 131);
            this.hideTrees.Name = "hideTrees";
            this.hideTrees.Size = new System.Drawing.Size(168, 16);
            this.hideTrees.TabIndex = 4;
            this.hideTrees.Text = "樹木の描画を省略";
            // 
            // comboSurfaceAlloc
            // 
            this.comboSurfaceAlloc.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboSurfaceAlloc.Items.AddRange(new object[] {
            "自動的に判断する",
            "ビデオメモリに限定する",
            "システムメモリに限定する"});
            this.comboSurfaceAlloc.Location = new System.Drawing.Point(171, 159);
            this.comboSurfaceAlloc.Name = "comboSurfaceAlloc";
            this.comboSurfaceAlloc.Size = new System.Drawing.Size(218, 20);
            this.comboSurfaceAlloc.TabIndex = 5;
            this.toolTip.SetToolTip(this.comboSurfaceAlloc, "描画が遅かったりエラーがでるようなら変更してください。");
            // 
            // label2
            // 
            this.label2.Location = new System.Drawing.Point(8, 160);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(157, 16);
            this.label2.TabIndex = 6;
            this.label2.Text = "オフスクリーンサーフェスの確保：";
            this.label2.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.toolTip.SetToolTip(this.label2, "描画が遅かったりエラーがでるようなら変更してください。");
            // 
            // optionTabControl
            // 
            this.optionTabControl.Controls.Add(this.tabPage1);
            this.optionTabControl.Controls.Add(this.bunkruptTabPage);
            this.optionTabControl.Controls.Add(this.fontstabPage);
            this.optionTabControl.Location = new System.Drawing.Point(6, 10);
            this.optionTabControl.Name = "optionTabControl";
            this.optionTabControl.SelectedIndex = 0;
            this.optionTabControl.Size = new System.Drawing.Size(409, 227);
            this.optionTabControl.TabIndex = 7;
            // 
            // tabPage1
            // 
            this.tabPage1.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.tabPage1.Controls.Add(this.drawTrainNames);
            this.tabPage1.Controls.Add(this.drawElectlicPoles);
            this.tabPage1.Controls.Add(this.groupBox1);
            this.tabPage1.Controls.Add(this.label2);
            this.tabPage1.Controls.Add(this.drawStationNames);
            this.tabPage1.Controls.Add(this.comboSurfaceAlloc);
            this.tabPage1.Controls.Add(this.hideTrees);
            this.tabPage1.Controls.Add(this.showBoundingBox);
            this.tabPage1.Location = new System.Drawing.Point(4, 21);
            this.tabPage1.Name = "tabPage1";
            this.tabPage1.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage1.Size = new System.Drawing.Size(401, 202);
            this.tabPage1.TabIndex = 0;
            this.tabPage1.Text = "全般";
            this.tabPage1.UseVisualStyleBackColor = true;
            // 
            // drawTrainNames
            // 
            this.drawTrainNames.AutoSize = true;
            this.drawTrainNames.Location = new System.Drawing.Point(199, 91);
            this.drawTrainNames.Name = "drawTrainNames";
            this.drawTrainNames.Size = new System.Drawing.Size(126, 16);
            this.drawTrainNames.TabIndex = 8;
            this.drawTrainNames.Text = "列車名を画面に表示";
            this.drawTrainNames.UseVisualStyleBackColor = true;
            // 
            // drawElectlicPoles
            // 
            this.drawElectlicPoles.AutoSize = true;
            this.drawElectlicPoles.Location = new System.Drawing.Point(199, 110);
            this.drawElectlicPoles.Name = "drawElectlicPoles";
            this.drawElectlicPoles.Size = new System.Drawing.Size(126, 16);
            this.drawElectlicPoles.TabIndex = 7;
            this.drawElectlicPoles.Text = "架線柱を画面に表示";
            this.drawElectlicPoles.UseVisualStyleBackColor = true;
            // 
            // fontstabPage
            // 
            this.fontstabPage.Controls.Add(this.trainFontSample);
            this.fontstabPage.Controls.Add(this.stationFontSample);
            this.fontstabPage.Controls.Add(this.fontselectTrain);
            this.fontstabPage.Controls.Add(this.fontselectStation);
            this.fontstabPage.Location = new System.Drawing.Point(4, 21);
            this.fontstabPage.Name = "fontstabPage";
            this.fontstabPage.Padding = new System.Windows.Forms.Padding(3);
            this.fontstabPage.Size = new System.Drawing.Size(401, 202);
            this.fontstabPage.TabIndex = 2;
            this.fontstabPage.Text = "フォント";
            this.fontstabPage.UseVisualStyleBackColor = true;
            // 
            // trainFontSample
            // 
            this.trainFontSample.BackColor = System.Drawing.Color.Black;
            this.trainFontSample.ImageAlign = System.Drawing.ContentAlignment.TopLeft;
            this.trainFontSample.Location = new System.Drawing.Point(177, 81);
            this.trainFontSample.Name = "trainFontSample";
            this.trainFontSample.Size = new System.Drawing.Size(197, 60);
            this.trainFontSample.TabIndex = 3;
            this.trainFontSample.Text = "列車名サンプル";
            this.trainFontSample.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // stationFontSample
            // 
            this.stationFontSample.BackColor = System.Drawing.Color.Black;
            this.stationFontSample.ImageAlign = System.Drawing.ContentAlignment.TopLeft;
            this.stationFontSample.Location = new System.Drawing.Point(177, 9);
            this.stationFontSample.Name = "stationFontSample";
            this.stationFontSample.Size = new System.Drawing.Size(197, 60);
            this.stationFontSample.TabIndex = 2;
            this.stationFontSample.Text = "駅名サンプル";
            this.stationFontSample.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // fontselectTrain
            // 
            this.fontselectTrain.Location = new System.Drawing.Point(16, 100);
            this.fontselectTrain.Name = "fontselectTrain";
            this.fontselectTrain.Size = new System.Drawing.Size(138, 23);
            this.fontselectTrain.TabIndex = 1;
            this.fontselectTrain.Text = "列車名フォント変更";
            this.fontselectTrain.UseVisualStyleBackColor = false;
            this.fontselectTrain.Click += new System.EventHandler(this.fontselectTrain_Click);
            // 
            // fontselectStation
            // 
            this.fontselectStation.Location = new System.Drawing.Point(16, 28);
            this.fontselectStation.Name = "fontselectStation";
            this.fontselectStation.Size = new System.Drawing.Size(138, 23);
            this.fontselectStation.TabIndex = 0;
            this.fontselectStation.Text = "駅名フォント変更";
            this.fontselectStation.UseVisualStyleBackColor = false;
            this.fontselectStation.Click += new System.EventHandler(this.fontselectStation_Click);
            // 
            // fontDialogStation
            // 
            this.fontDialogStation.AllowVerticalFonts = false;
            this.fontDialogStation.Color = this.stationFontSample.ForeColor;
            this.fontDialogStation.Font = this.stationFontSample.Font;
            this.fontDialogStation.FontMustExist = true;
            this.fontDialogStation.MaxSize = 16;
            this.fontDialogStation.MinSize = 8;
            this.fontDialogStation.ShowColor = true;
            // 
            // fontDialogTrain
            // 
            this.fontDialogTrain.AllowVerticalFonts = false;
            this.fontDialogTrain.Color = this.trainFontSample.ForeColor;
            this.fontDialogTrain.Font = this.trainFontSample.Font;
            this.fontDialogTrain.FontMustExist = true;
            this.fontDialogTrain.MaxSize = 16;
            this.fontDialogTrain.MinSize = 8;
            this.fontDialogTrain.ShowColor = true;
            // 
            // bunkruptTabPage
            // 
            this.bunkruptTabPage.Controls.Add(this.bunkruptplus3);
            this.bunkruptTabPage.Controls.Add(this.bunkruptplus2);
            this.bunkruptTabPage.Controls.Add(this.bunkruptplus1);
            this.bunkruptTabPage.Controls.Add(this.label3);
            this.bunkruptTabPage.Controls.Add(this.showMessageatBunkrupt);
            this.bunkruptTabPage.Location = new System.Drawing.Point(4, 21);
            this.bunkruptTabPage.Name = "bunkruptTabPage";
            this.bunkruptTabPage.Padding = new System.Windows.Forms.Padding(3);
            this.bunkruptTabPage.Size = new System.Drawing.Size(401, 202);
            this.bunkruptTabPage.TabIndex = 1;
            this.bunkruptTabPage.Text = "破産";
            this.bunkruptTabPage.UseVisualStyleBackColor = true;
            // 
            // showMessageatBunkrupt
            // 
            this.showMessageatBunkrupt.AutoSize = true;
            this.showMessageatBunkrupt.Checked = true;
            this.showMessageatBunkrupt.CheckState = System.Windows.Forms.CheckState.Checked;
            this.showMessageatBunkrupt.Location = new System.Drawing.Point(7, 17);
            this.showMessageatBunkrupt.Name = "showMessageatBunkrupt";
            this.showMessageatBunkrupt.Size = new System.Drawing.Size(166, 16);
            this.showMessageatBunkrupt.TabIndex = 0;
            this.showMessageatBunkrupt.Text = "破産時にメッセージを表示する";
            this.showMessageatBunkrupt.UseVisualStyleBackColor = true;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(7, 56);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(153, 12);
            this.label3.TabIndex = 1;
            this.label3.Text = "破産時に自動復活する資金額";
            // 
            // bunkruptplus1
            // 
            this.bunkruptplus1.AutoSize = true;
            this.bunkruptplus1.Checked = true;
            this.bunkruptplus1.Location = new System.Drawing.Point(7, 72);
            this.bunkruptplus1.Name = "bunkruptplus1";
            this.bunkruptplus1.Size = new System.Drawing.Size(69, 16);
            this.bunkruptplus1.TabIndex = 2;
            this.bunkruptplus1.TabStop = true;
            this.bunkruptplus1.Text = "10,000,000,000";
            this.bunkruptplus1.UseVisualStyleBackColor = true;
            // 
            // bunkruptplus2
            // 
            this.bunkruptplus2.AutoSize = true;
            this.bunkruptplus2.Location = new System.Drawing.Point(7, 95);
            this.bunkruptplus2.Name = "bunkruptplus2";
            this.bunkruptplus2.Size = new System.Drawing.Size(75, 16);
            this.bunkruptplus2.TabIndex = 3;
            this.bunkruptplus2.Text = "100,000,000,000";
            this.bunkruptplus2.UseVisualStyleBackColor = true;
            // 
            // bunkruptplus3
            // 
            this.bunkruptplus3.AutoSize = true;
            this.bunkruptplus3.Location = new System.Drawing.Point(7, 117);
            this.bunkruptplus3.Name = "bunkruptplus3";
            this.bunkruptplus3.Size = new System.Drawing.Size(81, 16);
            this.bunkruptplus3.TabIndex = 4;
            this.bunkruptplus3.Text = "1,000,000,000,000";
            this.bunkruptplus3.UseVisualStyleBackColor = true;
            // 
            // ConfigDialog
            // 
            this.AcceptButton = this.buttonOK;
            this.AutoScaleBaseSize = new System.Drawing.Size(5, 12);
            this.CancelButton = this.buttonCancel;
            this.ClientSize = new System.Drawing.Size(505, 337);
            this.Controls.Add(this.optionTabControl);
            this.Controls.Add(this.buttonCancel);
            this.Controls.Add(this.buttonOK);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ConfigDialog";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "FreeTrain Ex Avの設定";
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.msgStatusLength)).EndInit();
            this.optionTabControl.ResumeLayout(false);
            this.tabPage1.ResumeLayout(false);
            this.tabPage1.PerformLayout();
            this.fontstabPage.ResumeLayout(false);
            this.bunkruptTabPage.ResumeLayout(false);
            this.bunkruptTabPage.PerformLayout();
            this.ResumeLayout(false);

		}
		#endregion

		private void onRadioMsgStyle(object sender, EventArgs e) {
			msgStatusLength.Enabled = radioStatus.Checked;
		}

        private void fontselectStation_Click(object sender, EventArgs e) {
            if (fontDialogStation.ShowDialog() == DialogResult.OK) {
                stationFontSample.Font = fontDialogStation.Font;
                stationFontSample.ForeColor = fontDialogStation.Color;
            }
        }

        private void fontselectTrain_Click(object sender, EventArgs e) {
            if (fontDialogTrain.ShowDialog() == DialogResult.OK) {
                trainFontSample.Font = fontDialogTrain.Font;
                trainFontSample.ForeColor = fontDialogTrain.Color;
            }
        }


      
		private void buttonOK_Click(object sender, EventArgs e) {
			opts.showErrorMessageBox = radioMsgBox.Checked;
			opts.messageDisplayTime = msgStatusLength.Value;
			opts.drawStationNames = drawStationNames.Checked;
			opts.drawTrainNames = drawTrainNames.Checked;
			opts.drawElectlicPoles = drawElectlicPoles.Checked;
			opts.drawBoundingBox = showBoundingBox.Checked;
			opts.hideTrees = hideTrees.Checked;
			opts.SurfaceAlloc = (DDSurfaceAllocation)comboSurfaceAlloc.SelectedIndex;

            opts.bunkruptMessageFlag = showMessageatBunkrupt.Checked;

            if( bunkruptplus1.Checked ) opts.liquidPlusAtBunkrupt = 10000000000;
            if( bunkruptplus2.Checked ) opts.liquidPlusAtBunkrupt = 100000000000;
            if( bunkruptplus3.Checked ) opts.liquidPlusAtBunkrupt = 1000000000000;

            fntopt.fontStationNames = stationFontSample.Font;
            fntopt.colorStationNames = stationFontSample.ForeColor;
            fntopt.fontTrainNames = trainFontSample.Font;
            fntopt.colorTrainNames = trainFontSample.ForeColor;

            FontConverter fc = new FontConverter();
            opts.fontstringStationNames = fc.ConvertToString(fntopt.fontStationNames);
            opts.fontstringTrainNames = fc.ConvertToString(fntopt.fontTrainNames);
            opts.colorvalueStationNames = fntopt.colorStationNames.ToArgb();
            opts.colorvalueTrainNames = fntopt.colorTrainNames.ToArgb();
          
			opts.save();
			Close();
		}

	}
}
