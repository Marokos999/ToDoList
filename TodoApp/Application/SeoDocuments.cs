using System.Security;
using System.Text;

namespace TodoApp.Application;

public static class SeoDocuments
{
  // Only pages meant for search engines; everything behind login is excluded
  public static readonly string[] PublicPaths = ["/", "/faq", "/privacy", "/terms", "/Account/Register", "/Account/Login"];

  public static string Robots(string origin) => string.Join('\n',
    "User-agent: *",
    "Allow: /Account/Register",
    "Allow: /Account/Login",
    "Disallow: /Account/",
    "Disallow: /list/",
    "Disallow: /api/",
    $"Sitemap: {origin}/sitemap.xml") + "\n";

  public static string Sitemap(string origin)
  {
    var sb = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">\n");
    foreach(var path in PublicPaths)
      sb.Append("  <url><loc>").Append(SecurityElement.Escape(origin + (path == "/" ? "/" : path))).Append("</loc></url>\n");
    return sb.Append("</urlset>\n").ToString();
  }
}
