using System;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;
using service_station;

public class FieldUpdateInfo
{
    public string FieldName { get; set; }
    public string NewValue { get; set; }

    public FieldUpdateInfo(string fieldName, string newValue)
    {
        FieldName = fieldName;
        NewValue = newValue;
    }
}


public class FieldUpdateForm : Form
{
    private TextBox newValueTextBox;
    private Button addFieldButton;
    private Button submitButton;
    private ListBox fieldsListBox;
    private Label fieldNameLabel;
    private Label newValueLabel;
    private ComboBox fieldNameComboBox;

    public List<FieldUpdateInfo> FieldsToUpdate;

    public FieldUpdateForm(List<string> columns)
    {
        FieldsToUpdate = new List<FieldUpdateInfo>();

        this.Size = new Size(300, 400);
        this.StartPosition = FormStartPosition.CenterParent;
        this.Text = "Update data";

        fieldNameLabel = new Label
        {
            Text = "Enter name of row in DB",
            Location = new Point(15, 20),
            AutoSize = true
        };


        fieldNameComboBox = new ComboBox
        {
            Location = new Point(15, 45),
            Width = 250
        };

        foreach (string column in columns)
        {
            fieldNameComboBox.Items.Add(column);
        }

        newValueLabel = new Label
        {
            Text = "Enter a new value",
            Location = new Point(15, 80),
            AutoSize = true
        };

        newValueTextBox = new TextBox
        {
            Location = new Point(15, 105),
            Width = 250
        };

        addFieldButton = new Button
        {
            Text = "Add field",
            Location = new Point(15, 140)
        };
        addFieldButton.Click += AddFieldButton_Click;

        fieldsListBox = new ListBox
        {
            Location = new Point(15, 170),
            Width = 250,
            Height = 60
        };

        submitButton = new Button
        {
            Text = "Submit",
            Location = new Point(15, 240)
        };
        submitButton.Click += (sender, e) =>
        {
            if (FieldsToUpdate.Count > 0)
            {
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            else
            {
                MessageBox.Show("Please add at least one field to update.");
            }
        };

        this.Controls.Add(fieldNameLabel);
        this.Controls.Add(fieldNameComboBox);
        this.Controls.Add(newValueLabel);
        this.Controls.Add(newValueTextBox);
        this.Controls.Add(addFieldButton);
        this.Controls.Add(fieldsListBox);
        this.Controls.Add(submitButton);
    }

    private void AddFieldButton_Click(object sender, EventArgs e)
    {
        string fieldName = fieldNameComboBox.Text.Trim();
        string newValue = newValueTextBox.Text.Trim();

        if (!string.IsNullOrEmpty(fieldName) && !string.IsNullOrEmpty(newValue))
        {
            FieldsToUpdate.Add(new FieldUpdateInfo(fieldName, newValue));
            fieldsListBox.Items.Add($"{fieldName} = {newValue}");

            fieldNameComboBox.SelectedIndex = -1;
            newValueTextBox.Clear();
        }
        else
        {
            MessageBox.Show("Field name and value cannot be empty.");
        }
    }
}
