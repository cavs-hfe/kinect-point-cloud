using System;
using System.Collections.Generic;
using System.IO;
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
using HelixToolkit.Wpf;
using System.Windows.Media.Media3D;
using Microsoft.WindowsAPICodePack.Dialogs;
using System.ComponentModel;
using System.Text.RegularExpressions;

namespace KinectDepthToPointCloud
{
    /// <summary>
    /// Interaction logic for PlaybackWindow.xaml
    /// </summary>
    public partial class PlaybackWindow : Window, INotifyPropertyChanged
    {
        private int currentFrame = 0;
        private int numberOfFrames;
        private string fileName = "";

        private string directory = @"C:\Users\CAVS\Desktop\KinectPointCloud\";

        private string[] pcdFiles;

        private bool accumulateFrames = false;

        public ICommand OpenDirectoryCommand { get; set; }

        //setup window
        public PlaybackWindow()
        {
            InitializeComponent();

            this.OpenDirectoryCommand = new DelegateCommand(this.OpenDirectory);

            prevButton.IsEnabled = false;
            nextButton.IsEnabled = false;

            this.DataContext = this;
        }

        #region Property Code

        public int NumberOfFrames
        {
            get { return numberOfFrames; }
            set
            {
                numberOfFrames = value;
                // notify any bound elements that the value has changed
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged(this, new PropertyChangedEventArgs("NumberOfFrames"));
                }
            }
        }

        public int CurrentFrame
        {
            get { return currentFrame; }
            set
            {
                currentFrame = value;
                // notify any bound elements that the value has changed
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged(this, new PropertyChangedEventArgs("CurrentFrame"));
                }
            }
        }

        public string FileName
        {
            get { return fileName; }
            set
            {
                fileName = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged(this, new PropertyChangedEventArgs("FileName"));
                }
            }
        }

        #endregion

        /// <summary>
        /// INotifyPropertyChangedPropertyChanged event to allow window controls to bind to changeable data
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        private void prevButton_Click(object sender, RoutedEventArgs e)
        {
            if (currentFrame > 0)
            {
                this.CurrentFrame = currentFrame - 1;
                LoadPointCloud();
            }
        }

        private void nextButton_Click(object sender, RoutedEventArgs e)
        {
            if (currentFrame < numberOfFrames - 1)
            {
                this.CurrentFrame = currentFrame + 1;
                LoadPointCloud();
            }
        }

        //show directory picker and load point clouds
        private void OpenDirectory()
        {
            CommonOpenFileDialog ofd = new CommonOpenFileDialog();

            ofd.IsFolderPicker = true;
            ofd.Multiselect = false;
            ofd.Title = "Select Data Directory...";

            if (ofd.ShowDialog() == CommonFileDialogResult.Ok)
            {
                directory = ofd.FileName;
                this.CurrentFrame = 0;
                pcdFiles = Directory.GetFiles(directory, "*.pcd");
                this.NumberOfFrames = pcdFiles.Length;
                LoadPointCloud();

                prevButton.IsEnabled = true;
                nextButton.IsEnabled = true;
            }
            else
            {
                prevButton.IsEnabled = false;
                nextButton.IsEnabled = false;
            }
        }

        //load the next point cloud in the pcd array
        private void LoadPointCloud()
        {
            if (!accumulateFrames)
            {
                view1.Children.Clear();
            }

            this.FileName = System.IO.Path.GetFileName(pcdFiles[currentFrame]);

            view1.Children.Add(GetPointCloudData(pcdFiles[currentFrame], Colors.White));
        }

        //create a data object that stores the points to be loaded into the viewer
        private PointsVisual3D GetPointCloudData(string file, Color c)
        {
            Point3DCollection dataList = new Point3DCollection();

            //read all lines in the file into an array
            string[] points = File.ReadAllLines(file);

            foreach (string point in points)
            {
                //if the line starts with a number or a negative sign (-)
                if (Regex.IsMatch(point, @"^\d+") || point.StartsWith("-"))
                {
                    //split the line based on white space, and if it has 3 or more parts add it to the list
                    string[] parts = point.Split(' ');
                    if (parts.Length >= 3)
                    {
                        dataList.Add(new Point3D(double.Parse(parts[0]), double.Parse(parts[1]), double.Parse(parts[2])));
                    }
                }
            }

            //create the PointsVisual3D to store the point data and set up parameters
            PointsVisual3D cloudPoints = new PointsVisual3D();
            cloudPoints.Color = c;
            cloudPoints.Size = 2;

            cloudPoints.Points = dataList;

            return cloudPoints;
        }
    }
}
