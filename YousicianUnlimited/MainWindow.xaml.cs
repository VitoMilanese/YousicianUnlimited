using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Runtime.InteropServices;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace YousicianUnlimited
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		private static string ConfigFile { get; set; }
		private DateTime StartDate { get; set; }
		private DateTime LastDate { get; set; }
		private ulong Shift { get; set; }
		private MainWindowViewModel _vm { get; set; }

		private EventHandler DateSet { get; set; }
		private EventHandler DateReset { get; set; }

		public static string ConfigValue(string key, string defaultValue = null)
		{
			var file = File.ReadAllLines(ConfigFile);

			foreach (var line in file)
			{
				var pair = line?.Split('\t') ?? null;
				if (pair == null || pair.Length < 2) continue;
				if (pair[0].Equals(key))
					return pair[1];
			}
			return defaultValue;
		}

		public MainWindow()
		{
			InitializeComponent();
			_vm = (MainWindowViewModel)DataContext;
			var location = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
			ConfigFile = location + @"\start.cfg";
			var line = File.Exists(ConfigFile) ? File.ReadLines(ConfigFile)?.FirstOrDefault(p => !string.IsNullOrWhiteSpace(p))?.Trim() : string.Empty;
			if (string.IsNullOrWhiteSpace(line))
			{
				StartDate = DateTime.Now;
				LastDate = DateTime.Now;
				Shift = 0;
				SaveConfig();
			}
			else
			{
				StartDate = Convert.ToDateTime(ConfigValue(@"Start", DateTime.Now.ToString()));
				LastDate = Convert.ToDateTime(ConfigValue(@"Last", DateTime.Now.ToString()));
				Shift = ulong.Parse(ConfigValue(@"Shift", @"0"));
				SetDate(LastDate);
			}
			_vm.StartDate = StartDate.ToString(@"yyyy/MM/dd HH:mm:ss");
			_vm.LastDate = LastDate.ToString(@"yyyy/MM/dd HH:mm:ss");
			_vm.Shift = Shift;

			DateSet += (sender, args) => SaveConfig();
		}

		private void SaveConfig()
		{
			var lines = new List<string>
				{
					$"Start\t{StartDate.ToString(@"yyyy/MM/dd HH:mm:ss")}",
					//$"Start\t{_vm.StartDate}",
					$"Last\t{LastDate.ToString(@"yyyy/MM/dd HH:mm:ss")}",
					//$"Last\t{_vm.LastDate}",
					$"Shift\t{Shift}",
				};
			File.WriteAllLines(ConfigFile, lines);
		}

		private void ShiftButton_Click(object sender, RoutedEventArgs e)
		{
			var tag = ((sender as Button)?.Tag ?? null)?.ToString()?.Trim();
			if (string.IsNullOrWhiteSpace(tag)) return;
			if (!byte.TryParse(tag, out var value) || value < 0 || value > 1) return;
			if (value == 0)
			{
				if (Shift == 0) return;
				--Shift;
			}
			else
			{
				if (Shift == ulong.MaxValue) return;
				++Shift;
			}
			_vm.Shift = Shift;
			LastDate = StartDate.AddDays(Shift);
			_vm.LastDate = LastDate.ToString(@"yyyy/MM/dd HH:mm:ss");
			SetDate(LastDate);
		}

		private bool SetDate(DateTime date)
		{
			var offset = TimeZoneInfo.Local.GetUtcOffset(DateTime.UtcNow);
			var dtN = LastDate;
			//var dtI = StartDate;
			//var intDays = (dtN - dtI).Days;
			//if (intDays < 0) return false;
			//var diff = (ulong)intDays;
			//if (diff >= Shift) return false;
			//var days = Shift - diff;

			//dtN = dtN.AddDays(days);
			var nt = GetNetworkTime();
			nt = nt.Subtract(offset);
			nt = nt.Subtract(offset);
			dtN = dtN.Subtract(offset);
			var st = new SYSTEMTIME
			{
				wYear = (short)dtN.Year,
				wMonth = (short)dtN.Month,
				wDay = (short)dtN.Day,
				wHour = (short)nt.Hour,//dtN.Hour,
				wMinute = (short)nt.Minute,//dtN.Minute,
				wSecond = (short)nt.Second//dtN.Second
			};
			var ok = SetSystemTime(ref st);
			if (ok)
				DateSet?.Invoke(this, EventArgs.Empty);
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
				DateReset?.Invoke(this, EventArgs.Empty);
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

		[DllImport("kernel32.dll", SetLastError = true)]
		public static extern bool SetSystemTime(ref SYSTEMTIME st);

		private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
		{
			if (!_vm.NotClosing) return;
			DateReset += (sender, args) => Dispatcher.Invoke(() => Close());
			Task.Run(ResetDate);
			_vm.NotClosing = false;
			e.Cancel = true;
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
