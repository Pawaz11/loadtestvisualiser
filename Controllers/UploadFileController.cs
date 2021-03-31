using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Load_Test_Visualiser.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

// For more information on enabling MVC for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace Load_Test_Visualiser.Controllers
{
    [Route("fileUpload")]
    public class FileUploadController : Controller
    {
        private IWebHostEnvironment _env;
        private TextFile _uploadedFile;

        public FileUploadController(IWebHostEnvironment env)
        {
            _env = env;
        }
        public IActionResult SingleFile(IFormFile file)
        {
            var dir = _env.ContentRootPath;
            var path = Path.Combine(dir, "data", file.FileName);

            using (var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write))
            {
                file.CopyTo(fileStream);
            }

            var content = System.IO.File.ReadAllText(path);

            var readFile = new TextFile
            {
                Content = content,
                Name = file.FileName
            };

            _uploadedFile = readFile;

            ConvertXmlToJson();

            ViewBag.file = readFile;
            return View("Success");
        }

        private TextFile ConvertXmlToJson()
        {
            var xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(_uploadedFile.Content);

            var fileContentJson = JsonConvert.SerializeXmlNode(xmlDoc);
            var jsonConvertedFile = new TextFile
            {
                Name = _uploadedFile.Name,
                Content = fileContentJson
            };

            ViewBag.fileJson = jsonConvertedFile;
            return jsonConvertedFile;
        }
    }
}
