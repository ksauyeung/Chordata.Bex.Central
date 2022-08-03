namespace Chordata.Bex.Central
{
    partial class FrmMessage
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.tbMessage = new System.Windows.Forms.RichTextBox();
            this.SuspendLayout();
            // 
            // tbMessage
            // 
            this.tbMessage.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tbMessage.Location = new System.Drawing.Point(0, 0);
            this.tbMessage.Name = "tbMessage";
            this.tbMessage.Size = new System.Drawing.Size(1235, 142);
            this.tbMessage.TabIndex = 0;
            this.tbMessage.Text = "";
            // 
            // FrmMessage
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1235, 142);
            this.Controls.Add(this.tbMessage);
            this.Name = "FrmMessage";
            this.Text = "Messages";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.FrmMessage_FormClosing);
            this.Load += new System.EventHandler(this.FrmMessage_Load);
            this.ResumeLayout(false);

        }

        #endregion

        internal System.Windows.Forms.RichTextBox tbMessage;
    }
}