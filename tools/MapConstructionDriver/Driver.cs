using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using freetrain.framework;

namespace Driver
{
	public class Driver
	{

		[STAThread]
		static void Main( string[] args ) 
		{
			// record the installation directory
			Core.installationDirectory =
				Directory.GetParent(Application.ExecutablePath).FullName;

			if( Debugger.IsAttached )
				run(args);
			else
				try {
					run(args);
				} catch( Exception e ) {
					ErrorMessageBox.show(null,"エラーが発生しました",e);
				}
		}

		private static void run( string[] args ) {

			// start the game
			Application.Run(new MainWindow(args,true));
		}
	}
}
