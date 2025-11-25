using System;
using System.IO.Ports;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using Steema.TeeChart.Styles;

namespace MotorMonitorWpf
{
    public partial class MainWindow : Window
    {
        SerialPort _serial;

        Line _seriesSetpoint;
        Line _seriesRPM;
        Line _seriesPWM;
        Points _seriesStatus;

        const int MaxPoints = 500;

        public MainWindow()
        {
            InitializeComponent();
            InitCharts();

            // ====== SET TITLE CHART ======
            tChartSetpoint.Header.Text = "SETPOINT RPM";
            tChartRPM.Header.Text = "RPM MOTOR (Feedback)";
            tChartPWM.Header.Text = "PWM OUTPUT (Duty Cycle)";
            tChartStatus.Header.Text = "STATUS MOTOR";

            // Font agar lebih jelas
            tChartSetpoint.Header.Font.Size = 15;
            tChartRPM.Header.Font.Size = 15;
            tChartPWM.Header.Font.Size = 15;
            tChartStatus.Header.Font.Size = 15;

            comboPorts.ItemsSource = SerialPort.GetPortNames().OrderBy(n => n).ToArray();
            if (comboPorts.Items.Count > 0)
                comboPorts.SelectedIndex = 0;
        }

        private void InitCharts()
        {
            _seriesSetpoint = new Line(tChartSetpoint.Chart);
            _seriesSetpoint.Title = "Setpoint RPM";
            _seriesSetpoint.Color = System.Drawing.Color.Green;

            _seriesRPM = new Line(tChartRPM.Chart);
            _seriesRPM.Title = "RPM Motor";
            _seriesRPM.Color = System.Drawing.Color.Red;

            _seriesPWM = new Line(tChartPWM.Chart);
            _seriesPWM.Title = "PWM Output";
            _seriesPWM.Color = System.Drawing.Color.Blue;

            _seriesStatus = new Points(tChartStatus.Chart);
            _seriesStatus.Title = "Status";
        }

        // ======================
        // CONNECT / DISCONNECT
        // ======================
        private void BtnConnect_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _serial = new SerialPort(comboPorts.SelectedItem.ToString(), 9600);
                _serial.DataReceived += Serial_DataReceived;
                _serial.Open();

                btnConnect.IsEnabled = false;
                btnDisconnect.IsEnabled = true;
                btnMotorOn.IsEnabled = true;
                btnMotorOff.IsEnabled = true;

                MessageBox.Show("Connected to " + comboPorts.SelectedItem);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Connection failed: " + ex.Message);
            }
        }

        private void BtnDisconnect_Click(object sender, RoutedEventArgs e)
        {
            if (_serial != null && _serial.IsOpen)
            {
                _serial.Close();
            }

            btnConnect.IsEnabled = true;
            btnDisconnect.IsEnabled = false;
            btnMotorOn.IsEnabled = false;
            btnMotorOff.IsEnabled = false;

            MessageBox.Show("Disconnected");
        }

        // ======================
        // MOTOR COMMAND
        // ======================
        private void BtnMotorOn_Click(object sender, RoutedEventArgs e)
        {
            if (_serial != null && _serial.IsOpen)
                _serial.WriteLine("ON");
        }

        private void BtnMotorOff_Click(object sender, RoutedEventArgs e)
        {
            if (_serial != null && _serial.IsOpen)
                _serial.WriteLine("OFF");
        }

        // ======================
        // SERIAL DATA HANDLER
        // ======================
        private void Serial_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            string line = _serial.ReadLine();
            Dispatcher.BeginInvoke(new Action(() => ProcessLine(line)));
        }

        private void ProcessLine(string line)
        {
            line = line.Trim();
            string[] parts = line.Split(',');

            if (parts.Length < 5) return;

            int setpoint = int.Parse(parts[0]);
            int rpm = int.Parse(parts[1]);
            int pwm = int.Parse(parts[2]);
            float volt = float.Parse(parts[3]);
            string status = parts[4];

            double x = Environment.TickCount;

            txtSetpoint.Text = setpoint.ToString();
            txtRPM.Text = rpm.ToString();
            txtPWM.Text = pwm.ToString();
            txtStatus.Text = status;

            _seriesSetpoint.Add(x, setpoint);
            _seriesRPM.Add(x, rpm);
            _seriesPWM.Add(x, pwm);

            int statusVal = status == "STABLE" ? 2 :
                            status == "RAMPING" ? 1 :
                            status == "OVERSHOOT" ? 3 : 0;

            _seriesStatus.Add(x, statusVal);

            Trim(_seriesSetpoint);
            Trim(_seriesRPM);
            Trim(_seriesPWM);
            Trim(_seriesStatus);
        }

        void Trim(Series s)
        {
            while (s.Count > MaxPoints) s.Delete(0);
        }
    }
}
