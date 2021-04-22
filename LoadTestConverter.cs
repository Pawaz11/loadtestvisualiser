using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Load_Test_Visualiser
{
	public class LoadTestConverter
	{
		void Main()
		{

			var document = XElement.Load(@"C:\Users\Jake.Turner\Desktop\automation-package-repos\synergytrak-jmx\testdata.xml");
			var elements = document.Elements().ToList();
			var groupedIntoThreads = elements.GroupBy(d => d.Attribute("tn").Value);
			var failsInThreads = groupedIntoThreads.Select(group => new { Thread = group.Key, Failures = group.Where(g => g.Attribute("s").Value == "false") });

			var httpSamplers = elements.Where(d => d.Attribute("tn").Value != "setUp Thread Group 1-1" && d.Name == "httpSample").OrderBy(d => Int64.Parse(d.Attribute("ts").Value)).ToList();

			//var timeWindowSamples = GetTimeWindowSamples(10000, httpSamplers);

			var sb = new StringBuilder();
			//var throughputChartRowsSelector = timeWindowSamples.Select(tws =>
			//{
			//	if (i + 1 < totalTimeWindows)
			//		sb.AppendLine($"{{c:[{{v:  new Date({tws.AverageTimeStamp})}}, {{v: {tws.ThroughputSamplesPerSecond}}}, {{v: {tws.ErroredSamplesPerSecond}}}]}},");
			//	else
			//		sb.AppendLine($"{{c:[{{v:  new Date({tws.AverageTimeStamp})}}, {{v: {tws.ThroughputSamplesPerSecond}}}, {{v: {tws.ErroredSamplesPerSecond}}}]}}");
			//	return i;
			//}).AsParallel().ToList();
			//var throughputChartRows = sb.ToString();

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

				return new
				{
					ThreadName = s.ThreadName,
					Label = s.Label,
					StartTime = s.TimeStamp,
					EndTime = s.TimeStamp + s.ElapsedTime,
					Failure = s.Failure,
					ResponseMessage = responseMessage
				};
			}).ToList();

			normalisedSamples.Select(x => $"['{x.ThreadName}', '{x.Label}', new Date({x.StartTime}), new Date({x.EndTime})]").Aggregate((x, y) => x + "," + Environment.NewLine + y);

			var colourChartRows = normalisedSamples.Select(x =>
			{
				return $"{{c:[{{v: '{x.ThreadName}'}}, {{v: '{x.Label}'}}, {{v:  new Date({x.StartTime})}}, {{v:  new Date({x.EndTime})}} ]}}";
			}
			).Aggregate((x, y) => x + "," + Environment.NewLine + y);

			var errorChartRows = normalisedSamples.Select(x =>
			{
				var colour = "grey";
				if (x.Failure)
					colour = "red";

				return $"{{c:[{{v: '{x.ThreadName}'}}, {{v: '{x.Label}'}}, {{v: '{GetHtmlTooltip(x.Label, x.ResponseMessage)}'}}, {{v: '{colour}'}}, {{v:  new Date({x.StartTime})}}, {{v:  new Date({x.EndTime})}} ]}}";
			}
			).Aggregate((x, y) => x + "," + Environment.NewLine + y);

			var colourChart = $@"{{
    cols: [
			{{id: 'threadName', label: 'Thread Name', type: 'string'}},
			{{id: 'label', label: 'Label', type: 'string'}},
			{{id: 'start', type: 'date'}},
			{{id: 'end', type: 'date'}}
		],
    rows: [
			{colourChartRows}
		]
    }};";

			var errorChart = $@"{{
    cols: [
			{{id: 'threadName', label: 'Thread Name', type: 'string'}},
			{{id: 'label', label: 'Label', type: 'string'}},
			{{role: 'tooltip', type: 'string', 'p': {{'html': true}}}},
			{{id: 'style', role: 'style', type: 'string'}},
			{{id: 'start', type: 'date'}},
			{{id: 'end', type: 'date'}}
		],
    rows: [
			{errorChartRows}
		]
    }};";

		//	var throughputChart = $@"{{
  //  cols: [
		//	{{id: 'timeStamp', label: 'TimeStamp', type: 'date'}},
		//	{{id: 'throughput', label: 'Throughput (samples/s)', type: 'number'}},
		//	{{id: 'errors', label: 'Error Rate (samples/s)', type: 'number'}}
		//],
  //  rows: [
		//	{throughputChartRows}
		//]
  //  }}";
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

		string GetHtmlTooltip(string label, string responseMessage)
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

		decimal[] GetMovingAverage(int period, IList<decimal> source)
		{
			decimal[] buffer = new decimal[period];
			decimal[] output = new decimal[source.Count];
			var current_index = 0;
			for (int i = 0; i < source.Count; i++)
			{
				buffer[current_index] = source[i] / period;
				decimal ma = 0.0M;
				for (int j = 0; j < period; j++)
				{
					ma += buffer[j];
				}
				output[i] = ma;
				current_index = (current_index + 1) % period;
			}
			return output;
		}
		IEnumerable<TimeWindowSampleRange> GetTimeWindowSamples(int windowSizeEpochMilli, IEnumerable<XElement> samples)
		{
			var samplesWithTimesStamps = samples.Select(s => new SampleWithTimeStamp() { Sample = s, TimeStamp = Int64.Parse(s.Attribute("ts").Value), Success = Boolean.Parse(s.Attribute("s").Value) }).ToList();
			var firstSamplerStartTime = samplesWithTimesStamps.First().TimeStamp;
			var lastSamplerStartTime = samplesWithTimesStamps.Last().TimeStamp;

			var startOfWindowTimes = new List<long>() { };
			var currentWindowStart = firstSamplerStartTime;

			while (currentWindowStart < lastSamplerStartTime)
			{
				startOfWindowTimes.Add(currentWindowStart);
				currentWindowStart += windowSizeEpochMilli;
			}

			//[00000001, 100000002, ... ]
			// => 
			// [
			//    [Start: 0000001, End: 00000001 + 100000000, Samples: []],
			// ]

			var timeWindows = startOfWindowTimes.Select(ts => new { Start = ts, End = ts + windowSizeEpochMilli, Samples = new List<SampleWithTimeStamp>() }).ToList();

			int i = 0;
			samplesWithTimesStamps.Select(sampleWithTimeStamp => {
				var timeWindow = timeWindows.Single(tw => sampleWithTimeStamp.TimeStamp >= tw.Start && sampleWithTimeStamp.TimeStamp < tw.End);
				timeWindow.Samples.Add(sampleWithTimeStamp);
				return i;
			}).AsParallel().ToList();

			return timeWindows.Select(timeWindow => new TimeWindowSampleRange()
			{
				startTimeEpochMilli = timeWindow.Start,
				endTimeEpochMilli = timeWindow.End,
				sampleCount = timeWindow.Samples.Count,
				errorCount = timeWindow.Samples.Count(s => !s.Success)
			}).ToList();
		}

		IEnumerable<TimeWindowSampleRange> GetTimeWindowErrorSamples(int windowSizeEpochMilli, IEnumerable<XElement> samples)
		{
			var samplesWithTimesStamps = samples.Select(s => new { Sample = s, TimeStamp = Int64.Parse(s.Attribute("ts").Value), Failure = Boolean.Parse(s.Attribute("s").Value) }).ToList();
			var firstSamplerStartTime = samplesWithTimesStamps.First().TimeStamp;
			var lastSamplerStartTime = samplesWithTimesStamps.Last().TimeStamp;

			var startOfWindowTimes = new List<long>() { };
			var currentWindowStart = firstSamplerStartTime;

			while (currentWindowStart < lastSamplerStartTime)
			{
				startOfWindowTimes.Add(currentWindowStart);
				currentWindowStart += windowSizeEpochMilli;
			}

			return startOfWindowTimes.Select(ts => new { Start = ts, End = ts + windowSizeEpochMilli }).Select(timeWindow => new TimeWindowSampleRange()
			{
				startTimeEpochMilli = timeWindow.Start,
				endTimeEpochMilli = timeWindow.End,
				sampleCount = samplesWithTimesStamps.Count(sts => sts.TimeStamp >= timeWindow.Start && sts.TimeStamp < timeWindow.End)
			}).ToList();
		}

	}
	class SampleWithTimeStamp
	{
		public XElement Sample { get; set; }
		public long TimeStamp { get; set; }
		public bool Success { get; set; }
	}

	class TimeWindowSampleRange
	{
		public long startTimeEpochMilli;
		public long endTimeEpochMilli;
		public int sampleCount;
		public int errorCount;

		public float ThroughputSamplesPerSecond
		{
			get
			{
				var timeWindowLengthEpochMilli = endTimeEpochMilli - startTimeEpochMilli;
				var timeWindowLengthSeconds = (float)timeWindowLengthEpochMilli / 1000;

				return (float)sampleCount / timeWindowLengthSeconds;
			}
		}

		public float ErroredSamplesPerSecond
		{
			get
			{
				var timeWindowLengthEpochMilli = endTimeEpochMilli - startTimeEpochMilli;
				var timeWindowLengthSeconds = (float)timeWindowLengthEpochMilli / 1000;

				return (float)errorCount / timeWindowLengthSeconds;
			}
		}

		public long AverageTimeStamp => (startTimeEpochMilli + endTimeEpochMilli) / 2;
	}
}
