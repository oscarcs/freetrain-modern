using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Xml.Serialization;
using freetrain.framework;
using freetrain.framework.plugin;
using freetrain.util;

namespace freetrain.world.accounting
{
    public class HistoryViewDays : Form
    {


		/// <summary>
		/// Persistent setting.
		/// </summary>
		private Options options = new Options().load();
        private FontDialog fontDialog1;
        private MenuItem FontSelectorMenuItem;
        private MenuItem SeparatorMenuItem;
        private MenuItem GenreSelectorMenuItem;
        private MenuItem CloseMenuItem;


		/// <summary>
		/// Persistent information of this dialog.
		/// </summary>
		public class Options : PersistentOptions
		{
			/// <summary> display font. </summary>
			public FontInfo font;

			/// <summary>
			/// List of displayed genre ids.
			/// Public only for XmlSerializer.
			/// </summary>
			[XmlElement("genre")]
			public string[] _genre;

			[XmlIgnore()]
			public AccountGenre[] genres {
				get {
					try {
						AccountGenre[] r = new AccountGenre[_genre.Length];
						for( int i=0; i<r.Length; i++ )
							r[i] = (AccountGenre)PluginManager.theInstance.getContribution(_genre[i]);
						return r;
					} catch( Exception e ) {
						// recover from missing plug-in error by returning a default list.
						return new AccountGenre[] {
							AccountGenre.RAIL_SERVICE,
							AccountGenre.ROAD_SERVICE,
							AccountGenre.SUBSIDIARIES
						};
					}
				}
				set {
					_genre = new string[value.Length];
					for( int i=0; i<value.Length; i++ )
						_genre[i] = value[i].id;
					save();
				}
			}

			public new Options load() {
				return (Options)base.load();
			}
		}
		


		protected override void OnLoad(System.EventArgs e) {
			// initialize the font
			if(options.font!=null)	setFont(options.font.createFont());
		}

		protected override void OnClosed(System.EventArgs e) {
			options.save();
		}

		private void setFont( Font f ) {
			this.Font = f;
			listView1.Font = f;
			fontDialog1.Font = f;
			options.font = new FontInfo(f);
			options.save();
		}


      
        /// <summary>
        /// 必要なデザイナ変数です。
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// 使用中のリソースをすべてクリーンアップします。
        /// </summary>
        /// <param name="disposing">マネージ リソースが破棄される場合 true、破棄されない場合は false です。</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows フォーム デザイナで生成されたコード

        /// <summary>
        /// デザイナ サポートに必要なメソッドです。このメソッドの内容を
        /// コード エディタで変更しないでください。
        /// </summary>
        private void InitializeComponent()
        {
            this.selectSalesButton = new System.Windows.Forms.RadioButton();
            this.contextMenu = new System.Windows.Forms.ContextMenu();
            this.GenreSelectorMenuItem = new System.Windows.Forms.MenuItem();
            this.FontSelectorMenuItem = new System.Windows.Forms.MenuItem();
            this.SeparatorMenuItem = new System.Windows.Forms.MenuItem();
            this.CloseMenuItem = new System.Windows.Forms.MenuItem();
            this.selectExpendituresButton = new System.Windows.Forms.RadioButton();
            this.selectBalanceButton = new System.Windows.Forms.RadioButton();
            this.listView1 = new System.Windows.Forms.ListView();
            this.fontDialog1 = new System.Windows.Forms.FontDialog();
            this.SuspendLayout();
            // 
            // selectSalesButton
            // 
            this.selectSalesButton.AutoSize = true;
            this.selectSalesButton.Checked = true;
            this.selectSalesButton.ContextMenu = this.contextMenu;
            this.selectSalesButton.Location = new System.Drawing.Point(13, 13);
            this.selectSalesButton.Name = "selectSalesButton";
            this.selectSalesButton.Size = new System.Drawing.Size(47, 16);
            this.selectSalesButton.TabIndex = 2;
            this.selectSalesButton.TabStop = true;
            this.selectSalesButton.Text = "売上";
            this.selectSalesButton.UseVisualStyleBackColor = true;
            this.selectSalesButton.CheckedChanged += new System.EventHandler(this.selectSalesButton_CheckedChanged);
            // 
            // contextMenu
            // 
            this.contextMenu.MenuItems.AddRange(new System.Windows.Forms.MenuItem[] {
            this.GenreSelectorMenuItem,
            this.FontSelectorMenuItem,
            this.SeparatorMenuItem,
            this.CloseMenuItem});
            // 
            // GenreSelectorMenuItem
            // 
            this.GenreSelectorMenuItem.Index = 0;
            this.GenreSelectorMenuItem.Text = "表示項目の編集(&E)...";
            this.GenreSelectorMenuItem.Click += new System.EventHandler(this.GenreSelectorMenuItem_Click);
            // 
            // FontSelectorMenuItem
            // 
            this.FontSelectorMenuItem.Index = 1;
            this.FontSelectorMenuItem.Text = "フォント選択(&F)";
            this.FontSelectorMenuItem.Click += new System.EventHandler(this.FontSelectorMenuItem_Click);
            // 
            // SeparatorMenuItem
            // 
            this.SeparatorMenuItem.Index = 2;
            this.SeparatorMenuItem.Text = "-";
            // 
            // CloseMenuItem
            // 
            this.CloseMenuItem.Index = 3;
            this.CloseMenuItem.Text = "閉じる(&C)";
            // 
            // selectExpendituresButton
            // 
            this.selectExpendituresButton.AutoSize = true;
            this.selectExpendituresButton.ContextMenu = this.contextMenu;
            this.selectExpendituresButton.Location = new System.Drawing.Point(105, 13);
            this.selectExpendituresButton.Name = "selectExpendituresButton";
            this.selectExpendituresButton.Size = new System.Drawing.Size(47, 16);
            this.selectExpendituresButton.TabIndex = 3;
            this.selectExpendituresButton.TabStop = true;
            this.selectExpendituresButton.Text = "経費";
            this.selectExpendituresButton.UseVisualStyleBackColor = true;
            this.selectExpendituresButton.CheckedChanged += new System.EventHandler(this.selectExpendituresButton_CheckedChanged);
            // 
            // selectBalanceButton
            // 
            this.selectBalanceButton.AutoSize = true;
            this.selectBalanceButton.ContextMenu = this.contextMenu;
            this.selectBalanceButton.Location = new System.Drawing.Point(197, 12);
            this.selectBalanceButton.Name = "selectBalanceButton";
            this.selectBalanceButton.Size = new System.Drawing.Size(47, 16);
            this.selectBalanceButton.TabIndex = 4;
            this.selectBalanceButton.TabStop = true;
            this.selectBalanceButton.Text = "収支";
            this.selectBalanceButton.UseVisualStyleBackColor = true;
            this.selectBalanceButton.CheckedChanged += new System.EventHandler(this.selectBalanceButton_CheckedChanged);
            // 
            // listView1
            // 
            this.listView1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.listView1.ContextMenu = this.contextMenu;
            this.listView1.GridLines = true;
            this.listView1.Location = new System.Drawing.Point(12, 35);
            this.listView1.Name = "listView1";
            this.listView1.Size = new System.Drawing.Size(549, 105);
            this.listView1.TabIndex = 5;
            this.listView1.UseCompatibleStateImageBehavior = false;
            this.listView1.View = System.Windows.Forms.View.Details;
            // 
            // HistoryViewMonths
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(574, 152);
            this.Controls.Add(this.listView1);
            this.Controls.Add(this.selectBalanceButton);
            this.Controls.Add(this.selectExpendituresButton);
            this.Controls.Add(this.selectSalesButton);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.SizableToolWindow;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "HistoryViewDays";
            this.ShowInTaskbar = false;
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Show;
            this.Text = "月間売上履歴";
            this.ResumeLayout(false);
            this.PerformLayout();

        }
          

        #endregion

        private System.Windows.Forms.RadioButton selectSalesButton;
        private System.Windows.Forms.RadioButton selectExpendituresButton;
        private System.Windows.Forms.RadioButton selectBalanceButton;
        private System.Windows.Forms.ListView listView1;
		private System.Windows.Forms.ContextMenu contextMenu;
    
        public enum selector : int {
          Sales=0,
          Expenditures=1,
          Balance=2
        }

        public selector ButtonSelect = selector.Sales;
      
        public HistoryViewDays()
          {
            InitializeComponent();

            this.listView1.Columns.Add( "分類", 80, HorizontalAlignment.Center );
            for( int i=1; i<32; i++ ){
                string title = i.ToString() + "日前";
                this.listView1.Columns.Add( title, 80, HorizontalAlignment.Center );
            }
          
			populateListView();
            
          }


        /// <summary>
		/// Set up items in the list view according to <code>options.genres</code>.
		/// </summary>
		private void populateListView() {
			listView1.BeginUpdate();
			listView1.Items.Clear();
            	foreach( AccountGenre g in options.genres ) {
                    ListViewItem lvi = new GenreListItem(this, g, ButtonSelect);
                    listView1.Items.Add(lvi);
                }

            // updateItems();	// fill in the sub texts
			listView1.EndUpdate();			

        }

		/// <summary>
		/// Update the displayed data of list view items.
		/// </summary>
		private AccountListener updateItems;

		/// <summary>
		/// Manage ListViewItem and display information about an account genre.
		/// </summary>
		private class GenreListItem : ListViewItem, IDisposable {
			private readonly AccountGenre genre;
			private readonly HistoryViewDays parent;
            private selector ButtonSelect;

			/// <summary> History object that whose value we are displaying. </summary>
			private TransactionHistory history { get { return genre.history; } }

			internal GenreListItem( HistoryViewDays _parent, AccountGenre genre, selector _ButtonSelect ) {
				this.parent = _parent;
				this.genre = genre;
				this.Text = genre.name;
                this.ButtonSelect = _ButtonSelect;

				for( int i=1; i<32; i++ )
                    this.SubItems.Add("0");

                onUpdateDays31();

            genre.onUpdate += new AccountListener(onUpdateDays31);
			parent.updateItems += new AccountListener(onUpdateDays31);
			}
 
			public void Dispose() {
				// disconnect
				genre.onUpdate -= new AccountListener(onUpdateDays31);
				parent.updateItems -= new AccountListener(onUpdateDays31);
			}

            /// <summary> Update data on the screen. </summary>
			private void onUpdateDays31() { 
				TransactionAgoSummary s = history.dayAgo;
                for( int i=0 ; i<31 ; i++ ){
                    if( ButtonSelect==selector.Sales )
				        this.SubItems[i+1].Text = CurrencyUtil.format(s.sales(i));
                    if( ButtonSelect==selector.Expenditures )
				        this.SubItems[i+1].Text = CurrencyUtil.format(s.expenditures(i));
                    if( ButtonSelect==selector.Balance )
				        this.SubItems[i+1].Text = CurrencyUtil.format(s.balance(i));
                }
			}
		}

        private void selectSalesButton_CheckedChanged(object sender, EventArgs e)
        {
            ButtonSelect = selector.Sales;
            populateListView();
        }

        private void selectExpendituresButton_CheckedChanged(object sender, EventArgs e)
        {
            ButtonSelect = selector.Expenditures;
            populateListView();
        }

        private void selectBalanceButton_CheckedChanged(object sender, EventArgs e)
        {
            ButtonSelect = selector.Balance;
            populateListView();
        }

        private void GenreSelectorMenuItem_Click(object sender, EventArgs e)
        {

            using (GenreSelectorDialog dialog = new GenreSelectorDialog(options.genres))
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    // update this window
                    options.genres = dialog.selected;
                    populateListView();
                }
            }
        }

        private void FontSelectorMenuItem_Click(object sender, EventArgs e)
        {
			if(fontDialog1.ShowDialog(this)==DialogResult.OK) {
				setFont(fontDialog1.Font);
			}
        }
    }
}
