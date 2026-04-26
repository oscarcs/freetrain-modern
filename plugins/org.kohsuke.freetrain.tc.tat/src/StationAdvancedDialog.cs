// 2008.09.06 YZ Add forwarding and reverse & forwarding option
// 2008.09.09 YZ Add copy button
// 2008.09.09 YZ Add all clear button
// 2008.09.09 YZ Change not reset fields after add rule
// 2008.09.13 YZ Add rule sorter
// 2008.09.13 YZ Add check exist same rule
// 2008.11.08 YZ Modified serialization formatter
// 2008.11.11 YZ Delete rule sorter
// 2010.04.01 riorio Add TURNING reverse
using System;
using System.Diagnostics;
using System.Drawing;
using System.Collections;
using System.ComponentModel;
using System.Windows.Forms;

#region YZ_20080909_ADDED
using System.IO;
using System.Runtime.Serialization;
#endregion

namespace freetrain.world.rail.tattc
{
	/// <summary>
	/// StationAdvancedDialog の概要の説明です。
	/// </summary>
	internal class StationAdvancedDialog : Form
	{
		internal StationAdvancedDialog( AdvancedStationHandler handler ) {
			this.station = handler;
			//
			// Windows フォーム デザイナ サポートに必要です。
			//
			InitializeComponent();

#region YZ_20080913_ADDED
#region YZ_20081111_DELETED
//          this.triggerList.ListViewItemSorter = new TriggerListViewItemComparer();
#endregion
#endregion

            resetEntryBoxes();
			onSelectionChanged(null,null);

			// populate the list view
			foreach( AdvStationRule rule in station.rules ) {
				ListViewItem lvi = new ListViewItem();
				updateListViewItem(rule,lvi);
				triggerList.Items.Add(lvi);
			}
		}

		private System.Windows.Forms.ComboBox minBox;
		private System.Windows.Forms.RadioButton radioReverse;
        private RadioButton radioRForwarding;
        private RadioButton radioForwarding;
        private Button buttonCopy;
        private Button buttonReadDia;
        private Button buttonSaveDia;
        private Button buttonAllClear;
        private RadioButton radioRForwarding2;
        private RadioButton radioReverse2;

		private readonly AdvancedStationHandler station;

		/// <summary>
		/// 使用されているリソースに後処理を実行します。
		/// </summary>
		protected override void Dispose( bool disposing ) {
			if( disposing && components != null)
				components.Dispose();
			base.Dispose( disposing );
		}

		#region Windows Form Designer generated code
		private System.Windows.Forms.RadioButton radioStop;
		private System.Windows.Forms.RadioButton radioPass;
		private System.Windows.Forms.RadioButton radioGo;
		private System.Windows.Forms.Label label5;
		private System.Windows.Forms.ColumnHeader columnHeader6;
		private System.Windows.Forms.ColumnHeader columnHeader5;
		private System.Windows.Forms.GroupBox groupBox1;
		private System.Windows.Forms.ComboBox monthBox;
		private System.Windows.Forms.Label label1;
		private System.Windows.Forms.ComboBox dayBox;
		private System.Windows.Forms.Label label2;
		private System.Windows.Forms.ComboBox dayOfWeekBox;
		private System.Windows.Forms.Label label3;
		private System.Windows.Forms.ComboBox hourBox;
		private System.Windows.Forms.Label label4;
		private System.Windows.Forms.Button buttonAdd;
		private System.Windows.Forms.Button buttonUp;
		private System.Windows.Forms.Button buttonDown;
		private System.Windows.Forms.Button buttonRemove;
		private System.Windows.Forms.Button buttonOk;
		private System.Windows.Forms.ColumnHeader columnHeader1;
		private System.Windows.Forms.ColumnHeader columnHeader2;
		private System.Windows.Forms.ColumnHeader columnHeader3;
		private System.Windows.Forms.ColumnHeader columnHeader4;
		private System.Windows.Forms.Button buttonReplace;
		private System.Windows.Forms.ListView triggerList;
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
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.radioRForwarding2 = new System.Windows.Forms.RadioButton();
            this.radioReverse2 = new System.Windows.Forms.RadioButton();
            this.radioRForwarding = new System.Windows.Forms.RadioButton();
            this.radioForwarding = new System.Windows.Forms.RadioButton();
            this.radioReverse = new System.Windows.Forms.RadioButton();
            this.minBox = new System.Windows.Forms.ComboBox();
            this.radioGo = new System.Windows.Forms.RadioButton();
            this.radioStop = new System.Windows.Forms.RadioButton();
            this.radioPass = new System.Windows.Forms.RadioButton();
            this.buttonReplace = new System.Windows.Forms.Button();
            this.hourBox = new System.Windows.Forms.ComboBox();
            this.dayOfWeekBox = new System.Windows.Forms.ComboBox();
            this.dayBox = new System.Windows.Forms.ComboBox();
            this.monthBox = new System.Windows.Forms.ComboBox();
            this.buttonAdd = new System.Windows.Forms.Button();
            this.label5 = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.buttonUp = new System.Windows.Forms.Button();
            this.buttonDown = new System.Windows.Forms.Button();
            this.buttonRemove = new System.Windows.Forms.Button();
            this.buttonOk = new System.Windows.Forms.Button();
            this.triggerList = new System.Windows.Forms.ListView();
            this.columnHeader1 = new System.Windows.Forms.ColumnHeader();
            this.columnHeader2 = new System.Windows.Forms.ColumnHeader();
            this.columnHeader3 = new System.Windows.Forms.ColumnHeader();
            this.columnHeader4 = new System.Windows.Forms.ColumnHeader();
            this.columnHeader5 = new System.Windows.Forms.ColumnHeader();
            this.columnHeader6 = new System.Windows.Forms.ColumnHeader();
            this.buttonCopy = new System.Windows.Forms.Button();
            this.buttonReadDia = new System.Windows.Forms.Button();
            this.buttonSaveDia = new System.Windows.Forms.Button();
            this.buttonAllClear = new System.Windows.Forms.Button();
            this.groupBox1.SuspendLayout();
            this.SuspendLayout();
            // 
            // groupBox1
            // 
            this.groupBox1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBox1.Controls.Add(this.radioRForwarding2);
            this.groupBox1.Controls.Add(this.radioReverse2);
            this.groupBox1.Controls.Add(this.radioRForwarding);
            this.groupBox1.Controls.Add(this.radioForwarding);
            this.groupBox1.Controls.Add(this.radioReverse);
            this.groupBox1.Controls.Add(this.minBox);
            this.groupBox1.Controls.Add(this.radioGo);
            this.groupBox1.Controls.Add(this.radioStop);
            this.groupBox1.Controls.Add(this.radioPass);
            this.groupBox1.Controls.Add(this.buttonReplace);
            this.groupBox1.Controls.Add(this.hourBox);
            this.groupBox1.Controls.Add(this.dayOfWeekBox);
            this.groupBox1.Controls.Add(this.dayBox);
            this.groupBox1.Controls.Add(this.monthBox);
            this.groupBox1.Controls.Add(this.buttonAdd);
            this.groupBox1.Controls.Add(this.label5);
            this.groupBox1.Controls.Add(this.label4);
            this.groupBox1.Controls.Add(this.label3);
            this.groupBox1.Controls.Add(this.label2);
            this.groupBox1.Controls.Add(this.label1);
            this.groupBox1.Location = new System.Drawing.Point(0, 8);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(590, 80);
            this.groupBox1.TabIndex = 0;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "条件";
            // 
            // radioRForwarding2
            // 
            this.radioRForwarding2.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.radioRForwarding2.Location = new System.Drawing.Point(360, 38);
            this.radioRForwarding2.Name = "radioRForwarding2";
            this.radioRForwarding2.Size = new System.Drawing.Size(52, 38);
            this.radioRForwarding2.TabIndex = 19;
            this.radioRForwarding2.Text = "反転回送";
            this.radioRForwarding2.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // radioReverse2
            // 
            this.radioReverse2.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.radioReverse2.Location = new System.Drawing.Point(193, 38);
            this.radioReverse2.Name = "radioReverse2";
            this.radioReverse2.Size = new System.Drawing.Size(52, 38);
            this.radioReverse2.TabIndex = 18;
            this.radioReverse2.Text = "反転折返";
            this.radioReverse2.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // radioRForwarding
            // 
            this.radioRForwarding.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.radioRForwarding.Location = new System.Drawing.Point(301, 38);
            this.radioRForwarding.Name = "radioRForwarding";
            this.radioRForwarding.Size = new System.Drawing.Size(52, 38);
            this.radioRForwarding.TabIndex = 15;
            this.radioRForwarding.Text = "折返回送";
            this.radioRForwarding.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // radioForwarding
            // 
            this.radioForwarding.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.radioForwarding.Location = new System.Drawing.Point(260, 38);
            this.radioForwarding.Name = "radioForwarding";
            this.radioForwarding.Size = new System.Drawing.Size(40, 38);
            this.radioForwarding.TabIndex = 14;
            this.radioForwarding.Text = "回送";
            this.radioForwarding.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // radioReverse
            // 
            this.radioReverse.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.radioReverse.Location = new System.Drawing.Point(136, 38);
            this.radioReverse.Name = "radioReverse";
            this.radioReverse.Size = new System.Drawing.Size(52, 38);
            this.radioReverse.TabIndex = 13;
            this.radioReverse.Text = "折返発車";
            this.radioReverse.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // minBox
            // 
            this.minBox.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.minBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.minBox.Items.AddRange(new object[] {
            "*",
            "00",
            "10",
            "20",
            "30",
            "40",
            "50"});
            this.minBox.Location = new System.Drawing.Point(493, 16);
            this.minBox.MaxDropDownItems = 13;
            this.minBox.Name = "minBox";
            this.minBox.Size = new System.Drawing.Size(57, 20);
            this.minBox.TabIndex = 8;
            // 
            // radioGo
            // 
            this.radioGo.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.radioGo.Location = new System.Drawing.Point(96, 38);
            this.radioGo.Name = "radioGo";
            this.radioGo.Size = new System.Drawing.Size(40, 38);
            this.radioGo.TabIndex = 12;
            this.radioGo.Text = "発車";
            this.radioGo.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // radioStop
            // 
            this.radioStop.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.radioStop.Location = new System.Drawing.Point(54, 38);
            this.radioStop.Name = "radioStop";
            this.radioStop.Size = new System.Drawing.Size(40, 38);
            this.radioStop.TabIndex = 11;
            this.radioStop.Text = "停車";
            this.radioStop.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // radioPass
            // 
            this.radioPass.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.radioPass.Checked = true;
            this.radioPass.Location = new System.Drawing.Point(12, 38);
            this.radioPass.Name = "radioPass";
            this.radioPass.Size = new System.Drawing.Size(40, 38);
            this.radioPass.TabIndex = 10;
            this.radioPass.TabStop = true;
            this.radioPass.Text = "通過";
            // 
            // buttonReplace
            // 
            this.buttonReplace.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonReplace.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.buttonReplace.Location = new System.Drawing.Point(517, 48);
            this.buttonReplace.Name = "buttonReplace";
            this.buttonReplace.Size = new System.Drawing.Size(64, 24);
            this.buttonReplace.TabIndex = 17;
            this.buttonReplace.Text = "置換(&R)";
            this.buttonReplace.Click += new System.EventHandler(this.buttonReplace_Click);
            // 
            // hourBox
            // 
            this.hourBox.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.hourBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.hourBox.Items.AddRange(new object[] {
            "*",
            "00",
            "01",
            "02",
            "03",
            "04",
            "05",
            "06",
            "07",
            "08",
            "09",
            "10",
            "11",
            "12",
            "13",
            "14",
            "15",
            "16",
            "17",
            "18",
            "19",
            "20",
            "21",
            "22",
            "23"});
            this.hourBox.Location = new System.Drawing.Point(413, 16);
            this.hourBox.MaxDropDownItems = 13;
            this.hourBox.Name = "hourBox";
            this.hourBox.Size = new System.Drawing.Size(57, 20);
            this.hourBox.TabIndex = 6;
            // 
            // dayOfWeekBox
            // 
            this.dayOfWeekBox.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.dayOfWeekBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.dayOfWeekBox.Items.AddRange(new object[] {
            "*",
            "日",
            "月",
            "火",
            "水",
            "木",
            "金",
            "土"});
            this.dayOfWeekBox.Location = new System.Drawing.Point(317, 16);
            this.dayOfWeekBox.MaxDropDownItems = 13;
            this.dayOfWeekBox.Name = "dayOfWeekBox";
            this.dayOfWeekBox.Size = new System.Drawing.Size(56, 20);
            this.dayOfWeekBox.TabIndex = 4;
            // 
            // dayBox
            // 
            this.dayBox.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.dayBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.dayBox.Items.AddRange(new object[] {
            "*",
            "1",
            "2",
            "3",
            "4",
            "5",
            "6",
            "7",
            "8",
            "9",
            "10",
            "11",
            "12",
            "13",
            "14",
            "15",
            "16",
            "17",
            "18",
            "19",
            "20",
            "21",
            "22",
            "23",
            "24",
            "25",
            "26",
            "27",
            "28",
            "29",
            "30",
            "31"});
            this.dayBox.Location = new System.Drawing.Point(237, 16);
            this.dayBox.MaxDropDownItems = 13;
            this.dayBox.Name = "dayBox";
            this.dayBox.Size = new System.Drawing.Size(56, 20);
            this.dayBox.TabIndex = 2;
            // 
            // monthBox
            // 
            this.monthBox.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.monthBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.monthBox.Items.AddRange(new object[] {
            "*",
            "1",
            "2",
            "3",
            "4",
            "5",
            "6",
            "7",
            "8",
            "9",
            "10",
            "11",
            "12"});
            this.monthBox.Location = new System.Drawing.Point(157, 16);
            this.monthBox.MaxDropDownItems = 13;
            this.monthBox.Name = "monthBox";
            this.monthBox.Size = new System.Drawing.Size(56, 20);
            this.monthBox.TabIndex = 0;
            // 
            // buttonAdd
            // 
            this.buttonAdd.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonAdd.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.buttonAdd.Location = new System.Drawing.Point(446, 48);
            this.buttonAdd.Name = "buttonAdd";
            this.buttonAdd.Size = new System.Drawing.Size(64, 24);
            this.buttonAdd.TabIndex = 16;
            this.buttonAdd.Text = "追加(&A)";
            this.buttonAdd.Click += new System.EventHandler(this.buttonAdd_Click);
            // 
            // label5
            // 
            this.label5.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.label5.BackColor = System.Drawing.Color.Transparent;
            this.label5.Location = new System.Drawing.Point(557, 16);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(24, 20);
            this.label5.TabIndex = 9;
            this.label5.Text = "分";
            this.label5.TextAlign = System.Drawing.ContentAlignment.BottomLeft;
            // 
            // label4
            // 
            this.label4.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.label4.BackColor = System.Drawing.Color.Transparent;
            this.label4.Location = new System.Drawing.Point(477, 16);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(24, 20);
            this.label4.TabIndex = 7;
            this.label4.Text = "時";
            this.label4.TextAlign = System.Drawing.ContentAlignment.BottomLeft;
            // 
            // label3
            // 
            this.label3.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.label3.BackColor = System.Drawing.Color.Transparent;
            this.label3.Location = new System.Drawing.Point(381, 16);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(40, 20);
            this.label3.TabIndex = 5;
            this.label3.Text = "曜日";
            this.label3.TextAlign = System.Drawing.ContentAlignment.BottomLeft;
            // 
            // label2
            // 
            this.label2.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.label2.BackColor = System.Drawing.Color.Transparent;
            this.label2.Location = new System.Drawing.Point(301, 16);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(25, 20);
            this.label2.TabIndex = 3;
            this.label2.Text = "日";
            this.label2.TextAlign = System.Drawing.ContentAlignment.BottomLeft;
            // 
            // label1
            // 
            this.label1.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.label1.BackColor = System.Drawing.Color.Transparent;
            this.label1.Location = new System.Drawing.Point(221, 16);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(25, 20);
            this.label1.TabIndex = 1;
            this.label1.Text = "月";
            this.label1.TextAlign = System.Drawing.ContentAlignment.BottomLeft;
            // 
            // buttonUp
            // 
            this.buttonUp.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonUp.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.buttonUp.Location = new System.Drawing.Point(517, 96);
            this.buttonUp.Name = "buttonUp";
            this.buttonUp.Size = new System.Drawing.Size(64, 24);
            this.buttonUp.TabIndex = 19;
            this.buttonUp.Text = "↑";
            this.buttonUp.Click += new System.EventHandler(this.buttonUp_Click);
            // 
            // buttonDown
            // 
            this.buttonDown.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonDown.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.buttonDown.Location = new System.Drawing.Point(517, 128);
            this.buttonDown.Name = "buttonDown";
            this.buttonDown.Size = new System.Drawing.Size(64, 24);
            this.buttonDown.TabIndex = 20;
            this.buttonDown.Text = "↓";
            this.buttonDown.Click += new System.EventHandler(this.buttonDown_Click);
            // 
            // buttonRemove
            // 
            this.buttonRemove.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonRemove.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.buttonRemove.Location = new System.Drawing.Point(517, 186);
            this.buttonRemove.Name = "buttonRemove";
            this.buttonRemove.Size = new System.Drawing.Size(64, 24);
            this.buttonRemove.TabIndex = 22;
            this.buttonRemove.Text = "削除";
            this.buttonRemove.Click += new System.EventHandler(this.buttonRemove_Click);
            // 
            // buttonOk
            // 
            this.buttonOk.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonOk.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.buttonOk.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.buttonOk.Location = new System.Drawing.Point(493, 336);
            this.buttonOk.Name = "buttonOk";
            this.buttonOk.Size = new System.Drawing.Size(88, 24);
            this.buttonOk.TabIndex = 23;
            this.buttonOk.Text = "&OK";
            // 
            // triggerList
            // 
            this.triggerList.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.triggerList.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.columnHeader1,
            this.columnHeader2,
            this.columnHeader3,
            this.columnHeader4,
            this.columnHeader5,
            this.columnHeader6});
            this.triggerList.FullRowSelect = true;
            this.triggerList.HeaderStyle = System.Windows.Forms.ColumnHeaderStyle.Nonclickable;
            this.triggerList.HideSelection = false;
            this.triggerList.Location = new System.Drawing.Point(8, 96);
            this.triggerList.MultiSelect = false;
            this.triggerList.Name = "triggerList";
            this.triggerList.Size = new System.Drawing.Size(493, 232);
            this.triggerList.Sorting = System.Windows.Forms.SortOrder.Ascending;
            this.triggerList.TabIndex = 18;
            this.triggerList.UseCompatibleStateImageBehavior = false;
            this.triggerList.View = System.Windows.Forms.View.Details;
            this.triggerList.SelectedIndexChanged += new System.EventHandler(this.onSelectionChanged);
            // 
            // columnHeader1
            // 
            this.columnHeader1.Text = "月";
            // 
            // columnHeader2
            // 
            this.columnHeader2.Text = "日";
            // 
            // columnHeader3
            // 
            this.columnHeader3.Text = "曜日";
            // 
            // columnHeader4
            // 
            this.columnHeader4.Text = "時";
            // 
            // columnHeader5
            // 
            this.columnHeader5.Text = "分";
            // 
            // columnHeader6
            // 
            this.columnHeader6.Text = "動作";
            // 
            // buttonCopy
            // 
            this.buttonCopy.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonCopy.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.buttonCopy.Location = new System.Drawing.Point(517, 157);
            this.buttonCopy.Name = "buttonCopy";
            this.buttonCopy.Size = new System.Drawing.Size(64, 24);
            this.buttonCopy.TabIndex = 21;
            this.buttonCopy.Text = "コピー";
            this.buttonCopy.Click += new System.EventHandler(this.buttonCopy_Click);
            // 
            // buttonReadDia
            // 
            this.buttonReadDia.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonReadDia.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.buttonReadDia.Location = new System.Drawing.Point(149, 336);
            this.buttonReadDia.Name = "buttonReadDia";
            this.buttonReadDia.Size = new System.Drawing.Size(88, 24);
            this.buttonReadDia.TabIndex = 24;
            this.buttonReadDia.Text = "ダイヤを読込(&R)";
            this.buttonReadDia.Click += new System.EventHandler(this.buttonReadDia_Click);
            // 
            // buttonSaveDia
            // 
            this.buttonSaveDia.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonSaveDia.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.buttonSaveDia.Location = new System.Drawing.Point(241, 336);
            this.buttonSaveDia.Name = "buttonSaveDia";
            this.buttonSaveDia.Size = new System.Drawing.Size(89, 24);
            this.buttonSaveDia.TabIndex = 25;
            this.buttonSaveDia.Text = "ダイヤを保存(&S)";
            this.buttonSaveDia.Click += new System.EventHandler(this.buttonSaveDia_Click);
            // 
            // buttonAllClear
            // 
            this.buttonAllClear.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonAllClear.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.buttonAllClear.Location = new System.Drawing.Point(517, 214);
            this.buttonAllClear.Name = "buttonAllClear";
            this.buttonAllClear.Size = new System.Drawing.Size(64, 24);
            this.buttonAllClear.TabIndex = 26;
            this.buttonAllClear.Text = "全て消去";
            this.buttonAllClear.Click += new System.EventHandler(this.buttonAllClear_Click);
            // 
            // StationAdvancedDialog
            // 
            this.AcceptButton = this.buttonOk;
            this.AutoScaleBaseSize = new System.Drawing.Size(5, 12);
            this.ClientSize = new System.Drawing.Size(590, 365);
            this.Controls.Add(this.buttonAllClear);
            this.Controls.Add(this.buttonSaveDia);
            this.Controls.Add(this.buttonReadDia);
            this.Controls.Add(this.buttonCopy);
            this.Controls.Add(this.triggerList);
            this.Controls.Add(this.buttonOk);
            this.Controls.Add(this.buttonRemove);
            this.Controls.Add(this.buttonDown);
            this.Controls.Add(this.buttonUp);
            this.Controls.Add(this.groupBox1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.MinimumSize = new System.Drawing.Size(456, 312);
            this.Name = "StationAdvancedDialog";
            this.ShowInTaskbar = false;
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
            this.Text = "発車時刻の詳細設定";
            this.groupBox1.ResumeLayout(false);
            this.ResumeLayout(false);

		}
		#endregion
		
		/// <summary> clear all the boxes back to the default </summary>
		private void resetEntryBoxes() {
			monthBox.SelectedIndex = 0;
			dayBox.SelectedIndex = 0;
			dayOfWeekBox.SelectedIndex = 0;
			hourBox.SelectedIndex = 0;
			minBox.SelectedIndex = 0;
		}

		private void buttonAdd_Click(object sender, System.EventArgs e) {
			// update the data structure
			AdvStationRule tm = createRule();

#region YZ_20080913_ADDED
            foreach(AdvStationRule advRule in station.rules) {                  // Loop in rules
                if (advRule.month     == tm.month     &&
                    advRule.day       == tm.day       &&
                    advRule.dayOfWeek == tm.dayOfWeek &&
                    advRule.hour      == tm.hour      &&
                    advRule.minutes   == tm.minutes) {                          // If same rule
                                                                                // Display error message
                    MessageBox.Show("同じダイヤ設定は追加できません", "追加エラー", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;                                                     // Return
                }
            }
#endregion

			station.rules.add(tm);

			// update the UI
			ListViewItem lvi = new ListViewItem();
			updateListViewItem(tm,lvi);
			triggerList.Items.Add(lvi);
			
#region YZ_20080909_DELETE
//			resetEntryBoxes();
#endregion
        }

		private int selectedIndex { get { return triggerList.SelectedIndices[0]; } }
		private AdvStationRule selectedRule {
			get {
				return (AdvStationRule)triggerList.Items[selectedIndex].Tag;
			}
		}

		private void buttonReplace_Click(object sender, System.EventArgs e) {
			int idx = selectedIndex;

			// update the data structure
			AdvStationRule tm = createRule();
			station.rules.set( idx, tm );

			// update the UI
			ListViewItem lvi = triggerList.Items[idx];
			updateListViewItem(tm,lvi);
		}

		private void buttonUp_Click(object sender, System.EventArgs e) {
			moveData(-1);
		}

		private void buttonDown_Click(object sender, System.EventArgs e) {
			moveData(+1);
		}

		private void moveData( int offset ) {
			int idx = selectedIndex;
			
			// update the data structure
			AdvStationRule rule = selectedRule;
			station.rules.remove(rule);
			station.rules.insert(idx+offset,rule);

			// update the UI
			ListViewItem lvi = triggerList.Items[idx];
			triggerList.Items.Remove(lvi);
			triggerList.Items.Insert(idx+offset,lvi);
		}

		private void buttonRemove_Click(object sender, System.EventArgs e) {
			int idx = selectedIndex;

			// update the data structure
			station.rules.remove( selectedRule );

			// update the UI
			triggerList.Items.RemoveAt(idx);
        }

#region YZ_20080909_ADDED
        // Click copy button event handler
        private void buttonCopy_Click(object sender, EventArgs e) {
			AdvStationRule copyStationRule = copyRule(selectedRule);            // Copy diagram rule
			station.rules.add(copyStationRule);                                 // Add diagram rule to station

			ListViewItem LViewItem = new ListViewItem();                        // Create ListViewItem
			updateListViewItem(copyStationRule, LViewItem);                     // Update ListViewItem from copy diagram rule
			triggerList.Items.Add(LViewItem);                                   // Display diagram rule to listview
        }

        // Copy diagram rule
        private AdvStationRule copyRule(AdvStationRule srcStationRule) {
			AdvStationRule destStationRule = new AdvStationRule();              // Create diagram rule
			
			destStationRule.month = srcStationRule.month;                       // Copy month from source to dest
			destStationRule.day = srcStationRule.day;                           // Copy day from source to dest
			destStationRule.dayOfWeek = srcStationRule.dayOfWeek;               // Copy week from source to dest
			destStationRule.hour = srcStationRule.hour;                         // Copy hour from source to dest
			destStationRule.minutes = srcStationRule.minutes;                   // Copy minute from source to dest
            destStationRule.action = srcStationRule.action;                     // Copy action from source to dest

            return destStationRule;                                             // Return copy diagram rule
		}

        // Click all clear button event handler
        private void buttonAllClear_Click(object sender, EventArgs e) {
                                                                                // Comfirm all clear
            if (MessageBox.Show("ダイヤ設定を全て消去しますが、よろしいですか？", "消去確認", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes) {
                station.rules.Clear();                                          // Clear all station rule
                triggerList.Items.Clear();                                      // Clear listview
    			resetEntryBoxes();                                              // Reset all fields
            }
        }

		private const string filterString = "ダイアグラムデータ (*.ftdd)|*.ftdd"; // Diagram data file filter
		private const string defaultFileName = "diagram.ftdd";                  // Default diagram data file name
        private string diagramFileName = "";                                    // Diagram data file name

        // Click read diagram button event handler
        private void buttonReadDia_Click(object sender, EventArgs e) {
            using(OpenFileDialog openDlg = new OpenFileDialog()) {              // Open file dialog
				openDlg.Filter = filterString;                                  // Set dialog file filter
				openDlg.RestoreDirectory = true;                                // Set dialog restore original directory
                if (diagramFileName == "") {                                    // If diagram file name is null
                    openDlg.FileName = defaultFileName;                         // Set default file name
                } else {    
                    openDlg.FileName = diagramFileName;                         // Set diagram file name
                }

				if (openDlg.ShowDialog(this) == DialogResult.OK) {              // If open file dialog result is ok
                    FileInfo openInfo = new FileInfo(openDlg.FileName);         // Create file info
                    Stream openStream = openInfo.OpenRead();                    // Create open stream
                                                                                // Create deserialize formatter
#region YZ_20081108_MODIFIED
//                  IFormatter openFormatter = new System.Runtime.Serialization.Formatters.Soap.SoapFormatter();
                    IFormatter openFormatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
#endregion

                                                                                // Deserialize station rule object
                    AdvancedStationHandler.RuleCollection readDiagram = (AdvancedStationHandler.RuleCollection)openFormatter.Deserialize(openStream);
                    openStream.Close();                                         // Close stream
                
                    foreach(AdvStationRule advRule in readDiagram) {            // Loop read diagram rule
                        station.rules.add(advRule);                             // Add read diagram rule to station rule
        				ListViewItem LViewItem = new ListViewItem();            // Create ListViewItem
		        		updateListViewItem(advRule, LViewItem);                 // Update ListViewItem from copy diagram rule
				        triggerList.Items.Add(LViewItem);                       // Display diagram rule to listview
                    }
                    readDiagram.Clear();                                        // Clear read diagram
                }
			}   
        }

        // Click save diagram button event handler
        private void buttonSaveDia_Click(object sender, EventArgs e) {
            string  saveFileName;

            using(SaveFileDialog saveDlg = new SaveFileDialog()) {
				saveDlg.Filter = filterString;
				saveDlg.RestoreDirectory = true;
                if (diagramFileName == "") {
                    saveDlg.FileName = defaultFileName;
                } else {
                    saveDlg.FileName = diagramFileName;
                }

				if (saveDlg.ShowDialog(this) == DialogResult.OK) {
					saveFileName = saveDlg.FileName;
                    FileInfo saveInfo = new FileInfo(saveFileName);
                    Stream saveStream = saveInfo.OpenWrite();
#region YZ_20081108_MODIFIED
//                  IFormatter saveFormatter = new System.Runtime.Serialization.Formatters.Soap.SoapFormatter();
                    IFormatter saveFormatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
#endregion
                
                    saveFormatter.Serialize(saveStream, station.rules);              
                    saveStream.Close();
				}
			}
        }
#endregion

        private AdvStationRule createRule() {
			AdvStationRule tm = new AdvStationRule();
			
			tm.month = (sbyte)monthBox.SelectedIndex;
			if(tm.month==0)	tm.month = -1;

			tm.day = (sbyte)dayBox.SelectedIndex;
			if(tm.day==0)	tm.day = -1;

			tm.dayOfWeek = (sbyte)(dayOfWeekBox.SelectedIndex-1);

			tm.hour = (sbyte)(hourBox.SelectedIndex-1);

			tm.minutes = (sbyte)(minBox.SelectedIndex*10);
			if(tm.minutes==0)	tm.minutes = -1;
			else				tm.minutes -= 10;

			if( radioPass.Checked )		tm.action = StationAction.pass;
			if( radioStop.Checked )		tm.action = StationAction.stop;
			if( radioGo.Checked )		tm.action = StationAction.go;
			if( radioReverse.Checked )	tm.action = StationAction.reverse;
			if( radioReverse2.Checked )	tm.action = StationAction.reverse2;

#region YZ_20080906_ADDED
			if (radioForwarding.Checked == true) {
                tm.action = StationAction.forwarding;
            }
			if (radioRForwarding.Checked == true) {
                tm.action = StationAction.rforwarding;
            }
			if (radioRForwarding2.Checked == true) {
                tm.action = StationAction.rforwarding2;
            }
#endregion

            return tm;
		}

		private void updateListViewItem( AdvStationRule rule, ListViewItem lvi ) {
			lvi.SubItems.Clear();

			lvi.Tag = rule;
			lvi.Text = numberToString(rule.month);

			lvi.SubItems.Add(numberToString(rule.day));
			if( rule.dayOfWeek==-1 )
				lvi.SubItems.Add("*");
			else
				lvi.SubItems.Add(""+Clock.dayOfWeekChar(rule.dayOfWeek));
			lvi.SubItems.Add(numberToString(rule.hour));
			lvi.SubItems.Add(numberToString(rule.minutes));

			lvi.SubItems.Add( displayName(rule.action) );
		}

		private string displayName( StationAction a ) {
			switch(a) {
			case StationAction.go:		return "発車";
			case StationAction.pass:	return "通過";
			case StationAction.stop:	return "停車";
			case StationAction.reverse:	return "折返発車";

#region YZ_20080906_ADDED
			case StationAction.forwarding:	return "回送";
			case StationAction.rforwarding:	return "折返回送";
#endregion
			case StationAction.reverse2:	return "反転折返";
			case StationAction.rforwarding2:	return "反転回送";

			default:
				Debug.Assert(false);
				return null;
			}
		}
		private string numberToString( sbyte i ) {
			if(i==-1)	return "*";
			else		return i.ToString();
		}

		private void onSelectionChanged(object sender, System.EventArgs e) {
			bool b = ( triggerList.SelectedIndices.Count!=0 );
			int idx=-1;
			if( b )	idx = selectedIndex;

			buttonUp.Enabled = b && idx!=0;
			buttonDown.Enabled = b && idx!=triggerList.Items.Count-1;
			buttonRemove.Enabled = b;
			buttonReplace.Enabled = b;

#region YZ_20080909_ADDED
            buttonCopy.Enabled = b;                                             // Set enable/disable to copy button
#endregion

			if(idx!=-1) {
				// update the edit box
				AdvStationRule rule = selectedRule;

				if( rule.month== -1 )	monthBox.SelectedIndex = 0;
				else					monthBox.SelectedIndex = rule.month;

				if( rule.day== -1 )		dayBox.SelectedIndex = 0;
				else					dayBox.SelectedIndex = rule.month;

				dayOfWeekBox.SelectedIndex = rule.dayOfWeek+1;

				hourBox.SelectedIndex = rule.hour+1;

				if( rule.minutes== -1 )	minBox.SelectedIndex = 0;
				else					minBox.SelectedIndex = (rule.minutes/10)+1;

				radioGo.Checked		= (rule.action==StationAction.go);
				radioPass.Checked	= (rule.action==StationAction.pass);
				radioStop.Checked	= (rule.action==StationAction.stop);
				radioReverse.Checked= (rule.action==StationAction.reverse);

#region YZ_20080906_ADDED
			    radioForwarding.Checked  = (rule.action == StationAction.forwarding);
			    radioRForwarding.Checked = (rule.action == StationAction.rforwarding);
#endregion
				radioReverse2.Checked = (rule.action==StationAction.reverse2);
			    radioRForwarding2.Checked = (rule.action == StationAction.rforwarding2);

			}
		}
	}

#region YZ_20080913_ADDED
#region YZ_20081111_DELETED
//  class TriggerListViewItemComparer : IComparer
//  {
//      private string[] _weekNames = new string[] {"*", "日", "月", "火", "水", "木", "金", "土"};
        
        // Default constructor
//      public TriggerListViewItemComparer() { }

//      public int Compare(object x, object y)
//      {
//          int idx;                                                            // Index
//          int result = 0;                                                     // Result local variable

//          for(idx = 0; idx < ((ListViewItem)x).SubItems.Count; idx++) {       // Loop listview items
//              if (idx == 2) {                                                 // If column is week
                                                                                // Comapre week name
//                  result = String.Compare(getWeekNo(((ListViewItem)x).SubItems[idx].Text).ToString(), getWeekNo(((ListViewItem)y).SubItems[idx].Text).ToString());
//              } else {
                                                                                // Comapre column item
//                  result = String.Compare(((ListViewItem)x).SubItems[idx].Text, ((ListViewItem)y).SubItems[idx].Text);
//              }
//              if (result != 0) { return result; }                             // If result not equal zero return result
//          }

//          return result;                                                      // Return result
//      }

        // Return week no
//      private int getWeekNo(string weekName) {
//          int idx;                                                            // Index

//          for(idx = 0; idx < _weekNames.Length; idx++) {                      // Loop week names
//              if (string.Compare(_weekNames[idx], weekName) == 0) {           // If same week name exist
//                  return idx;                                                 // Return week no
//              }
//          }
//          return -1;                                                          // Return error
//      }
//  }
#endregion
#endregion
}
