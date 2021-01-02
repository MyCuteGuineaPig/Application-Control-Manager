using log4net;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Program_Control
{
    class Mysql
    {
        private readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private MySqlConnection Conn;
        private MySqlConnectionStringBuilder connString = new MySqlConnectionStringBuilder();

        public Mysql()
        {
            try
            {
                /**
                EDIT Here
                 */
                connString.Server = "";
                connString.Port = 0000;
                connString.UserID = "####";
                connString.Password = "####";
                Conn = new MySqlConnection(connString.ToString());
                log.Info("Build sql connection");
            }
            catch (Exception e)
            {
                log.Fatal("Cannot build sql connection => System Exit  Error Code " + e.Message);
                System.Environment.Exit(1);
            }

        }

        public bool re_connection()
        {
            try
            {
                Conn.Open();
                return true;
            }
            catch (MySql.Data.MySqlClient.MySqlException e)
            {
                log.Error("Cannot build sql connection => System Exit  Error Code " + e.Message);
                return false;
            }

        }

        public bool fetches(string query, ref DataTable dt)
        {
            if (Conn.State != System.Data.ConnectionState.Open)
            {
                if (!re_connection())
                    return false;
            }
            try
            {
                MySqlCommand cmd = new MySqlCommand(query, Conn);
                MySqlDataReader reader = cmd.ExecuteReader();
                dt.Load(reader);
                reader.Close();
                return true;
            }
            catch (MySqlException e)
            {
                log.Error("Cannot get query for MT4  SQL Error Code " + e.Message);
                return false;
            }
        }


        public bool fetches(string query, ref Dictionary<string, double> dict)
        {
            if (Conn.State != System.Data.ConnectionState.Open)
            {
                if (!re_connection())
                    return false;
            }
            try
            {
                MySqlCommand cmd = new MySqlCommand(query, Conn);
                MySqlDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    dict.Add(reader[0].ToString(), Convert.ToDouble(reader[1].ToString()));
                }
                reader.Close();
                return true;
            }
            catch (MySqlException e)
            {
                log.Error("Cannot get query for MT4  SQL Error Code " + e.Message);
                return false;
            }

        }
        public bool insert(string query, string table)
        {
            if (Conn.State != System.Data.ConnectionState.Open)
            {
                if (!re_connection())
                    return false;
            }
            try
            {
                MySqlCommand cmd = new MySqlCommand(query, Conn);
                cmd.ExecuteNonQuery();
                log.Info("Successful upload to DB : " + table);
                return true;
            }
            catch (MySqlException e)
            {
                log.Info(query);
                log.Error("Failed to Upload to DB table =>  " + table + ". SQL Error : " + e.Message);
                return false;
            }

        }
    }
}
