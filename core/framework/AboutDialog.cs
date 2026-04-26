// 2008.11.12 YZ Modify about dialog
// 2008.11.12 YZ Add display version
// 2008.11.19 YZ Modify webBrowser control
// 2008.11.19 YZ Delete unnecessary function
using System;
using System.Diagnostics;
using System.Drawing;
using System.Collections;
using System.ComponentModel;
using System.Windows.Forms;
using freetrain.controls;
using freetrain.util;
using freetrain.DirectXWrapper;

#region YZ_20081112_ADDED
using System.Reflection;
#endregion

namespace freetrain.framework
{
	/// <summary>
	/// AboutDialog の概要の説明です。
	/// </summary>
	public class AboutDialog : System.Windows.Forms.Form
	{
		public static void show() {
			AboutDialog dlg = new AboutDialog();
			dlg.ShowDialog(MainWindow.mainWindow);
		}

		public AboutDialog() {
			InitializeComponent();

#region YZ_20081119_MODIFIED
//			browser.navigate("about:blank");
//			browser.docHostUIHandler = new DocHostUIHandlerImpl(this);
//			browser.navigate(ResourceUtil.findSystemResource("about.html"));
			browser.Navigate("about:blank");
			browser.Navigate(ResourceUtil.findSystemResource("about.html"));
#endregion

#region YZ_20081112_ADDED
            FileVersionInfo freeTrainVerInfo = FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location);
            label_version.Text = freeTrainVerInfo.FileVersion;
#endregion
		}

		protected override void OnLoad(System.EventArgs e) {
			using( WindowedDirectDraw dd = new WindowedDirectDraw(this) ) {
				this.size.Text = format(dd.availableVideoMemory)+"/"+format(dd.totalVideoMemory);
				this.displayMode.Text = dd.primarySurface.displayModeName;
				this.progressBar.Value = Math.Min( 10000,
					(int)(10000.0*dd.availableVideoMemory/dd.totalVideoMemory) );
			}
		}

		private string format( long value ) {
			value /= 1024;
			return value+"KB";
		}

		protected override void Dispose( bool disposing ) {
			if( disposing && components != null)
				components.Dispose();
			base.Dispose( disposing );
		}

		#region Windows Form Designer generated code

		private System.Windows.Forms.Label label3;
		private System.Windows.Forms.Button okButton;
		private System.Windows.Forms.ProgressBar progressBar;
		private System.Windows.Forms.Label size;
		private System.Windows.Forms.Label label4;
		private System.Windows.Forms.Label displayMode;
#region YZ_20081119_MODIFIED
//		private freetrain.controls.WebBrowser browser;
		private System.Windows.Forms.WebBrowser browser;
#endregion
		private System.Windows.Forms.Panel panel1;
        private Label label_version;
        private Label label2;
		private System.ComponentModel.Container components = null;
		/// <summary>
		/// デザイナ サポートに必要なメソッドです。このメソッドの内容を
		/// コード エディタで変更しないでください。
		/// </summary>
		private void InitializeComponent()
		{
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(AboutDialog));
            this.progressBar = new System.Windows.Forms.ProgressBar();
            this.label3 = new System.Windows.Forms.Label();
            this.okButton = new System.Windows.Forms.Button();
            this.size = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.displayMode = new System.Windows.Forms.Label();
            this.browser = new System.Windows.Forms.WebBrowser();
            this.panel1 = new System.Windows.Forms.Panel();
            this.label_version = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.panel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // progressBar
            // 
            this.progressBar.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.progressBar.Location = new System.Drawing.Point(8, 291);
            this.progressBar.Maximum = 10000;
            this.progressBar.Name = "progressBar";
            this.progressBar.Size = new System.Drawing.Size(371, 8);
            this.progressBar.TabIndex = 3;
            this.progressBar.Value = 30;
            // 
            // label3
            // 
            this.label3.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.label3.Location = new System.Drawing.Point(8, 272);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(96, 16);
            this.label3.TabIndex = 4;
            this.label3.Text = "VRAM空き容量：";
            this.label3.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // okButton
            // 
            this.okButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.okButton.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.okButton.Location = new System.Drawing.Point(299, 303);
            this.okButton.Name = "okButton";
            this.okButton.Size = new System.Drawing.Size(80, 24);
            this.okButton.TabIndex = 5;
            this.okButton.Text = "&OK";
            this.okButton.Click += new System.EventHandler(this.okButton_Click);
            // 
            // size
            // 
            this.size.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.size.Location = new System.Drawing.Point(104, 271);
            this.size.Name = "size";
            this.size.Size = new System.Drawing.Size(152, 16);
            this.size.TabIndex = 6;
            this.size.Text = "100KB/64MB";
            this.size.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // label4
            // 
            this.label4.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.label4.Location = new System.Drawing.Point(8, 258);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(96, 16);
            this.label4.TabIndex = 7;
            this.label4.Text = "画面モード：";
            this.label4.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // displayMode
            // 
            this.displayMode.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.displayMode.Location = new System.Drawing.Point(104, 256);
            this.displayMode.Name = "displayMode";
            this.displayMode.Size = new System.Drawing.Size(144, 16);
            this.displayMode.TabIndex = 8;
            this.displayMode.Text = "---";
            this.displayMode.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // browser
            // 
            this.browser.Dock = System.Windows.Forms.DockStyle.Fill;
            this.browser.Location = new System.Drawing.Point(0, 0);
            this.browser.Name = "browser";
            this.browser.Size = new System.Drawing.Size(371, 228);
            this.browser.TabIndex = 0;
            // 
            // panel1
            // 
            this.panel1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.panel1.Controls.Add(this.browser);
            this.panel1.Location = new System.Drawing.Point(8, 8);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(371, 228);
            this.panel1.TabIndex = 9;
            // 
            // label_version
            // 
            this.label_version.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.label_version.Location = new System.Drawing.Point(104, 242);
            this.label_version.Name = "label_version";
            this.label_version.Size = new System.Drawing.Size(144, 16);
            this.label_version.TabIndex = 11;
            this.label_version.Text = "1.0.3168.2178";
            this.label_version.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // label2
            // 
            this.label2.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.label2.Location = new System.Drawing.Point(8, 244);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(96, 16);
            this.label2.TabIndex = 10;
            this.label2.Text = "Version：";
            this.label2.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // AboutDialog
            // 
            this.AcceptButton = this.okButton;
            this.AutoScaleBaseSize = new System.Drawing.Size(5, 12);
            this.ClientSize = new System.Drawing.Size(389, 331);
            this.Controls.Add(this.label_version);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.panel1);
            this.Controls.Add(this.displayMode);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.size);
            this.Controls.Add(this.okButton);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.progressBar);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "AboutDialog";
            this.ShowInTaskbar = false;
            this.Text = "About FreeTrain EX Av";
            this.panel1.ResumeLayout(false);
            this.ResumeLayout(false);

		}
		#endregion

		private void okButton_Click(object sender, System.EventArgs e) {
			Close();
        }

#region YZ_20081119_DELETED
//      private void linkLabel1_LinkClicked(object sender, System.Windows.Forms.LinkLabelLinkClickedEventArgs e) {
//			UrlInvoker.openUrl( ((LinkLabel)sender).Text );
//      }
#endregion

    }
}
