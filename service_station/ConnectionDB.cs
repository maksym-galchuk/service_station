using System;
using System.Data.SqlClient;

namespace service_station
{
    public class ConnectionDB
    {
        private static string connectionString = "Data Source=ACER_ASPIRE_7;Initial Catalog=service_station;Integrated Security=True";
        private SqlConnection connection = new SqlConnection(connectionString);

        public void OpenConnection()
        {
            try
            {
                if (connection.State == System.Data.ConnectionState.Closed)
                {
                    connection.Open();
                    Console.WriteLine("Connection successfully opened.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error while opening the connection: " + ex.Message);
            }
        }

        public void CloseConnection()
        {
            try
            {
                if (connection.State == System.Data.ConnectionState.Open)
                {
                    connection.Close();
                    Console.WriteLine("Connection closed.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error while closing the connection: " + ex.Message);
            }
        }

        public SqlConnection GetConnection()
        {
            return connection;
        }
    }
}