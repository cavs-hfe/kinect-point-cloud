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
            string[] files = Directory.GetFiles(path);

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
            }

        }

        private void CleanDataStatistical(string path, int meanK, double stdDevMultiplier)
        {
            string[] files = Directory.GetFiles(path);

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
            }
        }

        private void MergeButton_Click(object sender, RoutedEventArgs e)
        {
            MergeData(DirectoryBox.Text);
        }

        private void MergeData(string path)
        {
            //combine all of the point cloud files into one giant point cloud file that contains every point in the other point cloud files
            string[] files = Directory.GetFiles(path);

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

            //reduce the data using the voxel grid reduction
            Process p = new Process();
            p.StartInfo.FileName = "pcl_voxel_grid_release.exe";
            p.StartInfo.Arguments = System.IO.Path.Combine(path, "merge", "merged.pcd") + " " + System.IO.Path.Combine(path, "merge", "merged-reduced.pcd");
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.CreateNoWindow = true;

            p.Start();

            p.WaitForExit();
        }
    }
}
