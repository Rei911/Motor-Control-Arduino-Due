using System;
using System.IO.Ports;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using Steema.TeeChart.WPF;                 // namespace TeeChart WPF
using Steema.TeeChart.WPF;
using Steema.TeeChart.Styles;


namespace MotorMonitorWpf
{
    public partial class MainWindow : Window
    {
        SerialPort _serial;
        Line _seriesPWM;
        Line _seriesRPM;
        Line _seriesVolt;

        // batas jumlah poin di chart agar tidak membludak
        const int MaxPoints = 500;

        public MainWindow()
        {
            InitializeComponent();

            // isi list COM port
            comboPorts.ItemsSource = SerialPort.GetPortNames().OrderBy(n => n).ToArray();
            if (comboPorts.Items.Count > 0) comboPorts.SelectedIndex = 0;
            comboBaud.SelectedIndex = 0;

            // Setup TeeChart series
            _seriesPWM = new Line(tChart.Chart);
            _seriesRPM = new Line(tChart.Chart);
            _seriesVolt = new Line(tChart.Chart);

            _seriesPWM.Title = "PWM";
            _seriesRPM.Title = "RPM";
            _seriesVolt.Title = "Volt";

            _seriesPWM.Color = System.Drawing.Color.Red;
            _seriesRPM.Color = System.Drawing.Color.Blue;
            _seriesVolt.Color = System.Drawing.Color.Green;

            // gunakan axis kiri untuk RPM, kanan untuk Volt (opsional)
            tChart.Series.Add(_seriesPWM);
            tChart.Series.Add(_seriesRPM);
            tChart.Series.Add(_seriesVolt);

            // format time axis (X) sebagai timestamp
            tChart.Axes.Bottom.Title.Text = "Time (ms)";
            tChart.Legend.Visible = true;
        }

        private void BtnConnect_Click(object sender, RoutedEventArgs e)
        {
            if (comboPorts.SelectedItem == null)
            {
                MessageBox.Show("Pilih COM port dulu.");
                return;
            }

            string portName = comboPorts.SelectedItem.ToString();
            int baud = int.Parse(((System.Windows.Controls.ComboBoxItem)comboBaud.SelectedItem).Content.ToString());

            _serial = new SerialPort(portName, baud);
            _serial.NewLine = "\n";
            _serial.DataReceived += Serial_DataReceived;

            try
            {
                _serial.Open();
                btnConnect.IsEnabled = false;
                btnDisconnect.IsEnabled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Gagal buka port: " + ex.Message);
            }
        }

        private void BtnDisconnect_Click(object sender, RoutedEventArgs e)
        {
            if (_serial != null && _serial.IsOpen)
            {
                _serial.DataReceived -= Serial_DataReceived;
                _serial.Close();
                _serial.Dispose();
                _serial = null;
            }
            btnConnect.IsEnabled = true;
            btnDisconnect.IsEnabled = false;
        }

        private void Serial_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                string line = _serial.ReadLine();
                // jalankan di UI thread
                Dispatcher.BeginInvoke(new Action(() => ProcessLine(line)));
            }
            catch { /* ignore parse errors */ }
        }

        private void ProcessLine(string line)
        {
            // bersihkan whitespace
            line = line.Trim();

            // kemungkinan header "PWM,RPM,Volt" di awal - abaikan
            if (string.IsNullOrWhiteSpace(line)) return;
            if (line.StartsWith("PWM", StringComparison.OrdinalIgnoreCase)) return;

            // coba parse CSV (format: PWM,RPM,Volt)
            string[] parts = line.Split(',');

            if (parts.Length < 3) return;

            if (!int.TryParse(parts[0], out int pwm)) return;
            if (!int.TryParse(parts[1], out int rpm)) return;
            if (!float.TryParse(parts[2], out float volt)) return;

            // update UI labels
            txtPWM.Text = pwm.ToString();
            txtRPM.Text = rpm.ToString();
            txtVolt.Text = volt.ToString("F2");

            // add point to chart, gunakan Environment.TickCount sebagai X
            double x = Environment.TickCount;

            _seriesPWM.Add(x, pwm);
            _seriesRPM.Add(x, rpm);
            _seriesVolt.Add(x, volt);

            // truncate if too many points
            TrimSeries(_seriesPWM);
            TrimSeries(_seriesRPM);
            TrimSeries(_seriesVolt);
        }

        private void TrimSeries(Series series)
        {
            while (series.Count > MaxPoints)
                series.Delete(0);
        }

        // pastikan port ditutup saat tutup aplikasi
        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            try
            {
                if (_serial != null)
                {
                    _serial.DataReceived -= Serial_DataReceived;
                    if (_serial.IsOpen) _serial.Close();
                    _serial.Dispose();
                }
            }
            catch { }
        }
    }
}
