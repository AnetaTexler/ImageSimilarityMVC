using Microsoft.Owin;
using Owin;

[assembly: OwinStartupAttribute(typeof(ImageSimilarityMVC.Startup))]
namespace ImageSimilarityMVC
{
    public partial class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            ConfigureAuth(app);
        }
    }
}
