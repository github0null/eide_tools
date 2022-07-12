
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
using CommandLine;

namespace sdcc_asm_optimizer
{
    class Program
    {
        public class Options
        {
            [Option('w', "cwd", Required = true, HelpText = "current work folder")]
            public string WorkFolder { get; set; }

            [Option('o', "outdir", Required = true, HelpText = "output root folder")]
            public string OutputDir { get; set; }

            [Option("compiler-args", Required = true, HelpText = "compiler args")]
            public string CompilerArgs { get; set; }

            [Option("compiler-dir", Required = false, HelpText = "compiler root dir")]
            public string CompilerDir { get; set; }

            [Option("program-entry", Required = false, HelpText = "program entry name")]
            public string EntryName { get; set; }

            [Option('t', "test", Required = false, HelpText = "only test source file")]
            public bool OnlyTestSourceFile { get; set; }

            [Value(0, Min = 1, Max = 1, Required = true, HelpText = "source files with relative path")]
            public IEnumerable<string> InputSrcFiles { get; set; }
        }

        //
        // global const
        //
        public static readonly int CODE_ERR = 1;
        public static readonly int CODE_DONE = 0;

        static readonly Dictionary<string, string> AssemblerMap = new() {
            { "mcs51", "8051" },
            { "ds390", "390" },
            { "ds400", "390" },
            { "hc08", "6808" },
            { "s08", "6808" },
            { "r2k", "rab" },
            { "gbz80", "gb" },
            { "ez80_z80", "z80" }
        };

        static readonly string[] supportedAssembler = {
            "mcs51", "stm8"
        };

        //
        // global vars
        //
        public static bool ENABLE_PROFILE = true; // print parser profile info

        public static readonly TextWriter StdOut = Console.Out;
        public static readonly TextWriter StdErr = Console.Error;

        public static string objSuffix = ".rel";
        public static string compilerName = "sdcc";

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

                if (cliOptions.CompilerDir != null)
                    Append2SysEnv(cliOptions.CompilerDir);

                if (cliOptions.CompilerArgs.StartsWith("\""))
                    cliOptions.CompilerArgs = cliOptions.CompilerArgs.Trim('"');

                if (cliOptions.EntryName == null)
                    cliOptions.EntryName = "main";

                // handle files
                foreach (var srcFilePath in cliOptions.InputSrcFiles)
                {
                    List<string> outLines = new(64);

                    // preprocess source file: '.c' -> '.asm'
                    var fOutPath = Path.ChangeExtension(RelocatePath(cliOptions.OutputDir, srcFilePath), ".asm");
                    Directory.CreateDirectory(Path.GetDirectoryName(fOutPath));
                    var cliArgs = "-S --no-c-code-in-asm " + cliOptions.CompilerArgs
                        .Replace("${in}", string.Format("\"{0}\"", srcFilePath))
                        .Replace("${out}", string.Format("\"{0}\"", fOutPath));
                    int eCode = Execute(compilerName, cliArgs, out string allOut, out string stdErr);
                    StdErr.Write(allOut); // pass compiler out -> stderr
                    if (eCode != CODE_DONE) return eCode;

                    // parse asm source
                    StringWriter pStdOut = new(), pStdErr = new();
                    SourceContext parserCtx = ParseSourceFile(fOutPath, pStdOut, pStdErr);
                    if (pStdErr.GetStringBuilder().Length != 0) throw new Exception("Parser Error: " + pStdErr.ToString());

                    // if it's test mode, ignore split
                    if (cliOptions.OnlyTestSourceFile) continue;

                    // split modules
                    string[] outFiles = Array.Empty<string>();
                    if (supportedAssembler.Contains(parserCtx.assemblerName)) outFiles = SplitAndGenerateFiles(parserCtx);

                    // compile modules
                    outLines.Add("---> " + srcFilePath);
                    if (outFiles.Length == 0) outFiles = new string[] { fOutPath }; // if not need split, use origin file
                    var oLi = CompileModuleFiles(parserCtx.assemblerName, outFiles);
                    foreach (var p in oLi) outLines.Add(p);
                    outLines.Add("<---");
                    outLines.Add(pStdOut.GetStringBuilder().ToString());

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

        struct CompileWorkerData
        {
            public ManualResetEvent evtDone;
            public string[] files;
        };

        private static string[] CompileModuleFiles(string assemblerName, string[] inFiles)
        {
            Exception err = null;
            List<string> outFiles = new(64);

            // calcu thread number
            int threadNum = 2;
            if (inFiles.Length >= 15) threadNum = 3;
            if (inFiles.Length >= 24) threadNum = 4;

            // remap assembler name
            if (AssemblerMap.ContainsKey(assemblerName)) assemblerName = AssemblerMap[assemblerName];

            // worker
            ParameterizedThreadStart worker = delegate (object _dat) {

                CompileWorkerData workerParams = (CompileWorkerData)_dat;

                foreach (var path in workerParams.files)
                {
                    if (err != null) break;

                    try
                    {
                        var fin = path;
                        var fou = Path.ChangeExtension(path, objSuffix);
                        var asArgs = "-plosgffw ${out} ${in}"
                            .Replace("${in}", "\"" + fin + "\"")
                            .Replace("${out}", "\"" + fou + "\"");
                        var exitCode = Execute("sdas" + assemblerName, asArgs, out string out_, out string __);
                        lock (StdErr) { StdErr.Write(out_); } // pass compiler out -> stderr
                        if (exitCode != CODE_DONE) throw new Exception("compiler error at: " + fin);
                        lock (outFiles) { outFiles.Add(fou); }
                    }
                    catch (Exception err_)
                    {
                        err = err_;
                        break;
                    }
                }

                workerParams.evtDone.Set();
            };

            List<List<string>> fileGrps = new(32);
            Queue<string> allFiles = new(inFiles);
            int filesPerThread = (inFiles.Length / threadNum) + 1;

            while (allFiles.Count > 0)
            {
                List<string> li = new(32);

                for (int i = 0; i < filesPerThread; i++)
                {
                    if (allFiles.TryDequeue(out string f))
                        li.Add(f);
                    else
                        break;
                }

                if (li.Count > 0)
                    fileGrps.Add(li);
            }

            if (fileGrps.Count > 1) // real grp number > 1, use multi-thread
            {
                ManualResetEvent[] tEvents = new ManualResetEvent[fileGrps.Count];

                for (int i = 0; i < fileGrps.Count; i++)
                {
                    tEvents[i] = new(false);

                    new Thread(worker).Start(new CompileWorkerData {
                        evtDone = tEvents[i],
                        files = fileGrps[i].ToArray()
                    });
                }

                WaitHandle.WaitAll(tEvents);
            }

            else // we don't need multi-thread
            {
                worker(new CompileWorkerData {
                    evtDone = new ManualResetEvent(false),
                    files = inFiles
                });
            }

            if (err != null) throw err;

            outFiles.Sort();

            return outFiles.ToArray();
        }

        struct SymbolReferenceItem
        {
            public SourceSymbol rootSym;
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

            Directory.CreateDirectory(outDirPath);

            // make static ref chain, exclude static var
            Dictionary<string, SourceSymbol[]> symStaticRefs = ctx.MakeStaticRefs();

            List<SymbolReferenceItem> detachedSyms =
                symStaticRefs.Select(kv => new SymbolReferenceItem {
                    rootSym = ctx.GetSymbolByName(kv.Key),
                    refs = kv.Value
                }).ToList();

            detachedSyms.Sort((a, b) => b.refs.Length - a.refs.Length);

            // make symbol group

            List<List<SourceSymbol>> needDetachedSymGrps = new(128);

            Func<SourceSymbol, int> FindResolvedGrpIdx = delegate (SourceSymbol sym) {
                return needDetachedSymGrps.FindIndex((symLi) => symLi.Contains(sym));
            };

            foreach (var symInfo in detachedSyms)
            {
                var rootSym = symInfo.rootSym;

                if (FindResolvedGrpIdx(rootSym) != -1)
                    continue;

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
                        var n = needDetachedSymGrps[conflictIdx].Union(needDetachedSymGrps[expectedGrpIdx]).ToList();
                        needDetachedSymGrps = needDetachedSymGrps
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
                    needDetachedSymGrps[expectedGrpIdx].AddRange(curSyms);
                else
                    needDetachedSymGrps.Add(curSyms);
            }

            for (int i = 0; i < needDetachedSymGrps.Count; i++)
                needDetachedSymGrps[i] = needDetachedSymGrps[i].Distinct().ToList();

            // filter groups
            {
                List<SourceSymbol> excList = new(64);
                var forceLocalSyms = ctx.forceLocalSyms.ToList();

                // exclude main entry

                var programEntrySymName = "_" + cliOptions.EntryName;
                var entrySym = ctx.GetSymbolByName(programEntrySymName);
                if (entrySym != null) forceLocalSyms.Add(entrySym);

                // init exclude li

                excList.AddRange(forceLocalSyms);

                foreach (var s in forceLocalSyms)
                {
                    if (symStaticRefs.ContainsKey(s.name))
                    {
                        excList.AddRange(symStaticRefs[s.name]);
                    }
                }

                // do filter

                needDetachedSymGrps = needDetachedSymGrps.Where(grp => {

                    var isPassed = true;

                    foreach (var sym in grp)
                    {
                        if (excList.Contains(sym))
                        {
                            isPassed = false;
                            break;
                        }
                    }

                    return isPassed;

                }).ToList();
            }

            // generate files

            List<string> mFiles = new();

            string[] srcRawLines = File.ReadAllLines(ctx.SrcFilePath);

            int ModuleId = 0;
            var ObtainFileName = (bool noId) => {
                var cuID = ModuleId++;
                if (noId) return baseName + extName;
                return baseName + "_" + cuID.ToString() + extName;
            };

            var DisableLines = (StringBuilder[] lines, int startIdx, int stopIdx) => {
                for (int i = startIdx; i < stopIdx + 1; i++) lines[i].Insert(0, ';');
            };

            // stable prepend statement
            string[] stablePrependLines = Array.Empty<string>();
            {
                //
                // for sdcc-mcs51, we need predefine 'ar0~ar7' 
                // register addr and export them
                //
                if (ctx.assemblerName == "mcs51")
                {
                    List<string> lines = new(64);

                    lines.AddRange(new string[] {
                        ";--------------------------------------------------------",
                        "; Generate by eide.sdcc_asm_optimizer",
                        ";--------------------------------------------------------"
                    });

                    for (int i = 0; i < 8; i++)
                        lines.Add(string.Format("\tar{0} = 0x0{0}", i));

                    lines.Add("");

                    stablePrependLines = lines.ToArray();
                }
            }

            // gen base module
            {
                string srcFileName = outDirPath + Path.DirectorySeparatorChar + ObtainFileName(true);
                StringBuilder[] srcLines = srcRawLines.Select(l => new StringBuilder(l)).ToArray();

                foreach (var symGrpForFile in needDetachedSymGrps)
                {
                    foreach (var sym in symGrpForFile)
                    {
                        if (sym.symType == SymbolType.Variable)
                        {
                            DisableLines(srcLines, sym.startLine, sym.stopLine);

                            foreach (var xinitLoc in sym.xinitLocations)
                            {
                                DisableLines(srcLines, xinitLoc.startLine, xinitLoc.stopLine);
                            }
                        }
                        else
                        {
                            DisableLines(srcLines, sym.startLine, sym.stopLine);
                        }
                    }
                }

                var eSyms = needDetachedSymGrps.SelectMany(grp => grp, (grp, sym) => sym).ToList();
                var lSyms = ctx.symbols.Where(s => !eSyms.Contains(s));

                List<string> reservedImpHeaders = new(64);
                foreach (var localSym in lSyms)
                {
                    reservedImpHeaders.Add(localSym.name);
                    reservedImpHeaders.AddRange(localSym.refs);
                }

                // disable global import declare
                var disabledImpHeaders = ctx.globSymImportLineMap.Keys.Where(n => !reservedImpHeaders.Contains(n));
                foreach (var symName in disabledImpHeaders)
                {
                    if (ctx.globSymImportLineMap.ContainsKey(symName))
                    {
                        var lineIdx = ctx.globSymImportLineMap[symName];
                        DisableLines(srcLines, lineIdx, lineIdx);
                    }
                }

                var fLines = srcLines.Select(sl => sl.ToString()).ToList();
                File.WriteAllLines(srcFileName, stablePrependLines.Concat(fLines));
                mFiles.Add(srcFileName);
            }

            // handle need detached symbols

            foreach (var symGrpForFile in needDetachedSymGrps)
            {
                int CurModuleId = ModuleId;

                // prepare file
                string srcFileName = outDirPath + Path.DirectorySeparatorChar + ObtainFileName(false);
                StringBuilder[] srcLines = srcRawLines.Select(l => new StringBuilder(l)).ToArray();

                // rename module name
                srcLines[ctx.moduleHeaderLine].Replace(ctx.moduleName, ctx.moduleName + "_" + CurModuleId);

                // disable banned syms
                var bannedSyms = ctx.symbols.Where(s => !symGrpForFile.Contains(s));
                foreach (var disSym in bannedSyms)
                {
                    DisableLines(srcLines, disSym.startLine, disSym.stopLine);

                    foreach (var loc in disSym.xinitLocations)
                    {
                        DisableLines(srcLines, loc.startLine, loc.stopLine);
                    }
                }

                // disable noused import decl
                {
                    List<string> inUsedSyms = new(64);
                    foreach (var sym in symGrpForFile)
                    {
                        inUsedSyms.Add(sym.name);
                        foreach (var name in sym.refs) inUsedSyms.Add(name);
                    }

                    var unusedGloblSyms =
                        ctx.globSymImportLineMap.Keys.Where(name => !inUsedSyms.Contains(name));
                    foreach (var symName in unusedGloblSyms)
                    {
                        if (ctx.globSymImportLineMap.ContainsKey(symName))
                        {
                            var lineIdx = ctx.globSymImportLineMap[symName];
                            DisableLines(srcLines, lineIdx, lineIdx);
                        }
                    }
                }

                // disable standalone lines
                foreach (var lineIdx in ctx.isolatedStatementLines)
                    DisableLines(srcLines, lineIdx, lineIdx);

                // gen files
                var fLines = srcLines.Select(sl => sl.ToString()).ToArray();
                File.WriteAllLines(srcFileName, stablePrependLines.Concat(fLines));
                mFiles.Add(srcFileName);
            }

            return mFiles.ToArray();
        }

        private static SourceContext ParseSourceFile(string srcPath, StringWriter stdOut, StringWriter stdErr)
        {
            ICharStream input = CharStreams.fromStream(new FileStream(srcPath, FileMode.Open, FileAccess.Read));

            CodeParser cListener = new(srcPath, input);
            SdAsmLexer lexer = new(input);
            CommonTokenStream tokens = new(lexer);
            SdAsmParser parser = new(tokens, stdOut, stdErr);

            lexer.AddErrorListener(cListener);
            parser.AddErrorListener(cListener);
            parser.AddParseListener(cListener);

            parser.ErrorHandler = new BailErrorStrategy();
            parser.BuildParseTree = true;
            parser.Profile = ENABLE_PROFILE;

            var ctx = parser.asmFile();
            if (ctx.exception != null) throw ctx.exception;

            if (ENABLE_PROFILE)
            {
                var print = (string str, bool newLine) => {
                    if (newLine) stdOut.WriteLine(str);
                    else stdOut.Write(str);
                };

                print(string.Format("{0,-" + 35 + "}", "rule"), false);
                print(string.Format("{0,-" + 15 + "}", "time"), false);
                print(string.Format("{0,-" + 15 + "}", "invocations"), false);
                print(string.Format("{0,-" + 15 + "}", "lookahead"), false);
                print(string.Format("{0,-" + 15 + "}", "lookahead(max)"), false);
                print(string.Format("{0,-" + 15 + "}", "ambiguities"), false);
                print(string.Format("{0,-" + 15 + "}", "errors"), true);

                foreach (var decisionInfo in parser.ParseInfo.getDecisionInfo())
                {
                    var ds = parser.Atn.GetDecisionState(decisionInfo.decision);
                    var rule = parser.RuleNames[ds.ruleIndex];
                    if (decisionInfo.timeInPrediction > 0)
                    {
                        print(string.Format("{0,-" + 35 + "}", rule), false);
                        print(string.Format("{0,-" + 15 + "}", decisionInfo.timeInPrediction), false);
                        print(string.Format("{0,-" + 15 + "}", decisionInfo.invocations), false);
                        print(string.Format("{0,-" + 15 + "}", decisionInfo.SLL_TotalLook), false);
                        print(string.Format("{0,-" + 15 + "}", decisionInfo.SLL_MaxLook), false);
                        print(string.Format("{0,-" + 15 + "}", decisionInfo.ambiguities.Count), false);
                        print(string.Format("{0,-" + 15 + "}", decisionInfo.errors.Count), true);
                    }
                }
            }

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

            Process process = new();
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

    //////////////////////////////////////////////////////////////
    //  parser
    //////////////////////////////////////////////////////////////

    public enum SymbolType
    {
        Variable,
        Function,
    }

    class SymbolLocation
    {
        public int startLine = -1;
        public int stopLine = -1;
    }

    class SourceSymbol
    {
        public string name = ""; // identifier name, like: '_arr'

        public string seg = null;

        public string[] segOpts = null;

        public SymbolType symType = SymbolType.Function;

        public bool isStatic = true;

        public List<string> refs = null;

        public int startLine = -1;

        public int stopLine = -1;

        public List<SymbolLocation> xinitLocations = new(8);

        public string UID
        {
            get {
                if (startLine == -1 || stopLine == -1) return null;
                return string.Format("{0}-{1}-{2}-{3}",
                    name, symType.ToString().ToLower(), startLine, stopLine);
            }
        }

        public override bool Equals(object obj)
        {
            if (obj is SourceSymbol sym)
            {
                return sym.UID == this.UID;
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

        public string moduleName = null;

        public int moduleHeaderLine = -1;

        public string assemblerName = null;

        public SourceSymbol[] symbols = null;

        private Dictionary<string, SourceSymbol> symbolsMap = null;

        public Dictionary<string, int> globSymImportLineMap = null;

        public SourceSymbol[] forceLocalSyms = null;

        public SourceSymbol[] ignoreDetachSyms = null;

        public int[] isolatedStatementLines = null;

        public SourceContext(string SrcFilePath, ICharStream SrcFileStream)
        {
            this.SrcFilePath = SrcFilePath;
            this.SrcFileStream = SrcFileStream;
        }

        public IEnumerable<SourceSymbol> FindSymbols(string name)
        {
            return symbols.Where(s => s.name == name);
        }

        public SourceSymbol GetSymbolByName(string name)
        {
            if (!symbolsMap.ContainsKey(name)) return null;
            return symbolsMap[name];
        }

        public Dictionary<string, SourceSymbol[]> MakeStaticRefs()
        {
            Dictionary<string, SourceSymbol[]> symStaticRefs = new(64);

            var needDetachSyms = symbols.Where(s => !ignoreDetachSyms.Contains(s));

            foreach (var curSym in needDetachSyms)
            {
                List<SourceSymbol> curRefs = new(64);
                List<SourceSymbol> refSyms = new(64);

                foreach (var refName in curSym.refs)
                {
                    // skip direct recursive
                    if (curSym.name == refName)
                        continue;

                    refSyms.AddRange(FindSymbols(refName));
                }

                Stack<SourceSymbol> symStk = new(refSyms);

                while (symStk.Count > 0)
                {
                    var s = symStk.Pop();

                    // skip indirect recursive
                    if (curSym.name == s.name)
                        continue;

                    // if it's global sym, ignore it
                    // we only need to handle static reference
                    if (s.isStatic == false)
                        continue;

                    // skip existed syms (indirect recursive)
                    if (curRefs.Contains(s))
                        continue;

                    curRefs.Add(s);

                    foreach (var n in s.refs)
                    {
                        // skip direct recursive
                        if (s.name == n)
                            continue;

                        foreach (var item in FindSymbols(n))
                        {
                            symStk.Push(item);
                        }
                    }
                }

                symStaticRefs.Add(curSym.name, curRefs.Distinct().ToArray());
            }

            return symStaticRefs;
        }

        private static string[] _USER_CODE_SEGS = {
            "CODE", "CSEG"
        };

        public static bool IsInUserCodeSeg(SourceSymbol sym)
        {
            return _USER_CODE_SEGS.Contains(sym.seg);
        }

        public void Commit()
        {
            symbolsMap = new(symbols.Length);
            foreach (var sym in symbols) symbolsMap.Add(sym.name, sym);

            var IsFuncWithStaticLocalVar = (SourceSymbol sFunc) => {
                return symbols
                    .Where(s => s.symType == SymbolType.Variable && s.isStatic)
                    .Any(v => v.name.StartsWith(sFunc.name + "_"));
            };

            var forceLocalFuncSyms = symbols
                .Where(s => s.symType == SymbolType.Function)
                .Where(s => IsFuncWithStaticLocalVar(s) || !IsInUserCodeSeg(s));

            var forceLocalVarSyms = symbols
                .Where(s => s.symType == SymbolType.Variable)
                .Where(s => s.seg == "SSEG");

            forceLocalSyms = forceLocalFuncSyms
                .Concat(forceLocalVarSyms)
                .Distinct().ToArray();

            string[] ignoredSeg = { "RSEG" };

            ignoreDetachSyms = symbols.Where(sym => ignoredSeg.Contains(sym.seg)).ToArray();
        }
    }

    class CodeParser : SdAsmBaseListener, IAntlrErrorListener<int>, IAntlrErrorListener<IToken>
    {
        class AsmContext
        {
            public string module;

            public int moduleHeaderLine = -1;

            public string[] assemblerOpts = null;

            public Dictionary<string, int> globalSymbols = new(64);

            public List<SourceSymbol> symbols = new(64);

            public List<int> isolatedStatementLines = new(64);

            // current parser context

            public string segment;  // current segment

            public string[] segmentOpts;  // current segment

            public string label;    // current label

            public bool isGlobal;

            public SymbolType type = SymbolType.Function; // current ctx type

            public int startLine;   // label start line

            public int stopLine;    // label region end line

            public List<string> refs = new(64);

            public bool IsActived { get { return label != null; } }

            // funcs

            public void Flush(ParserRuleContext ctx, bool isFileEnd = false)
            {
                if (this.IsActived)
                {
                    this.stopLine = ctx.Stop.Line - (isFileEnd ? 1 : 2); // prev sym stopLine

                    if (label.StartsWith("__xinit_"))
                    {
                        var valName = label.Replace("__xinit_", "");
                        var idx = symbols.FindIndex(s => s.name == valName);

                        if (idx != -1)
                        {
                            symbols[idx].xinitLocations.Add(new SymbolLocation {
                                startLine = this.startLine,
                                stopLine = this.stopLine
                            });

                            symbols[idx].refs.AddRange(this.refs);
                        }
                    }
                    else
                    {
                        symbols.Add(new SourceSymbol {
                            name = this.label,
                            seg = this.segment,
                            segOpts = this.segmentOpts,
                            symType = this.type,
                            isStatic = !this.isGlobal,
                            startLine = this.startLine,
                            stopLine = this.stopLine,
                            refs = this.refs.Distinct().Where(s => s.StartsWith('_')).ToList()
                        });
                    }
                }

                // reinit all
                this.label = null;
                this.isGlobal = false;
                this.type = SymbolType.Function;
                this.startLine = -1;
                this.stopLine = -1;
                this.refs.Clear();
            }

            public void Commit(SourceContext srcCtx)
            {
                srcCtx.moduleName = this.module;
                srcCtx.moduleHeaderLine = this.moduleHeaderLine;
                srcCtx.symbols = this.symbols.Distinct().ToArray();
                srcCtx.globSymImportLineMap = globalSymbols.ToDictionary(kv => kv.Key, kv => kv.Value);
                srcCtx.isolatedStatementLines = this.isolatedStatementLines.ToArray();

                foreach (var sym in srcCtx.symbols)
                {
                    if (globalSymbols.Keys.Contains(sym.name))
                        sym.isStatic = false;
                }

                foreach (var item in assemblerOpts)
                {
                    if (item.StartsWith("-m"))
                    {
                        srcCtx.assemblerName = item.Substring(2);
                        break;
                    }
                }

                srcCtx.Commit();
            }
        }

        //////////////////////////////////////////////////////////////////////

        private readonly SourceContext _src_ctx;
        private AsmContext AsmSrcContext = new();

        public CodeParser(string srcFileName, ICharStream input)
        {
            _src_ctx = new(srcFileName, input);
        }

        public SourceContext SourceContext
        {
            get {
                return _src_ctx;
            }
        }

        private string GetFullTextByCtx<T>(T ctx) where T : ParserRuleContext
        {
            return SourceContext.SrcFileStream.GetText(new Interval(ctx.Start.StartIndex, ctx.Stop.StopIndex));
        }

        private T[] FindChild<T>(ParserRuleContext rootCtx) where T : ParserRuleContext
        {
            List<T> result = new();

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

        /////////////////////////////////// parser //////////////////////////////////////////

        public override void ExitDirective([NotNull] SdAsmParser.DirectiveContext context)
        {
            AsmSrcContext.Flush(context);

            if (context.GetChild(1) is ITerminalNode n)
            {
                switch (n.GetText())
                {
                    case "module":
                        AsmSrcContext.module = context.moduleName().GetText();
                        AsmSrcContext.moduleHeaderLine = context.Start.Line - 1;
                        break;
                    case "optsdcc":
                        AsmSrcContext.assemblerOpts = context.sdccOpts().Select(ctx => ctx.GetText()).ToArray();
                        break;
                    default:
                        break;
                }
            }
        }

        public override void ExitSegment([NotNull] SdAsmParser.SegmentContext context)
        {
            AsmSrcContext.Flush(context);

            switch (context.SegmentType().GetText())
            {
                case "globl":
                    AsmSrcContext.globalSymbols.Add(context.segmentName().GetText(), context.Start.Line - 1);
                    break;
                case "area":
                    AsmSrcContext.segment = context.segmentName().GetText();
                    AsmSrcContext.segmentOpts = Array.ConvertAll(context.segmentOpts(), ctx => ctx.GetText());
                    break;
                default:
                    break;
            }
        }

        private bool IsFunctionSym(AsmContext ctx)
        {
            string[] __CODE_SEG_LI = {
                "HOME", "GSINIT","GSFINAL", "CODE", "CSEG", "_CODE",
                "GSINIT0", "GSINIT1", "GSINIT2", "GSINIT3", "GSINIT4", "GSINIT5"
            };

            return __CODE_SEG_LI.Contains(ctx.segment);
        }

        public override void ExitLabel([NotNull] SdAsmParser.LabelContext context)
        {
            if (context.GetChild(0) is SdAsmParser.NormalLabelContext ctx)
            {
                AsmSrcContext.Flush(context);
                AsmSrcContext.label = ctx.Identifier().GetText();
                AsmSrcContext.type = IsFunctionSym(AsmSrcContext) ? SymbolType.Function : SymbolType.Variable;
                AsmSrcContext.startLine = ctx.Start.Line - 1;
                AsmSrcContext.isGlobal = context.Colon().Length > 1;
            }
        }

        public override void ExitAsmFile([NotNull] SdAsmParser.AsmFileContext context)
        {
            AsmSrcContext.Flush(context, true);
            AsmSrcContext.Commit(SourceContext);
        }

        public override void ExitStatement([NotNull] SdAsmParser.StatementContext context)
        {
            if (AsmSrcContext.IsActived &&
                AsmSrcContext.type == SymbolType.Function)
            {
                var idLi = FindChild<SdAsmParser.NormalLabelContext>(context)
                    .Select(c => c.Identifier().GetText()).ToArray();
                AsmSrcContext.refs.AddRange(idLi);
            }

            else
            {
                AsmSrcContext.Flush(context);

                if (context.GetChild(0) is SdAsmParser.AbsAddrAllocExprContext absAddrExpr)
                {
                    AsmSrcContext.symbols.Add(new SourceSymbol {
                        name = absAddrExpr.Identifier().GetText(),
                        symType = SymbolType.Variable,
                        seg = AsmSrcContext.segment,
                        segOpts = AsmSrcContext.segmentOpts,
                        startLine = context.Start.Line - 1,
                        stopLine = context.Start.Line - 1,
                        refs = new()
                    });
                }
                else
                {
                    AsmSrcContext.isolatedStatementLines.Add(context.Start.Line - 1);
                }
            }
        }

        // error handler

        public void SyntaxError(TextWriter output, IRecognizer recognizer, int offendingSymbol, int line, int charPositionInLine, string msg, RecognitionException e)
        {
            throw new Exception(string.Format("\"{0}\":{1},{2}: LexerError: {3}", SourceContext.SrcFilePath, line, charPositionInLine, msg));
        }

        public void SyntaxError(TextWriter output, IRecognizer recognizer, IToken offendingSymbol, int line, int charPositionInLine, string msg, RecognitionException e)
        {
            throw new Exception(string.Format("\"{0}\":{1},{2}: SyntaxError: {3}", SourceContext.SrcFilePath, line, charPositionInLine, msg));
        }
    }
}
