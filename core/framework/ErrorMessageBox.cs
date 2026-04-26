//***************************************************************************/
//* Project   : FreeTrain Ex-Aver Project                                   */
//* Package   : FreeTrain.Core.2008                                         */
//*                                                                         */
//* Namespace : freetrain.framework                                         */
//* Type      : [ ]Interface  [ ]Class  [*]Form  [ ]Form(Partial)           */
//* FileID    : ErrorMessageBox.cs                                          */
//* Detail    : Definition of error message box design                      */
//***************************************************************************/
//* FreeTrain        Copyright(C) 2002 -, Kohsuke Kawaguchi.                */
//* FreeTrainEx      Copyright(C) 2005 -, C477.                             */
//* FreeTrainEx-Aver Copyright(C) 2008 -, FreeTrain Ex-Aver Project.        */
//***************************************************************************/
//* 2008.11.28 YZ       Modified partial class                              */
//***************************************************************************/
using freetrain.controls;

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
		#region Windows Form Designer generated code
		private System.Windows.Forms.Label msg;
		private UrlLinkLabel linkLabel1;
		private System.Windows.Forms.PictureBox icon;
		private System.Windows.Forms.TextBox detail;
		private System.Windows.Forms.Button okButton;
		private System.ComponentModel.Container components = null;
		
		private void InitializeComponent()
		{
            this.icon = new System.Windows.Forms.PictureBox();
            this.detail = new System.Windows.Forms.TextBox();
            this.okButton = new System.Windows.Forms.Button();
            this.msg = new System.Windows.Forms.Label();
            this.linkLabel1 = new freetrain.controls.UrlLinkLabel();
            ((System.ComponentModel.ISupportInitialize)(this.icon)).BeginInit();
            this.SuspendLayout();
            // 
            // icon
            // 
            this.icon.Location = new System.Drawing.Point(19, 0);
            this.icon.Name = "icon";
            this.icon.Size = new System.Drawing.Size(58, 60);
            this.icon.SizeMode = System.Windows.Forms.PictureBoxSizeMode.CenterImage;
            this.icon.TabIndex = 0;
            this.icon.TabStop = false;
            // 
            // detail
            // 
            this.detail.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.detail.Location = new System.Drawing.Point(19, 60);
            this.detail.Multiline = true;
            this.detail.Name = "detail";
            this.detail.ReadOnly = true;
            this.detail.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.detail.Size = new System.Drawing.Size(362, 76);
            this.detail.TabIndex = 2;
            this.detail.Text = "detail";
            // 
            // okButton
            // 
            this.okButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.okButton.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.okButton.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.okButton.Location = new System.Drawing.Point(285, 141);
            this.okButton.Name = "okButton";
            this.okButton.Size = new System.Drawing.Size(96, 30);
            this.okButton.TabIndex = 3;
            this.okButton.Text = "&OK";
            this.okButton.Click += new System.EventHandler(this.okButton_Click);
            // 
            // msg
            // 
            this.msg.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.msg.Location = new System.Drawing.Point(86, 10);
            this.msg.Name = "msg";
            this.msg.Size = new System.Drawing.Size(295, 20);
            this.msg.TabIndex = 1;
            this.msg.Text = "エラーが発生しました";
            // 
            // linkLabel1
            // 
            this.linkLabel1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.linkLabel1.Location = new System.Drawing.Point(86, 30);
            this.linkLabel1.Name = "linkLabel1";
            this.linkLabel1.Size = new System.Drawing.Size(295, 20);
            this.linkLabel1.TabIndex = 4;
            this.linkLabel1.TabStop = true;
            this.linkLabel1.TargetUrl = "http://www.kohsuke.org/freetrain/wiki/pukiwiki.php?%A5%D0%A5%B0%CA%F3%B9%F0%A4%CE" +
                "%BC%EA%BD%E7";
            this.linkLabel1.Text = "バグを報告する";
            this.linkLabel1.TextAlign = System.Drawing.ContentAlignment.TopRight;
            this.linkLabel1.Visible = false;
            // 
            // ErrorMessageBox
            // 
            this.AutoScaleBaseSize = new System.Drawing.Size(6, 15);
            this.ClientSize = new System.Drawing.Size(400, 174);
            this.Controls.Add(this.linkLabel1);
            this.Controls.Add(this.okButton);
            this.Controls.Add(this.detail);
            this.Controls.Add(this.msg);
            this.Controls.Add(this.icon);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ErrorMessageBox";
            this.Text = "エラー";
            ((System.ComponentModel.ISupportInitialize)(this.icon)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

		}
		#endregion
	}
}
