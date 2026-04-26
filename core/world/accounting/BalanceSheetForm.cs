// 2008.11.19 YZ Change webBrowser control
// 2008.11.19 YZ Delete AxSHDoc and MsHtml
using System;
using System.Drawing;
using System.Collections;
using System.ComponentModel;
using System.Windows.Forms;
#region YZ_20081119_DELETED
//using AxSHDocVw;
//using MsHtmlHost;
#endregion
using freetrain.controls;
using freetrain.framework;
using freetrain.framework.plugin;

namespace freetrain.world.accounting
{
	/// <summary>
	/// Displays the balance sheet.
	/// </summary>
	public class BalanceSheetForm : Form
	{
		#region singleton instance
		public static void create() {
			if( theInstance==null ) {
				theInstance = new BalanceSheetForm();
				theInstance.Show();
			}
			theInstance.BringToFront();
        }

		private static Form theInstance = null;
		
		protected override void OnClosed(EventArgs e) {
			base.OnClosed(e);
			theInstance = null;
		}
		#endregion


		private System.ComponentModel.Container components = null;
#region YZ_20081119_MODIFIED
//		private freetrain.controls.WebBrowser webBrowser;
		private System.Windows.Forms.WebBrowser webBrowser;
#endregion

		private BalanceSheetForm() {
//			this.MdiParent = MainWindow.mainWindow;
			InitializeComponent();

//            object flags = 0;
//            object targetFrame = String.Empty;
//            object postData = String.Empty;
//            object headers = String.Empty;
//            webBrowser.Navigate("about:hello", ref flags, ref targetFrame, ref postData, ref headers);

#region YZ_20081119_MODIFIED
//			webBrowser.navigate("about:blank");
//			webBrowser.docHostUIHandler = new DocHostUIHandlerImpl(this);
//			webBrowser.navigate(ResourceUtil.findSystemResource("balanceSheet.html"));
            webBrowser.Navigate("about:blank");
			webBrowser.Navigate(ResourceUtil.findSystemResource("balanceSheet.html"));
#endregion
        }

		protected override void Dispose( bool disposing ) {
			if( disposing && components != null)
				components.Dispose();
			base.Dispose( disposing );
		}

		#region Windows Form Designer generated code
		private void InitializeComponent() {
        this.webBrowser = new System.Windows.Forms.WebBrowser();
        this.SuspendLayout();
        // 
        // webBrowser
        // 
        this.webBrowser.Dock = System.Windows.Forms.DockStyle.Fill;
        this.webBrowser.Location = new System.Drawing.Point(0,0);
        this.webBrowser.Name = "webBrowser";
        this.webBrowser.Size = new System.Drawing.Size(592,206);
        this.webBrowser.TabIndex = 0;
        // 
        // BalanceSheetForm
        // 
        this.AutoScaleBaseSize = new System.Drawing.Size(6,15);
        this.ClientSize = new System.Drawing.Size(592,206);
        this.Controls.Add(this.webBrowser);
        this.Name = "BalanceSheetForm";
        this.Text = "バランスシート";
        this.ResumeLayout(false);

		}
		#endregion
	}
}
