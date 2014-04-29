using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using Mono.Options;
using Newtonsoft.Json;
using ZedGraph;
using CH = RA.HashPeak.ConsoleHelper;

namespace RA.HashPeak
{
	class HashPeak
	{
		private const string LogFilenameFormat = "HashPeak_{0}_GPU{1}_MEM{2}_{3}-{4}_{5}.csv";
		private const string GraphFilenameFormat = "HashPeak_{0}_GPU{1}_MEM{2}_{3}-{4}_{5}.png";
		private const int StabilitySampleCount = 3;
		private const double StabilityMaxDiff = 1.0d;
		private const int AutoDelayMaxWaitSeconds = 30;

		private string _host;
		private int _port = -1;
		private int _gpuId = -1;
		private int _minGpuClock = -1;
		private int _maxGpuClock = -1;
		private int _step = -1;
		private int _delay = -1;
		private bool _autoDelayEnabled = true;

		private IPEndPoint _endpoint;
		private int _saveGpuClock;
		private int _saveMemClock;
		private string _logFilename;

		private PointPairList _hashRatePoints;
		private PointPairList _hwErrorPoints;

		static void Main(string[] args)
		{
			ShowBanner();

			var hp = new HashPeak();
			hp.ParseCommandLineArgs(args);
			hp.Run();
		}

		public static void ShowBanner()
		{
			CH.Write(@"  _   _           _    ", ConsoleColor.White); CH.Write(@" ____            _     ", ConsoleColor.Green); CH.Write(@" _   ___  " + Environment.NewLine, ConsoleColor.DarkGreen);
			CH.Write(@" | | | | __ _ ___| |__ ", ConsoleColor.White); CH.Write(@"|  _ \ ___  __ _| | __ ", ConsoleColor.Green); CH.Write(@"/ | / _ \ " + Environment.NewLine, ConsoleColor.DarkGreen);
			CH.Write(@" | |_| |/ _` / __| '_ \", ConsoleColor.White); CH.Write(@"| |_) / _ \/ _` | |/ / ", ConsoleColor.Green); CH.Write(@"| || | | |   Copyright 2014" + Environment.NewLine, ConsoleColor.DarkGreen);
			CH.Write(@" |  _  | (_| \__ \ | | ", ConsoleColor.White); CH.Write(@"|  __/  __/ (_| |   <  ", ConsoleColor.Green); CH.Write(@"| || |_| |   Rickard Andersson" + Environment.NewLine, ConsoleColor.DarkGreen);
			CH.Write(@" |_| |_|\__,_|___/_| |_", ConsoleColor.White); CH.Write(@"|_|   \___|\__,_|_|\_\ ", ConsoleColor.Green); CH.Write(@"|_(_)___/ " + Environment.NewLine + Environment.NewLine, ConsoleColor.DarkGreen);
		}

		public void ParseCommandLineArgs(string[] args)
		{
			var showHelp = false;
			var delayString = "auto";

			// We're using Mono.Options to parse the command line arguments (see Options.cs)
			var os = new OptionSet
			{
				{ "host=", "IP or hostname for miner API. Default: 127.0.0.1.", v => _host = v },
				{ "port=", "Port number for miner API. Default: 4028.", (int v) => _port = v },
				{ "gpu-id=", "GPU ID to work on. [required]", (int v) => _gpuId = v },
				{ "min-gpu-clock=", "Lower limit of GPU engine clock frequency range to test. [required]", (int v) => _minGpuClock = v },
				{ "max-gpu-clock=", "Upper limit of GPU engine clock frequency range to test. [required]", (int v) => _maxGpuClock = v },
				{ "step=", "Number of MHz to increase GPU engine clock per iteration. Default 1.", (int v) => _step = v },
				{ "delay=", "Seconds to wait between setting new clock and testing the hashrate. Default: auto (see README).", v => delayString = v },
				{ "help", "Show this message and exit.", v => showHelp = v != null }
			};

			try
			{
				os.Parse(args);
			}
			catch (OptionException e)
			{
				CH.Exit(e.Message + Environment.NewLine + "Try `HashPeak --help' for more information.");
			}

			if (showHelp)
				CH.ShowHelp(os);

			// Validate parameter values and set defaults
			if (_host == null)
				_host = "127.0.0.1";
			if (_port < 0)
				_port = 4028;
			if (_gpuId < 0)
				CH.Exit("The --gpu-id parameter is missing or invalid. Try `HashPeak --help' for more information.");
			if (_minGpuClock < 0)
				CH.Exit("The --min-gpu-clock parameter is missing or invalid. Try `HashPeak --help' for more information.");
			if (_maxGpuClock < 0)
				CH.Exit("The --max-gpu-clock parameter is missing or invalid. Try `HashPeak --help' for more information.");
			if (_step < 0)
				_step = 1;

			if (delayString.ToLower() != "auto")
			{
				if (!int.TryParse(delayString, out _delay))
					CH.Exit("The --delay parameter is missing or invalid. The paramater must be either `auto' or a delay value in seconds. Try `HashPeak --help' for more information.");

				if (_delay < 0)
					_delay = 20;

				_autoDelayEnabled = false;
			}
		}

		public void Run()
		{
			// Query API for version number (verifies connectivity)
			CH.Write(string.Format(" - Connecting to API on {0}:{1}... ", _host, _port), ConsoleColor.White);
			var versionResponse = Send<VersionResponse>(JsonConvert.SerializeObject(new Request { Command = "version" }));
			CH.Write("Success", ConsoleColor.Green);
			CH.Write(string.Format(" ({0})" + Environment.NewLine, versionResponse.Status[0].Description), ConsoleColor.Gray);

			// The autoDelay feature doesn't make sense when running against cgminer due to the limited API hashrate accuracy
			if (_autoDelayEnabled && versionResponse.Status[0].Description.StartsWith("cgminer"))
			{
				_autoDelayEnabled = false;
				_delay = 20;

				CH.Write(" - Detected cgminer. Forcing delay to 20 seconds." + Environment.NewLine, ConsoleColor.White);
			}

			// Verify that we have privileged access to the API
			CH.Write(" - Verifying priviliged access to API... ", ConsoleColor.White);
			var statusResponse = Send<StatusResponse>(JsonConvert.SerializeObject(new Request { Command = "privileged" }));
			if (statusResponse.Status[0].Status == "E")
				CH.Exit("Failure", statusResponse.Status[0].Msg);

			CH.Write("Success" + Environment.NewLine, ConsoleColor.Green);

			// Query API for the specified GPU id
			CH.Write(string.Format(" - Looking for GPU with id {0}... ", _gpuId), ConsoleColor.White);
			var gpuResponse = Send<GpuResponse>(JsonConvert.SerializeObject(new Request { Command = "gpu", Parameter = _gpuId.ToString(CultureInfo.InvariantCulture) }));
			if (gpuResponse.Gpu == null)
				CH.Exit("Failure", gpuResponse.Status[0].Msg);

			CH.Write("Success", ConsoleColor.Green);
			CH.Write(string.Format(" ({0} {1}/{2})" + Environment.NewLine, gpuResponse.Gpu[0].Status, gpuResponse.Gpu[0].GpuClock, gpuResponse.Gpu[0].MemoryClock), ConsoleColor.Gray);
			_saveMemClock = gpuResponse.Gpu[0].MemoryClock;
			_saveGpuClock = gpuResponse.Gpu[0].GpuClock;

			// TODO: Only proceed if card is "Alive"?

			CH.Write(Environment.NewLine + " Starting measurements." + Environment.NewLine + Environment.NewLine, ConsoleColor.White);

			// Setup
			_logFilename = string.Format(LogFilenameFormat, _host, _gpuId, _saveMemClock, _minGpuClock, _maxGpuClock, DateTime.Now.ToString("yyyyMMdd"));
			var peakHashRate = 0d;
			var peakGpuClock = 0;
			var hwErrorsTally = 0;
			var peakHwErrors = 0;
			_hashRatePoints = new PointPairList();
			_hwErrorPoints = new PointPairList();

			// Loop through GPU engine clock range
			for (var gpuClock = _minGpuClock; gpuClock <= _maxGpuClock; gpuClock += _step)
			{
				CH.Write(string.Format(" - Setting GPU engine clock to {0}... ", gpuClock), ConsoleColor.White);
				statusResponse = Send<StatusResponse>(JsonConvert.SerializeObject(new Request { Command = "gpuengine", Parameter = _gpuId + "," + gpuClock }));
				if (statusResponse.Status[0].Status == "E" || statusResponse.Status[0].Status == "F")
					CH.Exit("Failure", statusResponse.Status[0].Msg);
				CH.Write("Success" + Environment.NewLine, ConsoleColor.Green);

				double currentHashRate;
				if (_autoDelayEnabled)
				{
					CH.Write(" - Waiting for hashrate to stabilize... ", ConsoleColor.White);
					currentHashRate = GetStableHashRate();
					CH.Write("Done", ConsoleColor.Green);
					CH.Write(string.Format(" ({0} khash/s)" + Environment.NewLine, currentHashRate), ConsoleColor.Gray);
				}
				else
				{
					// Wait _delay seconds and then take the measurement
					// TODO: Wait longer the first time
					CH.Write(string.Format(" - Waiting {0} seconds for hashrate to stabilize... ", _delay), ConsoleColor.White);
					System.Threading.Thread.Sleep(_delay * 1000);
					CH.Write("Done", ConsoleColor.Green);

					gpuResponse = Send<GpuResponse>(JsonConvert.SerializeObject(new Request { Command = "gpu", Parameter = _gpuId.ToString(CultureInfo.InvariantCulture) }));
					if (gpuResponse.Gpu == null)
						CH.Exit("Failure", gpuResponse.Status[0].Msg);

					currentHashRate = gpuResponse.Gpu[0].MhsXs * 1000;
					CH.Write(string.Format(" ({0} khash/s)" + Environment.NewLine, currentHashRate), ConsoleColor.Gray);
				}

				// Check for hashrate peak
				if (currentHashRate > peakHashRate)
				{
					peakHashRate = currentHashRate;
					peakGpuClock = gpuClock;
				}

				// Calculate HW errors delta and check for peak
				var hwErrorsDelta = gpuResponse.Gpu[0].HardwareErrors - hwErrorsTally;
				hwErrorsTally = gpuResponse.Gpu[0].HardwareErrors;
				if (hwErrorsDelta > peakHwErrors)
					peakHwErrors = hwErrorsDelta;

				// Log to csv file
				AppendToLog(DateTime.Now, gpuResponse.Gpu[0].MemoryClock, gpuClock, currentHashRate, hwErrorsDelta);

				// Save data points for graph
				_hashRatePoints.Add(gpuClock, currentHashRate);
				_hwErrorPoints.Add(gpuClock, hwErrorsDelta);
			}

			CH.Write(string.Format(" - Measurements completed. Resetting GPU engine clock to {0}... ", _saveGpuClock), ConsoleColor.White);
			statusResponse = Send<StatusResponse>(JsonConvert.SerializeObject(new Request { Command = "gpuengine", Parameter = _gpuId + "," + _saveGpuClock }));
			if (statusResponse.Status[0].Status == "E" || statusResponse.Status[0].Status == "F")
				CH.Exit("Failure", statusResponse.Status[0].Msg);
			CH.Write("Success" + Environment.NewLine, ConsoleColor.Green);

			CH.Write(Environment.NewLine + string.Format("Peak of {0} khash/s detected at GPU clock {1} MHz. See {2} for details.", peakHashRate.ToString("F1"), peakGpuClock, _logFilename) + Environment.NewLine, ConsoleColor.White);

			GenerateGraph();
		}

		private double GetStableHashRate()
		{
			var currentHashRate = 0d;
			var hashRateHistory = new List<double>();

			// Try for a maximum of AutoDelayMaxWaitSeconds seconds
			for (var i = 0; i < AutoDelayMaxWaitSeconds; i++)
			{
				System.Threading.Thread.Sleep(1000);

				var gpuResponse = Send<GpuResponse>(JsonConvert.SerializeObject(new Request { Command = "gpu", Parameter = _gpuId.ToString(CultureInfo.InvariantCulture) }));
				if (gpuResponse.Gpu == null)
					CH.Exit("Failure", gpuResponse.Status[0].Msg);

				currentHashRate = gpuResponse.Gpu[0].MhsXs * 1000;

				// Do we have enough samples?
				if (hashRateHistory.Count == StabilitySampleCount)
				{
					if (HashRateIsStable(currentHashRate, hashRateHistory))
						break;

					// Remove the oldest sample
					hashRateHistory.RemoveAt(0);
				}

				// Add a new sample
				hashRateHistory.Add(currentHashRate);
			}

			return currentHashRate;
		}

		private bool HashRateIsStable(double currentHashRate, IEnumerable<double> hashRateHistory)
		{
			// The hashrate is considered stable if the current rate is within StabilityMaxDiff
			// of StabilitySampleCount samples in the history
			foreach (var historicRate in hashRateHistory)
			{
				if (Math.Abs(currentHashRate - historicRate) > StabilityMaxDiff)
					return false;
			}

			return true;
		}

		private void AppendToLog(DateTime dateTime, int memClock, int gpuClock, double hashRate, int hardwareErrors)
		{
			if (!File.Exists(_logFilename))
			{
				// The csv file doesn't exist so create it and add the CSV headers
				using (var w = File.AppendText(_logFilename))
				{
					w.WriteLine("\"Timestamp\",\"Memory clock\",\"GPU clock\",\"Hashrate (khash/s)\",\"Hardware errors\"");
				}
			}

			using (var w = File.AppendText(_logFilename))
			{
				w.WriteLine("\"{0}\",\"{1}\",\"{2}\",\"{3}\",\"{4}\"", dateTime.ToString("yyyy-MM-dd HH:mm:ss"), memClock, gpuClock, hashRate.ToString("F1"), hardwareErrors);
			}
		}

		private void GenerateGraph()
		{
			// Setup the graph pane
			var pane = new GraphPane(new RectangleF(0, 0, 1024, 480), "HashPeak", "GPU engine clock (MHz)", "Hashrate (khash/s)");
			pane.Y2Axis.Title.Text = "Hardware errors";

			// Add the hashrate curve
			pane.AddCurve("Hashrate", _hashRatePoints, Color.Green, SymbolType.None);
			((LineItem)pane.CurveList[0]).Line.Width = 2.0F;
			((LineItem)pane.CurveList[0]).Line.IsAntiAlias = true;

			// Add the HW errors curve
			pane.AddCurve("Hardware errors", _hwErrorPoints, Color.Red, SymbolType.None);
			((LineItem)pane.CurveList[1]).Line.Width = 2.0F;
			((LineItem)pane.CurveList[1]).Line.IsAntiAlias = true;
			pane.CurveList[1].IsY2Axis = true;
			pane.Y2Axis.IsVisible = true;

			// Setup scales
			pane.XAxis.Scale.Min = _minGpuClock;
			pane.XAxis.Scale.Max = _maxGpuClock;
			pane.Y2Axis.Scale.Min = 0;

			// Save as PNG
			var bitmap = new Bitmap(1, 1);
			using (var g = Graphics.FromImage(bitmap))
				pane.AxisChange(g);

			try
			{
				pane.GetImage(true).Save(string.Format(GraphFilenameFormat, _host, _gpuId, _saveMemClock, _minGpuClock, _maxGpuClock, DateTime.Now.ToString("yyyyMMdd")), ImageFormat.Png);
			}
			catch (Exception)
			{
				CH.Exit("An error occurred while saving graph to file {0}. Is the file in use?", string.Format(GraphFilenameFormat, _host, _gpuId, _saveMemClock, _minGpuClock, _maxGpuClock, DateTime.Now.ToString("yyyyMMdd")));
			}
		}

		public T Send<T>(string request)
		{
			// Initialize the socket endpoint
			if (_endpoint == null)
				InitializeEndpoint();

			// Setup socket
			var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
			{
				SendTimeout = 5000,
				ReceiveTimeout = 5000
			};
			var sb = new StringBuilder(256);

			try
			{
				// Connect to endpoint and send payload
				socket.Connect(_endpoint);
				socket.Send(Encoding.ASCII.GetBytes(request));

				// Receive response
				var buffer = new byte[65535];
				while (true)
				{
					var len = socket.Receive(buffer, 65535, SocketFlags.None);
					if (len < 1)
						break;
					sb.Append(Encoding.ASCII.GetString(buffer), 0, len);
					if (buffer[len - 1] == '\0')
						break;
				}

				// Tidy up
				socket.Shutdown(SocketShutdown.Both);
				socket.Close();
			}
			catch (Exception ex)
			{
				CH.Exit("Failure", ex.Message);
			}

			var response = sb.ToString();

			// The property name for the average Mhash varies depending on log interval. Normalize it to "MHS Xs".
			if (typeof(T) == typeof(GpuResponse))
				response = Regex.Replace(response, "\"MHS [0-9]+s\"", "\"MHS Xs\"");

			// Deserialize response
			var result = JsonConvert.DeserializeObject<T>(response);
			if (result == null)
				CH.Exit("Failure", "Received unexpected response from API.");

			return result;
		}

		private void InitializeEndpoint()
		{
			// Get host addresses for specified host
			var addresses = Dns.GetHostAddresses(_host);
			if (addresses.Length == 0)
				CH.Exit("Failure", "Unable to retrieve address from specified host.");

			// Loop through addresses and use the first IPV4 address
			for (var i = 0; i < addresses.Length; i++)
			{
				if (addresses[i].AddressFamily == AddressFamily.InterNetwork)
				{
					_endpoint = new IPEndPoint(addresses[i], _port);
					break;
				}
			}

			if (_endpoint == null)
				CH.Exit("Failure", "Unable to resolve specified host to a valid IP address.");
		}
	}
}
