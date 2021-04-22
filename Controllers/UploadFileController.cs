using Load_Test_Visualiser.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

// For more information on enabling MVC for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace Load_Test_Visualiser.Controllers
{
    [Route("fileUpload")]
    public class FileUploadController : Controller
    {
        private IWebHostEnvironment _env;
        private TextFile _uploadedFile;
        private ChartData _chartData;

        public FileUploadController(IWebHostEnvironment env)
        {
            _env = env;
        }

        [HttpPost]
        [RequestFormLimits(MultipartBodyLengthLimit = 209715200)]
        [RequestSizeLimit(209715200)]
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

            var colourChartData = GetColourChartJson(ExtractThreadsSamplersAndFormat());
            var colourChartDataJson = JsonConvert.DeserializeObject(colourChartData); 
            var chartData = new ChartData {
                ColourChartJson = colourChartDataJson
            };

            ViewBag.file = readFile;
            _chartData = chartData;

            ViewBag.chartData = chartData;

            return View("Success");
        }

        public ChartData GetDataTable()
        {
            return _chartData;

        }

        private string GetColourChartJson(List<NormalisedSamples> normalisedSamples)
        {
            var threadNameColumn = new Column { Id = "threadName", Label = "Thread Name", Type = "string" };
            var labelColumn = new Column { Id = "label", Label = "Label", Type = "string" };
            var startDateColumn = new Column { Id = "start", Type = "date" };
            var endDateColumn = new Column { Id = "end", Type = "date" };

            var columns = new List<Column> { threadNameColumn, labelColumn, startDateColumn, endDateColumn };

            var allRows = normalisedSamples.Select(x =>
                new Row { ColumnValues =
                new ColumnValues {
                        ThreadName = x.ThreadName,
                        Label = x.Label,
                        StartEpochMilli = x.StartTime,
                        EndEpochMilli = x.EndTime
                    }
                }).ToList();

            var dataTable = new DataTable { Columns = columns, Rows = allRows};

            return JsonConvert.SerializeObject(dataTable);

  //          var colourChartRows = normalisedSamples.Select(x =>
  //          {
  //              return $"{{c:[{{v: '{x.ThreadName}'}}, {{v: '{x.Label}'}}, {{v:  new Date({x.StartTime})}}, {{v:  new Date({x.EndTime})}} ]}}";
  //          }
  //          ).Aggregate((x, y) => x + "," + Environment.NewLine + y);

  //          var colourChart = $@"{{
  //  cols: [
		//	{{id: 'threadName', label: 'Thread Name', type: 'string'}},
		//	{{id: 'label', label: 'Label', type: 'string'}},
		//	{{id: 'start', type: 'date'}},
		//	{{id: 'end', type: 'date'}}
		//],
  //  rows: [
		//	{colourChartRows}
		//]
  //  }}";
        }

        private List<NormalisedSamples> ExtractThreadsSamplersAndFormat()
        {
            var uploadedFile = _uploadedFile;
            var dir = _env.ContentRootPath;
            var path = Path.Combine(dir, "data", uploadedFile.Name);
            var document = XElement.Load(path);
            var elements = document.Elements().ToList();

            var groupedIntoThreads = elements.GroupBy(d => d.Attribute("tn").Value);
            var failsInThreads = groupedIntoThreads.Select(group => new { Thread = group.Key, Failures = group.Where(g => g.Attribute("s").Value == "false") });

            var httpSamplers = elements.Where(d => d.Attribute("tn").Value != "setUp Thread Group 1-1" && d.Name == "httpSample").OrderBy(d => Int64.Parse(d.Attribute("ts").Value)).ToList();
            var sb = new StringBuilder();

            var formattedSamples = httpSamplers.Select(s => new
            {
                ThreadName = Regex.Match(s.Attribute("tn").Value, @"(\d+-\d+)$").Value,
                Label = s.Attribute("lb").Value,
                TimeStamp = Int64.Parse(s.Attribute("ts").Value),
                ElapsedTime = Int64.Parse(s.Attribute("t").Value),
                Failure = !Boolean.Parse(s.Attribute("s").Value),
                ResponseMessage = s.Attribute("rm").Value,
                AssertionResults = s.Elements("assertionResult").Select(e =>
                {
                    var name = e.Element("name").Value;
                    var isFailure = Boolean.Parse(e.Element("failure").Value);
                    var isError = Boolean.Parse(e.Element("error").Value);
                    var message = e.Element("failureMessage")?.Value;

                    var startOfResult = isFailure ? "Failure:" : "Error:";
                    return $"{startOfResult} {name}" + Environment.NewLine + message;
                }).ToList()
            }).ToList();

            var startTime = formattedSamples.Select(s => s.TimeStamp).Min();

            var normalisedSamples = formattedSamples.Select(s =>
            {
                var responseMessage = String.Empty;
                if (s.Failure && s.AssertionResults.Count() > 0)
                    responseMessage = EncodeJsString(s.AssertionResults.Aggregate((x, y) => x + Environment.NewLine + Environment.NewLine + y));
                else
                    responseMessage = EncodeJsString(s.ResponseMessage);

                return new NormalisedSamples
                {
                    ThreadName = s.ThreadName,
                    Label = s.Label,
                    StartTime = s.TimeStamp,
                    EndTime = s.TimeStamp + s.ElapsedTime,
                    Failure = s.Failure,
                    ResponseMessage = responseMessage
                };
            }).ToList();

            //normalisedSamples.Select(x => $"['{x.ThreadName}', '{x.Label}', new Date({x.StartTime}), new Date({x.EndTime})]").Aggregate((x, y) => x + "," + Environment.NewLine + y);
            return normalisedSamples;
        }

        //      private string GetErrorChartJson(List<NormalisedSamples> normalisedSamples) 
        //      {
        //          var errorChartRows = normalisedSamples.Select(x =>
        //          {
        //              var colour = "grey";
        //              if (x.Failure)
        //                  colour = "red";

        //              return $"{{c:[{{v: '{x.ThreadName}'}}, {{v: '{x.Label}'}}, {{v: '{GetHtmlTooltip(x.Label, x.ResponseMessage)}'}}, {{v: '{colour}'}}, {{v:  new Date({x.StartTime})}}, {{v:  new Date({x.EndTime})}} ]}}";
        //          }
        //          ).Aggregate((x, y) => x + "," + Environment.NewLine + y);

        //          var errorChart = $@"{{
        //  cols: [
        //	{{id: 'threadName', label: 'Thread Name', type: 'string'}},
        //	{{id: 'label', label: 'Label', type: 'string'}},
        //	{{role: 'tooltip', type: 'string', 'p': {{'html': true}}}},
        //	{{id: 'style', role: 'style', type: 'string'}},
        //	{{id: 'start', type: 'date'}},
        //	{{id: 'end', type: 'date'}}
        //],
        //  rows: [
        //	{errorChartRows}
        //]
        //  }};";

        //          var errorChartJson = JsonConvert.SerializeObject(errorChart);
        //          return errorChartJson;
        //      }



        private string GetHtmlTooltip(string label, string responseMessage)
        {
            return $@"<div class=""google-visualization-tooltip"" clone=""true"">
<ul class=""google-visualization-tooltip-item-list""><li class=""google-visualization-tooltip-item"">
<span style=""font-family:Arial;font-size:12px;color:#000000;opacity:1;margin:0;font-style:none;text-decoration:none;font-weight:bold;"">{label}</span>
</li>
</ul>
<div class=""google-visualization-tooltip-separator"">
</div>
<ul class=""google-visualization-tooltip-action-list"">
<li data-logicalname=""action#"" class=""google-visualization-tooltip-action"">
<span style=""font-family:Arial;font-size:12px;color:#000000;opacity:1;margin:0;font-style:none;text-decoration:none;font-weight:bold;"">Response Message:</span>
<span style=""font-family:Arial;font-size:12px;color:#000000;opacity:1;margin:0;font-style:none;text-decoration:none;font-weight:none;"">{responseMessage.Replace('\'', '"')}</span>
</li>
</ul>
</div>".Replace(Environment.NewLine, "");
        }

        public static string EncodeJsString(string s)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("\"");
            foreach (char c in s)
            {
                switch (c)
                {
                    case '\"':
                        sb.Append("\\\"");
                        break;
                    case '\\':
                        sb.Append("\\\\");
                        break;
                    case '\b':
                        sb.Append("\\b");
                        break;
                    case '\f':
                        sb.Append("\\f");
                        break;
                    case '\n':
                        sb.Append("\\n");
                        break;
                    case '\r':
                        sb.Append("\\r");
                        break;
                    case '\t':
                        sb.Append("\\t");
                        break;
                    default:
                        int i = (int)c;
                        if (i < 32 || i > 127)
                        {
                            sb.AppendFormat("\\u{0:X04}", i);
                        }
                        else
                        {
                            sb.Append(c);
                        }
                        break;
                }
            }
            sb.Append("\"");

            return sb.ToString();
        }

    }
}
