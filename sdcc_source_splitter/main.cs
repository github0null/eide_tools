﻿
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

    /*  
     export interface BuilderParams {
        name: string;
        target: string;
        toolchain: ToolchainName;
        toolchainCfgFile: string;
        toolchainLocation: string;
        buildMode: string;
        showRepathOnLog?: boolean,
        threadNum?: number;
        dumpPath: string;
        outDir: string;
        builderDir?: string;
        rootDir: string;
        ram?: number;
        rom?: number;
        sourceList: string[];
        sourceParams?: { [name: string]: string; };
        sourceParamsMtime?: number;
        incDirs: string[];
        libDirs: string[];
        defines: string[];
        options: ICompileOptions;
        sha?: { [options_name: string]: string };
        env?: { [name: string]: any };
     }
    */

    class Program
    {
        public class Options
        {
            [Option('c', Required = true, HelpText = "compiler id")]
            public string toolchainSelected { get; set; }

            [Option('d', Required = true, HelpText = "current work folder")]
            public string workFolder { get; set; }

            [Option("test", Required = false, HelpText = "current work folder")]
            public bool onlyTestSourceFile { get; set; }

            [Value(0, Min = 1, Required = true, HelpText = "preprocessed source files")]
            public IEnumerable<string> inputSrcFiles { get; set; }
        }

        //
        // global const
        //
        public static readonly int CODE_ERR = 1;
        public static readonly int CODE_DONE = 0;

        // file filters
        static readonly Regex gCsrcFileFilter = new Regex(@"\.c$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // supported list
        static readonly string[] gSupportedToolLi = { "sdcc" };

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

                List<SourceContext> srcContextLi = new();

                foreach (var filePath in cliOptions.inputSrcFiles)
                {
                    if (gCsrcFileFilter.IsMatch(filePath))
                    {
                        srcContextLi.Add(parseSdccSourceFile(filePath));
                    }
                }

                // if not exception, parse done
                if (cliOptions.onlyTestSourceFile)
                {
                    log("ok !");
                    return CODE_DONE;
                }

                // split and generate files
                foreach (var item in srcContextLi)
                {
                    splitAndGenerateFiles(item);
                }
            }
            catch (Exception err)
            {
                log(err.ToString());
                return CODE_ERR;
            }

            return CODE_DONE;
        }

        private static void splitAndGenerateFiles(SourceContext info_)
        {
            string baseName = Path.GetFileName(info_.srcFilePath);
            string outDirPath = Path.GetDirectoryName(info_.srcFilePath) + Path.DirectorySeparatorChar + baseName + ".split";

            // clean old files
            if (Directory.Exists(outDirPath)) { Directory.Delete(outDirPath, true); }
            Directory.CreateDirectory(outDirPath);

            // 
            string[] srcTxtLines = File.ReadAllLines(info_.srcFilePath);

            // 
        }

        private static SourceContext parseSdccSourceFile(string srcPath)
        {
            ICharStream input = CharStreams.fromStream(new FileStream(srcPath, FileMode.Open, FileAccess.Read));
            CodeListener cListener = new CodeListener(srcPath, input);

            SdccLexer lexer = new SdccLexer(input);
            lexer.AddErrorListener(cListener);

            SdccParser parser = new SdccParser(new CommonTokenStream(lexer));
            parser.AddErrorListener(cListener);
            parser.AddParseListener(cListener);
            var ctx = parser.compilationUnit();

            if (ctx.exception != null) throw ctx.exception;

            return cListener.SourceContext;
        }

        public static void log(string line, bool newLine = true)
        {
            if (newLine) Console.WriteLine(line);
            else Console.Write(line);
        }

        public static void debug(string line, bool newLine = true)
        {
            if (newLine) Console.WriteLine(line);
            else Console.Write(line);
        }
    }

    enum SymbolType
    {
        Variable,
        Function,
    }

    class SourceSymbol
    {
        public string name;
        public SymbolType type;
        public string[] attrs;
        public string[] refers;

        public IToken startLocation;
        public IToken stopLocation;

        public bool IsStatic()
        {
            return attrs.Contains("static");
        }

        public bool Equal(SourceSymbol sym)
        {
            return sym.name == name && sym.type == type;
        }
    }

    class SourceContext
    {
        public string srcFilePath;
        public ICharStream srcFileStream;
        public List<SourceSymbol> symbols = new(256);
    }

    class CodeListener : SdccBaseListener, IAntlrErrorListener<int>, IAntlrErrorListener<IToken>
    {
        private SourceContext sourceContext;

        public CodeListener(string srcFileName, ICharStream input)
        {
            sourceContext = new SourceContext
            {
                srcFilePath = srcFileName,
                srcFileStream = input
            };

            stack.Push(ParserStatus.InGlobal);
        }

        public SourceContext SourceContext
        {
            get {
                return sourceContext;
            }
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
                throw new Exception("Internal State Error");
            }

            // parse global vars
            if (stack.Peek() == ParserStatus.InGlobal)
            {
                List<string> vAttrList = new();
                List<string> vNameList = new();
                List<string> vRefeList = new();

                var declSpec = context.declarationSpecifiers();

                if (declSpec == null)
                    return; // skip other declare type

                foreach (var declaration in declSpec.declarationSpecifier())
                {
                    // Store Class
                    {
                        var spec = declaration.storageClassSpecifier();

                        if (spec != null)
                        {
                            var text = spec.GetText();

                            if (text != "typedef")
                            {
                                vAttrList.Add(text);
                            }
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

                    // skip some type
                    {
                        var spec = declaration.typeSpecifier();
                        if (spec != null)
                        {
                            var structSpec = spec.structOrUnionSpecifier();
                            var enumSpec = spec.enumSpecifier();

                            if (structSpec != null && structSpec.structDeclarationList() != null)
                            {
                                // it's a struct type declare, skip it
                                return;
                            }

                            else if (enumSpec != null && enumSpec.enumeratorList() != null)
                            {
                                // it's a enum type declare, skip it
                                return;
                            }
                        }
                    }
                }

                var initDeclCtx = context.initDeclaratorList();

                if (initDeclCtx == null)
                    return; // it's not a var declare, skip

                foreach (var item in initDeclCtx.initDeclarator())
                {
                    var decl = item.declarator().directDeclarator();

                    if (decl.LeftParen() != null && decl.RightParen() != null)
                        continue; // it's a function decl, skip

                    var initializerCtx = item.initializer();

                    if (initializerCtx != null) // parse var references
                    {
                        var baseExprLi = FindChild<SdccParser.PrimaryExpressionContext>(initializerCtx);

                        foreach (var exprCtx in baseExprLi)
                        {
                            var idf = exprCtx.Identifier();

                            if (idf != null)
                            {
                                vRefeList.Add(idf.GetText());
                            }
                        }
                    }

                    vNameList.Add(GetIdentifierFromDirectDeclarator(decl).GetText());
                }

                if (vNameList.Count > 0)
                {
                    foreach (var name in vNameList)
                    {
                        sourceContext.symbols.Add(new SourceSymbol
                        {
                            name = name,
                            type = SymbolType.Variable,
                            attrs = vAttrList.ToArray(),
                            refers = vRefeList.ToArray(),
                            startLocation = context.Start,
                            stopLocation = context.Stop
                        });
                    }
                }
            }
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

        private ITerminalNode GetIdentifierFromDirectDeclarator(SdccParser.DirectDeclaratorContext ctx)
        {
            if (ctx.Identifier() != null)
            {
                return ctx.Identifier();
            }

            else if (ctx.declarator() != null)
            {
                return GetIdentifierFromDirectDeclarator(ctx.declarator().directDeclarator());
            }

            else if (ctx.directDeclarator() != null)
            {
                return GetIdentifierFromDirectDeclarator(ctx.directDeclarator());
            }

            else
            {
                throw new Exception("Internal Error In: 'getIdentifierFromDirectDeclarator'");
            }
        }

        public override void EnterStatement([NotNull] SdccParser.StatementContext context)
        {
            stack.Push(ParserStatus.InStatement);
        }

        public override void ExitStatement([NotNull] SdccParser.StatementContext context)
        {
            Program.debug(string.Format("[Statement]: {0}",
                sourceContext.srcFileStream.GetText(Interval.Of(context.Start.StartIndex, context.Stop.StartIndex))));

            if (stack.Pop() != ParserStatus.InStatement)
            {
                throw new Exception("Internal State Error");
            }
        }

        public override void EnterFunctionDefinition([NotNull] SdccParser.FunctionDefinitionContext context)
        {
            stack.Push(ParserStatus.InFunctionDefine);
        }

        public override void ExitFunctionDefinition([NotNull] SdccParser.FunctionDefinitionContext context)
        {
            Program.debug(string.Format("[end] [FunctionDefinition]: {0}",
                sourceContext.srcFileStream.GetText(Interval.Of(context.Start.StartIndex, context.Stop.StartIndex))));

            if (stack.Pop() != ParserStatus.InFunctionDefine)
            {
                throw new Exception("Internal State Error");
            }
        }

        public void SyntaxError(TextWriter output, IRecognizer recognizer, int offendingSymbol, int line, int charPositionInLine, string msg, RecognitionException e)
        {
            throw new Exception(string.Format("\"{0}\":{1},{2}: LexerError: {3}", sourceContext.srcFilePath, line, charPositionInLine, msg));
        }

        public void SyntaxError(TextWriter output, IRecognizer recognizer, IToken offendingSymbol, int line, int charPositionInLine, string msg, RecognitionException e)
        {
            throw new Exception(string.Format("\"{0}\":{1},{2}: SyntaxError: {3}", sourceContext.srcFilePath, line, charPositionInLine, msg));
        }
    }
}
