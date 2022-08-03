namespace Chordata.Bex.Central
{
    partial class FrmExchanges
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
            this.label4 = new System.Windows.Forms.Label();
            this.textBox2 = new System.Windows.Forms.TextBox();
            this.label13 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.tbApiKey = new System.Windows.Forms.TextBox();
            this.label12 = new System.Windows.Forms.Label();
            this.textBox1 = new System.Windows.Forms.TextBox();
            this.tbApiSecret = new System.Windows.Forms.TextBox();
            this.numericUpDown1 = new System.Windows.Forms.NumericUpDown();
            this.label2 = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.label5 = new System.Windows.Forms.Label();
            this.textBox3 = new System.Windows.Forms.TextBox();
            this.label6 = new System.Windows.Forms.Label();
            this.label7 = new System.Windows.Forms.Label();
            this.textBox4 = new System.Windows.Forms.TextBox();
            this.label8 = new System.Windows.Forms.Label();
            this.textBox5 = new System.Windows.Forms.TextBox();
            this.textBox6 = new System.Windows.Forms.TextBox();
            this.numericUpDown2 = new System.Windows.Forms.NumericUpDown();
            this.label9 = new System.Windows.Forms.Label();
            this.groupBox1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDown1)).BeginInit();
            this.groupBox2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDown2)).BeginInit();
            this.SuspendLayout();
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.label4);
            this.groupBox1.Controls.Add(this.textBox2);
            this.groupBox1.Controls.Add(this.label13);
            this.groupBox1.Controls.Add(this.label3);
            this.groupBox1.Controls.Add(this.tbApiKey);
            this.groupBox1.Controls.Add(this.label12);
            this.groupBox1.Controls.Add(this.textBox1);
            this.groupBox1.Controls.Add(this.tbApiSecret);
            this.groupBox1.Controls.Add(this.numericUpDown1);
            this.groupBox1.Controls.Add(this.label2);
            this.groupBox1.Location = new System.Drawing.Point(12, 12);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(389, 159);
            this.groupBox1.TabIndex = 0;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "BitMEX";
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(34, 23);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(55, 13);
            this.label4.TabIndex = 72;
            this.label4.Text = "REST Url:";
            // 
            // textBox2
            // 
            this.textBox2.DataBindings.Add(new System.Windows.Forms.Binding("Text", global::Chordata.Bex.Central.Properties.Settings.Default, "BitMEX_WebSocket", true, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged));
            this.textBox2.Location = new System.Drawing.Point(95, 46);
            this.textBox2.Name = "textBox2";
            this.textBox2.Size = new System.Drawing.Size(272, 20);
            this.textBox2.TabIndex = 71;
            this.textBox2.Text = global::Chordata.Bex.Central.Properties.Settings.Default.BitMEX_WebSocket;
            // 
            // label13
            // 
            this.label13.AutoSize = true;
            this.label13.Location = new System.Drawing.Point(28, 101);
            this.label13.Name = "label13";
            this.label13.Size = new System.Drawing.Size(61, 13);
            this.label13.TabIndex = 65;
            this.label13.Text = "API Secret:";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(6, 49);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(83, 13);
            this.label3.TabIndex = 70;
            this.label3.Text = "WebSocket Url:";
            // 
            // tbApiKey
            // 
            this.tbApiKey.DataBindings.Add(new System.Windows.Forms.Binding("Text", global::Chordata.Bex.Central.Properties.Settings.Default, "BitMEX_ApiKey", true, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged));
            this.tbApiKey.Location = new System.Drawing.Point(95, 72);
            this.tbApiKey.Name = "tbApiKey";
            this.tbApiKey.Size = new System.Drawing.Size(272, 20);
            this.tbApiKey.TabIndex = 62;
            this.tbApiKey.Text = global::Chordata.Bex.Central.Properties.Settings.Default.BitMEX_ApiKey;
            // 
            // label12
            // 
            this.label12.AutoSize = true;
            this.label12.Location = new System.Drawing.Point(41, 75);
            this.label12.Name = "label12";
            this.label12.Size = new System.Drawing.Size(48, 13);
            this.label12.TabIndex = 63;
            this.label12.Text = "API Key:";
            // 
            // textBox1
            // 
            this.textBox1.DataBindings.Add(new System.Windows.Forms.Binding("Text", global::Chordata.Bex.Central.Properties.Settings.Default, "BitMEX_REST", true, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged));
            this.textBox1.Location = new System.Drawing.Point(95, 20);
            this.textBox1.Name = "textBox1";
            this.textBox1.Size = new System.Drawing.Size(272, 20);
            this.textBox1.TabIndex = 69;
            this.textBox1.Text = global::Chordata.Bex.Central.Properties.Settings.Default.BitMEX_REST;
            // 
            // tbApiSecret
            // 
            this.tbApiSecret.DataBindings.Add(new System.Windows.Forms.Binding("Text", global::Chordata.Bex.Central.Properties.Settings.Default, "BitMEX_ApiSecret", true, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged));
            this.tbApiSecret.Location = new System.Drawing.Point(95, 98);
            this.tbApiSecret.Name = "tbApiSecret";
            this.tbApiSecret.PasswordChar = 'x';
            this.tbApiSecret.Size = new System.Drawing.Size(272, 20);
            this.tbApiSecret.TabIndex = 64;
            this.tbApiSecret.Text = global::Chordata.Bex.Central.Properties.Settings.Default.BitMEX_ApiSecret;
            // 
            // numericUpDown1
            // 
            this.numericUpDown1.DataBindings.Add(new System.Windows.Forms.Binding("Value", global::Chordata.Bex.Central.Properties.Settings.Default, "BitMEX_TakerFee", true, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged));
            this.numericUpDown1.DecimalPlaces = 6;
            this.numericUpDown1.Location = new System.Drawing.Point(95, 124);
            this.numericUpDown1.Minimum = new decimal(new int[] {
            100,
            0,
            0,
            -2147483648});
            this.numericUpDown1.Name = "numericUpDown1";
            this.numericUpDown1.Size = new System.Drawing.Size(120, 20);
            this.numericUpDown1.TabIndex = 68;
            this.numericUpDown1.Value = global::Chordata.Bex.Central.Properties.Settings.Default.BitMEX_TakerFee;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(30, 126);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(59, 13);
            this.label2.TabIndex = 67;
            this.label2.Text = "Taker Fee:";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(240, 174);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(319, 13);
            this.label1.TabIndex = 1;
            this.label1.Text = "Warning: Changing API Key and Secret requires restart of the API.";
            // 
            // groupBox2
            // 
            this.groupBox2.Controls.Add(this.label5);
            this.groupBox2.Controls.Add(this.textBox3);
            this.groupBox2.Controls.Add(this.label6);
            this.groupBox2.Controls.Add(this.label7);
            this.groupBox2.Controls.Add(this.textBox4);
            this.groupBox2.Controls.Add(this.label8);
            this.groupBox2.Controls.Add(this.textBox5);
            this.groupBox2.Controls.Add(this.textBox6);
            this.groupBox2.Controls.Add(this.numericUpDown2);
            this.groupBox2.Controls.Add(this.label9);
            this.groupBox2.Location = new System.Drawing.Point(407, 12);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(389, 159);
            this.groupBox2.TabIndex = 2;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "Poloniex";
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(34, 23);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(55, 13);
            this.label5.TabIndex = 72;
            this.label5.Text = "REST Url:";
            // 
            // textBox3
            // 
            this.textBox3.DataBindings.Add(new System.Windows.Forms.Binding("Text", global::Chordata.Bex.Central.Properties.Settings.Default, "Poloniex_WebSocket", true, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged));
            this.textBox3.Location = new System.Drawing.Point(95, 46);
            this.textBox3.Name = "textBox3";
            this.textBox3.Size = new System.Drawing.Size(272, 20);
            this.textBox3.TabIndex = 71;
            this.textBox3.Text = global::Chordata.Bex.Central.Properties.Settings.Default.Poloniex_WebSocket;
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(28, 101);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(61, 13);
            this.label6.TabIndex = 65;
            this.label6.Text = "API Secret:";
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(6, 49);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(83, 13);
            this.label7.TabIndex = 70;
            this.label7.Text = "WebSocket Url:";
            // 
            // textBox4
            // 
            this.textBox4.DataBindings.Add(new System.Windows.Forms.Binding("Text", global::Chordata.Bex.Central.Properties.Settings.Default, "Poloniex_ApiKey", true, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged));
            this.textBox4.Location = new System.Drawing.Point(95, 72);
            this.textBox4.Name = "textBox4";
            this.textBox4.Size = new System.Drawing.Size(272, 20);
            this.textBox4.TabIndex = 62;
            this.textBox4.Text = global::Chordata.Bex.Central.Properties.Settings.Default.Poloniex_ApiKey;
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.Location = new System.Drawing.Point(41, 75);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(48, 13);
            this.label8.TabIndex = 63;
            this.label8.Text = "API Key:";
            // 
            // textBox5
            // 
            this.textBox5.DataBindings.Add(new System.Windows.Forms.Binding("Text", global::Chordata.Bex.Central.Properties.Settings.Default, "Poloniex_REST", true, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged));
            this.textBox5.Location = new System.Drawing.Point(95, 20);
            this.textBox5.Name = "textBox5";
            this.textBox5.Size = new System.Drawing.Size(272, 20);
            this.textBox5.TabIndex = 69;
            this.textBox5.Text = global::Chordata.Bex.Central.Properties.Settings.Default.Poloniex_REST;
            // 
            // textBox6
            // 
            this.textBox6.DataBindings.Add(new System.Windows.Forms.Binding("Text", global::Chordata.Bex.Central.Properties.Settings.Default, "Poloniex_ApiSecret", true, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged));
            this.textBox6.Location = new System.Drawing.Point(95, 98);
            this.textBox6.Name = "textBox6";
            this.textBox6.PasswordChar = 'x';
            this.textBox6.Size = new System.Drawing.Size(272, 20);
            this.textBox6.TabIndex = 64;
            this.textBox6.Text = global::Chordata.Bex.Central.Properties.Settings.Default.Poloniex_ApiSecret;
            // 
            // numericUpDown2
            // 
            this.numericUpDown2.DataBindings.Add(new System.Windows.Forms.Binding("Value", global::Chordata.Bex.Central.Properties.Settings.Default, "Poloniex_TakerFee", true, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged));
            this.numericUpDown2.DecimalPlaces = 6;
            this.numericUpDown2.Location = new System.Drawing.Point(95, 124);
            this.numericUpDown2.Minimum = new decimal(new int[] {
            100,
            0,
            0,
            -2147483648});
            this.numericUpDown2.Name = "numericUpDown2";
            this.numericUpDown2.Size = new System.Drawing.Size(120, 20);
            this.numericUpDown2.TabIndex = 68;
            this.numericUpDown2.Value = global::Chordata.Bex.Central.Properties.Settings.Default.Poloniex_TakerFee;
            // 
            // label9
            // 
            this.label9.AutoSize = true;
            this.label9.Location = new System.Drawing.Point(30, 126);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(59, 13);
            this.label9.TabIndex = 67;
            this.label9.Text = "Taker Fee:";
            // 
            // FrmExchanges
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(809, 204);
            this.Controls.Add(this.groupBox2);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.groupBox1);
            this.Name = "FrmExchanges";
            this.Text = "Exchanges";
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDown1)).EndInit();
            this.groupBox2.ResumeLayout(false);
            this.groupBox2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDown2)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label13;
        private System.Windows.Forms.TextBox tbApiSecret;
        private System.Windows.Forms.Label label12;
        private System.Windows.Forms.TextBox tbApiKey;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.TextBox textBox2;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TextBox textBox1;
        private System.Windows.Forms.NumericUpDown numericUpDown1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.TextBox textBox3;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.TextBox textBox4;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.TextBox textBox5;
        private System.Windows.Forms.TextBox textBox6;
        private System.Windows.Forms.NumericUpDown numericUpDown2;
        private System.Windows.Forms.Label label9;
    }
}