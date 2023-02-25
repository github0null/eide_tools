using Newtonsoft.Json;
using System.IO;
using System.IO.Ports;
using System.Text;
using System.Threading;
using System.Runtime.InteropServices;
using CommandLine;
using CommandLine.Text;

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

        public class Options
        {
            [Option('n', "name", Required = false, HelpText = "port name", Default = "NULL")]
            public string PortName { get; set; }

            [Option('b', "baud", Required = false, HelpText = "baud rate", Default = 9600)]
            public int BaudRate { get; set; }

            [Option('d', "data-width", Required = false, HelpText = "data width, like: 8bits, 9bits", Default = 8)]
            public int DataWidth { get; set; }

            [Option('p', "parity", Required = false, HelpText = "parity", Default = Parity.None)]
            public Parity Parity { get; set; }

            [Option('s', "stopbits", Required = false, HelpText = "stop bits", Default = StopBits.One)]
            public StopBits StopBits { get; set; }

            [Option('u', "unix-crlf", Required = false, HelpText = "use unix new line")]
            public bool UnixCRLF { get; set; }

            [Option("no-header", Required = false, HelpText = "not print header")]
            public bool NoPrintHeader { get; set; }
        }

        static void Main(string[] args_)
        {
            System.Console.OutputEncoding = RuntimeEncoding.instance().UTF8;

            if (args_.Length == 0)
            {
                string data = JsonConvert.SerializeObject(SerialPort.GetPortNames());
                System.Console.Write(data);
                return;
            }

            Options cliArgs = null;

            //
            // parse cli args
            //
            {
                var parserResult = new CommandLine.Parser(with => with.HelpWriter = null)
                .ParseArguments<Options>(args_);

                var hTxt = HelpText.AutoBuild(parserResult, h => {
                    h.AutoVersion = false;
                    return HelpText.DefaultParsingErrorsHandler(parserResult, h);
                }, e => e);

                parserResult.WithNotParsed(errs => {
                    Console.WriteLine(Environment.NewLine + hTxt);
                });

                cliArgs = parserResult.Value;
            }

            if (cliArgs == null)
                return;

            if (cliArgs.UnixCRLF) CRLF = "\n";

            try
            {
                if (!cliArgs.NoPrintHeader)
                {
                    System.ConsoleColor color = System.ConsoleColor.Cyan;

                    System.Console.WriteLine("==========================================================================");

                    ColorPrint("port: ", color);
                    System.Console.Write(cliArgs.PortName + ", ");

                    ColorPrint("baud rate: ", color);
                    System.Console.Write(cliArgs.BaudRate.ToString() + ", ");

                    ColorPrint("bit width: ", color);
                    System.Console.Write(cliArgs.DataWidth.ToString() + ", ");

                    ColorPrint("parity: ", color);
                    System.Console.Write(cliArgs.Parity.ToString() + ", ");

                    ColorPrint("stop bits: ", color);
                    System.Console.Write(cliArgs.StopBits.ToString() + "\n");

                    System.Console.WriteLine("==========================================================================\n");
                }

                SerialPort port = new(cliArgs.PortName, 
                    cliArgs.BaudRate, cliArgs.Parity,
                    cliArgs.DataWidth, cliArgs.StopBits);

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
}
