﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using MissionPlanner.Comms;
using MissionPlanner.Utilities;

namespace MissionPlanner
{
    public partial class SerialOutputNMEA : Form
    {
        static double updaterate = 5;
        System.Threading.Thread t12;
        static bool threadrun = false;
        static internal SerialPort comPort = new SerialPort();
        static internal PointLatLngAlt HomeLoc = new PointLatLngAlt(0, 0, 0, "Home");

        public SerialOutputNMEA()
        {
            InitializeComponent();

            CMB_serialport.DataSource = SerialPort.GetPortNames();

            CMB_updaterate.Text = updaterate + "hz";

            if (threadrun)
            {
                BUT_connect.Text = Strings.Stop;
            }

            MissionPlanner.Utilities.Tracking.AddPage(this.GetType().ToString(), this.Text);
        }

        private void BUT_connect_Click(object sender, EventArgs e)
        {
            if (comPort.IsOpen)
            {
                threadrun = false;
                comPort.Close();
                BUT_connect.Text = Strings.Connect;
            }
            else
            {
                try
                {
                    comPort.PortName = CMB_serialport.Text;
                }
                catch
                {
                    CustomMessageBox.Show(Strings.InvalidPortName);
                    return;
                }
                try
                {
                    comPort.BaudRate = int.Parse(CMB_baudrate.Text);
                }
                catch
                {
                    CustomMessageBox.Show(Strings.InvalidBaudRate);
                    return;
                }
                try
                {
                    comPort.Open();
                }
                catch
                {
                    CustomMessageBox.Show("Error Connecting\nif using com0com please rename the ports to COM??");
                    return;
                }

                t12 = new System.Threading.Thread(new System.Threading.ThreadStart(mainloop))
                {
                    IsBackground = true,
                    Name = "Nmea output"
                };
                t12.Start();

                BUT_connect.Text = Strings.Stop;
            }
        }

        void mainloop()
        {
            threadrun = true;
            comPort.NewLine = "\r\n";
            int counter = 0;
            while (threadrun)
            {
                try
                {
                    double lat = (int) MainV2.comPort.MAV.cs.lat +
                                 ((MainV2.comPort.MAV.cs.lat - (int) MainV2.comPort.MAV.cs.lat)*.6f);
                    double lng = (int) MainV2.comPort.MAV.cs.lng +
                                 ((MainV2.comPort.MAV.cs.lng - (int) MainV2.comPort.MAV.cs.lng)*.6f);
                    string line = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "$GP{0},{1:HHmmss.fff},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},", "GGA",
                        DateTime.Now.ToUniversalTime(), Math.Abs(lat*100).ToString("0.00000"), MainV2.comPort.MAV.cs.lat < 0 ? "S" : "N",
                        Math.Abs(lng*100).ToString("0.00000"), MainV2.comPort.MAV.cs.lng < 0 ? "W" : "E",
                        MainV2.comPort.MAV.cs.gpsstatus >= 3 ? 1 : 0, MainV2.comPort.MAV.cs.satcount,
                        MainV2.comPort.MAV.cs.gpshdop, MainV2.comPort.MAV.cs.altasl, "M", 0, "M", "");

                    string checksum = GetChecksum(line);
                    comPort.WriteLine(line + "*" + checksum);

                    line = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "$GP{0},{1:HHmmss.fff},{2},{3},{4},{5},{6},{7},{8},{9:ddMMyy},{10},", "RMC",
                        DateTime.Now.ToUniversalTime(), "A", Math.Abs(lat*100).ToString("0.00000"),
                        MainV2.comPort.MAV.cs.lat < 0 ? "S" : "N", Math.Abs(lng*100).ToString("0.00000"),
                        MainV2.comPort.MAV.cs.lng < 0 ? "W" : "E", (MainV2.comPort.MAV.cs.groundspeed*1.943844).ToString("0.0"),
                        MainV2.comPort.MAV.cs.groundcourse.ToString("0.0"), DateTime.Now, 0);

                    checksum = GetChecksum(line);
                    comPort.WriteLine(line + "*" + checksum);

                    if (counter%20 == 0 && HomeLoc.Lat != 0 && HomeLoc.Lng != 0)
                    {
                        line = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                            "$GP{0},{1:HHmmss.fff},{2},{3},{4},{5},{6},{7},", "HOM", DateTime.Now.ToUniversalTime(),
                            Math.Abs(HomeLoc.Lat*100).ToString("0.00000"), HomeLoc.Lat < 0 ? "S" : "N", Math.Abs(HomeLoc.Lng*100).ToString("0.00000"),
                            HomeLoc.Lng < 0 ? "W" : "E", HomeLoc.Alt, "M");

                        checksum = GetChecksum(line);
                        comPort.WriteLine(line + "*" + checksum);
                    }

                    line = string.Format(System.Globalization.CultureInfo.InvariantCulture, "$GP{0},{1},{2},{3},", "RPY",
                        MainV2.comPort.MAV.cs.roll.ToString("0.00000"), MainV2.comPort.MAV.cs.pitch.ToString("0.00000"), MainV2.comPort.MAV.cs.yaw.ToString("0.00000"));

                    checksum = GetChecksum(line);
                    comPort.WriteLine(line + "*" + checksum);
                    
                    var nextsend = DateTime.Now.AddMilliseconds(1000 / updaterate);
                    var sleepfor = Math.Min((int)Math.Abs((nextsend - DateTime.Now).TotalMilliseconds), 4000);
                    System.Threading.Thread.Sleep(sleepfor);
                    counter++;
                }
                catch
                {
                }
            }
        }

        private void SerialOutput_FormClosing(object sender, FormClosingEventArgs e)
        {
        }

        // Calculates the checksum for a sentence
        string GetChecksum(string sentence)
        {
            // Loop through all chars to get a checksum
            int Checksum = 0;
            foreach (char Character in sentence.ToCharArray())
            {
                switch (Character)
                {
                    case '$':
                        // Ignore the dollar sign
                        break;
                    case '*':
                        // Stop processing before the asterisk
                        continue;
                    default:
                        // Is this the first value for the checksum?
                        if (Checksum == 0)
                        {
                            // Yes. Set the checksum to the value
                            Checksum = Convert.ToByte(Character);
                        }
                        else
                        {
                            // No. XOR the checksum with this character's value
                            Checksum = Checksum ^ Convert.ToByte(Character);
                        }
                        break;
                }
            }
            // Return the checksum formatted as a two-character hexadecimal
            return Checksum.ToString("X2");
        }

        private void CMB_updaterate_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                updaterate = float.Parse(CMB_updaterate.Text.Replace("hz", ""));
            }
            catch
            {
                CustomMessageBox.Show(Strings.InvalidUpdateRate, Strings.ERROR);
            }
        }
    }
}