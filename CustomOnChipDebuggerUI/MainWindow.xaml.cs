using CustomOnChipDebuggerConsoleApp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace CustomOnChipDebuggerUI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private FTDI ftdi;
        private DispatcherTimer timer;

        public MainWindow()
        {
            InitializeComponent();

            // Initialize FTDI
            ftdi = new FTDI();
            FTDI.FT_STATUS status = ftdi.OpenByIndex(0);
            if (status != FTDI.FT_STATUS.FT_OK)
            {
                MessageBox.Show("Failed to open FTDI device.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
            }

            // Initialize timer to update registers periodically
            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(500);
            timer.Tick += Timer_Tick;
            timer.Start();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            // Read and update register values
            uint[] registers = new uint[32];
            for (int i = 0; i < registers.Length; i++)
            {
                uint value = 0;
                int result = register_read(i, 4, out value);
                if (result != 0)
                {
                    // Failed to read register
                    registers[i] = 0;
                    continue;
                }
                registers[i] = value;
            }

            // Update UI elements
            for (int i = 0; i < registerLabels.Length; i++)
            {
                registerLabels[i].Content = $"x{i}:";
                registerValues[i].Content = $"0x{registers[i]:X8}";
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Cleanup
            timer.Stop();
            ftdi.Close();
        }

        private int register_read(uint number, int size, out uint value)
        {
            // TODO: Implement register read using JTAG
            // For now, just return a random value
            value = (uint)new Random().Next();
            return 0;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Implement button click action
        }
    }
}
