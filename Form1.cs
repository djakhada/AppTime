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

//#pragma warning disable 0169, 0414

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

        
        //settings        
        bool settingsPanelExtended;
        bool showPaths;
        bool minimizeToTray;

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
                this.Size = new Size(752, 499); //with sidepanel
                settingsPanelExtended = true;
            }
            else this.Size = new Size(601, 499);settingsPanelExtended = false;

            loadSettings();
            timeTrackTimer.Start();
            automaticUpdateTimer.Start();
        }

        private void ProgramDeleteButton_Click(object sender, EventArgs e, int id)
        {
            DialogResult dialogResult = MessageBox.Show("Do you really want to remove this program?", "Delete Program", MessageBoxButtons.YesNo);
            if (dialogResult == DialogResult.Yes)
            {
                deleteProgram(id);
            }
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
                dbLoc = sfd.FileName;
                using (RegistryKey Key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\sinnzrAppTime\", true))
                    if (Key != null) //SubKey already exists
                    {
                        SQLiteConnection.CreateFile(sfd.FileName);
                        Key.SetValue("databaseLocation", sfd.FileName);
                        dbExist = true;
                        databaseAvailableLabel.Visible = false;
                        connectDb(true, true);
                    }
                    else //create key
                    {
                        SQLiteConnection.CreateFile(sfd.FileName);
                        using (RegistryKey SubKey = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\sinnzrAppTime"))
                            SubKey.SetValue("databaseLocation", sfd.FileName);
                        dbExist = true;
                        databaseAvailableLabel.Visible = false;
                        connectDb(true, true);
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
                this.Size = new Size(601, 499);
                settingsPanelExtended = false;
            }
            else {
                panel2.Enabled = true;
                this.Size = new Size(752, 499);
                settingsPanelExtended = true;
            }
        }

        private void AddProgram(string path, string name)
        {
            SQLiteCommand cmd = new SQLiteCommand("insert into programs (name, path, timeAdded) VALUES (@name, @path, @timeAdded);", dbConnection);
            cmd.Parameters.AddWithValue("@name", name);
            cmd.Parameters.AddWithValue("@path", path);
            cmd.Parameters.AddWithValue("@timeAdded", DateTime.Now);
            cmd.ExecuteNonQuery();
            TrackedProgram p = new TrackedProgram(path, name, 0);
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
                    SQLiteCommand cmd = new SQLiteCommand("UPDATE programs SET runtime = @runtime, lastTimeRun = @lastTimeRun WHERE name = @name AND path = @path", dbConnection);
                    cmd.Parameters.AddWithValue("@runtime", item.runtime);
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
            for (int i = 0; i < numPrograms; i++)
            {
                panel1.Controls.RemoveByKey("programLabelID" + i);
                panel1.Controls.RemoveByKey("programDeleteButtonID" + i);
            }
            numPrograms = 0;
            string lastTimeRun = "Never";
            string Name;
            foreach (TrackedProgram item in Programs)
            {
                int id = numPrograms;
                Button programDeleteButton = new Button();
                programDeleteButton.Size = new Size(96, 37);
                programDeleteButton.Text = "Delete Program";
                programDeleteButton.Location = new Point(430, (id * 100) + 46);
                programDeleteButton.Name = "programDeleteButtonID" + id;
                programDeleteButton.Click += new EventHandler((sender, e) => ProgramDeleteButton_Click(sender, e, id));

                Label programLabel = new Label();
                programLabel.Location = new Point(0, (id * 100) + 12);
                programLabel.Font = new Font(programLabel.Font.FontFamily, 10);
                programLabel.AutoSize = true;
                programLabel.Name = "programLabelID" + id;

                string s = "";
                string Path = "";
                var days = TimeSpan.FromMinutes(item.runtime).Days;
                var hours = TimeSpan.FromMinutes(item.runtime).Hours;
                var minutes = TimeSpan.FromMinutes(item.runtime).Minutes;
                if (item.lastTimeRun != DateTime.MinValue) lastTimeRun = item.lastTimeRun.ToString("F");
                if (processIsRunning(item.Path)) Name = item.Name + " (Currently running)";
                else Name = item.Name;
                if (showPaths == true) Path = "\nPath: " + item.Path;
                s =
                    s +
                    "Name: " +
                    Name + "    [ID: " + numPrograms + "]" +
                    "\nActive run time: " +
                    String.Format("{0} Days, {1} Hours, {2} Minutes", days, hours, minutes) +
                    "\nLast time run: " +
                    lastTimeRun +
                    "\nTime added: " +
                    item.timeAdded.ToString("F") + Path;

                programLabel.Text = s;
                panel1.Controls.Add(programLabel);
                panel1.Controls.Add(programDeleteButton);
                programDeleteButton.BringToFront();

                numPrograms++;
            }
            label1.Text = "Current number of tracked programs: " + numPrograms;
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
                    ExecuteQuery(@"CREATE TABLE programs (name VARCHAR(128), path VARCHAR(128), runtime INT default 0, lastTimeRun DATETIME default null, timeAdded DATETIME default CURRENT_TIMESTAMP);
                                   CREATE TABLE `config` (`settingkey`	VARCHAR(128),`settingstate`	INT DEFAULT null,`settingvalue`	VARCHAR(128) DEFAULT null);
                                   INSERT INTO config (settingkey, settingstate) VALUES ('showPaths', 0);
                                   INSERT INTO config (settingkey, settingstate) VALUES ('minimizeToTray', 0);");
                }
                if (receiveSuccessMessage) MessageBox.Show("Sucessfully connected to SQLite database.");
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
                command.ExecuteNonQuery();
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
            }
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            showPaths = checkBox1.Checked;
            updateLabel();
        }

        private void checkBox2_CheckedChanged(object sender, EventArgs e)
        {
            minimizeToTray = checkBox2.Checked;
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (dbConnected)
            {
                updatePrograms(false, true);
                updateSettings();
                dbConnection.Close();
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
    }

    class TrackedProgram
    {
        public string Path;
        public string Name;
        public int runtime = 0; //in minutes
        public DateTime lastTimeRun;// = new DateTime(2017, 1, 1, 1, 1, 1, 1);
        public DateTime timeAdded = DateTime.Now;// = DateTime.Now;
        public TrackedProgram(string selectedPath = "None Given", string selectedName = "None Given", int selectedRuntime = 0, DateTime? selectedTimeAdded = null, DateTime? selectedLastTimeRun = null)
        {
            Path = selectedPath;
            Name = selectedName;
            selectedRuntime = runtime;
            selectedLastTimeRun = lastTimeRun;
            selectedTimeAdded = timeAdded;
        }
    }
}
