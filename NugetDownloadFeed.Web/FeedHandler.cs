using System;
using System.Linq;
using System.ServiceModel.Syndication;
using System.Web;
using System.Xml;

namespace NugetDownloadFeed.WebRole
{
    public class FeedHandler : IHttpHandler
    {
        private static int downloadCount = 0;
        private DateTime lastUpdated = DateTime.Now;
        public bool IsReusable
        {
            get { return true; }
        }

        public void ProcessRequest(HttpContext context)
        {
            var packageName = context.Request.QueryString["package"];
            var version = context.Request.QueryString["version"];
            const string nugetServiceUri = "http://packages.nuget.org/v1/FeedService.svc";
            var nugetContext = new Nuget.GalleryFeedContext(new Uri(nugetServiceUri));
            var count = (from x in nugetContext.Packages where x.Id == packageName && x.Version == version select x.DownloadCount).First();
            if(count > downloadCount)
                lastUpdated = DateTime.Now;
            downloadCount = count;
            var nugetUrl = string.Format(
                "{0}/Packages(Id='{1}',Version='{2}')", nugetServiceUri, packageName, version);
            var feed = new SyndicationFeed("Nuget Download Count Feed",
                                           "Provides the current total download count for a Nuget Package",
                                           new Uri(nugetUrl),string.Format("Nuget Downloads for {0} version {1}", packageName, version), new DateTimeOffset(lastUpdated),
                                           new[]
                                               {
                                                   new SyndicationItem(
                                                       string.Format("{0} has {1} total downloads", packageName, count),
                                                       "",
                                                       new Uri(string.Format("http://nuget.org/packages/{0}",
                                                                             packageName)), count.ToString(),
                                                       new DateTimeOffset(lastUpdated))
                                               });
            using (var xmlWriter = XmlWriter.Create(context.Response.OutputStream))
            {
                feed.SaveAsRss20(xmlWriter);
                xmlWriter.Flush();
                xmlWriter.Close();
            }

            context.Response.ContentType = "text/xml";
            context.Response.End();
        }

    }
}
