namespace InstallGui
{
    partial class UIPage
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(UIPage));
            this.pictureBox1 = new System.Windows.Forms.PictureBox();
            this.Default = new System.Windows.Forms.Button();
            this.Next = new System.Windows.Forms.Button();
            this.Back = new System.Windows.Forms.Button();
            this.progressBar1 = new System.Windows.Forms.ProgressBar();
            this.Title = new System.Windows.Forms.Label();
            this.Extra = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).BeginInit();
            this.SuspendLayout();
            // 
            // pictureBox1
            // 
            this.pictureBox1.BackColor = System.Drawing.SystemColors.Window;
            this.pictureBox1.Image = ((System.Drawing.Image)(resources.GetObject("pictureBox1.Image")));
            this.pictureBox1.Location = new System.Drawing.Point(0, 0);
            this.pictureBox1.Margin = new System.Windows.Forms.Padding(0);
            this.pictureBox1.MaximumSize = new System.Drawing.Size(493, 312);
            this.pictureBox1.MinimumSize = new System.Drawing.Size(493, 312);
            this.pictureBox1.Name = "pictureBox1";
            this.pictureBox1.Size = new System.Drawing.Size(493, 312);
            this.pictureBox1.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
            this.pictureBox1.TabIndex = 0;
            this.pictureBox1.TabStop = false;
            this.pictureBox1.Click += new System.EventHandler(this.pictureBox1_Click);
            // 
            // Default
            // 
            this.Default.Location = new System.Drawing.Point(393, 322);
            this.Default.Name = "Default";
            this.Default.Size = new System.Drawing.Size(91, 23);
            this.Default.TabIndex = 1;
            this.Default.Text = "Cancel";
            this.Default.UseVisualStyleBackColor = true;
            this.Default.Click += new System.EventHandler(this.Default_Click);
            // 
            // Next
            // 
            this.Next.Location = new System.Drawing.Point(292, 322);
            this.Next.Name = "Next";
            this.Next.Size = new System.Drawing.Size(91, 23);
            this.Next.TabIndex = 2;
            this.Next.Text = "&Next";
            this.Next.UseVisualStyleBackColor = true;
            this.Next.Click += new System.EventHandler(this.Next_Click);
            // 
            // Back
            // 
            this.Back.Location = new System.Drawing.Point(195, 322);
            this.Back.Name = "Back";
            this.Back.Size = new System.Drawing.Size(91, 23);
            this.Back.TabIndex = 3;
            this.Back.Text = "&Back";
            this.Back.UseVisualStyleBackColor = true;
            this.Back.Click += new System.EventHandler(this.button3_Click);
            // 
            // progressBar1
            // 
            this.progressBar1.Location = new System.Drawing.Point(173, 279);
            this.progressBar1.Name = "progressBar1";
            this.progressBar1.Size = new System.Drawing.Size(306, 15);
            this.progressBar1.Style = System.Windows.Forms.ProgressBarStyle.Continuous;
            this.progressBar1.TabIndex = 4;
            // 
            // Title
            // 
            this.Title.BackColor = System.Drawing.SystemColors.Window;
            this.Title.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Title.Location = new System.Drawing.Point(173, 18);
            this.Title.Name = "Title";
            this.Title.Size = new System.Drawing.Size(306, 65);
            this.Title.TabIndex = 5;
            this.Title.Text = "label 1.";
            this.Title.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.Title.Click += new System.EventHandler(this.Title_Click);
            // 
            // Extra
            // 
            this.Extra.BackColor = System.Drawing.SystemColors.Window;
            this.Extra.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Extra.Location = new System.Drawing.Point(177, 95);
            this.Extra.Name = "Extra";
            this.Extra.Size = new System.Drawing.Size(273, 173);
            this.Extra.TabIndex = 6;
            this.Extra.Text = "This is an example of a long quantity of text to see how well wrapping works";
            // 
            // UIPage
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(482, 340);
            this.Controls.Add(this.Extra);
            this.Controls.Add(this.Title);
            this.Controls.Add(this.progressBar1);
            this.Controls.Add(this.Back);
            this.Controls.Add(this.Next);
            this.Controls.Add(this.Default);
            this.Controls.Add(this.pictureBox1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MaximumSize = new System.Drawing.Size(498, 378);
            this.MinimumSize = new System.Drawing.Size(498, 378);
            this.Name = "UIPage";
            this.Text = "Citrix XenServer Tools Installer";
            this.Load += new System.EventHandler(this.UIPage_Load);
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.PictureBox pictureBox1;
        private System.Windows.Forms.Button Default;
        private System.Windows.Forms.Button Next;
        private System.Windows.Forms.Button Back;
        private System.Windows.Forms.ProgressBar progressBar1;
        private System.Windows.Forms.Label Title;
        private System.Windows.Forms.Label Extra;
    }
}

