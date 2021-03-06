﻿using GraphLib;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GVRET
{
    public partial class GraphingForm : Form
    {

        public MainForm parent;
        private List<int> foundID = new List<int>();
        private static int numGraphs = 14;

        private List<CANFrame>[] frames;

        GraphData[] Graphs = new GraphData[numGraphs];

        public GraphingForm()
        {
            InitializeComponent();
        }

        public void setParent(MainForm val)
        {
            parent = val;
            parent.onGotCANFrame += GotCANFrame;
        }

        public delegate void GotCANDelegate(CANFrame frame);
        public void GotCANFrame(CANFrame frame)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new GotCANDelegate(GotCANFrame), frame);
            }
            else
            {

                //first of all, always try to keep track of every ID we've ever seen while this form is open
                //if we find one we haven't seen yet then add it
                //foundID is really only used as a fast way to know if the frame ID has been seen before.
                //I believe it to be faster to search than directly searching entries in the listbox
                if (!foundID.Contains(frame.ID))
                {
                    foundID.Add(frame.ID);
                    listFrameIDs.Items.Add("0x" + frame.ID.ToString("X4"));
                }
                /*
                if (frame.ID == targettedID)
                {

                }
                 * */
            }
        }

        /*
         * Run through the whole frame cache and turn it into an ordered list of frames by ID.
        */
        private void parseFrameCache()
        {
            List<CANFrame> pFrames = parent.FrameCache;

            int numFrames = pFrames.Count;
            int numIDs = foundID.Count;

            frames = new List<CANFrame>[numIDs];

            for (int c = 0; c < numIDs; c++) frames[c] = new List<CANFrame>();

                for (int i = 0; i < numFrames; i++)
                {
                    for (int j = 0; j < numIDs; j++)
                    {
                        if (pFrames[i].ID == foundID[j])
                        {
                            frames[j].Add(pFrames[i]);
                        }
                    }
                }
        }

        private int getIdxForID(int ID) 
        {
            for (int j = 0; j < foundID.Count; j++)
            {
                if (foundID[j] == ID) return j;
            }
            return -1;
        }

        private int getMaxFrames()
        {
            int max = 0;
            for (int j = 0; j < numGraphs; j++) 
            {
                if (Graphs[j].valueCache != null)
                {
                    if (Graphs[j].valueCache.Length > max) max = Graphs[j].valueCache.Length;
                }
            }
            return max;
        }

        private void GraphingForm_Load(object sender, EventArgs e)
        {
            List<CANFrame> frames = parent.FrameCache;

            for (int i = 0; i < frames.Count; i++)
            {
                if (!foundID.Contains(frames[i].ID))
                {
                    foundID.Add(frames[i].ID);
                    listFrameIDs.Items.Add("0x" + frames[i].ID.ToString("X4"));
                }
            }

            display.Smoothing = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        }

        protected void fillDataSource(DataSource src, int idx)
        {
            if (idx < 0 || idx > numGraphs - 1) return;
            for (int i = 0; i < src.Length; i++)
            {
                src.Samples[i].x = i;

                src.Samples[i].y = (float)Graphs[idx].valueCache[i];
            }
            src.OnRenderYAxisLabel = RenderYLabel;
        }


        private void setupGraphs()
        {
            int place = -1;
            this.SuspendLayout();

            display.DataSources.Clear();

            int totalFrames = getMaxFrames();

            display.SetDisplayRangeX(0f, (float)totalFrames);
            display.SetGridDistanceX((float)(totalFrames / 10.0));

            for (int graph = 0; graph < numGraphs; graph++)
            {
                if (Graphs[graph].valueCache != null)
                {
                    display.DataSources.Add(new DataSource());
                    place++;
                    if (graph < 6)
                    {
                        display.DataSources[place].Name = "Graph " + (graph + 1);
                    }
                    else 
                    {
                        display.DataSources[place].Name = "Byte " + (graph-6);
                    }
                    display.DataSources[place].AutoScaleX = false;
                    display.DataSources[place].OnRenderXAxisLabel += RenderXLabel;

                    display.DataSources[place].Length = Graphs[graph].valueCache.Length;
                    display.PanelLayout = PlotterGraphPaneEx.LayoutMode.NORMAL;
                    display.DataSources[place].AutoScaleY = false;
                    display.DataSources[place].SetDisplayRangeY((float)Graphs[graph].minVal, (float)Graphs[graph].maxVal);
                    float YDist = (float)((Graphs[graph].maxVal - Graphs[graph].minVal) / 5.0);
                    if (YDist < 1.0f) YDist = 1.0f;
                    display.DataSources[place].SetGridDistanceY(YDist);
                    display.DataSources[place].OnRenderYAxisLabel = RenderYLabel;
                    display.DataSources[place].GraphColor = Graphs[graph].color;
                    fillDataSource(display.DataSources[place], graph);
                }
            }

            //ApplyColorSchema();

            this.ResumeLayout();
            display.Refresh();
        }

        private String RenderXLabel(DataSource s, int idx)
        {
            return String.Format("{0}", (int)s.Samples[idx].x);
        }

        private String RenderYLabel(DataSource s, float value)
        {
            return String.Format("{0:0.0}", value);
        }


        private void pbColor_Click(object sender, EventArgs e)
        {
            if (colorDialog1.ShowDialog() == DialogResult.OK)
            {
                ((PictureBox)sender).BackColor = colorDialog1.Color;
            }
        }

        private void setupGraph(int which) 
        {

            if (which < 0 || which > (numGraphs - 1)) return;

            float bias, scale;
            int v1, v2, numFrames, idx, tempVal, stride;
            Boolean signed;

            Graphs[which].minVal = 9999999.0F;
            Graphs[which].maxVal = -9999999.0F;

            bias = Graphs[which].bias;
            scale = Graphs[which].scale;
            signed = Graphs[which].signed;
            stride = Graphs[which].stride;

            v1 = Graphs[which].B1;
            v2 = Graphs[which].B2;
            idx = getIdxForID(Graphs[which].ID);
            numFrames = frames[idx].Count / stride;

            Graphs[which].valueCache = new float[numFrames];

            Debug.Print("SetupGraph for " + which.ToString() + " V1: " + v1.ToString() + " V2: " + v2.ToString() + " numFrames: " + numFrames.ToString());

            if (v1 == -1) return;

            if (v1 < 128) //are we operating on whole bytes?
            {

                //three options here. There can be no val2 in which case this is a single byte value
                //and we can process from there. The second value can be larger than the first in which
                //case this is big endian. The second value can be smaller than the first in which case
                //this is little endian.
                if ((v2 == -1) || (v1 == v2)) //single byte value
                {
                    for (int j = 0; j < numFrames; j++)
                    {
                        tempVal = (frames[idx].ElementAt(j*stride).data[v1] & Graphs[which].mask);
                        if (signed && tempVal > 127)
                        {
                            tempVal = tempVal - 256;
                        }
                        Graphs[which].valueCache[j] = (tempVal + bias) * scale;
                        if (Graphs[which].valueCache[j] > Graphs[which].maxVal) Graphs[which].maxVal = Graphs[which].valueCache[j];
                        if (Graphs[which].valueCache[j] < Graphs[which].minVal) Graphs[which].minVal = Graphs[which].valueCache[j];
                    }
                }
                else if (v2 > v1)  //big endian
                {
                    float tempValue;
                    int tempValInt;
                    int numBytes = (v2 - v1) + 1;
                    int shiftRef = 1 << (numBytes * 8);
                    for (int j = 0; j < numFrames; j++)
                    {
                        tempValInt = 0;
                        int expon = 1;
                        for (int c = 0; c < numBytes; c++)
                        {
                            tempValInt += (frames[idx].ElementAt(j).data[v2 - c] * expon);
                            expon *= 256;
                        }

                        tempValInt &= Graphs[which].mask;

                        if (signed && tempValInt > ((shiftRef / 2) - 1))
                        {
                            tempValInt = tempValInt - shiftRef;
                        }

                        tempValue = (float)tempValInt;
                        Graphs[which].valueCache[j] = (tempValue + bias) * scale;
                        if (Graphs[which].valueCache[j] > Graphs[which].maxVal) Graphs[which].maxVal = Graphs[which].valueCache[j];
                        if (Graphs[which].valueCache[j] < Graphs[which].minVal) Graphs[which].minVal = Graphs[which].valueCache[j];
                    }
                }
                else //must be little endian then
                {
                    float tempValue;
                    int tempValInt;
                    int numBytes = (v1 - v2) + 1;
                    int shiftRef = 1 << (numBytes * 8);
                    for (int j = 0; j < numFrames; j++)
                    {
                        tempValInt = 0;
                        int expon = 1;
                        for (int c = 0; c < numBytes; c++)
                        {
                            tempValInt += frames[idx].ElementAt(j).data[v2 + c] * expon;
                            expon *= 256;
                        }
                        tempValInt &= Graphs[which].mask;

                        if (signed && tempValInt > ((shiftRef / 2) - 1))
                        {
                            tempValInt = tempValInt - shiftRef;
                        }

                        tempValue = (float)tempValInt;
                        Graphs[which].valueCache[j] = (tempValue + bias) * scale;
                        if (Graphs[which].valueCache[j] > Graphs[which].maxVal) Graphs[which].maxVal = Graphs[which].valueCache[j];
                        if (Graphs[which].valueCache[j] < Graphs[which].minVal) Graphs[which].minVal = Graphs[which].valueCache[j];
                    }
                }
            }
            else //doing bit level work
            {
 
            }
        }

        private void listFrameIDs_SelectedIndexChanged(object sender, EventArgs e)
        {
            int idx, numFrames, targettedID;
            int minLen, maxLen, thisLen;
            int[] minData = new int[8];
            int[] maxData = new int[8];
            int[,] dataHistogram = new int[256,8];
            TreeNode baseNode, dataBase, histBase, numBase;
            Color[] theseColors = { Color.White, Color.Red, Color.Blue, Color.Green, Color.Purple, Color.Pink, Color.Yellow, Color.SlateGray };

            if (listFrameIDs.SelectedIndex > -1)
            {
                parseFrameCache();
                targettedID = Utility.ParseStringToNum(listFrameIDs.Items[listFrameIDs.SelectedIndex].ToString());
                idx = getIdxForID(targettedID);
                numFrames = frames[idx].Count;

                treeDetails.Nodes.Clear();
                baseNode = treeDetails.Nodes.Add("ID: " + listFrameIDs.Items[listFrameIDs.SelectedIndex].ToString());
                if (frames[idx].ElementAt(0).extended) //if these frames seem to be extended then try for J1939 decoding
                {
                    J1939ID jid = new J1939ID();
                    jid.src = targettedID & 0xFF;
                    jid.priority = targettedID >> 26;
                    jid.pgn = (targettedID >> 8) & 0x3FFFF; //18 bits
                    jid.pf = (targettedID >> 16) & 0xFF;
                    jid.ps = (targettedID >> 8) & 0xFF;

                    if (jid.pf > 0xEF)
                    {
                        jid.isBroadcast = true;
                        jid.dest = 0xFFFF;
                        baseNode.Nodes.Add("Broadcast frame");
                    }
                    else
                    {
                        jid.dest = jid.ps;
                        baseNode.Nodes.Add("Destination ID: 0x" + jid.dest.ToString("X2"));
                    }
                    baseNode.Nodes.Add("SRC: 0x" + jid.src.ToString("X2"));
                    baseNode.Nodes.Add("PGN: " + jid.pgn.ToString());
                    baseNode.Nodes.Add("PF: 0x" + jid.pf.ToString("X2"));
                    baseNode.Nodes.Add("PS: 0x" + jid.ps.ToString("X2"));
                }
                treeDetails.Nodes.Add("# Of Frames: " + numFrames.ToString());
                minLen = 8;
                maxLen = 0;
                for (int i = 0; i < 8; i++)
                {
                    minData[i] = 256;
                    maxData[i] = -1;
                    for (int k = 0; k < 256; k++) dataHistogram[k, i] = 0;
                }
                for (int j = 0; j < numFrames; j++)
                {
                    thisLen = frames[idx].ElementAt(j).len;
                    if (thisLen > maxLen) maxLen = thisLen;
                    if (thisLen < minLen) minLen = thisLen;
                    for (int c = 0; c < thisLen; c++)
                    {
                        byte dat = frames[idx].ElementAt(j).data[c];
                        if (minData[c] > dat) minData[c] = dat;
                        if (maxData[c] < dat) maxData[c] = dat;
                        dataHistogram[dat, c]++; //add one to count for this 
                    }
                }
                if (minLen < maxLen)
                    baseNode = treeDetails.Nodes.Add("Data Length: " + minLen.ToString() + " to " + maxLen.ToString());
                else
                    baseNode = treeDetails.Nodes.Add("Data Length: " + minLen.ToString());

                for (int d = 0; d < numGraphs; d++)
                {
                    Graphs[d].valueCache = null;
                    Graphs[d].ID = 0;
                }

                for (int c = 0; c < maxLen; c++)
                {
                    Graphs[6 + c].bias = 0;
                    Graphs[6 + c].scale = 1;
                    Graphs[6 + c].mask = 0xFF;
                    Graphs[6 + c].B1 = c;
                    Graphs[6 + c].B2 = c;
                    Graphs[6 + c].ID = targettedID;
                    Graphs[6 + c].color = theseColors[c];
                    Graphs[6 + c].signed = false;
                    if (numFrames > 100)
                    {
                        Graphs[6 + c].stride = (int)((((float)numFrames) / 100.0f) + 0.5f);
                    }
                    else Graphs[6 + c].stride = 1;

                    dataBase = baseNode.Nodes.Add("Data Byte " + c.ToString());
                    dataBase.Nodes.Add("Range: " + minData[c] + " to " + maxData[c]);
                    histBase = dataBase.Nodes.Add("Histogram");
                    for (int d = 0; d < 256; d++)
                    {
                        if (dataHistogram[d, c] > 0)
                        {
                            numBase = histBase.Nodes.Add(d.ToString() + "/0x" + d.ToString("X2") + ": " + dataHistogram[d, c]);
                        }
                    }
                }                

                parseFrameCache();
                for (int c = 0; c < maxLen; c++) setupGraph(6 + c);                    
                setupGraphs();                
            }
            else
            {
            }
        }

        private void btnRefresh1_Click(object sender, EventArgs e)
        {
            int ID, B1, B2, whichGraph, stride;
            int Mask;
                    
            float Bias, Scale;
            String strID, strMask, strBytes, strBias, strScale, strStride;
            Color thisColor;
            Boolean signedVal;

            strID = "";
            strMask = "";
            strBytes = "";
            strBias = "";
            strScale = "";
            strStride = "";
            signedVal = false;
            whichGraph = 0;
            thisColor = pbColor1.BackColor;


            if (sender.Equals(btnRefresh1))
            {
                strID = txtID1.Text;
                strMask = txtMask1.Text;
                strBytes = txtByte1.Text;
                signedVal = cbSigned1.Checked;
                strBias = txtBias1.Text;
                strScale = txtScale1.Text;
                strStride = txtStride1.Text;
                thisColor = pbColor1.BackColor;
                whichGraph = 0;
            }
            else if (sender.Equals(btnRefresh2))
            {
                strID = txtID2.Text;
                strMask = txtMask2.Text;
                strBytes = txtByte2.Text;
                signedVal = cbSigned2.Checked;
                strBias = txtBias2.Text;
                strStride = txtStride2.Text;
                strScale = txtScale2.Text;
                thisColor = pbColor2.BackColor;
                whichGraph = 1;
            }
            else if (sender.Equals(btnRefresh3))
            {
                strID = txtID3.Text;
                strMask = txtMask3.Text;
                strBytes = txtByte3.Text;
                signedVal = cbSigned3.Checked;
                strBias = txtBias3.Text;
                strScale = txtScale3.Text;
                strStride = txtStride3.Text;
                thisColor = pbColor3.BackColor;
                whichGraph = 2;
            }
            else if (sender.Equals(btnRefresh4))
            {
                strID = txtID4.Text;
                strMask = txtMask4.Text;
                strBytes = txtByte4.Text;
                signedVal = cbSigned4.Checked;
                strBias = txtBias4.Text;
                strScale = txtScale4.Text;
                strStride = txtStride4.Text;
                thisColor = pbColor4.BackColor;
                whichGraph = 3;
            }
            else if (sender.Equals(btnRefresh5))
            {
                strID = txtID5.Text;
                strMask = txtMask5.Text;
                strBytes = txtByte5.Text;
                signedVal = cbSigned5.Checked;
                strBias = txtBias5.Text;
                strScale = txtScale5.Text;
                strStride = txtStride5.Text;
                thisColor = pbColor5.BackColor;
                whichGraph = 4;
            }
            else if (sender.Equals(btnRefresh6))
            {
                strID = txtID6.Text;
                strMask = txtMask6.Text;
                strBytes = txtByte6.Text;
                signedVal = cbSigned6.Checked;
                strBias = txtBias6.Text;
                strScale = txtScale6.Text;
                strStride = txtStride6.Text;
                thisColor = pbColor6.BackColor;
                whichGraph = 5;
            }
            try
            {
                ID = Utility.ParseStringToNum(strID);
                Mask = Utility.ParseStringToNum(strMask);
                Bias = float.Parse(strBias);
                Scale = float.Parse(strScale);
                stride = Utility.ParseStringToNum(strStride);
            }
            catch (FormatException fe) 
            {
                ID = 0;
                Mask = 0;
                Bias = 0f;
                Scale = 0f;
                stride = 1;
            }

            B1 = -1;
            B2 = -1;

            //remove the temporary graphs used if you clicked on an ID on the side list.
            for (int d = 0; d < 8; d++)
            {
                Graphs[6 + d].valueCache = null;
                Graphs[6 + d].ID = 0;
            }

            if (strBytes.ToUpper().StartsWith("B")) //bit based
            {
                try
                {
                    string[] values = strBytes.Substring(1).Split('-');
                    Debug.Print("Split values: " + values.Length.ToString());
                    if (values.Length > 0)
                    {
                        B1 = int.Parse(values[0]) + 128; //the +128 lets the subsequent work know that we're going to use bits instead of bytes
                        if (values.Length > 1)
                        {
                            B2 = int.Parse(values[1]);
                        }
                    }
                }
                catch (FormatException fee)
                {
                }
            }
            else { //byte based
                try
                {
                    string[] values = strBytes.Split('-');
                    Debug.Print("Split values: " + values.Length.ToString());
                    if (values.Length > 0)
                    {
                        B1 = int.Parse(values[0]);
                        if (values.Length > 1)
                        {
                            B2 = int.Parse(values[1]);
                        }
                    }
                }
                catch (FormatException fee) 
                { 
                }
            }

            //do some basic error checking. On error null out this graph and reprocess
            if ((B1 == -1 && B2 == -1) || B1 > 7 || B2 > 7)
            {
                Graphs[whichGraph].valueCache = null;
                setupGraphs();
                return;
            }

            Graphs[whichGraph].bias = Bias;
            Graphs[whichGraph].scale = Scale;
            Graphs[whichGraph].mask = Mask;
            Graphs[whichGraph].B1 = B1;
            Graphs[whichGraph].B2 = B2;
            Graphs[whichGraph].ID = ID;
            Graphs[whichGraph].color = thisColor;
            Graphs[whichGraph].signed = signedVal;
            Graphs[whichGraph].stride = stride;

            //At long last all input value parsing is done. Now recalculate the given graph.

            //Sort all frames into lists based on ID
            parseFrameCache();

            int idx = getIdxForID(ID);
            if (idx > -1)
            {
                setupGraph(whichGraph);                
            }
            else 
            {
                Graphs[whichGraph].valueCache = null;
            }

            setupGraphs();                
        }

        private void groupBox1_Enter(object sender, EventArgs e)
        {

        }

        private void txtMask4_TextChanged(object sender, EventArgs e)
        {

        }

        private void label38_Click(object sender, EventArgs e)
        {

        }
    }

    struct GraphData
    {
        public float[] valueCache;
        public float min, max;
        public float bias, scale;
        public int mask;
        public float minVal, maxVal;
        public int ID;
        public int B1, B2;
        public int stride;
        public Color color;
        public Boolean signed;
    }
}
