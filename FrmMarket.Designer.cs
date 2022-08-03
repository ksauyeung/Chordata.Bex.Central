namespace Chordata.Bex.Central
{
    partial class FrmMarket
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
            this.lblLastIndexPrice = new System.Windows.Forms.Label();
            this.label8 = new System.Windows.Forms.Label();
            this.label7 = new System.Windows.Forms.Label();
            this.lblLastPrice = new System.Windows.Forms.Label();
            this.label6 = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.tbMessages = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.tbBuyQty = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.tbSellQty = new System.Windows.Forms.TextBox();
            this.btnConnect = new System.Windows.Forms.Button();
            this.btnBuy = new System.Windows.Forms.Button();
            this.btnSell = new System.Windows.Forms.Button();
            this.btnSubscribe = new System.Windows.Forms.Button();
            this.dgvMessage = new System.Windows.Forms.DataGridView();
            this.colBuyTotalAmount = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colBuyAmount = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colBuyPrice = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colSellPrice = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colSellAmount = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colSellTotalAmount = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.lblStatus = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.tbCurrentSymbol = new System.Windows.Forms.TextBox();
            this.label5 = new System.Windows.Forms.Label();
            this.btnOrders = new System.Windows.Forms.Button();
            this.tbAmendPrice = new System.Windows.Forms.TextBox();
            this.label11 = new System.Windows.Forms.Label();
            this.label10 = new System.Windows.Forms.Label();
            this.label9 = new System.Windows.Forms.Label();
            this.amendQty = new System.Windows.Forms.TextBox();
            this.btnAmend = new System.Windows.Forms.Button();
            this.tbCancelOrder = new System.Windows.Forms.TextBox();
            this.btnCancel = new System.Windows.Forms.Button();
            this.label14 = new System.Windows.Forms.Label();
            this.label15 = new System.Windows.Forms.Label();
            this.tbBuyPrice = new System.Windows.Forms.TextBox();
            this.tbSellPrice = new System.Windows.Forms.TextBox();
            this.cbSymbol = new System.Windows.Forms.ComboBox();
            ((System.ComponentModel.ISupportInitialize)(this.dgvMessage)).BeginInit();
            this.SuspendLayout();
            // 
            // lblLastIndexPrice
            // 
            this.lblLastIndexPrice.AutoSize = true;
            this.lblLastIndexPrice.Location = new System.Drawing.Point(311, 101);
            this.lblLastIndexPrice.Name = "lblLastIndexPrice";
            this.lblLastIndexPrice.Size = new System.Drawing.Size(22, 13);
            this.lblLastIndexPrice.TabIndex = 81;
            this.lblLastIndexPrice.Text = "0.0";
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.Location = new System.Drawing.Point(36, 101);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(57, 13);
            this.label8.TabIndex = 80;
            this.label8.Text = "Last Price;";
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(219, 101);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(86, 13);
            this.label7.TabIndex = 79;
            this.label7.Text = "Last Index Price:";
            // 
            // lblLastPrice
            // 
            this.lblLastPrice.AutoSize = true;
            this.lblLastPrice.Location = new System.Drawing.Point(99, 101);
            this.lblLastPrice.Name = "lblLastPrice";
            this.lblLastPrice.Size = new System.Drawing.Size(22, 13);
            this.lblLastPrice.TabIndex = 78;
            this.lblLastPrice.Text = "0.0";
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(49, 17);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(44, 13);
            this.label6.TabIndex = 76;
            this.label6.Text = "Symbol:";
            // 
            // label4
            // 
            this.label4.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(11, 365);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(58, 13);
            this.label4.TabIndex = 75;
            this.label4.Text = "Messages:";
            // 
            // tbMessages
            // 
            this.tbMessages.AcceptsReturn = true;
            this.tbMessages.AcceptsTab = true;
            this.tbMessages.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tbMessages.Location = new System.Drawing.Point(1, 381);
            this.tbMessages.Multiline = true;
            this.tbMessages.Name = "tbMessages";
            this.tbMessages.Size = new System.Drawing.Size(641, 142);
            this.tbMessages.TabIndex = 74;
            this.tbMessages.WordWrap = false;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(302, 17);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(47, 13);
            this.label3.TabIndex = 72;
            this.label3.Text = "Buy Qty:";
            // 
            // tbBuyQty
            // 
            this.tbBuyQty.Location = new System.Drawing.Point(357, 14);
            this.tbBuyQty.Name = "tbBuyQty";
            this.tbBuyQty.Size = new System.Drawing.Size(64, 20);
            this.tbBuyQty.TabIndex = 71;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(302, 43);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(46, 13);
            this.label2.TabIndex = 70;
            this.label2.Text = "Sell Qty:";
            // 
            // tbSellQty
            // 
            this.tbSellQty.Location = new System.Drawing.Point(357, 40);
            this.tbSellQty.Name = "tbSellQty";
            this.tbSellQty.Size = new System.Drawing.Size(64, 20);
            this.tbSellQty.TabIndex = 69;
            // 
            // btnConnect
            // 
            this.btnConnect.Location = new System.Drawing.Point(557, 96);
            this.btnConnect.Name = "btnConnect";
            this.btnConnect.Size = new System.Drawing.Size(75, 23);
            this.btnConnect.TabIndex = 68;
            this.btnConnect.Text = "Connect";
            this.btnConnect.UseVisualStyleBackColor = true;
            this.btnConnect.Click += new System.EventHandler(this.btnConnect_Click);
            // 
            // btnBuy
            // 
            this.btnBuy.Location = new System.Drawing.Point(557, 12);
            this.btnBuy.Name = "btnBuy";
            this.btnBuy.Size = new System.Drawing.Size(75, 23);
            this.btnBuy.TabIndex = 67;
            this.btnBuy.Text = "Buy";
            this.btnBuy.UseVisualStyleBackColor = true;
            this.btnBuy.Click += new System.EventHandler(this.btnBuy_Click);
            // 
            // btnSell
            // 
            this.btnSell.Location = new System.Drawing.Point(557, 38);
            this.btnSell.Name = "btnSell";
            this.btnSell.Size = new System.Drawing.Size(75, 23);
            this.btnSell.TabIndex = 66;
            this.btnSell.Text = "Sell";
            this.btnSell.UseVisualStyleBackColor = true;
            this.btnSell.Click += new System.EventHandler(this.btnSell_Click);
            // 
            // btnSubscribe
            // 
            this.btnSubscribe.Location = new System.Drawing.Point(201, 12);
            this.btnSubscribe.Name = "btnSubscribe";
            this.btnSubscribe.Size = new System.Drawing.Size(75, 23);
            this.btnSubscribe.TabIndex = 65;
            this.btnSubscribe.Text = "Start";
            this.btnSubscribe.UseVisualStyleBackColor = true;
            this.btnSubscribe.Click += new System.EventHandler(this.btnSubscribe_Click);
            // 
            // dgvMessage
            // 
            this.dgvMessage.AllowUserToAddRows = false;
            this.dgvMessage.AllowUserToDeleteRows = false;
            this.dgvMessage.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.dgvMessage.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvMessage.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colBuyTotalAmount,
            this.colBuyAmount,
            this.colBuyPrice,
            this.colSellPrice,
            this.colSellAmount,
            this.colSellTotalAmount});
            this.dgvMessage.Location = new System.Drawing.Point(1, 125);
            this.dgvMessage.Name = "dgvMessage";
            this.dgvMessage.ReadOnly = true;
            this.dgvMessage.RowHeadersWidth = 21;
            this.dgvMessage.Size = new System.Drawing.Size(641, 237);
            this.dgvMessage.TabIndex = 64;
            this.dgvMessage.CellClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.dgvMessage_CellClick);
            this.dgvMessage.CellValueChanged += new System.Windows.Forms.DataGridViewCellEventHandler(this.dgvMessage_CellValueChanged);
            // 
            // colBuyTotalAmount
            // 
            this.colBuyTotalAmount.HeaderText = "Total Buy Amt";
            this.colBuyTotalAmount.Name = "colBuyTotalAmount";
            this.colBuyTotalAmount.ReadOnly = true;
            // 
            // colBuyAmount
            // 
            this.colBuyAmount.HeaderText = "Buy Amount";
            this.colBuyAmount.Name = "colBuyAmount";
            this.colBuyAmount.ReadOnly = true;
            // 
            // colBuyPrice
            // 
            this.colBuyPrice.HeaderText = "Buy Price";
            this.colBuyPrice.Name = "colBuyPrice";
            this.colBuyPrice.ReadOnly = true;
            // 
            // colSellPrice
            // 
            this.colSellPrice.HeaderText = "Sell Price";
            this.colSellPrice.Name = "colSellPrice";
            this.colSellPrice.ReadOnly = true;
            // 
            // colSellAmount
            // 
            this.colSellAmount.HeaderText = "Sell Amount";
            this.colSellAmount.Name = "colSellAmount";
            this.colSellAmount.ReadOnly = true;
            // 
            // colSellTotalAmount
            // 
            this.colSellTotalAmount.HeaderText = "Total Sell Amt";
            this.colSellTotalAmount.Name = "colSellTotalAmount";
            this.colSellTotalAmount.ReadOnly = true;
            // 
            // lblStatus
            // 
            this.lblStatus.AutoSize = true;
            this.lblStatus.Location = new System.Drawing.Point(473, 101);
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Size = new System.Drawing.Size(73, 13);
            this.lblStatus.TabIndex = 63;
            this.lblStatus.Text = "Disconnected";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(427, 101);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(40, 13);
            this.label1.TabIndex = 62;
            this.label1.Text = "Status:";
            // 
            // tbCurrentSymbol
            // 
            this.tbCurrentSymbol.Location = new System.Drawing.Point(99, 40);
            this.tbCurrentSymbol.Name = "tbCurrentSymbol";
            this.tbCurrentSymbol.ReadOnly = true;
            this.tbCurrentSymbol.Size = new System.Drawing.Size(96, 20);
            this.tbCurrentSymbol.TabIndex = 91;
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(12, 43);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(81, 13);
            this.label5.TabIndex = 92;
            this.label5.Text = "Current Symbol:";
            // 
            // btnOrders
            // 
            this.btnOrders.Location = new System.Drawing.Point(201, 38);
            this.btnOrders.Name = "btnOrders";
            this.btnOrders.Size = new System.Drawing.Size(75, 23);
            this.btnOrders.TabIndex = 96;
            this.btnOrders.Text = "Orders";
            this.btnOrders.UseVisualStyleBackColor = true;
            this.btnOrders.Click += new System.EventHandler(this.btnOrders_Click);
            // 
            // tbAmendPrice
            // 
            this.tbAmendPrice.Location = new System.Drawing.Point(412, 69);
            this.tbAmendPrice.Name = "tbAmendPrice";
            this.tbAmendPrice.Size = new System.Drawing.Size(58, 20);
            this.tbAmendPrice.TabIndex = 111;
            // 
            // label11
            // 
            this.label11.AutoSize = true;
            this.label11.Location = new System.Drawing.Point(359, 72);
            this.label11.Name = "label11";
            this.label11.Size = new System.Drawing.Size(47, 13);
            this.label11.TabIndex = 110;
            this.label11.Text = "New Px:";
            // 
            // label10
            // 
            this.label10.AutoSize = true;
            this.label10.Location = new System.Drawing.Point(282, 72);
            this.label10.Name = "label10";
            this.label10.Size = new System.Drawing.Size(26, 13);
            this.label10.TabIndex = 109;
            this.label10.Text = "Qty:";
            // 
            // label9
            // 
            this.label9.AutoSize = true;
            this.label9.Location = new System.Drawing.Point(7, 72);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(86, 13);
            this.label9.TabIndex = 108;
            this.label9.Text = "Amend Order ID:";
            // 
            // amendQty
            // 
            this.amendQty.Location = new System.Drawing.Point(314, 69);
            this.amendQty.Name = "amendQty";
            this.amendQty.Size = new System.Drawing.Size(39, 20);
            this.amendQty.TabIndex = 107;
            // 
            // btnAmend
            // 
            this.btnAmend.Location = new System.Drawing.Point(476, 67);
            this.btnAmend.Name = "btnAmend";
            this.btnAmend.Size = new System.Drawing.Size(75, 23);
            this.btnAmend.TabIndex = 106;
            this.btnAmend.Text = "Amend";
            this.btnAmend.UseVisualStyleBackColor = true;
            this.btnAmend.Click += new System.EventHandler(this.btnAmend_Click);
            // 
            // tbCancelOrder
            // 
            this.tbCancelOrder.Location = new System.Drawing.Point(99, 69);
            this.tbCancelOrder.Name = "tbCancelOrder";
            this.tbCancelOrder.Size = new System.Drawing.Size(177, 20);
            this.tbCancelOrder.TabIndex = 105;
            // 
            // btnCancel
            // 
            this.btnCancel.Location = new System.Drawing.Point(557, 67);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 104;
            this.btnCancel.Text = "Cancel Ord";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            // 
            // label14
            // 
            this.label14.AutoSize = true;
            this.label14.Location = new System.Drawing.Point(427, 17);
            this.label14.Name = "label14";
            this.label14.Size = new System.Drawing.Size(43, 13);
            this.label14.TabIndex = 112;
            this.label14.Text = "Buy Px:";
            // 
            // label15
            // 
            this.label15.AutoSize = true;
            this.label15.Location = new System.Drawing.Point(428, 43);
            this.label15.Name = "label15";
            this.label15.Size = new System.Drawing.Size(42, 13);
            this.label15.TabIndex = 113;
            this.label15.Text = "Sell Px:";
            // 
            // tbBuyPrice
            // 
            this.tbBuyPrice.Location = new System.Drawing.Point(476, 14);
            this.tbBuyPrice.Name = "tbBuyPrice";
            this.tbBuyPrice.Size = new System.Drawing.Size(75, 20);
            this.tbBuyPrice.TabIndex = 115;
            // 
            // tbSellPrice
            // 
            this.tbSellPrice.Location = new System.Drawing.Point(476, 41);
            this.tbSellPrice.Name = "tbSellPrice";
            this.tbSellPrice.Size = new System.Drawing.Size(75, 20);
            this.tbSellPrice.TabIndex = 114;
            // 
            // cbSymbol
            // 
            this.cbSymbol.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cbSymbol.FormattingEnabled = true;
            this.cbSymbol.Location = new System.Drawing.Point(99, 14);
            this.cbSymbol.Name = "cbSymbol";
            this.cbSymbol.Size = new System.Drawing.Size(96, 21);
            this.cbSymbol.TabIndex = 117;
            // 
            // FrmMarket
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(644, 525);
            this.Controls.Add(this.cbSymbol);
            this.Controls.Add(this.tbBuyPrice);
            this.Controls.Add(this.tbSellPrice);
            this.Controls.Add(this.label15);
            this.Controls.Add(this.label14);
            this.Controls.Add(this.tbAmendPrice);
            this.Controls.Add(this.label11);
            this.Controls.Add(this.label10);
            this.Controls.Add(this.label9);
            this.Controls.Add(this.amendQty);
            this.Controls.Add(this.btnAmend);
            this.Controls.Add(this.tbCancelOrder);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOrders);
            this.Controls.Add(this.label5);
            this.Controls.Add(this.tbCurrentSymbol);
            this.Controls.Add(this.lblLastIndexPrice);
            this.Controls.Add(this.label8);
            this.Controls.Add(this.label7);
            this.Controls.Add(this.lblLastPrice);
            this.Controls.Add(this.label6);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.tbMessages);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.tbBuyQty);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.tbSellQty);
            this.Controls.Add(this.btnConnect);
            this.Controls.Add(this.btnBuy);
            this.Controls.Add(this.btnSell);
            this.Controls.Add(this.btnSubscribe);
            this.Controls.Add(this.dgvMessage);
            this.Controls.Add(this.lblStatus);
            this.Controls.Add(this.label1);
            this.Name = "FrmMarket";
            this.Text = "Market";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.FrmMarket_FormClosing);
            this.Load += new System.EventHandler(this.FrmMarket_Load);
            ((System.ComponentModel.ISupportInitialize)(this.dgvMessage)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.Label lblLastIndexPrice;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.Label lblLastPrice;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.TextBox tbMessages;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TextBox tbBuyQty;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox tbSellQty;
        private System.Windows.Forms.Button btnConnect;
        private System.Windows.Forms.Button btnBuy;
        private System.Windows.Forms.Button btnSell;
        private System.Windows.Forms.Button btnSubscribe;
        private System.Windows.Forms.DataGridView dgvMessage;
        private System.Windows.Forms.Label lblStatus;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox tbCurrentSymbol;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.DataGridViewTextBoxColumn colBuyTotalAmount;
        private System.Windows.Forms.DataGridViewTextBoxColumn colBuyAmount;
        private System.Windows.Forms.DataGridViewTextBoxColumn colBuyPrice;
        private System.Windows.Forms.DataGridViewTextBoxColumn colSellPrice;
        private System.Windows.Forms.DataGridViewTextBoxColumn colSellAmount;
        private System.Windows.Forms.DataGridViewTextBoxColumn colSellTotalAmount;
        private System.Windows.Forms.Button btnOrders;
        private System.Windows.Forms.TextBox tbAmendPrice;
        private System.Windows.Forms.Label label11;
        private System.Windows.Forms.Label label10;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.TextBox amendQty;
        private System.Windows.Forms.Button btnAmend;
        private System.Windows.Forms.TextBox tbCancelOrder;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Label label14;
        private System.Windows.Forms.Label label15;
        private System.Windows.Forms.TextBox tbBuyPrice;
        private System.Windows.Forms.TextBox tbSellPrice;
        private System.Windows.Forms.ComboBox cbSymbol;
    }
}