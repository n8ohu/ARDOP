namespace ARDOP
{
    partial class Main
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
            this.components = new System.ComponentModel.Container();
            this.pnlWaterfall = new System.Windows.Forms.Panel();
            this.pnlConstellation = new System.Windows.Forms.Panel();
            this.toolStrip1 = new System.Windows.Forms.ToolStrip();
            this.tmtPoll = new System.Windows.Forms.Timer(this.components);
            this.tmrStartCODEC = new System.Windows.Forms.Timer(this.components);
            this.SuspendLayout();
            // 
            // pnlWaterfall
            // 
            this.pnlWaterfall.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.pnlWaterfall.Location = new System.Drawing.Point(317, 70);
            this.pnlWaterfall.Name = "pnlWaterfall";
            this.pnlWaterfall.Size = new System.Drawing.Size(200, 100);
            this.pnlWaterfall.TabIndex = 0;
            // 
            // pnlConstellation
            // 
            this.pnlConstellation.Location = new System.Drawing.Point(52, 80);
            this.pnlConstellation.Name = "pnlConstellation";
            this.pnlConstellation.Size = new System.Drawing.Size(86, 100);
            this.pnlConstellation.TabIndex = 1;
            // 
            // toolStrip1
            // 
            this.toolStrip1.Location = new System.Drawing.Point(0, 0);
            this.toolStrip1.Name = "toolStrip1";
            this.toolStrip1.Size = new System.Drawing.Size(784, 25);
            this.toolStrip1.TabIndex = 2;
            this.toolStrip1.Text = "toolStrip1";
            // 
            // Main
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(784, 217);
            this.Controls.Add(this.toolStrip1);
            this.Controls.Add(this.pnlConstellation);
            this.Controls.Add(this.pnlWaterfall);
            this.Name = "Main";
            this.Text = "ARDOP Modem";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Panel pnlWaterfall;
        private System.Windows.Forms.Panel pnlConstellation;
        private System.Windows.Forms.ToolStrip toolStrip1;
        private System.Windows.Forms.Timer tmtPoll;
        private System.Windows.Forms.Timer tmrStartCODEC;
    }
}

