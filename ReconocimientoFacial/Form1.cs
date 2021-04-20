using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Emgu.CV;
using Emgu.CV.Structure;
using Emgu.CV.Face;
using Emgu.CV.CvEnum;
using System.IO;
using System.Diagnostics;
using System.Threading;
using System.Runtime.InteropServices;

namespace ReconocimientoFacial
{
    public partial class Form1 : Form
    {
        #region variables 
        private Capture video = null;
        private Image<Bgr, Byte> CurrentFrame = null;
        Mat frame = new Mat();
        private bool facesDetectionEnable = false;
        CascadeClassifier faceCascadeClassifier = new CascadeClassifier("haarcascade_frontalface_alt.xml");
        List<Image<Gray, Byte>> TrainedFaces = new List<Image<Gray, byte>>();
        List<int> PersonsLabels = new List<int>();
        bool EnableSaveImage = false;
        private static bool isTrained = false;
        EigenFaceRecognizer recognizer;
        List<string> PersonsNames = new List<string>();
        #endregion
        [DllImport("Gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
        private static extern IntPtr CreateRoundRectRgn
        (
            int nLeftRect,     // x-coordinate of upper-left corner
            int nTopRect,      // y-coordinate of upper-left corner
            int nRightRect,    // x-coordinate of lower-right corner
            int nBottomRect,   // y-coordinate of lower-right corner
            int nWidthEllipse, // height of ellipse
            int nHeightEllipse // width of ellipse
        );
        public Form1()
        {
            InitializeComponent();
            this.FormBorderStyle = FormBorderStyle.None;
            Region = System.Drawing.Region.FromHrgn(CreateRoundRectRgn(0, 0, Width, Height, 20, 20));
        }

        private void btnCapture_Click(object sender, EventArgs e)
        {
            video = new Capture();
            video.ImageGrabbed += ProcessFrame;
            video.Start();
        }

        private void ProcessFrame(object sender, EventArgs e)
        {
            //capturar video 
            video.Retrieve(frame, 0);
            CurrentFrame = frame.ToImage<Bgr, Byte>().Resize(picCapture.Width, picCapture.Height, Inter.Cubic);
            //Detectar 
            if (facesDetectionEnable)
            {
                Mat grayImage = new Mat();
                CvInvoke.CvtColor(CurrentFrame, grayImage, ColorConversion.Bgr2Gray);
                CvInvoke.EqualizeHist(grayImage, grayImage);
                Rectangle[] faces = faceCascadeClassifier.DetectMultiScale(grayImage, 1.1, 3, Size.Empty, Size.Empty);
                if (faces.Length > 0)
                {
                    foreach (var face in faces)
                    {
                        CvInvoke.Rectangle(CurrentFrame, face, new Bgr(Color.Green).MCvScalar, 2);

                        Image<Bgr, Byte> resultImage = CurrentFrame.Convert<Bgr, Byte>();
                        resultImage.ROI = face;
                        picDetected.SizeMode = PictureBoxSizeMode.StretchImage;
                        picDetected.Image = resultImage.Bitmap;

                        if (EnableSaveImage)
                        {
                            string path = Directory.GetCurrentDirectory() + @"\TrainedImages";
                            if (!Directory.Exists(path))
                            {
                                Directory.CreateDirectory(path);
                            }
                            for (int i = 0; i < 10; i++)
                            {

                                resultImage.Resize(200, 200, Inter.Cubic).Save(path + @"\" + txtPersonName.Text + "_" + DateTime.Now.ToString("dd-mm-yyyy-hh-mm-ss") + ".jpg");
                                Thread.Sleep(1000);
                            }
                        }
                        EnableSaveImage = false;

                        if (btnAdd.InvokeRequired)
                        {
                            btnAdd.Invoke(new ThreadStart(delegate {
                                btnAdd.Enabled = true; 
                            }));
                        }

                        //reconocer 
                        if (isTrained)
                        {
                            Image<Gray, Byte> grayFaceResult = resultImage.Convert<Gray, Byte>().Resize(200,200, Inter.Cubic);
                            CvInvoke.EqualizeHist(grayFaceResult, grayFaceResult);
                            var result = recognizer.Predict(grayFaceResult);
                            pictureBox1.Image = grayFaceResult.Bitmap;
                            pictureBox2.Image = TrainedFaces[result.Label].Bitmap;
                            Debug.WriteLine(result.Label + ". " + result.Distance);
                            if (result.Label > 0)
                            {
                                CvInvoke.PutText(CurrentFrame, PersonsNames[result.Label], new Point(face.X - 2, face.Y - 2), FontFace.HersheyComplex, 1.0, new Bgr(Color.Green).MCvScalar);
                            }
                            else
                            {
                                CvInvoke.PutText(CurrentFrame, "Kevin", new Point(face.X - 2, face.Y - 2), FontFace.HersheyComplex, 1.0, new Bgr(Color.Green).MCvScalar);
                            }

                        }
                    }
                }
            }
            //renderizar
            picCapture.Image = CurrentFrame.Bitmap;
        }

        private void btnDetect_Click(object sender, EventArgs e)
        {
            facesDetectionEnable = true; 

        }

        private void btnAdd_Click(object sender, EventArgs e)
        {
            btnSave.Enabled = true;
            btnAdd.Enabled = false;
            EnableSaveImage = true; 
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            btnSave.Enabled = false;
            btnAdd.Enabled = true;
            EnableSaveImage = false;
            txtPersonName.Text = ""; 
        }

        private void btnTrainImages_Click(object sender, EventArgs e)
        {
            TrainImagesFromDir();
        }
        //imagenes 
        private bool TrainImagesFromDir()
        {
            int ImagesCount = 0;
            double Threshold = 2000;
            TrainedFaces.Clear();
            PersonsLabels.Clear();
            PersonsNames.Clear();
            try
            {
                string path = Directory.GetCurrentDirectory() + @"\TrainedImages";
                string[] files = Directory.GetFiles(path, "*.jpg", SearchOption.AllDirectories);

                foreach (var file in files)
                {
                    Image<Gray, byte> trainedImage = new Image<Gray, byte>(file).Resize(200, 200, Inter.Cubic);
                    CvInvoke.EqualizeHist(trainedImage, trainedImage);
                    TrainedFaces.Add(trainedImage);
                    PersonsLabels.Add(ImagesCount);
                    string name = file.Split('\\').Last().Split('_')[0];
                    PersonsNames.Add(name);
                    ImagesCount++;
                    Debug.WriteLine(ImagesCount + ". " + name);

                }

                if (TrainedFaces.Count() > 0)
                {
                    // recognizer = new EigenFaceRecognizer(ImagesCount,Threshold);
                    recognizer = new EigenFaceRecognizer(ImagesCount, Threshold);
                    recognizer.Train(TrainedFaces.ToArray(), PersonsLabels.ToArray());

                    isTrained = true;
                    //Debug.WriteLine(ImagesCount);
                    //Debug.WriteLine(isTrained);
                    return true;
                }
                else
                {
                    isTrained = false;
                    return false;
                }
            }
            catch (Exception ex)
            {
                isTrained = false;
                MessageBox.Show("Error in Train Images: " + ex.Message);
                return false;
            }

        }

        private void pictureBox1_Click(object sender, EventArgs e)
        {

        }

        private void btnMinimize_Click(object sender, EventArgs e)
        {
            this.WindowState = FormWindowState.Minimized;
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }

        private void btnHelp_Click(object sender, EventArgs e)
        {
            ayuda f = new ayuda();
            f.ShowDialog();
        }
    }
}
