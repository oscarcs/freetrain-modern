//***************************************************************************/
//* Project   : FreeTrain Ex-Aver Project                                   */
//* Package   : FreeTrain.Core.2008                                         */
//*                                                                         */
//* Namespace : freetrain.framework                                         */
//* Type      : [ ]Interface  [ ]Class  [ ]Form  [*]Form(Partial)           */
//* FileID    : ErrorMessageBox.cs                                          */
//* Detail    : Definition of error message box constructor/destructor and  */
//*             event                                                       */
//***************************************************************************/
//* FreeTrain        Copyright(C) 2002 -, Kohsuke Kawaguchi.                */
//* FreeTrainEx      Copyright(C) 2005 -, C477.                             */
//* FreeTrainEx-Aver Copyright(C) 2008 -, FreeTrain Ex-Aver Project.        */
//***************************************************************************/
//* 2008.11.28 YZ       Modified partial class                              */
//* 2008.11.28 YZ       Added output of error report                        */
//* 2009.05.06 YZ       内部例外のエラーメッセージ編集が永久ループになって  */
//*                     いたため、修正                                      */
//***************************************************************************/
using System;
using System.Drawing;
using System.Collections;
using System.ComponentModel;
using System.Windows.Forms;
using freetrain.controls;
using System.IO;
using freetrain.world;

namespace freetrain.framework
{
    /// <summary>
    /// FreeTrainEXAv error message box
    /// </summary>
    /// <remarks>
    /// Definition of exception error message box for FreeTrainEXAv.
    /// Show exception error message, output of error report.
    /// </remarks>
	public sealed partial class ErrorMessageBox : System.Windows.Forms.Form
	{
        //******************/
        //* CONSTANT VALUE */
        //******************/

        /// <summary>
        /// Default plugin directory name
        /// </summary>
        /// <remarks>
        /// This constant value is default plugin directory name.
        /// </remarks>
        /// <permission cref="System.Security.PermissionSet">
        /// Can access it only from this class.
        /// </permission>
        private const string ErrorReportName = "\\errorreport_{0,4:D4}{1,2:D2}{2,2:D2}{3,2:D2}{4,2:D2}{5,2:D2}.log";

        private const string ErrorDataName   = "\\errordata_{0,4:D4}{1,2:D2}{2,2:D2}{3,2:D2}{4,2:D2}{5,2:D2}.ftgt";

        //****************/
        //* MEMBER FIELD */
        //****************/

        /// <summary>
        /// Exception
        /// </summary>
        /// <remarks>
        /// This member field is exception object which occurred.
        /// </remarks>
        /// <permission cref="System.Security.PermissionSet">
        /// Can access it from anywhere.
        /// </permission>
        private Exception exception;

        //**************************/
        //* CONSTRUCTOR/DESTRUCTOR */
        //**************************/

        /// <summary>
        /// Parameter constructor
        /// </summary>
        /// <remarks>
        /// This is constructor of error message box with parameter.
        /// This constructer sets error message and stacktrace.
        /// </remarks>
        /// <param name="caption">
        /// Error message box caption string
        /// </param>
        /// <param name="e">
        /// Exception object
        /// </param>
        /// <permission cref="System.Security.PermissionSet">
        /// Can access it only from this class.
        /// </permission>
        private ErrorMessageBox(string caption, Exception e) {
			this.exception = e;                                                 // Sets exception object
			this.Text = caption;                                                // Sets form caption

			InitializeComponent();                                              // Initializes component

			base.Icon =  SystemIcons.Error;                                     // Sets form icon
			icon.Image = SystemIcons.Error.ToBitmap();                          // Sets form icon image

			detail.Text = e.Message + "\n" + e.StackTrace;                      // Sets exception message and stacktrace in detailed message

    		Exception exp = e.InnerException;                                   // Sets inner exception object 
			while(true) {                                                       // Loop
                if (exp == null) {                                              // If inner exception object doesn't exist
                    break;                                                      // Break loop
                } else {
			                                                                    // Adds exception message and stacktrace in detailed message
    				detail.Text = detail.Text + "\n" + exp.Message + "\n" + exp.StackTrace;
                }

                exp = exp.InnerException;                                       // Sets inner exception object 
			}

			detail.Select(0,0);                                                 // Selects detailed text
		}

        /// <summary>
        /// Dispose error message box
        /// </summary>
        /// <remarks>
        /// The method cancel set game options.
        /// This is destructor of error message box.
        /// This destructor dispose error message box.
        /// </remarks>
        /// <param name="disposing">
        /// Dispose flag
        /// true :dispose manage and unmanage resource
        /// false:dispose unmanage resource
        /// </param>
        /// <permission cref="System.Security.PermissionSet">
        /// Can access it only from this class and inheritance class.
        /// </permission>
        protected override void Dispose(bool disposing) {
            if (disposing && components != null) {                              // If dispose flag equal true and component is not null
                components.Dispose();                                           // Dispose component
            }

            base.Dispose(disposing);                                            // Dispose base class
        }

        //******************/
        //* EVENT DELEGATE */
        //******************/

        /// <summary>
        /// Click ok button
        /// </summary>
        /// <remarks>
        /// This is click event of ok button.
        /// The method output error report and error data.
        /// </remarks>
        /// <param name="sender">
        /// Event send object
        /// </param>
        /// <param name="e">
        /// Event argument
        /// </param>
        /// <permission cref="System.Security.PermissionSet">
        /// Can access it only from this class.
        /// </permission>
        private void okButton_Click(object sender, System.EventArgs e)
        {
                                                                                // Create error report file information
            FileInfo finfo = new FileInfo(Application.StartupPath + string.Format(ErrorReportName, DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second));
            using(Stream fs = finfo.OpenWrite()) {                              // Open error report stream
                                                                                // Convert detailed text to byte data
                byte[] byteData = System.Text.Encoding.UTF8.GetBytes(detail.Text);
                
                fs.Write(byteData, 0, byteData.Length);                         // Output detailed text for error report stream
                fs.Close();                                                     // Close error report stream
            }

                                                                                // Create error data file information
            FileInfo finfo2 = new FileInfo(Application.StartupPath + string.Format(ErrorDataName, DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second));
			using (Stream fs2 = finfo2.OpenWrite()) {                           // Open error data stream
    			fs2.WriteByte((byte)'U');                                       // Output header for error data stream
	    		fs2.WriteByte((byte)'C');                                       // Output header for error data stream
                                                                                // Output world data for error data stream
		    	World.world.save(new System.Runtime.Serialization.Formatters.Soap.SoapFormatter(), fs2);
			    fs2.Close();                                                    // Close error data stream
            }
        }

        //****************/
        //* CLASS METHOD */
        //****************/

        /// <summary>
        /// Show exception error message box
        /// </summary>
        /// <remarks>
        /// This method show exception error message box.
        /// </remarks>
        /// <param name="owner">
        /// Error message box owner object
        /// </param>
        /// <param name="caption">
        /// Error message box caption string
        /// </param>
        /// <param name="e">
        /// Exception object
        /// </param>
        /// <permission cref="System.Security.PermissionSet">
        /// Can access it only from this class.
        /// </permission>
		public static void show(IWin32Window owner, string caption, Exception e) {
			using(Form frmErrorMessageBox = new ErrorMessageBox(caption, e) ) {
				frmErrorMessageBox.ShowDialog(owner);
			}
		}

        //*******************/
        //* INSTANCE METHOD */
        //*******************/
	}
}
