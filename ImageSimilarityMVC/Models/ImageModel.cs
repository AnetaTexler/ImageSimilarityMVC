using System.ComponentModel.DataAnnotations;

namespace ImageSimilarityMVC.Models
{
    public class ImageModel
    {
        [Key]
        public int ID { get; set; }
        public string TimeStamp { get; set; }
        [Required(ErrorMessage = "You did not fill the name of image.")]
        public string Name { get; set; }
        public string Type { get; set; }
        public string Size { get; set; }
        public byte[] Image { get; set; }
        public byte[] HistogramR { get; set; }
        public byte[] HistogramG { get; set; }
        public byte[] HistogramB { get; set; }
    }
}