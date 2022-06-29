
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

            foreach (var srcSyms in symGrps)
            {
                StringBuilder[] srcLines = srcRawLines.Select(l => new StringBuilder(l)).ToArray();

                bool IsInModule_0 = NextFileId == 0;
                string srcFileName = outDirPath + Path.DirectorySeparatorChar + ObtainFileName();

                // --- share decl var symbols

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

                // --- normal symbols

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
                    }
                }

                var wLines = srcLines.Select(sl => sl.ToString()).ToArray();
                File.WriteAllLines(srcFileName, wLines);

                print(srcFileName);
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

        public string name = ""; // identifier name, like: 'arr'
        public SymbolType symType = SymbolType.Variable;
        public string typeName = TYPE_NAME_UNKOWN;
        public string declareSpec = "";
        public string[] attrs = Array.Empty<string>();
        public string[] refers = Array.Empty<string>();

        public string initializer = null;  // like: '= 15', '= { 12, 3 }' ...
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
        public IEnumerable<SourceSymbol> Symbols { get { return _symbols.Values; } }
        public Dictionary<string, SourceSymbol> RawSymbolTable { get { return _symbols; } }

        private readonly Dictionary<string, List<SourceSymbol>> _share_declare_symbols = new(64);
        public IEnumerable<List<SourceSymbol>> ShareDeclareSymbols { get { return _share_declare_symbols.Values; } }

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

            var allChildren = FindChild<ParserRuleContext>(declSpec);

            // parse all specs

            var sClasSpecs = allChildren.Where(c => c is SdccParser.StorageClassSpecifierContext).ToArray();

            foreach (var item in sClasSpecs)
            {
                var nodeTxt = item.GetText();

                if (nodeTxt == "typedef")
                    return Array.Empty<SourceSymbol>(); // skip typedef

                vAttrList.Add(nodeTxt);
            }

            var typeSpecs = allChildren.Where(c => {
                return c is SdccParser.TypeSpecifierContext &&
                       c.Parent is SdccParser.DeclarationSpecifierContext;
            }).ToArray();

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

            if (vTypeName == null)
                return Array.Empty<SourceSymbol>(); // it's not a var declare, skip

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

                    if (decl.LeftParen() != null && !(decl.GetChild(0) is ITerminalNode))
                    {
                        continue; // it's a function decl, skip
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

            Queue<ParserRuleContext> ctxQueue = new();

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

        private static void WalkChild<T>(ParserRuleContext ctx, RuleContextChildWalker<T> walker) where T : ParserRuleContext
        {
            foreach (var child in ctx.children)
            {
                if (child is ParserRuleContext c)
                {
                    WalkChild(c, walker);
                }
            }

            if (ctx is T t)
            {
                if (walker(t))
                    return;
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

                if (symLi.Length > 0)
                {
                    SourceContext.AddRangeSymbol(symLi);

                    if (symLi.Length > 1)
                    {
                        SourceContext.AddShareDeclareSymbols(symLi);
                    }
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

                    vFuncDeclSpecTxt = GetParseNodeFullText(SourceContext.SrcFileStream, declSpec);
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
            SourceContext.AddSymbol(new SourceSymbol {
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
            throw new CodeParserException(string.Format("\"{0}\":{1},{2}: LexerError: {3}", SourceContext.SrcFilePath, line, charPositionInLine, msg));
        }

        public void SyntaxError(TextWriter output, IRecognizer recognizer, IToken offendingSymbol, int line, int charPositionInLine, string msg, RecognitionException e)
        {
            throw new CodeParserException(string.Format("\"{0}\":{1},{2}: SyntaxError: {3}", SourceContext.SrcFilePath, line, charPositionInLine, msg));
        }
    }
}
