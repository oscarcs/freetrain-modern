using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Threading;
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
					Console.Error.WriteLine(e);
					ErrorMessageBox.show(null,"エラーが発生しました",e);
				}
		}

		private static void run( string[] args ) {

			// start the game
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += new ThreadExceptionEventHandler(threadExceptionHandler);

			Application.Run(new MainWindow(args,false));
		}

        private static void threadExceptionHandler(object sender, ThreadExceptionEventArgs e) {
			Console.Error.WriteLine(e.Exception);
		    ErrorMessageBox.show(null, "エラーが発生しました", e.Exception);
        }
	}
}
