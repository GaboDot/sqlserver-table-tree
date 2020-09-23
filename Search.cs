using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Windows.Forms;


namespace TableLookUp
{
    public partial class SearchWindow : Form
    {
        private List<string> errores = new List<string>();
        private List<string> globalTables = new List<string>();
        private JArray allTables = new JArray();
        private BackgroundWorker bgWorker;
        private Stopwatch stopwatch;
        private JSONWindow ex;
        private ErrorWindow err;
        private int totalTables = 0;
        private int idFlag = 1;
        public SearchWindow()
        {
            InitializeComponent();
            loadingGif.SizeMode = PictureBoxSizeMode.StretchImage;
            loadingGif.Visible = false;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            UseWaitCursor = true;
            button1.Enabled = false;
            ipTXT.Enabled = false;
            dbTXT.Enabled = false;
            usrTXT.Enabled = false;
            pwdTXT.Enabled = false;
            loadingGif.Visible = true;

            globalTables = new List<string>();
            stopwatch = new Stopwatch();
            stopwatch.Start();

            bgWorker = new BackgroundWorker();
            bgWorker.DoWork += new DoWorkEventHandler(InitSearch);
            bgWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(finished);
            bgWorker.RunWorkerAsync();
        }

        private void InitSearch(object sender, DoWorkEventArgs e)
        {
            totalTables = 0;
            try
            {
                allTables = rootTables(ipTXT.Text, dbTXT.Text, usrTXT.Text, pwdTXT.Text);
                idFlag = 1;
            }
            catch (Exception ex)
            {
                idFlag = ex.HResult;
                MessageBox.Show(ex.Message, "", MessageBoxButtons.OK, MessageBoxIcon.Error);
                bgWorker.CancelAsync();
            }
        }

        private void finished(object sender, RunWorkerCompletedEventArgs e)
        {
            loadingGif.Visible = false;
            button1.Enabled = true;
            ipTXT.Enabled = true;
            dbTXT.Enabled = true;
            usrTXT.Enabled = true;
            pwdTXT.Enabled = true;
            stopwatch.Stop();
            this.UseWaitCursor = false;
            if (idFlag == 1)
            {
                string time = stopwatch.Elapsed.ToString("hh\\:mm\\:ss\\.ffff");
                ex = new JSONWindow(allTables, dbTXT.Text, totalTables, time);
                ex.FormClosed += OnCloseJson;
                ex.Show();
                if (errores.Count > 0)
                {
                    err = new ErrorWindow(errores);
                    err.Show();
                }
            }
            bgWorker.Dispose();
        }

        public void OnCloseJson(object sender, EventArgs e)
        {
            string errMess = "";
            try
            {
                if (ex != null && err != null)
                {
                    err.Close();
                    err.Dispose();
                }
            }
            catch (Exception exc)
            {
                errMess = exc.Message;
            }
        }

        private JArray rootTables(string ip, string database, string usr, string pwd)
        {
            JArray tables = new JArray();
            DataTable dt = new DataTable();
            string query = @"select name from sysobjects where xtype = 'U' order by name asc";
            string str = "server=" + ip + ";database=" + database + ";UID=" + usr + ";password=" + pwd;
            SqlConnection con = new SqlConnection(str);
            SqlCommand cmd = new SqlCommand(query, con);
            con.Open();
            SqlDataAdapter da = new SqlDataAdapter(cmd);
            da.Fill(dt);
            con.Close();
            da.Dispose();
            foreach(DataRow dr in dt.Rows)
            {
                DataTable parents = new DataTable();
                string tableName = dr["name"].ToString().ToUpper();
                query = @"SELECT
                    count(distinct SOO.NAME) AS parent_table
                  FROM sysforeignkeys FK
                  LEFT JOIN SYSOBJECTS SO
                    ON FK.fkeyid = SO.ID
                  LEFT JOIN SYSOBJECTS SOO
                    ON SOO.ID = FK.rkeyid
                  LEFT JOIN SYSOBJECTS SOOO
                    ON SOOO.ID = FK.constid
                  LEFT JOIN information_schema.key_column_usage IE
                    ON IE.CONSTRAINT_NAME = SOOO.NAME
                    AND ie.ORDINAL_POSITION = FK.keyno
                    AND ie.TABLE_NAME = SO.NAME
                  WHERE SO.NAME = '" + tableName +@"'
                  ORDER BY parent_table ASC";
                con = new SqlConnection(str);
                cmd = new SqlCommand(query, con);
                con.Open();
                da = new SqlDataAdapter(cmd);
                da.Fill(parents);
                con.Close();
                da.Dispose();
                int totalParents = Convert.ToInt32(parents.Rows[0]["parent_table"].ToString());
                if(totalParents == 0)
                {
                    totalTables++;
                    JObject obj = new JObject {{ "name", tableName}};
                    JArray subChilds = childFromParent(tableName, str);
                    if(subChilds.Count > 0)
                    {
                        obj.Add("related", subChilds);
                    }
                    tables.Add(obj);
                }
            }
            return tables;
        }

        private JArray childFromParent(string tableName, string conStr)
        {
            globalTables.Add(tableName);
            JArray tables = new JArray();
            DataTable dt = new DataTable();
            string query = @" SELECT
                distinct SO.NAME AS foreign_table
                FROM sysforeignkeys FK
                LEFT JOIN SYSOBJECTS SO
                ON FK.fkeyid = SO.ID
                LEFT JOIN SYSOBJECTS SOO
                ON SOO.ID = FK.rkeyid
                LEFT JOIN SYSOBJECTS SOOO
                ON SOOO.ID = FK.constid
                LEFT JOIN information_schema.key_column_usage IE
                ON IE.CONSTRAINT_NAME = SOOO.NAME
                AND IE.ORDINAL_POSITION = FK.keyno
                AND IE.TABLE_NAME = SO.NAME
                WHERE SOO.NAME = '" + tableName + @"'
                ORDER BY foreign_table ASC";
            SqlConnection con = new SqlConnection(conStr);
            SqlCommand cmd = new SqlCommand(query, con);
            con.Open();
            SqlDataAdapter da = new SqlDataAdapter(cmd);
            da.Fill(dt);
            con.Close();
            da.Dispose();
            foreach (DataRow dr in dt.Rows)
            {
                string childName = dr["foreign_table"].ToString().ToUpper();
                if (!globalTables.Contains(childName))
                {
                    JObject child = new JObject { { "name", childName } };
                    totalTables++;
                    JArray subChilds = childFromParent(childName, conStr);
                    if (subChilds.Count > 0)
                    { child.Add("related", subChilds); }
                    tables.Add(child);
                }
            }
            return tables;
        }
    }
}
