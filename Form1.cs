using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SQLite;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AppTime
{
    public partial class Form1 : Form
    {
        List<TrackedProgram> Programs = new List<TrackedProgram>();
        int numPrograms = 0; 

        public SQLiteConnection dbConnection;
        string dbLoc;
        bool dbConnected = false;
        bool dbExist = false;
        int version = 1;


        //settings        
        bool settingsPanelExtended;
        bool showPaths;
        bool minimizeToTray;
        RegistryKey rkApp = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);

        public Form1()
        {
            InitializeComponent();

            //Registry
            using (RegistryKey Key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\sinnzrAppTime\"))
                if (Key != null)
                {
                    dbLoc = (string)Key.GetValue("databaseLocation");
                    
                    if (File.Exists(dbLoc))
                    {
                        dbExist = true;
                        connectDb(false, false);
                        databaseAvailableLabel.Visible = false;
                        loadDatabase();
                    }
                }
            if (!dbExist)
            {
                panel2.Enabled = true;
                this.Size = new Size(782, 499); //with sidepanel
                settingsPanelExtended = true;
            }
            else this.Size = new Size(628, 499);settingsPanelExtended = false;

            if (rkApp.GetValue("sinnzrAppTime") == null) checkBox3.Checked = false;
            else checkBox3.Checked = true;
           
            loadSettings();
            timeTrackTimer.Start();
            automaticUpdateTimer.Start();
            if (!showPaths) dataGridView1.Columns[5].Visible = false;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (dbConnected)
            {
                OpenFileDialog ofd = new OpenFileDialog();
                ofd.Filter = "Executable Files (*.exe)|*.exe";
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    AddProgram(ofd.FileName, ofd.SafeFileName);
                }
            }
            else MessageBox.Show("Create a databank first.");
        }

        private void button2_Click(object sender, EventArgs e)
        {
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Filter = "SQLite Databank (*.sqlite)|*.sqlite|All files (*.*)|*.*";
            sfd.FileName = "AppTime.sqlite";
            if (sfd.ShowDialog() == DialogResult.OK)
            {
                closeDb(true);
                if (File.Exists(sfd.FileName)) File.Delete(sfd.FileName);
                dbLoc = sfd.FileName;
                using (RegistryKey Key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\sinnzrAppTime\", true))
                    if (Key != null) //SubKey already exists
                    {
                        try { SQLiteConnection.CreateFile(sfd.FileName); }
                        catch (IOException) { MessageBox.Show("IOException while trying to create database file."); }
                        finally {
                            Key.SetValue("databaseLocation", sfd.FileName);
                            dbExist = true;
                            databaseAvailableLabel.Visible = false;
                            connectDb(true, true);
                        }
                        
                    }
                    else //create key
                    {
                        try { SQLiteConnection.CreateFile(sfd.FileName); }
                        catch (IOException) { MessageBox.Show("IOException while trying to create database file."); }
                        finally {
                            using (RegistryKey SubKey = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\sinnzrAppTime"))
                                SubKey.SetValue("databaseLocation", sfd.FileName);
                            dbExist = true;
                            databaseAvailableLabel.Visible = false;
                            connectDb(true, true);
                        }
                    }
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            updatePrograms(true, false);
            updateSettings();
        }

        private void button4_Click(object sender, EventArgs e)
        {
            if (settingsPanelExtended)
            {
                panel2.Enabled = false;
                this.Size = new Size(628, 499);
                settingsPanelExtended = false;
            }
            else {
                panel2.Enabled = true;
                this.Size = new Size(782, 499);
                settingsPanelExtended = true;
            }
        }

        private void AddProgram(string path, string name)
        {
            string friendlyName;
            if (File.Exists(path))
            {
                FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(path);
                friendlyName = fvi.FileDescription;
                if (friendlyName == null) friendlyName = name;
            }
            else friendlyName = name;
            SQLiteCommand cmd = new SQLiteCommand("insert into programs (name, friendlyName, path, timeAdded) VALUES (@name, @friendlyName, @path, @timeAdded);", dbConnection);
            cmd.Parameters.AddWithValue("@name", name);
            cmd.Parameters.AddWithValue("@friendlyName", friendlyName);
            cmd.Parameters.AddWithValue("@path", path);
            cmd.Parameters.AddWithValue("@timeAdded", DateTime.Now);
            cmd.ExecuteNonQuery();

            TrackedProgram p = new TrackedProgram(path, name, friendlyName, 0);
            Programs.Add(p);
            updateLabel();
        }

        private void deleteProgram(int id)
        {
            if (dbConnected)
            {
                SQLiteCommand cmd = new SQLiteCommand("DELETE FROM programs WHERE name=@name AND path=@path", dbConnection);
                cmd.Parameters.AddWithValue("@name", Programs[id].Name);
                cmd.Parameters.AddWithValue("@path", Programs[id].Path);
                cmd.ExecuteNonQuery();
                Programs.RemoveAt(id);
                updateLabel();
                MessageBox.Show("Successfully deleted Program.");
            }
            else MessageBox.Show("Can't delete program: Not connected to database.");
        }

        private void updatePrograms(bool receiveSuccessMessage, bool automatic)
        {
            if (dbConnected)
            {
                foreach (TrackedProgram item in Programs)
                {
                    SQLiteCommand cmd = new SQLiteCommand("UPDATE programs SET runtime = @runtime, friendlyName = @friendlyName, lastTimeRun = @lastTimeRun WHERE name = @name AND path = @path", dbConnection);
                    cmd.Parameters.AddWithValue("@runtime", item.runtime);
                    cmd.Parameters.AddWithValue("@friendlyName", item.friendlyName);
                    cmd.Parameters.AddWithValue("@lastTimeRun", item.lastTimeRun);
                    cmd.Parameters.AddWithValue("@name", item.Name);
                    cmd.Parameters.AddWithValue("@path", item.Path);
                    cmd.ExecuteNonQuery();
                }
                if (receiveSuccessMessage) MessageBox.Show("Successfully updated database.");
                if (automatic) label3.Text = "Last updated: " + DateTime.Now.ToString("F") + " (Automatic update)";
                else {
                    label3.Text = "Last updated: " + DateTime.Now.ToString("F");
                    automaticUpdateTimer.Stop();
                    automaticUpdateTimer.Start();
                }
            }
            else MessageBox.Show("Can't update programs: not connected to a database.");
        }

        private void updateLabel()
        {
            dataGridView1.AllowUserToAddRows = true;
            dataGridView1.Rows.Clear();
            numPrograms = 0;
            string lastTimeRun = "Never";
            string friendlyName;
            foreach (TrackedProgram item in Programs)
            {
                int id = numPrograms;

                var days = TimeSpan.FromMinutes(item.runtime).Days;
                var hours = TimeSpan.FromMinutes(item.runtime).Hours;
                var minutes = TimeSpan.FromMinutes(item.runtime).Minutes;
                if (item.lastTimeRun != DateTime.MinValue) lastTimeRun = item.lastTimeRun.ToString("F");
                if (processIsRunning(item.Path)) friendlyName = item.friendlyName + " (Currently running)";
                else friendlyName = item.friendlyName;

                DataGridViewRow row = (DataGridViewRow)dataGridView1.Rows[0].Clone();
                row.Cells[0].Value = id;
                row.Cells[1].Value = friendlyName;
                row.Cells[2].Value = String.Format("{0} Days, {1} Hours, {2} Minutes", days, hours, minutes);
                row.Cells[3].Value = lastTimeRun;
                row.Cells[4].Value = item.timeAdded.ToString("F");
                if(showPaths)row.Cells[5].Value = item.Path;
                dataGridView1.Rows.Add(row);

                numPrograms++;
            }
            label1.Text = "Current number of tracked programs: " + numPrograms;
            dataGridView1.AllowUserToAddRows = false;
        }

        private void connectDb(bool firstTime, bool receiveSuccessMessage)
        {
            if (dbConnected) return;
            if (dbExist)
            {
                dbConnection = new SQLiteConnection("Data Source=" + dbLoc + ";Version=3;");
                dbConnection.Open();
                dbConnected = true;
                label6.Text = "Database location:\n" + dbLoc;
                if (firstTime)
                {
                    ExecuteQuery(@"CREATE TABLE programs (name VARCHAR(128), friendlyName VARCHAR(128), path VARCHAR(128), runtime INT default 0, lastTimeRun DATETIME default null, timeAdded DATETIME default CURRENT_TIMESTAMP);
                                   CREATE TABLE `config` (`settingkey`	VARCHAR(128),`settingstate`	INT DEFAULT null,`settingvalue`	VARCHAR(128) DEFAULT null);
                                   INSERT INTO config (settingkey, settingstate) VALUES ('showPaths', 0);
                                   INSERT INTO config (settingkey, settingstate) VALUES ('minimizeToTray', 0);");
                    MessageBox.Show("Successfully created new database.");
                }
                if (receiveSuccessMessage) MessageBox.Show("Sucessfully connected to SQLite database.");
                updateLabel();
            }
            else MessageBox.Show("Database doesn't exist, unable to connect.");
        }

        private void loadDatabase()
        {
            if (dbConnected)
            {
                string stm = "select * from programs";
                using (SQLiteCommand cmd = new SQLiteCommand(stm, dbConnection))
                {
                    using (SQLiteDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            TrackedProgram tp = new TrackedProgram();
                            tp.Path = reader["path"].ToString();
                            tp.Name = reader["name"].ToString();
                            tp.friendlyName = reader["friendlyName"].ToString();
                            tp.runtime = (int)reader["runtime"];
                            tp.lastTimeRun = reader["lastTimeRun"] as DateTime? ?? default(DateTime);
                            tp.timeAdded = (DateTime)reader["timeAdded"];
                            Programs.Add(tp);
                        }
                    }
                }
                updateLabel();
            }
            else MessageBox.Show("Can't load programs: not connected to a database.");
        }

        private void ExecuteQuery(string query)
        {
            if (dbConnected)
            {
                SQLiteCommand command = new SQLiteCommand(query, dbConnection);
                try { command.ExecuteNonQuery(); }
                catch (Exception ex){ MessageBox.Show("Error while executing SQL query: " + ex.Message); }
            }
            else MessageBox.Show("Error executing query: not connected to database.");
        }

        private bool processIsRunning(string path)
        {
            Process[] processes = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(path));
            if (processes.Length > 0) return true;
            else return false;
        }

        private void timeTrackTimer_Tick(object sender, EventArgs e)
        {
            foreach (TrackedProgram item in Programs)
            {
                if (processIsRunning(item.Path))
                {
                    item.runtime++;
                    item.lastTimeRun = DateTime.Now;
                }
            }
            updateLabel();
        }

        private void automaticUpdateTimer_Tick(object sender, EventArgs e)
        {
            updatePrograms(false, true);
        }
        
        public void updateSettings()
        {
            if (dbConnected)
            {
                int showPathsState;
                int minimizeToTrayState;
                SQLiteCommand cmd = new SQLiteCommand("UPDATE config SET settingstate=@settingstate WHERE settingkey='showPaths';", dbConnection);
                if (showPaths) showPathsState = 1;
                else showPathsState = 0;
                cmd.Parameters.AddWithValue("@settingstate", showPathsState);
                cmd.ExecuteNonQuery();

                if (minimizeToTray) minimizeToTrayState = 1;
                else minimizeToTrayState = 0;
                cmd.CommandText = "UPDATE config SET settingstate=@settingstate WHERE settingkey='minimizeToTray'";
                cmd.Parameters.AddWithValue("@settingstate", minimizeToTrayState);
                cmd.ExecuteNonQuery();
            }
        }

        public void loadSettings()
        {
            if (dbConnected)
            {
                SQLiteCommand cmd = new SQLiteCommand("SELECT settingstate FROM config WHERE settingkey='showPaths'", dbConnection);
                if (Convert.ToInt32(cmd.ExecuteScalar()) == 0) { this.showPaths = false; checkBox1.Checked = false; }
                else { this.showPaths = true; checkBox1.Checked = true; }

                cmd.CommandText = "SELECT settingstate FROM config WHERE settingkey='minimizeToTray'";
                if (Convert.ToInt32(cmd.ExecuteScalar()) == 0) { this.minimizeToTray = false; checkBox2.Checked = false; }
                else { this.minimizeToTray = true; checkBox2.Checked = true; }

                cmd.CommandText = "SELECT settingstate FROM config WHERE settingkey='version'";
                version = Convert.ToInt32(cmd.ExecuteScalar());
            }
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            showPaths = checkBox1.Checked;
            updateLabel();
            if (showPaths) dataGridView1.Columns[5].Visible = true;
            else dataGridView1.Columns[5].Visible = false;
        }

        private void checkBox2_CheckedChanged(object sender, EventArgs e)
        {
            minimizeToTray = checkBox2.Checked;
        }

        private void checkBox3_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox3.Checked) rkApp.SetValue("sinnzrAppTime", Application.ExecutablePath);
            else rkApp.DeleteValue("sinnzrAppTime", false);
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            closeDb(false);
            Application.Exit();
        }

        private void closeDb(bool runGC)
        {
            if (dbConnected)
            {
                updatePrograms(false, true);
                updateSettings();
                dbConnection.Close();
                dbConnected = false;
                Programs.Clear();
                if (runGC)
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                }
            }
            else return;
        }

        private void Form1_Resize(object sender, EventArgs e)
        {
            if (minimizeToTray)
            {
                if (FormWindowState.Minimized == this.WindowState)
                {
                    if (FormWindowState.Minimized == this.WindowState)
                    {
                        notifyIcon1.Visible = true;
                        this.Hide();
                    }
                    else if (FormWindowState.Normal == this.WindowState)
                    {
                        notifyIcon1.Visible = false;
                    }
                }
            }
        }

        private void notifyIcon1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            notifyIcon1.Visible = false;
            this.Show();
            this.WindowState = FormWindowState.Normal;
        }

        private void dataGridView1_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            var senderGrid = (DataGridView)sender;

            if (senderGrid.Columns[e.ColumnIndex] is DataGridViewButtonColumn &&
            e.RowIndex >= 0)
            {
                DialogResult dialogResult = MessageBox.Show("Do you really want to remove this program?", "Delete Program", MessageBoxButtons.YesNo);
                if (dialogResult == DialogResult.Yes)
                    deleteProgram(e.RowIndex);
                
            }
        }

        private void button5_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "SQLite Databank (*.sqlite)|*.sqlite|All files (*.*)|*.*";
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                if (dbConnected) closeDb(false);

                dbLoc = ofd.FileName;
                using (RegistryKey SubKey = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\sinnzrAppTime"))
                    SubKey.SetValue("databaseLocation", ofd.FileName);
                dbExist = true;
                databaseAvailableLabel.Visible = false;
                connectDb(false, true);
                loadSettings();
                loadDatabase();
                updateLabel();
                MessageBox.Show("Successfully imported database.");
            }
            else MessageBox.Show("Please select a valid databank.");
        }

        private void button6_Click(object sender, EventArgs e)
        {
            foreach(TrackedProgram program in Programs)
            {
                if (File.Exists(program.Path))
                {
                    FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(program.Path);
                    program.friendlyName = fvi.FileDescription;
                    if (program.friendlyName == null) program.friendlyName = program.Name;
                }
            }
            updateLabel();
        }
    }

    class TrackedProgram
    {
        public string Path;
        public string Name;
        public string friendlyName;
        public int runtime = 0; //in minutes
        public DateTime lastTimeRun;// = new DateTime(2017, 1, 1, 1, 1, 1, 1);
        public DateTime timeAdded = DateTime.Now;// = DateTime.Now;
        public TrackedProgram(string selectedPath = "None Given", string selectedName = "None Given", string selectedFriendlyName = "None Given", int selectedRuntime = 0, DateTime? selectedTimeAdded = null, DateTime? selectedLastTimeRun = null)
        {
            Path = selectedPath;
            Name = selectedName;
            friendlyName = selectedFriendlyName;
            selectedRuntime = runtime;
            selectedLastTimeRun = lastTimeRun;
            selectedTimeAdded = timeAdded;
        }
    }
}
