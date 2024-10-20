using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace service_station
{
    public partial class Form1 : Form
    {
        private ConnectionDB connectionDB;
        private List<string> tables;
        private string selectedTable;
        private Dictionary<string, string>  displayedColumns;

        public Form1()
        {
            InitializeComponent();
            connectionDB = new ConnectionDB();
            tables = GetTables();
            LoadTablesIntoComboBox();
            displayedColumns = new Dictionary<string, string>();
            GetDisplayedColumns();
        }

        private void GetDisplayedColumns()
        {
            displayedColumns.Add("clients", "name");
            displayedColumns.Add("orders", "id");
            displayedColumns.Add("spare_parts", "name");
            displayedColumns.Add("suppliers", "name");
            displayedColumns.Add("vehicles", "client_id, make");
        }

        private List<string> GetTables()
        {
            List<string> tables = new List<string>();

            try
            {
                connectionDB.OpenConnection();

                string query = "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE'";
                SqlCommand command = new SqlCommand(query, connectionDB.GetConnection());

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        tables.Add(reader.GetString(0));
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error while getting tables: " + ex.Message);
            }
            finally
            {
                connectionDB.CloseConnection();
            }

            return tables;
        }

        private string LabelFromSnakeCase(string tableName)
        {
            var regex = new Regex(Regex.Escape(tableName[0].ToString()));
            return regex.Replace(tableName, tableName[0].ToString().ToUpper(), 1).Replace("_", " ").Replace("id", "");
        }  
        
        private string SnakeCaseFromLabel(string tableLabel)
        {
            return tableLabel.ToLower().Replace(" ", "_");
        }

        private void LoadTablesIntoComboBox()
        {

            comboBox1.Items.Clear();
            foreach (string tableName in tables) 
            { 
                comboBox1.Items.Add(LabelFromSnakeCase(tableName));
            }
        }

        private void LoadTableData(string tableName)
        {
            connectionDB.OpenConnection();

            SqlCommand command = new SqlCommand($"SELECT * FROM {tableName}", connectionDB.GetConnection());
            SqlDataAdapter adapter = new SqlDataAdapter(command);
            DataTable table = new DataTable();
            adapter.Fill(table);

            dataGridView1.DataSource = null;
            dataGridView1.Columns.Clear();
            dataGridView1.DataSource = table;

            connectionDB.CloseConnection();
        }

        private void DeleteRowFromDatabase(string id)
        {
            try
            {
                connectionDB.OpenConnection();

                SqlCommand command = new SqlCommand($"DELETE FROM {selectedTable} WHERE id = @id", connectionDB.GetConnection());
                command.Parameters.AddWithValue("@id", id);
                command.ExecuteNonQuery();

                connectionDB.CloseConnection();

                MessageBox.Show($"Id: {id}, will be deleted.");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error while deleting: " + ex.Message);
            }
        }

        private void EditRowInDatabase(string id)
        {
            try
            {
                connectionDB.OpenConnection();
                string columnQuery = $@"
            SELECT 
                c.name AS ColumnName, 
                t.name AS DataType,
                CASE 
                    WHEN fk.parent_column_id IS NOT NULL THEN 'FK'
                    ELSE NULL
                END AS IsForeignKey,
                OBJECT_NAME(fk.referenced_object_id) AS ReferencedTable
            FROM sys.columns c
            INNER JOIN sys.types t ON c.user_type_id = t.user_type_id
            LEFT JOIN sys.foreign_key_columns fk ON fk.parent_object_id = c.object_id AND fk.parent_column_id = c.column_id
            WHERE OBJECT_NAME(c.object_id) = '{selectedTable}' AND c.name != 'id'";

                SqlCommand columnCommand = new SqlCommand(columnQuery, connectionDB.GetConnection());
                List<(string ColumnName, string DataType, bool IsForeignKey, string ReferencedTable)> columns = new List<(string, string, bool, string)>();

                using (SqlDataReader reader = columnCommand.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        columns.Add((
                            reader.GetString(0),
                            reader.GetString(1),
                            !reader.IsDBNull(2),
                            reader.IsDBNull(3) ? null : reader.GetString(3)
                        ));
                    }
                }

                // Fetch current values
                string selectQuery = $"SELECT * FROM {selectedTable} WHERE id = @id";
                SqlCommand selectCommand = new SqlCommand(selectQuery, connectionDB.GetConnection());
                selectCommand.Parameters.AddWithValue("@id", id);
                Dictionary<string, object> currentValues = new Dictionary<string, object>();

                using (SqlDataReader reader = selectCommand.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        foreach (var column in columns)
                        {
                            currentValues[column.ColumnName] = reader[column.ColumnName];
                        }
                    }
                    else
                    {
                        MessageBox.Show($"No record found with id {id}");
                        return;
                    }
                }

                Form editForm = new Form();
                editForm.Text = $"Editing data in table {selectedTable}";
                editForm.Size = new Size(400, 100 + columns.Count * 30);
                int y = 10;
                Dictionary<string, Control> inputControls = new Dictionary<string, Control>();

                foreach (var column in columns)
                {
                    Label label = new Label
                    {
                        Text = LabelFromSnakeCase(column.ColumnName),
                        Location = new Point(10, y),
                        AutoSize = true
                    };
                    editForm.Controls.Add(label);

                    Control inputControl;

                    if (column.IsForeignKey)
                    {
                        ComboBox comboBox = new ComboBox
                        {
                            Location = new Point(150, y),
                            Width = 200
                        };
                        PopulateForeignKeyComboBox(comboBox, column.ReferencedTable);
                        SetComboBoxValue(comboBox, currentValues[column.ColumnName]);
                        inputControl = comboBox;
                    }
                    else
                    {
                        switch (column.DataType.ToLower())
                        {
                            case "date":
                                DateTimePicker datePicker = new DateTimePicker
                                {
                                    Location = new Point(150, y),
                                    Width = 200,
                                    Format = DateTimePickerFormat.Short,
                                    Value = (DateTime)currentValues[column.ColumnName]
                                };
                                inputControl = datePicker;
                                break;
                            case "datetime":
                            case "datetime2":
                            case "smalldatetime":
                                DateTimePicker dateTimePicker = new DateTimePicker
                                {
                                    Location = new Point(150, y),
                                    Width = 200,
                                    Format = DateTimePickerFormat.Custom,
                                    CustomFormat = "yyyy-MM-dd HH:mm:ss",
                                    Value = (DateTime)currentValues[column.ColumnName]
                                };
                                inputControl = dateTimePicker;
                                break;
                            case "time":
                                DateTimePicker timePicker = new DateTimePicker
                                {
                                    Location = new Point(150, y),
                                    Width = 200,
                                    Format = DateTimePickerFormat.Time,
                                    ShowUpDown = true,
                                    Value = DateTime.Today.Add((TimeSpan)currentValues[column.ColumnName])
                                };
                                inputControl = timePicker;
                                break;
                            default:
                                TextBox textBox = new TextBox
                                {
                                    Location = new Point(150, y),
                                    Width = 200,
                                    Text = currentValues[column.ColumnName].ToString()
                                };
                                inputControl = textBox;
                                break;
                        }
                    }

                    editForm.Controls.Add(inputControl);
                    inputControls[column.ColumnName] = inputControl;
                    y += 30;
                }

                Button submitButton = new Button
                {
                    Text = "Update",
                    Location = new Point(150, y)
                };
                submitButton.Click += (s, ev) =>
                {
                    string updateQuery = $"UPDATE {selectedTable} SET {string.Join(", ", columns.Select(c => $"{c.ColumnName} = @{c.ColumnName}"))} WHERE id = @id";
                    SqlCommand updateCommand = new SqlCommand(updateQuery, connectionDB.GetConnection());

                    foreach (var column in columns)
                    {
                        object value = GetValueFromControl(inputControls[column.ColumnName], column.DataType);
                        updateCommand.Parameters.AddWithValue("@" + column.ColumnName, value ?? DBNull.Value);
                    }
                    updateCommand.Parameters.AddWithValue("@id", id);

                    try
                    {
                        int rowsAffected = updateCommand.ExecuteNonQuery();
                        if (rowsAffected > 0)
                        {
                            MessageBox.Show("Data updated successfully.");
                            editForm.Close();
                        }
                        else
                        {
                            MessageBox.Show("Failed to update data.");
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Error updating data: " + ex.Message);
                    }
                };
                editForm.Controls.Add(submitButton);
                editForm.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error getting column information: " + ex.Message);
            }
            finally
            {
                LoadTableData(selectedTable);
                connectionDB.CloseConnection();
            }
        }

        private void SetComboBoxValue(ComboBox comboBox, object value)
        {
            foreach (KeyValuePair<int, string> item in comboBox.Items)
            {
                if (item.Key.Equals(value))
                {
                    comboBox.SelectedItem = item;
                    break;
                }
            }
        }

        private void showAllTables(object sender, EventArgs e)
        {
            if (comboBox1.SelectedItem != null)
            {
                selectedTable = SnakeCaseFromLabel(comboBox1.SelectedItem.ToString());
                LoadTableData(selectedTable);
            }
        }

        private void deleteButton(object sender, EventArgs e)
        {
            if(isTableNotSelected()) return;

            using (InputForm inputForm = new InputForm())
            {
                if (inputForm.ShowDialog() == DialogResult.OK)
                {
                    string enteredId = inputForm.InputText;

                    if (!string.IsNullOrEmpty(enteredId))
                    {
                        try
                        {
                            DeleteRowFromDatabase(enteredId);
                            LoadTableData(selectedTable);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show("Error while deleting: " + ex.Message);
                            return;
                        }
                        
                    }
                    else
                    {
                        MessageBox.Show("ID cannot be empty");
                    }
                }
            }
        }

        private void editButton(object sender, EventArgs e)
        {
            if (isTableNotSelected()) return;

            using (InputForm inputForm = new InputForm())
            {
                if (inputForm.ShowDialog() == DialogResult.OK)
                {
                    string enteredId = inputForm.InputText;

                    if (!string.IsNullOrEmpty(enteredId))
                    {
                        try
                        {
                            EditRowInDatabase(enteredId);  
                            LoadTableData(selectedTable);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show("Error while editing: " + ex.Message);
                            return;
                        }
                    }
                    else
                    {
                        MessageBox.Show("ID cannot be empty");
                    }
                }
            }
        }

        private void buttonFilter_Click(object sender, EventArgs e)
        {
            if (isTableNotSelected()) return;

            try
            {
                connectionDB.OpenConnection();

                // Get column information
                string columnQuery = $@"
            SELECT 
                c.name AS ColumnName, 
                t.name AS DataType
            FROM sys.columns c
            INNER JOIN sys.types t ON c.user_type_id = t.user_type_id
            WHERE OBJECT_NAME(c.object_id) = '{selectedTable}'";

                SqlCommand columnCommand = new SqlCommand(columnQuery, connectionDB.GetConnection());
                List<(string ColumnName, string DataType)> columns = new List<(string, string)>();

                using (SqlDataReader reader = columnCommand.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        columns.Add((reader.GetString(0), reader.GetString(1)));
                    }
                }

                // Create filter form
                Form filterForm = new Form();
                filterForm.Text = $"Filter {selectedTable}";
                filterForm.Size = new Size(400, 300);
                filterForm.FormBorderStyle = FormBorderStyle.FixedDialog;
                filterForm.StartPosition = FormStartPosition.CenterParent;

                int y = 10;

                // Column dropdown
                ComboBox columnComboBox = new ComboBox();
                columnComboBox.Location = new Point(10, y);
                columnComboBox.Width = 120;
                columnComboBox.Items.AddRange(columns.Select(c => c.ColumnName).ToArray());
                filterForm.Controls.Add(columnComboBox);

                // Operator dropdown
                ComboBox operatorComboBox = new ComboBox();
                operatorComboBox.Location = new Point(140, y);
                operatorComboBox.Width = 80;
                operatorComboBox.Items.AddRange(new string[] { "=", "!=", ">", "<", ">=", "<=", "LIKE" });
                filterForm.Controls.Add(operatorComboBox);

                // Value textbox
                TextBox valueTextBox = new TextBox();
                valueTextBox.Location = new Point(230, y);
                valueTextBox.Width = 140;
                filterForm.Controls.Add(valueTextBox);

                y += 40;

                // Add filter button
                Button addFilterButton = new Button();
                addFilterButton.Text = "Add Filter";
                addFilterButton.Location = new Point(10, y);
                filterForm.Controls.Add(addFilterButton);

                // Filter conditions listbox
                ListBox filtersListBox = new ListBox();
                filtersListBox.Location = new Point(10, y + 40);
                filtersListBox.Width = 360;
                filtersListBox.Height = 100;
                filterForm.Controls.Add(filtersListBox);

                y += 150;

                // Apply filters button
                Button applyFiltersButton = new Button();
                applyFiltersButton.Text = "Apply Filters";
                applyFiltersButton.Location = new Point(10, y);
                filterForm.Controls.Add(applyFiltersButton);

                // Clear filters button
                Button clearFiltersButton = new Button();
                clearFiltersButton.Text = "Clear Filters";
                clearFiltersButton.Location = new Point(100, y);
                filterForm.Controls.Add(clearFiltersButton);

                List<string> filterConditions = new List<string>();

                addFilterButton.Click += (s, ev) =>
                {
                    if (columnComboBox.SelectedItem == null || operatorComboBox.SelectedItem == null || string.IsNullOrWhiteSpace(valueTextBox.Text))
                    {
                        MessageBox.Show("Please select a column, operator, and enter a value.");
                        return;
                    }

                    string column = columnComboBox.SelectedItem.ToString();
                    string op = operatorComboBox.SelectedItem.ToString();
                    string value = valueTextBox.Text;

                    string dataType = columns.First(c => c.ColumnName == column).DataType;
                    string condition;

                    if (dataType.ToLower().Contains("char") || dataType.ToLower().Contains("text"))
                    {
                        condition = $"{column} {op} '{value.Replace("'", "''")}'";
                    }
                    else if (dataType.ToLower().Contains("date") || dataType.ToLower().Contains("time"))
                    {
                        condition = $"{column} {op} '{value}'";
                    }
                    else
                    {
                        condition = $"{column} {op} {value}";
                    }

                    filterConditions.Add(condition);
                    filtersListBox.Items.Add($"{column} {op} {value}");

                    columnComboBox.SelectedIndex = -1;
                    operatorComboBox.SelectedIndex = -1;
                    valueTextBox.Clear();
                };

                clearFiltersButton.Click += (s, ev) =>
                {
                    filterConditions.Clear();
                    filtersListBox.Items.Clear();
                };

                applyFiltersButton.Click += (s, ev) =>
                {
                    string query;
                    if (filterConditions.Count == 0)
                    {
                        query = $"SELECT * FROM {selectedTable}";
                    } else {
                        string whereClause = string.Join(" AND ", filterConditions);
                        query = $"SELECT * FROM {selectedTable} WHERE {whereClause}";
                    }

                    
                    SqlCommand command = new SqlCommand(query, connectionDB.GetConnection());
                    SqlDataAdapter adapter = new SqlDataAdapter(command);
                    DataTable resultTable = new DataTable();
                    adapter.Fill(resultTable);

                    dataGridView1.DataSource = resultTable;

                    MessageBox.Show("Filtering completed.");
                    filterForm.Close();
                };

                filterForm.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error while filtering: " + ex.Message);
            }
            finally
            {
                connectionDB.CloseConnection();
            }
        }

        private void buttonCalculateStats_Click(object sender, EventArgs e)
        {
            if (isTableNotSelected()) return;

            try
            {
                connectionDB.OpenConnection();

                // Отримуємо інформацію про числові поля
                string columnQuery = $@"
            SELECT 
                c.name AS ColumnName, 
                t.name AS DataType
            FROM sys.columns c
            INNER JOIN sys.types t ON c.user_type_id = t.user_type_id
            WHERE OBJECT_NAME(c.object_id) = '{selectedTable}'
            AND t.name IN ('tinyint', 'smallint', 'int', 'bigint', 'decimal', 'numeric', 'float', 'real')";

                SqlCommand columnCommand = new SqlCommand(columnQuery, connectionDB.GetConnection());
                List<string> numericColumns = new List<string>();

                using (SqlDataReader reader = columnCommand.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        numericColumns.Add(reader.GetString(0));
                    }
                }

                if (numericColumns.Count == 0)
                {
                    MessageBox.Show("No numeric columns found in the selected table.");
                    return;
                }

                // Створюємо форму для вибору операції та поля
                Form mathForm = new Form();
                mathForm.Text = "Mathematical Processing";
                mathForm.Size = new Size(300, 200);
                mathForm.FormBorderStyle = FormBorderStyle.FixedDialog;
                mathForm.StartPosition = FormStartPosition.CenterParent;

                ComboBox operationComboBox = new ComboBox();
                operationComboBox.Location = new Point(10, 10);
                operationComboBox.Width = 260;
                operationComboBox.Items.AddRange(new string[] { "Sum", "Average", "Maximum", "Minimum"});
                mathForm.Controls.Add(operationComboBox);

                ComboBox columnComboBox = new ComboBox();
                columnComboBox.Location = new Point(10, 40);
                columnComboBox.Width = 260;
                columnComboBox.Items.AddRange(numericColumns.ToArray());
                mathForm.Controls.Add(columnComboBox);

                TextBox percentageTextBox = new TextBox();
                percentageTextBox.Location = new Point(10, 70);
                percentageTextBox.Width = 260;
                percentageTextBox.Visible = false;
                mathForm.Controls.Add(percentageTextBox);

                Button processButton = new Button();
                processButton.Text = "Process";
                processButton.Location = new Point(10, 100);
                processButton.Click += (s, ev) =>
                {
                    if (operationComboBox.SelectedItem == null || columnComboBox.SelectedItem == null)
                    {
                        MessageBox.Show("Please select both operation and column.");
                        return;
                    }

                    string operation = operationComboBox.SelectedItem.ToString();
                    string column = columnComboBox.SelectedItem.ToString();

                    string query = "";
                    switch (operation)
                    {
                        case "Sum":
                            query = $"SELECT SUM({column}) AS Result FROM {selectedTable}";
                            break;
                        case "Average":
                            query = $"SELECT AVG({column}) AS Result FROM {selectedTable}";
                            break;
                        case "Maximum":
                            query = $"SELECT MAX({column}) AS Result FROM {selectedTable}";
                            break;
                        case "Minimum":
                            query = $"SELECT MIN({column}) AS Result FROM {selectedTable}";
                            break;
                        case "Percentage Increase":
                            if (!double.TryParse(percentageTextBox.Text, out double percentage))
                            {
                                MessageBox.Show("Please enter a valid percentage.");
                                return;
                            }
                            query = $"SELECT {column}, {column} * (1 + {percentage / 100}) AS Result FROM {selectedTable}";
                            break;
                    }

                    SqlCommand command = new SqlCommand(query, connectionDB.GetConnection());
                    SqlDataAdapter adapter = new SqlDataAdapter(command);
                    DataTable resultTable = new DataTable();
                    adapter.Fill(resultTable);

                    object result = resultTable.Rows[0]["Result"];
                    MessageBox.Show($"The {operation} of {column} is: {result}");

                    mathForm.Close();
                };
                mathForm.Controls.Add(processButton);

                mathForm.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error during mathematical processing: " + ex.Message);
            }
            finally
            {
                connectionDB.CloseConnection();
            }
        }
        private bool isTableNotSelected()
        {
            if (string.IsNullOrEmpty(selectedTable))
            {
                MessageBox.Show("Choose a table first.");
                return true;
            }
            
            return false;
        }

        private void insertButton(object sender, EventArgs e)
        {
            if (isTableNotSelected()) return;
            try
            {
                connectionDB.OpenConnection();
                string columnQuery = $@"
            SELECT 
                c.name AS ColumnName, 
                t.name AS DataType,
                CASE 
                    WHEN fk.parent_column_id IS NOT NULL THEN 'FK'
                    ELSE NULL
                END AS IsForeignKey,
                OBJECT_NAME(fk.referenced_object_id) AS ReferencedTable
            FROM sys.columns c
            INNER JOIN sys.types t ON c.user_type_id = t.user_type_id
            LEFT JOIN sys.foreign_key_columns fk ON fk.parent_object_id = c.object_id AND fk.parent_column_id = c.column_id
            WHERE OBJECT_NAME(c.object_id) = '{selectedTable}' AND c.name != 'id'";

                SqlCommand columnCommand = new SqlCommand(columnQuery, connectionDB.GetConnection());
                List<(string ColumnName, string DataType, bool IsForeignKey, string ReferencedTable)> columns = new List<(string, string, bool, string)>();

                using (SqlDataReader reader = columnCommand.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        columns.Add((
                            reader.GetString(0),
                            reader.GetString(1),
                            reader.IsDBNull(2) ? false : true,
                            reader.IsDBNull(3) ? null : reader.GetString(3)
                        ));
                    }
                }

                Form insertForm = new Form();
                insertForm.Text = $"Inserting data into table {selectedTable}";
                insertForm.Size = new Size(400, 100 + columns.Count * 30);
                int y = 10;
                Dictionary<string, Control> inputControls = new Dictionary<string, Control>();

                foreach (var column in columns)
                {
                    Label label = new Label
                    {
                        Text = LabelFromSnakeCase(column.ColumnName),
                        Location = new Point(10, y),
                        AutoSize = true
                    };
                    insertForm.Controls.Add(label);

                    Control inputControl;

                    if (column.IsForeignKey)
                    {
                        ComboBox comboBox = new ComboBox
                        {
                            Location = new Point(150, y),
                            Width = 200
                        };
                        PopulateForeignKeyComboBox(comboBox, column.ReferencedTable);
                        inputControl = comboBox;
                    }
                    else
                    {
                        switch (column.DataType.ToLower())
                        {
                            case "date":
                                DateTimePicker datePicker = new DateTimePicker
                                {
                                    Location = new Point(150, y),
                                    Width = 200,
                                    Format = DateTimePickerFormat.Short
                                };
                                inputControl = datePicker;
                                break;
                            case "datetime":
                            case "datetime2":
                            case "smalldatetime":
                                DateTimePicker dateTimePicker = new DateTimePicker
                                {
                                    Location = new Point(150, y),
                                    Width = 200,
                                    Format = DateTimePickerFormat.Custom,
                                    CustomFormat = "yyyy-MM-dd HH:mm:ss"
                                };
                                inputControl = dateTimePicker;
                                break;
                            case "time":
                                DateTimePicker timePicker = new DateTimePicker
                                {
                                    Location = new Point(150, y),
                                    Width = 200,
                                    Format = DateTimePickerFormat.Time,
                                    ShowUpDown = true
                                };
                                inputControl = timePicker;
                                break;
                            default:
                                TextBox textBox = new TextBox
                                {
                                    Location = new Point(150, y),
                                    Width = 200
                                };
                                inputControl = textBox;
                                break;
                        }
                    }

                    insertForm.Controls.Add(inputControl);
                    inputControls[column.ColumnName] = inputControl;
                    y += 30;
                }

                Button submitButton = new Button
                {
                    Text = "Enter",
                    Location = new Point(150, y)
                };
                submitButton.Click += (s, ev) =>
                {
                    string insertQuery = $"INSERT INTO {selectedTable} ({string.Join(", ", columns.Select(c => c.ColumnName))}) VALUES ({string.Join(", ", columns.Select(c => "@" + c.ColumnName))})";
                    SqlCommand insertCommand = new SqlCommand(insertQuery, connectionDB.GetConnection());

                    foreach (var column in columns)
                    {
                        object value = GetValueFromControl(inputControls[column.ColumnName], column.DataType);
                        insertCommand.Parameters.AddWithValue("@" + column.ColumnName, value ?? DBNull.Value);
                    }

                    try
                    {
                        int rowsAffected = insertCommand.ExecuteNonQuery();
                        if (rowsAffected > 0)
                        {
                            MessageBox.Show("Data inserted successfully.");
                            insertForm.Close();
                        }
                        else
                        {
                            MessageBox.Show("Failed to insert data.");
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Error inserting data: " + ex.Message);
                    }
                };
                insertForm.Controls.Add(submitButton);
                insertForm.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error getting column information: " + ex.Message);
            }
            finally
            {
                LoadTableData(selectedTable);
                connectionDB.CloseConnection();
            }
        }

        private void PopulateForeignKeyComboBox(ComboBox comboBox, string referencedTable)
        {
            string displayFields = displayedColumns[referencedTable];
            string displayFieldsLength = displayFields.Split(',').Length.ToString();
            var displayFieldsArray = displayFields.Split(',');
            string query = $"SELECT id, {displayFields} FROM {referencedTable}";
            SqlCommand command = new SqlCommand(query, connectionDB.GetConnection());
            using (SqlDataReader reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    string value = "";
                    for (int i = 0; i < displayFieldsArray.Length; i++)
                    {
                        value += displayFieldsArray[i] + ": " + reader.GetValue(i + 1).ToString() + " ";
                    }
                    comboBox.Items.Add(new KeyValuePair<int, string>(reader.GetInt32(0), value));
                }
            }
            comboBox.DisplayMember = "Value";
            comboBox.ValueMember = "Key";
        }

        private object GetValueFromControl(Control control, string dataType)
        {
            switch (control)
            {
                case ComboBox comboBox:
                    return ((KeyValuePair<int, string>)comboBox.SelectedItem).Key;
                case DateTimePicker dateTimePicker:
                    switch (dataType.ToLower())
                    {
                        case "date":
                            return dateTimePicker.Value.Date;
                        case "time":
                            return dateTimePicker.Value.TimeOfDay;
                        default:
                            return dateTimePicker.Value;
                    }
                case TextBox textBox:
                    return string.IsNullOrEmpty(textBox.Text) ? null : textBox.Text;
                default:
                    return null;
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }
    }

}
