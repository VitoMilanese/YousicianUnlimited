using System;
using System.Threading.Tasks;
using System.Windows;
using System.Runtime.InteropServices;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace YousicianUnlimited
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		private MainWindowViewModel _vm { get; }
		private object _locker { get; }
		private Stopwatch _timer { get; }
		private Task _timeTask { get; }
		private DateTime _startDate { get; set; }
		private EventHandler _dateReset { get; set; }


		public static string AppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"\YousucuanUnlimited";
		public static string ConfigFile = AppData + @"\config.cfg";

		public MainWindow()
		{
			InitializeComponent();
			_vm = (MainWindowViewModel)DataContext;
			_locker = new object();
			_timer = new Stopwatch();
			_timeTask = new Task(TimeTask);
			_timeTask.Start();
		}

		public static string ConfigValue(string key, string defaultValue = null)
		{
			try
			{
				if (!Directory.Exists(AppData)) Directory.CreateDirectory(AppData);
				if (!File.Exists(ConfigFile)) File.WriteAllText(ConfigFile, string.Empty);
				var file = File.ReadAllLines(ConfigFile);

				foreach (var line in file)
				{
					var pair = line?.Split('\t') ?? null;
					if (pair == null || pair.Length < 2) continue;
					if (pair[0].Equals(key))
						return pair[1];
				}
			}
			catch (Exception ex)
			{
			}
			return defaultValue;
		}


		private ulong Shift
		{
			get => ulong.Parse(ConfigValue(@"DaysShift", @"0"));
			set
			{
				for (var tries = 0; tries < 3; ++tries)
				{
					try
					{
						var res = $"DaysShift\t{value}";
						if (!Directory.Exists(AppData)) Directory.CreateDirectory(AppData);
						if (!File.Exists(ConfigFile))
						{
							File.WriteAllText(ConfigFile, res);
							break;
						}
						var file = File.ReadAllLines(ConfigFile);

						for (var i = 0; i < file.Length; ++i)
						{
							var pair = file[i]?.Split('\t') ?? null;
							if (pair == null || pair.Length < 2) continue;
							if (pair[0].Equals(@"DaysShift"))
							{
								file[i] = res;
								File.WriteAllLines(ConfigFile, file);
							}
						}
						break;
					}
					catch (Exception ex)
					{
						Thread.Sleep(300);
					}
				}
			}
		}

		private bool SetDate(DateTime date)
		{
			var offset = TimeZoneInfo.Local.GetUtcOffset(DateTime.UtcNow);
			var dtN = date.Subtract(offset);
			var daysShift = Shift;
			dtN = dtN.AddDays(daysShift);
			var hoursShift = int.Parse(ConfigValue(@"HoursShift", @"0"));
			dtN = dtN.AddHours(hoursShift);
			var st = new SYSTEMTIME
			{
				wYear = (short)dtN.Year,
				wMonth = (short)dtN.Month,
				wDay = (short)dtN.Day,
				wHour = (short)dtN.Hour,
				wMinute = (short)dtN.Minute,
				wSecond = (short)dtN.Second
			};
			var ok = SetSystemTime(ref st);
			if (ok)
				Dispatcher.Invoke(() => _vm.StartDate = dtN.ToString(@"yyyy/MM/dd HH{0}mm{1}ss{2}"));
			return ok;
		}

		private bool ResetDate()
		{
			var offset = TimeZoneInfo.Local.GetUtcOffset(DateTime.UtcNow);
			var nt = GetNetworkTime();
			Thread.Sleep(1000);
			nt = nt.Subtract(offset);
			var st = new SYSTEMTIME
			{
				wYear = (short)nt.Year,
				wMonth = (short)nt.Month,
				wDay = (short)nt.Day,
				wHour = (short)nt.Hour,
				wMinute = (short)nt.Minute,
				wSecond = (short)nt.Second
			};
			var ok = SetSystemTime(ref st);
			if (ok)
				_dateReset?.Invoke(this, EventArgs.Empty);
			return ok;
		}

		public static DateTime GetNetworkTime()
		{
			//default Windows time server
			const string ntpServer = "time.windows.com";

			// NTP message size - 16 bytes of the digest (RFC 2030)
			var ntpData = new byte[48];

			//Setting the Leap Indicator, Version Number and Mode values
			ntpData[0] = 0x1B; //LI = 0 (no warning), VN = 3 (IPv4 only), Mode = 3 (Client Mode)

			var addresses = Dns.GetHostEntry(ntpServer).AddressList;

			//The UDP port number assigned to NTP is 123
			var ipEndPoint = new IPEndPoint(addresses[0], 123);
			//NTP uses UDP

			using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
			{
				socket.Connect(ipEndPoint);

				//Stops code hang if NTP is blocked
				socket.ReceiveTimeout = 3000;

				socket.Send(ntpData);
				socket.Receive(ntpData);
				socket.Close();
			}

			//Offset to get to the "Transmit Timestamp" field (time at which the reply 
			//departed the server for the client, in 64-bit timestamp format."
			const byte serverReplyTime = 40;

			//Get the seconds part
			ulong intPart = BitConverter.ToUInt32(ntpData, serverReplyTime);

			//Get the seconds fraction
			ulong fractPart = BitConverter.ToUInt32(ntpData, serverReplyTime + 4);

			//Convert From big-endian to little-endian
			intPart = SwapEndianness(intPart);
			fractPart = SwapEndianness(fractPart);

			var milliseconds = (intPart * 1000) + ((fractPart * 1000) / 0x100000000L);

			//**UTC** time
			var networkDateTime = (new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc)).AddMilliseconds((long)milliseconds);

			return networkDateTime.ToLocalTime();
		}

		static uint SwapEndianness(ulong x)
		{
			return (uint)(((x & 0x000000ff) << 24) +
						  ((x & 0x0000ff00) << 8) +
						  ((x & 0x00ff0000) >> 8) +
						  ((x & 0xff000000) >> 24));
		}

#if DEBUG
		private DateTime DateTimeNow => DateTime.Now.AddDays(_vm.Started ? Shift : 0);

		public static bool SetSystemTime(ref SYSTEMTIME _)
		{
			return true;
		}
#else
		private static DateTime DateTimeNow => DateTime.Now;

		[DllImport("kernel32.dll", SetLastError = true)]
		public static extern bool SetSystemTime(ref SYSTEMTIME st);
#endif

		private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
		{
			if (!_vm.NotClosing) return;
			_dateReset += (sender, args) => Dispatcher.Invoke(Close);
			_vm.NotClosing = false;
			_timeTask.Wait();
			Task.Run(() =>
			{
				bool reset = false;
				for (var i = 0; i < 3; ++i)
				{
					reset = ResetDate();
					if (reset) break;
					Thread.Sleep(1000);
				}
				if (!reset)
				{
					MessageBox.Show(@"Failed to reset DateTime", @"Reset Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
					Dispatcher.Invoke(Close);
				}
			});
			e.Cancel = true;
		}

		private void StartButton_Click(object sender, RoutedEventArgs e)
		{
			_startDate = DateTimeNow;
			_vm.StartDate = _startDate.ToString(@"yyyy/MM/dd HH{0}mm{1}ss{2}");
			_vm.Started = true;
			_vm.IsTimerRunning = true;
		}

		private void StopButton_Click(object sender, RoutedEventArgs e)
		{
			_vm.Started = false;
			_vm.IsTimerRunning = false;
			_vm.StartDate = @"not set";
			_timer.Reset();
			_vm.RemainingTime = @"00h 00m 00s";
			ResetDate();
		}

		private void PauseButton_Click(object sender, RoutedEventArgs e)
		{
			if (_timer == null) return;
			lock(_locker)
			{
				_vm.IsTimerRunning = !_vm.IsTimerRunning;
				if (!_vm.IsTimerRunning)
					_timer.Stop();
				else
					_timer.Start();
			}
		}

		private void TimeTask()
		{
			_timer.Reset();
			while (_vm.NotClosing)
			{
				var time = int.Parse(ConfigValue(@"TimeShiftInterval", @"600"));
				var h = time / 3600;
				var m = (time % 3600) / 60;
				var s = time - h * 3600 - m * 60;
				var interval = new TimeSpan(h, m, s);

				if (_vm.IsTimerRunning)
				{
					var remaining = interval - _timer.Elapsed;
					if (_vm.IsTimerRunning)
						_vm.RemainingTime = $"{remaining.Hours}h {remaining.Minutes}m {remaining.Seconds}s";
				}

				lock (_locker)
				{
					if (!_vm.IsTimerRunning) continue;

					if (!_timer.IsRunning || _timer.ElapsedTicks >= interval.Ticks)
						_timer.Restart();
					else
					{
						if (!_vm.Started)
							_timer.Reset();
						continue;
					}

					if (!_vm.Started)
					{
						_timer.Reset();
						continue;
					}

					SetDate(_startDate);
					++Shift;
				}
			}
		}

		private void RestartYousicianButton_Click(object sender, RoutedEventArgs e)
		{
			var procName = @"Yousician";
			var processes = Process.GetProcessesByName(procName).ToList();
			if (processes.Count == 0) return;
			try
			{
				var path = processes.First().MainModule.FileName;
				path = Path.GetDirectoryName(Path.GetDirectoryName(path)) + @"\Yousician Launcher.exe";
				processes.ForEach(p => p.Kill());
				Process.Start(path);
			}
			catch(Exception ex)
			{
			}
		}

		public static void StartProcess(string procName)
		{
			if (procName[0] != '"') procName = '"' + procName + '"';
			Process.Start(procName);
		}

		public static bool KillProcess(string procName)
		{
			var processes = Process.GetProcessesByName(procName).ToList();
			if (processes.Count == 0) return false;
			processes.ForEach(p => p.Kill());
			return true;
		}
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct SYSTEMTIME
	{
		public short wYear;
		public short wMonth;
		public short wDayOfWeek;
		public short wDay;
		public short wHour;
		public short wMinute;
		public short wSecond;
		public short wMilliseconds;
	}
}
