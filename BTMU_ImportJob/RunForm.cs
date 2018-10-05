using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Linq;

namespace RunStoreProcedure
{
    public partial class RunForm : Form
    {
        public RunForm()
        {
            InitializeComponent();
        }

        private void RunForm_Load(object sender, EventArgs e)
        {
            bool isWinAuthen = true;
            int typeSplit = 0;
            string connString = "";
            string dataSrc = "";
            string db = "";
            string userid = "";
            string password = "";
            List<string> nonReqTransStoreProc = new List<string>();
            DataTable tblReqTransStoreProc = new DataTable();
            int countTrans = 0;

            tblReqTransStoreProc.Columns.Add("id", typeof(int));
            tblReqTransStoreProc.Columns.Add("sp_name", typeof(string));

            int condition = 0;

            string fileConfig = File.Exists(@"Connect.config") ? "Connect.config" : "..\\..\\Connect.config";
            
            XmlDocument doc = new XmlDocument();
            doc.Load(fileConfig);
            XmlElement elmroot = doc.DocumentElement;


            foreach (XmlElement elm in elmroot)
            {
                switch (elm.Name)
                {
                    case "WindowsAuthentication":
                        if (elm.Attributes[0].Value == "0")
                        {
                            isWinAuthen = false;
                            break;
                        }
                        break;
                    case "ConnectionString":

                        foreach (XmlNode elmChild in elm)
                        {
                            switch (elmChild.Name)
                            {
                                case "DataSource":
                                    dataSrc = elmChild.Attributes[0].Value.ToString();
                                    break;
                                case "Catalog":
                                    db = elmChild.Attributes[0].Value.ToString();
                                    break;
                                case "UserID":
                                    userid = elmChild.Attributes[0].Value.ToString();
                                    break;
                                case "Password":
                                    password = elmChild.Attributes[0].Value.ToString();
                                    break;
                            }
                        }
                        connString = isWinAuthen ? @"Data Source=" + dataSrc
                                                + ";Initial Catalog=" + db
                                                + ";Persist Security Info=True;Integrated Security=SSPI;" : @"Data Source=" + dataSrc
                                                                                                            + ";Initial Catalog=" + db
                                                                                                            + ";Persist Security Info=True;User ID=" + userid
                                                                                                            + ";Password=" + password;
                        break;
                    case "StoreProcedure":
                        foreach (XmlNode elmChild in elm)
                        {
                            switch (elmChild.Name)
                            {
                                case "NonRequiredTransaction":
                                    foreach (XmlNode elmChildElse in elmChild)
                                    {
                                        nonReqTransStoreProc.Add(elmChildElse.Attributes[0].Value.ToString());
                                    }
                                    break;
                                case "RequiredTransaction":
                                    ++countTrans;
                                    foreach (XmlNode elmChildElse in elmChild)
                                    {
                                        tblReqTransStoreProc.Rows.Add(countTrans, elmChildElse.Attributes[0].Value.ToString());
                                    }
                                    break;
                            }
                        }
                        break;
                    case "SetUpSpitFile":
                        int.TryParse(elm.Attributes[0].Value, out typeSplit);
                        int.TryParse(elm.FirstChild.Attributes[0].Value, out condition);
                        break;
                }
            }
            

            SqlConnection connection = new SqlConnection(connString);

            try
            {
                connection.Open();

                foreach (string store in nonReqTransStoreProc)
                {
                    try
                    {
                        SqlCommand cmd = new SqlCommand(store, connection);
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.ExecuteNonQuery();
                    }
                    catch (Exception en)
                    {
                        StoreLogToFile(en.Message, typeSplit, condition);
                    }
                }

                int checkRow = 0;
                
                for (int i = 1; i <= countTrans; i++)
                {
                    List<string> sp = new List<string>();
                    for (int j = checkRow; j < tblReqTransStoreProc.Rows.Count; j++)
                    {
                        int idNow;
                        int.TryParse(tblReqTransStoreProc.Rows[j].ItemArray[0].ToString(), out idNow);
                        if (idNow == i)
                        {
                            sp.Add(tblReqTransStoreProc.Rows[j].ItemArray[1].ToString());
                        }
                        else if (idNow != i)
                        {
                            checkRow = j;
                            break;
                        }
                    }

                    SqlTransaction sqlTransaction = connection.BeginTransaction();

                    foreach (string store in sp)
                    {
                        
                        try
                        {
                            SqlCommand cmd = new SqlCommand(store, connection,sqlTransaction);
                            cmd.CommandType = CommandType.StoredProcedure;
                            cmd.ExecuteNonQuery();
                        }
                        catch (Exception en)
                        {
                            sqlTransaction.Rollback();
                            StoreLogToFile(en.Message, typeSplit, condition);
                            this.Close();
                            return;
                        }
                    }
                    sqlTransaction.Commit();
                }

                connection.Close();
                this.Close();
            }
            catch (Exception en)
            {
                
                StoreLogToFile(en.Message, typeSplit, condition);
                connection.Close();
                this.Close();
            }
        }

        private void StoreLogToFile(string strLog, int typeSlplit, int condition)
        {
            if (File.Exists(@"log.txt"))
            {
                FileInfo fileInfo = new FileInfo("log.txt");
                DateTime fileCreateDate = File.GetCreationTime("log.txt");

                if (CheckCondition(typeSlplit, condition))
                {
                    string fBackupName = ".\\" + "bk_" + DateTime.Today.ToShortDateString() + "_" + DateTime.Now.ToLongTimeString() + "_log.txt";
                    fBackupName = fBackupName.Replace(" ", "_");
                    fBackupName = fBackupName.Replace("/", "-");
                    fBackupName = fBackupName.Replace(":", "-");
                    string fBkName = Path.GetFileName(fBackupName);
                    File.Move(@"log.txt", fBkName);
                    File.Delete(@"log.txt");
                    File.WriteAllText(@"log.txt", "[" + DateTime.Now.ToString() + "] Error: " + strLog + "\r\n");
                    return;
                }
                File.AppendAllText(@"log.txt", "[" + DateTime.Now.ToString() + "] Error: " + strLog + "\r\n");
            }
            else if (!File.Exists(@"log.txt"))
            {
                File.WriteAllText(@"log.txt", "[" + DateTime.Now.ToString() + "] Error: " + strLog + "\r\n");
            }
        }

        private bool CheckCondition(int typeSlplit, int condition)
        {
            if (1 == typeSlplit)
            {
                return (new FileInfo("log.txt")).Length > condition;
            }
            else if (2 == typeSlplit)
            {
                return DateTime.Compare(DateTime.Today.Date, File.GetCreationTime("log.txt")) >= condition;
            }
            return false;
        }
    }
}
