using System;
using System.Collections.Generic;
using System.Linq;
using System.Media;
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
using Microsoft.Kinect;
using System.ComponentModel;
using System.IO;

namespace KinectDepthToPointCloud
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        /// <summary>
        /// Map depth range to byte range
        /// </summary>
        private const int MapDepthToByte = 8000 / 256;

        /// <summary>
        /// Active Kinect sensor
        /// </summary>
        private KinectSensor kinectSensor = null;

        /// <summary>
        /// Reader for depth frames
        /// </summary>
        private DepthFrameReader depthFrameReader = null;

        /// <summary>
        /// Description of the data contained in the depth frame
        /// </summary>
        private FrameDescription depthFrameDescription = null;

        /// <summary>
        /// Bitmap to display
        /// </summary>
        private WriteableBitmap depthBitmap = null;

        /// <summary>
        /// Intermediate storage for frame data converted to color
        /// </summary>
        private byte[] depthPixels = null;

        private double minDepth = 0;

        private double maxDepth = 2.5;

        private string path = @"C:\Users\CAVS\Desktop\KinectPointCloud\";

        private string trialDataFile = "participant_list.csv";

        private int imageCount = 0;
        private int globalFrameNumber = 0;

        private bool recording = false;

        public ICommand ViewDataCommand { get; set; }
        public ICommand DataCleanupCommand { get; set; }
        public ICommand ParticipantNumberCommand { get; set; }
        public ICommand ParticipantSettingsCommad { get; set; }
        public ICommand ShowTutorialCommand { get; set; }

        private int framesPerSecond = 15;

        private long lastFrameRecorded = 0;

        private int participantNumber = 0;
        private string participantDirectory = "";
        private string fileName = "Pre-Start___";
        private Queue<MarkerCondition> trialConditions;

        private string leftHandText = "";
        private string rightHandText = "";
        private string currentMarkerLeft = "";
        private string currentMarkerRight = "";
        private string currentStatus = "";

        private string rhLabel = "Right Hand";
        private string lhLabel = "Left Hand";
        private string standUpLabel = "Please Stand Up";
        private string twoHandLabel = "Begin two-hand trials";
        private string experiementCompleteLabel = "Experiment Complete";

        private enum ConditionState { SeatedOneHanded, SeatedTwoHanded, StandingOneHanded, StandingTwoHanded };
        private ConditionState state = ConditionState.SeatedOneHanded;

        private double nextMarkerTimerSeconds = 5.0;

        private bool useTimer = true;
        private System.Windows.Forms.Timer timer;
        private bool timerRunning = false;

        private bool showFixation = true;

        public MainWindow()
        {
            // get the kinectSensor object
            this.kinectSensor = KinectSensor.GetDefault();

            // open the reader for the depth frames
            this.depthFrameReader = this.kinectSensor.DepthFrameSource.OpenReader();

            // wire handler for frame arrival
            this.depthFrameReader.FrameArrived += this.Reader_FrameArrived;

            // get FrameDescription from DepthFrameSource
            this.depthFrameDescription = this.kinectSensor.DepthFrameSource.FrameDescription;

            // allocate space to put the pixels being received and converted
            this.depthPixels = new byte[this.depthFrameDescription.Width * this.depthFrameDescription.Height];

            // create the bitmap to display
            this.depthBitmap = new WriteableBitmap(this.depthFrameDescription.Width, this.depthFrameDescription.Height, 96.0, 96.0, PixelFormats.Gray8, null);

            // open the sensor
            this.kinectSensor.Open();

            // use the window object as the view model in this simple example
            this.DataContext = this;

            this.ViewDataCommand = new DelegateCommand(this.ViewData);
            this.DataCleanupCommand = new DelegateCommand(this.LaunchDataCleanup);
            this.ParticipantNumberCommand = new DelegateCommand(this.PromptParticipantNumber);
            this.ParticipantSettingsCommad = new DelegateCommand(this.ParticipantSettings);
            this.ShowTutorialCommand = new DelegateCommand(this.ShowTutorial);

            InitializeComponent();
        }

        private void PromptParticipantNumber()
        {
            ParticipantNumberDialog pnd = new ParticipantNumberDialog();
            pnd.Owner = this;

            if (pnd.ShowDialog() == true)
            {
                participantNumber = int.Parse(pnd.ParticipantNumber);
                LoadTrialData();

                participantDirectory = "Participant" + participantNumber.ToString("00");
                Directory.CreateDirectory(System.IO.Path.Combine(path, participantDirectory));

                ParticipantMarkerDisplayWindow pmdw = new ParticipantMarkerDisplayWindow(this);
                pmdw.Owner = this;
                pmdw.Show();
            }
        }

        private void ParticipantSettings()
        {
            ParticipantSettingsWindow psw = new ParticipantSettingsWindow(useTimer, nextMarkerTimerSeconds);
            psw.Owner = this;

            if (psw.ShowDialog() == true)
            {
                useTimer = psw.UseTimer;
                nextMarkerTimerSeconds = psw.TimerSeconds;
            }
        }

        /// <summary>
        /// Setup queue of conditions. Adds two entries for each one handed target (left and right hand), and one entry for each two handed target. 
        /// Included fixation (or other image) conditon between targets. 
        /// </summary>
        private void LoadTrialData()
        {
            trialConditions = new Queue<MarkerCondition>();

            string[] lines = File.ReadAllLines(System.IO.Path.Combine(path, trialDataFile));

            ConditionState currentState = ConditionState.SeatedOneHanded;

            foreach (string cond in lines[participantNumber].Split(',').Skip(1))
            {
                if (cond.Equals(""))
                {
                    if (currentState == ConditionState.SeatedOneHanded)
                    {
                        currentState = ConditionState.SeatedTwoHanded;
                        trialConditions.Enqueue(new MarkerCondition(twoHandLabel));
                    }
                    else if (currentState == ConditionState.SeatedTwoHanded)
                    {
                        currentState = ConditionState.StandingOneHanded;
                        trialConditions.Enqueue(new MarkerCondition(standUpLabel));
                    }
                    else if (currentState == ConditionState.StandingOneHanded)
                    {
                        currentState = ConditionState.StandingTwoHanded;
                        trialConditions.Enqueue(new MarkerCondition(twoHandLabel));
                    }
                }
                else if (currentState == ConditionState.SeatedOneHanded || currentState == ConditionState.StandingOneHanded)
                {
                    trialConditions.Enqueue(new MarkerCondition(MarkerCondition.Hand.Left, cond));
                    trialConditions.Enqueue(new MarkerCondition(true));
                    trialConditions.Enqueue(new MarkerCondition(MarkerCondition.Hand.Right, cond));
                    trialConditions.Enqueue(new MarkerCondition(true));
                }
                else if (currentState == ConditionState.SeatedTwoHanded || currentState == ConditionState.StandingTwoHanded)
                {
                    trialConditions.Enqueue(new MarkerCondition(MarkerCondition.Hand.Both, cond.Replace('-', ' ')));
                    trialConditions.Enqueue(new MarkerCondition(true));
                }
            }

            trialConditions.Enqueue(new MarkerCondition(experiementCompleteLabel));

            CurrentStatus = "Ready to begin!";
        }

        private void StartTimer()
        {
            timer = new System.Windows.Forms.Timer();
            timer.Tick += timer_Tick;
            timer.Interval = (int)(nextMarkerTimerSeconds * 1000);
            timer.Start();
            timerRunning = true;
        }

        private void timer_Tick(object sender, EventArgs e)
        {
            NextMarker();
        }

        private void StopTimer()
        {
            timer.Stop();
            timer.Dispose();
            timerRunning = false;
        }

        private void NextMarker()
        {
            if (trialConditions.Count > 0)
            {
                MarkerCondition currentCondition = trialConditions.Dequeue();

                if (currentCondition.status.Equals(twoHandLabel) && state == ConditionState.SeatedOneHanded)
                {
                    state = ConditionState.SeatedTwoHanded;
                }
                else if (currentCondition.status.Equals(twoHandLabel) && state == ConditionState.StandingOneHanded)
                {
                    state = ConditionState.StandingTwoHanded;
                }
                else if (currentCondition.status.Equals(standUpLabel))
                {
                    state = ConditionState.StandingOneHanded;
                    StopRecording();
                }
                else if (currentCondition.status.Equals(experiementCompleteLabel))
                {
                    StopRecording();
                }

                string lastFileName = fileName;
                fileName = "";

                if (state == ConditionState.SeatedOneHanded || state == ConditionState.SeatedTwoHanded)
                {
                    fileName = fileName + "Seated_";
                }
                else
                {
                    fileName = fileName + "Standing_";
                }

                if (currentCondition.hand == MarkerCondition.Hand.Left)
                {
                    LeftHandText = lhLabel;
                    RightHandText = "";
                    CurrentMarkerLeft = currentCondition.marker;
                    CurrentMarkerRight = "";
                    fileName = fileName + currentCondition.marker + "_X_Reach_";
                }
                else if (currentCondition.hand == MarkerCondition.Hand.Right)
                {
                    LeftHandText = "";
                    RightHandText = rhLabel;
                    CurrentMarkerLeft = "";
                    CurrentMarkerRight = currentCondition.marker;
                    fileName = fileName + "X_" + currentCondition.marker + "_Reach_";
                }
                else if (currentCondition.hand == MarkerCondition.Hand.Both)
                {
                    LeftHandText = lhLabel;
                    RightHandText = rhLabel;
                    string[] markers = currentCondition.marker.Split(' ');
                    CurrentMarkerLeft = markers[0];
                    CurrentMarkerRight = markers[1];
                    fileName = fileName + markers[0] + "_" + markers[1] + "_Reach_";
                }
                else if (currentCondition.hand == MarkerCondition.Hand.None)
                {
                    LeftHandText = "";
                    RightHandText = "";
                    CurrentMarkerLeft = "";
                    CurrentMarkerRight = "";
                    if (currentCondition.status.Equals(""))
                    {
                        fileName = lastFileName.Substring(0, lastFileName.Length - 6) + "Return_";
                    }
                    else
                    {
                        fileName = "Status-" + currentCondition.status.Replace(' ', '-') + "____";
                    }
                }

                CurrentStatus = currentCondition.status;
                if (currentCondition.fixation)
                {
                    SystemSounds.Beep.Play();
                }
                ShowFixation = currentCondition.fixation;

                imageCount = 0;
            }
            else if (trialConditions.Count == 0 && timerRunning)
            {
                StopTimer();
            }
        }

        /// <summary>
        /// INotifyPropertyChangedPropertyChanged event to allow window controls to bind to changeable data
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        #region Data Handlers

        /// <summary>
        /// Gets the bitmap to display
        /// </summary>
        public ImageSource ImageSource
        {
            get
            {
                return this.depthBitmap;
            }
        }

        public double MinDepth
        {
            get { return this.minDepth; }
            set
            {
                if (this.minDepth != value && value > 0 && value < 8)
                {
                    this.minDepth = value;

                    // notify any bound elements that the value has changed
                    if (this.PropertyChanged != null)
                    {
                        this.PropertyChanged(this, new PropertyChangedEventArgs("MinDepth"));
                    }
                }
            }
        }

        public double MaxDepth
        {
            get { return this.maxDepth; }
            set
            {
                if (this.maxDepth != value && value > 0 && value < 8)
                {
                    this.maxDepth = value;

                    // notify any bound elements that the value has changed
                    if (this.PropertyChanged != null)
                    {
                        this.PropertyChanged(this, new PropertyChangedEventArgs("MaxDepth"));
                    }
                }
            }
        }

        public string LeftHandText
        {
            get { return this.leftHandText; }
            set
            {
                if (this.leftHandText != value)
                {
                    this.leftHandText = value;

                    if (this.PropertyChanged != null)
                    {
                        this.PropertyChanged(this, new PropertyChangedEventArgs("LeftHandText"));
                    }
                }
            }
        }

        public string RightHandText
        {
            get { return this.rightHandText; }
            set
            {
                if (this.rightHandText != value)
                {
                    this.rightHandText = value;

                    if (this.PropertyChanged != null)
                    {
                        this.PropertyChanged(this, new PropertyChangedEventArgs("RightHandText"));
                    }
                }
            }
        }

        public string CurrentMarkerLeft
        {
            get { return this.currentMarkerLeft; }
            set
            {
                if (this.currentMarkerLeft != value)
                {
                    this.currentMarkerLeft = value;

                    if (this.PropertyChanged != null)
                    {
                        this.PropertyChanged(this, new PropertyChangedEventArgs("CurrentMarkerLeft"));
                    }
                }
            }
        }

        public string CurrentMarkerRight
        {
            get { return this.currentMarkerRight; }
            set
            {
                if (this.currentMarkerRight != value)
                {
                    this.currentMarkerRight = value;

                    if (this.PropertyChanged != null)
                    {
                        this.PropertyChanged(this, new PropertyChangedEventArgs("CurrentMarkerRight"));
                    }
                }
            }
        }

        public string CurrentStatus
        {
            get { return this.currentStatus; }
            set
            {
                if (this.currentStatus != value)
                {
                    this.currentStatus = value;

                    if (this.PropertyChanged != null)
                    {
                        this.PropertyChanged(this, new PropertyChangedEventArgs("CurrentStatus"));
                    }
                }
            }
        }

        public bool ShowFixation
        {
            get { return this.showFixation; }
            set
            {
                if (this.showFixation != value)
                {
                    this.showFixation = value;

                    if (this.PropertyChanged != null)
                    {
                        this.PropertyChanged(this, new PropertyChangedEventArgs("ShowFixation"));
                    }
                }
            }
        }

        #endregion

        /// <summary>
        /// Execute shutdown tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            if (this.depthFrameReader != null)
            {
                // DepthFrameReader is IDisposable
                this.depthFrameReader.Dispose();
                this.depthFrameReader = null;
            }

            if (this.kinectSensor != null)
            {
                this.kinectSensor.Close();
                this.kinectSensor = null;
            }
        }

        private void Reader_FrameArrived(object sender, DepthFrameArrivedEventArgs e)
        {
            bool depthFrameProcessed = false;

            using (DepthFrame depthFrame = e.FrameReference.AcquireFrame())
            {
                if (depthFrame != null)
                {
                    if (recording && DateTime.Now.Ticks > (lastFrameRecorded + (1e7 / framesPerSecond)))
                    {
                        ushort[] depthPoints = new ushort[this.depthFrameDescription.Width * this.depthFrameDescription.Height];
                        depthFrame.CopyFrameDataToArray(depthPoints);

                        CameraSpacePoint[] cameraPtsArray = new CameraSpacePoint[this.depthFrameDescription.Width * this.depthFrameDescription.Height];
                        kinectSensor.CoordinateMapper.MapDepthFrameToCameraSpace(depthPoints, cameraPtsArray);

                        this.ProcessDepthFrameDataToFile(cameraPtsArray, (ushort)(minDepth), (ushort)(maxDepth), "S" + participantNumber.ToString("00") + "_" + globalFrameNumber.ToString("00000") + "_" + fileName + "Frame" + imageCount.ToString("0000") + ".pcd");

                        lastFrameRecorded = DateTime.Now.Ticks;
                        imageCount++;
                        globalFrameNumber++;
                    }

                    // the fastest way to process the body index data is to directly access 
                    // the underlying buffer
                    using (Microsoft.Kinect.KinectBuffer depthBuffer = depthFrame.LockImageBuffer())
                    {
                        // verify data and write the color data to the display bitmap
                        if (((this.depthFrameDescription.Width * this.depthFrameDescription.Height) == (depthBuffer.Size / this.depthFrameDescription.BytesPerPixel)) &&
                            (this.depthFrameDescription.Width == this.depthBitmap.PixelWidth) && (this.depthFrameDescription.Height == this.depthBitmap.PixelHeight))
                        {
                            // Note: In order to see the full range of depth (including the less reliable far field depth)
                            // we are setting maxDepth to the extreme potential depth threshold
                            //ushort maxDepth = ushort.MaxValue;

                            // If you wish to filter by reliable depth distance, uncomment the following line:
                            //maxDepth = depthFrame.DepthMaxReliableDistance;

                            this.ProcessDepthFrameData(depthBuffer.UnderlyingBuffer, depthBuffer.Size, (ushort)(minDepth * 1000), (ushort)(maxDepth * 1000));
                            depthFrameProcessed = true;
                            //this.ProcessDepthFrameDataToFile(depthBuffer.UnderlyingBuffer, depthBuffer.Size, depthFrame.DepthMinReliableDistance, (ushort)(maxDepth * 1000));
                        }
                    }
                }
            }

            if (depthFrameProcessed)
            {
                this.RenderDepthPixels();
            }
        }

        /// <summary>
        /// Directly accesses the underlying image buffer of the DepthFrame to 
        /// create a displayable bitmap.
        /// This function requires the /unsafe compiler option as we make use of direct
        /// access to the native memory pointed to by the depthFrameData pointer.
        /// </summary>
        /// <param name="depthFrameData">Pointer to the DepthFrame image data</param>
        /// <param name="depthFrameDataSize">Size of the DepthFrame image data</param>
        /// <param name="minDepth">The minimum reliable depth value for the frame</param>
        /// <param name="maxDepth">The maximum reliable depth value for the frame</param>
        private unsafe void ProcessDepthFrameData(IntPtr depthFrameData, uint depthFrameDataSize, ushort minDepth, ushort maxDepth)
        {
            // depth frame data is a 16 bit value
            ushort* frameData = (ushort*)depthFrameData;

            // convert depth to a visual representation
            for (int i = 0; i < (int)(depthFrameDataSize / this.depthFrameDescription.BytesPerPixel); ++i)
            {
                // Get the depth for this pixel
                ushort depth = frameData[i];

                // To convert to a byte, we're mapping the depth value to the byte range.
                // Values outside the reliable depth range are mapped to 0 (black).
                this.depthPixels[i] = (byte)(depth >= minDepth && depth <= maxDepth ? (depth / MapDepthToByte) : 0);
            }
        }

        private void ProcessDepthFrameDataToFile(CameraSpacePoint[] cameraSpacePoints, ushort minDepth, ushort maxDepth, string filename)
        {
            Task t = Task.Run(() =>
            {
                List<string> lines = new List<string>();

                foreach (CameraSpacePoint csp in cameraSpacePoints)
                {
                    if (csp.Z >= minDepth && csp.Z <= maxDepth)
                    {
                        lines.Add(csp.X + " " + csp.Y + " " + csp.Z);
                    }
                }

                using (StreamWriter streamWriter = new StreamWriter(System.IO.Path.Combine(path, participantDirectory, filename)))
                {

                    //write file header
                    streamWriter.WriteLine("VERSION .7");
                    streamWriter.WriteLine("FIELDS x y z");
                    streamWriter.WriteLine("SIZE 4 4 4");
                    streamWriter.WriteLine("TYPE F F F");
                    streamWriter.WriteLine("COUNT 1 1 1");
                    streamWriter.WriteLine("WIDTH " + lines.Count);
                    streamWriter.WriteLine("HEIGHT 1");
                    streamWriter.WriteLine("VIEWPOINT 0 0 0 1 0 0 0");
                    streamWriter.WriteLine("POINTS " + lines.Count);
                    streamWriter.WriteLine("DATA ascii");

                    //write points
                    foreach (string s in lines)
                    {
                        streamWriter.WriteLine(s);
                    }
                }
            });
        }

        /// <summary>
        /// Renders color pixels into the writeableBitmap.
        /// </summary>
        private void RenderDepthPixels()
        {
            this.depthBitmap.WritePixels(
                new Int32Rect(0, 0, this.depthBitmap.PixelWidth, this.depthBitmap.PixelHeight),
                this.depthPixels,
                this.depthBitmap.PixelWidth,
                0);
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (recording)
            {
                StopRecording();
            }
            else if(participantNumber != 0)
            {
                if (state == ConditionState.StandingOneHanded)
                {
                    StartRecording(false);
                }
                else
                {
                    StartRecording();
                }

            }
            else if(useTimer && !timerRunning)
            {
                StartTimer();
            }
        }

        private void StartRecording()
        {
            StartRecording(true);
        }

        private void StartRecording(bool resetFrameCounters)
        {
            RecordButton.Content = "Stop Recording";
            if (resetFrameCounters)
            {
                imageCount = 0;
                globalFrameNumber = 0;
            }

            recording = true;
            if (useTimer && !timerRunning)
            {
                StartTimer();
            }
        }

        private void StopRecording()
        {
            RecordButton.Content = "Start Recording";
            recording = false;
            if (timerRunning)
            {
                StopTimer();
            }
        }

        private void ViewData()
        {
            PlaybackWindow pw = new PlaybackWindow();
            pw.Owner = this;
            pw.Show();
        }

        private void LaunchDataCleanup()
        {
            DataCleanupWindow dcw = new DataCleanupWindow();
            dcw.Owner = this;
            dcw.Show();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Right && !useTimer)
            {
                NextMarker();
            }
        }

        private void ShowTutorial()
        {
            trialConditions = new Queue<MarkerCondition>();

            trialConditions.Enqueue(new MarkerCondition(MarkerCondition.Hand.Left, "B2"));
            trialConditions.Enqueue(new MarkerCondition(true));
            trialConditions.Enqueue(new MarkerCondition(MarkerCondition.Hand.Right, "B2"));
            trialConditions.Enqueue(new MarkerCondition(true));

            trialConditions.Enqueue(new MarkerCondition(MarkerCondition.Hand.Left, "C5"));
            trialConditions.Enqueue(new MarkerCondition(true));
            trialConditions.Enqueue(new MarkerCondition(MarkerCondition.Hand.Right, "C5"));
            trialConditions.Enqueue(new MarkerCondition(true));

            trialConditions.Enqueue(new MarkerCondition(twoHandLabel));

            trialConditions.Enqueue(new MarkerCondition(MarkerCondition.Hand.Both, "D1 B5"));
            trialConditions.Enqueue(new MarkerCondition(true));

            trialConditions.Enqueue(new MarkerCondition(MarkerCondition.Hand.Both, "B3 B7"));
            trialConditions.Enqueue(new MarkerCondition(true));

            trialConditions.Enqueue(new MarkerCondition(experiementCompleteLabel));

            CurrentStatus = "Ready to begin!";

            ParticipantMarkerDisplayWindow pmdw = new ParticipantMarkerDisplayWindow(this);
            pmdw.Owner = this;
            pmdw.Show();
        }
    }
}
