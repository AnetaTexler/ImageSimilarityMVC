using System.Data.Entity;

namespace ImageSimilarityMVC.Models
{
    // represents the Entity Framework image database context, 
    // which handles fetching, storing, and updating ImageModel class instances in a database
    public class ImageModelDBContext : DbContext
    {
        public DbSet<ImageModel> Images { get; set; }
    }
}