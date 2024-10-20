using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace service_station
{
    public class InputForm : Form
    {
        private TextBox textBox;
        private Button submitButton;
        private Label label;

        public string InputText { get; private set; }

        public InputForm()
        {
            this.Size = new Size(250, 150);
            this.StartPosition = FormStartPosition.CenterParent;
            this.Text = "Enter ID";

        
            label = new Label
            {
                Text = "Enter ID, for continue",
                Location = new Point(15, 20),
                AutoSize = true 
            };

            textBox = new TextBox
            {
                Location = new Point(15, 50),
                Width = 200
            };

            submitButton = new Button
            {
                Text = "Submit",
                Location = new Point(15, 90)
            };
            submitButton.Click += (sender, e) =>
            {
                InputText = textBox.Text;
                this.DialogResult = DialogResult.OK;
                this.Close();
            };

            this.Controls.Add(label);
            this.Controls.Add(textBox);
            this.Controls.Add(submitButton);
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            // 
            // InputForm
            // 
            this.ClientSize = new System.Drawing.Size(384, 350);
            this.Name = "InputForm";
            this.ResumeLayout(false);

        }

    }
}
