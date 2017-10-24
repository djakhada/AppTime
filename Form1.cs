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
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace AppTime
{
    public partial class Form1 : Form
    {
        List<TrackedProgram> Programs = new List<TrackedProgram>();
        int numPrograms = 0; 

        //Database
        public SQLiteConnection dbConnection;
        public static string dbLoc;
        bool dbConnected = false;
        bool dbExist = false;

        //Settings   
        SettingsForm settingsForm = new SettingsForm();
        public static bool showPaths;
        public static bool minimizeToTray;
        public static RegistryKey rkApp = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);

        [DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
        private static extern IntPtr GetForegroundWindow();



        public Form1()
        {
            InitializeComponent();

            bool isElevated;
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                isElevated = principal.IsInRole(WindowsBuiltInRole.Administrator);
            }

            if (!isElevated) MessageBox.Show("Program not run as administrator.\nIt is highly encouraged that you run this program as an administrator as you might encounter errors without the necessary permissions.");

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
                        loadDb();
                    }
                }
            if (!dbExist)settingsForm.Show();
           
            timeTrackTimer.Start();
            automaticUpdateTimer.Start();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (dbConnected)
            {
                contextMenuStrip1.Show(button1, 0, button1.Height);
            }
            else MessageBox.Show("Create a databank first.");
        }

        private void contextMenuStrip1_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
            if (e.ClickedItem.Text == "Select executable file...")
            {
                contextMenuStrip1.Hide();
                OpenFileDialog ofd = new OpenFileDialog();
                ofd.Filter = "Executable Files (*.exe)|*.exe";
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    AddProgram(ofd.FileName);
                }
            }
            else if (e.ClickedItem.Text == "Select from list of currently running processes...")
            {
                new ProcessForm().Show();
            }
        }

        public void createDb()
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

        private void button4_Click(object sender, EventArgs e)
        {
            if (!settingsForm.Visible) settingsForm.Show();
            else settingsForm.Hide();

        }

        public void AddProgram(string path)
        {
            if (!Programs.Any(item => item.Path == path))
            {
                string friendlyName;
                string name = Path.GetFileName(path);
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
                updateProgramList();
            }
            else MessageBox.Show(path + " is already in list.");
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
                updateProgramList();
            }
            else MessageBox.Show("Can't delete program: Not connected to database.");
        }

        public void updateDb(bool receiveSuccessMessage, bool automatic)
        {
            if (dbConnected)
            {
                foreach (TrackedProgram item in Programs)
                {
                    using (SQLiteCommand cmd = new SQLiteCommand("UPDATE programs SET runtime = @runtime, activeRuntime = @activeRuntime, friendlyName = @friendlyName, lastTimeRun = @lastTimeRun WHERE name = @name AND path = @path", dbConnection))
                    {
                        cmd.Parameters.AddWithValue("@runtime", item.runtime);
                        cmd.Parameters.AddWithValue("@activeRuntime", item.activeRuntime);
                        cmd.Parameters.AddWithValue("@friendlyName", item.friendlyName);
                        cmd.Parameters.AddWithValue("@lastTimeRun", item.lastTimeRun);
                        cmd.Parameters.AddWithValue("@name", item.Name);
                        cmd.Parameters.AddWithValue("@path", item.Path);
                        cmd.ExecuteNonQuery();
                    }
                }
                int showPathsState;
                if (showPaths) showPathsState = 1;
                else showPathsState = 0;

                int minimizeToTrayState;
                if (minimizeToTray) minimizeToTrayState = 1;
                else minimizeToTrayState = 0;

                using (SQLiteCommand cmd = new SQLiteCommand(dbConnection))
                {
                    cmd.CommandText = @"UPDATE config SET settingstate=@showPathState WHERE settingkey='showPaths';
                                    UPDATE config SET settingstate=@minimizeToTrayState WHERE settingkey='minimizeToTray';
                                    UPDATE config SET settingvalue=@settingvalue0 WHERE settingkey='column0Width';
                                    UPDATE config SET settingvalue=@settingvalue1 WHERE settingkey='column1Width';
                                    UPDATE config SET settingvalue=@settingvalue2 WHERE settingkey='column2Width';
                                    UPDATE config SET settingvalue=@settingvalue3 WHERE settingkey='column3Width';
                                    UPDATE config SET settingvalue=@settingvalue4 WHERE settingkey='column4Width';
                                    UPDATE config SET settingvalue=@settingvalue5 WHERE settingkey='column5Width';
                                    UPDATE config SET settingvalue=@settingvalue6 WHERE settingkey='column6Width';
                                    UPDATE config SET settingvalue=@formWidth WHERE settingkey='formWidth';
                                    UPDATE config SET settingvalue=@formHeight WHERE settingkey='formHeight';";
                    cmd.Parameters.AddWithValue("@showPathState", showPathsState);
                    cmd.Parameters.AddWithValue("@minimizeToTrayState", minimizeToTrayState);
                    cmd.Parameters.AddWithValue("@settingvalue0", dataGridView1.Columns[0].Width);
                    cmd.Parameters.AddWithValue("@settingvalue1", dataGridView1.Columns[1].Width);
                    cmd.Parameters.AddWithValue("@settingvalue2", dataGridView1.Columns[2].Width);
                    cmd.Parameters.AddWithValue("@settingvalue3", dataGridView1.Columns[3].Width);
                    cmd.Parameters.AddWithValue("@settingvalue4", dataGridView1.Columns[4].Width);
                    cmd.Parameters.AddWithValue("@settingvalue5", dataGridView1.Columns[5].Width);
                    cmd.Parameters.AddWithValue("@settingvalue6", dataGridView1.Columns[6].Width);
                    cmd.Parameters.AddWithValue("@formWidth", this.Width);
                    cmd.Parameters.AddWithValue("@formHeight", this.Height);
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

        private void updateProgramList()
        {
            dataGridView1.AllowUserToAddRows = true;
            dataGridView1.Rows.Clear();
            numPrograms = 0;
            string lastTimeRun = "Never";
            string friendlyName;

            foreach (TrackedProgram item in Programs)
            {
                DataGridViewRow row = (DataGridViewRow)dataGridView1.Rows[0].Clone();
                var Tdays = TimeSpan.FromMinutes(item.runtime).Days;
                var Thours = TimeSpan.FromMinutes(item.runtime).Hours;
                var Tminutes = TimeSpan.FromMinutes(item.runtime).Minutes;
                var Adays = TimeSpan.FromMinutes(item.activeRuntime).Days;
                var Ahours = TimeSpan.FromMinutes(item.activeRuntime).Hours;
                var Aminutes = TimeSpan.FromMinutes(item.activeRuntime).Minutes;
                if (item.lastTimeRun != DateTime.MinValue) lastTimeRun = item.lastTimeRun.ToString("F");
                if (processIsRunning(item.Path))
                {
                    friendlyName = item.friendlyName + " (Currently running)";
                    row.DefaultCellStyle.BackColor = SystemColors.ControlLight;
                }
                else {
                    friendlyName = item.friendlyName;
                    row.DefaultCellStyle.BackColor = SystemColors.Window;
                }
      
                row.Cells[0].Value = numPrograms;
                row.Cells[1].Value = friendlyName;
                row.Cells[2].Value = String.Format("{0} Days, {1} Hours, {2} Minutes", Adays, Ahours, Aminutes);
                row.Cells[3].Value = String.Format("{0} Days, {1} Hours, {2} Minutes", Tdays, Thours, Tminutes);
                row.Cells[4].Value = lastTimeRun;
                row.Cells[5].Value = item.timeAdded.ToString("F");
                if(showPaths)row.Cells[6].Value = item.Path;
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
                if (firstTime)
                {
                    ExecuteQuery(@"CREATE TABLE programs (name VARCHAR(128), friendlyName VARCHAR(128), path VARCHAR(128), runtime INT default 0, activeRuntime	INT DEFAULT 0, lastTimeRun DATETIME default null, timeAdded DATETIME default CURRENT_TIMESTAMP);
                                   CREATE TABLE `config` (`settingkey`	VARCHAR(128),`settingstate`	INT DEFAULT null,`settingvalue`	VARCHAR(128) DEFAULT null);
                                   INSERT INTO config (settingkey, settingstate) VALUES ('showPaths', 0);
                                   INSERT INTO config (settingkey, settingstate) VALUES ('minimizeToTray', 0);
                                   INSERT INTO config (settingkey, settingvalue) VALUES ('column0Width', 30);
                                   INSERT INTO config (settingkey, settingvalue) VALUES ('column1Width', 100);
                                   INSERT INTO config (settingkey, settingvalue) VALUES ('column2Width', 100);
                                   INSERT INTO config (settingkey, settingvalue) VALUES ('column3Width', 100);
                                   INSERT INTO config (settingkey, settingvalue) VALUES ('column4Width', 100);
                                   INSERT INTO config (settingkey, settingvalue) VALUES ('column5Width', 100);
                                   INSERT INTO config (settingkey, settingvalue) VALUES ('column6Width', 100);
                                   INSERT INTO config (settingkey, settingvalue) VALUES ('formWidth', 733);
                                   INSERT INTO config (settingkey, settingvalue) VALUES ('formHeight', 439);");
                    MessageBox.Show("Successfully created new database.");
                }
                if (receiveSuccessMessage) MessageBox.Show("Sucessfully connected to SQLite database.");
                updateProgramList();
            }
            else MessageBox.Show("Database doesn't exist, unable to connect.");
        }

        private void loadDb()
        {
            if (dbConnected)
            {
                using (SQLiteCommand cmd = new SQLiteCommand("select * from programs", dbConnection))
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
                            tp.activeRuntime = (int)reader["activeRuntime"];
                            tp.lastTimeRun = reader["lastTimeRun"] as DateTime? ?? default(DateTime);
                            tp.timeAdded = (DateTime)reader["timeAdded"];
                            Programs.Add(tp);
                        }
                    }
                }
                updateProgramList();

                using (SQLiteCommand cmd = new SQLiteCommand("SELECT settingstate FROM config WHERE settingkey='showPaths'", dbConnection))
                {
                    if (Convert.ToInt32(cmd.ExecuteScalar()) == 0) changeSetting("showPaths", false);
                    else changeSetting("showPaths", true);

                    cmd.CommandText = "SELECT settingstate FROM config WHERE settingkey='minimizeToTray'";
                    if (Convert.ToInt32(cmd.ExecuteScalar()) == 0) minimizeToTray = false;
                    else minimizeToTray = true;

                    cmd.CommandText = "SELECT settingvalue FROM config WHERE settingkey='column0Width';";
                    dataGridView1.Columns[0].Width = Convert.ToInt32(cmd.ExecuteScalar());
                    cmd.CommandText = "SELECT settingvalue FROM config WHERE settingkey='column1Width';";
                    dataGridView1.Columns[1].Width = Convert.ToInt32(cmd.ExecuteScalar());
                    cmd.CommandText = "SELECT settingvalue FROM config WHERE settingkey='column2Width';";
                    dataGridView1.Columns[2].Width = Convert.ToInt32(cmd.ExecuteScalar());
                    cmd.CommandText = "SELECT settingvalue FROM config WHERE settingkey='column3Width';";
                    dataGridView1.Columns[3].Width = Convert.ToInt32(cmd.ExecuteScalar());
                    cmd.CommandText = "SELECT settingvalue FROM config WHERE settingkey='column4Width';";
                    dataGridView1.Columns[4].Width = Convert.ToInt32(cmd.ExecuteScalar());
                    cmd.CommandText = "SELECT settingvalue FROM config WHERE settingkey='column5Width';";
                    dataGridView1.Columns[5].Width = Convert.ToInt32(cmd.ExecuteScalar());
                    cmd.CommandText = "SELECT settingvalue FROM config WHERE settingkey='column6Width';";
                    dataGridView1.Columns[6].Width = Convert.ToInt32(cmd.ExecuteScalar());
                    cmd.CommandText = "SELECT settingvalue FROM config WHERE settingkey='formWidth';";
                    this.Width = Convert.ToInt32(cmd.ExecuteScalar());
                    cmd.CommandText = "SELECT settingvalue FROM config WHERE settingkey='formHeight';";
                    this.Height = Convert.ToInt32(cmd.ExecuteScalar());
                }
            }
            else MessageBox.Show("Can't load programs: not connected to a database.");
        }

        private void ExecuteQuery(string query)
        {
            if (dbConnected)
            {
                using (SQLiteCommand command = new SQLiteCommand(query, dbConnection))
                {
                    try { command.ExecuteNonQuery(); }
                    catch (Exception ex) { MessageBox.Show("Error while executing SQL query: " + ex.Message); }
                }
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
                var activatedHandle = GetForegroundWindow();
                Process[] processes = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(item.Path));
                if (processes.Length > 0)
                { 
                    item.runtime++;
                    item.lastTimeRun = DateTime.Now;
                    if(processes.Any(p => p.MainWindowHandle == activatedHandle || p.Handle == activatedHandle))
                    {
                        item.activeRuntime++;
                    }
                }
            }
            updateProgramList();
        }

        private void automaticUpdateTimer_Tick(object sender, EventArgs e)
        {
            updateDb(false, true);
        }
        
        public void changeSetting(string setting, bool val)
        {
            if(setting == "showPaths")
            {
                showPaths = val;
                dataGridView1.Columns[6].Visible = val;
                updateProgramList();
            }
            else if (setting == "minimizeToTray")
            {
                minimizeToTray = val;
            }
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
                updateDb(false, true);
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

        public void importDb()
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
                loadDb();
                updateProgramList();
                MessageBox.Show("Successfully imported database.");
            }
            else MessageBox.Show("Please select a valid databank.");
        }

        public void fixFriendlyNames()
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
            updateProgramList();
        }

        
    }

    class TrackedProgram
    {
        public string Path;
        public string Name;
        public string friendlyName;
        public int runtime = 0; //in minutes
        public int activeRuntime = 0;
        public DateTime lastTimeRun;// = new DateTime(2017, 1, 1, 1, 1, 1, 1);
        public DateTime timeAdded = DateTime.Now;// = DateTime.Now;
        public TrackedProgram(string selectedPath = "None Given", string selectedName = "None Given", string selectedFriendlyName = "None Given", int selectedRuntime = 0, int selectedactiveRuntime = 0, DateTime? selectedTimeAdded = null, DateTime? selectedLastTimeRun = null)
        {
            activeRuntime = selectedactiveRuntime;
            Path = selectedPath;
            Name = selectedName;
            friendlyName = selectedFriendlyName;
            selectedRuntime = runtime;
            selectedLastTimeRun = lastTimeRun;
            selectedTimeAdded = timeAdded;
        }
    }
}
