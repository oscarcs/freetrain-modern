using System;
using System.Reflection;
using System.Windows.Forms;
using System.Xml;
using freetrain.contributions.others;
using freetrain.framework;
using freetrain.framework.plugin;

namespace freetrain.views
{
	/// <summary>
	/// 
	/// </summary>
	public class StationListPlugIn : MenuContribution
	{
        /// <summary>StationListPlugIn constructor</summary>
		public StationListPlugIn(XmlElement e) : base(e) {}

        /// <summary>Merge StationListPlugIn to main menu</summary>
		public override void mergeMenu(MainMenu containerMenu) {
			MenuItem item = new MenuItem();
			item.Text = "駅一覧";
			item.Click += new System.EventHandler(onClick);

			containerMenu.MenuItems[1].MenuItems.Add(item);
		}

		private void onClick(object sender, EventArgs args) {
			Form form = new StationList();
			form.MdiParent = MainWindow.mainWindow;
			form.Show();
		}
	}
}
