using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using ImageSimilarityMVC.Models;
using System.Drawing;
using System.IO;
using System.Diagnostics;

namespace ImageSimilarityMVC.Controllers
{
    public class ImagesController : Controller
    {
        private ImageModelDBContext db = new ImageModelDBContext();

        // GET: Images
        public ActionResult Index(string searchString)
        {
            var images = from i in db.Images select i;

            if (!String.IsNullOrEmpty(searchString))
            {
                images = images.Where(i => i.Name.Contains(searchString));
            }

            images = images.OrderByDescending(i => i.TimeStamp);

            return View(images.ToList());
        }

        // GET: Images/Details/5
        public ActionResult Details(int? id, string similarImagesToDisplayCnt, string similarityFunction)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            ImageModel imageModel = db.Images.Find(id);

            if (imageModel == null)
            {
                return HttpNotFound();
            }

            if (!String.IsNullOrEmpty(similarImagesToDisplayCnt) && !String.IsNullOrEmpty(similarityFunction))
            {
                ViewBag.DisplayCnt = similarImagesToDisplayCnt;
                ViewBag.SimFunction = similarityFunction;
                return RedirectToAction("ShowSimilar", new { id = imageModel.ID, similarImagesToDisplayCnt = ViewBag.DisplayCnt, similarityFunction = ViewBag.SimFunction });
            }

            return View(imageModel);
        }

        // GET: Images/Create
        public ActionResult Create()
        {
            return View();
        }

        // POST: Images/Create
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create([Bind(Include = "Name,Image")] ImageModel imageModel, HttpPostedFileBase file)
        {
            if (ModelState.IsValid)
            {
                if (file != null && file.ContentLength > 0 && file.ContentType.Contains("image"))
                {
                    try
                    {
                        imageModel.TimeStamp = DateTime.Now.ToString("yyMMddHHmmss");
                        imageModel.Type = file.ContentType;
                        imageModel.Image = StreamToBytes(file.InputStream);
                        AddSizeAndHistograms(ref imageModel);
                        ViewBag.Message = "File uploaded successfully.";
                    }
                    catch (Exception ex)
                    {
                        ViewBag.Message = "ERROR:" + ex.Message.ToString();
                        return View(imageModel);
                        //return RedirectToAction("Create");
                    }
                }
                else if (file != null && file.ContentLength > 0 && !file.ContentType.Contains("image"))
                {
                    ViewBag.Message = "Not a valid image.";
                    return View(imageModel);
                }
                else
                {
                    ViewBag.Message = "You have not specified a file.";
                    return View(imageModel);
                }

                db.Images.Add(imageModel);
                db.SaveChanges();
                return RedirectToAction("Index");
            }

            return View(imageModel);
        }

        // GET: Images/Edit/5
        public ActionResult Edit(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            ImageModel imageModel = db.Images.Find(id);

            if (imageModel == null)
            {
                return HttpNotFound();
            }

            return View(imageModel);
        }

        // POST: Images/Edit/5
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind(Include = "ID,TimeStamp,Name,Type,Size,Image,HistogramR,HistogramG,HistogramB")] ImageModel imageModel)
        {
            if (ModelState.IsValid)
            {
                db.Entry(imageModel).State = EntityState.Modified;

                // preserve following
                db.Entry(imageModel).Property(x => x.Size).IsModified = false;
                db.Entry(imageModel).Property(x => x.TimeStamp).IsModified = false;
                db.Entry(imageModel).Property(x => x.Type).IsModified = false;
                db.Entry(imageModel).Property(x => x.Image).IsModified = false;
                db.Entry(imageModel).Property(x => x.HistogramR).IsModified = false;
                db.Entry(imageModel).Property(x => x.HistogramG).IsModified = false;
                db.Entry(imageModel).Property(x => x.HistogramB).IsModified = false;

                db.SaveChanges();
                return RedirectToAction("Index");
            }
            return View(imageModel);
        }

        // GET: Images/Delete/5
        public ActionResult Delete(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            ImageModel imageModel = db.Images.Find(id);

            if (imageModel == null)
            {
                return HttpNotFound();
            }

            return View(imageModel);
        }

        // POST: Images/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            ImageModel imageModel = db.Images.Find(id);
            db.Images.Remove(imageModel);
            db.SaveChanges();
            return RedirectToAction("Index");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }

        // GET: Images/ShowSimilar/5?similarImagesToDisplayCnt=.....
        public ActionResult ShowSimilar(int? id, string similarImagesToDisplayCnt, string similarityFunction)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            ImageModel inputImageModel = db.Images.Find(id);

            if (inputImageModel == null)
            {
                return HttpNotFound();
            }

            ViewBag.DisplayCnt = similarImagesToDisplayCnt;
            ViewBag.SimFunction = similarityFunction;

            List<ImageModel> candidateImageModels = db.Images.ToList();
            candidateImageModels.RemoveAt(candidateImageModels.IndexOf(inputImageModel)); // list of pontencial similar images apart from the input image

            //List<Tuple<double, ImageModel>> bhattacharyyaDistanceImagePairList = new List<Tuple<double, ImageModel>>();
            List<Tuple<double, double, double, double, ImageModel>> similarityTupleList = new List<Tuple<double, double, double, double, ImageModel>>();

            // GET FREQUENCY ARRAY FROM INPUT IMAGE HISTOGRAMS ------------------------------------------------
            int[] frequencyArrR_input = GetFrequency(inputImageModel.HistogramR);
            int[] frequencyArrG_input = GetFrequency(inputImageModel.HistogramG);
            int[] frequencyArrB_input = GetFrequency(inputImageModel.HistogramB);
            // 16 BINS (each bin is 16px wide with same values - average of each 16 values from frequency array)
            int binSize = 16; // have to be the power of 2, smaller than 256!!
            int sumOfRPixels, sumOfGPixels, sumOfBPixels;
            ModifyFrequencyArr(ref frequencyArrR_input, binSize, out sumOfRPixels); // out - sum of one column in each bin
            ModifyFrequencyArr(ref frequencyArrG_input, binSize, out sumOfGPixels);
            ModifyFrequencyArr(ref frequencyArrB_input, binSize, out sumOfBPixels);
            // -------------------------------------------------------------------------------------------------

            Stopwatch sw = new Stopwatch();
            sw.Start();

            foreach (ImageModel candidateImageModel in candidateImageModels)
            {
                switch (similarityFunction)
                {
                    case "1": // EUCLIDEAN
                        similarityTupleList.Add(CompareImages(frequencyArrR_input, frequencyArrG_input, frequencyArrB_input, 
                                                              sumOfRPixels, sumOfGPixels, sumOfBPixels, candidateImageModel, binSize, "1"));
                        break;
                    case "2": // BHATTACHARYYA
                        similarityTupleList.Add(CompareImages(frequencyArrR_input, frequencyArrG_input, frequencyArrB_input,
                                                              sumOfRPixels, sumOfGPixels, sumOfBPixels, candidateImageModel, binSize, "2"));
                        break;
                    default:
                        throw new Exception("Unknown similarity function.");
                }
            }

            sw.Stop();
            double time = sw.Elapsed.TotalMilliseconds;
            ViewBag.Time = time;

            // DESCENDING SORT
            similarityTupleList.Sort((x, y) => y.Item1.CompareTo(x.Item1));

            int cntToDisplay = Int32.Parse(similarImagesToDisplayCnt);
            if (cntToDisplay > similarityTupleList.Count)
                cntToDisplay = similarityTupleList.Count;

            ViewBag.SimilarImages = similarityTupleList.Take(cntToDisplay).ToList();

            candidateImageModels.Clear();
            similarityTupleList.Clear();

            return View(inputImageModel);
        }



        #region Private methods

        #region EXTRACT HISTOGRAMS, NORMALIZE THEM AND SAVE THEM TO IMAGE MODEL
        private void AddSizeAndHistograms(ref ImageModel imageModel)
        {
 
            Bitmap imageBitmap = new Bitmap(new MemoryStream(imageModel.Image));
            int[] frequencyArr_channelR = Enumerable.Repeat(0, 256).ToArray(); // red 
            int[] frequencyArr_channelG = Enumerable.Repeat(0, 256).ToArray(); // green
            int[] frequencyArr_channelB = Enumerable.Repeat(0, 256).ToArray(); // blue

            for (int col = 0; col < imageBitmap.Width; col++)
            {
                for (int row = imageBitmap.Height - 1; row >= 0; row--)
                {
                    Color color = imageBitmap.GetPixel(col, row);
                    frequencyArr_channelR[color.R]++;
                    frequencyArr_channelG[color.G]++;
                    frequencyArr_channelB[color.B]++;
                }
            }

            // normalization (high of histogram = 200)
            Normalize(ref frequencyArr_channelR);
            Normalize(ref frequencyArr_channelG);
            Normalize(ref frequencyArr_channelB);

            // create histogram images
            Bitmap histogramImageR = new Bitmap(256, 200 /*frequencyArr_channelR.Max()*/);
            Bitmap histogramImageG = new Bitmap(256, 200 /*frequencyArr_channelG.Max()*/);
            Bitmap histogramImageB = new Bitmap(256, 200 /*frequencyArr_channelB.Max()*/);

            CreateImage(frequencyArr_channelR, Color.Red, ref histogramImageR);
            CreateImage(frequencyArr_channelG, Color.Green, ref histogramImageG);
            CreateImage(frequencyArr_channelB, Color.Blue, ref histogramImageB);

            imageModel.Size = imageBitmap.Width + " x " + imageBitmap.Height;
            imageModel.HistogramR = BitmapToBytes(histogramImageR);
            imageModel.HistogramG = BitmapToBytes(histogramImageG);
            imageModel.HistogramB = BitmapToBytes(histogramImageB);
        }

        private void Normalize(ref int[] frequencyArr)
        {
            double norm = frequencyArr.Max() / 200.0;
            for (int i = 0; i < frequencyArr.Length; i++)
                frequencyArr[i] = (int)Math.Ceiling(frequencyArr[i] / norm);
        }

        private void CreateImage(int[] frequencyArr, Color color, ref Bitmap histogramImage)
        {
            for (int col = 0; col < histogramImage.Width; col++)
            {
                for (int row = histogramImage.Height - 1; row >= 0; row--)
                {
                    if (histogramImage.Height - row <= frequencyArr[col])
                        histogramImage.SetPixel(col, row, color);
                    else
                        histogramImage.SetPixel(col, row, Color.Black);
                }
            }
        }

        private byte[] BitmapToBytes(Bitmap image)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                image.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                return stream.ToArray();
            }
        }

        #endregion
        //============================================================================================================

        #region COMPARE HISTOGRAMS, GET RATE OF SIMILARITY
        private Tuple<double, double, double, double, ImageModel> CompareImages(int[] frequencyArrR_input, int[] frequencyArrG_input, int[] frequencyArrB_input,
                                                                                int sumOfRPxlsInOneColInEachBin_input, int sumOfGPxlsInOneColInEachBin_input, 
                                                                                int sumOfBPxlsInOneColInEachBin_input, ImageModel candidateModel, int binSize, 
                                                                                string simFunciton)
        {
            double distanceR = 0.0;
            double distanceG = 0.0;
            double distanceB = 0.0;
            double resultDistance = 0.0;
            
            int[] frequencyArrR_candidate = GetFrequency(candidateModel.HistogramR);
            int[] frequencyArrG_candidate = GetFrequency(candidateModel.HistogramG);
            int[] frequencyArrB_candidate = GetFrequency(candidateModel.HistogramB);

            // 16 BINS (each bin is 16px wide with same values - average of each 16 values from frequency array)
            int sumOfRPxlsInOneColInEachBin_candidate, sumOfGPxlsInOneColInEachBin_candidate, sumOfBPxlsInOneColInEachBin_candidate;
            ModifyFrequencyArr(ref frequencyArrR_candidate, binSize, out sumOfRPxlsInOneColInEachBin_candidate);
            ModifyFrequencyArr(ref frequencyArrG_candidate, binSize, out sumOfGPxlsInOneColInEachBin_candidate);
            ModifyFrequencyArr(ref frequencyArrB_candidate, binSize, out sumOfBPxlsInOneColInEachBin_candidate);

            switch (simFunciton)
            {
                case "1": // EUCLIDEAN DISTANCE (lowest = exact match)
                    for (int i = 0; i < 256; i += binSize)
                    {
                        distanceR += Math.Pow((frequencyArrR_input[i] - frequencyArrR_candidate[i]) / 200.0, 2);
                        distanceG += Math.Pow((frequencyArrG_input[i] - frequencyArrG_candidate[i]) / 200.0, 2);
                        distanceB += Math.Pow((frequencyArrB_input[i] - frequencyArrB_candidate[i]) / 200.0, 2);
                    }

                    distanceR = Math.Sqrt(distanceR / (256 / binSize));
                    distanceG = Math.Sqrt(distanceG / (256 / binSize));
                    distanceB = Math.Sqrt(distanceB / (256 / binSize));

                    // convert to %
                    distanceR = (100 - (distanceR * 100));
                    distanceG = (100 - (distanceG * 100));
                    distanceB = (100 - (distanceB * 100));

                    resultDistance = (distanceR + distanceG + distanceB) / 3; // convert to % and count average
                    break;

                case "2": // BHATTACHARYYA DISTANCE (highest = exact match)
                    for (int i = 0; i < 256; i += binSize)
                    {
                        // values are devided by sum of all non black pixels in a column in a bin  - to get precentages share of a color in a histogram (histogram devided to bins)
                        distanceR += Math.Sqrt(((frequencyArrR_input[i] / ((double)sumOfRPxlsInOneColInEachBin_input)) * (frequencyArrR_candidate[i] / ((double)sumOfRPxlsInOneColInEachBin_candidate))));
                        distanceG += Math.Sqrt(((frequencyArrG_input[i] / ((double)sumOfGPxlsInOneColInEachBin_input)) * (frequencyArrG_candidate[i] / ((double)sumOfGPxlsInOneColInEachBin_candidate))));
                        distanceB += Math.Sqrt(((frequencyArrB_input[i] / ((double)sumOfBPxlsInOneColInEachBin_input)) * (frequencyArrB_candidate[i] / ((double)sumOfBPxlsInOneColInEachBin_candidate))));
                    }

                    // convert to %
                    distanceR = (distanceR * 100);
                    distanceG = (distanceG * 100);
                    distanceB = (distanceB * 100);

                    resultDistance = (distanceR + distanceG + distanceB) / 3; // count average
                    break;
            }
            
            return new Tuple<double, double, double, double, ImageModel>(resultDistance, distanceR, distanceG, distanceB, candidateModel);
        }

        private void ModifyFrequencyArr(ref int[] frequencyArr, int binSize, out int sumOfNonBlackPixels)
        {
            sumOfNonBlackPixels = 0;
            for (int i = 0; i < 256; i += binSize)
            {
                int sum = 0;
                for (int j = i; j < i + binSize; j++)
                {
                    sum += frequencyArr[j];
                }
                int avg = sum / binSize;
                for (int j = i; j < i + binSize; j++)
                {
                    frequencyArr[j] = avg;
                }
                sumOfNonBlackPixels += frequencyArr[i];
            }
        }

        private int[] GetFrequency(byte[] histogram)
        {
            int[] frequencyArr = Enumerable.Repeat(0, 256).ToArray();
            Bitmap histogramImage = new Bitmap(new MemoryStream(histogram));

            // start in left bottom corner of image
            for (int col = 0; col < histogramImage.Width; col++)
            {
                for (int row = histogramImage.Height - 1; row >= 0; row--)
                {
                    Color color = histogramImage.GetPixel(col, row);

                    if (color.R == 0 && color.G == 0 && color.B == 0)
                        break;

                    frequencyArr[col]++;
                }
            }

            return frequencyArr;
        }

        #endregion
        //============================================================================================================

        private byte[] StreamToBytes(Stream input)
        {
            byte[] buffer = new byte[16 * 1024];
            using (MemoryStream ms = new MemoryStream())
            {
                int read;
                while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    ms.Write(buffer, 0, read);
                }
                return ms.ToArray();
            }
        }

        #endregion
    }
}
