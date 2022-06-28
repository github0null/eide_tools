
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
            return Regex.IsMatch(path, @"^(?:[a-z]:|/)", RegexOptions.IgnoreCase);
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
            [Option('c', Required = true, HelpText = "compiler id")]
            public string toolchainSelected { get; set; }

            [Option('d', Required = true, HelpText = "current work folder")]
            public string workFolder { get; set; }

            [Option("test", Required = false, HelpText = "only test source file")]
            public bool onlyTestSourceFile { get; set; }

            [Value(0, Min = 1, Required = true, HelpText = "preprocessed source files")]
            public IEnumerable<string> inputSrcFiles { get; set; }
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

        //
        // program entry
        //

        public static int Main(string[] args)
        {
            Options cliOptions = CommandLine.Parser.Default.ParseArguments<Options>(args).Value;

            try
            {
                // set current workspace
                Environment.CurrentDirectory = cliOptions.workFolder;

                // check supported toolchain
                if (!gSupportedToolLi.Contains(cliOptions.toolchainSelected.ToLower()))
                    throw new Exception(string.Format("We not support this toolchain: '{0}'", cliOptions.toolchainSelected));

                // handle files
                foreach (var filePath in cliOptions.inputSrcFiles)
                {
                    if (!gCsrcFileFilter.IsMatch(filePath)) continue;
                    StringWriter sOut = new(), sErr = new();
                    SourceContext result = ParseSourceFile(filePath, sOut, sErr);
                    if (cliOptions.onlyTestSourceFile) continue; // if it's test mode, ignore split
                    if (sErr.GetStringBuilder().Length != 0) throw new Exception("Parser Error: " + sErr.ToString());
                    SplitAndGenerateFiles(result);
                }
            }
            catch (Exception err)
            {
                error(err.ToString());
                return CODE_ERR;
            }

            return CODE_DONE;
        }

        private struct SymbolReferenceItem
        {
            public string uid;
            public SourceSymbol[] refs;
        };

        private static void SplitAndGenerateFiles(SourceContext ctx)
        {
            string baseName = Path.GetFileNameWithoutExtension(ctx.SrcFilePath);
            string extName = Path.GetExtension(ctx.SrcFilePath) ?? "";
            string baseDir = Path.GetDirectoryName(ctx.SrcFilePath);
            if (string.IsNullOrWhiteSpace(baseDir)) baseDir = ".";
            string outDirPath = baseDir + Path.DirectorySeparatorChar + baseName + extName + ".modules";

            // funcs
            uint NextFileId = 0;
            var ObtainFileName = () => baseName + "_" + (NextFileId++).ToString() + extName;

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

            foreach (var syms in symGrps)
            {
                StringBuilder[] srcLines = srcRawLines.Select(l => new StringBuilder(l)).ToArray();

                uint curFileId = NextFileId;
                string srcFileName = outDirPath + Path.DirectorySeparatorChar + ObtainFileName();

                // ban unused symbols
                var disSymLi = ctx.Symbols.Where(sym => !syms.Contains(sym));
                foreach (var sym in disSymLi)
                {
                    // try store some vars for module[0]
                    if (curFileId == 0)
                    {
                        if (sym.symType == SourceSymbol.SymbolType.Variable &&
                            !sym.IsStatic)
                            continue; // store global var or extern var
                    }

                    // we need add 'extern' prefix for global vars in module[1...n]
                    else
                    {
                        if (sym.symType == SourceSymbol.SymbolType.Variable &&
                            !sym.IsStatic && !sym.IsExtern)
                        {
                            string nDeclareTxt = "extern " + sym.declareSpec + " " + sym.name + ";";
                            srcLines[sym.stopLocation.Line - 1].Insert(sym.stopLocation.Column + 1, "\n" + nDeclareTxt);
                        }
                    }

                    //
                    // disable lines by add '//' or '/**/' annotation
                    //

                    if (sym.startLocation.Line == sym.stopLocation.Line) // for single line symbol
                    {
                        var linIdx = sym.stopLocation.Line;
                        srcLines[linIdx - 1].Insert(sym.stopLocation.Column + 1, "\n");
                        srcLines[linIdx - 1].Insert(sym.startLocation.Column, "//");
                    }
                    else // for multi-line symbol
                    {
                        for (int i = sym.startLocation.Line - 1; i < sym.stopLocation.Line; i++)
                        {
                            if (i == sym.startLocation.Line - 1)
                            {
                                srcLines[i].Insert(sym.startLocation.Column, "//");
                            }
                            else
                            {
                                if (i == sym.stopLocation.Line - 1)
                                {
                                    srcLines[i].Insert(sym.stopLocation.Column + 1, "\n");
                                }

                                srcLines[i].Insert(0, "//");
                            }
                        }
                    }
                }

                var wLines = srcLines.Select(sl => sl.ToString()).ToArray();
                File.WriteAllLines(srcFileName, wLines);
            }
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

            var ctx = parser.compilationUnit();

            if (ctx.exception != null) throw ctx.exception;

            return cListener.SourceContext;
        }

        public static void error(string line, bool newLine = true)
        {
            if (newLine) Console.WriteLine(line);
            else Console.Write(line);
        }

        public static void print(string line, bool newLine = true)
        {
            if (newLine) Console.WriteLine(line);
            else Console.Write(line);
        }
    }

    class SourceSymbol
    {
        public enum SymbolType
        {
            Variable,
            Function,
        }

        public static string TYPE_NAME_UNKOWN = "<unkown-type>";

        // ---

        public string name = "";
        public SymbolType symType = SymbolType.Variable;
        public string typeName = TYPE_NAME_UNKOWN;
        public string declareSpec = "";
        public string[] attrs = Array.Empty<string>();
        public string[] refers = Array.Empty<string>();

        public IToken startLocation = null;
        public IToken stopLocation = null;

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

        public string UID
        {
            get {
                if (startLocation == null || stopLocation == null) return null;
                return string.Format("{0}-{1}-{2}-{3}",
                    name, symType.ToString().ToLower(),
                    startLocation.TokenIndex, stopLocation.TokenIndex);
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
        public IEnumerable<SourceSymbol> Symbols { get { return _symbols.Values; } }
        public Dictionary<string, SourceSymbol> RawSymbolTable { get { return _symbols; } }

        private readonly Dictionary<string, SourceSymbol> _symbols = new(256);

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

        public void AddRangeSymbol(IEnumerable<SourceSymbol> sym)
        {
            foreach (var symbol in sym)
            {
                var uid = symbol.UID;

                if (!_symbols.ContainsKey(uid))
                {
                    _symbols.Add(uid, symbol);
                }
            }
        }

        public IEnumerable<SourceSymbol> FindSymbol(string symName)
        {
            return from kv in _symbols
                   where kv.Value.name == symName
                   select kv.Value;
        }

        public SourceSymbol GetSymbol(string key)
        {
            return _symbols[key];
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
        private SourceContext sourceContext;

        public CodeListener(string srcFileName, ICharStream input)
        {
            sourceContext = new(srcFileName, input);
            stack.Push(ParserStatus.InGlobal);
        }

        public SourceContext SourceContext
        {
            get {
                return sourceContext;
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
            string vDeclSpecTxt = null;
            List<string> vAttrList = new();
            List<string> vNameList = new();
            Dictionary<string, List<string>> vRefsMap = new();

            // ---

            var declSpec = context.declarationSpecifiers();

            if (declSpec == null)
                return Array.Empty<SourceSymbol>(); // skip other declare type

            foreach (var declaration in declSpec.declarationSpecifier())
            {
                var typeSpec = declaration.typeSpecifier();

                // skip some type
                if (typeSpec != null)
                {
                    var structSpec = typeSpec.structOrUnionSpecifier();

                    if (structSpec != null && structSpec.structDeclarationList() != null)
                    {
                        // it's a struct type declare, skip it
                        return Array.Empty<SourceSymbol>();
                    }

                    var enumSpec = typeSpec.enumSpecifier();

                    if (enumSpec != null && enumSpec.enumeratorList() != null)
                    {
                        // it's a enum type declare, skip it
                        return Array.Empty<SourceSymbol>();
                    }
                }

                // get type name
                if (typeSpec != null)
                {
                    vTypeName = GetParseNodeFullText(SourceContext.SrcFileStream, typeSpec);
                }

                // Store Class
                {
                    var spec = declaration.storageClassSpecifier();

                    if (spec != null)
                    {
                        var text = spec.GetText();

                        if (text == "typedef")
                            return Array.Empty<SourceSymbol>(); // skip typedef declare

                        vAttrList.Add(text);
                    }
                }

                // Qualifier
                {
                    var spec = declaration.typeQualifier();

                    if (spec != null)
                    {
                        vAttrList.Add(spec.GetText());
                    }
                }
            }

            // get full decl spec txt
            vDeclSpecTxt = GetParseNodeFullText(SourceContext.SrcFileStream, declSpec);

            var initDeclCtx = context.initDeclaratorList();

            if (initDeclCtx == null)
                return Array.Empty<SourceSymbol>(); // it's not a var declare, skip

            if (vTypeName == null)
                vTypeName = SourceSymbol.TYPE_NAME_UNKOWN;

            foreach (var item in initDeclCtx.initDeclarator())
            {
                var decl = item.declarator().directDeclarator();

                if (decl.LeftParen() != null && decl.RightParen() != null)
                    continue; // it's a function decl, skip

                var vName = GetIdentifierFromDirectDeclarator(decl).GetText();

                vNameList.Add(vName);

                if (vRefsMap.ContainsKey(vName) == false)
                    vRefsMap.Add(vName, new());

                var initializerCtx = item.initializer();

                if (initializerCtx != null) // parse var references
                {
                    var baseExprLi = FindChild<SdccParser.PrimaryExpressionContext>(initializerCtx);

                    foreach (var exprCtx in baseExprLi)
                    {
                        var idf = exprCtx.Identifier();

                        if (idf != null)
                        {
                            vRefsMap[vName].Add(idf.GetText());
                        }
                    }
                }
            }

            List<SourceSymbol> symList = new();

            if (vNameList.Count > 0)
            {
                foreach (var name in vNameList)
                {
                    symList.Add(new SourceSymbol {
                        name = name,
                        symType = SourceSymbol.SymbolType.Variable,
                        declareSpec = vDeclSpecTxt,
                        typeName = vTypeName,
                        attrs = vAttrList.ToArray(),
                        refers = vRefsMap[name].ToArray(),
                        startLocation = context.Start,
                        stopLocation = context.Stop
                    });
                }
            }

            return symList.ToArray();
        }

        private static T[] FindChild<T>(ParserRuleContext rootCtx) where T : ParserRuleContext
        {
            List<T> result = new();

            Stack<ParserRuleContext> ctxStack = new();

            ctxStack.Push(rootCtx);

            while (ctxStack.Count > 0)
            {
                var ctx = ctxStack.Pop();

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
                            ctxStack.Push(c);
                        }
                    }
                }
            }

            return result.ToArray();
        }

        private delegate bool RuleContextChildWalker<T>(T ctx) where T : ParserRuleContext;

        private static void WalkChild<T>(ParserRuleContext rootCtx, RuleContextChildWalker<T> walker) where T : ParserRuleContext
        {
            Stack<ParserRuleContext> ctxStack = new();

            ctxStack.Push(rootCtx);

            while (ctxStack.Count > 0)
            {
                var ctx = ctxStack.Pop();

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
                            ctxStack.Push(c);
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

            throw new CodeParserException("Internal Error In: 'getIdentifierFromDirectDeclarator'");
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
                var symLi = ParseVariableDeclare(context);
                sourceContext.AddRangeSymbol(symLi);
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

            string vFuncName = null;

            string vFuncDeclSpecTxt = "";
            List<string> vAttrList = new();
            List<string> vRefeList = new();

            // function local vars
            List<SourceSymbol> localVars = new();

            // get name and params list
            {
                WalkChild(context.declarator(), delegate (SdccParser.DirectDeclaratorContext directDeclCtx) {

                    var subDirectDecl = directDeclCtx.directDeclarator();

                    if (subDirectDecl != null &&
                        directDeclCtx.LeftParen() != null &&
                        directDeclCtx.RightParen() != null) // check func decl, like: 'foo (int a, ...)'
                    {
                        if (subDirectDecl.Colon() != null)
                            return false; // skip bit field decl

                        var paramsLiCtx = directDeclCtx.parameterTypeList();

                        if (paramsLiCtx != null)
                        {
                            foreach (var declCtx in FindChild<SdccParser.DeclaratorContext>(paramsLiCtx))
                            {
                                foreach (var directDeclItem in FindChild<SdccParser.DirectDeclaratorContext>(declCtx))
                                {
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
                                    }
                                }
                            }
                        }

                        var idfCtx = subDirectDecl.Identifier();

                        if (idfCtx != null)
                        {
                            vFuncName = idfCtx.GetText();
                            return true;
                        }

                        var decl = subDirectDecl.declarator();

                        if (decl != null)
                        {
                            WalkChild(decl.directDeclarator(), delegate (SdccParser.DirectDeclaratorContext directDeclCtx) {

                                var idfCtx = directDeclCtx.Identifier();

                                if (idfCtx != null &&
                                    directDeclCtx.ChildCount == 1 && // only have a identifier node
                                    directDeclCtx.Parent is SdccParser.DeclaratorContext) // parent is declarator
                                {
                                    vFuncName = idfCtx.GetText();
                                    return true;
                                }

                                return false;
                            });
                        }
                    }

                    return false;
                });
            }

            if (vFuncName == null)
                return;

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

                    vFuncDeclSpecTxt = GetParseNodeFullText(sourceContext.SrcFileStream, declSpec);
                }
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
                            localVars.AddRange(ParseVariableDeclare(declCtx));
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

            // add to func li
            sourceContext.AddSymbol(new SourceSymbol {
                name = vFuncName,
                symType = SourceSymbol.SymbolType.Function,
                typeName = SourceSymbol.TYPE_NAME_UNKOWN,
                declareSpec = vFuncDeclSpecTxt,
                attrs = vAttrList.ToArray(),
                refers = vRefeList.ToArray(),
                startLocation = context.Start,
                stopLocation = context.Stop
            });
        }

        public void SyntaxError(TextWriter output, IRecognizer recognizer, int offendingSymbol, int line, int charPositionInLine, string msg, RecognitionException e)
        {
            throw new CodeParserException(string.Format("\"{0}\":{1},{2}: LexerError: {3}", sourceContext.SrcFilePath, line, charPositionInLine, msg));
        }

        public void SyntaxError(TextWriter output, IRecognizer recognizer, IToken offendingSymbol, int line, int charPositionInLine, string msg, RecognitionException e)
        {
            throw new CodeParserException(string.Format("\"{0}\":{1},{2}: SyntaxError: {3}", sourceContext.SrcFilePath, line, charPositionInLine, msg));
        }
    }
}
