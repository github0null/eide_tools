using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace axf2elf
{
    class Program
    {
        static readonly int CODE_ERR = 1;
        static readonly int CODE_DONE = 0;

        static Regex entry_header_matcher = new Regex(@"^\*\* program header [^\[]+ .*\bPF_ARM_ENTRY\b.*", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        static Regex section_matcher = new Regex(@"^\*\* section #\d+ '(\S+)'[^\[]* \[(.+)\]", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        static Regex addr_matcher = new Regex(@"address:\s*(0x[0-9a-f]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        static bool  isWin32 = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        public class section_info
        {
            public string name;
            public string address;
            public string[] flags;
        };

        /**
         * params format:
         *              -d <Arm_tool_folder>
         *              -i <axf_file_path>
         *              -o <output_file_path>
         */
        static int Main(string[] args)
        {
            if (args.Length % 2 != 0)
            {
                error("params format error !");
                return CODE_ERR;
            }

            string arm_tool_folder = "";
            string axf_file_path = "";
            string output_file_path = "";

            for (int i = 0; i < args.Length; i += 2)
            {
                switch (args[i])
                {
                    case "-i":
                        axf_file_path = args[i + 1];
                        break;
                    case "-o":
                        output_file_path = args[i + 1];
                        break;
                    case "-d":
                        arm_tool_folder = args[i + 1];
                        break;
                    default:
                        break;
                }
            }

            string fromelf_path = arm_tool_folder + "\\bin\\fromelf" + (isWin32 ? ".exe" : "");

            int eCode;
            string exe_output;

            // make bin
            string bin_file_name = Path.GetFileNameWithoutExtension(axf_file_path) + "_" + randomStr() + ".bin";
            string bin_file_path = Path.GetTempPath() + Path.DirectorySeparatorChar + bin_file_name;
            eCode = runExe(fromelf_path, $"--bincombined --output \"{bin_file_path}\" \"{axf_file_path}\"", out exe_output);
            if (eCode != CODE_DONE)
            {
                log(exe_output);
                return CODE_ERR;
            }

            // get axf information
            eCode = runExe(fromelf_path, $"-r \"{axf_file_path}\"", out exe_output);
            if (eCode != CODE_DONE)
            {
                log(exe_output);
                return CODE_ERR;
            }

            // === parse section info ===

            string[] log_lines = Regex.Split(exe_output, @"\r\n|\n");

            List<section_info> section_list = new List<section_info>();
            string entry_header_addr = null;

            bool is_matched_section = false;
            bool is_matched_entry_header = false;

            foreach (string line in log_lines)
            {
                if (line.StartsWith("** Program header"))
                {
                    is_matched_entry_header = false;
                    is_matched_section = false;

                    if (entry_header_matcher.IsMatch(line))
                    {
                        is_matched_entry_header = true;
                    }
                }

                if (line.StartsWith("** Section"))
                {
                    is_matched_entry_header = false;
                    is_matched_section = false;

                    Match match_res = section_matcher.Match(line);
                    if (match_res.Success && match_res.Groups.Count > 1)
                    {
                        is_matched_section = true;

                        string section_name = match_res.Groups[1].Value;
                        string[] sec_flags = Regex.Split(match_res.Groups[2].Value, @"\s*\+\s*");

                        section_list.Add(new section_info
                        {
                            name = section_name,
                            flags = sec_flags
                        });
                    }
                }

                if (string.IsNullOrEmpty(line.Trim()))
                {
                    is_matched_entry_header = false;
                    is_matched_section = false;
                }

                // parse section addr data

                if (is_matched_entry_header)
                {
                    Match match_res = addr_matcher.Match(line);
                    if (match_res.Success && match_res.Groups.Count > 1)
                    {
                        if (entry_header_addr != null)
                        {
                            error("parse error !, duplicated 'ARM_ENTRY' header !");
                            return CODE_ERR;
                        }

                        entry_header_addr = match_res.Groups[1].Value.Trim();
                    }
                }
                else if (is_matched_section)
                {
                    Match match_res = addr_matcher.Match(line);
                    if (match_res.Success && match_res.Groups.Count > 1)
                    {
                        if (section_list.Count == 0)
                        {
                            error("parse error !, found section address but section count == 0 !");
                            return CODE_ERR;
                        }

                        section_list[section_list.Count - 1].address = match_res.Groups[1].Value.Trim();
                    }
                }
            }

            // === generate command line ===

            section_info entry_section = null;
            List<string> rm_sec_list = new List<string>();

            foreach (section_info sec_info in section_list)
            {
                if (sec_info.address == entry_header_addr)
                {
                    if (entry_section != null)
                    {
                        error("error !, duplicated entry section: " + entry_section.name + " and " + sec_info.name);
                        return CODE_ERR;
                    }

                    entry_section = sec_info;
                }

                else if (!Array.Exists(sec_info.flags, delegate (string flag) { return flag == "SHF_WRITE"; }))
                {
                    rm_sec_list.Add(sec_info.name);
                }
            }

            if (entry_section == null)
            {
                error("not found entry section !");
                return CODE_ERR;
            }

            string command_params = "--update-section " + entry_section.name + "=\"" + bin_file_path + "\"";

            foreach (string sec_name in rm_sec_list)
            {
                command_params += " --remove-section " + sec_name;
            }

            string command_line = command_params
                + " \"" + axf_file_path + "\""
                + " \"" + output_file_path + "\"";

            // === convert axf to elf ===

            log("arm-none-eabi-objcopy " + command_line + "\r\n");

            eCode = runExe("arm-none-eabi-objcopy", command_line, out string output_log);

            log(output_log);

            // === clean ===
            try
            {
                File.Delete(bin_file_path);
            }
            catch (Exception)
            {
                log($"fail to delete temp file: {bin_file_path}");
            }

            return eCode;
        }

        static string randomStr(int length = 8)
        {
            var crypto = RandomNumberGenerator.Create();
            var bits = length * 6;
            var byte_size = (bits + 7) / 8;
            var bytesarray = new byte[byte_size];
            crypto.GetBytes(bytesarray);
            return Convert.ToBase64String(bytesarray);
        }

        static int runExe(string exePath, string args, out string _output)
        {
            try
            {
                Process process = new Process();
                process.StartInfo.FileName = exePath;
                process.StartInfo.Arguments = args;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.CreateNoWindow = true;
                process.Start();

                StringBuilder output = new StringBuilder();

                process.OutputDataReceived += delegate (object sender, DataReceivedEventArgs e)
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        output.AppendLine(e.Data);
                    }
                };

                process.ErrorDataReceived += delegate (object sender, DataReceivedEventArgs e)
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        output.AppendLine(e.Data);
                    }
                };

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                process.WaitForExit();
                int exitCode = process.ExitCode;
                process.Close();

                _output = output.ToString();

                return exitCode;
            }
            catch (Exception err)
            {
                _output = err.Message;
                return CODE_ERR;
            }
        }

        static void log(string line, bool newLine = true)
        {
            if (newLine)
                Console.WriteLine(line);
            else
                Console.Write(line);
        }

        static void success(string txt, bool newLine = true)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            if (newLine)
                Console.WriteLine(txt);
            else
                Console.Write(txt);
            Console.ResetColor();
        }

        static void info(string txt, bool newLine = true)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            if (newLine)
                Console.WriteLine(txt);
            else
                Console.Write(txt);
            Console.ResetColor();
        }

        static void warn(string txt, bool newLine = true)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            if (newLine)
                Console.WriteLine(txt);
            else
                Console.Write(txt);
            Console.ResetColor();
        }

        static void error(string txt, bool newLine = true)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            if (newLine)
                Console.WriteLine(txt);
            else
                Console.Write(txt);
            Console.ResetColor();
        }
    }
}
