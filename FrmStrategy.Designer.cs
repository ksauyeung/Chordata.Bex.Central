namespace Chordata.Bex.Central
{
    partial class FrmStrategy
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
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.btnTakerTaker = new System.Windows.Forms.Button();
            this.btnMakerTaker = new System.Windows.Forms.Button();
            this.label6 = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.btnMakerTakerLeft = new System.Windows.Forms.Button();
            this.label2 = new System.Windows.Forms.Label();
            this.groupBox1.SuspendLayout();
            this.SuspendLayout();
            // 
            // groupBox1
            // 
            this.groupBox1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBox1.Controls.Add(this.btnMakerTakerLeft);
            this.groupBox1.Controls.Add(this.label2);
            this.groupBox1.Controls.Add(this.btnTakerTaker);
            this.groupBox1.Controls.Add(this.btnMakerTaker);
            this.groupBox1.Controls.Add(this.label6);
            this.groupBox1.Controls.Add(this.label1);
            this.groupBox1.Location = new System.Drawing.Point(12, 12);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(222, 157);
            this.groupBox1.TabIndex = 0;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Cash n Carry";
            // 
            // btnTakerTaker
            // 
            this.btnTakerTaker.Location = new System.Drawing.Point(111, 54);
            this.btnTakerTaker.Name = "btnTakerTaker";
            this.btnTakerTaker.Size = new System.Drawing.Size(75, 23);
            this.btnTakerTaker.TabIndex = 12;
            this.btnTakerTaker.Text = "Start";
            this.btnTakerTaker.UseVisualStyleBackColor = true;
            this.btnTakerTaker.Click += new System.EventHandler(this.btnTakerTaker_Click);
            // 
            // btnMakerTaker
            // 
            this.btnMakerTaker.Location = new System.Drawing.Point(111, 25);
            this.btnMakerTaker.Name = "btnMakerTaker";
            this.btnMakerTaker.Size = new System.Drawing.Size(75, 23);
            this.btnMakerTaker.TabIndex = 11;
            this.btnMakerTaker.Text = "Start";
            this.btnMakerTaker.UseVisualStyleBackColor = true;
            this.btnMakerTaker.Click += new System.EventHandler(this.btnMakerMaker_Click);
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(6, 30);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(68, 13);
            this.label6.TabIndex = 10;
            this.label6.Text = "Maker-Taker";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(10, 59);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(66, 13);
            this.label1.TabIndex = 0;
            this.label1.Text = "Taker-Taker";
            // 
            // btnMakerTakerLeft
            // 
            this.btnMakerTakerLeft.Location = new System.Drawing.Point(111, 94);
            this.btnMakerTakerLeft.Name = "btnMakerTakerLeft";
            this.btnMakerTakerLeft.Size = new System.Drawing.Size(75, 23);
            this.btnMakerTakerLeft.TabIndex = 14;
            this.btnMakerTakerLeft.Text = "Start";
            this.btnMakerTakerLeft.UseVisualStyleBackColor = true;
            this.btnMakerTakerLeft.Click += new System.EventHandler(this.btnMakerTakerLeft_Click);
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(6, 99);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(89, 13);
            this.label2.TabIndex = 13;
            this.label2.Text = "Maker-Taker-Left";
            // 
            // FrmStrategy
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(247, 175);
            this.Controls.Add(this.groupBox1);
            this.Name = "FrmStrategy";
            this.Text = "Strategy";
            this.Load += new System.EventHandler(this.FrmStrategy_Load);
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.Button btnTakerTaker;
        private System.Windows.Forms.Button btnMakerTaker;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Button btnMakerTakerLeft;
        private System.Windows.Forms.Label label2;
    }
}