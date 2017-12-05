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
using System.Web.Helpers;

namespace ImageSimilarityMVC.Controllers
{
    public class ImagesController : Controller
    {
        private ImageModelDBContext db = new ImageModelDBContext();

        // GET: Images
        public ActionResult Index()
        {
            return View(db.Images.ToList());
        }

        // GET: Images/Details/5
        public ActionResult Details(int? id)
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
        public ActionResult Create([Bind(Include = "ID,TimeStamp,Name,Type,Size,Image,HistogramR,HistogramG,HistogramB")] ImageModel imageModel,
            HttpPostedFileBase file)
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
                        return RedirectToAction("Create");
                    }
                }
                else if (!file.ContentType.Contains("image"))
                {
                    ViewBag.Message = "Not a valid image.";
                }
                else
                {
                    ViewBag.Message = "You have not specified a file.";
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
        public ActionResult Edit([Bind(Include = "Name")] ImageModel imageModel)
        {
            if (ModelState.IsValid)
            {
                db.Entry(imageModel).State = EntityState.Modified;
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



        #region Private methods

        private void AddSizeAndHistograms(ref ImageModel imageModel)
        {
            Bitmap imageBitmap = new Bitmap(new MemoryStream(imageModel.Image));
            int[] frequencyArr_channelR = Enumerable.Repeat(0, 256).ToArray(); // red 
            int[] frequencyArr_channelG = Enumerable.Repeat(0, 256).ToArray(); // green
            int[] frequencyArr_channelB = Enumerable.Repeat(0, 256).ToArray(); // blue

            for (int row = 0; row < imageBitmap.Height; row++)
            {
                for (int col = 0; col < imageBitmap.Width; col++)
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
            Bitmap histogramImageR = new Bitmap(256, frequencyArr_channelR.Max());
            Bitmap histogramImageG = new Bitmap(256, frequencyArr_channelG.Max());
            Bitmap histogramImageB = new Bitmap(256, frequencyArr_channelB.Max());

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
                frequencyArr[i] = (int)Math.Ceiling((double)frequencyArr[i] / norm);
        }

        private void CreateImage(int[] frequencyArr, Color color, ref Bitmap histogramImage)
        {
            for (int row = 0; row < histogramImage.Height; row++)
            {
                for (int col = 0; col < histogramImage.Width; col++)
                {
                    if (row + 1 <= frequencyArr[col])
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
