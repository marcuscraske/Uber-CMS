﻿/*
 * UBERMEAT FOSS
 * ****************************************************************************************
 * License:                 Creative Commons Attribution-ShareAlike 3.0 unported
 *                          http://creativecommons.org/licenses/by-sa/3.0/
 * 
 * Project:                 UberLib.CC128
 * File:                    /CC128.cs
 * Author(s):               limpygnome						limpygnome@gmail.com
 * To-do/bugs:              none
 * 
 * Contains all of the classes required for this library to interact with the CC128 energy monitor
 * device by CurrentCost Ltd; information for this library has derived from:
 * http://www.currentcost.com/cc128/xml.htm
 * 
 * You will also need to use a 3.3v RJ45 to serial/USB cable to interface with the CC128, opposed
 * to a standard 5v.
 */
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Xml;
using System.Threading;
using System.IO.Ports;

namespace UberLib.CC128
{
    public class EnergyMonitor
    {
        #region "Event Delegates"
        public delegate void _eventNewSensorData(EnergyReading reading);
        public delegate void _eventNewSensorDataMalformed(string data, Exception ex);
        public delegate void _eventStateChange(State newState);
        #endregion

        #region "Events"
        /// <summary>
        /// Raised when new sensor data is available to read.
        /// </summary>
        public event _eventNewSensorData eventNewSensorData;
        /// <summary>
        /// Raised when sensor data is unable to be read.
        /// </summary>
        public event _eventNewSensorDataMalformed eventNewSensorDataMalformed;
        /// <summary>
        /// Raised when the state of the connection changes with the power monitor.
        /// </summary>
        public event _eventStateChange eventStateChange;
        #endregion

        #region "Enums"
        public enum State
        {
            /// <summary>
            /// Thrown when the start method is invoked and an error occurs, not allowing for sensor data to be collected; this is typically due to e.g. the energy meter being inaccessible/unplugged.
            /// </summary>
            ErrorOccurredOnStart,
            Running,
            NotRunning,
            RuntimeError,
            DeviceDisconnected
        }
        #endregion

        #region "Variables"
        private SerialPort serialPort = null;
        private State state = State.NotRunning;
        private string port;
        private Thread disconnectThread = null;
        private DateTime lastCommunicated;
        #endregion

        #region "Methods - Constructors"
        public EnergyMonitor()
        {
            port = "COM1";
        }
        /// <summary>
        /// </summary>
        /// <param name="port">E.g. COM1.</param>
        public EnergyMonitor(string port)
        {
            this.port = port;
        }
        /// <summary>
        /// </summary>
        /// <param name="port">E.g. COM1.</param>
        /// <param name="autoStart">If true, the connection will be automatically established when this class is instantiated.</param>
        public EnergyMonitor(string port, bool autoStart)
        {
            this.port = port;
            if (autoStart) start();
        }
        #endregion

        #region "Methods - Start/stop/serial-port handlers"
        /// <summary>
        /// Creates the serial port for communication wit hthe energy monitor.
        /// </summary>
        public void initSerialPort()
        {
            try
            {
                // Create the serial port and hook its events
                serialPort = new SerialPort();
                serialPort.BaudRate = 57600;
                serialPort.PortName = port;
                serialPort.Open();
                serialPort.ErrorReceived += new SerialErrorReceivedEventHandler(serialPort_ErrorReceived);
                serialPort.DataReceived += new SerialDataReceivedEventHandler(serialPort_DataReceived);
                // Launch thread for detecting time-out
                disconnectThread = new Thread(
                    delegate()
                    {
                        disconnectThreadWorker();
                    });
                lastCommunicated = DateTime.Now;
                disconnectThread.Start();
                // Update state
                state = State.Running;
            }
            catch
            {
                // Update state
                state = State.ErrorOccurredOnStart;
            }
            if (eventStateChange != null) eventStateChange(state);
        }
        /// <summary>
        /// Disposes the serial port for communication with the energy monitor.
        /// </summary>
        public void disposeSerialPort(bool deviceDisconnected)
        {
            try
            {
                serialPort.Close();
            }
            catch { }
            finally
            {
                serialPort.Dispose();
                serialPort = null;
            }
            if (deviceDisconnected)
                // Update the status
                state = State.DeviceDisconnected;
            else
                state = State.NotRunning;
            if (eventStateChange != null) eventStateChange(state);
            disconnectThread.Abort();
            disconnectThread = null;
        }
        /// <summary>
        /// Starts the connection.
        /// </summary>
        public void start()
        {
            if (serialPort != null) return;
            // Create serial port
            initSerialPort();
        }
        /// <summary>
        /// Used to detect if no data has been received for longer than seven seconds; if so, it's likely the energy monitor
        /// has been disconnected with the host.
        /// </summary>
        private void disconnectThreadWorker()
        {
            while (true)
            {
                if (DateTime.Now.Subtract(lastCommunicated).TotalSeconds > 15)
                    // Dispose port
                    disposeSerialPort(true);
                Thread.Sleep(1000);
            }
        }
        void serialPort_ErrorReceived(object sender, SerialErrorReceivedEventArgs e)
        {
            state = State.RuntimeError;
            if (eventStateChange != null) eventStateChange(state);
        }
        /// <summary>
        /// Stops the connection.
        /// </summary>
        public void stop()
        {
            // Dispose port
            disposeSerialPort(false);
        }
        void serialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            lastCommunicated = DateTime.Now;
            try
            {
                string data = serialPort.ReadLine();
                try
                {
                    EnergyReading re = EnergyReading.parse(data);
                    if (re != null && eventNewSensorData != null) eventNewSensorData(re);
                }
                catch (Exception ex)
                {
                    if (eventNewSensorDataMalformed != null) eventNewSensorDataMalformed(data, ex);
                }
            }
            catch { }
        }
        #endregion

        #region "Methods - Properties"
        public bool isRunning
        {
            get
            {
                return serialPort != null;
            }
        }
        public string Port
        {
            get
            {
                return port;
            }
        }
        public State ReadState
        {
            get
            {
                return state;
            }
        }
        #endregion
    }
    public class EnergyReading
    {
        #region "Variables"
        private string software;
        private string daysExecuted;
        private string currentTime;
        private float temperature;
        private string sensorID;
        private string radioID;
        public int[] sensorInts;
        #endregion

        #region "Methods - Constructors"
        public EnergyReading(string software, string daysExecuted, string currentTime, float temperature, string sensorID, string radioID, int[] sensorInts)
        {
            this.software = software;
            this.daysExecuted = daysExecuted;
            this.currentTime = currentTime;
            this.temperature = temperature;
            this.sensorID = sensorID;
            this.radioID = radioID;
            this.sensorInts = sensorInts;
        }
        #endregion

        #region "Methods - Properties"
        /// <summary>
        /// Returns the wattage of a sensor.
        /// </summary>
        /// <param name="sensor"></param>
        /// <returns></returns>
        public int this[int sensor]
        {
            get
            {
                return sensorInts[sensor];
            }
        }
        public string Software
        {
            get
            {
                return software;
            }
        }
        public string DaysExecuted
        {
            get
            {
                return daysExecuted;
            }
        }
        public string CurrentTime
        {
            get
            {
                return currentTime;
            }
        }
        public float Temperature
        {
            get
            {
                return temperature;
            }
        }
        public string SensorID
        {
            get
            {
                return sensorID;
            }
        }
        public string RadioID
        {
            get
            {
                return radioID;
            }
        }
        public int[] Sensors
        {
            get
            {
                return sensorInts;
            }
        }
        #endregion

        #region "Methods - Static"
        /// <summary>
        /// Tries to parse energy-reading data from the CC128; this will throw null if the data is
        /// malformed or return null if the data is not real-time (in-which case you should ignore the reading)
        /// because it's most likely history.
        /// </summary>
        /// <param name="data"></param>
        /// <param name="error"
        /// <returns></returns>
        /// <exception cref="InvalidDataException">Occurs when the data is malformed and cannot be read.</exception>
        public static EnergyReading parse(string data)
        {
            try
            {
                XmlDocument doc = new XmlDocument();
                // Load received data
                doc.LoadXml(data);
                // Validate the data is not history or any other xml data
                XmlElement dataSrc = doc["msg"]["src"];
                XmlElement dataDsb = doc["msg"]["dsb"];
                XmlElement dataTime = doc["msg"]["time"];
                XmlElement dataTmpr = doc["msg"]["tmpr"];
                XmlElement dataSensor = doc["msg"]["sensor"];
                XmlElement dataRadioID = doc["msg"]["id"];
                if (dataSrc == null || dataDsb == null || dataTime == null || dataTmpr == null || dataSensor == null || dataRadioID == null)
                    return null;
                // Load sensor data
                List<int> sensorData = new List<int>();
                for (int i = 1; i <= 9; i++)
                    if (doc["msg"]["ch" + i.ToString()] != null)
                        sensorData.Add(int.Parse(doc["msg"]["ch" + i.ToString()].InnerText));
                // Return instantiated object
                return new EnergyReading(dataSrc.InnerText, dataDsb.InnerText, dataTime.InnerText, float.Parse(dataTmpr.InnerText), dataSensor.InnerText, dataRadioID.InnerText, sensorData.ToArray());
            }
            catch (Exception ex)
            {
                throw new InvalidDataException("Failed to read sensor data - " + ex.Message, ex);
            }
        }
        #endregion
    }
    [Serializable]
    public class InvalidDataException : Exception
    {
        public InvalidDataException() : base() { }
        public InvalidDataException(string message) : base(message) { }
        public InvalidDataException(string message, Exception innerException) : base(message, innerException) { }
        public InvalidDataException(SerializationInfo info, StreamingContext context) : base(info, context) { }
    }
}