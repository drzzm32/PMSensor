using System;
using System.Windows;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using System.Collections.Generic; 

namespace PMSensor
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        SerialPort port;
        List<byte> buffer;
        Timer timer;
        Task task;

        public MainWindow()
        {
            InitializeComponent();
            port = new SerialPort();
            port.BaudRate = 9600;
            buffer = new List<byte>(10);
            timer = new Timer(timerWork);
            task = new Task(coreWork);
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            double scaleX, scaleY;
            scaleX = this.Width / this.MinWidth;
            scaleY = this.Height / this.MinHeight;
            Resources["ValueScaleX"] = scaleX;
            Resources["ValueScaleY"] = scaleY;
        }

        private void Window_StateChanged(object sender, EventArgs e)
        {
            double scaleX, scaleY;
            scaleX = this.Width / this.MinWidth;
            scaleY = this.Height / this.MinHeight;
            Resources["ValueScaleX"] = scaleX;
            Resources["ValueScaleY"] = scaleY;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            string[] pts = SerialPort.GetPortNames();
            portList.Items.Clear();
            foreach (var i in pts) portList.Items.Add(i);
        }

        private void btnRefresh_Click(object sender, RoutedEventArgs e)
        {
            if (port.IsOpen)
            {
                btnConnect.Content = "Connect";
                timer.Change(Timeout.Infinite, 1000);
                while (task.Status == TaskStatus.Running) DisapcherHelper.DoEvents();
                port.Close();
            }
            string[] pts = SerialPort.GetPortNames();
            portList.Items.Clear();
            foreach (var i in pts) portList.Items.Add(i);
        }

        private void btnConnect_Click(object sender, RoutedEventArgs e)
        {
            if (!port.IsOpen)
            {
                try
                {
                    port.PortName = (string)portList.SelectedItem;
                    port.Open();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK);
                    return;
                }
                btnConnect.Content = "Disconnect";
                state.IsChecked = true;
                timer.Change(0, 1000);
            }
            else
            {
                btnConnect.Content = "Connect";
                timer.Change(Timeout.Infinite, 1000);
                while (task.Status == TaskStatus.Running) DisapcherHelper.DoEvents();
                port.Close();
                state.IsChecked = false;
            }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            timer.Change(Timeout.Infinite, 1000);
            while (task.Status == TaskStatus.Running) DisapcherHelper.DoEvents();
            port.Close();
            timer.Dispose();
        }

        void timerWork(object obj)
        {
            if (task.Status == TaskStatus.RanToCompletion)
            {
                task = new Task(coreWork);
            }
            if (task.Status == TaskStatus.Created)
            {
                task.Start();
            }
        }

        void coreWork()
        {
            byte tmp;
            if (port.IsOpen)
            {
                while (true)
                {
                    tmp = (byte)port.ReadByte();
                    if (tmp == 0xAA)
                    {
                        buffer.Clear();
                        buffer.Add(tmp);
                        while (true)
                        {
                            tmp = (byte)port.ReadByte();
                            buffer.Add(tmp);
                            if (tmp == 0xAB || buffer.ToArray().Length == buffer.Capacity) break;
                        }
                        port.ReadExisting();

                        if (buffer.ToArray().Length != buffer.Capacity) break;
                        pm25Value.Dispatcher.Invoke(() =>
                        {
                            pm25Value.Content = (buffer[3] * 256 + buffer[2]) / 10;
                        });
                        pm10Value.Dispatcher.Invoke(() =>
                        {
                            pm10Value.Content = (buffer[5] * 256 + buffer[4]) / 10;
                        });
                    }
                    break;
                }
            }
        }

    }

    public static class DisapcherHelper
    {
        private static readonly DispatcherOperationCallback _exit_frame_callback = (state) => {
            var frame = state as DispatcherFrame;
            if (null == frame)
                return null;
            frame.Continue = false;
            return null;
        };

        public static void DoEvents()
        {
            var frame = new DispatcherFrame();
            var exOperation = Dispatcher.CurrentDispatcher.BeginInvoke(
                DispatcherPriority.Background, _exit_frame_callback,
                frame
                );

            Dispatcher.PushFrame(frame);
            if (exOperation.Status != DispatcherOperationStatus.Completed)
            {
                exOperation.Abort();

            }
        }
    }

}
