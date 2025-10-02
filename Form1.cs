using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using Newtonsoft.Json;

namespace AutoClickerApp
{
    public partial class Form1 : Form
    {
        // WinAPI for mouse control
        [DllImport("user32.dll")]
        static extern bool SetCursorPos(int X, int Y);

        [DllImport("user32.dll")]
        static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);

        const uint MOUSEEVENTF_LEFTDOWN = 0x02;
        const uint MOUSEEVENTF_LEFTUP = 0x04;

        // Global F10
        const int MY_F10_ID = 1;
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        System.Windows.Forms.Timer clickTimer = new System.Windows.Forms.Timer();
        System.Windows.Forms.Timer scrollTimer = new System.Windows.Forms.Timer();

        List<ClickPoint> clickPoints = new List<ClickPoint>();
        int mainPointIndex = -1;
        string saveFile = "points.json";

        Random rnd = new Random();
        int scrollCounter = 0; // <-- FIX: class-level counter

        public Form1()
        {
            InitializeComponent();

            // Register global F10
            RegisterHotKey(this.Handle, MY_F10_ID, 0, (uint)Keys.F10);

            this.Text = "Advanced Auto Clicker";
            this.Width = 700;
            this.Height = 550;
            this.KeyPreview = true;
            this.KeyDown += Form1_KeyDown;

            // --- Controls ---
            var btnRecord = new Button() { Text = "Record Point", Top = 10, Left = 10, Width = 120 };
            var btnDelete = new Button() { Text = "Delete All", Top = 40, Left = 10, Width = 120 };
            var btnDeleteSelected = new Button() { Text = "Delete Selected", Top = 70, Left = 10, Width = 120 };
            var btnStart = new Button() { Text = "Start", Top = 100, Left = 10, Width = 120 };
            var btnStop = new Button() { Text = "Stop", Top = 130, Left = 10, Width = 120 };
            var lstPoints = new ListBox() { Top = 10, Left = 150, Width = 500, Height = 250 };
            lstPoints.DisplayMember = "Display";
            var btnSetMain = new Button() { Text = "Set Main Point", Top = 270, Left = 10, Width = 120 };
            var lblMainPoint = new Label() { Text = "Main Point: None", Top = 310, Left = 10, Width = 400 };

            var lblSpeed = new Label() { Text = "Click Interval (ms):", Top = 340, Left = 10, Width = 120 };
            var nudInterval = new NumericUpDown() { Top = 340, Left = 140, Width = 60, Minimum = 1, Maximum = 1000, Value = 10 };

            var chkScroll = new CheckBox() { Text = "Drag Scroll Up/Down", Top = 370, Left = 10, Width = 180 };
            var lblScrollInterval = new Label() { Text = "Every N ticks:", Top = 370, Left = 200, Width = 100 };
            var nudScrollInterval = new NumericUpDown() { Top = 370, Left = 300, Width = 60, Minimum = 1, Maximum = 100, Value = 10 };

            var lblDragDistance = new Label() { Text = "Drag Distance (px):", Top = 400, Left = 10, Width = 120 };
            var nudDragDistance = new NumericUpDown() { Top = 400, Left = 140, Width = 60, Minimum = 10, Maximum = 1000, Value = 100 };

            var lblDragDuration = new Label() { Text = "Drag Duration (ms):", Top = 430, Left = 10, Width = 130 };
            var nudDragDuration = new NumericUpDown() { Top = 430, Left = 150, Width = 60, Minimum = 50, Maximum = 5000, Value = 300 };

            var lblOtherWeight = new Label() { Text = "Other Points Weight:", Top = 460, Left = 10, Width = 130 };
            var nudOtherWeight = new NumericUpDown() { Top = 460, Left = 150, Width = 60, Minimum = 1, Maximum = 100, Value = 1 };

            this.Controls.AddRange(new Control[] {
                btnRecord, btnDelete, btnDeleteSelected, btnStart, btnStop, lstPoints, btnSetMain, lblMainPoint,
                lblSpeed, nudInterval, chkScroll, lblScrollInterval, nudScrollInterval,
                lblDragDistance, nudDragDistance, lblDragDuration, nudDragDuration,
                lblOtherWeight, nudOtherWeight
            });

            // --- Click Timer ---
            clickTimer.Interval = (int)nudInterval.Value;
            clickTimer.Tick += (s, e) =>
            {
                if (clickPoints.Count == 0) return;

                var cp = GetWeightedPoint(clickPoints, mainPointIndex, (int)nudOtherWeight.Value);
                SetCursorPos(cp.Point.X, cp.Point.Y);
                mouse_event(MOUSEEVENTF_LEFTDOWN | MOUSEEVENTF_LEFTUP, (uint)cp.Point.X, (uint)cp.Point.Y, 0, UIntPtr.Zero);
            };
            nudInterval.ValueChanged += (s, e) => clickTimer.Interval = (int)nudInterval.Value;

            // --- Scroll Timer ---
            scrollTimer.Interval = 50; // small tick
            scrollTimer.Tick += (s, e) =>
            {
                if (!clickTimer.Enabled) return; // only scroll if active
                if (chkScroll.Checked)
                {
                    scrollCounter++;
                    if (scrollCounter >= (int)nudScrollInterval.Value)
                    {
                        // Drag up
                        DragMouse(-(int)nudDragDistance.Value, (int)nudDragDuration.Value);
                        System.Threading.Thread.Sleep(200); // pause before drag down
                        // Drag down
                        DragMouse((int)nudDragDistance.Value, (int)nudDragDuration.Value);

                        scrollCounter = 0; // reset counter after drag
                    }
                }
            };
            scrollTimer.Start();

            // --- Buttons ---
            btnRecord.Click += (s, e) => RecordPoint(lstPoints);
            btnDelete.Click += (s, e) => { DeleteAllPoints(lstPoints, lblMainPoint); };
            btnDeleteSelected.Click += (s, e) => { DeleteSelectedPoint(lstPoints, lblMainPoint); };
            btnStart.Click += (s, e) => { StartClicker(); scrollCounter = 0; };
            btnStop.Click += (s, e) => { StopClicker(); };

            btnSetMain.Click += (s, e) =>
            {
                if (lstPoints.SelectedIndex >= 0)
                {
                    mainPointIndex = lstPoints.SelectedIndex;
                    lblMainPoint.Text = $"Main Point: {lstPoints.SelectedItem}";
                }
            };

            // --- Load Points ---
            LoadPoints(lstPoints);
        }

        void RecordPoint(ListBox lstPoints)
        {
            var pos = Cursor.Position;
            var cp = new ClickPoint() { Point = pos };
            clickPoints.Add(cp);
            lstPoints.Items.Add(cp);
            SavePoints();
        }

        ClickPoint GetWeightedPoint(List<ClickPoint> points, int mainIndex, int otherWeight)
        {
            List<ClickPoint> weightedList = new List<ClickPoint>();
            for (int i = 0; i < points.Count; i++)
            {
                int weight = (i == mainIndex) ? 50 : otherWeight;
                for (int j = 0; j < weight; j++)
                    weightedList.Add(points[i]);
            }
            return weightedList[rnd.Next(weightedList.Count)];
        }

        void DragMouse(int deltaY, int durationMs)
        {
            Point start = Cursor.Position;
            mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, UIntPtr.Zero);

            int steps = 20;
            for (int i = 1; i <= steps; i++)
            {
                int y = start.Y + deltaY * i / steps;
                SetCursorPos(start.X, y);
                System.Threading.Thread.Sleep(durationMs / steps);
            }

            mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);
        }

        void SavePoints() => File.WriteAllText(saveFile, JsonConvert.SerializeObject(clickPoints));

        void LoadPoints(ListBox lstPoints)
        {
            if (File.Exists(saveFile))
            {
                clickPoints = JsonConvert.DeserializeObject<List<ClickPoint>>(File.ReadAllText(saveFile));
                foreach (var p in clickPoints) lstPoints.Items.Add(p);
            }
        }

        void DeleteAllPoints(ListBox lstPoints, Label lblMainPoint)
        {
            clickPoints.Clear();
            lstPoints.Items.Clear();
            mainPointIndex = -1;
            lblMainPoint.Text = "Main Point: None";
            if (File.Exists(saveFile)) File.Delete(saveFile);
        }

        void DeleteSelectedPoint(ListBox lstPoints, Label lblMainPoint)
        {
            int idx = lstPoints.SelectedIndex;
            if (idx >= 0)
            {
                clickPoints.RemoveAt(idx);
                lstPoints.Items.RemoveAt(idx);
                if (mainPointIndex == idx)
                {
                    mainPointIndex = -1;
                    lblMainPoint.Text = "Main Point: None";
                }
                SavePoints();
            }
        }

        void StartClicker()
        {
            clickTimer.Start();
            scrollTimer.Start();
            scrollCounter = 0;
        }

        void StopClicker()
        {
            clickTimer.Stop();
            scrollTimer.Stop();
            scrollCounter = 0;
        }

        private void Form1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F8 || e.KeyCode == Keys.T)
            {
                RecordPoint(this.Controls.OfType<ListBox>().First());
            }

            if (e.KeyCode == Keys.F10)
            {
                StopClicker();
            }
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_HOTKEY = 0x0312;
            if (m.Msg == WM_HOTKEY)
            {
                int id = m.WParam.ToInt32();
                if (id == MY_F10_ID)
                    StopClicker();
            }
            base.WndProc(ref m);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            UnregisterHotKey(this.Handle, MY_F10_ID);
            base.OnFormClosing(e);
        }
    }

    public class ClickPoint
    {
        public Point Point { get; set; }
        public override string ToString() => $"({Point.X}, {Point.Y})";
        public string Display => ToString();
    }
}
