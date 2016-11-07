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
using System.Windows.Shapes;

namespace KinectDepthToPointCloud
{
    /// <summary>
    /// Interaction logic for ParticipantSettingsWindow.xaml
    /// </summary>
    public partial class ParticipantSettingsWindow : Window
    {
        public ParticipantSettingsWindow(bool useTimer, double timerSeconds)
        {
            InitializeComponent();

            TimerCheckbox.IsChecked = useTimer;
            TimerSecondsBox.Text = timerSeconds.ToString();
        }

        public bool UseTimer { get { return (bool)TimerCheckbox.IsChecked; } }
        public double TimerSeconds { get { return double.Parse(TimerSecondsBox.Text); } }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}
