using System.Reflection;
using Microsoft.AspNetCore.Mvc;

namespace FastTravelPlugin;

// 0.0.54 port note: the 0.0.54 host does not serve plugin wwwroot/ at /static/<plugin>/ yet
// (that mechanism landed upstream after 0.0.54). This controller provides exactly the URLs
// that lua/fasttravel.lua expects (see its baseUrl): /static/FastTravelPlugin/<file>.png.
[ApiController]
public class FastTravelStaticController : ControllerBase
{
    private static readonly string WwwrootPath = Path.Join(
        Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "wwwroot");

    [HttpGet("/static/FastTravelPlugin/{fileName}")]
    public ActionResult GetStaticFile(string fileName)
    {
        // flat directory only — reject anything that smells like path traversal
        if (fileName.Contains("..") || fileName.Contains('/') || fileName.Contains('\\'))
            return BadRequest();

        var fullPath = Path.Join(WwwrootPath, fileName);
        if (!System.IO.File.Exists(fullPath))
            return NotFound();

        return PhysicalFile(fullPath, GetContentType(fileName));
    }

    private static string GetContentType(string fileName) => Path.GetExtension(fileName).ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".webp" => "image/webp",
        _ => "application/octet-stream"
    };
}
