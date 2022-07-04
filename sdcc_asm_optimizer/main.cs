
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

            [Option('c', "compiler", Required = true, HelpText = "compiler name")]
            public string CompilerName { get; set; }

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

        static readonly Regex cSourceMatcher = new(@"\.(c)$", RegexOptions.IgnoreCase | RegexOptions.Compiled); // file filters
        static readonly string[] gSupportedToolLi = { "sdcc" }; // supported list

        //
        // global vars
        //
        public static bool ENABLE_PROFILE = true; // print parser profile info

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

                // filter source file
                cliOptions.InputSrcFiles = cliOptions.InputSrcFiles
                    .Where(p => cSourceMatcher.IsMatch(p));

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
                    int eCode = Execute(cliOptions.CompilerName, cliArgs, out string allOut, out string stdErr);
                    StdErr.Write(allOut); // pass compiler out -> stderr
                    if (eCode != CODE_DONE) return eCode;

                    // split modules
                    StringWriter pStdOut = new(), pStdErr = new();
                    SourceContext result = ParseSourceFile(fOutPath, pStdOut, pStdErr);
                    if (cliOptions.OnlyTestSourceFile) continue; // if it's test mode, ignore split
                    if (pStdErr.GetStringBuilder().Length != 0)
                        throw new Exception("Parser Error: " + pStdErr.ToString());
                    var outFiles = SplitAndGenerateFiles(result);

                    // compile modules
                    outLines.Add("---> " + srcFilePath);
                    if (outFiles.Length == 0) outFiles = new string[] { fOutPath }; // use origin file
                    var oLi = CompileModuleFiles(cliOptions.CompilerName, cliOptions.CompilerArgs, outFiles);
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

            Directory.CreateDirectory(outDirPath);

            var funcSyms = ctx.globalFuncSyms.ToList();

            // make entry func at the first

            var entryIdx = funcSyms.FindIndex(c => c.name == "_" + cliOptions.EntryName);
            var IsEntrySourceFile = entryIdx != -1;
            if (IsEntrySourceFile)
            {
                var s = funcSyms[entryIdx];
                funcSyms.RemoveAt(entryIdx);
                funcSyms.Insert(0, s);
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

            foreach (var curSym in funcSyms)
            {
                bool IsInModule_0 = ModuleId == 0;
                bool IsEntryModule = IsInModule_0 && IsEntrySourceFile;

                // prepare files

                int CurModuleId = ModuleId;
                string srcFileName = outDirPath + Path.DirectorySeparatorChar + ObtainFileName(IsEntryModule);
                StringBuilder[] srcLines = srcRawLines.Select(l => new StringBuilder(l)).ToArray();

                // rename module name

                if (!IsEntryModule)
                {
                    srcLines[ctx.moduleHeaderLine].Replace(ctx.moduleName, ctx.moduleName + "_" + CurModuleId);
                }

                // del syms

                if (IsInModule_0) // only ban func syms in module[0]
                {
                    foreach (var bannedSym in ctx.globalFuncSyms.Where(s => s.UID != curSym.UID))
                    {
                        DisableLines(srcLines, bannedSym.startLine, bannedSym.stopLine);
                    }
                }
                else // ban non-self syms in module[1..n]
                {
                    foreach (var bannedSym in ctx.symbols.Where(s => s.UID != curSym.UID))
                    {
                        DisableLines(srcLines, bannedSym.startLine, bannedSym.stopLine);
                    }

                    foreach (var lineIdx in ctx.isolatedStatementLines)
                    {
                        DisableLines(srcLines, lineIdx, lineIdx);
                    }
                }

                // gen files

                var wLines = srcLines.Select(sl => sl.ToString()).ToArray();
                File.WriteAllLines(srcFileName, wLines);

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

        public SymbolType symType = SymbolType.Function;

        public List<string> refs = null;

        public int startLine = -1;

        public int stopLine = -1;

        public List<SymbolLocation> xinitLocations = null;

        public string UID
        {
            get {
                if (startLine == -1 || stopLine == -1) return null;
                return string.Format("{0}-{1}-{2}-{3}",
                    name, symType.ToString().ToLower(),
                    startLine, stopLine);
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

        public SourceSymbol[] symbols = null;

        public SourceSymbol[] globalSymbols = null;

        public SourceSymbol[] globalFuncSyms = null;

        public int[] isolatedStatementLines = null;

        public SourceContext(string SrcFilePath, ICharStream SrcFileStream)
        {
            this.SrcFilePath = SrcFilePath;
            this.SrcFileStream = SrcFileStream;
        }

        public void Commit()
        {
            globalFuncSyms = symbols
                .Where(s => s.symType == SymbolType.Function && globalSymbols.Contains(s))
                .ToArray();
        }
    }

    class CodeParser : SdAsmBaseListener, IAntlrErrorListener<int>, IAntlrErrorListener<IToken>
    {
        class AsmContext
        {
            public string module;

            public int moduleHeaderLine = -1;

            public List<string> globalSymbols = new(64);

            public List<SourceSymbol> symbols = new(64);

            public List<int> isolatedStatementLines = new(64);

            // current parser context

            public string segment;  // current segment

            public string label;    // current label

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

                    symbols.Add(new SourceSymbol {
                        name = this.label,
                        seg = this.segment,
                        symType = this.type,
                        startLine = this.startLine,
                        stopLine = this.stopLine,
                        refs = this.refs.Distinct().Where(s => s.StartsWith('_')).ToList()
                    });
                }

                // reinit all
                this.label = null;
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
                srcCtx.globalSymbols = symbols.Where(s => this.globalSymbols.Contains(s.name)).ToArray();
                srcCtx.isolatedStatementLines = this.isolatedStatementLines.ToArray();
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

        /////////////////////////////////// parser //////////////////////////////////////////

        public override void ExitSegment([NotNull] SdAsmParser.SegmentContext context)
        {
            AsmSrcContext.Flush(context);

            var vName = context.SegmentType().GetText();

            switch (vName)
            {
                case "module":
                    AsmSrcContext.module = context.segmentSpec().GetText();
                    AsmSrcContext.moduleHeaderLine = context.Start.Line - 1;
                    break;
                case "globl":
                    AsmSrcContext.globalSymbols.Add(context.segmentSpec().GetText());
                    break;
                case "area":
                    AsmSrcContext.segment = context.segmentSpec().GetToken(SdAsmParser.Identifier, 0).GetText();
                    break;
                default:
                    break;
            }
        }

        public override void ExitLabel([NotNull] SdAsmParser.LabelContext context)
        {
            if (context.GetChild(0) is SdAsmParser.NormalLabelContext ctx)
            {
                AsmSrcContext.Flush(context);
                AsmSrcContext.label = ctx.Identifier().GetText();
                AsmSrcContext.type = SymbolType.Function;
                AsmSrcContext.startLine = ctx.Start.Line - 1;
            }
        }

        public override void ExitAsmFile([NotNull] SdAsmParser.AsmFileContext context)
        {
            AsmSrcContext.Flush(context, true);
            AsmSrcContext.Commit(SourceContext);
        }

        public override void ExitMemoryAlloc([NotNull] SdAsmParser.MemoryAllocContext context)
        {
            if (AsmSrcContext.IsActived &&
                AsmSrcContext.type != SymbolType.Variable)
            {
                AsmSrcContext.type = SymbolType.Variable;
            }
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
                AsmSrcContext.isolatedStatementLines.Add(context.Start.Line - 1);
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
