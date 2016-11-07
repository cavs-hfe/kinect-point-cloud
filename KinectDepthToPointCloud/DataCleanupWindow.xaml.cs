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
using Microsoft.WindowsAPICodePack.Dialogs;
using System.IO;
using System.Windows.Media.Media3D;
using System.Threading;
using System.ComponentModel;
using System.Diagnostics;

namespace KinectDepthToPointCloud
{
    /// <summary>
    /// Interaction logic for DataCleanupWindow.xaml
    /// </summary>
    public partial class DataCleanupWindow : Window, INotifyPropertyChanged
    {
        private int currentPoint;
        private int totalPoints;
        private int currentFile;
        private int totalFiles;

        public DataCleanupWindow()
        {
            InitializeComponent();

            this.DataContext = this;
        }

        /// <summary>
        /// INotifyPropertyChangedPropertyChanged event to allow window controls to bind to changeable data
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        private void CleanButton_Click(object sender, RoutedEventArgs e)
        {
            if (Directory.Exists(DirectoryBox.Text))
            {
                if ((bool)RadiusRadio.IsChecked)
                {
                    CleanDataRadius(DirectoryBox.Text, double.Parse(FilterDistanceBox.Text), int.Parse(MinPointsBox.Text));
                }
                else if ((bool)StatRadio.IsChecked)
                {
                    CleanDataStatistical(DirectoryBox.Text, int.Parse(MeanKBox.Text), double.Parse(StdDevBox.Text));
                }
                else
                {
                    MessageBox.Show("Please select a cleanup method (radius or statistical) using the radio buttons above.", "Choose Method", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                }
            }
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            CommonOpenFileDialog ofd = new CommonOpenFileDialog();

            ofd.IsFolderPicker = true;
            ofd.Multiselect = false;
            ofd.Title = "Select Data Directory...";

            if (ofd.ShowDialog() == CommonFileDialogResult.Ok)
            {
                DirectoryBox.Text = ofd.FileName;
            }
        }

        private void CleanDataRadius(string path, double radius, int minPoints)
        {
            //TODO: use these instead, way faster than my method
            //foreach file 
            //pcl_outlier_removal_release.exe imageX.pcd clean/imageX-temp.pcd -method statistical -mean_k 50 -std_dev_mul 1 -inliers 0
            //pcl_convert_pcd_ascii_binary_release.exe clean/imageX-temp.pcd clean/imageX.pcd 0
            //File.Delete(clean/imageX-temp.pcd);

            string[] files = Directory.GetFiles(path);

            this.CurrentFile = 1;
            this.TotalFiles = files.Length;

            Directory.CreateDirectory(System.IO.Path.Combine(path, "clean"));

            foreach (string s in files)
            {
                Process p = new Process();
                p.StartInfo.FileName = "pcl_outlier_removal_release.exe";
                p.StartInfo.Arguments = s + " " + System.IO.Path.Combine(path, "clean", System.IO.Path.GetFileNameWithoutExtension(s) + "-temp.pcd") + " -method radius -radius " + radius + " -min_pts " + minPoints;
                p.StartInfo.UseShellExecute = false;

                p.Start();

                p.WaitForExit();

                p = new Process();
                p.StartInfo.FileName = "pcl_convert_pcd_ascii_binary_release.exe";
                p.StartInfo.Arguments = System.IO.Path.Combine(path, "clean", System.IO.Path.GetFileNameWithoutExtension(s) + "-temp.pcd") + " " + System.IO.Path.Combine(path, "clean", System.IO.Path.GetFileNameWithoutExtension(s) + ".pcd") + " 0";

                p.Start();

                p.WaitForExit();

                File.Delete(System.IO.Path.Combine(path, "clean", System.IO.Path.GetFileNameWithoutExtension(s) + "-temp.pcd"));

                this.CurrentFile = currentFile + 1;
            }

        }

        private void CleanDataStatistical(string path, int meanK, double stdDevMultiplier)
        {
            string[] files = Directory.GetFiles(path);

            this.CurrentFile = 1;
            this.TotalFiles = files.Length;

            Directory.CreateDirectory(System.IO.Path.Combine(path, "clean"));

            foreach (string s in files)
            {
                Process p = new Process();
                p.StartInfo.FileName = "pcl_outlier_removal_release.exe";
                p.StartInfo.Arguments = s + " " + System.IO.Path.Combine(path, "clean", System.IO.Path.GetFileNameWithoutExtension(s) + "-temp.pcd") + " -method statistical -mean_k " + meanK + " -std_dev_mul " + stdDevMultiplier + " -inliers 0";
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.CreateNoWindow = true;

                p.Start();

                p.WaitForExit();

                p = new Process();
                p.StartInfo.FileName = "pcl_convert_pcd_ascii_binary_release.exe";
                p.StartInfo.Arguments = System.IO.Path.Combine(path, "clean", System.IO.Path.GetFileNameWithoutExtension(s) + "-temp.pcd") + " " + System.IO.Path.Combine(path, "clean", System.IO.Path.GetFileNameWithoutExtension(s) + ".pcd") + " 0";
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.CreateNoWindow = true;

                p.Start();

                p.WaitForExit();

                File.Delete(System.IO.Path.Combine(path, "clean", System.IO.Path.GetFileNameWithoutExtension(s) + "-temp.pcd"));

                this.CurrentFile = currentFile + 1;
            }
        }

        private void MergeButton_Click(object sender, RoutedEventArgs e)
        {
            MergeData(DirectoryBox.Text);
        }

        private void MergeData(string path)
        {
            string[] files = Directory.GetFiles(path);

            this.CurrentFile = 1;
            this.TotalFiles = files.Length;

            Directory.CreateDirectory(System.IO.Path.Combine(path, "merge"));

            List<string> data = new List<string>();

            foreach (string s in files)
            {
                string[] points = File.ReadAllLines(s);

                foreach (string point in points)
                {
                    string[] parts = point.Split(' ');
                    if (parts.Length == 3)
                    {
                        data.Add(point);
                    }
                }

                this.CurrentFile = currentFile + 1;
            }

            using (StreamWriter streamWriter = new StreamWriter(System.IO.Path.Combine(path, "merge", "merged.pcd")))
            {
                streamWriter.WriteLine("VERSION .7");
                streamWriter.WriteLine("FIELDS x y z");
                streamWriter.WriteLine("SIZE 4 4 4");
                streamWriter.WriteLine("TYPE F F F");
                streamWriter.WriteLine("COUNT 1 1 1");
                streamWriter.WriteLine("WIDTH " + data.Count);
                streamWriter.WriteLine("HEIGHT 1");
                streamWriter.WriteLine("VIEWPOINT 0 0 0 1 0 0 0");
                streamWriter.WriteLine("POINTS " + data.Count);
                streamWriter.WriteLine("DATA ascii");

                foreach (string s in data)
                {
                    streamWriter.WriteLine(s);
                }
            }

            /*Process p = new Process();
            p.StartInfo.FileName = "pcl_outlier_removal_release.exe";
            p.StartInfo.Arguments = s + " " + System.IO.Path.Combine(path, "clean", System.IO.Path.GetFileNameWithoutExtension(s) + "-temp.pcd") + " -method statistical -mean_k " + meanK + " -std_dev_mul " + stdDevMultiplier + " -inliers 0";
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.CreateNoWindow = true;

            p.Start();

            p.WaitForExit();*/
        }

        public int CurrentPoint
        {
            get { return currentPoint; }
            set
            {
                currentPoint = value;
                // notify any bound elements that the value has changed
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged(this, new PropertyChangedEventArgs("CurrentPoint"));
                }
            }
        }

        public int TotalPoints
        {
            get { return totalPoints; }
            set
            {
                totalPoints = value;
                // notify any bound elements that the value has changed
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged(this, new PropertyChangedEventArgs("TotalPoints"));
                }
            }
        }

        public int CurrentFile
        {
            get { return currentFile; }
            set
            {
                currentFile = value;
                // notify any bound elements that the value has changed
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged(this, new PropertyChangedEventArgs("CurrentPoint"));
                }
            }
        }

        public int TotalFiles
        {
            get { return totalFiles; }
            set
            {
                totalFiles = value;
                // notify any bound elements that the value has changed
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged(this, new PropertyChangedEventArgs("TotalFiles"));
                }
            }
        }



    }
}
