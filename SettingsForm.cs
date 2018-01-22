using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;


namespace AppTime
{
    public partial class SettingsForm : Form
    {
        public SettingsForm()
        {
            InitializeComponent();   
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            if (Application.OpenForms["Form1"] != null)
            {
                (Application.OpenForms["Form1"] as Form1).changeSetting("showPaths", checkBox1.Checked);
            }
        }

        private void checkBox2_CheckedChanged(object sender, EventArgs e)
        {
            if (Application.OpenForms["Form1"] != null)
            {
                (Application.OpenForms["Form1"] as Form1).changeSetting("minimizeToTray", checkBox2.Checked);
            }
        }

        private void checkBox3_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox3.Checked) Form1.rkApp.SetValue("sinnzrAppTime", Application.ExecutablePath);
            else Form1.rkApp.DeleteValue("sinnzrAppTime", false);
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (Application.OpenForms["Form1"] != null)(Application.OpenForms["Form1"] as Form1).createDb();
            label6.Text = "Database location:\n" + Form1.dbLoc;
            if (Form1.showPaths) checkBox1.Checked = true;
            if (Form1.minimizeToTray) checkBox2.Checked = true;
        }

        private void SettingsForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            this.Hide();
        }

        private void SettingsForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
            }
        }

        private void SettingsForm_Shown(object sender, EventArgs e)
        {
            label6.Text = "Database location:\n" + Form1.dbLoc;
            if (Form1.rkApp.GetValue("sinnzrAppTime") == null) checkBox3.Checked = false;
            else checkBox3.Checked = true;

            if (Form1.showPaths) checkBox1.Checked = true;
            if (Form1.minimizeToTray) checkBox2.Checked = true;
        }

        private void button5_Click(object sender, EventArgs e)
        {
            if (Application.OpenForms["Form1"] != null)(Application.OpenForms["Form1"] as Form1).importDb();
            label6.Text = "Database location:\n" + Form1.dbLoc;
            if (Form1.showPaths) checkBox1.Checked = true;
            if (Form1.minimizeToTray) checkBox2.Checked = true;
        }

        private void button3_Click(object sender, EventArgs e)
        {
            if (Application.OpenForms["Form1"] != null)(Application.OpenForms["Form1"] as Form1).updateDb(true, false);
        }

        private void button6_Click(object sender, EventArgs e)
        {
            if (Application.OpenForms["Form1"] != null)(Application.OpenForms["Form1"] as Form1).fixFriendlyNames();
        }
    }
}
