
/* 
 * SPDX identifier
 * 
 * BSD-3-Clause
 * 
 * License text
 * 
 * github0null.io
 * 
 * Redistribution and use in source and binary forms, with or without modification, are permitted provided that the following conditions are met:
 * 
 * 1. Redistributions of source code must retain the above copyright notice, this list of conditions and the following disclaimer.
 *
 * 2. Redistributions in binary form must reproduce the above copyright notice, this list of conditions and the following disclaimer in the documentation and/or other materials provided with the distribution.
 *
 * 3. 'github0null.io' be used to endorse or promote products derived from this software without specific prior written permission.
 * 
 * THIS SOFTWARE IS PROVIDED BY 'github0null.io' "AS IS" AND ANY 'github0null.io' OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, 
 * THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. 
 * IN NO EVENT SHALL github0null.io BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, 
 * OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; 
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, 
 * WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, 
 * EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */


using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using CommandLine;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using System.Text.RegularExpressions;
using Antlr4.Runtime.Misc;
using System.Diagnostics;

namespace c_source_splitter
{
    class OsInfo
    {
        private static OsInfo _instance = null;

        public string OsType { get; }

        public string CRLF { get; }

        private OsInfo()
        {
            OsType = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "win32" : "linux";
            CRLF = OsType == "win32" ? "\r\n" : "\n";
        }

        public static OsInfo instance()
        {
            if (_instance == null)
                _instance = new OsInfo();
            return _instance;
        }
    }

    class RtEncoding
    {
        public int CurrentCodePage { get; }
        public Encoding Default { get; } // 用于本地打印输出的默认字符集（ANSI）
        public Encoding UTF8 { get; }
        public Encoding UTF16 { get; }
        public Encoding UTF16BE { get; }
        public Encoding UTF32BE { get; }

        private static RtEncoding _instance = null;

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern int GetACP();

        private RtEncoding()
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

        public static RtEncoding instance()
        {
            if (_instance == null)
                _instance = new RtEncoding();
            return _instance;
        }
    }

    class Utility
    {
        public delegate TargetType MapCallBk<Type, TargetType>(Type element);

        public static TargetType[] map<Type, TargetType>(IEnumerable<Type> iterator, MapCallBk<Type, TargetType> callBk)
        {
            List<TargetType> res = new List<TargetType>(16);

            foreach (var item in iterator)
            {
                res.Add(callBk(item));
            }

            return res.ToArray();
        }

        public static string toUnixPath(string path)
        {
            return path.Replace('\\', '/');
        }

        public static string toLocalPath(string path, string pathSep = null)
        {
            pathSep = pathSep ?? Path.DirectorySeparatorChar.ToString();

            if (pathSep == "\\")
                return path.Replace("/", pathSep);
            else
                return path.Replace("\\", pathSep);
        }

        public static string formatPath(string path_)
        {
            List<string> pList = new List<string>();

            // delete '.'
            {
                string[] partList = toUnixPath(path_).Split('/');

                foreach (var str in partList)
                {
                    if (str != ".") pList.Add(str);
                }
            }

            return string.Join(Path.DirectorySeparatorChar.ToString(), pList);
        }

        public static bool isAbsolutePath(string path)
        {
            return Path.IsPathRooted(path);
        }

        public static string toRelativePath(string root_, string targetPath_, bool useUnixPath = false)
        {
            string DIR_SEP = (useUnixPath ? '/' : Path.DirectorySeparatorChar).ToString();

            // check null or empty
            if (String.IsNullOrWhiteSpace(root_) ||
                String.IsNullOrWhiteSpace(targetPath_)) return null;

            string root = formatPath(root_);
            string targetPath = formatPath(targetPath_);

            // must be abs path
            if (Path.IsPathRooted(root) == false ||
                Path.IsPathRooted(targetPath) == false) return null;

            // split path to list
            string[] rootList = Utility.toUnixPath(root).Trim('/').Split('/');
            string[] targetList = Utility.toUnixPath(targetPath).Trim('/').Split('/');

            // find common prefix
            int minLen = Math.Min(rootList.Length, targetList.Length);
            int comLen = 0; // common prefix len
            for (; comLen < minLen; comLen++)
            {
                if (rootList[comLen] != targetList[comLen])
                    break;
            }

            // no any common prefix, not have relative path
            if (comLen == 0) return null;

            // ---

            List<string> rePath = new List<string>(256);

            // push parent path '..'
            for (int i = 0; i < rootList.Length - comLen; i++)
                rePath.Add("..");

            // push base path
            for (int i = comLen; i < targetList.Length; i++)
                rePath.Add(targetList[i]);

            return String.Join(DIR_SEP, rePath);
        }
    }

    class Program
    {
        public class Options
        {
            [Option("cwd", Required = true, HelpText = "current work folder")]
            public string WorkFolder { get; set; }

            [Option("outdir", Required = true, HelpText = "output folder")]
            public string OutputDir { get; set; }

            [Option("compiler", Required = true, HelpText = "compiler name")]
            public string CompilerName { get; set; }

            [Option("compiler-args", Required = true, HelpText = "compiler args")]
            public string CompilerArgs { get; set; }

            [Option("compiler-dir", Required = false, HelpText = "compiler root dir")]
            public string CompilerDir { get; set; }

            [Option("program-entry", Required = false, HelpText = "program entry name")]
            public string EntryName { get; set; }

            [Option("test", Required = false, HelpText = "only test source file")]
            public bool OnlyTestSourceFile { get; set; }

            [Value(0, Min = 1, Max = 1, Required = true, HelpText = "source files")]
            public IEnumerable<string> InputSrcFiles { get; set; }
        }

        //
        // global const
        //
        public static readonly int CODE_ERR = 1;
        public static readonly int CODE_DONE = 0;

        static readonly Regex gCsrcFileFilter = new(@"\.c$", RegexOptions.IgnoreCase | RegexOptions.Compiled); // file filters
        static readonly string[] gSupportedToolLi = { "sdcc" }; // supported list

        //
        // global vars
        //
        public static readonly TextWriter StdOut = Console.Out;
        public static readonly TextWriter StdErr = Console.Error;

        public static string objSuffix = ".o";

        //
        // program entry
        //
        public static Options cliOptions;

        public static int Main(string[] args)
        {
            cliOptions = CommandLine.Parser.Default.ParseArguments<Options>(args).Value;
            if (cliOptions == null) return CODE_ERR;

            try
            {
                // set current workspace
                Environment.CurrentDirectory = cliOptions.WorkFolder;

                // check supported toolchain
                if (!gSupportedToolLi.Contains(cliOptions.CompilerName.ToLower()))
                    throw new Exception(string.Format("We not support this toolchain: '{0}'", cliOptions.CompilerName));

                if (cliOptions.CompilerDir != null)
                    Append2SysEnv(cliOptions.CompilerDir);

                if (cliOptions.CompilerArgs.StartsWith("\""))
                    cliOptions.CompilerArgs = cliOptions.CompilerArgs.Trim('"');

                if (cliOptions.EntryName == null)
                    cliOptions.EntryName = "main";

                if (cliOptions.CompilerName == "sdcc")
                    objSuffix = ".rel";

                // handle files
                foreach (var srcFilePath in cliOptions.InputSrcFiles)
                {
                    // preprocess source file
                    var fOutPath = RelocatePath(cliOptions.OutputDir, srcFilePath);
                    Directory.CreateDirectory(Path.GetDirectoryName(fOutPath));
                    var cliArgs = "-E " + cliOptions.CompilerArgs
                        .Replace("${in}", string.Format("\"{0}\"", srcFilePath))
                        .Replace("${out}", string.Format("\"{0}\"", fOutPath));
                    int eCode = Execute(cliOptions.CompilerName, cliArgs, out string allOut, out string stdErr);
                    StdErr.Write(allOut); // pass compiler out -> stderr
                    if (eCode != CODE_DONE) return eCode;

                    // split modules
                    if (!gCsrcFileFilter.IsMatch(fOutPath)) continue;
                    StringWriter sOut = new(), sErr = new();
                    SourceContext result = ParseSourceFile(fOutPath, sOut, sErr);
                    if (cliOptions.OnlyTestSourceFile) continue; // if it's test mode, ignore split
                    if (sErr.GetStringBuilder().Length != 0) throw new Exception("Parser Error: " + sErr.ToString());
                    var outFiles = SplitAndGenerateFiles(result);

                    // compile modules
                    List<string> outLines = new(64);
                    outLines.Add("---> " + srcFilePath);
                    if (outFiles.Length == 0) outFiles = new string[] { fOutPath }; // use origin file
                    var oLi = CompileModuleFiles(cliOptions.CompilerName, cliOptions.CompilerArgs, outFiles);
                    foreach (var p in oLi) outLines.Add(p);

                    // output result
                    foreach (var line in outLines) print(line);
                    File.WriteAllLines(Path.ChangeExtension(fOutPath, ".mods"), outLines);
                }
            }
            catch (Exception err)
            {
                StdErr.Write(err.ToString());
                return CODE_ERR;
            }

            return CODE_DONE;
        }

        private static string RelocatePath(string outRootDir, string path)
        {
            List<string> pList = new(Utility.toUnixPath(path).Split('/'));

            for (int idx = 0; idx < pList.Count; idx++)
            {
                if (pList[idx] == "..") pList[idx] = "__";
                if (pList[idx].EndsWith(":")) pList[idx] = pList[idx].Substring(0, pList[idx].Length - 1);
            }

            pList.Insert(0, outRootDir);

            pList.RemoveAll(s => s == string.Empty);

            return string.Join(Path.DirectorySeparatorChar, pList);
        }

        private static string[] CompileModuleFiles(string ccName, string ccArgs, string[] files)
        {
            List<string> outFiles = new(64);

            foreach (var path in files)
            {
                var fin = path;
                var fou = Path.ChangeExtension(path, objSuffix);
                var cArgs = ccArgs.Replace("${in}", "\"" + fin + "\"")
                    .Replace("${out}", "\"" + fou + "\"");

                var exitCode = Execute(ccName, cArgs, out string out_, out string __);
                StdErr.Write(out_); // pass compiler out -> stderr
                if (exitCode != CODE_DONE) throw new Exception("compile error at: " + fin);
                outFiles.Add(fou);
            }

            return outFiles.ToArray();
        }

        private struct SymbolReferenceItem
        {
            public string uid;
            public SourceSymbol[] refs;
        };

        private static string[] SplitAndGenerateFiles(SourceContext ctx)
        {
            string baseName = Path.GetFileNameWithoutExtension(ctx.SrcFilePath);
            string extName = Path.GetExtension(ctx.SrcFilePath) ?? "";
            string baseDir = Path.GetDirectoryName(ctx.SrcFilePath);
            if (string.IsNullOrWhiteSpace(baseDir)) baseDir = ".";

            string outDirPath = string.Join(Path.DirectorySeparatorChar, new string[] {
                baseDir, ".modules", baseName + extName
            });

            // entry name not need declare, add it
            ctx.FunctionDeclares.Add(new SourceSymbol {
                name = cliOptions.EntryName,
                symType = SourceSymbol.SymbolType.Function
            });

            // funcs
            uint NextFileId = 0;
            var ObtainFileName = (bool noId) => {
                var cuID = NextFileId++;
                if (noId) return baseName + extName;
                return baseName + "_" + cuID.ToString() + extName;
            };

            // generate static reference chain
            Dictionary<string, SourceSymbol[]> refLink = new(256);
            foreach (var curSym in ctx.Symbols)
            {
                List<SourceSymbol> curRefs = new(64);
                List<SourceSymbol> refSyms = new(64);

                foreach (var refName in curSym.refers.Distinct())
                {
                    if (curSym.name == refName)
                        continue; // skip loop self ref

                    refSyms.AddRange(ctx.FindSymbol(refName));
                }

                Stack<SourceSymbol> symStk = new(refSyms);

                while (symStk.Count > 0)
                {
                    var s = symStk.Pop();

                    if (!s.IsStatic)
                        continue; // we only need to handle static reference

                    curRefs.Add(s);

                    foreach (var n in s.refers)
                    {
                        if (s.name == n)
                            continue; // skip loop self ref

                        foreach (var item in ctx.FindSymbol(n))
                        {
                            symStk.Push(item);
                        }
                    }
                }

                refLink.Add(curSym.UID, curRefs.Distinct().ToArray());
            }

            // sort
            List<SymbolReferenceItem> unresolvedLi = new(64);
            foreach (var kv in refLink)
            {
                unresolvedLi.Add(new SymbolReferenceItem {
                    uid = kv.Key,
                    refs = kv.Value
                });
            }
            unresolvedLi.Sort((a, b) => b.refs.Length - a.refs.Length);

            // split all sym
            List<List<SourceSymbol>> symGrps = new(128);

            Func<SourceSymbol, int> FindResolvedGrpIdx = delegate (SourceSymbol sym) {
                return symGrps.FindIndex((symLi) => symLi.Contains(sym));
            };

            foreach (var symInfo in unresolvedLi)
            {
                var rootSym = ctx.GetSymbol(symInfo.uid);

                if (FindResolvedGrpIdx(rootSym) != -1)
                    continue;

                if (rootSym.symType == SourceSymbol.SymbolType.Variable &&
                    rootSym.IsStatic == false)
                    continue; // ignore global variables, it's extern

                List<SourceSymbol> curSyms = new();

                // add root
                curSyms.Add(rootSym);

                int expectedGrpIdx = -1;

                // add refs
                foreach (var refedSym in symInfo.refs)
                {
                    var conflictIdx = FindResolvedGrpIdx(refedSym);

                    // If have cross references, merge group
                    // In general, this branch is unreachable
                    if (conflictIdx != -1 && expectedGrpIdx != -1 &&
                        conflictIdx != expectedGrpIdx)
                    {
                        var n = symGrps[conflictIdx].Union(symGrps[expectedGrpIdx]).ToList();
                        symGrps = symGrps
                            .Where((li, idx) => { return idx != conflictIdx && idx != expectedGrpIdx; })
                            .ToList();
                        expectedGrpIdx = conflictIdx = -1;
                        curSyms = n.Union(curSyms).ToList();
                    }

                    if (conflictIdx != -1)
                        expectedGrpIdx = conflictIdx;

                    curSyms.Add(refedSym);
                }

                if (expectedGrpIdx != -1)
                    symGrps[expectedGrpIdx].AddRange(curSyms);
                else
                    symGrps.Add(curSyms);
            }

            // del repeat
            for (int i = 0; i < symGrps.Count; i++)
                symGrps[i] = symGrps[i].Distinct().ToList();

            // create dir
            Directory.CreateDirectory(outDirPath);

            // generate files
            string[] srcRawLines = File.ReadAllLines(ctx.SrcFilePath);
            SourceSymbol[] globalVars = ctx.Symbols
                .Where(sym => sym.symType != SourceSymbol.SymbolType.Function && !sym.IsStatic && !sym.IsExtern)
                .ToArray();

            var DisableSymbolFromTxtLines = delegate (StringBuilder[] lines_, SourceSymbol sym_) {

                if (sym_.startLocation.Line == sym_.stopLocation.Line) // for single line symbol
                {
                    var linIdx = sym_.stopLocation.Line;
                    var len = (sym_.stopLocation.Column - sym_.startLocation.Column) + 1;
                    lines_[linIdx - 1].Remove(sym_.startLocation.Column, len);
                    lines_[linIdx - 1].Insert(sym_.startLocation.Column, "".PadRight(len));
                }
                else // for multi-line symbol
                {
                    for (int i = sym_.startLocation.Line - 1; i < sym_.stopLocation.Line; i++)
                    {
                        if (i == sym_.startLocation.Line - 1)
                        {
                            var remainLen = lines_[i].Length - sym_.startLocation.Column;
                            lines_[i].Remove(sym_.startLocation.Column, remainLen);
                            lines_[i].Insert(sym_.startLocation.Column, "".PadRight(remainLen));
                        }
                        else if (i == sym_.stopLocation.Line - 1)
                        {
                            lines_[i].Remove(0, sym_.stopLocation.Column + 1);
                            lines_[i].Insert(0, "".PadRight(sym_.stopLocation.Column + 1));
                        }
                        else
                        {
                            lines_[i].Remove(0, lines_[i].Length);
                        }
                    }
                }
            };

            List<string> mFiles = new(64);

            foreach (var srcSyms in symGrps)
            {
                StringBuilder[] srcLines = srcRawLines.Select(l => new StringBuilder(l)).ToArray();

                // alloc out file name

                bool isEntryModule = srcSyms.Any(s => {
                    return s.symType == SourceSymbol.SymbolType.Function && s.name == cliOptions.EntryName;
                });

                bool IsInModule_0 = NextFileId == 0;
                string srcFileName = outDirPath + Path.DirectorySeparatorChar + ObtainFileName(isEntryModule);

                // process share decl var symbols

                foreach (var symGrp in ctx.ShareDeclareSymbols)
                {
                    if (symGrp[0].IsStatic)
                    {
                        var isBannedSomeone = symGrp.Any(s => !srcSyms.Contains(s));

                        if (isBannedSomeone) // if some sym is disabled, we need split them
                        {
                            var aliveSyms = symGrp.Where(s => srcSyms.Contains(s)).ToArray();

                            if (aliveSyms.Length > 0)
                            {
                                string nDeclLine = "";

                                if (symGrp[0].IsInlineStructOrEnumDecl)
                                {
                                    nDeclLine = symGrp[0].declareSpec + ";\n";
                                }

                                foreach (var sym_ in aliveSyms)
                                {
                                    nDeclLine += sym_.declareSpecWithNoBlock + " " + (sym_.declFullName ?? sym_.name);
                                    if (sym_.initializer != null) nDeclLine += "=" + sym_.initializer;
                                    nDeclLine += ";\n";
                                }

                                var stopLocation = symGrp[0].stopLocation;
                                srcLines[stopLocation.Line - 1].Insert(stopLocation.Column + 1, nDeclLine);
                            }

                            DisableSymbolFromTxtLines(srcLines, symGrp[0]);
                        }
                    }

                    else
                    {
                        // in module[0] file
                        if (IsInModule_0)
                        {
                            // nothing need to do
                        }

                        // in module[1..n] files
                        else
                        {
                            string nDeclLine = "";

                            if (symGrp[0].IsInlineStructOrEnumDecl)
                            {
                                nDeclLine = symGrp[0].declareSpec + ";\n";
                            }

                            foreach (var sym_ in symGrp)
                            {
                                nDeclLine += "extern " + sym_.declareSpecWithNoBlock + " " + (sym_.declFullName ?? sym_.name) + ";\n";
                            }

                            var stopLocation = symGrp[0].stopLocation;
                            srcLines[stopLocation.Line - 1].Insert(stopLocation.Column + 1, nDeclLine);

                            DisableSymbolFromTxtLines(srcLines, symGrp[0]);
                        }
                    }
                }

                // process normal symbols

                var bannedSyms = ctx.Symbols.Where(s => !srcSyms.Contains(s) && !ctx.IsShareDeclareSymbol(s.LocationID));

                foreach (var sym in bannedSyms)
                {
                    if (sym.symType == SourceSymbol.SymbolType.Variable)
                    {
                        if (sym.IsStatic)
                        {
                            DisableSymbolFromTxtLines(srcLines, sym);
                        }
                        else
                        {
                            if (IsInModule_0)
                            {
                                continue; // store global var or extern var
                            }
                            else
                            {
                                string nDeclareTxt = "";

                                if (sym.IsInlineStructOrEnumDecl)
                                {
                                    nDeclareTxt = sym.declareSpec + ";\n";
                                }

                                nDeclareTxt = "extern " + sym.declareSpecWithNoBlock + " " + (sym.declFullName ?? sym.name) + ";";
                                srcLines[sym.stopLocation.Line - 1].Insert(sym.stopLocation.Column + 1, nDeclareTxt);
                                DisableSymbolFromTxtLines(srcLines, sym);
                            }
                        }
                    }

                    else // function
                    {
                        DisableSymbolFromTxtLines(srcLines, sym);

                        // if not found declare, add it
                        // ignore static function
                        if (!sym.IsStatic &&
                            !ctx.FunctionDeclares.Any(s => s.name == sym.name))
                        {
                            string txt = sym.declareSpec + ";";
                            srcLines[sym.startLocation.Line - 1].Insert(sym.startLocation.Column, txt);
                        }
                    }
                }

                // gen files

                var wLines = srcLines.Select(sl => sl.ToString()).ToArray();
                File.WriteAllLines(srcFileName, wLines);

                mFiles.Add(srcFileName);
            }

            return mFiles.ToArray();
        }

        private static SourceContext ParseSourceFile(string srcPath, in StringWriter stdOut, in StringWriter stdErr)
        {
            ICharStream input = CharStreams.fromStream(new FileStream(srcPath, FileMode.Open, FileAccess.Read));

            CodeListener cListener = new(srcPath, input);
            SdccLexer lexer = new(input);
            CommonTokenStream tokens = new(lexer);
            SdccParser parser = new(tokens, stdOut, stdErr);

            lexer.AddErrorListener(cListener);
            parser.AddErrorListener(cListener);
            parser.AddParseListener(cListener);

            parser.ErrorHandler = new BailErrorStrategy();
            parser.BuildParseTree = true;

            var ctx = parser.compilationUnit();
            if (ctx.exception != null) throw ctx.exception;

            return cListener.SourceContext;
        }

        public static int Execute(string filename, string args,
            out string allout, out string stderr, Encoding encoding = null)
        {
            // if executable is 'cmd.exe', force use ascii
            if (filename == "cmd" ||
                filename == "cmd.exe")
            {
                encoding = RtEncoding.instance().Default;
            }

            Process process = new Process();
            process.StartInfo.FileName = filename;
            process.StartInfo.Arguments = args;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.StandardOutputEncoding = encoding ?? RtEncoding.instance().Default;
            process.StartInfo.StandardErrorEncoding = encoding ?? RtEncoding.instance().Default;
            process.Start();

            StringBuilder _out = new StringBuilder();
            StringBuilder _err = new StringBuilder();

            process.OutputDataReceived += delegate (object sender, DataReceivedEventArgs e) {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    lock (_out)
                    {
                        _out.AppendLine(e.Data);
                    }
                }
            };

            process.ErrorDataReceived += delegate (object sender, DataReceivedEventArgs e) {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    _err.AppendLine(e.Data);

                    lock (_out)
                    {
                        _out.AppendLine(e.Data);
                    }
                }
            };

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            process.WaitForExit();
            int exitCode = process.ExitCode;
            process.Close();

            allout = _out.ToString();
            stderr = _err.ToString();

            return exitCode;
        }

        public static void Append2SysEnv(string value, string keyName = null)
        {
            if (keyName == null || keyName.ToLower() == "path")
            {
                var sysPathName = OsInfo.instance().OsType == "win32" ? "Path" : "PATH";

                string val = Environment.GetEnvironmentVariable(sysPathName);

                if (val != null) // found path, append it
                    Environment.SetEnvironmentVariable(sysPathName, value + Path.PathSeparator + val);
                else // not found, set it
                    Environment.SetEnvironmentVariable(sysPathName, value);

                return;
            }

            Environment.SetEnvironmentVariable(keyName, value);
        }

        public static void print(string line, bool newLine = true)
        {
            if (newLine) Console.WriteLine(line);
            else Console.Write(line);
        }
    }

    //////////////////////////////////////////////////////////////////
    //  parser
    //////////////////////////////////////////////////////////////////

    class SourceSymbol
    {
        public enum SymbolType
        {
            Variable,
            Function,
        }

        public static string TYPE_NAME_UNKOWN = "<unkown-type>";

        // ---

        public string name = ""; // identifier name, like: 'arr'
        public SymbolType symType = SymbolType.Variable;
        public string typeName = TYPE_NAME_UNKOWN;
        public string[] attrs = Array.Empty<string>();
        public string[] refers = Array.Empty<string>();

        // for 'variable', it's type preffix, like: 'static const uint8'.
        // for 'function', it's full declare txt, like 'static void foo (int a, int b) __ram()'.
        public string declareSpec = "";

        public string initializer = null;  // variable initializer, like: '= 15', '= { 12, 3 }' ...
        public string declFullName = null; // variable define full name, like: 'arr[128]'

        public IToken startLocation = null;
        public IToken stopLocation = null;

        public string declareSpecWithNoBlock
        {
            get {
                return Regex.Replace(declareSpec, @"\{.*\}", "");
            }
        }

        public bool IsStatic
        {
            get {
                return attrs.Contains("static");
            }
        }

        public bool IsExtern
        {
            get {
                return attrs.Contains("extern");
            }
        }

        public bool IsInlineStructOrEnumDecl
        {
            get {
                return (declareSpec.Contains("{") && declareSpec.Contains("}")) &&
                       (declareSpec.Contains("struct") || declareSpec.Contains("union") ||
                        declareSpec.Contains("enum"));
            }
        }

        public string UID
        {
            get {
                if (startLocation == null || stopLocation == null) return null;
                return string.Format("{0}-{1}-{2}-{3}",
                    name, symType.ToString().ToLower(),
                    startLocation.TokenIndex, stopLocation.TokenIndex);
            }
        }

        public string LocationID
        {
            get {
                if (startLocation == null || stopLocation == null) return null;
                return string.Format("{0}-{1}", startLocation.TokenIndex, stopLocation.TokenIndex);
            }
        }

        public override bool Equals(object obj)
        {
            if (obj is SourceSymbol sym)
            {
                return sym.name == name &&
                       sym.symType == symType &&
                       sym.typeName == typeName;
            }

            return false;
        }

        public override int GetHashCode()
        {
            return UID.GetHashCode();
        }
    }

    class SourceContext
    {
        public string SrcFilePath;
        public ICharStream SrcFileStream;

        private readonly Dictionary<string, SourceSymbol> _symbols = new(256);
        private readonly Dictionary<string, List<SourceSymbol>> _share_declare_symbols = new(64);
        private readonly List<SourceSymbol> _functionDeclares = new(64);

        // attrs
        public IEnumerable<SourceSymbol> Symbols { get { return _symbols.Values; } }
        public Dictionary<string, SourceSymbol> RawSymbolTable { get { return _symbols; } }
        public IEnumerable<List<SourceSymbol>> ShareDeclareSymbols { get { return _share_declare_symbols.Values; } }
        public List<SourceSymbol> FunctionDeclares { get { return _functionDeclares; } }

        public SourceContext(string SrcFilePath, ICharStream SrcFileStream)
        {
            this.SrcFilePath = SrcFilePath;
            this.SrcFileStream = SrcFileStream;
        }

        public void AddSymbol(SourceSymbol sym)
        {
            var uid = sym.UID;

            if (!_symbols.ContainsKey(uid))
            {
                _symbols.Add(uid, sym);
            }
        }

        public void AddRangeSymbol(IEnumerable<SourceSymbol> syms)
        {
            foreach (var symbol in syms)
            {
                var uid = symbol.UID;

                if (!_symbols.ContainsKey(uid))
                {
                    _symbols.Add(uid, symbol);
                }
            }
        }

        public void AddShareDeclareSymbols(IEnumerable<SourceSymbol> syms)
        {
            var sym = syms.First();

            if (sym == null) return;

            AddRangeSymbol(syms);

            if (_share_declare_symbols.ContainsKey(sym.LocationID))
                _share_declare_symbols[sym.LocationID].AddRange(syms);
            else
                _share_declare_symbols.Add(sym.LocationID, new List<SourceSymbol>(syms));
        }

        public IEnumerable<SourceSymbol> FindSymbol(string symName)
        {
            return from kv in _symbols
                   where kv.Value.name == symName
                   select kv.Value;
        }

        public SourceSymbol GetSymbol(string uid)
        {
            if (!_symbols.ContainsKey(uid))
                return null;

            return _symbols[uid];
        }

        public SourceSymbol[] GetShareDeclareSymbols(string locationID)
        {
            if (!_share_declare_symbols.ContainsKey(locationID))
                return null;

            return _share_declare_symbols[locationID].ToArray();
        }

        public bool IsShareDeclareSymbol(string locationID)
        {
            return _share_declare_symbols.ContainsKey(locationID);
        }
    }

    class CodeParserException : Exception
    {
        public CodeParserException(string msg) : base(msg)
        {
        }
    }

    class CodeListener : SdccBaseListener, IAntlrErrorListener<int>, IAntlrErrorListener<IToken>
    {
        private readonly SourceContext _src_ctx;

        public CodeListener(string srcFileName, ICharStream input)
        {
            _src_ctx = new(srcFileName, input);
            stack.Push(ParserStatus.InGlobal);
        }

        public SourceContext SourceContext
        {
            get {
                return _src_ctx;
            }
        }

        //
        // private utils func
        //

        private static string GetParseNodeFullText<T>(ICharStream input, T ctx) where T : ParserRuleContext
        {
            return input.GetText(new Interval(ctx.Start.StartIndex, ctx.Stop.StopIndex));
        }

        private SourceSymbol[] ParseVariableDeclare([NotNull] SdccParser.DeclarationContext context)
        {
            string vTypeName = null;

            List<string> vAttrList = new();
            Dictionary<string, string> vNameMap = new();
            Dictionary<string, List<string>> vRefsMap = new();
            Dictionary<string, string> initializerMap = new();

            // ---

            var declSpec = context.declarationSpecifiers();
            if (declSpec == null)
                return Array.Empty<SourceSymbol>(); // skip other declare type

            //
            // ANTLR4 Grammar 
            //
            // declarationSpecifier
            //  :   storageClassSpecifier
            //  |   typeSpecifier
            //  |   typeQualifier
            //  |   functionSpecifier
            //  |   alignmentSpecifier
            //  ;

            var allChildren = declSpec.declarationSpecifier().Select(c => (ParserRuleContext)c.GetChild(0));

            // parse all specs

            var sClasSpecs = allChildren.Where(c => c is SdccParser.StorageClassSpecifierContext).ToArray();

            foreach (var item in sClasSpecs)
            {
                var nodeTxt = item.GetText();

                if (nodeTxt == "typedef")
                    return Array.Empty<SourceSymbol>(); // skip typedef

                vAttrList.Add(nodeTxt);
            }

            var typeSpecs = allChildren.Where(c => c is SdccParser.TypeSpecifierContext).ToArray();

            if (typeSpecs.Length == 0)
                return Array.Empty<SourceSymbol>(); // not found type, skip

            foreach (var typeSpecCtx in typeSpecs)
            {
                var typeCtx = typeSpecCtx.GetChild(0);

                if (typeCtx is ITerminalNode ||
                    typeCtx is SdccParser.AtomicTypeSpecifierContext)
                {
                    vTypeName = GetParseNodeFullText(SourceContext.SrcFileStream, typeSpecCtx);
                }

                else if (typeCtx is SdccParser.StructOrUnionSpecifierContext _struct_ctx)
                {
                    vTypeName = _struct_ctx.structOrUnion().GetText() + " " +
                                _struct_ctx.Identifier().GetText();
                }

                else if (typeCtx is SdccParser.EnumSpecifierContext _enum_ctx)
                {
                    vTypeName = _enum_ctx.Enum().GetText() + " " +
                                _enum_ctx.Identifier().GetText();
                }

                else if (typeCtx is SdccParser.TypedefNameContext)
                {
                    if (vTypeName == null)
                    {
                        vTypeName = GetParseNodeFullText(SourceContext.SrcFileStream, typeSpecCtx);
                    }
                    else // if type name is existed, this is a varName
                    {
                        var varName = GetParseNodeFullText(SourceContext.SrcFileStream, typeSpecCtx);
                        vNameMap.Add(varName, varName);
                    }
                }
            }

            if (vTypeName == null)
                return Array.Empty<SourceSymbol>(); // it's not a var declare, skip

            var typeQualSpecs = allChildren.Where(c => c is SdccParser.TypeQualifierContext).ToArray();

            foreach (var item in typeQualSpecs)
            {
                vAttrList.Add(item.GetText());
            }

            var attrSpecs = allChildren.Where(c => c is SdccParser.FunctionSpecifierContext).ToArray();

            foreach (var item in attrSpecs)
            {
                vAttrList.Add(GetParseNodeFullText(SourceContext.SrcFileStream, item));
            }

            var declSpecTxt = GetParseNodeFullText(SourceContext.SrcFileStream, declSpec);

            var initDeclCtx = context.initDeclaratorList();

            if ((initDeclCtx == null && vNameMap.Count == 0) ||
                (initDeclCtx != null && vNameMap.Count != 0))
                return Array.Empty<SourceSymbol>(); // it's not a var declare, skip

            if (initDeclCtx != null)
            {
                foreach (var declCtx in initDeclCtx.initDeclarator())
                {
                    var decl = declCtx.declarator().directDeclarator();

                    // it is a func declare ?
                    if (decl.ChildCount > 2)
                    {
                        // ANTLR4 grammar:
                        //
                        // directDeclarator:
                        //     |   directDeclarator '(' parameterTypeList ')'
                        //     |   directDeclarator '(' identifierList ? ')'
                        //

                        var child_0 = decl.GetChild(0);
                        var child_1 = decl.GetChild(1);

                        if (child_0 is SdccParser.DirectDeclaratorContext dirDeclarCtx &&
                            child_1 is ITerminalNode node &&
                            node.Symbol.Type == SdccParser.LeftParen)
                        {
                            var funcName = GetIdentifierFromDirectDeclarator(dirDeclarCtx).GetText();

                            SourceSymbol sym = new() {
                                name = funcName,
                                symType = SourceSymbol.SymbolType.Function,
                                startLocation = context.Start,
                                stopLocation = context.Stop,
                            };

                            return new SourceSymbol[] { sym };
                        }
                    }

                    var vIdentifier = GetIdentifierFromDirectDeclarator(decl).GetText();
                    var vFullDeclName = GetParseNodeFullText(SourceContext.SrcFileStream, decl);

                    vNameMap.TryAdd(vIdentifier, vFullDeclName);
                    vRefsMap.TryAdd(vIdentifier, new());

                    var initializerCtx = declCtx.initializer();

                    if (initializerCtx != null) // parse var references
                    {
                        var baseExprLi = FindChild<SdccParser.PrimaryExpressionContext>(initializerCtx);

                        foreach (var exprCtx in baseExprLi)
                        {
                            var idf = exprCtx.Identifier();

                            if (idf != null)
                            {
                                vRefsMap[vIdentifier].Add(idf.GetText());
                            }
                        }

                        initializerMap.TryAdd(vIdentifier,
                            GetParseNodeFullText(SourceContext.SrcFileStream, initializerCtx));
                    }
                }
            }
            else // typeName is var name, remove it from type decl text
            {
                foreach (var KV in vNameMap)
                {
                    declSpecTxt = declSpecTxt.Replace(KV.Key, "").TrimEnd();
                }
            }

            List<SourceSymbol> symList = new();

            foreach (var KV in vNameMap)
            {
                var name = KV.Key;
                var declName = KV.Value;

                var nSym = new SourceSymbol {
                    name = name,
                    declFullName = declName,
                    symType = SourceSymbol.SymbolType.Variable,
                    declareSpec = declSpecTxt,
                    typeName = vTypeName,
                    attrs = vAttrList.ToArray(),
                    startLocation = context.Start,
                    stopLocation = context.Stop
                };

                if (vRefsMap.ContainsKey(name))
                {
                    nSym.refers = vRefsMap[name].ToArray();
                }

                if (initializerMap.ContainsKey(name))
                {
                    nSym.initializer = initializerMap[name];
                }

                symList.Add(nSym);
            }

            return symList.ToArray();
        }

        private static T[] FindChild<T>(ParserRuleContext rootCtx) where T : ParserRuleContext
        {
            List<T> result = new();

            Queue<ParserRuleContext> ctxQueue = new(64);

            ctxQueue.Enqueue(rootCtx);

            while (ctxQueue.Count > 0)
            {
                var ctx = ctxQueue.Dequeue();

                if (ctx is T t)
                {
                    result.Add(t);
                }

                if (ctx.ChildCount > 0)
                {
                    foreach (var child in ctx.children)
                    {
                        if (child is ParserRuleContext c)
                        {
                            ctxQueue.Enqueue(c);
                        }
                    }
                }
            }

            return result.ToArray();
        }

        private delegate bool RuleContextChildWalker<T>(T ctx) where T : ParserRuleContext;

        private static void WalkChild<T>(ParserRuleContext rootCtx, RuleContextChildWalker<T> walker) where T : ParserRuleContext
        {
            Queue<ParserRuleContext> ctxQueue = new(64);

            foreach (var child in rootCtx.children)
            {
                if (child is ParserRuleContext c)
                {
                    ctxQueue.Enqueue(c);
                }
            }

            while (ctxQueue.Count > 0)
            {
                var ctx = ctxQueue.Dequeue();

                if (ctx is T t)
                {
                    if (walker(t))
                        return;
                }

                if (ctx.ChildCount > 0)
                {
                    foreach (var child in ctx.children)
                    {
                        if (child is ParserRuleContext c)
                        {
                            ctxQueue.Enqueue(c);
                        }
                    }
                }
            }
        }

        private static ITerminalNode GetIdentifierFromDirectDeclarator(SdccParser.DirectDeclaratorContext ctx)
        {
            if (ctx.Identifier() != null)
            {
                return ctx.Identifier();
            }

            var declCtx = ctx.declarator();

            if (declCtx != null)
            {
                return GetIdentifierFromDirectDeclarator(declCtx.directDeclarator());
            }

            var directDeclCtx = ctx.directDeclarator();

            if (directDeclCtx != null)
            {
                return GetIdentifierFromDirectDeclarator(directDeclCtx);
            }

            return null;
        }

        private static bool IsTokenAhead(IToken a, IToken b)
        {
            return a.Line < b.Line || (a.Line == b.Line && a.Column < b.Column);
        }

        //
        // interval vars
        //

        enum ParserStatus
        {
            InGlobal,
            InFunctionDefine,
            InDeclaration,
            InStatement
        }

        private Stack<ParserStatus> stack = new(10);

        public override void EnterDeclaration([NotNull] SdccParser.DeclarationContext context)
        {
            stack.Push(ParserStatus.InDeclaration);
        }

        public override void ExitDeclaration([NotNull] SdccParser.DeclarationContext context)
        {
            if (stack.Pop() != ParserStatus.InDeclaration)
            {
                throw new CodeParserException("Internal State Error");
            }

            // parse global vars
            if (stack.Peek() == ParserStatus.InGlobal)
            {
                var symDecls = ParseVariableDeclare(context);

                var varLi = symDecls.Where(s => s.symType == SourceSymbol.SymbolType.Variable).ToArray();

                if (varLi.Length > 0)
                {
                    SourceContext.AddRangeSymbol(varLi);

                    if (varLi.Length > 1)
                    {
                        SourceContext.AddShareDeclareSymbols(varLi);
                    }
                }

                var fucLi = symDecls.Where(s => s.symType == SourceSymbol.SymbolType.Function).ToArray();

                if (fucLi.Length > 0)
                {
                    SourceContext.FunctionDeclares.AddRange(fucLi);
                }
            }
        }

        public override void EnterStatement([NotNull] SdccParser.StatementContext context)
        {
            stack.Push(ParserStatus.InStatement);
        }

        public override void ExitStatement([NotNull] SdccParser.StatementContext context)
        {
            if (stack.Pop() != ParserStatus.InStatement)
            {
                throw new CodeParserException("Internal State Error");
            }
        }

        public override void EnterFunctionDefinition([NotNull] SdccParser.FunctionDefinitionContext context)
        {
            stack.Push(ParserStatus.InFunctionDefine);
        }

        public override void ExitFunctionDefinition([NotNull] SdccParser.FunctionDefinitionContext context)
        {
            if (stack.Pop() != ParserStatus.InFunctionDefine)
            {
                throw new CodeParserException("Internal State Error");
            }

            //
            // functionDefinition
            //  :   declarationSpecifiers? declarator declarationList? compoundStatement
            //  ;
            //

            string vFuncName = null;

            List<string> vAttrList = new();
            List<string> vRefeList = new();

            string declarationSpecifiersFullTxt = null;
            string declaratorFullTxt = null;

            // function local vars
            List<SourceSymbol> localVars = new();

            // get name and params list
            {
                //
                // ANTLR4 grammar for function declare
                //
                // DirectDeclarator:
                // ...
                //  |    directDeclarator '(' parameterTypeList ')'
                //  |    directDeclarator '(' identifierList? ')'
                //

                var declaratorCtx = context.declarator();
                var directDeclCtx = declaratorCtx.directDeclarator();

                if (directDeclCtx.ChildCount < 3)
                    return; // not a func define ??

                var child_0 = directDeclCtx.GetChild(0);
                var child_1 = directDeclCtx.GetChild(1);

                // check func decl, like: '(foo) (int a, ...)'
                if (child_0 is SdccParser.DirectDeclaratorContext funcNameDeclCtx &&
                    child_1 is ITerminalNode t && t.Symbol.Type == SdccParser.LeftParen)
                {
                    vFuncName = GetIdentifierFromDirectDeclarator(funcNameDeclCtx).GetText();
                }

                // not a func define. exit
                if (vFuncName == null)
                    return;

                // get full delare txt
                declaratorFullTxt = GetParseNodeFullText(SourceContext.SrcFileStream, declaratorCtx);

                // parse params list
                var paramsTypeLiCtx = directDeclCtx.parameterTypeList();
                if (paramsTypeLiCtx != null)
                {
                    // ANTLR4 Grammar
                    //
                    //parameterTypeList
                    //    :   parameterList (',' '...')?
                    //    ;
                    //
                    //parameterList
                    //    :   parameterDeclaration (',' parameterDeclaration)*
                    //    ;
                    //
                    //parameterDeclaration
                    //    :   declarationSpecifiers declarator
                    //    |   declarationSpecifiers2 abstractDeclarator?
                    //    ;
                    //
                    foreach (var paramDeclCtx in paramsTypeLiCtx.parameterList().parameterDeclaration())
                    {
                        if (paramDeclCtx.GetChild(1) is SdccParser.DeclaratorContext declCtx)
                        {
                            WalkChild(declCtx, delegate (SdccParser.DirectDeclaratorContext directDeclItem) {

                                var ch = directDeclItem.GetChild(0);

                                if (ch is ITerminalNode node &&
                                    node.Symbol.Type == SdccParser.Identifier)
                                {
                                    localVars.Add(new SourceSymbol {
                                        name = node.GetText(),
                                        symType = SourceSymbol.SymbolType.Variable,
                                        startLocation = directDeclItem.Start,
                                        stopLocation = directDeclItem.Stop
                                    });

                                    // if found a identify name in 
                                    // parameterDeclaration, exit walker
                                    return true;
                                }

                                return false;
                            });
                        }
                    }
                }
            }

            // get specifiers
            {
                var declSpec = context.declarationSpecifiers();

                if (declSpec != null)
                {
                    foreach (var storeClasCtx in FindChild<SdccParser.StorageClassSpecifierContext>(declSpec))
                    {
                        vAttrList.Add(storeClasCtx.GetText());
                    }

                    foreach (var funcSpecCtx in FindChild<SdccParser.FunctionSpecifierContext>(declSpec))
                    {
                        vAttrList.Add(funcSpecCtx.GetText());
                    }
                }

                declarationSpecifiersFullTxt = GetParseNodeFullText(SourceContext.SrcFileStream, declSpec);
            }

            // parse ref
            {
                var funcBlockCtx = context.compoundStatement().blockItemList();

                if (funcBlockCtx != null)
                {
                    foreach (var blockItemCtx in funcBlockCtx.blockItem())
                    {
                        var curCtx = blockItemCtx.GetChild(0);

                        if (curCtx is SdccParser.StatementContext smtCtx)
                        {
                            foreach (var postFixExprCtx in FindChild<SdccParser.PostfixExpressionContext>(smtCtx))
                            {
                                WalkChild(postFixExprCtx, delegate (SdccParser.PrimaryExpressionContext baseExpr) {

                                    var idfCtx = baseExpr.GetChild(0);

                                    if (idfCtx is ITerminalNode node &&
                                        node.Symbol.Type == SdccParser.Identifier)
                                    {
                                        vRefeList.Add(node.GetText());
                                    }

                                    return false;
                                });
                            }
                        }

                        else if (curCtx is SdccParser.DeclarationContext declCtx)
                        {
                            localVars.AddRange(ParseVariableDeclare(declCtx)
                                .Where(s => s.symType == SourceSymbol.SymbolType.Variable));
                        }
                    }

                    // add initializer's refs
                    foreach (var sym in localVars)
                    {
                        vRefeList.AddRange(sym.refers);
                    }

                    // del repeat items
                    vRefeList = vRefeList.Distinct().ToList();

                    // del local var refs
                    vRefeList.RemoveAll((name) => {

                        var v = localVars.Find((sym) => {
                            return sym.name == name;
                        });

                        return v != null;
                    });
                }
            }

            string funcDeclFullTxt = "";

            // combine decl txt
            {
                if (declarationSpecifiersFullTxt != null)
                    funcDeclFullTxt += declarationSpecifiersFullTxt;

                funcDeclFullTxt += " " + declaratorFullTxt;
            }

            // add to func li
            SourceContext.AddSymbol(new SourceSymbol {
                name = vFuncName,
                symType = SourceSymbol.SymbolType.Function,
                typeName = SourceSymbol.TYPE_NAME_UNKOWN,
                declareSpec = funcDeclFullTxt,
                attrs = vAttrList.ToArray(),
                refers = vRefeList.ToArray(),
                startLocation = context.Start,
                stopLocation = context.Stop
            });
        }

        public void SyntaxError(TextWriter output, IRecognizer recognizer, int offendingSymbol, int line, int charPositionInLine, string msg, RecognitionException e)
        {
            throw new CodeParserException(string.Format("\"{0}\":{1},{2}: LexerError: {3}", SourceContext.SrcFilePath, line, charPositionInLine, msg));
        }

        public void SyntaxError(TextWriter output, IRecognizer recognizer, IToken offendingSymbol, int line, int charPositionInLine, string msg, RecognitionException e)
        {
            throw new CodeParserException(string.Format("\"{0}\":{1},{2}: SyntaxError: {3}", SourceContext.SrcFilePath, line, charPositionInLine, msg));
        }
    }
}
