using Newtonsoft.Json;
using System.IO;
using System.IO.Ports;
using System.Text;
using System.Threading;
using System.Runtime.InteropServices;

namespace SerialPortMonitor
{
    class OsInfo
    {
        private static OsInfo _instance = null;

        public string OsType { get; }

        private OsInfo()
        {
            OsType = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "win32" : "linux";
        }

        public static OsInfo instance()
        {
            if (_instance == null)
                _instance = new OsInfo();
            return _instance;
        }
    }

    class RuntimeEncoding
    {
        public int CurrentCodePage { get; }
        public Encoding Default { get; } // 用于本地打印输出的默认字符集（ANSI）
        public Encoding UTF8 { get; }
        public Encoding UTF16 { get; }
        public Encoding UTF16BE { get; }
        public Encoding UTF32BE { get; }

        private static RuntimeEncoding _instance = null;

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern int GetACP();

        private RuntimeEncoding()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            UTF8 = new UTF8Encoding(false);
            UTF16 = Encoding.Unicode;
            UTF16BE = new UnicodeEncoding(true, false);
            UTF32BE = new UTF32Encoding(true, false);

            // init system default encoding
            CurrentCodePage = OsInfo.instance().OsType == "win32" ? GetACP() : Encoding.Default.CodePage;
            Encoding sysDefEncoding = Encoding.GetEncoding(CurrentCodePage);
            if (CurrentCodePage == 65001) sysDefEncoding = UTF8; // no bom-header
            Default = sysDefEncoding;
        }

        public static RuntimeEncoding instance()
        {
            if (_instance == null)
                _instance = new RuntimeEncoding();
            return _instance;
        }
    }

    class Program
    {
        static string CRLF = System.Environment.NewLine;

        static void Main(string[] args)
        {
            System.Console.OutputEncoding = RuntimeEncoding.instance().UTF8;

            if (args.Length == 0)
            {
                string data = JsonConvert.SerializeObject(SerialPort.GetPortNames());
                System.Console.Write(data);
                return;
            }

            if (args.Length < 2)
            {
                LogError("Too few parameters !");
                return;
            }

            if (args.Length % 2 != 0)
            {
                LogError("Incorrect parameter format !");
                return;
            }

            string portName = "NULL";
            SerialPortOption option = SerialPortOption.GetDefault();

            for (int i = 0; i < args.Length - 1; i += 2)
            {
                string val = args[i + 1];

                try
                {
                    switch (args[i].Substring(1))
                    {
                        case "n":
                            portName = val;
                            break;
                        case "b":
                            option.baudRate = int.Parse(val);
                            break;
                        case "d":
                            option.dataBits = int.Parse(val);
                            break;
                        case "p":
                            option.parity = (Parity)int.Parse(val);
                            break;
                        case "s":
                            option.stopBits = (StopBits)int.Parse(val);
                            break;
                        case "l":
                            if (int.Parse(val) == 1)
                                CRLF = "\n";
                            break;
                        default:
                            break;
                    }
                }
                catch (System.Exception)
                {
                    // do nothing
                }
            }

            try
            {
                System.ConsoleColor color = System.ConsoleColor.Cyan;

                System.Console.WriteLine("==========================================================================");

                ColorPrint("port: ", color);
                System.Console.Write(portName + ", ");

                ColorPrint("baud rate: ", color);
                System.Console.Write(option.baudRate.ToString() + ", ");

                ColorPrint("bit width: ", color);
                System.Console.Write(option.dataBits.ToString() + ", ");

                ColorPrint("parity: ", color);
                System.Console.Write(option.parity.ToString() + ", ");

                ColorPrint("stop bits: ", color);
                System.Console.Write(option.stopBits.ToString() + "\n");

                System.Console.WriteLine("==========================================================================\n");

                SerialPort port = new SerialPort(portName, option.baudRate, option.parity,
                   option.dataBits, option.stopBits);

                port.Open();

                Thread workThread = new Thread(new ParameterizedThreadStart(OutputTask));

                try
                {
                    workThread.IsBackground = true;

                    workThread.Start(port);

                    Encoding gbk = Encoding.GetEncoding(936);

                    while (true)
                    {
                        string line = System.Console.ReadLine() + CRLF;
                        byte[] buf = Encoding.Convert(Encoding.Unicode, gbk, Encoding.Unicode.GetBytes(line));
                        port.Write(buf, 0, buf.Length);
                    }
                }
                catch (System.Exception error)
                {
                    ColorPrint("\r\n" + error.Message + "\r\n", System.ConsoleColor.Red);
                    ColorPrint("---------- serialport closed ----------", System.ConsoleColor.Yellow);
                }

                if (port.IsOpen) // close serialport
                {
                    port.Close();
                }
            }
            catch (System.Exception err)
            {
                LogError(err.Message);
            }
        }

        static void OutputTask(object _port)
        {
            SerialPort port = (SerialPort)_port;

            try
            {
                byte[] buf = new byte[2];
                Encoding utf8 = new UTF8Encoding(false);
                Encoding gbk = Encoding.GetEncoding(936);

                while (true)
                {
                    buf[0] = (byte)port.ReadByte();
                    buf[1] = buf[0] > 0x80 ? ((byte)port.ReadByte()) : (byte)0;
                    byte[] utf8Buf = Encoding.Convert(gbk, utf8, buf, 0, buf[1] == 0 ? 1 : 2);
                    System.Console.Write(utf8.GetString(utf8Buf));
                }
            }
            catch (System.Exception)
            {
                if (port.IsOpen) // close serialport to trigger Exception on Main thread
                {
                    port.Close();
                }
            }
        }

        public static void LogError(string txt)
        {
            System.Console.ForegroundColor = System.ConsoleColor.Red;
            System.Console.WriteLine(txt);
            System.Console.ResetColor();
        }

        public static void LogWarn(string txt)
        {
            System.Console.ForegroundColor = System.ConsoleColor.Yellow;
            System.Console.WriteLine(txt);
            System.Console.ResetColor();
        }

        public static void ColorPrint(string txt, System.ConsoleColor color)
        {
            System.Console.ForegroundColor = color;
            System.Console.Write(txt);
            System.Console.ResetColor();
        }
    }

    struct SerialPortOption
    {
        public int baudRate;
        public int dataBits;
        public Parity parity;
        public StopBits stopBits;

        public static SerialPortOption GetDefault()
        {
            return new SerialPortOption
            {
                baudRate = 9600,
                parity = Parity.None,
                dataBits = 8,
                stopBits = StopBits.One
            };
        }
    }
}
