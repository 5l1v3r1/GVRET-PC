﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GVRET
{
    public partial class FileLoadingForm : Form
    {

        MainForm parent;
        List<CANFrame> loadedFrames;
        private bool playbackActive = false, playbackForward = true;
        private int playbackPos = 0;
        private int numFrames;
        MicroLibrary.MicroTimer fastTimer;

        public FileLoadingForm()
        {
            InitializeComponent();
            fastTimer = new MicroLibrary.MicroTimer();
            fastTimer.MicroTimerElapsed +=
               new MicroLibrary.MicroTimer.MicroTimerElapsedEventHandler(OnTimedEvent);
            fastTimer.Interval = 20000; //in microseconds
        }

        private void FileLoadingForm_Load(object sender, EventArgs e)
        {
            updateFrameCounter();
        }

        public void setParent(MainForm val)
        {
            parent = val;
            //parent.onGotCANFrame += GotCANFrame;
        }

        private void updateFrameCounter()
        {
            lblFrames.Text = (playbackPos + 1).ToString() + " of " + numFrames.ToString();
        }

        private void updatePosition(bool forward)
        {
            
            if (forward)
            {
                if (playbackPos < (numFrames - 1)) playbackPos++;
                else if (ckLoop.Checked) playbackPos = 0;
            }
            else
            {
                if (playbackPos > 0) playbackPos--;
                else if (ckLoop.Checked) playbackPos = numFrames - 1;
            }
            //Debug.Print(playbackPos.ToString());
            loadedFrames[playbackPos].timestamp = Utility.GetTimeMS();
            parent.sideloadFrame(loadedFrames[playbackPos]);
            int bus = cbCANSend.SelectedIndex;
            //index 0 is none, 1 is Bus 0, 2 is bus 1, 3 is from file.
            if (bus == 1) parent.SendCANFrame(loadedFrames[playbackPos], 0);
            if (bus == 2) parent.SendCANFrame(loadedFrames[playbackPos], 1);
            if (bus == 3) parent.SendCANFrame(loadedFrames[playbackPos], loadedFrames[playbackPos].bus);
            //updateFrameCounter();
        }

        private void loadNativeCSVFile(string filename)
        {
            CANFrame thisFrame;
            long temp;

            System.Globalization.NumberStyles style;

            if (ckUseHex.Checked) style = System.Globalization.NumberStyles.HexNumber;
            else style = System.Globalization.NumberStyles.Integer;

            StreamReader csvStream = null;

            csvStream = new StreamReader(filename);
            csvStream.ReadLine(); //ignore header line
            loadedFrames = new List<CANFrame>();
            while (!csvStream.EndOfStream)
            {
                string thisLine = csvStream.ReadLine();
                if (thisLine.Length > 1)
                {
                    string[] theseTokens = thisLine.Split(',');
                    thisFrame = new CANFrame();
                    if (theseTokens[0].Length > 10)
                    {
                        temp = Int64.Parse(theseTokens[0]);
                        DateTime stamp = DateTime.MinValue.AddTicks(temp);
                        thisFrame.timestamp = (UInt32)(((stamp.Hour * 3600) + (stamp.Minute * 60) + (stamp.Second) * 1000) + stamp.Millisecond);
                    }
                    else
                    {
                        thisFrame.timestamp = Utility.GetTimeMS();
                    }
                    thisFrame.ID = int.Parse(theseTokens[1], style);
                    thisFrame.extended = bool.Parse(theseTokens[2]);
                    thisFrame.bus = int.Parse(theseTokens[3]);
                    thisFrame.len = int.Parse(theseTokens[4]);
                    for (int c = 0; c < 8; c++) thisFrame.data[c] = 0;
                    for (int d = 0; d < thisFrame.len; d++) thisFrame.data[d] = byte.Parse(theseTokens[5 + d], style);
                    loadedFrames.Add(thisFrame);
                }
            }
            csvStream.Close();
        }

        private void loadGenericCSVFile(string filename)
        {
            CANFrame thisFrame;
            long temp;

            System.Globalization.NumberStyles style;

            if (ckUseHex.Checked) style = System.Globalization.NumberStyles.HexNumber;
            else style = System.Globalization.NumberStyles.Integer;

            StreamReader csvStream = new StreamReader(filename);

            loadedFrames = new List<CANFrame>();
            while (!csvStream.EndOfStream)
            {
                string thisLine = csvStream.ReadLine();
                if (thisLine.Length > 1)
                {
                    string[] theseTokens = thisLine.Split(',');
                    thisFrame = new CANFrame();
                    if (theseTokens.Length > 1)
                    {
                        thisFrame.timestamp = Utility.GetTimeMS();
                        thisFrame.ID = int.Parse(theseTokens[0], style);
                        if (thisFrame.ID > 0x7FF) thisFrame.extended = true;
                        else thisFrame.extended = false;
                        thisFrame.bus = 0;

                        string[] dataBytes = theseTokens[1].Split(' ');
                        thisFrame.len = dataBytes.Length;
                        if (thisFrame.len > 8) thisFrame.len = 8;
                        for (int c = 0; c < 8; c++) thisFrame.data[c] = 0;
                        for (int d = 0; d < thisFrame.len; d++) thisFrame.data[d] = byte.Parse(dataBytes[d], style);

                        loadedFrames.Add(thisFrame);
                    }
                }
            }
            csvStream.Close();
        }


        private void loadMicrochipFile(string filename)
        {
            CANFrame thisFrame;
            bool inComment = false;
            StreamReader logStream = new StreamReader(filename);
            string thisLine;

            loadedFrames = new List<CANFrame>();

            while (!logStream.EndOfStream)
            {
                thisLine = logStream.ReadLine();

                if (thisLine.StartsWith("//"))
                {
                    inComment = !inComment;
                }
                else
                {

                    /*
                    tokens:
                    0 = timestamp
                    1 = Transmission direction
                    2 = ID
                    3 = Data byte length
                    4-x = The data bytes
                    */
                    if (thisLine.Length > 1 && !inComment)
                    {
                        string[] theseTokens = thisLine.Split(';');
                        thisFrame = new CANFrame();
                        thisFrame.timestamp = Utility.GetTimeMS();
                        thisFrame.ID = Utility.ParseStringToNum(theseTokens[2]);
                        if (thisFrame.ID <= 0x7FF) thisFrame.extended = false;
                        else thisFrame.extended = true;
                        thisFrame.bus = 0;
                        thisFrame.len = int.Parse(theseTokens[3]);
                        for (int c = 0; c < 8; c++) thisFrame.data[c] = 0;
                        for (int d = 0; d < thisFrame.len; d++) thisFrame.data[d] = (byte)Utility.ParseStringToNum(theseTokens[4 + d]);
                        loadedFrames.Add(thisFrame);
                    }
                }
            }
            logStream.Close();
        }

        private void loadLogFile(string filename)
        {
            CANFrame thisFrame;
            StreamReader logStream = new StreamReader(filename);
            string thisLine;
            System.Globalization.NumberStyles style;

            if (ckUseHex.Checked) style = System.Globalization.NumberStyles.HexNumber;
            else style = System.Globalization.NumberStyles.Integer;

            loadedFrames = new List<CANFrame>();
            
            while (!logStream.EndOfStream)
            {
                thisLine = logStream.ReadLine();

                if (thisLine.StartsWith("***")) continue;

                /*
                tokens:
                0 = timestamp
                1 = Transmission direction
                2 = Channel
                3 = ID
                4 = Type (s = standard, I believe x = extended)
                5 = Data byte length
                6-x = The data bytes
                */
                if (thisLine.Length > 1)
                {
                    string[] theseTokens = thisLine.Split(' ');
                    thisFrame = new CANFrame();
                    thisFrame.timestamp = Utility.GetTimeMS();
                    thisFrame.ID = int.Parse(theseTokens[3].Substring(2), style);
                    if (theseTokens[4] == "s") thisFrame.extended = false;
                    else thisFrame.extended = true;                    
                    thisFrame.bus = int.Parse(theseTokens[2]) - 1;
                    thisFrame.len = int.Parse(theseTokens[5]);
                    for (int c = 0; c < 8; c++) thisFrame.data[c] = 0;
                    for (int d = 0; d < thisFrame.len; d++) thisFrame.data[d] = byte.Parse(theseTokens[6 + d], style);
                    loadedFrames.Add(thisFrame);
                }
            }
            logStream.Close();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            string filename;
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                filename = openFileDialog1.FileName;
                string fnSmall = filename.ToLower();

                //doing the try/catch here allows for a generic handling of any file loading related problems.
                try
                {
                    switch (openFileDialog1.FilterIndex)
                    {
                        case 1:
                            loadNativeCSVFile(filename);
                            break;
                        case 2:
                            loadGenericCSVFile(filename);
                            break;
                        case 3:
                            loadLogFile(filename);
                            break;
                        case 4:
                            loadMicrochipFile(filename);
                            break;
                        default:
                            break;
                    }
                }
                catch (Exception ee)
                {
                    MessageBox.Show("Error while loading file! Aborted!");
                }
                numFrames = loadedFrames.Count;

                //If the user does not intend to use the playback system then immediately add all of the frames to the
                //main form instantly but make it seem like they came in 10ms apart to give them all a unique time stamp
                if (!cbUsePlayback.Checked)
                {
                    UInt32 theTime = 0;
                    for (int i = 0; i < numFrames; i++)
                    {
                        theTime += 10;
                        loadedFrames[i].timestamp = theTime;
                        parent.sideloadFrame(loadedFrames[i]);
                    }
                }

                //updateFrameCounter();
            }
        }

        private void numPlaybackSpeed_ValueChanged(object sender, EventArgs e)
        {
            fastTimer.Interval = (int)numPlaybackSpeed.Value;
        }

        private void btnBackOne_Click(object sender, EventArgs e)
        {
            fastTimer.Stop(); //pushing this button halts automatic playback
            playbackActive = false;

            updatePosition(false);
            
        }

        private void btnForwardOne_Click(object sender, EventArgs e)
        {
            fastTimer.Stop(); //pushing this button halts automatic playback
            playbackActive = false;

            updatePosition(true);
            
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            fastTimer.Stop(); //pushing this button halts automatic playback
            playbackActive = false;
            playbackPos = 0;
            updateFrameCounter();
        }

        private void btnPause_Click(object sender, EventArgs e)
        {
            fastTimer.Stop(); //pushing this button pauses automatic playback
            playbackActive = false;
        }

        private void btnReverse_Click(object sender, EventArgs e)
        {
            playbackActive = true;
            playbackForward = false;
            fastTimer.Start();
        }

        private void btnPlay_Click(object sender, EventArgs e)
        {
            playbackActive = true;
            playbackForward = true;
            fastTimer.Start();
        }

        private void OnTimedEvent(object sender,
                                  MicroLibrary.MicroTimerEventArgs timerEventArgs)
        {
            //Debug.Print("tick");
            if (!playbackActive)
            {
                fastTimer.Stop();
                return;
            }
            if (playbackForward)
            {
                //Debug.Print("forward");
                updatePosition(true);
            }
            else
            {
                //Debug.Print("back");
                updatePosition(false);
            }

            if (!ckLoop.Checked)
            {
                if (playbackPos == 0) playbackActive = false;
                if (playbackPos == (numFrames - 1)) playbackActive = false;
            }
        }

        private void timer2_Tick(object sender, EventArgs e)
        {
            updateFrameCounter();
        }

        private void ckUseHex_CheckedChanged(object sender, EventArgs e)
        {

        }

        private void cbCANSend_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void cbUsePlayback_CheckedChanged(object sender, EventArgs e)
        {
            if (cbUsePlayback.Checked)
            {
                groupBox3.Enabled = true;
                if (cbCANSend.SelectedIndex < 0) cbCANSend.SelectedIndex = 0;
            }
            else
            {
                groupBox3.Enabled = false;
            }
        }
    }
}
