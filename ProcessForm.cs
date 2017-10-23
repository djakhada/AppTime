using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using System.Diagnostics;

namespace AppTime
{
    public partial class ProcessForm : Form
    {
        public ProcessForm()
        {
            InitializeComponent();
        }

        private void ProcessForm_Shown(object sender, EventArgs e)
        {
            dataGridView1.AllowUserToAddRows = true;
            Process[] processlist = Process.GetProcesses();
            string description;
            foreach (Process process in processlist)
            {
                try
                {
                    DataGridViewRow row = (DataGridViewRow)dataGridView1.Rows[0].Clone();
                    if (process.MainModule.FileVersionInfo.FileDescription == null) description = process.ProcessName;
                    else description = process.MainModule.FileVersionInfo.FileDescription;
                    row.Cells[0].Value = process.ProcessName;
                    row.Cells[1].Value = description;
                    row.Cells[2].Value = process.MainModule.FileName;
                    dataGridView1.Rows.Add(row);
                }
                catch
                {
                    continue;
                }
            }
            dataGridView1.AllowUserToAddRows = false;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            this.Hide();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            foreach(DataGridViewRow r in dataGridView1.SelectedRows)
            {
                if (Application.OpenForms["Form1"] != null) (Application.OpenForms["Form1"] as Form1).AddProgram((string)r.Cells[2].Value);
            }
            this.Hide();
        }
    }
}
