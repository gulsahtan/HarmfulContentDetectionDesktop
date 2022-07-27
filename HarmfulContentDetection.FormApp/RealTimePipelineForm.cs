﻿using HarmfulContentDetection.FormApp.DataStructures;
using HarmfulContentDetection.Scorer;
using HarmfulContentDetection.Scorer.Models;
using Microsoft.ML;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace HarmfulContentDetection.FormApp
{
    public partial class RealTimePipelineForm : Form
    {
        private VideoCapture _capture;
        private Image _mySharpImage;
        double totalFrame;
        int frameNo;
        bool isReadingFrame;
        private string assetsRelativePath = @"Assets";
        private string assetsPath;
        private string modelFilePath;
        public RealTimePipelineForm()
        {
            InitializeComponent();
            assetsPath = GetAbsolutePath(assetsRelativePath);
        }
        public static string GetAbsolutePath(string relativePath)
        {
            FileInfo _dataRoot = new FileInfo(typeof(Program).Assembly.Location);
            string assemblyFolderPath = _dataRoot.Directory.FullName;

            string fullPath = Path.Combine(assemblyFolderPath, relativePath);

            return fullPath;
        }

        private static void DrawBoundingBox(ref Image image, IList<Prediction> filteredBoundingBoxes)
        {
            var originalImageHeight = image.Height;
            var originalImageWidth = image.Width;
            using var graphics = Graphics.FromImage(image);
            Bitmap img1 = new Bitmap(image);
            foreach (var box in filteredBoundingBoxes)
            {
                double score = Math.Round(box.Score, 2);


                for (Int32 xx = (int)box.Rectangle.Left; xx < box.Rectangle.Right; xx += 25)
                {
                    for (Int32 yy = (int)box.Rectangle.Top; yy < box.Rectangle.Bottom; yy += 25)
                    {
                        Int32 avgR = 0, avgG = 0, avgB = 0;
                        Int32 blurPixelCount = 0;
                        Rectangle currentRect = new Rectangle(xx, yy, 25, 25);

                        for (Int32 a = currentRect.Left; (a < currentRect.Right && a < image.Width); a++)
                        {
                            for (Int32 b = currentRect.Top; (b < currentRect.Bottom && b < image.Height); b++)
                            {
                                Color pixel = img1.GetPixel(a, b);

                                avgR += pixel.R;
                                avgG += pixel.G;
                                avgB += pixel.B;

                                blurPixelCount++;
                            }
                        }

                        avgR = avgR / blurPixelCount;
                        avgG = avgG / blurPixelCount;
                        avgB = avgB / blurPixelCount;

                        graphics.FillRectangle(new SolidBrush(Color.FromArgb(avgR, avgG, avgB)), currentRect);
                    }
                }
            }
        }


        private void ObjectDetection(bool alcohol, bool violence, bool cigarette)
        {
            Stopwatch stopwatch = new Stopwatch();
            DateTime startTime = DateTime.Now;
            TimeSpan timeElapsed = DateTime.Now - startTime;
            var milisec = timeElapsed.TotalMilliseconds;

            Mat image = new Mat();


            while (isReadingFrame == true && (frameNo + 1 < totalFrame))
            {
                stopwatch.Start();
                frameNo += Convert.ToInt16(1);
                _capture.Set(CaptureProperty.PosFrames, frameNo);
                _capture.Read(image);

                if (image.Empty())
                    break;
                var mlContext = new MLContext();

                if (Alcohol.Checked == true || Cigarette.Checked == true || Violence.Checked == true)
                {
                    if (Cigarette.Checked == true && Alcohol.Checked == false && Violence.Checked == false)
                    {
                        modelFilePath = Path.Combine(assetsPath, "cigaretteweight", "best.onnx");

                    }
                    if (Cigarette.Checked == false && Alcohol.Checked == true && Violence.Checked == false)
                    {
                        modelFilePath = Path.Combine(assetsPath, "alcoholweight", "best.onnx");

                    }

                    if (Cigarette.Checked == false && Alcohol.Checked == false && Violence.Checked == true)
                    {
                        modelFilePath = Path.Combine(assetsPath, "violenceweight", "best.onnx");

                    }
                    if (Cigarette.Checked == false && Alcohol.Checked == true && Violence.Checked == true)
                    {
                        modelFilePath = Path.Combine(assetsPath, "alcoholviolenceweight", "best.onnx");

                    }
                    if (Cigarette.Checked == true && Alcohol.Checked == false && Violence.Checked == true)
                    {
                        modelFilePath = Path.Combine(assetsPath, "cigaretteviolenceweight", "best.onnx");

                    }
                    if (Cigarette.Checked == true && Alcohol.Checked == true && Violence.Checked == false)
                    {
                        modelFilePath = Path.Combine(assetsPath, "alcoholcigaretteweight", "best.onnx");

                    }
                    if (Cigarette.Checked == true && Alcohol.Checked == true && Violence.Checked == true)
                    {
                        modelFilePath = Path.Combine(assetsPath, "alcoholcigaretteviolenceweight", "best.onnx");

                    }

                    var modelScorer = new PipelineModelScorer(modelFilePath, mlContext);
                    var model = modelScorer.LoadModel();
                    ImageNetData[] inMemoryCollection = new ImageNetData[]
                {
                    new ImageNetData
                    {
                        InputImage = OpenCvSharp.Extensions.BitmapConverter.ToBitmap(image),
                        Label = "",
                    }
                };
                    var imageDataView = mlContext.Data.LoadFromEnumerable<ImageNetData>(inMemoryCollection);

                    _mySharpImage = (Image)OpenCvSharp.Extensions.BitmapConverter.ToBitmap(image);

                    IEnumerable<float[]> probabilities = modelScorer.Score(model, imageDataView);

                    List<Prediction> predictions = new List<Prediction>();
                    if (Alcohol.Checked == true || Cigarette.Checked == true || Violence.Checked == true)
                    {
                        if (Cigarette.Checked == true && Alcohol.Checked == false && Violence.Checked == false)
                        {
                            using var cigaretteScorer = new Scorer<CigaretteModel>("Assets/cigaretteweight/best.onnx");
                            List<Prediction> cigarettePred = cigaretteScorer.Predict(_mySharpImage);
                            predictions.AddRange(cigarettePred);
                        }
                        if (Cigarette.Checked == false && Alcohol.Checked == true && Violence.Checked == false)
                        {
                            using var alcoholScorer = new Scorer<AlcoholModel>("Assets/alcoholweight/best.onnx");
                            List<Prediction> alcoholPred = alcoholScorer.Predict(_mySharpImage);
                            predictions.AddRange(alcoholPred);
                            using var yolosScorer = new Scorer<YoloCocoP5Model>("Assets/Weights/yolov5s.onnx");
                            List<Prediction> yolosPred = yolosScorer.Predict(_mySharpImage);
                            Parallel.ForEach(yolosPred, pred =>
                            {

                                if (pred.Label.Id == 40 || pred.Label.Id == 41)
                                {
                                    predictions.Add(pred);
                                }
                            });
                        }

                        if (Cigarette.Checked == false && Alcohol.Checked == false && Violence.Checked == true)
                        {
                            using var violenceScorer = new Scorer<ViolenceModel>("Assets/violenceweight/best.onnx");
                            List<Prediction> violencePred = violenceScorer.Predict(_mySharpImage);
                            predictions.AddRange(violencePred);
                            using var yolosScorer = new Scorer<YoloCocoP5Model>("Assets/Weights/yolov5s.onnx");
                            List<Prediction> yolosPred = yolosScorer.Predict(_mySharpImage);
                            Parallel.ForEach(yolosPred, pred =>
                            {
                                if (pred.Label.Id == 44)
                                {
                                    predictions.Add(pred);
                                }
                            });
                        }
                        if (Cigarette.Checked == false && Alcohol.Checked == true && Violence.Checked == true)
                        {
                            using var alcoholviolenceScorer = new Scorer<AlcoholViolenceModel>("Assets/alcoholviolenceweight/best.onnx");
                            List<Prediction> alcoholviolencePred = alcoholviolenceScorer.Predict(_mySharpImage);
                            predictions.AddRange(alcoholviolencePred);
                        }
                        if (Cigarette.Checked == true && Alcohol.Checked == false && Violence.Checked == true)
                        {
                            using var cigaretteviolenceScorer = new Scorer<CigaretteViolenceModel>("Assets/cigaretteviolenceweight/best.onnx");
                            List<Prediction> cigaretteviolencePred = cigaretteviolenceScorer.Predict(_mySharpImage);
                            predictions.AddRange(cigaretteviolencePred);
                        }
                        if (Cigarette.Checked == true && Alcohol.Checked == true && Violence.Checked == false)
                        {
                            using var alcoholcigaretteScorer = new Scorer<AlcoholCigaretteModel>("Assets/alcoholcigaretteweight/best.onnx");
                            List<Prediction> alcoholcigarette = alcoholcigaretteScorer.Predict(_mySharpImage);
                            predictions.AddRange(alcoholcigarette);
                        }
                        if (Cigarette.Checked == true && Alcohol.Checked == true && Violence.Checked == true)
                        {
                            using var allScorer = new Scorer<AlcoholCigaretteViolenceModel>("Assets/alcoholcigaretteviolenceweight/best.onnx");
                            List<Prediction> allPred = allScorer.Predict(_mySharpImage);
                            predictions.AddRange(allPred);
                        }
                    }

                    DrawBoundingBox(ref _mySharpImage, predictions);

                    pictureBox1.SizeMode = PictureBoxSizeMode.StretchImage;
                    pictureBox1.Image = _mySharpImage;
                    Cv2.WaitKey(1);

                    _mySharpImage.Dispose();
                    inMemoryCollection[0].InputImage.Dispose();
                }
                else
                {
                    _mySharpImage = (Image)OpenCvSharp.Extensions.BitmapConverter.ToBitmap(image);
                    pictureBox1.SizeMode = PictureBoxSizeMode.StretchImage;
                    pictureBox1.Image = _mySharpImage; Cv2.WaitKey(1);

                    _mySharpImage.Dispose();
                }
            }
        }

        private void openToolStripMenuItem_Click_1(object sender, EventArgs e)
        {
            frameNo = 0;
            OpenFileDialog ofd = new OpenFileDialog();
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                _capture = new VideoCapture(ofd.FileName);
                Mat image = new Mat();
                _capture.Read(image);
                var mlContext = new MLContext();

                if (Alcohol.Checked == true || Cigarette.Checked == true || Violence.Checked == true)
                {
                    if (Cigarette.Checked == true && Alcohol.Checked == false && Violence.Checked == false)
                    {
                        modelFilePath = Path.Combine(assetsPath, "cigaretteweight", "best.onnx");

                    }
                    if (Cigarette.Checked == false && Alcohol.Checked == true && Violence.Checked == false)
                    {
                        modelFilePath = Path.Combine(assetsPath, "alcoholweight", "best.onnx");

                    }

                    if (Cigarette.Checked == false && Alcohol.Checked == false && Violence.Checked == true)
                    {
                        modelFilePath = Path.Combine(assetsPath, "violenceweight", "best.onnx");

                    }
                    if (Cigarette.Checked == false && Alcohol.Checked == true && Violence.Checked == true)
                    {
                        modelFilePath = Path.Combine(assetsPath, "alcoholviolenceweight", "best.onnx");

                    }
                    if (Cigarette.Checked == true && Alcohol.Checked == false && Violence.Checked == true)
                    {
                        modelFilePath = Path.Combine(assetsPath, "cigaretteviolenceweight", "best.onnx");

                    }
                    if (Cigarette.Checked == true && Alcohol.Checked == true && Violence.Checked == false)
                    {
                        modelFilePath = Path.Combine(assetsPath, "alcoholcigaretteweight", "best.onnx");

                    }
                    if (Cigarette.Checked == true && Alcohol.Checked == true && Violence.Checked == true)
                    {
                        modelFilePath = Path.Combine(assetsPath, "alcoholcigaretteviolenceweight", "best.onnx");

                    }

                    var modelScorer = new PipelineModelScorer(modelFilePath, mlContext);
                    var model = modelScorer.LoadModel();

                    ImageNetData[] inMemoryCollection = new ImageNetData[]
                    {
                    new ImageNetData
                    {
                        InputImage = OpenCvSharp.Extensions.BitmapConverter.ToBitmap(image),
                        Label = "",
                    }
                    };
                    var imageDataView = mlContext.Data.LoadFromEnumerable<ImageNetData>(inMemoryCollection);

                    _mySharpImage = (Image)OpenCvSharp.Extensions.BitmapConverter.ToBitmap(image);

                    IEnumerable<float[]> probabilities = modelScorer.Score(model, imageDataView);

                    List<Prediction> predictions = new List<Prediction>();

                    if (Cigarette.Checked == true && Alcohol.Checked == false && Violence.Checked == false)
                    {
                        using var cigaretteScorer = new Scorer<CigaretteModel>("Assets/cigaretteweight/best.onnx");
                        List<Prediction> cigarettePred = cigaretteScorer.Predict(_mySharpImage);
                        predictions.AddRange(cigarettePred);
                    }
                    if (Cigarette.Checked == false && Alcohol.Checked == true && Violence.Checked == false)
                    {
                        using var alcoholScorer = new Scorer<AlcoholModel>("Assets/alcoholweight/best.onnx");
                        List<Prediction> alcoholPred = alcoholScorer.Predict(_mySharpImage);
                        predictions.AddRange(alcoholPred);
                        using var yolosScorer = new Scorer<YoloCocoP5Model>("Assets/Weights/yolov5s.onnx");
                        List<Prediction> yolosPred = yolosScorer.Predict(_mySharpImage);
                        Parallel.ForEach(yolosPred, pred =>
                        {

                            if (pred.Label.Id == 40 || pred.Label.Id == 41)
                            {
                                predictions.Add(pred);
                            }
                        });
                    }

                    if (Cigarette.Checked == false && Alcohol.Checked == false && Violence.Checked == true)
                    {
                        using var violenceScorer = new Scorer<ViolenceModel>("Assets/violenceweight/best.onnx");
                        List<Prediction> violencePred = violenceScorer.Predict(_mySharpImage);
                        predictions.AddRange(violencePred);
                        using var yolosScorer = new Scorer<YoloCocoP5Model>("Assets/Weights/yolov5s.onnx");
                        List<Prediction> yolosPred = yolosScorer.Predict(_mySharpImage);
                        Parallel.ForEach(yolosPred, pred =>
                        {
                            if (pred.Label.Id == 44)
                            {
                                predictions.Add(pred);
                            }
                        });
                    }
                    if (Cigarette.Checked == false && Alcohol.Checked == true && Violence.Checked == true)
                    {
                        using var alcoholviolenceScorer = new Scorer<AlcoholViolenceModel>("Assets/alcoholviolenceweight/best.onnx");
                        List<Prediction> alcoholviolencePred = alcoholviolenceScorer.Predict(_mySharpImage);
                        predictions.AddRange(alcoholviolencePred);
                    }
                    if (Cigarette.Checked == true && Alcohol.Checked == false && Violence.Checked == true)
                    {
                        using var cigaretteviolenceScorer = new Scorer<CigaretteViolenceModel>("Assets/cigaretteviolenceweight/best.onnx");
                        List<Prediction> cigaretteviolencePred = cigaretteviolenceScorer.Predict(_mySharpImage);
                        predictions.AddRange(cigaretteviolencePred);
                    }
                    if (Cigarette.Checked == true && Alcohol.Checked == true && Violence.Checked == false)
                    {
                        using var alcoholcigaretteScorer = new Scorer<AlcoholCigaretteModel>("Assets/alcoholcigaretteweight/best.onnx");
                        List<Prediction> alcoholcigarette = alcoholcigaretteScorer.Predict(_mySharpImage);
                        predictions.AddRange(alcoholcigarette);
                    }
                    if (Cigarette.Checked == true && Alcohol.Checked == true && Violence.Checked == true)
                    {
                        using var allScorer = new Scorer<AlcoholCigaretteViolenceModel>("Assets/alcoholcigaretteviolenceweight/best.onnx");
                        List<Prediction> allPred = allScorer.Predict(_mySharpImage);
                        predictions.AddRange(allPred);
                    }
                    DrawBoundingBox(ref _mySharpImage, predictions);
                    totalFrame = _capture.Get(CaptureProperty.FrameCount);

                    pictureBox1.SizeMode = PictureBoxSizeMode.StretchImage;
                    pictureBox1.Image = _mySharpImage;
                    Cv2.WaitKey(1);

                    _mySharpImage.Dispose();
                    inMemoryCollection[0].InputImage.Dispose();
                }
                else
                {
                    totalFrame = _capture.Get(CaptureProperty.FrameCount);

                    _mySharpImage = (Image)OpenCvSharp.Extensions.BitmapConverter.ToBitmap(image);
                    pictureBox1.SizeMode = PictureBoxSizeMode.StretchImage;
                    pictureBox1.Image = _mySharpImage;
                    Cv2.WaitKey(1);
                }
            }

        }

        private void btnPlay_Click_1(object sender, EventArgs e)
        {
            if (_capture == null)
            {
                return;
            }
            isReadingFrame = true;

            ObjectDetection(Alcohol.Checked, Violence.Checked, Cigarette.Checked);
        }

        private void btnPause_Click_1(object sender, EventArgs e)
        {
            isReadingFrame = false;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            this.Close();
            MenuForms to = new MenuForms();
            to.Show();
        }
    }
}
