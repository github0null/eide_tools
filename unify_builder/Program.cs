using ConsoleTableExt;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CommandLine;
using CommandLine.Text;
using DotNet.Globbing;

// 有关程序集的一般信息由以下
// 控制。更改这些特性值可修改
// 与程序集关联的信息。
//[assembly: AssemblyTitle("unify_builder")]
//[assembly: AssemblyDescription("unify code builder for embedded ide")]
//[assembly: AssemblyConfiguration("")]
//[assembly: AssemblyCompany("em-ide.com")]
//[assembly: AssemblyProduct("unify_builder")]
//[assembly: AssemblyCopyright("Copyright © em-ide.com 2022 all right reserved")]
//[assembly: AssemblyTrademark("")]
//[assembly: AssemblyCulture("")]

// 将 ComVisible 设置为 false 会使此程序集中的类型
//对 COM 组件不可见。如果需要从 COM 访问此程序集中的类型
//请将此类型的 ComVisible 特性设置为 true。
//[assembly: ComVisible(false)]

// 如果此项目向 COM 公开，则下列 GUID 用于类型库的 ID
//[assembly: Guid("c3ae1aa9-c6fe-4e51-919c-923bcdb3f67b")]

// 程序集的版本信息由下列四个值组成: 
//
//      主版本
//      次版本
//      生成号
//      修订号
//
// 可以指定所有值，也可以使用以下所示的 "*" 预置版本号和修订号
// 方法是按如下所示使用“*”: :
// [assembly: AssemblyVersion("1.0.*")]
//[assembly: AssemblyVersion("3.0.0.0")]
//[assembly: AssemblyFileVersion("2.0.0.0")]

namespace unify_builder
{
    class Utility
    {
        public delegate TargetType MapCallBk<Type, TargetType>(Type element);

        public static TargetType[] map<Type, TargetType>(IEnumerable<Type> iterator, MapCallBk<Type, TargetType> callBk)
        {
            List<TargetType> res = new(16);

            foreach (var item in iterator)
            {
                res.Add(callBk(item));
            }

            return res.ToArray();
        }

        public static T getJsonVal<T>(JObject jobj, string key, T defVal)
        {
            if (!jobj.ContainsKey(key)) return defVal;
            return jobj[key].Value<T>();
        }

        public static T[] getJsonArray<T>(JObject jobj, string key, T[] defVal = null) where T : class
        {
            if (!jobj.ContainsKey(key)) return defVal;
            return jobj[key].Values<T>().ToArray();
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
            List<string> pList = new(256);

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

        /// <summary>
        /// 相对路径转换
        /// </summary>
        /// <param name="root_"></param>
        /// <param name="targetPath_"></param>
        /// <param name="useUnixPath"></param>
        /// <returns>返回相对路径，比如：abc\def。如果失败则返回null</returns>
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

            List<string> rePath = new(256);

            // push parent path '..'
            for (int i = 0; i < rootList.Length - comLen; i++)
                rePath.Add("..");

            // push base path
            for (int i = comLen; i < targetList.Length; i++)
                rePath.Add(targetList[i]);

            return String.Join(DIR_SEP, rePath);
        }

        // convert JObject to 'string' or 'IEnumerable<string>'
        public static object getJObjectVal(JToken jobj)
        {
            object paramsValue;

            switch (jobj.Type)
            {
                case JTokenType.String:
                    paramsValue = jobj.Value<string>();
                    break;
                case JTokenType.Boolean:
                    paramsValue = jobj.Value<bool>() ? "true" : "false";
                    break;
                case JTokenType.Integer:
                case JTokenType.Float:
                    paramsValue = jobj.Value<object>().ToString();
                    break;
                case JTokenType.Array:
                    paramsValue = jobj.Values<string>();
                    break;
                default:
                    paramsValue = null;
                    break;
            }

            return paramsValue;
        }
    }

    class OsInfo
    {
        private static OsInfo _instance = null;

        public string OsType { get; }

        public string CRLF { get; }

        public int SysCmdLenLimit { get; }

        private OsInfo()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                OsType = "win32";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                OsType = "linux";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                OsType = "osx";
            }
            else
            {
                OsType = "freebsd";
            }

            CRLF = OsType == "win32" ? "\r\n" : "\n";

            if (OsType == "win32")
                SysCmdLenLimit = 8 * 1024;
            else
                SysCmdLenLimit = 32 * 1024;
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

    class CmdGenerator
    {
        public struct GeneratorOption
        {
            public string bindirEnvName;
            public string bindirAbsPath;
            public string outpath;
            public string cwd;
            public bool testMode;
            public string compiler_prefix;
            public Dictionary<string, string> srcParams;
            public bool outDirTree; // output dir tree
        };

        public class CmdInfo
        {
            public string exePath;          // [required] executable file full path
            public string commandLine;      // [required] 传递给 exePath 的命令行。取值根据编译器的命令行模式而有所不同
                                            //      当编译器从命令行读取参数时，该值与sourceArgs相同，
                                            //      当编译器从文件中读取参数时，该值为编译器要求的格式，比如：--Via <filepath> ...
            public string sourcePath;       // [required] 对于编译器来说，值为源文件的绝对路径; 对于链接器来说，该值无意义，暂时用作存放map文件的路径
            public string sourceArgs;       // [required] c/c++/asm 源文件的完整编译参数。
            public string sourceType;       // [required] 源文件类型，取值：'c', 'cpp', 'asm', or 'other'
            public string outPath;          // [required] 输出文件的完整路径（绝对路径）
            public Encoding outputEncoding; // [required] 命令行的字符集编码：UTF8/GBK/...
            public bool sourceArgsChanged;  // [required] 指示自从上次编译后编译参数是否改变，用于确定是否需要重新编译
            public string baseArgs;         // [optional] 基础的编译参数，不含有 -o，-MMD 等带有输出文件路径的参数

            public string compilerId;       // [optional] 编译器id（小写），比如：'gcc', 'sdcc'
            public string compilerModel;    // [optional] 编译器的类型名字，比如：'c', 'cpp', 'c/cpp', 'asm', 'asm-clang', 'linker' ...

            public string title;            // [optional] a title for this command
            public string shellCommand;     // [optional] shell command which will be invoke compiler, used to gen 'compile_commands.json'
            public string[] outputs;        // [optional] if output more than one files, use this field
            public string argsForSplitter;  // [optional] compiler args for 'source_splitter' tool

            public string sdcc_bundleLibArgs; // [specific] just for SDCC compiler
        };

        public class LinkerExCmdInfo : CmdInfo
        {
            public string type;
        };

        class TypeErrorException : Exception
        {
            public TypeErrorException(string msg) : base(msg)
            {
            }
        };

        public delegate void CmdVisitor(string key, string cmdLine);

        class CmdFormat
        {
            public string prefix = "";
            public string body = null;
            public string body_noval = null; // 适用于宏定义没有值的情况下，指定格式字符串
            public string suffix = "";
            public string sep = " ";
            public bool noQuotes = false;
        };

        class InvokeFormat
        {
            public bool useFile = false;
            public string body = "${value}";
        };

        public static readonly string optionKey = "options";
        public static readonly string[] formatKeyList = {
            "$includes", "$defines", "$libs"
        };

        private readonly Dictionary<string, Encoding> encodings = new(8);

        private readonly Dictionary<string, string[]> baseOpts = new(512); // base compiler options
        private readonly Dictionary<string, string[]> userOpts = new(512); // user compiler options

        private readonly Dictionary<string, JObject> paramObj = new(8);
        private readonly Dictionary<string, JObject> models = new(8);

        private readonly Dictionary<string, Dictionary<string, CmdFormat>> formats = new(16);
        private readonly Dictionary<string, InvokeFormat> invokeFormats = new(16);

        private readonly string toolPrefix; // compiler prefix, like: arm-none-eabi-
        private readonly string toolId;     // compiler ID

        private readonly bool useUnixPath; // whether use unix path in compiler options
        private readonly bool outDirTree;  // whether generate a tree struct in build folder

        private readonly string outDir; // output root folder
        private readonly string binDir; // compiler root folder (with tail '/'), default: '%TOOL_DIR%/'
        private readonly string cwd;    // project root folder

        private readonly string compilerAttr_commandPrefix;      // the compiler options prefix
        private readonly string compilerAttr_directorySeparator;   // the path-sep for compiler options

        private readonly bool compilerAttr_sdcc_module_split = false; // one-module-per-function for sdcc

        private readonly JObject model;         // compiler model obj
        private readonly JObject parameters;    // builder.params obj

        public string compilerName { get; }     // complier short name
        public string compilerVersion { get; }  // compiler version string, like: '5.06 update 6 (build 750)'
        public string compilerFullName { get; } // compiler full name (contain version string)

        private readonly Dictionary<string, int> objNameMap = new(512);
        private readonly Dictionary<string, string> srcParams = new(512);

        public string asmCompilerName;  // asm compiler type we used

        public CmdGenerator(JObject cModel, JObject cParams, GeneratorOption option)
        {
            model = cModel;
            parameters = cParams;
            outDir = option.outpath;
            // 注意：为什么不将变量 %TOOL_DIR% 替换为实际的值？历史原因，某些情况下使用cmd执行命令，使用环境变量可以避免路径超出长度
            binDir = option.bindirEnvName != null ? (option.bindirEnvName + Path.DirectorySeparatorChar) : "";
            cwd = option.cwd;
            compilerAttr_commandPrefix = option.compiler_prefix;
            srcParams = option.srcParams;
            outDirTree = option.outDirTree;

            toolId = cModel["id"].Value<string>();
            useUnixPath = cModel.ContainsKey("useUnixPath") ? cModel["useUnixPath"].Value<bool>() : false;
            compilerAttr_directorySeparator = useUnixPath ? "/" : Path.DirectorySeparatorChar.ToString();

            // init compiler params
            JObject compileOptions = (JObject)cParams[optionKey];
            paramObj.Add("global", compileOptions.ContainsKey("global") ? (JObject)compileOptions["global"] : new JObject());
            paramObj.Add("c", compileOptions.ContainsKey("c/cpp-compiler") ? (JObject)compileOptions["c/cpp-compiler"] : new JObject());
            paramObj.Add("cpp", compileOptions.ContainsKey("c/cpp-compiler") ? (JObject)compileOptions["c/cpp-compiler"] : new JObject());
            paramObj.Add("asm", compileOptions.ContainsKey("asm-compiler") ? (JObject)compileOptions["asm-compiler"] : new JObject());
            paramObj.Add("linker", compileOptions.ContainsKey("linker") ? (JObject)compileOptions["linker"] : new JObject());

            // init compiler models
            string cCompilerName = ((JObject)cModel["groups"]).ContainsKey("c/cpp") ? "c/cpp" : "c";
            string cppCompilerName = ((JObject)cModel["groups"]).ContainsKey("c/cpp") ? "c/cpp" : "cpp";
            string linkerName = getUserSpecifiedModelName("linker");

            if (!((JObject)cModel["groups"]).ContainsKey(cCompilerName))
                throw new Exception("Not found c compiler model");

            if (!((JObject)cModel["groups"]).ContainsKey(cppCompilerName))
                throw new Exception("Not found cpp compiler model");

            if (!((JObject)cModel["groups"]).ContainsKey(linkerName))
                throw new Exception("Invalid '$use' option, please check compile option 'linker.$use'");

            models.Add("c", (JObject)cModel["groups"][cCompilerName]);
            models.Add("cpp", (JObject)cModel["groups"][cppCompilerName]);
            models.Add("linker", (JObject)cModel["groups"][linkerName]);

            // init asm compiler models and params
            asmCompilerName = getUserSpecifiedModelName("asm");

            if (asmCompilerName == "asm-auto")
            {
                models.Add("asm", (JObject)cModel["groups"]["asm"]);

                foreach (var item in (JObject)cModel["groups"])
                {
                    if (item.Key.StartsWith("asm-"))
                    {
                        models.Add(item.Key, (JObject)cModel["groups"][item.Key]);
                        paramObj.Add(item.Key, paramObj["asm"]);
                    }
                }
            }
            else
            {
                if (!((JObject)cModel["groups"]).ContainsKey(asmCompilerName))
                    throw new Exception("Invalid '$use' option, please check compile option 'asm-compiler.$use'");

                models.Add("asm", (JObject)cModel["groups"][asmCompilerName]);
            }

            // init command line from model
            JObject globalParams = paramObj["global"];

            // set tool path prefix
            {
                if (globalParams.ContainsKey("toolPrefix"))
                {
                    toolPrefix = globalParams["toolPrefix"].Value<string>();
                }
                else if (cModel.ContainsKey("toolPrefix"))
                {
                    toolPrefix = cModel["toolPrefix"].Value<string>();
                }
                else
                {
                    toolPrefix = "";
                }
            }

            // set executable suffix
            {
                JObject linkerParams = paramObj["linker"];

                if (linkerParams.ContainsKey("elf-suffix"))
                {
                    models["linker"]["$outputSuffix"] = linkerParams["elf-suffix"].Value<string>();
                }
            }

            // replace tool prefix
            foreach (var key in models.Keys)
            {
                string oldName = models[key]["$path"].Value<string>();
                models[key]["$path"] = oldName.Replace("${toolPrefix}", toolPrefix);
            }

            // replace tool name
            foreach (var key in models.Keys)
            {
                if (paramObj[key].ContainsKey("$toolName"))
                {
                    string tName = paramObj[key]["$toolName"].Value<string>();
                    string oldName = models[key]["$path"].Value<string>();
                    models[key]["$path"] = oldName.Replace("${toolName}", tName);
                }

                else if (models[key].ContainsKey("$defToolName"))
                {
                    string tName = models[key]["$defToolName"].Value<string>();
                    string oldName = models[key]["$path"].Value<string>();
                    models[key]["$path"] = oldName.Replace("${toolName}", tName);
                }
            }

            // set object suffix
            if (paramObj["linker"].ContainsKey("$objectSuffix"))
            {
                string objSuffix = paramObj["linker"]["$objectSuffix"].Value<string>().Trim();

                if (!string.IsNullOrEmpty(objSuffix))
                {
                    string[] baseList = { "c", "cpp", "asm" };

                    foreach (var key in baseList)
                    {
                        if (models[key].ContainsKey("$outputSuffix"))
                            models[key]["$outputSuffix"] = objSuffix;
                    }

                    foreach (var key in models.Keys)
                    {
                        if (key.StartsWith("asm-"))
                        {
                            if (models[key].ContainsKey("$outputSuffix"))
                                models[key]["$outputSuffix"] = objSuffix;
                        }
                    }
                }
            }

            // try to get compiler fullname and version
            {
                // init default value
                compilerName = getModelName();
                compilerVersion = string.Empty;
                compilerFullName = compilerName;

                // parse from compiler
                try
                {
                    string exePath = option.bindirAbsPath + Path.DirectorySeparatorChar + getActivedRawToolPath("c");
                    JObject vMatcher = this.getToolchainVersionMatcher();

                    if (vMatcher != null)
                    {
                        var matcher = new Regex(vMatcher["matcher"].Value<string>(), RegexOptions.IgnoreCase);
                        int eCode = Program.runExe(exePath, vMatcher["args"].Value<string>(), out string output);

                        // ignore exit code for keil_c51 compiler
                        if (getCompilerId() == "KEIL_C51" ||
                            getCompilerId() == "COSMIC_STM8")
                        {
                            eCode = Program.CODE_DONE;
                        }

                        if (eCode == Program.CODE_DONE && !String.IsNullOrWhiteSpace(output))
                        {
                            string[] lines = Program.CRLFMatcher.Split(output);

                            foreach (var line in lines)
                            {
                                var res = matcher.Match(line);

                                if (res.Success && res.Groups.Count > 0)
                                {
                                    if (res.Groups["name"] != null)
                                    {
                                        compilerName = res.Groups["name"].Value;
                                    }

                                    if (res.Groups["version"] != null)
                                    {
                                        compilerVersion = res.Groups["version"].Value;
                                    }

                                    // full name is the full matched txt
                                    compilerFullName = line.Trim();

                                    break; // found it, exit
                                }
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    // nothing todo
                }

                // if compiler name is empty, use default name
                if (string.IsNullOrWhiteSpace(compilerName))
                {
                    compilerName = getModelName();
                }
            }

            // init params for other spec compiler
            {
                if (toolId == "SDCC")
                {
                    if (paramObj["global"].ContainsKey("$one-module-per-function"))
                    {
                        compilerAttr_sdcc_module_split = false; // @disabled paramObj["global"]["$one-module-per-function"].Value<bool>();
                    }

                    // check sdcc version, must > v4.x.x
                    if (compilerAttr_sdcc_module_split && !string.IsNullOrEmpty(compilerVersion))
                    {
                        if (!Regex.IsMatch(compilerVersion, @"^([4-9]|[1-9]\d+)\."))
                        {
                            var msg = string.Format("In module split mode, sdcc version must >= 'v4.x.x', now is '{0}' !", compilerVersion);
                            throw new Exception(msg);
                        }
                    }
                }
            }

            // set encodings
            foreach (string modelName in models.Keys)
            {
                if (models[modelName].ContainsKey("$encoding"))
                {
                    string codeName = models[modelName]["$encoding"].Value<string>();
                    switch (codeName)
                    {
                        case "UTF8":
                            encodings.Add(modelName, RuntimeEncoding.instance().UTF8);
                            break;
                        default:
                            encodings.Add(modelName, getEncoding(codeName));
                            break;
                    }
                }
                else
                {
                    // force set encoding for gcc 1x.x
                    /*if (getModelID().Contains("GCC") &&
                        Regex.IsMatch(compilerVersion, @"^[1-9]\d+\."))
                    {
                        encodings.Add(modelName, RuntimeEncoding.instance().UTF8);
                    }

                    // other's is ANSI
                    else*/
                    {
                        encodings.Add(modelName, RuntimeEncoding.instance().Default);
                    }
                }
            }

            // set include, define commands format
            foreach (string modelName in models.Keys)
            {
                JObject modelParams = models[modelName];

                Dictionary<string, CmdFormat> properties = new(32);
                foreach (string key in formatKeyList)
                {
                    if (modelParams.ContainsKey(key))
                    {
                        properties.Add(key, modelParams[key].ToObject<CmdFormat>());
                    }
                }
                formats.Add(modelName, properties);

                // invoker mode
                InvokeFormat invokeFormat;

                if (modelParams.ContainsKey("$invoke"))
                    invokeFormat = modelParams["$invoke"].ToObject<InvokeFormat>();
                else
                    invokeFormat = new InvokeFormat();

                if (option.testMode) invokeFormat.useFile = false;
                invokeFormats.Add(modelName, invokeFormat);
            }

            // set outName to unique
            getUniqueName(getOutName());

            // set stable compiler options
            foreach (var model in models)
            {
                string name = model.Key;
                JObject cmpModel = model.Value;

                JObject[] cmpParams = {
                    globalParams,
                    paramObj[name]
                };

                var baseOptLi = new List<string>(64);
                var userOptLi = new List<string>(256);

                // set default options
                if (cmpModel.ContainsKey("$default"))
                {
                    foreach (var ele in ((JArray)cmpModel["$default"]).Values<string>())
                        baseOptLi.Add(ele);
                }

                // set include path and defines for c/c++/asm compiler
                if (name != "linker")
                {
                    // include list
                    var incOpts = getIncludesCmdLine(name, ((JArray)cParams["incDirs"]).Values<string>());
                    if (!string.IsNullOrEmpty(incOpts)) userOptLi.Add(incOpts);

                    // macro list
                    var defOpts = getdefinesCmdLine(name, ((JArray)cParams["defines"]).Values<string>());
                    if (!string.IsNullOrEmpty(defOpts)) userOptLi.Add(defOpts);
                }
                // set lib search folders for linker
                else
                {
                    string command = getLibSearchFolders(name, ((JArray)cParams["libDirs"]).Values<string>());

                    if (!string.IsNullOrEmpty(command))
                    {
                        baseOptLi.Add(command);
                    }
                }

                // merge user compiler options
                foreach (var ele in cmpModel)
                {
                    // skip built-in args
                    if (ele.Key[0] == '$') continue;

                    try
                    {
                        object paramsValue = mergeParamsList(cmpParams, ele.Key, ele.Value["type"].Value<string>());

                        string cmd = getCommandValue((JObject)ele.Value, paramsValue).Trim();

                        if (!string.IsNullOrEmpty(cmd))
                        {
                            userOptLi.Add(cmd);
                        }
                    }
                    catch (TypeErrorException err)
                    {
                        throw new Exception("Error field type for '" + ele.Key[0] + "'", err);
                    }
                    catch (Exception err)
                    {
                        throw new Exception("Init command failed: '" + name + "', Key: '" + ele.Key + "' !, " + err.Message);
                    }
                }

                if (cmpModel.ContainsKey("$default-tail"))
                {
                    foreach (var ele in ((JArray)cmpModel["$default-tail"]).Values<string>())
                        userOptLi.Add(ele);
                }

                // format ${var} variables in string
                formatVarInCompilerOptions(cmpModel, baseOptLi, cmpParams);
                formatVarInCompilerOptions(cmpModel, userOptLi, cmpParams);

                baseOpts.Add(name, baseOptLi.ToArray());
                userOpts.Add(name, userOptLi.ToArray());
            }
        }

        public bool IsUseSdccModuleOptimizer
        {
            get {
                return compilerAttr_sdcc_module_split;
            }
        }

        public CmdInfo fromCFile(string fpath, bool onlyCmd = false)
        {
            return fromModel("c", "language-c", fpath, onlyCmd);
        }

        public CmdInfo fromCppFile(string fpath, bool onlyCmd = false)
        {
            return fromModel("cpp", "language-cpp", fpath, onlyCmd);
        }

        private void formatVarInCompilerOptions(JObject model, List<string> opts, JObject[] userParams)
        {
            var matcher = new Regex(@"\$\{([^\}]+)\}", RegexOptions.IgnoreCase | RegexOptions.Compiled);

            for (int i = 0; i < opts.Count; i++)
            {
                Match mList = matcher.Match(opts[i]);

                if (mList.Success && mList.Groups.Count > 1)
                {
                    for (int mIndex = 1; mIndex < mList.Groups.Count; mIndex++)
                    {
                        string key = mList.Groups[mIndex].Value;

                        if (!model.ContainsKey(key)) continue;

                        try
                        {
                            JObject field = (JObject)model[key];
                            object paramsVal = mergeParamsList(userParams, key, field["type"].Value<string>());
                            string cmdStr = getCommandValue(field, paramsVal);
                            opts[i] = opts[i].Replace("${" + key + "}", cmdStr);
                        }
                        catch (Exception)
                        {
                            // ignore log
                        }
                    }
                }
            }
        }

        private bool isArmGnuAsmFile(string fpath)
        {
            try
            {
                int lineLimit = 200; // max line we will detect

                foreach (var line_ in File.ReadLines(fpath))
                {
                    string line = line_.Trim();

                    // skip empty line
                    if (String.IsNullOrWhiteSpace(line))
                        continue;

                    // it's a armasm comment syntax
                    if (line.StartsWith(";"))
                        return false;

                    // it's a arm gnu comment syntax
                    if (line.StartsWith("/*") || line.StartsWith("//"))
                        return true;

                    // it's gnu asm label
                    if (Regex.IsMatch(line, @"^\s*\.(?:syntax|arch|section|global)\s+"))
                        return true;

                    // line limit
                    if (--lineLimit <= 0)
                        break;
                }
            }
            catch (Exception)
            {
                // nothing todo
            }

            return false;
        }

        public CmdInfo fromAsmFile(string fpath, bool onlyCmd = false)
        {
            string asmType = "asm";

            if (asmCompilerName == "asm-auto")
            {
                if (toolId == "AC6")
                {
                    if (isArmGnuAsmFile(fpath))
                    {
                        asmType = "asm-clang";
                    }
                }
            }

            return fromModel(asmType, null, fpath, onlyCmd);
        }

        public List<string> getMapMatcher()
        {
            JObject linkerModel = models["linker"];
            return linkerModel.ContainsKey("$matcher")
                ? new List<string>(linkerModel["$matcher"].Values<string>()) : new List<string>();
        }

        public Regex getRamSizeMatcher()
        {
            JObject linkerModel = models["linker"];
            return linkerModel.ContainsKey("$ramMatcher")
                ? new Regex(linkerModel["$ramMatcher"].Value<string>(), RegexOptions.IgnoreCase) : null;
        }

        public Regex getRomSizeMatcher()
        {
            JObject linkerModel = models["linker"];
            return linkerModel.ContainsKey("$romMatcher")
                ? new Regex(linkerModel["$romMatcher"].Value<string>(), RegexOptions.IgnoreCase) : null;
        }

        public string getLinkerLibFlags()
        {
            JObject linkerModel = models["linker"];
            JObject linkerParams = paramObj["linker"];
            return linkerModel.ContainsKey("$LIB_FLAGS") && linkerParams.ContainsKey("LIB_FLAGS")
                ? getCommandValue((JObject)linkerModel["$LIB_FLAGS"], Utility.getJObjectVal(linkerParams["LIB_FLAGS"])) : "";
        }

        public string getUserSpecifiedModelName(string modelName)
        {
            if (!paramObj[modelName].ContainsKey("$use"))
                return modelName;

            var name = paramObj[modelName]["$use"].Value<string>();

            if (string.IsNullOrWhiteSpace(name))
                return modelName;

            if (name.StartsWith(modelName + "-") || name == modelName)
                return name;

            return modelName + '-' + name;
        }

        public CmdInfo genLinkCommand(string[] objList, bool cliTestMode = false)
        {
            JObject linkerModel = models["linker"];
            JObject linkerParams = paramObj["linker"];
            InvokeFormat iFormat = invokeFormats["linker"];
            string sep = (iFormat.useFile && !cliTestMode) ? "\r\n" : " ";

            string outSuffix = linkerModel.ContainsKey("$outputSuffix")
                ? linkerModel["$outputSuffix"].Value<string>() : ".axf";

            string mapSuffix = linkerModel.ContainsKey("$mapSuffix")
                ? linkerModel["$mapSuffix"].Value<string>() : ".map";

            string cmdLocation = linkerModel.ContainsKey("$commandLocation")
                ? linkerModel["$commandLocation"].Value<string>() : "start";

            string objSep = linkerModel.ContainsKey("$objPathSep")
                ? linkerModel["$objPathSep"].Value<string>() : "\r\n";

            bool checkEntryOrderForSdcc = linkerModel.ContainsKey("$mainFirst")
                ? linkerModel["$mainFirst"].Value<bool>() : false;

            string lib_flags = getLinkerLibFlags();

            string outElfName = getOutName();

            string compilerId = getCompilerId();

            //--

            // For SDCC, bundled *.rel files as a *.lib file
            // ref: https://sourceforge.net/p/sdcc/discussion/1865/thread/e395ff7a42/#a03e
            // cmd: sdar -rcv ${out} ${in}
            string sdcc_bundleLibArgs = null;
            if (!cliTestMode && compilerId == "SDCC")
            {
                List<string> sourcesObjs = new(objList);
                List<string> finalObjsLi = new(128); // must be absolute path
                List<string> bundledList = new(128); // must be relative path

                // make entry src file at the first of cli args
                if (checkEntryOrderForSdcc)
                {
                    string mainName = linkerParams.ContainsKey("$mainFileName")
                        ? linkerParams["$mainFileName"].Value<string>() : "main";

                    int index = sourcesObjs.FindIndex((string fName) => Path.GetFileNameWithoutExtension(fName).Equals(mainName));

                    if (index != -1)
                    {
                        finalObjsLi.Add(sourcesObjs[index]);
                        sourcesObjs.RemoveAt(index);
                    }
                    else
                    {
                        throw new Exception("Not found '"
                            + mainName + ".rel' object file in output list, the '"
                            + mainName + ".rel' object file must be the first object file !");
                    }
                }

                // split objs
                foreach (string objPath in sourcesObjs)
                {
                    if (objPath.EndsWith(".lib") || objPath.EndsWith(".a") || Program.cliArgs.SdccNotBundleRel)
                        finalObjsLi.Add(objPath);
                    else
                        bundledList.Add(toRelativePathForCompilerArgs(objPath));
                }

                // don't link empty 'no_entry_bundled.lib'
                if (bundledList.Count > 0)
                {
                    string bundledFullOutPath = outDir + Path.DirectorySeparatorChar + "no_entry_bundled.lib";
                    string bundledOutPath = toRelativePathForCompilerArgs(bundledFullOutPath);

                    string cliStr = "-rc ${out} ${in}"
                        .Replace("${out}", bundledOutPath)
                        .Replace("${in}", string.Join(" ", bundledList));

                    // dump cli args for user
                    string cliArgsPath = Path.ChangeExtension(bundledFullOutPath, ".args.txt");
                    if (string.IsNullOrEmpty(cliArgsPath)) throw new Exception("cannot generate '.args.txt' for: " + bundledFullOutPath);
                    File.WriteAllText(cliArgsPath, cliStr, encodings["linker"]);

                    // del old .lib file
                    if (File.Exists(bundledFullOutPath)) File.Delete(bundledFullOutPath);

                    // make bundled lib
                    sdcc_bundleLibArgs = cliStr;
                    int exitCode = Program.runExe(getOtherUtilToolFullPath("linker-lib"), cliStr,
                        out string log, null, Program.cliArgs.DryRun);
                    if (exitCode != Program.CODE_DONE)
                        throw new Exception("bundled lib file failed, exit code: " + exitCode + ", msg: " + log);

                    // append to linker obj list
                    finalObjsLi.Add(bundledFullOutPath);
                }

                // set real obj list
                objList = finalObjsLi.ToArray();
            }

            //--

            string outName = outDir + Path.DirectorySeparatorChar + outElfName;
            string outPath = outName + outSuffix;
            string mapPath = outName + mapSuffix;
            string stableCommand = string.Join(" ", baseOpts["linker"].Concat(userOpts["linker"]));
            string cmdLine = compilerAttr_commandPrefix;

            // ARM Compiler 6 on macOS only.
            // ref: https://github.com/ARM-software/vscode-environment-manager/issues/6
            if (compilerId == "AC6" && OsInfo.instance().OsType == "osx")
            {
                cmdLine += " --lto_liblto_location=%TOOL_DIR%/bin/libLTO.dylib ";
            }

            if (cmdLocation == "start")
            {
                cmdLine += stableCommand;

                if (linkerModel.ContainsKey("$linkMap"))
                {
                    cmdLine += sep + getCommandValue((JObject)linkerModel["$linkMap"], "")
                        .Replace("${mapPath}", toRelativePathForCompilerArgs(mapPath));
                }
            }

            for (int i = 0; i < objList.Length; i++)
            {
                objList[i] = toRelativePathForCompilerArgs(objList[i]);
            }

            if (!cliTestMode)
            {
                cmdLine += sep + linkerModel["$output"].Value<string>()
                    .Replace("${out}", toRelativePathForCompilerArgs(outPath))
                    .Replace("${in}", string.Join(objSep, objList.ToArray()))
                    .Replace("${lib_flags}", lib_flags);
            }

            if (cmdLocation == "end")
            {
                if (linkerModel.ContainsKey("$linkMap"))
                {
                    cmdLine += sep + getCommandValue((JObject)linkerModel["$linkMap"], "")
                        .Replace("${mapPath}", toRelativePathForCompilerArgs(mapPath));
                }

                cmdLine += " " + stableCommand;
            }

            // expand args in files
            cmdLine = expandArgs(cmdLine);

            // repleace eide cmd vars
            string reOutDir = toRelativePathForCompilerArgs(outDir, false, false);
            cmdLine = cmdLine
                .Replace("${OutName}", outElfName)
                .Replace("${OutDir}", reOutDir)
                .Replace("${outName}", outElfName)
                .Replace("${outDir}", reOutDir);

            // replace system env
            cmdLine = Program.replaceEnvVariable(cmdLine);

            // ---
            // For COSMIC STM8 clnk
            //  - We need put all objs into *.lkf files
            if (compilerId == "COSMIC_STM8" &&
                getUserSpecifiedModelName("linker") == "linker")
            {
                string usrLkfPath = null;

                if (linkerParams.ContainsKey("linker-script"))
                {
                    var jobj = linkerParams["linker-script"];

                    if (jobj.Type == JTokenType.Array)
                    {
                        var arr = jobj.Values<string>().ToArray();

                        if (arr.Length > 0)
                        {
                            usrLkfPath = arr[0];
                        }
                    }
                    else
                    {
                        usrLkfPath = jobj.Value<string>();
                    }
                }
                else if (linkerParams.ContainsKey("$lkfPath"))
                {
                    usrLkfPath = linkerParams["$lkfPath"].Value<string>();
                }

                if (string.IsNullOrEmpty(usrLkfPath))
                {
                    throw new Exception("Missing *.lkf file for COSMIC linker(clnk)");
                }

                string outLkfPath = outDir + Path.DirectorySeparatorChar + Path.GetFileNameWithoutExtension(usrLkfPath) + ".lkf";

                List<string> lkfLines = new(512);
                List<string> objFiles = new(objList);
                List<string> libFiles = new(new Regex(@"\s+").Split(lib_flags));

                // setup lib search path
                //  set CXLIB=C:\COSMIC\LIB
                Program.setEnvVariable("CXLIB", binDir + Path.DirectorySeparatorChar + "Lib");

                lkfLines.AddRange(new string[] {
                    "############################################",
                    "# Auto generated by EIDE (unify_builder)   #",
                    "############################################",
                    ""
                });

                // replace vars in lkf files:
                //  $<objs:pattern>
                //  $<libs:pattern>
                Regex patternMatcher = new(@"\$<(?<name>\w+)\:(?<glob>.*?)>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
                foreach (var input_ in File.ReadAllLines(usrLkfPath))
                {
                    string input = Program.replaceEnvVariable(input_);

                    while (true)
                    {
                        var m = patternMatcher.Match(input);

                        if (m.Success && m.Groups.Count > 2)
                        {
                            List<string> sources;

                            switch (m.Groups["name"].Value)
                            {
                                case "objs":
                                    sources = objFiles;
                                    break;
                                case "libs":
                                    sources = libFiles;
                                    break;
                                default:
                                    throw new Exception("Not support this pattern in lkf, pattern class: '" + m.Groups["name"].Value + "'");
                            }

                            List<string> results = new(128);

                            if (!string.IsNullOrEmpty(m.Groups["glob"].Value))
                            {
                                Glob filePattern = Glob.Parse(m.Groups["glob"].Value);

                                List<string> rmList = new(128);

                                foreach (var filepath in sources)
                                {
                                    if (filePattern.IsMatch(filepath.Replace("\"", "")))
                                    {
                                        results.Add(filepath);
                                        rmList.Add(filepath);
                                    }
                                }

                                sources.RemoveAll((p) => rmList.Contains(p));
                            }

                            // expand value
                            input = input.Replace(
                                m.Groups[0].Value,
                                string.Join(OsInfo.instance().CRLF, results));
                        }
                        else
                        {
                            break;
                        }
                    }

                    lkfLines.Add(input);
                }

                File.WriteAllLines(outLkfPath, lkfLines);

                cmdLine += " " + toRelativePathForCompilerArgs(outLkfPath);
            }

            //--

            var linkerRealArgs = cmdLine.Replace("\r\n", " ").Replace("\n", " ");
            FileInfo paramFile = new(outName + ".lnp");

            if (!cliTestMode)
            {
                FileInfo objliFile = new(outName + ".objlist");
                File.WriteAllText(objliFile.FullName, string.Join(objSep, objList.ToArray()), encodings["linker"]);
                File.WriteAllText(paramFile.FullName, cmdLine, encodings["linker"]);
            }

            string commandLine = null;

            if (iFormat.useFile)
                commandLine = iFormat.body.Replace("${value}", "\"" + paramFile.FullName + "\"");
            else
                commandLine = cmdLine;

            // rename old map file
            if (File.Exists(mapPath))
            {
                try
                {
                    File.WriteAllText(mapPath + ".old",
                        File.ReadAllText(mapPath),
                        RuntimeEncoding.instance().UTF8);
                }
                catch (Exception)
                {
                    // do nothing
                }
            }

            // gen .map.view for eide (yaml format)
            try
            {
                string[] cont = new string[] {
                    $"tool: {toolId}",
                    $"fileName: '{Path.GetFileName(mapPath)}'",
                    $"elfName: '{Path.GetFileName(outPath)}'",
                    $"compilerName: '{compilerName}'",
                    $"compilerFullName: '{compilerFullName}'",
                };

                File.WriteAllLines(outName + ".map.view", cont, RuntimeEncoding.instance().UTF8);
            }
            catch (Exception)
            {
                // do nothing
            }

            return new CmdInfo {
                compilerId = compilerId.ToLower(),
                compilerModel = "linker",
                exePath = getActivedToolFullPath("linker"),
                commandLine = commandLine,
                sourcePath = mapPath,
                sourceType = "other",
                sourceArgs = linkerRealArgs,
                outPath = outPath,
                outputEncoding = encodings["linker"],
                sdcc_bundleLibArgs = sdcc_bundleLibArgs
            };
        }

        public CmdInfo[] genOutputCommand(string linkerOutputFile, string[] excludes)
        {
            JObject linkerModel = models["linker"];
            List<CmdInfo> commandsList = new();

            // model file not support output .hex .bin .s19 files
            if (!linkerModel.ContainsKey("$outputBin"))
                return commandsList.ToArray();

            string outFileName = outDir + Path.DirectorySeparatorChar + getOutName();

            foreach (JObject outputModel in (JArray)linkerModel["$outputBin"])
            {
                string outFilePath = outFileName;

                string outFileSuffix = "";
                if (outputModel.ContainsKey("outputSuffix"))
                    outFileSuffix = outputModel["outputSuffix"].Value<string>();

                // don't generate some specific files
                if (excludes.Contains(outFileSuffix))
                    continue;

                outFilePath += outFileSuffix;

                string command = outputModel["command"].Value<string>()
                    .Replace("${linkerOutput}", toRelativePathForCompilerArgs(linkerOutputFile))
                    .Replace("${output}", toRelativePathForCompilerArgs(outFilePath));

                // replace system env
                command = Program.replaceEnvVariable(command, true);

                commandsList.Add(new CmdInfo {
                    title = outputModel["name"].Value<string>(),
                    exePath = toAbsToolPath(outputModel["toolPath"].Value<string>()),
                    commandLine = command,
                    sourcePath = linkerOutputFile,
                    sourceType = "other",
                    outPath = outFilePath,
                    outputEncoding = encodings["linker"]
                });
            }

            return commandsList.ToArray();
        }

        public LinkerExCmdInfo[] genLinkerExtraCommand(string linkerOutputFile)
        {
            JObject linkerModel = models["linker"];
            List<LinkerExCmdInfo> commandList = new();

            // not have Extra Command
            if (!linkerModel.ContainsKey("$extraCommand"))
                return commandList.ToArray();

            foreach (JObject model in (JArray)linkerModel["$extraCommand"])
            {
                string exePath = toAbsToolPath(model["toolPath"].Value<string>());

                string command = compilerAttr_commandPrefix + model["command"].Value<string>()
                    .Replace("${linkerOutput}", toRelativePathForCompilerArgs(linkerOutputFile));

                // replace system env
                command = Program.replaceEnvVariable(command, true);

                commandList.Add(new LinkerExCmdInfo {
                    title = model.ContainsKey("name") ? model["name"].Value<string>() : exePath,
                    type = model.ContainsKey("type") ? model["type"].Value<string>() : "",
                    exePath = exePath,
                    commandLine = command,
                    sourcePath = linkerOutputFile,
                    outPath = null
                });
            }

            return commandList.ToArray();
        }

        public string getOutName()
        {
            return parameters.ContainsKey("name") ? parameters["name"].Value<string>() : "main";
        }

        public string getBuildConfigName()
        {
            return parameters.ContainsKey("target") ? parameters["target"].Value<string>() : "Debug";
        }

        private string toAbsPathByProjectRoot(string path)
        {
            if (Utility.isAbsolutePath(path)) return path;
            return cwd + Path.DirectorySeparatorChar + path;
        }

        /// <summary>
        /// 将一个编译工具的相对路径转换为带有 编译器根目录变量 的绝对路径
        /// </summary>
        /// <param name="repath">相对路径</param>
        /// <returns>路径字符串，比如：%TOOL_DIR%\bin\gcc.exe</returns>
        private string toAbsToolPath(string repath)
        {
            return binDir + repath.Replace("${toolPrefix}", toolPrefix);
        }

        /// <summary>
        /// 获取model中的其他工具的绝对路径，比如 ar.exe (linker-lib)
        /// </summary>
        /// <remarks></remarks>
        /// <param name="name">工具的代号，比如：linker-lib</param>
        /// <returns>路径字符串（路径中的所有变量已被替换），比如：c:\aa\bb\cc\bin\ar.exe</returns>
        public string getOtherUtilToolFullPath(string name)
        {
            string path = model["groups"][name]["$path"].Value<string>()
                .Replace("${toolPrefix}", toolPrefix);
            return Program.replaceEnvVariable(binDir + path);
        }

        /// <summary>
        /// 检查model中是否存在某个编译工具
        /// </summary>
        /// <remarks>不要去检查 c, cpp, asm, linker 是否存在，这些是必选的工具，一定是存在的</remarks>
        /// <param name="name"></param>
        /// <returns>是否存在</returns>
        public bool hasOtherUtilTool(string name)
        {
            return ((JObject)model["groups"]).ContainsKey(name);
        }

        /// <summary>
        /// 获取当前活动的编译工具路径
        /// </summary>
        /// <param name="name">可选的值有：c, cpp, asm, linker, 对于某些工具链可能存在 asm-xxx, c-xxx 等变体</param>
        /// <returns>返回一个相对于编译器根目录的路径（路径中的变量已被替换），比如：bin\arm-none-eabi.exe</returns>
        public string getActivedRawToolPath(string name)
        {
            return models[name]["$path"].Value<string>();
        }
        /// <summary>
        /// 获取当前活动的编译工具带有编译器根目录变量的完整路径
        /// </summary>
        /// <param name="name">可选的值有：c, cpp, asm, linker, 对于某些工具链可能存在 asm-xxx, c-xxx 等变体</param>
        /// <returns>路径字符串，比如：%TOOL_DIR%\bin\arm-none-eabi.exe</returns>
        /// <remarks>为什么不将变量 %TOOL_DIR% 替换为实际的值？历史原因，某些情况下使用cmd执行命令，使用环境变量可以避免路径超出长度</remarks>
        public string getActivedToolFullPath(string name)
        {
            return binDir + getActivedRawToolPath(name);
        }

        public string getModelName()
        {
            return model.ContainsKey("name") ? model["name"].Value<string>() : "null";
        }

        private JObject getToolchainVersionMatcher()
        {
            return model.ContainsKey("version") ? (JObject)model["version"] : null;
        }

        /// <summary>
        /// Get current compiler's identifier
        /// </summary>
        /// <returns>
        ///  - KEIL_C51
        ///  - GCC
        ///  - IAR_ARM
        ///  - AC5
        ///  - AC6
        ///  - SDCC
        ///  - COSMIC_STM8
        ///  - IAR_STM8
        /// </returns>
        public string getCompilerId()
        {
            return model.ContainsKey("id") ? model["id"].Value<string>() : getModelName();
        }

        public string getToolPrefix()
        {
            return toolPrefix;
        }

        public bool isDisableOutputTask()
        {
            return paramObj["linker"].ContainsKey("$disableOutputTask")
                && paramObj["linker"]["$disableOutputTask"].Type == JTokenType.Boolean
                && paramObj["linker"]["$disableOutputTask"].Value<bool>();
        }

        public string[] getOutputTaskExcludes()
        {
            if (paramObj["linker"].ContainsKey("$outputTaskExcludes") && 
                paramObj["linker"]["$outputTaskExcludes"].Type == JTokenType.Array)
            {
                return paramObj["linker"]["$outputTaskExcludes"].Values<string>().ToArray();
            }
            return Array.Empty<string>();
        }

        //------------

        private readonly Regex compilerOpts_argsFileVarMatcher = new(@"\$\{argsFile:(.+?)\}", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private string expandArgs(string str_)
        {
            var str = str_.Trim();

            // insert args from file
            {
                var repList = new Dictionary<string, string>();

                foreach (var m in compilerOpts_argsFileVarMatcher.Matches(str).ToList())
                {
                    if (m.Success && m.Groups.Count > 1)
                    {
                        var fpath = toAbsPathByProjectRoot(m.Groups[1].Value);
                        var argLi = new List<string>(256);
                        var fcont = trimComment(File.ReadAllText(fpath));

                        foreach (var line in Program.CRLFMatcher.Split(fcont))
                        {
                            if (string.IsNullOrWhiteSpace(line))
                                continue;

                            argLi.Add(line.Trim());
                        }

                        var key = m.Groups[0].Value;
                        var val = string.Join(' ', argLi).Trim();
                        if (!repList.TryAdd(key, val))
                        {
                            repList[key] = val;
                        }
                    }
                }

                var sb = new StringBuilder(str);

                foreach (var kv in repList)
                {
                    sb.Replace(kv.Key, kv.Value);
                }

                str = sb.ToString();
            }

            return str;
        }

        private string trimComment(string txt)
        {
            // comments:
            //  /* xxxx */
            //  // xxxx
            //  # xxxx
            //
            // strings:
            //  "xxx"

            var str = new StringBuilder(2048);

            var chrStk = new Stack<string>(16);

            var isInString = () => {
                if (chrStk.Count == 0) return false;
                var s = chrStk.Peek();
                return s == "\"";
            };

            var isInComment = () => chrStk.Count > 0;

            var isInSingleLineComment = () => {
                if (chrStk.Count == 0) return false;
                var s = chrStk.Peek();
                return s == "//" || s == "#";
            };

            var chr_prev = '\0';
            for (var i = 0; i < txt.Length; i++)
            {
                var chr = txt[i];
                var chr_next = (i + 1 < txt.Length) ? txt[i + 1] : '\0';

                // in string region
                if (isInString())
                {
                    str.Append(chr);

                    if (chr == '"' && chr_prev != '\\')
                    {
                        chrStk.Pop();
                    }
                }

                // in comment
                else if (isInComment())
                {
                    if (isInSingleLineComment())
                    {
                        if (chr == '\n')
                        {
                            str.Append(chr);
                            chrStk.Pop();
                        }
                    }

                    else
                    {
                        if (chr_prev == '*' && chr == '/')
                        {
                            chrStk.Pop();
                        }
                    }
                }

                // normal char
                else
                {
                    // '#...' comment
                    if (chr == '#' && chr_prev != '\'' && chr_next != '\'')
                    {
                        chrStk.Push(chr.ToString());
                    }

                    // '/*...*/ or //...' comment
                    else if (chr == '/' && (chr_next == '/' || chr_next == '*'))
                    {
                        chrStk.Push(chr.ToString() + chr_next.ToString());
                        chr_prev = chr_next;
                        i++; // skip '/' or '*'
                        continue;
                    }

                    else
                    {
                        if (chr == '"' && chr_prev != '\\')
                        {
                            chrStk.Push(chr.ToString());
                        }

                        str.Append(chr);
                    }
                }

                chr_prev = chr;
            }

            return str.ToString();
        }

        // merge value
        private object mergeParamsList(JObject[] pList, string key, string paramsType)
        {
            List<JToken> objList = new(128);

            foreach (var param in pList)
            {
                if (param.ContainsKey(key))
                {
                    JToken oldObj = objList.Count > 0 ? objList[0] : null;
                    JToken jobj = param[key];

                    if (oldObj != null && oldObj.Type != jobj.Type)
                    {
                        objList.Clear();
                    }

                    objList.Add(jobj);
                }
            }

            object result = null;

            if (objList.Count > 0)
            {
                switch (objList[0].Type)
                {
                    case JTokenType.Boolean:
                        result = objList[objList.Count - 1].Value<bool>() ? "true" : "false";
                        break;
                    case JTokenType.Integer:
                    case JTokenType.Float:
                        result = objList[objList.Count - 1].Value<object>().ToString();
                        break;
                    case JTokenType.String:
                        {
                            // it's a option, overwrite old
                            if (paramsType == "selectable" || paramsType == "keyValue")
                            {
                                result = objList[objList.Count - 1].Value<string>();
                            }

                            // it's a string, merge it
                            else
                            {
                                foreach (JToken jobj in objList)
                                {
                                    if (result == null)
                                    {
                                        result = jobj.Value<string>();
                                    }
                                    else
                                    {
                                        result += " " + jobj.Value<string>();
                                    }
                                }
                            }
                        }
                        break;
                    case JTokenType.Array: // string list
                        {
                            List<string> list = new(64);

                            foreach (JToken jobj in objList)
                            {
                                list.AddRange(jobj.Values<string>());
                            }

                            result = list;
                        }
                        break;
                    default:
                        break;
                }
            }

            return result;
        }

        private readonly Regex compilerOpts_overrideExprMatcher = new(@"\$<override:(.+?)>", RegexOptions.Compiled);
        private readonly Regex compilerOpts_replaceExprMatcher = new(@"\$<replace:(?<old>.+?)/(?<new>.*?)>", RegexOptions.Compiled);

        private CmdInfo fromModel(string modelName, string langName, string fpath, bool dryRun = false)
        {
            JObject cModel = models[modelName];
            JObject cParams = paramObj[modelName];
            InvokeFormat iFormat = invokeFormats[modelName];

            string outputSuffix = ".o";
            string paramsSuffix = ".args.txt";

            bool isQuote = true; // quote path which have whitespace
            bool isSplitterEn = compilerAttr_sdcc_module_split && modelName != "asm";

            if (cModel.ContainsKey("$outputSuffix")) outputSuffix = cModel["$outputSuffix"].Value<string>();
            if (cModel.ContainsKey("$quotePath")) isQuote = cModel["$quotePath"].Value<bool>();

            // if use splitter, outpath is a preprocessed .c file
            if (isSplitterEn) outputSuffix = ".mods";

            //--

            string srcPath = Utility.toRelativePath(cwd, fpath) ?? fpath;
            string srcDir = Path.GetDirectoryName(srcPath);
            if (string.IsNullOrWhiteSpace(srcDir)) srcDir = ".";
            string srcName = Path.GetFileNameWithoutExtension(srcPath);

            //--

            // create obj root dir
            string _objRootDir = outDir + Path.DirectorySeparatorChar + ".obj";
            Directory.CreateDirectory(_objRootDir);

            string _outFileName = null; // a repath for source (without suffix), like: 'src/app/main'
            if (outDirTree) // generate dir tree struct
            {
                // it's a relative path
                if (!Utility.isAbsolutePath(srcPath))
                {
                    // replace '..' in path 
                    string[] pList = Utility.toUnixPath(srcPath).Split('/');
                    for (int idx = 0; idx < pList.Length; idx++)
                    {
                        if (pList[idx] == "..") pList[idx] = "__";
                    }

                    string path = String.Join(Path.DirectorySeparatorChar.ToString(), pList);
                    string fDir = Path.GetDirectoryName(path);

                    if (!string.IsNullOrWhiteSpace(fDir))
                    {
                        Directory.CreateDirectory(_objRootDir + Path.DirectorySeparatorChar + fDir);
                        _outFileName = fDir + Path.DirectorySeparatorChar + srcName;
                    }
                    else // no parent dir
                    {
                        _outFileName = srcName;
                    }
                }

                // if we can't calcu repath, gen complete path to out folder
                else
                {
                    string fmtSrcDir = Utility.toUnixPath(srcDir).Trim('/');
                    // convert 'c:\xxx\a.c' -> '<build_out_dir>/c/xxx/a.??'
                    Regex drvReplacer = new Regex(@"^(?<drv>[a-z]):/", RegexOptions.IgnoreCase);
                    string fDir = Utility.toLocalPath(drvReplacer.Replace(fmtSrcDir, "${drv}/"));
                    Directory.CreateDirectory(_objRootDir + Path.DirectorySeparatorChar + fDir);
                    _outFileName = fDir + Path.DirectorySeparatorChar + srcName;
                }
            }

            // generate to output root directly
            else
            {
                _outFileName = srcName;
            }

            string outName = getUniqueName(_objRootDir + Path.DirectorySeparatorChar + _outFileName);
            string outPath = outName + outputSuffix;
            string refPath = outName + ".d"; // --depend ${refPath} 
            string listPath = outName + ".lst";

            List<string> compiler_cmds = new(baseOpts[modelName]);

            // setup built-in compiler options
            if (langName != null &&
                cModel.ContainsKey("$" + langName) &&
                cParams.ContainsKey(langName))
            {
                string langOption = cParams[langName].Value<string>();
                if (!string.IsNullOrWhiteSpace(langOption))
                    compiler_cmds.Add(getCommandValue((JObject)cModel["$" + langName], langOption));
            }

            // set independent options for source file
            if (srcParams.ContainsKey(fpath) && !string.IsNullOrWhiteSpace(srcParams[fpath]))
            {
                var srcOpts = srcParams[fpath].Trim();

                //
                // !!! this block must at the first !!!
                //
                // override expr:
                //      override global user options if we need;
                //      otherwise, concat user options and source options
                {
                    var overrided = false;

                    while (true)
                    {
                        var mRes = compilerOpts_overrideExprMatcher.Match(srcOpts);

                        if (mRes.Success && mRes.Groups.Count > 1)
                        {
                            srcOpts   = mRes.Groups[1].Value.Trim();
                            overrided = true;
                        }
                        else
                        {
                            break;
                        }
                    }

                    if (!overrided)
                    {
                        compiler_cmds.AddRange(userOpts[modelName]);
                    }
                }

                // replace expr:
                //      replace some matched options to other
                while (true)
                {
                    var mRes = compilerOpts_replaceExprMatcher.Match(srcOpts);

                    if (mRes.Success && mRes.Groups.Count > 1)
                    {
                        var oldVal = mRes.Groups["old"].Value.Trim();
                        var newVal = "";

                        if (mRes.Groups.ContainsKey("new"))
                        {
                            newVal = mRes.Groups["new"].Value.Trim();
                        }

                        // delete expression self
                        srcOpts = srcOpts.Remove(mRes.Groups[0].Index, mRes.Groups[0].Length);

                        if (!string.IsNullOrEmpty(oldVal))
                        {
                            for (int i = 0; i < compiler_cmds.Count; i++)
                            {
                                compiler_cmds[i] = compiler_cmds[i].Replace(oldVal, newVal);
                            }
                        }
                    }
                    else
                    {
                        break;
                    }
                }

                //
                // add to compiler options
                //
                if (!string.IsNullOrEmpty(srcOpts))
                {
                    compiler_cmds.Add(srcOpts);
                }
            }

            // add user global options if not have source independent options
            else
            {
                compiler_cmds.AddRange(userOpts[modelName]);
            }

            List<string> commands = new(compiler_cmds);

            // delete whitespace
            commands.RemoveAll(delegate (string _command) { return string.IsNullOrWhiteSpace(_command); });

            // function: resolveVariables
            string fOutDir = Path.GetDirectoryName(outName);
            string fOutNam = Path.GetFileName(outName);
            string reOutDir = toRelativePathForCompilerArgs(fOutDir, false, false);
            string reSrcDir = toRelativePathForCompilerArgs(srcDir, false, false);
            var resolveVariables = delegate (string cmd) {
                var t = expandArgs(cmd);
                t = t.Replace("${OutName}", fOutNam)
                    .Replace("${OutDir}", reOutDir)
                    .Replace("${FileName}", srcName)
                    .Replace("${FileDir}", reSrcDir)
                    .Replace("${outName}", fOutNam)
                    .Replace("${outDir}", reOutDir)
                    .Replace("${fileName}", srcName)
                    .Replace("${fileDir}", reSrcDir);
                t = Program.replaceEnvVariable(t);
                return t;
            };

            // replace eide cmd vars and system envs
            for (int i = 0; i < commands.Count; i++)
                commands[i] = resolveVariables(commands[i]);

            string sourceBaseArgs = compilerAttr_commandPrefix + string.Join(" ", commands);

            // 最后一步，处理命令行中的文件路径参数，得到完整的命令行
            // 大多数编译器使用 -o 的参数指定 obj 路径，KeilC51 例外
            {
                if (cModel.ContainsKey("$listPath"))
                {
                    var val = getCommandValue((JObject)cModel["$listPath"], "");
                    val = val.Replace("${listPath}", toRelativePathForCompilerArgs(listPath, isQuote));
                    if (!string.IsNullOrWhiteSpace(val))
                        commands.Add(resolveVariables(val));
                }

                string outputFormat = cModel["$output"].Value<string>();

                if (outputFormat.Contains("${in}"))
                {
                    if (!isSplitterEn)
                    {
                        outputFormat = outputFormat
                            .Replace("${out}", toRelativePathForCompilerArgs(outPath, isQuote))
                            .Replace("${in}", toRelativePathForCompilerArgs(fpath, isQuote));
                    }
                }
                else /* compate KEIL_C51 */
                {
                    commands.Insert(0, toRelativePathForCompilerArgs(fpath));

                    if (!isSplitterEn)
                    {
                        outputFormat = outputFormat
                            .Replace("${out}", toRelativePathForCompilerArgs(outPath, isQuote));
                    }
                }

                var outputCmd = outputFormat
                    .Replace("${refPath}", toRelativePathForCompilerArgs(refPath, isQuote));

                commands.Add(resolveVariables(outputCmd));
            }

            string commandLines = compilerAttr_commandPrefix + string.Join(" ", commands);
            string compilerArgs = commandLines;
            string exeFullPath = getActivedToolFullPath(modelName);

            // 如果编译器可以从文件读取参数，且当命令行参数长度超过系统限制后，可将参数保存到文件
            int sysCmdMaxLen = OsInfo.instance().SysCmdLenLimit - 512;
            if (iFormat.useFile && !dryRun && compilerArgs.Length > sysCmdMaxLen)
            {
                FileInfo paramFile = new(outName + paramsSuffix);
                File.WriteAllText(paramFile.FullName, compilerArgs, encodings[modelName]);
                commandLines = iFormat.body.Replace("${value}", "\"" + paramFile.FullName + "\"");
            }

            var buildArgs = new CmdInfo {
                compilerId = getCompilerId().ToLower(),
                compilerModel = modelName,
                exePath = exeFullPath,
                commandLine = commandLines,
                sourcePath = fpath,
                sourceType = modelName.StartsWith("asm") ? "asm" : modelName,
                sourceArgs = compilerArgs,
                outPath = outPath,
                outputEncoding = encodings[modelName],
                sourceArgsChanged = true,
                baseArgs = sourceBaseArgs
            };

            if (isSplitterEn)
                buildArgs.argsForSplitter = compilerArgs;

            // create cli args for 'compile_commands.json'
            buildArgs.shellCommand =
                "\"" + Program.replaceEnvVariable(exeFullPath) + "\" "
                     + Program.replaceEnvVariable(compilerArgs);

            // check .cmd file to determine source file need recompile
            FileInfo cmdFile = new(outPath + ".cmd");
            if (File.Exists(cmdFile.FullName))
            {
                buildArgs.sourceArgsChanged = !File.ReadAllText(
                    cmdFile.FullName, RuntimeEncoding.instance().UTF8).Equals(buildArgs.shellCommand);
            }

            return buildArgs;
        }

        private string formatPathForCompilerArgs(string path)
        {
            return Utility.toLocalPath(path, compilerAttr_directorySeparator);
        }

        public string toRelativePathForCompilerArgs(string path, bool quote = true, bool addDotPrefix = true)
        {
            if (cwd != null)
            {
                string rePath = Utility.toRelativePath(cwd, path);

                if (rePath != null)
                {
                    path = rePath;

                    if (addDotPrefix)
                    {
                        path = "." + Path.DirectorySeparatorChar + path;
                    }
                }
            }

            if (useUnixPath)
            {
                path = Utility.toUnixPath(path);
            }

            return (quote && path.Contains(' ')) ? ("\"" + path + "\"") : path;
        }

        private string getUniqueName(string expectedName)
        {
            string lowerName = expectedName.ToLower();

            if (objNameMap.ContainsKey(lowerName))
            {
                objNameMap[lowerName] += 1;
                return expectedName + '_' + objNameMap[lowerName].ToString();
            }
            else
            {
                objNameMap.Add(lowerName, 0);
                return expectedName;
            }
        }

        private Encoding getEncoding(string name)
        {
            switch (name.ToLower())
            {
                case "utf8":
                    return RuntimeEncoding.instance().UTF8;
                case "utf16":
                    return RuntimeEncoding.instance().UTF16;
                default:
                    return RuntimeEncoding.instance().Default;
            }
        }

        private string getCommandValue(JObject option, object value)
        {
            string type = option["type"].Value<string>();

            // check list type
            if (type == "list")
            {
                if (value is string)
                {
                    type = "value"; /* compatible type: 'list' and 'value' */
                }
                else if (value != null && (value as IEnumerable<string>) == null)
                {
                    throw new TypeErrorException("IEnumerable<string>");
                }
            }

            // check other type (other type must be a string)
            else if (value != null && !(value is string))
            {
                throw new TypeErrorException("string");
            }

            string prefix = option.ContainsKey("prefix") ? option["prefix"].Value<string>() : "";
            string suffix = option.ContainsKey("suffix") ? option["suffix"].Value<string>() : "";
            string command = null;

            switch (type)
            {
                case "selectable":

                    if (!option.ContainsKey("command"))
                        throw new Exception("type \'selectable\' must have \'command\' key !");

                    if (value != null && ((JObject)option["command"]).ContainsKey((string)value))
                    {
                        command = option["command"][value].Value<string>();
                    }
                    else
                    {
                        command = option["command"]["false"].Value<string>();
                    }
                    break;
                case "keyValue":

                    if (!option.ContainsKey("enum"))
                        throw new Exception("type \'keyValue\' must have \'enum\' key !");

                    if (value != null && ((JObject)option["enum"]).ContainsKey((string)value))
                    {
                        command = option["command"].Value<string>() + option["enum"][value].Value<string>();
                    }
                    else
                    {
                        command = option["command"].Value<string>() + option["enum"]["default"].Value<string>();
                    }
                    break;
                case "value":
                    if (value != null)
                    {
                        command = option["command"].Value<string>() + value;
                    }
                    break;
                case "list":
                    {
                        List<string> cmdList = new();

                        string cmd = option["command"].Value<string>();

                        if (value != null)
                        {
                            foreach (var item in (IEnumerable<string>)value)
                            {
                                cmdList.Add(cmd + item);
                            }

                            command = string.Join(" ", cmdList.ToArray());
                        }
                    }
                    break;
                default:
                    throw new Exception("Invalid type \"" + type + "\"");
            }

            if (command == null)
                return "";

            return prefix + command + suffix;
        }

        private string getIncludesCmdLine(string modelName, IEnumerable<string> incList)
        {
            if (!formats[modelName].ContainsKey("$includes")) return "";

            List<string> cmds = new(64);
            CmdFormat incFormat = formats[modelName]["$includes"];

            foreach (var inculdePath in incList)
            {
                cmds.Add(incFormat.body.Replace("${value}", toRelativePathForCompilerArgs(inculdePath, !incFormat.noQuotes, false)));
            }

            return incFormat.prefix + string.Join(incFormat.sep, cmds.ToArray()) + incFormat.suffix;
        }

        private string getdefinesCmdLine(string modelName, IEnumerable<string> defList)
        {
            if (!formats[modelName].ContainsKey("$defines"))
            {
                return "";
            }

            List<string> cmds = new(64);
            CmdFormat defFormat = formats[modelName]["$defines"];

            foreach (var define in defList)
            {
                string macro = null;
                string value = null;

                int index = define.IndexOf('=');
                if (index >= 0) // macro have '=' ?
                {
                    macro = define.Substring(0, index).Trim();
                    value = define.Substring(index + 1).Trim();
                }

                // it's a macro with a value
                if (!string.IsNullOrWhiteSpace(macro) &&
                    !string.IsNullOrWhiteSpace(value))
                {
                    // escape ' and " for KEIL_C51 macros
                    // example: -DTest='a' -> -DTest="'a'"
                    //          -DTest="a" -> -DTest='"a"'
                    if (getCompilerId() == "KEIL_C51")
                    {
                        string escape_char = "'";

                        // string with ', we use " quote it
                        if (Regex.IsMatch(value, "^'(?<val>.*)'$"))
                        {
                            escape_char = "\"";
                        }
                        // string with ", we use ' quote it
                        else if (Regex.IsMatch(value, "^\"(?<val>.*)\"$"))
                        {
                            escape_char = "'";
                        }

                        value = escape_char + value + escape_char;
                    }

                    // escape '"' for macros
                    // example: -DTest="aaa" -> -DTest="\"aaa\""
                    else
                    {
                        value = Regex.Replace(value, "^\"(?<val>.*)\"$", "\\\"${val}\\\"");
                    }

                    cmds.Add(defFormat.body.Replace("${key}", macro).Replace("${value}", value));
                }

                // it's a non-value macro
                else
                {
                    macro = define.Trim();

                    // if macro is '', skip
                    if (string.IsNullOrWhiteSpace(macro))
                        continue;

                    string macroStr;

                    if (modelName == "asm")
                    {
                        macroStr = defFormat.body
                            .Replace("${key}", macro)
                            .Replace("${value}", "1");
                    }
                    else
                    {
                        if (defFormat.body_noval != null)
                            macroStr = defFormat.body_noval
                                .Replace("${key}", macro);
                        else
                            macroStr = defFormat.body
                                .Replace("${key}", macro)
                                .Replace("${value}", "1");
                    }

                    cmds.Add(macroStr);
                }
            }

            return defFormat.prefix + string.Join(defFormat.sep, cmds.ToArray()) + defFormat.suffix;
        }

        private string getLibSearchFolders(string modelName, IEnumerable<string> libList)
        {
            if (!formats[modelName].ContainsKey("$libs"))
            {
                return "";
            }

            List<string> cmds = new(64);
            CmdFormat incFormat = formats[modelName]["$libs"];

            foreach (var libDirPath in libList)
            {
                cmds.Add(incFormat.body.Replace("${value}", toRelativePathForCompilerArgs(libDirPath, !incFormat.noQuotes, false)));
            }

            return incFormat.prefix + string.Join(incFormat.sep, cmds.ToArray()) + incFormat.suffix;
        }
    }

    class Program
    {
        public static readonly int CODE_ERR = 1;
        public static readonly int CODE_DONE = 0;

        public static readonly string sdcc_asm_optimizer = "sdcc_asm_optimizer";

        // minimum amount of files to enable multi-thread compilation
        static readonly int minFilesNumsForMultiThread = 8;

        // file filters
        static readonly Regex cFileFilter = new Regex(@"\.c$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        static readonly Regex asmFileFilter = new Regex(@"\.(?:s|asm|a51)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        static readonly Regex libFileFilter = new Regex(@"\.(?:lib|a|o|obj|sm8)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        static readonly Regex cppFileFilter = new Regex(@"\.(?:cpp|cxx|cc|c\+\+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // string matcher
        public static readonly Regex CRLFMatcher = new(@"\r\n|\n", RegexOptions.Compiled);

        // output highlight render
        static readonly string WARN_RENDER = "\x1b[33;22m$1\x1b[0m";
        static readonly string ERRO_RENDER = "\x1b[31;22m$1\x1b[0m";
        static readonly string NOTE_RENDER = "\x1b[36;22m$1\x1b[0m";
        static readonly string HINT_RENDER = "\x1b[35;22m$1\x1b[0m";

        static Dictionary<Regex, string> ccOutputRender = new();
        static Dictionary<Regex, string> lkOutputRender = new();

        static readonly HashSet<string> srcList = new(512); // @note this path list use local path sep
        static readonly HashSet<string> libList = new(512); // other '.o' '.a' '.lib' files that in sourceList

        //
        // object file order
        //   we will sort obj list for gcc linker 
        //
        static readonly int orderNumberBase = 100;
        static readonly Dictionary<string, int> objOrder = new(512);   // source object file order for linker
        static readonly List<UserObjOrderGlob> objOrderUsr = new(512); // user pattern for obj order

        // compiler params for single source file, Map<absPath, params>
        static readonly Dictionary<string, string> srcParams = new(512);

        // some source files always need to be build, setup them
        static readonly HashSet<string> alwaysInBuildSources = new(512);

        static readonly string appBaseDir = AppDomain.CurrentDomain.SetupInformation.ApplicationBase.TrimEnd(Path.DirectorySeparatorChar);
        static readonly Dictionary<string, string> curEnvs = new(64); // sys envs
        static readonly Dictionary<string, string> cliVars = new(64); // format: <key: 'ENV_NAME', val: 'ENV_VALxxxx'>

        // Used to determine whether the received
        // return code is an error code
        static int ERR_LEVEL = CODE_DONE;

        static int ram_max_size = -1;
        static int rom_max_size = -1;

        static string dumpPath;
        static string toolchainRoot; // the compiler root dir
        static int reqThreadsNum;
        static JObject compilerModel;
        static JObject paramsObj;
        static string outDir;
        static string projectRoot;
        static List<string> projectSysPaths = new(16);
        static Dictionary<string, string> projectEnvs = new(16);
        static string builderDir; // unify_builder.exe self dir
        static string paramsFilePath;
        static string refJsonName;

        static FileStream logStream = null;
        static FileStream compilerLogStream = null;

        static bool enableNormalOut = true;
        static bool showRelativePathOnLog = false;
        static bool colorRendererEnabled = true;

        static HashSet<BuilderMode> modeList = new();

        public static Options cliArgs = null;
        static StringBuilder makefileOutput = new(4096);
        static Dictionary<string, string> makefileCompilers = new(8);
        static BlockingCollection<Task> ioAsyncTask = new();

        enum BuilderMode
        {
            NORMAL = 0,
            FAST,
            MULTHREAD
        }

        struct CompileCommandsDataBaseItem
        {
            // The working directory of the compilation.
            // All paths specified in the command or file fields must be either absolute or relative to this directory.
            public string directory;

            // The main translation unit source processed by this compilation step.
            // This is used by tools as the key into the compilation database.
            // There can be multiple command objects for the same file,
            // for example if the same source file is compiled with different configurations.
            public string file;

            // The compile command argv as list of strings.
            // This should run the compilation step for the translation unit file.
            // arguments[0] should be the executable name, such as clang++.
            // Arguments should not be escaped, but ready to pass to execvp().
            //public string[] arguments;

            // The compile command as a single shell-escaped string.
            // Arguments may be shell quoted and escaped following platform
            // conventions, with ‘"’ and ‘\’ being the only special characters.
            // Shell expansion is not supported.
            public string command;

            // The name of the output created by this compilation step.
            // This field is optional.
            // It can be used to distinguish different processing modes of the same input file.
            //public string output;
        }

        struct UserObjOrderGlob
        {
            public Glob pattern;
            public int order;
        };

        class MapRegion
        {
            public string name;
            public uint   addr;
            public uint   size;
            public uint   max_size;
        };

        class MapRegionItem
        {
            public MapRegion   attr;
            public MapRegion[] children;
        };

        class MapRegionInfo
        {
            public MapRegionItem[] load_regions;
        };

        /**
         * command format: 
         * 
         *   global:
         *   
         *      -v                      print version
         * 
         *   builder group mode:
         *   
         *      -r <commandJsonFile>    run commands json file path
         *      
         *   builder mode:
         *   
         *      -p <paramsFile>         builder params file path
         *      -no-color               close color for output
         *      -force-color            force apply color for output
         *      -only-dump-args         only print compiler args
         */

        public class Options
        {
            [Option('v', Required = false, HelpText = "print app version")]
            public bool PrintVersion { get; set; }

            [Option('r', "run", Required = false, HelpText = "run commands json file path")]
            public string RunCommandsJsonPath { get; set; }

            [Option('p', "params-file", Required = false, HelpText = "builder params file path")]
            public string ParamsFilePath { get; set; }

            [Option("no-color", Required = false, HelpText = "close color render for output")]
            public bool NotUseColor { get; set; }

            [Option("force-color", Required = false, HelpText = "force apply color render for output")]
            public bool ForceUseColor { get; set; }

            [Option("rebuild", Required = false, HelpText = "force rebuild project")]
            public bool ForceRebuild { get; set; }

            [Option("only-dump-args", Required = false, HelpText = "just only print compiler args")]
            public bool OnlyPrintArgs { get; set; }

            [Option("only-dump-compilerdb", Required = false, HelpText = "just only dump compile_commands.json")]
            public bool OnlyDumpCompilerDB { get; set; }

            [Option("use-ccache", Required = false, HelpText = "use ccache speed up compilation")]
            public bool UseCcache { get; set; }

            [Option("out-makefile", Required = false, HelpText = "generate GNU Makefile when build")]
            public bool OutputMakefile { get; set; }

            [Option("dry-run", Required = false, HelpText = "dry run mode. Don't realy do compile")]
            public bool DryRun { get; set; }

            [Option("sdcc-not-bundle-rel", Required = false, HelpText = "do not bundle *.rel for sdcc")]
            public bool SdccNotBundleRel { get; set; }
        }

        // linux VT100 color
        // https://docs.microsoft.com/en-us/windows/console/console-virtual-terminal-sequences?redirectedfrom=MSDN#samples
        // 
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool SetConsoleMode(IntPtr hConsoleHandle, int mode);
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool GetConsoleMode(IntPtr handle, out int mode);
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr GetStdHandle(int handle);
        static int Main(string[] args_)
        {
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
                    log(OsInfo.instance().CRLF + hTxt);
                });

                cliArgs = parserResult.Value;
            }

            //
            // check, convert cli args
            //

            if (cliArgs == null)
                return CODE_ERR;

            if (cliArgs.PrintVersion)
            {
                printAppInfo();
                return CODE_DONE;
            }

            if (string.IsNullOrEmpty(cliArgs.RunCommandsJsonPath) &&
                string.IsNullOrEmpty(cliArgs.ParamsFilePath))
            {
                errorWithLable("params too less !, we need 'builder.params' or 'commands.json' file path !");
                return CODE_ERR;
            }

            if (!string.IsNullOrEmpty(cliArgs.ParamsFilePath))
                cliArgs.ParamsFilePath = Path.GetFullPath(cliArgs.ParamsFilePath);

            if (!string.IsNullOrEmpty(cliArgs.RunCommandsJsonPath))
                cliArgs.RunCommandsJsonPath = Path.GetFullPath(cliArgs.RunCommandsJsonPath);

            //
            // program start
            //

            bool systemSupportColorRenderer = true;

            // init cwd
            resetWorkDir();

            // print new line
            // log("");

            // try to enable VT100 console color
            if (OsInfo.instance().OsType == "win32")
            {
                try
                {
                    const int STD_OUTPUT_HANDLE = -11;
                    const int ENABLE_VIRTUAL_TERMINAL_PROCESSING = 0x04;
                    IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);

                    var handle = GetStdHandle(STD_OUTPUT_HANDLE);
                    if (handle != INVALID_HANDLE_VALUE)
                    {
                        GetConsoleMode(handle, out int mode);
                        systemSupportColorRenderer = SetConsoleMode(handle, mode | ENABLE_VIRTUAL_TERMINAL_PROCESSING);
                    }
                    else
                    {
                        systemSupportColorRenderer = false;
                    }
                }
                catch (Exception)
                {
                    systemSupportColorRenderer = false;
                }
            }

            /* start commands runner ? */
            if (!string.IsNullOrEmpty(cliArgs.RunCommandsJsonPath))
            {
                try
                {
                    return RunCommandsJson();
                }
                catch (Exception err)
                {
                    errorWithLable("runner aborted, msg: " + err.Message);
                    return CODE_ERR;
                }
            }

            // init all params
            try
            {
                /* load params */
                try
                {
                    paramsFilePath = cliArgs.ParamsFilePath;

                    // load params file
                    string paramsJson = File.ReadAllText(paramsFilePath, RuntimeEncoding.instance().UTF8);
                    if (String.IsNullOrWhiteSpace(paramsJson)) throw new ArgumentException("file '" + paramsFilePath + "' is empty !");
                    paramsObj = JObject.Parse(paramsJson);

                    // load core params
                    toolchainRoot = paramsObj["toolchainLocation"].Value<string>();
                    string modelFilePath = paramsObj["toolchainCfgFile"].Value<string>();

                    // load compiler model
                    string modelJson = File.ReadAllText(modelFilePath, RuntimeEncoding.instance().UTF8);
                    if (String.IsNullOrWhiteSpace(modelJson)) throw new ArgumentException("file '" + modelFilePath + "' is empty !");
                    compilerModel = JObject.Parse(modelJson);
                }
                catch (KeyNotFoundException err)
                {
                    errorWithLable("Load params failed: 'some arguments missing' !\r\n" + err.ToString());
                    return CODE_ERR;
                }
                catch (Exception err)
                {
                    errorWithLable("Load params failed !\r\n" + err.ToString());
                    return CODE_ERR;
                }

                // init path
                projectRoot = paramsObj["rootDir"].Value<string>();
                dumpPath = paramsObj["dumpPath"].Value<string>();
                outDir = paramsObj["outDir"].Value<string>();
                builderDir = appBaseDir;

                // init syspath
                if (paramsObj.ContainsKey("sysPaths"))
                {
                    foreach (var path in paramsObj["sysPaths"].Values<string>())
                    {
                        setEnvVariable("PATH", path);
                        projectSysPaths.Add(path);
                    }
                }

                // get real path
                dumpPath = Utility.isAbsolutePath(dumpPath) ? dumpPath : (projectRoot + Path.DirectorySeparatorChar + dumpPath);
                outDir = Utility.isAbsolutePath(outDir) ? outDir : (projectRoot + Path.DirectorySeparatorChar + outDir);

                // prepare source
                JObject srcParamsObj = paramsObj.ContainsKey("sourceParams") 
                    ? (JObject)paramsObj["sourceParams"] : null;
                IEnumerable<string> srcList = paramsObj["sourceList"].Values<string>();
                HashSet<string> alwaysBuild = new HashSet<string>(paramsObj.ContainsKey("alwaysInBuildSources")
                    ? paramsObj["alwaysInBuildSources"].Values<string>() : Array.Empty<string>());
                prepareSourceFiles(projectRoot, srcList, srcParamsObj, alwaysBuild);

                // other params
                modeList.Add(BuilderMode.NORMAL);
                reqThreadsNum = paramsObj.ContainsKey("threadNum") ? paramsObj["threadNum"].Value<int>() : 0;
                ram_max_size = paramsObj.ContainsKey("ram") ? paramsObj["ram"].Value<int>() : -1;
                rom_max_size = paramsObj.ContainsKey("rom") ? paramsObj["rom"].Value<int>() : -1;
                showRelativePathOnLog = paramsObj.ContainsKey("showRepathOnLog") ? paramsObj["showRepathOnLog"].Value<bool>() : false;
                refJsonName = paramsObj.ContainsKey("sourceMapName") ? paramsObj["sourceMapName"].Value<string>() : "ref.json";

                // prepare builder params
                ERR_LEVEL = compilerModel.ContainsKey("ERR_LEVEL") ? compilerModel["ERR_LEVEL"].Value<int>() : ERR_LEVEL;
                prepareModel(compilerModel);
                prepareParams(paramsObj);

                // other bool options
                systemSupportColorRenderer = !cliArgs.NotUseColor;
                bool forceUseColorRender = cliArgs.ForceUseColor;
                colorRendererEnabled = systemSupportColorRenderer || forceUseColorRender;

                // load builder mode
                if (paramsObj.ContainsKey("buildMode"))
                {
                    string[] mList = paramsObj["buildMode"].Value<string>().Split('|');

                    foreach (var modeStr in mList)
                    {
                        try
                        {
                            modeList.Add((BuilderMode)Enum.Parse(typeof(BuilderMode), modeStr.ToUpper()));
                        }
                        catch (Exception)
                        {
                            warn("\r\nInvalid mode option '" + modeStr + "', ignore it !\r\n");
                        }
                    }
                }

                // load user object order
                if (paramsObj.ContainsKey("options") &&
                    (paramsObj["options"] as JObject).ContainsKey("linker") &&
                    (paramsObj["options"]["linker"] as JObject).ContainsKey("object-order") &&
                    (paramsObj["options"]["linker"]["object-order"] is JArray objOrderList))
                {
                    foreach (JObject item in objOrderList)
                    {
                        try
                        {
                            var pattern = item["pattern"].Value<string>();

                            if (string.IsNullOrEmpty(pattern))
                                continue;

                            var order = 0;

                            switch (item["order"].Type)
                            {
                                case JTokenType.String:
                                    order = int.Parse(item["order"].Value<string>());
                                    break;
                                case JTokenType.Integer:
                                    order = item["order"].Value<int>();
                                    break;
                                default:
                                    throw new Exception("invalid type, 'order' type is: " + item["order"].Type.ToString());
                            }

                            objOrderUsr.Add(new UserObjOrderGlob {
                                pattern = Glob.Parse(pattern.Trim()),
                                order = order,
                            });
                        }
                        catch (Exception)
                        {
                            var json_str = JsonConvert.SerializeObject(item);
                            warn("\r\nIgnored invalid 'objOrder' item: " + json_str + "\r\n");
                        }
                    }
                }

                if (cliArgs.ForceRebuild)
                {
                    modeList.Remove(BuilderMode.FAST);
                }
            }
            catch (Exception err)
            {
                errorWithLable("Init build failed !\r\n" + err.ToString());
                return CODE_ERR;
            }

            // record build start time
            DateTime time = DateTime.Now;

            if (cliArgs.OnlyDumpCompilerDB)
            {
                log("start generating compiler database ...");
            }
            else if (cliArgs.OnlyPrintArgs)
            {
                // nothing todo
            }
            else
            {
                infoWithLable("", false);
                info($"start building at {time:yyyy-MM-dd HH:mm:ss}{(cliArgs.DryRun ? "(dry-run)" : "")}\r\n");
            }

            // open and lock log file
            lockLogs();

            // boost process priority
            try
            {
                Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.High;
            }
            catch (Exception)
            {
                // ignore
            }

            Dictionary<string, CmdGenerator.CmdInfo> commands = new(512);

            // compiler errlog list
            List<string> errLogs = new(512);

            // compiler prefix
            string COMPILER_CMD_PREFIX = "";

            try
            {
                Directory.CreateDirectory(outDir);

                // add appBase folder to system env
                setEnvValue("PATH", appBaseDir);

                // add user env from bulder.params
                if (paramsObj.ContainsKey("env"))
                {
                    JObject envs = (JObject)paramsObj["env"];

                    foreach (JProperty field in envs.Properties())
                    {
                        string envName = field.Name.ToString().Trim();
                        string envValue = field.Value.ToString().Trim();

                        if (!Regex.IsMatch(envName, @"^[\w\$]+$") ||
                            envName.ToLower() == "path")
                        {
                            warn(string.Format("\r\nignore incorrect env: '{0}'\r\n", envName));
                            continue;
                        }

                        // add shell env
                        setEnvValue(envName, envValue);

                        if (projectEnvs.ContainsKey(envName))
                            projectEnvs[envName] = envValue;
                        else
                            projectEnvs.Add(envName, envValue);

                        // set cmd prefix
                        if (envName == "COMPILER_CMD_PREFIX" && !string.IsNullOrWhiteSpace(envValue))
                        {
                            COMPILER_CMD_PREFIX = envValue + " ";
                        }
                    }
                }

                // set toolchain root env
                try
                {
                    setEnvValue("TOOL_DIR", toolchainRoot);
                }
                catch (Exception e)
                {
                    throw new Exception("Set Environment Failed !, [path] : \"" + toolchainRoot + "\"", e);
                }

                // for output makefile, force set 'useUnixPath' -> true
                if (cliArgs.OutputMakefile)
                {
                    if (compilerModel.ContainsKey("useUnixPath"))
                    {
                        compilerModel["useUnixPath"] = true;
                    }
                    else
                    {
                        compilerModel.Add("useUnixPath", new JValue(true));
                    }
                }

                // create command generator
                CmdGenerator cmdGen = new(compilerModel, paramsObj, new CmdGenerator.GeneratorOption {
                    bindirEnvName = "%TOOL_DIR%",
                    bindirAbsPath = toolchainRoot,
                    outpath = outDir,
                    cwd = projectRoot,
                    testMode = cliArgs.OnlyPrintArgs,
                    compiler_prefix = COMPILER_CMD_PREFIX,
                    srcParams = srcParams,
                    outDirTree = true
                });

                // ingnore keil c51 normal output
                if (cmdGen.getCompilerId() == "KEIL_C51" ||
                    cmdGen.getCompilerId() == "COSMIC_STM8")
                {
                    enableNormalOut = false;
                }

                // add console color render
                if (colorRendererEnabled)
                {
                    // compiler id
                    string ccID = cmdGen.getCompilerId().ToLower();

                    switch (ccID)
                    {
                        case "keil_c51":
                            {
                                /* compiler */
                                ccOutputRender.Add(new Regex(@"(\bwarning\s[A-Z][0-9]+(?::\s.+)?)",
                                    RegexOptions.IgnoreCase | RegexOptions.Compiled), WARN_RENDER);
                                ccOutputRender.Add(new Regex(@"(\berror\s[A-Z][0-9]+(?::\s.+)?)",
                                    RegexOptions.IgnoreCase | RegexOptions.Compiled), ERRO_RENDER);

                                /* linker */
                                lkOutputRender.Add(new Regex(@"(\bwarning\s[A-Z][0-9]+(?::\s.+)?)",
                                    RegexOptions.IgnoreCase | RegexOptions.Compiled), WARN_RENDER);
                                lkOutputRender.Add(new Regex(@"(\berror\s[A-Z][0-9]+(?::\s.+)?)",
                                    RegexOptions.IgnoreCase | RegexOptions.Compiled), ERRO_RENDER);
                            }
                            break;
                        case "sdcc":
                            {
                                // source splitter
                                ccOutputRender.Add(new Regex(@"(SyntaxError:)",
                                    RegexOptions.IgnoreCase | RegexOptions.Compiled), ERRO_RENDER);

                                /* compiler */
                                ccOutputRender.Add(new Regex(@"(warning \d+:|\swarning:\s|^warning:\s)",
                                    RegexOptions.IgnoreCase | RegexOptions.Compiled), WARN_RENDER);
                                ccOutputRender.Add(new Regex(@"(error \d+:|\serror:\s|^error:\s)",
                                    RegexOptions.IgnoreCase | RegexOptions.Compiled), ERRO_RENDER);

                                /* linker */
                                lkOutputRender.Add(new Regex(@"(warning \d+:|\swarning:\s|^warning:\s)",
                                    RegexOptions.IgnoreCase | RegexOptions.Compiled), WARN_RENDER);
                                lkOutputRender.Add(new Regex(@"(error \d+:|\serror:\s|^error:\s)",
                                    RegexOptions.IgnoreCase | RegexOptions.Compiled), ERRO_RENDER);
                                lkOutputRender.Add(new Regex(@"(ASlink-Warning-\w+)",
                                    RegexOptions.IgnoreCase | RegexOptions.Compiled), WARN_RENDER);
                            }
                            break;
                        case "iar_stm8":
                        case "iar_arm":
                            {
                                /* compiler */
                                ccOutputRender.Add(new Regex(@"\b(warning\[\w+\]:\s)",
                                    RegexOptions.IgnoreCase | RegexOptions.Compiled), WARN_RENDER);
                                ccOutputRender.Add(new Regex(@"\b(error\[\w+\]:\s)",
                                    RegexOptions.IgnoreCase | RegexOptions.Compiled), ERRO_RENDER);
                                // cc hint msg
                                ccOutputRender.Add(new Regex(@"^([\^~\s]*\^[\^~\s]*)$",
                                    RegexOptions.IgnoreCase | RegexOptions.Compiled), HINT_RENDER);
                                ccOutputRender.Add(new Regex(@"^([~\s]*~[~\s]*)$",
                                    RegexOptions.IgnoreCase | RegexOptions.Compiled), NOTE_RENDER);
                                ccOutputRender.Add(new Regex(@"^(\s*\|.+)",
                                    RegexOptions.IgnoreCase | RegexOptions.Compiled), HINT_RENDER);

                                /* linker */
                                lkOutputRender.Add(new Regex(@"\b(warning\[\w+\]:\s)",
                                    RegexOptions.IgnoreCase | RegexOptions.Compiled), WARN_RENDER);
                                lkOutputRender.Add(new Regex(@"\b(error\[\w+\]:\s)",
                                    RegexOptions.IgnoreCase | RegexOptions.Compiled), ERRO_RENDER);
                            }
                            break;
                        case "cosmic_stm8":
                            {
                                //#error cpstm8 acia.c:33(25) incompatible compare types
                                //#error clnk acia.lkf:1 symbol f_recept not defined (vector.o )
                                //#error clnk acia.lkf:1 symbol f__stext not defined (vector.o )

                                /* compiler */
                                ccOutputRender.Add(new Regex(@"^(#warning \w+)",
                                    RegexOptions.IgnoreCase | RegexOptions.Compiled), WARN_RENDER);
                                ccOutputRender.Add(new Regex(@"^(#error \w+)",
                                    RegexOptions.IgnoreCase | RegexOptions.Compiled), ERRO_RENDER);

                                /* linker */
                                lkOutputRender.Add(new Regex(@"^(#warning \w+)",
                                    RegexOptions.IgnoreCase | RegexOptions.Compiled), WARN_RENDER);
                                lkOutputRender.Add(new Regex(@"^(#error \w+)",
                                    RegexOptions.IgnoreCase | RegexOptions.Compiled), ERRO_RENDER);
                                lkOutputRender.Add(new Regex(@"\b(symbol \w+ not defined)",
                                    RegexOptions.IgnoreCase | RegexOptions.Compiled), ERRO_RENDER);
                                lkOutputRender.Add(new Regex(@"\b(segment [\.\w\-]+ size overflow)",
                                    RegexOptions.IgnoreCase | RegexOptions.Compiled), ERRO_RENDER);
                            }
                            break;
                        /* other modern compilers */
                        default:
                            {
                                /* common */
                                {
                                    ccOutputRender.Add(new Regex(@"\b(warning:\s)",
                                        RegexOptions.IgnoreCase | RegexOptions.Compiled), WARN_RENDER);
                                    ccOutputRender.Add(new Regex(@"\b(error:\s)",
                                        RegexOptions.IgnoreCase | RegexOptions.Compiled), ERRO_RENDER);
                                    ccOutputRender.Add(new Regex(@"\b(note:\s)",
                                        RegexOptions.IgnoreCase | RegexOptions.Compiled), NOTE_RENDER);
                                    // hint msg
                                    ccOutputRender.Add(new Regex(@"^([\^~\s]*\^[\^~\s]*)$",
                                        RegexOptions.IgnoreCase | RegexOptions.Compiled), HINT_RENDER);
                                    ccOutputRender.Add(new Regex(@"^([~\s]*~[~\s]*)$",
                                        RegexOptions.IgnoreCase | RegexOptions.Compiled), NOTE_RENDER);
                                    ccOutputRender.Add(new Regex(@"^(\s*\|.+)",
                                        RegexOptions.IgnoreCase | RegexOptions.Compiled), HINT_RENDER);
                                }

                                /* for gcc */
                                if (ccID == "gcc")
                                {
                                    /* compiler */
                                    ccOutputRender.Add(new Regex(@"(\[\-w[\w\-=]+\])",
                                        RegexOptions.IgnoreCase | RegexOptions.Compiled), HINT_RENDER);
                                    ccOutputRender.Add(new Regex(@"(\{aka [^\}]+\})",
                                        RegexOptions.IgnoreCase | RegexOptions.Compiled), NOTE_RENDER);

                                    /* linker */
                                    lkOutputRender.Add(new Regex(@"\b(warning:\s)",
                                        RegexOptions.IgnoreCase | RegexOptions.Compiled), WARN_RENDER);
                                    lkOutputRender.Add(new Regex(@"\b(error:\s)",
                                        RegexOptions.IgnoreCase | RegexOptions.Compiled), ERRO_RENDER);
                                    lkOutputRender.Add(new Regex(@"\b(note:\s)",
                                        RegexOptions.IgnoreCase | RegexOptions.Compiled), NOTE_RENDER);
                                    lkOutputRender.Add(new Regex(@"\b(cannot open linker script file)\b",
                                        RegexOptions.Compiled), ERRO_RENDER);
                                    lkOutputRender.Add(new Regex(@"\b(undefined reference to `[^']+')",
                                        RegexOptions.Compiled), ERRO_RENDER);
                                    lkOutputRender.Add(new Regex(@"\b(multiple definition of `[^']+')",
                                        RegexOptions.Compiled), ERRO_RENDER);
                                    lkOutputRender.Add(new Regex(@"\b(section `[^']+' will not fit in region `[^']+')",
                                        RegexOptions.Compiled), ERRO_RENDER);
                                    lkOutputRender.Add(new Regex(@"\b(region `[^']+' overflowed by \w+ bytes)",
                                        RegexOptions.Compiled), HINT_RENDER);
                                    lkOutputRender.Add(new Regex(@"\b(region \w+ overflowed with \w+)",
                                        RegexOptions.Compiled), HINT_RENDER);
                                    lkOutputRender.Add(new Regex(@"(\[\-w[\w\-=]+\])",
                                        RegexOptions.IgnoreCase | RegexOptions.Compiled), HINT_RENDER);
                                }

                                /* for ac5/ac6 */
                                else if (ccID.StartsWith("ac"))
                                {
                                    /* linker */
                                    lkOutputRender.Add(new Regex(@"(Fatal error: L\w+:)",
                                        RegexOptions.IgnoreCase | RegexOptions.Compiled), ERRO_RENDER);
                                    lkOutputRender.Add(new Regex(@"(Error: L\w+:)",
                                        RegexOptions.IgnoreCase | RegexOptions.Compiled), ERRO_RENDER);
                                    lkOutputRender.Add(new Regex(@"(Warning: L\w+:)",
                                        RegexOptions.IgnoreCase | RegexOptions.Compiled), WARN_RENDER);
                                }
                            }
                            break;
                    }
                }

                // compiler path
                var CC_PATH = toolchainRoot + Path.DirectorySeparatorChar + cmdGen.getActivedRawToolPath("c");
                var AS_PATH = toolchainRoot + Path.DirectorySeparatorChar + cmdGen.getActivedRawToolPath("asm");
                var CXX_PATH = toolchainRoot + Path.DirectorySeparatorChar + cmdGen.getActivedRawToolPath("cpp");
                // 注意：当处于 lib 生成模式时，最终被使用的 linker 实际是 ar.exe
                // 但 LD_PATH 代表 ld 的路径，因此我们需要获取原始的linker的路径
                var LD_PATH = cmdGen.getOtherUtilToolFullPath("linker");

                // export compiler bin folder to PATH
                var CC_DIR = Path.GetDirectoryName(CC_PATH);
                setEnvValue("PATH", CC_DIR);

                //
                setEnvValue("EIDE_CUR_OS_TYPE", OsInfo.instance().OsType);

                // export compiler info
                setEnvValue("EIDE_CUR_COMPILER_ID", cmdGen.getCompilerId().ToLower());
                setEnvValue("EIDE_CUR_COMPILER_NAME", cmdGen.compilerName);
                setEnvValue("EIDE_CUR_COMPILER_NAME_FULL", cmdGen.compilerFullName);
                setEnvValue("EIDE_CUR_COMPILER_VERSION", cmdGen.compilerVersion);

                // export compiler path
                setEnvValue("EIDE_CUR_COMPILER_PREFIX", cmdGen.getToolPrefix());
                setEnvValue("EIDE_CUR_COMPILER_CC_PATH", CC_PATH);
                setEnvValue("EIDE_CUR_COMPILER_AS_PATH", AS_PATH);
                setEnvValue("EIDE_CUR_COMPILER_LD_PATH", LD_PATH);
                setEnvValue("EIDE_CUR_COMPILER_CXX_PATH", CXX_PATH);

                // export compiler base commands
                {
                    string basecli;

                    basecli = cmdGen.fromCFile("<c_file>", true).baseArgs;
                    setEnvValue("EIDE_CUR_COMPILER_CC_BASE_ARGS", basecli);

                    basecli = cmdGen.fromCppFile("<cxx_file>", true).baseArgs;
                    setEnvValue("EIDE_CUR_COMPILER_CXX_BASE_ARGS", basecli);

                    basecli = cmdGen.fromAsmFile("<asm_file>", true).baseArgs;
                    setEnvValue("EIDE_CUR_COMPILER_AS_BASE_ARGS", basecli);
                }

                // preset env vars for tasks
                setEnvValue("TargetName", cmdGen.getOutName());
                setEnvValue("ConfigName", cmdGen.getBuildConfigName());
                setEnvValue("ProjectRoot", projectRoot);
                setEnvValue("BuilderFolder", builderDir);
                setEnvValue("OutDir", outDir);

                setEnvValue("ToolchainRoot", toolchainRoot);
                setEnvValue("CompilerPrefix", cmdGen.getToolPrefix());
                setEnvValue("CompilerFolder", CC_DIR);
                setEnvValue("CompilerId", cmdGen.getCompilerId().ToLower());
                setEnvValue("CompilerName", cmdGen.compilerName);
                setEnvValue("CompilerFullName", cmdGen.compilerFullName);
                setEnvValue("CompilerVersion", cmdGen.compilerVersion);

                // only for task command
                addCliVar("re:ProjectRoot", ".");
                addCliVar("re:BuilderFolder", Utility.toRelativePath(projectRoot, builderDir) ?? builderDir);
                addCliVar("re:OutDir", Utility.toRelativePath(projectRoot, outDir) ?? outDir);
                addCliVar("re:ToolchainRoot", Utility.toRelativePath(projectRoot, toolchainRoot) ?? toolchainRoot);
                addCliVar("re:CompilerFolder", Utility.toRelativePath(projectRoot, CC_DIR) ?? CC_DIR);

                if (cliArgs.OnlyPrintArgs)
                {
                    CmdGenerator.CmdInfo cmdInf;

                    info("App Info:\r\n");
                    printAppInfo();

                    cmdInf = cmdGen.fromCFile("${c}", true);
                    warn("\r\nC command line (" + Path.GetFileNameWithoutExtension(cmdInf.exePath) + "): \r\n");
                    log(cmdInf.commandLine);

                    cmdInf = cmdGen.fromCppFile("${cpp}", true);
                    warn("\r\nCPP command line (" + Path.GetFileNameWithoutExtension(cmdInf.exePath) + "): \r\n");
                    log(cmdInf.commandLine);

                    cmdInf = cmdGen.fromAsmFile("${asm}", true);
                    warn("\r\nASM command line (" + Path.GetFileNameWithoutExtension(cmdInf.exePath) + "): \r\n");
                    log(cmdInf.commandLine);

                    cmdInf = cmdGen.genLinkCommand(new string[] { "${obj1}", "${obj2}" }, true);
                    warn("\r\nLinker command line (" + Path.GetFileNameWithoutExtension(cmdInf.exePath) + "): \r\n");
                    log(cmdInf.commandLine);

                    warn("\r\nOutput file command line: \r\n");
                    CmdGenerator.CmdInfo[] cmdInfoList = cmdGen.genOutputCommand(cmdInf.outPath, Array.Empty<string>());
                    foreach (CmdGenerator.CmdInfo info in cmdInfoList)
                    {
                        log("\t" + info.title + ": ");
                        log("\t\t" + info.exePath + " " + info.commandLine);
                        log("");
                    }

                    // close and unlock log file
                    unlockLogs();

                    return CODE_DONE;
                }

                // Check toolchain root folder
                if (!Directory.Exists(toolchainRoot))
                {
                    throw new Exception("Not found toolchain directory !, [path] : \"" + toolchainRoot + "\"");
                }

                // 生成 makefile 要放在 rebuild task 之前
                if (cliArgs.OutputMakefile)
                {
                    var builderVersion = Assembly.GetExecutingAssembly().GetName().Version;
                    makefileOutput
                        .AppendLine("#################################################################")
                        .AppendLine($"# AUTO GENERATE AT {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")} BY unify_builder v{builderVersion}")
                        .AppendLine("#################################################################")
                        .AppendLine();

                    var outpath = Utility.toRelativePath(projectRoot, outDir, true);

                    // dump informations
                    makefileOutput
                        .AppendLine("# Usage:")
                        .AppendLine("#  - Build:\tmake COMPILER_DIR=<dir path>")
                        .AppendLine("#  - Clean:\tmake clean")
                        .AppendLine()
                        .AppendLine("# Targets Dependences Chain:")
                        .AppendLine("#  all -> postbuild -> bin -> elf -> prebuild, *.o ...")
                        .AppendLine();

                    // verbose mode
                    makefileOutput
                        .AppendLine("# Use 'make V=1' to see the full commands")
                        .AppendLine("ifdef V")
                        .AppendLine("\tQ = ")
                        .AppendLine("else")
                        .AppendLine("\tQ = @")
                        .AppendLine("endif")
                        .AppendLine();

                    // setup compiler
                    makefileCompilers.Add("CC", Utility.toUnixPath(CC_PATH));
                    makefileCompilers.Add("AS", Utility.toUnixPath(AS_PATH));
                    makefileCompilers.Add("CXX", Utility.toUnixPath(CXX_PATH));
                    makefileCompilers.Add("LD", Utility.toUnixPath(LD_PATH));
                    if (cmdGen.hasOtherUtilTool("linker-lib"))
                    {
                        var p = cmdGen.getOtherUtilToolFullPath("linker-lib");
                        makefileCompilers.Add("AR", Utility.toUnixPath(p));
                    }

                    // setup toolchain
                    var dirvarValue = Utility.toUnixPath(toolchainRoot);
                    if (OsInfo.instance().OsType == "win32") // on Win32, conv 'C:\xxx' -> /C/xx for GNU make
                        dirvarValue = Regex.Replace(dirvarValue, @"^([a-zA-Z]):/", "/$1/");
                    makefileOutput.AppendLine($"COMPILER_DIR ?= {dirvarValue}");
                    makefileOutput
                        .AppendLine("_PATH_TMP:=$(COMPILER_DIR)/bin:$(PATH)")
                        .AppendLine("export PATH=$(_PATH_TMP)")
                        .AppendLine();
                    var exeSuffix = OsInfo.instance().OsType == "win32" ? ".exe" : "";
                    makefileOutput.AppendLine($"EXE?={exeSuffix}");
                    foreach (var item in makefileCompilers)
                    {
                        var val = Regex
                            .Replace(item.Value, @"\.exe$", "$(EXE)", RegexOptions.IgnoreCase)
                            .Replace(toolchainRoot, "$(COMPILER_DIR)")
                            .Replace(Utility.toUnixPath(toolchainRoot), "$(COMPILER_DIR)");
                        makefileOutput.AppendLine($"{item.Key}={quotePath(val)}");
                    }
                    makefileOutput.AppendLine();

                    makefileOutput
                        .AppendLine("###########################")
                        .AppendLine("# targets")
                        .AppendLine("###########################")
                        .AppendLine();

                    // color
                    makefileOutput
                        .AppendLine("COLOR_END=\"\\e[0m\"")
                        .AppendLine("COLOR_ERR=\"\\e[31;1m\"")
                        .AppendLine("COLOR_WRN=\"\\e[33;1m\"")
                        .AppendLine("COLOR_SUC=\"\\e[32;1m\"")
                        .AppendLine("COLOR_INF=\"\\e[34;1m\"")
                        .AppendLine();

                    // PHONY target
                    makefileOutput
                        .AppendLine(".PHONY : all postbuild bin elf prebuild clean")
                        .AppendLine();

                    // target: all
                    makefileOutput
                        .AppendLine("all: postbuild")
                        .AppendLine("\t@echo ==========")
                        .AppendLine("\t@echo $(COLOR_SUC)\"ALL DONE.\"$(COLOR_END)")
                        .AppendLine("\t@echo ==========")
                        .AppendLine();

                    // target: clean
                    makefileOutput
                        .AppendLine("clean:")
                        .AppendLine($"\t-rm -fv ./{outpath}/*.elf ./{outpath}/*.axf ./{outpath}/*.out")
                        .AppendLine($"\t-rm -fv ./{outpath}/*.hex ./{outpath}/*.bin ./{outpath}/*.s19")
                        .AppendLine($"\t-rm -rfv ./{outpath}/.obj")
                        .AppendLine();
                }

                // run prebuild task
                if (!cliArgs.OnlyDumpCompilerDB)
                {
                    switchWorkDir(projectRoot);
                    var ret = runTasks("PRE-BUILD TASKS", "beforeBuildTasks");
                    resetWorkDir();
                    if (ret != CODE_DONE)
                        throw new Exception("Run Tasks Failed !, Stop Build !");
                }

                if (!cliArgs.OnlyDumpCompilerDB)
                {
                    infoWithLable(cmdGen.compilerFullName + "\r\n", true, "TOOL");
                }

                // some compiler database informations
                Dictionary<string, string> sourceRefs = new();
                List<CompileCommandsDataBaseItem> compilerArgsDataBase = new(256);
                Dictionary<string, List<string>> linkerObjs = new(256);

                var PushLinkerObjs = delegate (CmdGenerator.CmdInfo ccArgs) {

                    // it's a normal obj
                    if (string.IsNullOrEmpty(ccArgs.argsForSplitter))
                    {
                        linkerObjs.Add(ccArgs.sourcePath, new() { ccArgs.outPath });

                        var order = orderNumberBase + objOrder.Count;

                        if (objOrder.TryAdd(ccArgs.outPath, order) == false)
                        {
                            objOrder[ccArgs.outPath] = order;
                        }
                    }

                    // it's a splitted obj, parse from file
                    else if (File.Exists(ccArgs.outPath))
                    {
                        var objLi = parseSourceSplitterOutput(File.ReadLines(ccArgs.outPath))
                            .Select(path => {
                                return Utility.isAbsolutePath(path) ? path : (projectRoot + Path.DirectorySeparatorChar + path);
                            }).ToList();

                        linkerObjs.Add(ccArgs.sourcePath, objLi);

                        foreach (var _objPath in objLi)
                        {
                            var order = orderNumberBase + objOrder.Count;

                            if (objOrder.TryAdd(_objPath, order) == false)
                            {
                                objOrder[_objPath] = order;
                            }
                        }
                    }
                };

                int src_count_c = 0;
                int src_count_cpp = 0;
                int src_count_asm = 0;
                int src_count_lib = libList.Count;

                foreach (var srcPath in srcList)
                {
                    CmdGenerator.CmdInfo cmdInf;

                    if (cFileFilter.IsMatch(srcPath))
                    {
                        cmdInf = cmdGen.fromCFile(srcPath);
                        src_count_c++;
                    }
                    else if (cppFileFilter.IsMatch(srcPath))
                    {
                        cmdInf = cmdGen.fromCppFile(srcPath);
                        src_count_cpp++;
                    }
                    else if (asmFileFilter.IsMatch(srcPath))
                    {
                        cmdInf = cmdGen.fromAsmFile(srcPath);
                        src_count_asm++;
                    }
                    else
                    {
                        continue;
                    }

                    commands.Add(cmdInf.sourcePath, cmdInf);
                    PushLinkerObjs(cmdInf);

                    sourceRefs.Add(cmdInf.sourcePath, cmdInf.outPath);
                    compilerArgsDataBase.Add(new CompileCommandsDataBaseItem {
                        file = cmdInf.sourcePath,
                        directory = projectRoot,
                        command = cmdInf.shellCommand
                    });
                }

                // save compiler database informations
                try
                {
                    string refFilePath = outDir + Path.DirectorySeparatorChar + refJsonName;
                    File.WriteAllText(refFilePath, JsonConvert.SerializeObject(sourceRefs), RuntimeEncoding.instance().UTF8);

                    string compilerDbPath = outDir + Path.DirectorySeparatorChar + "compile_commands.json";
                    CompileCommandsDataBaseItem[] iLi = compilerArgsDataBase.ToArray();
                    File.WriteAllText(compilerDbPath, JsonConvert.SerializeObject(iLi), RuntimeEncoding.instance().UTF8);

                    if (cliArgs.OnlyDumpCompilerDB)
                    {
                        log("Source Map Database Path: " + refFilePath);
                        log("Compiler Database Path: " + compilerDbPath);
                        unlockLogs();
                        return CODE_DONE;
                    }
                }
                catch (Exception err)
                {
                    if (cliArgs.OnlyDumpCompilerDB)
                    {
                        error("Failed:");
                        error(err.ToString() + "\n");
                        unlockLogs();
                        return CODE_ERR;
                    }
                }

                // check source file count
                if (src_count_c + src_count_cpp + src_count_asm == 0)
                {
                    throw new Exception("Not found any source files !, please add some source files !");
                }

                // check compiler 
                {
                    if (src_count_c > 0)
                    {
                        string absPath = replaceEnvVariable(cmdGen.getActivedToolFullPath("c"));

                        if (!File.Exists(absPath))
                        {
                            throw new Exception("Not found 'C Compiler' !, [path]: \"" + absPath + "\"");
                        }
                    }

                    if (src_count_cpp > 0)
                    {
                        string absPath = replaceEnvVariable(cmdGen.getActivedToolFullPath("cpp"));

                        if (!File.Exists(absPath))
                        {
                            throw new Exception("Not found 'C++ Compiler' !, [path]: \"" + absPath + "\"");
                        }
                    }

                    if (src_count_asm > 0)
                    {
                        string absPath = replaceEnvVariable(cmdGen.getActivedToolFullPath("asm"));

                        if (!File.Exists(absPath))
                        {
                            throw new Exception("Not found 'Assembler' !, [path]: \"" + absPath + "\"");
                        }
                    }

                    {
                        string absPath = replaceEnvVariable(cmdGen.getActivedToolFullPath("linker"));

                        if (!File.Exists(absPath))
                        {
                            throw new Exception("Not found 'Linker' !, [path]: \"" + absPath + "\"");
                        }
                    }
                }

                if (cliArgs.OutputMakefile)
                {
                    HashSet<string> dirRules = new(512);

                    foreach (var item in commands)
                    {
                        var source = cmdGen.toRelativePathForCompilerArgs(item.Key);
                        var mkinfo = item.Value;

                        var target = cmdGen.toRelativePathForCompilerArgs(mkinfo.outPath);
                        var targetDir = Utility.toUnixPath(Path.GetDirectoryName(target));
                        if (!dirRules.Contains(targetDir))
                        {
                            dirRules.Add(targetDir);
                            makefileOutput
                                .AppendLine($"{targetDir}:")
                                .AppendLine($"\t$(Q)mkdir -p $@");
                        }

                        var dep = Path.ChangeExtension(target, ".d");
                        var CC = quotePath(Utility.toUnixPath(replaceEnvVariable(mkinfo.exePath)));
                        var title = item.Value.sourceType.StartsWith("asm") ? "assembling" : "compiling";
                        makefileOutput
                            .AppendLine($"-include {dep}")
                            .AppendLine($"{target}: {source} Makefile | {targetDir}")
                            .AppendLine($"\t@echo {title} $< ...")
                            .AppendLine($"\t$(Q){aliasMakefileCompiler(CC)} {mkinfo.sourceArgs}")
                            .AppendLine();
                    }
                }

                /* use incremental mode */
                if (checkMode(BuilderMode.FAST))
                {
                    string ccID = cmdGen.getCompilerId().ToLower();
                    if (cliArgs.UseCcache && ccID == "gcc")
                    {
                        infoWithLable("file statistics (ccache enabled)\r\n");

                        setEnvVariable("CCACHE_DIR", outDir + Path.DirectorySeparatorChar
                            + ".ccache");
                        setEnvVariable("CCACHE_LOGFILE", outDir + Path.DirectorySeparatorChar
                            + "ccache.log");

                        var specs_mather = new Regex(@"specs=([\w][^ \\\/]+)", RegexOptions.Compiled);

                        foreach (var item in commands)
                        {
                            string _srcArgs = item.Value.sourceArgs;
                            // TO FIX ccache: Failed to stat nano.specs: No such file or directory
                            _srcArgs = specs_mather.Replace(_srcArgs, "specs=\"%TOOL_DIR%/arm-none-eabi/lib/$1\"");
                            item.Value.commandLine = $"\"{item.Value.exePath}\" " + _srcArgs;
                            item.Value.exePath     = "ccache";
                        }
                    }
                    else if (cliArgs.UseCcache && ccID == "ac6")
                    {
                        infoWithLable("file statistics (ccache enabled)\r\n");

                        setEnvVariable("CCACHE_DIR", outDir + Path.DirectorySeparatorChar
                            + ".ccache");
                        setEnvVariable("CCACHE_LOGFILE", outDir + Path.DirectorySeparatorChar
                            + "ccache.log");

                        foreach (var item in commands)
                        {
                            // skip ccache for armasm.exe
                            if (item.Value.compilerModel == "asm") continue;
                            string _srcArgs = item.Value.sourceArgs;
                            item.Value.commandLine = $"\"{item.Value.exePath}\" " + _srcArgs;
                            item.Value.exePath     = "ccache";
                        }
                    }
                    else
                    {
                        // 如果编译器位置已经更改，则需要重新编译
                        CheckDiffRes res = checkDiff(cmdGen.getCompilerId(), commands);
                        src_count_c   = res.cCount;
                        src_count_cpp = res.cppCount;
                        src_count_asm = res.asmCount;
                        commands      = res.totalCmds;
                        infoWithLable("file statistics (incremental mode)\r\n");
                    }
                }

                /* rebuild mode */
                else
                {
                    infoWithLable("file statistics (rebuild mode)\r\n");
                }

                int totalFilesCount = (src_count_c + src_count_cpp + src_count_asm + libList.Count);

                string tString = ConsoleTableBuilder
                    .From(new List<List<object>> { new List<object> { src_count_c, src_count_cpp, src_count_asm, libList.Count, totalFilesCount } })
                    .WithFormat(ConsoleTableBuilderFormat.Alternative)
                    .WithColumn(new List<string> { "C Files", "Cpp Files", "Asm Files", "Lib/Obj Files", "Totals" })
                    .Export()
                    //.Insert(0, "   ").Replace("\n", "\n   ")
                    .ToString();

                Console.Write(tString);

                // build start
                switchWorkDir(projectRoot);

                log("");
                infoWithLable("", false);

                if (!checkMode(BuilderMode.MULTHREAD) ||
                    commands.Values.Count < minFilesNumsForMultiThread)
                {
                    // print action title
                    info("start compiling ...");
                    if (commands.Count > 0) log("");

                    int total = commands.Count;
                    int curCnt = 0;

                    foreach (var cmdInfo in commands.Values)
                    {
                        curCnt++;

                        string compilerTag = getCompileLogTag(cmdInfo.sourceType);
                        string progressTag = genProgressTag(curCnt, total);

                        log(">> " + progressTag + " " + compilerTag + " '" + toHumanReadablePath(cmdInfo.sourcePath) + "'");

                        int exitCode;
                        string ccLog;

                        if (string.IsNullOrEmpty(cmdInfo.argsForSplitter)) // normal compile
                        {
                            exitCode = runExe(cmdInfo.exePath, cmdInfo.commandLine,
                                out string ccOut, cmdInfo.outputEncoding, cliArgs.DryRun);
                            ccLog = ccOut.Trim();
                        }
                        else // use source splitter
                        {
                            string[] argsLi = {
                                "--cwd", projectRoot,
                                "--outdir", Utility.toRelativePath(projectRoot, outDir) ?? outDir,
                                "--compiler-args", "\\\"" + cmdInfo.argsForSplitter + "\\\"",
                                "--compiler-dir", curEnvs["TOOL_DIR"] + Path.DirectorySeparatorChar + "bin",
                                Utility.toRelativePath(projectRoot, cmdInfo.sourcePath) ?? outDir
                            };

                            string exeArgs = string.Join(" ",
                                argsLi.Select(str => str.Contains(' ') ? ("\"" + str + "\"") : str).ToArray());

                            exitCode = runExe(sdcc_asm_optimizer, exeArgs, out string __,
                                out string resOut, out string ccOut, cmdInfo.outputEncoding, cliArgs.DryRun);
                            ccLog = ccOut.Trim();

                            // parse and set obj list
                            cmdInfo.outputs = parseSourceSplitterOutput(resOut);
                        }

                        // ignore normal output
                        if (enableNormalOut || exitCode != CODE_DONE)
                        {
                            printCompileOutput(ccLog);
                            storeCompileOutput(ccLog);
                        }

                        // compile failed.
                        if (exitCode > ERR_LEVEL)
                        {
                            errLogs.Add(ccLog);
                            string msg = "compilation failed at : \"" + cmdInfo.sourcePath + "\", exit code: " + exitCode.ToString()
                                       + "\ncommand: \n  " + cmdInfo.shellCommand;
                            throw new Exception(msg);
                        }
                        // compile ok.
                        else
                        {
                            if (!cliArgs.DryRun)
                            {
                                FileInfo cmdFile = new(cmdInfo.outPath + ".cmd");
                                var t = File.WriteAllTextAsync(cmdFile.FullName, cmdInfo.shellCommand, RuntimeEncoding.instance().UTF8);
                                ioAsyncTask.Add(t);
                            }
                        }
                    }
                }
                else
                {
                    int threads = calcuThreads(reqThreadsNum, commands.Count);
                    // reduce thread number, because module optimizer is also a multi-thread program.
                    if (cmdGen.IsUseSdccModuleOptimizer && threads >= 6) threads -= 2;
                    compileByMulThread(threads, commands.Values.ToArray(), errLogs);
                }

                // update objs list if source file have more than one objs
                foreach (var buildArgs in commands.Values)
                {
                    if (buildArgs.outputs != null &&
                        buildArgs.outputs.Length > 0)
                    {
                        var objLi = buildArgs.outputs
                                .Select(p => Path.IsPathRooted(p) ? p : (projectRoot + Path.DirectorySeparatorChar + p))
                                .ToList();

                        if (linkerObjs.ContainsKey(buildArgs.sourcePath))
                            linkerObjs[buildArgs.sourcePath] = objLi;
                        else
                            linkerObjs.Add(buildArgs.sourcePath, objLi);
                    }
                }

                // add all static libs
                linkerObjs.Add("<global>/libs", libList.ToList());

                log("");
                infoWithLable("", false);
                info("start linking ...");

                if (libList.Count > 0)
                {
                    log("");

                    foreach (var lib in libList)
                    {
                        if (lib.EndsWith(".lib") || lib.EndsWith(".a"))
                            log("add lib '" + toHumanReadablePath(lib) + "'");
                        else
                            log("add obj '" + toHumanReadablePath(lib) + "'");
                    }
                }

                var allObjs = linkerObjs
                    .SelectMany(kv => kv.Value, (kv, item) => item)
                    .ToList();

                // apply user obj order
                foreach (var orderInf in objOrderUsr)
                {
                    foreach (var objPath in allObjs)
                    {
                        var repath = Utility.toRelativePath(projectRoot, objPath, true) ?? Utility.toUnixPath(objPath);

                        if (orderInf.pattern.IsMatch(repath))
                        {
                            if (objOrder.ContainsKey(objPath))
                            {
                                objOrder[objPath] = orderInf.order;
                            }
                            else
                            {
                                objOrder.Add(objPath, orderInf.order);
                            }
                        }
                    }
                }

                // sort objs by objOrder
                allObjs.Sort((p1, p2) => {
                    int order_1 = objOrder.ContainsKey(p1) ? objOrder[p1] : Int32.MaxValue;
                    int order_2 = objOrder.ContainsKey(p2) ? objOrder[p2] : Int32.MaxValue;
                    return order_1 - order_2;
                });

                CmdGenerator.CmdInfo linkInfo = cmdGen.genLinkCommand(allObjs.ToArray());

                int linkerExitCode = runExe(linkInfo.exePath, linkInfo.commandLine,
                    out string linkerOut, linkInfo.outputEncoding, cliArgs.DryRun);

                if (!string.IsNullOrEmpty(linkerOut.Trim()))
                {
                    log(""); // newline
                    printCompileOutput(linkerOut, true);
                    storeCompileOutput(linkerOut, true);
                }

                if (linkerExitCode > ERR_LEVEL)
                {
                    errLogs.Add(linkerOut);
                    throw new Exception("link failed !, exit code: " + linkerExitCode.ToString());
                }

                // execute extra command
                var extraLinkCmds = cmdGen.genLinkerExtraCommand(linkInfo.outPath);
                foreach (CmdGenerator.LinkerExCmdInfo extraLinkerCmd in extraLinkCmds)
                {
                    if (runExe(extraLinkerCmd.exePath, extraLinkerCmd.commandLine,
                        out string cmdOutput, extraLinkerCmd.outputEncoding, cliArgs.DryRun) == CODE_DONE)
                    {
                        log("\r\n>> " + extraLinkerCmd.title);

                        /* skip empty string */
                        if (string.IsNullOrEmpty(cmdOutput))
                            continue;

                        log("\r\n" + cmdOutput, false);
                    }
                }

                if (cliArgs.OutputMakefile)
                {
                    var objdeps = allObjs.Select(p => cmdGen.toRelativePathForCompilerArgs(p));
                    var elfpath = cmdGen.toRelativePathForCompilerArgs(linkInfo.outPath);

                    // target: elf
                    var LD = quotePath(Utility.toUnixPath(replaceEnvVariable(linkInfo.exePath)));
                    makefileOutput
                        .AppendLine($"objs = {string.Join(' ', objdeps)}")
                        .AppendLine("elf: prebuild $(objs) Makefile")
                        .AppendLine($"\t@echo $(COLOR_INF)\"linking {elfpath} ...\"$(COLOR_END)");

                    if (cmdGen.getCompilerId() == "SDCC" && linkInfo.sdcc_bundleLibArgs != null)
                    {
                        makefileOutput.AppendLine($"\t$(AR) {linkInfo.sdcc_bundleLibArgs}");
                    }

                    makefileOutput.AppendLine($"\t{aliasMakefileCompiler(LD)} {linkInfo.sourceArgs}");

                    if (extraLinkCmds.Length > 0)
                    {
                        makefileOutput.AppendLine($"\t@echo $(COLOR_INF)\"execute extra link command ...\"$(COLOR_END)");

                        foreach (var cmd in extraLinkCmds)
                        {
                            var exePath = quotePath(Utility.toUnixPath(replaceEnvVariable(cmd.exePath)));
                            makefileOutput
                                .AppendLine($"\t{exePath} {cmd.commandLine}");
                        }
                    }

                    makefileOutput
                        .AppendLine();
                }

                // print map content by filter, calcu ram/rom usage
                string mapFileFullPath = linkInfo.sourcePath;
                if (mapFileFullPath != null && File.Exists(mapFileFullPath))
                {
                    try
                    {
                        int ram_size;
                        int rom_size;
                        string mapLog;

                        // parse map file
                        string ccID = cmdGen.getCompilerId().ToLower();
                        {
                            switch (ccID)
                            {
                                case "sdcc":
                                    parseMapFileForSdcc(mapFileFullPath,
                                        out ram_size, out rom_size, out mapLog);
                                    break;
                                case "iar_stm8":
                                case "iar_arm":
                                    parseMapFileForIar(mapFileFullPath,
                                        out ram_size, out rom_size, out mapLog);
                                    break;
                                default:
                                    parseMapFileForDef(mapFileFullPath, cmdGen,
                                        out ram_size, out rom_size, out mapLog);
                                    break;
                            }

                            if (ram_size == 0) ram_size = -1;
                            if (rom_size == 0) rom_size = -1;

                            if (!string.IsNullOrWhiteSpace(mapLog))
                            {
                                log("\r\n" + mapLog.TrimEnd());
                            }
                        }

                        // log mem size
                        //   if user defined 'ram_max_size' and 'rom_max_size'
                        if ((ram_size >= 0 || rom_size >= 0) &&
                            (ram_max_size > 0 && rom_max_size > 0))
                        {
                            log("");
                            log("Total Memory Usage:");
                            log("");

                            if (ram_size >= 0) // print ram usage
                            {
                                string s = $"{memorysize2str((uint)ram_size)}/{memorysize2str((uint)ram_max_size)}";
                                printProgress("  RAM: ", (float)ram_size / ram_max_size, s);
                            }

                            if (rom_size >= 0) // print rom usage
                            {
                                string s = $"{memorysize2str((uint)rom_size)}/{memorysize2str((uint)rom_max_size)}";
                                printProgress("  ROM: ", (float)rom_size / rom_max_size, s);
                            }
                        }

                        if (ccID == "ac5" || ccID == "ac6")
                        {
                            parseMapRegionInfoForArmlink(mapFileFullPath, out MapRegionInfo mapinfo);

                            if (mapinfo.load_regions.Length > 0)
                            {
                                log("");
                                log("Section Memory Usage:");

                                var makeRegionDespStr = (MapRegion region) => $"{region.name} (0x{region.addr:X8})";

                                var printRegion = (MapRegion region, int desp_max_len, int depth, string prefix) => {
                                    string s = $"{memorysize2str(region.size)}/{memorysize2str(region.max_size)}";
                                    string n = makeRegionDespStr(region);
                                    if (desp_max_len > 0) n = n.PadRight(desp_max_len);
                                    printProgress("".PadRight(depth * 2) + prefix + $"{n}: ", (float)region.size / region.max_size, s);
                                };

                                foreach (var region in mapinfo.load_regions)
                                {
                                    log("");

                                    printRegion(region.attr, 0, 1, "");

                                    if (region.children.Length > 0)
                                    {
                                        int _max_len = 0;
                                        foreach (var child in region.children)
                                        {
                                            var n = makeRegionDespStr(child);
                                            if (_max_len < n.Length)
                                                _max_len = n.Length;
                                        }

                                        foreach (var child in region.children)
                                        {
                                            printRegion(child, _max_len, 2, "- ");
                                        }
                                    }
                                    else
                                    {
                                        log("".PadRight(2 * 2) + "  " + "** This load region have no execution regions. **");
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception err)
                    {
                        warn("\r\ncan't read information from '.map' file !, " + err.Message + "\n " + err.StackTrace);
                    }
                }

                // execute output command
                CmdGenerator.CmdInfo[] commandList = (cmdGen.isDisableOutputTask()) ?
                    null :
                    cmdGen.genOutputCommand(linkInfo.outPath, cmdGen.getOutputTaskExcludes());

                if (commandList != null &&
                    commandList.Length > 0)
                {
                    log("");
                    infoWithLable("", false);
                    info("start outputting files ...");

                    foreach (CmdGenerator.CmdInfo outputCmdInfo in commandList)
                    {
                        log("\r\n>> " + outputCmdInfo.title, false);

                        string exeLog = "";

                        string task_command = "\"" + outputCmdInfo.exePath + "\" " + outputCmdInfo.commandLine;

                        try
                        {
                            string exeAbsPath = replaceEnvVariable(outputCmdInfo.exePath);

                            if (!File.Exists(exeAbsPath))
                            {
                                throw new Exception("not found " + Path.GetFileName(exeAbsPath)
                                    + " !, [path] : \"" + exeAbsPath + "\"");
                            }

                            // must use 'cmd', because SDCC has '>' command
                            int eCode = runShellCommand(task_command, out string _exe_log, null, cliArgs.DryRun);
                            exeLog = _exe_log;

                            if (eCode > ERR_LEVEL)
                                throw new Exception("execute command failed !, exit code: " + eCode.ToString());

                            // done !, output txt

                            success("\t\t[done]"); // show status after title

                            if (!string.IsNullOrEmpty(exeLog.Trim()))
                            {
                                log("\r\n" + exeLog, false);
                            }

                            string outPath = Utility.toRelativePath(projectRoot, outputCmdInfo.outPath, true) ?? outputCmdInfo.outPath;
                            log("\r\nfile path: \"" + outPath + "\"");
                        }
                        catch (Exception err)
                        {
                            error("\t\t[failed]"); // show status after title

                            error("\r\ncommand: " + task_command);

                            if (!string.IsNullOrEmpty(exeLog.Trim()))
                            {
                                log("\r\n" + exeLog, false);
                            }

                            error("\r\n" + err.Message);
                        }
                    }
                }

                if (cliArgs.OutputMakefile)
                {
                    makefileOutput.AppendLine($"bin: elf Makefile");

                    if (commandList != null && commandList.Length > 0)
                    {
                        makefileOutput.AppendLine($"\t@echo $(COLOR_INF)\"make bin files ...\"$(COLOR_END)");

                        foreach (var cmd in commandList)
                        {
                            var exePath = quotePath(Utility.toUnixPath(replaceEnvVariable(cmd.exePath)));
                            makefileOutput
                                .AppendLine($"\t{aliasMakefileCompiler(exePath)} {cmd.commandLine}");
                        }
                        makefileOutput.AppendLine();
                    }
                }

                // reset work directory
                resetWorkDir();

                TimeSpan tSpan = DateTime.Now.Subtract(time);
                log("");
                doneWithLable("", false);
                success("build successfully !, elapsed time " + string.Format("{0}:{1}:{2}", tSpan.Hours, tSpan.Minutes, tSpan.Seconds), false);
                log("", true);
                log("");

                // dump log
                appendLogs("[done]", "\tbuild successfully !");

                dumpCompilerLog();
            }
            catch (Exception err)
            {
                TimeSpan tSpan = DateTime.Now.Subtract(time);
                log("");
                errorWithLable(err.Message + "\r\n");
                errorWithLable("build failed !, elapsed time " + string.Format("{0}:{1}:{2}", tSpan.Hours, tSpan.Minutes, tSpan.Seconds));
                log("");

                // reset work dir when failed
                resetWorkDir();

                // dump error log
                appendErrLogs(err, errLogs.ToArray());

                dumpCompilerLog();

                // close and unlock log file
                unlockLogs();

                return CODE_ERR;
            }

            // wait All async IO task
            if (ioAsyncTask.Count > 0)
            {
                Task.WaitAll(ioAsyncTask.ToArray());
            }

            try
            {
                switchWorkDir(projectRoot);
                runTasks("POST-BUILD TASKS", "afterBuildTasks");
                resetWorkDir();
            }
            catch (Exception err)
            {
                errorWithLable(err.Message + "\r\n");
            }

            if (cliArgs.OutputMakefile)
            {
                File.WriteAllText(
                    projectRoot + Path.DirectorySeparatorChar + "Makefile",
                    makefileOutput.ToString());
            }

            // close and unlock log file
            unlockLogs();

            return CODE_DONE;
        }

        static int RunCommandsJson()
        {
            List<CommandInfo> cmds = new(16);
            JArray jobj = JArray.Parse(
                File.ReadAllText(cliArgs.RunCommandsJsonPath, RuntimeEncoding.instance().UTF8)
            );

            foreach (JObject item in jobj)
            {
                string program = item.ContainsKey("program") ? item["program"].Value<string>() : null;
                string command = item["command"].Value<string>();

                if (program == null)
                {
                    if (OsInfo.instance().OsType == "win32")
                    {
                        program = "cmd";
                        command = "/C \"" + command + "\"";
                    }
                    else
                    {
                        program = "/bin/bash";
                        command = "-c \"" + command + "\"";
                    }
                }

                cmds.Add(new CommandInfo {
                    title = item["title"].Value<string>(),
                    program = program,
                    command = command,
                    order = item.ContainsKey("order") ? item["order"].Value<int>() : 100,
                    ignoreFailed = item.ContainsKey("ignoreFailed") && item["ignoreFailed"].Value<bool>(),
                });
            }

            cmds.Sort(delegate (CommandInfo x, CommandInfo y) {
                return x.order - y.order;
            });

            DateTime cur_time = DateTime.Now;
            infoWithLable("", false);
            info("start building at " + cur_time.ToString("yyyy-MM-dd HH:mm:ss") + "\r\n");

            /* exec runner */
            int exitCode = executeCommands(cmds.ToArray());

            TimeSpan tSpan = DateTime.Now.Subtract(cur_time);

            log("");
            doneWithLable("", false);

            if (exitCode == CODE_DONE)
            {
                success("build successfully !, elapsed time " + string.Format("{0}:{1}:{2}", tSpan.Hours, tSpan.Minutes, tSpan.Seconds), false);
            }
            else
            {
                error("build failed !, elapsed time " + string.Format("{0}:{1}:{2}", tSpan.Hours, tSpan.Minutes, tSpan.Seconds), false);
            }

            log("", true);
            log("");

            return exitCode;
        }

        static string memorysize2str(uint size)
        {
            if (size > 1024)
            {
                if (size > 1024 * 1024)
                {
                    return $"{size / (1024.0f * 1024.0f):f1}MB";
                }
                else
                {
                    return $"{size / (1024.0f):f1}KB";
                }
            }
            else
            {
                return $"{size}B";
            }
        }

        static void parseMapRegionInfoForArmlink(string mapFileFullPath, out MapRegionInfo regionInfo)
        {
            Func<string, MapRegion> parseRegion = (string line) => {

                var m = Regex.Match(line, @"^\w+ Region\s+(?<name>[^\s]+)\s+\((?<attrs>.+)\)$");
                if (m.Success && m.Groups.Count > 2)
                {
                    MapRegion region = new MapRegion {
                        name = null,
                        addr = 0,
                        size = 0,
                        max_size = 0,
                    };

                    region.name = m.Groups["name"].Value;
                    var attrs   = m.Groups["attrs"].Value.Split(',');
                    foreach (var attr in attrs)
                    {
                        var parts = attr.Split(':');
                        if (parts.Length == 2)
                        {
                            var k = parts[0].Trim();
                            var v = parts[1].Trim();

                            if (k == "Base")
                                region.addr = Convert.ToUInt32(v, 16);
                            else if (k == "Size")
                                region.size = Convert.ToUInt32(v, 16);
                            else if (k == "Max")
                                region.max_size = Convert.ToUInt32(v, 16);
                        }
                    }

                    if (region.name != null && region.max_size > 0)
                        return region;
                }

                return null;
            };

            List<MapRegionItem> load_regions = new(4);

            MapRegionItem   cur_region = null;
            List<MapRegion> cur_children = new(10);

            foreach (string _line in File.ReadLines(mapFileFullPath))
            {
                string line_trimed = _line.Trim();

                // parse these:
                // ---
                //Load Region LR$$.ARM.__AT_0x30040000(Base: 0x30040000, Size: 0x00000000, Max: 0x00000060, ABSOLUTE)
                if (line_trimed.StartsWith("Load Region "))
                {
                    var region = parseRegion(line_trimed);
                    if (region != null)
                    {
                        if (cur_region != null)
                        {
                            cur_region.children = cur_children.ToArray();
                            load_regions.Add(cur_region);
                        }

                        cur_region = new MapRegionItem {
                            attr     = region,
                            children = null,
                        };
                        cur_children.Clear();
                    }
                }

                // parse these:
                // ---
                // Execution Region ER$$.ARM.__AT_0x30040000(Base: 0x30040000, Size: 0x00000060, Max: 0x00000060, ABSOLUTE, UNINIT)
                //  Base Addr    Size Type   Attr Idx    E Section Name Object
                //  0x30040000   0x00000060   Zero RW        59977    .ARM.__AT_0x30040000 nx_stm32_eth_driver.o
                // Execution Region ER_IROM1 (Base: 0x08000000, Size: 0x00076e70, Max: 0x00200000, ABSOLUTE)
                if (line_trimed.StartsWith("Execution Region "))
                {
                    var region = parseRegion(line_trimed);
                    if (region != null)
                    {
                        if (cur_region != null)
                        {
                            cur_children.Add(region);
                        }
                    }
                }
            }

            if (cur_region != null)
            {
                cur_region.children = cur_children.ToArray();
                load_regions.Add(cur_region);
            }

            regionInfo = new MapRegionInfo {
                load_regions = load_regions.ToArray()
            };
        }

        static void parseMapFileForIar(string mapFileFullPath,
            out int ramSize, out int romSize, out string maplog)
        {
            ramSize = 0;
            romSize = 0;
            maplog = null;

            StringBuilder mLog = new StringBuilder();

            // example:
            // 1 610 bytes of readonly  code memory
            //   136 bytes of readonly  data memory
            //   774 bytes of readwrite data memory

            List<string> strLi = new(64);

            int ro_code_size = -1;
            int ro_data_size = -1;
            int rw_data_size = -1;

            foreach (var line in File.ReadLines(mapFileFullPath))
            {
                var mRes = Regex.Match(line, @"^\s*([\d\s]+) bytes of (readonly|readwrite)\s+(code|data) memory");

                if (mRes.Success && mRes.Groups.Count > 3)
                {
                    string memSize = mRes.Groups[1].Value.Replace(" ", "");
                    string memType = mRes.Groups[2].Value + "_" + mRes.Groups[3].Value;

                    switch (memType)
                    {
                        case "readonly_code":
                            ro_code_size = int.Parse(memSize);
                            break;
                        case "readonly_data":
                            ro_data_size = int.Parse(memSize);
                            break;
                        case "readwrite_data":
                            rw_data_size = int.Parse(memSize);
                            break;
                        default:
                            break;
                    }

                    strLi.Add(line);
                }
            }

            if (ro_code_size >= 0 &&
                ro_data_size >= 0 &&
                rw_data_size >= 0)
            {
                ramSize = rw_data_size;
                romSize = ro_code_size + ro_data_size;
            }

            int firstNonSpaceIdx = 0;

            foreach (var item in strLi)
            {
                int idx = Array.FindIndex(item.ToCharArray(), (c) => {
                    return c != ' ';
                });

                if (firstNonSpaceIdx == 0 || idx < firstNonSpaceIdx)
                {
                    firstNonSpaceIdx = idx;
                }
            }

            foreach (var item in strLi)
            {
                mLog.AppendLine(item.Substring(firstNonSpaceIdx));
            }

            maplog = mLog.ToString();
        }

        struct SdccMapSectionDef
        {
            public string name;
            public int addr;
            public int size;
        };

        static void parseMapFileForSdcc(string mapFileFullPath,
            out int ramSize, out int romSize, out string maplog)
        {
            ramSize = 0;
            romSize = 0;
            maplog = null;

            StringBuilder mLog = new StringBuilder();

            // if target is mcs51, use .mem file
            string mcs51MemFilePath = Path.ChangeExtension(mapFileFullPath, ".mem");
            if (mcs51MemFilePath != null && File.Exists(mcs51MemFilePath))
            {
                bool isMatched = false;

                foreach (string line in File.ReadLines(mcs51MemFilePath))
                {
                    if (isMatched == false && (line.StartsWith("Stack starts") || line.StartsWith("Other memory:")))
                        isMatched = true;

                    if (isMatched) mLog.AppendLine(line);
                }

                maplog = mLog.ToString();
                return;
            }

            Dictionary<string, SdccMapSectionDef> secList = new(32);

            //
            // example line:
            //
            // Area Addr        Size Decimal Bytes(Attributes)
            // -------------------------------        ----        ----        ------- ----- -------------
            // GSFINAL                             0000005F    00000003 =           3. bytes (REL,CON,CODE)
            //

            const int SM_STATE_READY = 0;
            const int SM_STATE_IN_AREA = 1;
            const int SM_STATE_IN_AREA_INFO_LINE = 2;

            int cur_state = SM_STATE_READY;

            foreach (string line in File.ReadLines(mapFileFullPath))
            {
                switch (cur_state)
                {
                    case SM_STATE_READY:
                        {
                            if (line.TrimStart().StartsWith("Area"))
                            {
                                cur_state = SM_STATE_IN_AREA;
                            }
                        }
                        break;
                    case SM_STATE_IN_AREA:
                        {
                            if (line.TrimStart().StartsWith("Area"))
                            {
                                cur_state = SM_STATE_IN_AREA;
                            }
                            else if (line.TrimStart().StartsWith("----------"))
                            {
                                cur_state = SM_STATE_IN_AREA_INFO_LINE;
                            }
                            else
                            {
                                cur_state = SM_STATE_READY;
                            }
                        }
                        break;
                    case SM_STATE_IN_AREA_INFO_LINE:
                        {
                            var mRes = Regex.Match(line, @"^\s*([\w-]+)\s+([0-9a-f]+)\s+([0-9a-f]+)", RegexOptions.IgnoreCase);

                            if (mRes.Success && mRes.Groups.Count > 3)
                            {
                                var secInf = new SdccMapSectionDef {
                                    name = mRes.Groups[1].Value,
                                    addr = int.Parse(mRes.Groups[2].Value, System.Globalization.NumberStyles.HexNumber),
                                    size = int.Parse(mRes.Groups[3].Value, System.Globalization.NumberStyles.HexNumber),
                                };

                                secList.TryAdd(secInf.name, secInf);
                            }

                            cur_state = SM_STATE_READY;
                        }
                        break;
                    default: // err state
                        cur_state = SM_STATE_READY;
                        break;
                }
            }

            // calcu mem size
            // ref: https://sourceforge.net/p/sdcc/discussion/1864/thread/f26b730d/?limit=25#c47f

            string[] ramSegLi = { "DATA", "INITALIZED", "SSEG" };
            string[] romSegLi = { "CODE", "CONST", "INITIALIZER", "GSINIT", "HOME", "GSFINAL" };

            foreach (string name in ramSegLi)
            {
                if (secList.TryGetValue(name, out var secInfo))
                {
                    ramSize += secInfo.size;
                }
            }

            foreach (string name in romSegLi)
            {
                if (secList.TryGetValue(name, out var secInfo))
                {
                    romSize += secInfo.size;
                }
            }

            List<List<object>> tableData = new(64);

            foreach (var item in secList.Values)
            {
                tableData.Add(new List<object> {
                    item.name,
                    item.addr.ToString("X8"),
                    item.size.ToString("X8"),
                    item.size.ToString(),
                });
            }

            mLog.AppendLine(ConsoleTableBuilder
                .From(tableData)
                .WithFormat(ConsoleTableBuilderFormat.Minimal)
                .WithColumn("Segment", "Address", "Size", "Size(Decimal)")
                .WithMinLength(new Dictionary<int, int> {
                    { 0, 12 }, { 1, 12 }, { 2, 12 }, { 3, 12 }
                })
                .Export().ToString().Trim());

            mLog.AppendLine();

            string stack_prompt_txt = "";

            // if sseg.size == 1, the stack size is 'MCU_RAM_SIZE - ALLOCATED_RAM_SIZE'
            // it's 'AUTO STACK SIZE', append a log to prompt it 
            if (secList.TryGetValue("SSEG", out var ssegInf) &&
                ssegInf.size == 1)
            {
                if (ram_max_size > 0)
                    stack_prompt_txt = string.Format(", Stack Size: {0} Bytes (MCU_RAM_SIZE - RAM_TOTAL)", ram_max_size - ramSize);
                else
                    stack_prompt_txt = ", Stack Size: Auto (MCU_RAM_SIZE - RAM_TOTAL)";
            }

            int maxPadWidth = (ramSize > romSize ? ramSize : romSize).ToString().Length;

            mLog.AppendLine("RAM Total: "
                + ramSize.ToString().PadRight(maxPadWidth) + " Bytes (" + string.Join(" + ", ramSegLi) + ")"
                + stack_prompt_txt);

            mLog.AppendLine("ROM Total: "
                + romSize.ToString().PadRight(maxPadWidth) + " Bytes (" + string.Join(" + ", romSegLi) + ")");

            maplog = mLog.ToString();
        }

        static void parseMapFileForDef(string mapFileFullPath, CmdGenerator cmdGen,
            out int ramSize, out int romSize, out string maplog)
        {
            ramSize = -1;
            romSize = -1;
            maplog = null;

            StringBuilder mLog = new StringBuilder();

            Regex ramReg = cmdGen.getRamSizeMatcher();
            Regex romReg = cmdGen.getRomSizeMatcher();
            Regex[] regList = cmdGen.getMapMatcher().ConvertAll((string reg) => {
                return new Regex(reg, RegexOptions.IgnoreCase | RegexOptions.Compiled);
            }).ToArray();

            foreach (string line in File.ReadLines(mapFileFullPath))
            {
                if (Array.FindIndex(regList, (Regex reg) => { return reg.IsMatch(line); }) != -1)
                {
                    mLog.AppendLine(line.Trim());

                    if (ramSize == -1 && ramReg != null)
                    {
                        Match matcher = ramReg.Match(line);
                        if (matcher.Success && matcher.Groups.Count > 1)
                        {
                            ramSize = int.Parse(matcher.Groups[1].Value);
                        }
                    }

                    if (romSize == -1 && romReg != null)
                    {
                        Match matcher = romReg.Match(line);
                        if (matcher.Success && matcher.Groups.Count > 1)
                        {
                            romSize = int.Parse(matcher.Groups[1].Value);
                        }
                    }
                }
            }

            maplog = mLog.ToString();
        }

        static void printAppInfo()
        {
            string appName = Assembly.GetExecutingAssembly().GetName().Name;
            log("app_name: " + appName);
            log("app_version: " + "v" + Assembly.GetExecutingAssembly().GetName().Version);
            log("os: " + OsInfo.instance().OsType);
            log("codepage: " + RuntimeEncoding.instance().CurrentCodePage.ToString());
        }

        /**
         * @ret string '<rendered_output> + CRLF'
         */
        static string renderCompilerOutput(string output, bool isLinker = false)
        {
            if (string.IsNullOrWhiteSpace(output)) return ""; // is an empty line

            if (!colorRendererEnabled) return output + OsInfo.instance().CRLF;

            StringBuilder ret = new StringBuilder();

            string[] lines = CRLFMatcher.Split(output);

            // search first non-empty line
            int startIdx = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(lines[i]))
                {
                    startIdx = i;
                    break;
                }
            }

            // search last non-empty line
            int endIdx;
            for (endIdx = lines.Length - 1; endIdx >= 0; endIdx--)
            {
                if (!string.IsNullOrWhiteSpace(lines[endIdx]))
                {
                    break;
                }
            }

            // select render
            var renderMap = isLinker ? lkOutputRender : ccOutputRender;

            for (int i = startIdx; i <= endIdx; i++)
            {
                string line_ = lines[i];

                if (string.IsNullOrWhiteSpace(line_))
                {
                    ret.AppendLine(line_);
                    continue;
                }

                string line = line_;

                foreach (var render in renderMap)
                {
                    line = render.Key.Replace(line, render.Value);
                }

                ret.AppendLine(line);
            }

            return ret.ToString();
        }

        static void printCompileOutput(string output, bool isLinker = false)
        {
            string s = renderCompilerOutput(output, isLinker);
            Console.Write(s);
        }

        static List<string> compiler_log_cpp = new(256);
        static List<string> compiler_log_lnk = new(256);
        static void storeCompileOutput(string output, bool isLinker = false)
        {
            if (isLinker) compiler_log_lnk.Add(output);
            else compiler_log_cpp.Add(output);
        }

        static void printProgress(string label, float progress, string suffix = "")
        {
            const int BAR_MAX_LEN = 20;
            char[] barTxt = new char[BAR_MAX_LEN];

            // fill progress bar
            {
                int num = (int)((progress * BAR_MAX_LEN) + 0.45f);
                num = num > BAR_MAX_LEN ? BAR_MAX_LEN : num;

                for (int i = 0; i < BAR_MAX_LEN; i++)
                    barTxt[i] = ' ';

                for (int i = 0; i < num; i++)
                    barTxt[i] = '#';
            }

            string progTxt = label + "[" + new string(barTxt) + "] "
                + ((progress * 100).ToString("f1") + "%").PadRight(9)
                + (" " + suffix);

            if (progress > 1.0f)
            {
                error(progTxt);
            }
            else if (progress >= 0.95f)
            {
                warn(progTxt);
            }
            else
            {
                log(progTxt);
            }
        }

        static int calcuThreads(int threads, int cmdCount)
        {
            if (threads < 2)
            {
                return 4;
            }

            int minThread = threads >= 8 ? (threads / 4) : 2;
            int maxThread = threads;
            int expactThread = threads >= 4 ? (threads / 2) : 4;

            if (cmdCount / maxThread >= 2)
            {
                return maxThread;
            }

            if (cmdCount / expactThread >= 2)
            {
                return expactThread;
            }

            if (cmdCount / minThread >= 2)
            {
                return minThread;
            }

            return 8;
        }

        static void switchWorkDir(string path)
        {
            try
            {
                Environment.CurrentDirectory = path;
            }
            catch (DirectoryNotFoundException e)
            {
                throw new Exception("Switch workspace failed ! Not found directory: " + path, e);
            }
        }

        static void resetWorkDir()
        {
            Environment.CurrentDirectory = appBaseDir;
        }

        //---

        static void addCliVar(string key, string val)
        {
            if (cliVars.ContainsKey(key))
            {
                cliVars[key] = val;
            }
            else
            {
                cliVars.Add(key, val);
            }
        }

        static void setEnvValue(string key, string value)
        {
            // insert to 'PATH'
            if (key.ToLower() == "path")
            {
                key = OsInfo.instance().OsType == "win32" ? "Path" : "PATH";

                string PATH_VAL = Environment.GetEnvironmentVariable(key);

                if (PATH_VAL != null) // found 'PATH', append it
                {
                    PATH_VAL = value + Path.PathSeparator + PATH_VAL;
                    Environment.SetEnvironmentVariable(key, PATH_VAL);
                }
                else // not found 'PATH', set it
                {
                    Environment.SetEnvironmentVariable(key, value);
                }
            }

            // set env
            else
            {
                if (curEnvs.ContainsKey(key)) curEnvs[key] = value;
                else curEnvs.Add(key, value);
                Environment.SetEnvironmentVariable(key, value);
            }
        }

        public static void setEnvVariable(string key, string value)
        {
            setEnvValue(key, value);
        }

        private static readonly Regex env_exprMatcher1 = new(@"\$\{[\w]+\}", RegexOptions.Compiled);
        private static readonly Regex env_exprMatcher2 = new(@"\$\([\w]+\)", RegexOptions.Compiled);

        public static string replaceEnvVariable(string str, bool make_undef_var_as_empty = false)
        {
            // max deep: 5
            for (int i = 0; i < 5; i++)
            {
                if (!(str.Contains('%') || str.Contains("$(") || str.Contains("${")))
                    break; // no any variable in str, end

                foreach (var keyValue in curEnvs)
                {
                    str = str
                        .Replace("%" + keyValue.Key + "%", keyValue.Value)
                        .Replace("${" + keyValue.Key + "}", keyValue.Value)
                        .Replace("$(" + keyValue.Key + ")", keyValue.Value);
                }
            }

            // resolve unknown vars, set empty value for them !
            if (make_undef_var_as_empty)
            {
                for (int i = 0; i < 3; i++)
                {
                    if (!(str.Contains("$(") || str.Contains("${")))
                        break; // no any variable in str, end

                    str = env_exprMatcher1.Replace(str, "");
                    str = env_exprMatcher2.Replace(str, "");
                }
            }

            return str;
        }

        //////////////////////////////////////////////////
        ///             command runner
        //////////////////////////////////////////////////

        struct CommandInfo
        {
            public string title;
            public string program;
            public string command;
            public int order;
            public bool ignoreFailed;
        };

        static int executeCommands(CommandInfo[] cmds)
        {
            int titleMaxLen = 0;

            foreach (CommandInfo cmd in cmds)
            {
                if (cmd.title.Length > titleMaxLen)
                {
                    titleMaxLen = cmd.title.Length;
                }
            }

            bool hasFailed = false;

            for (int idx = 0; idx < cmds.Length; idx++)
            {
                CommandInfo cmd = cmds[idx];

                /* run command */
                log(">> " + cmd.title + getBlanks(titleMaxLen - cmd.title.Length) + " ...\t", false);

                int exit_code = runExe(cmd.program, cmd.command, out string cmd_output);

                /* append command runner flag */
                if (exit_code == CODE_DONE)
                    success("[done]");
                else
                    error("[failed]");

                if (exit_code != CODE_DONE)
                {
                    hasFailed = true;

                    log("");
                    string logTxt = "   " + cmd_output;
                    Console.Write(Regex.Replace(logTxt, @"(?<enter>\n)", "${enter}   "));

                    if (!cmd.ignoreFailed)
                    {
                        return exit_code;
                    }

                    // if not last cmd, print a newline
                    if (idx != cmds.Length - 1)
                    {
                        log("");
                    }
                }
            }

            return hasFailed ? CODE_ERR : CODE_DONE;
        }

        //////////////////////////////////////////////////
        ///             utility function
        //////////////////////////////////////////////////


        public static string genProgressTag(int curVal, int total)
        {
            if (curVal >= total)
                return "[100%]";

            string progress = (curVal * 100 / total).ToString();

            int wsSize = 3 - progress.Length;
            if (wsSize > 0) progress = new String(' ', wsSize) + progress;

            return "[" + progress + "%]";
        }

        public static int runExe(string exe, string args, out string output_, Encoding encoding = null, bool dryRun = false)
        {
            return runExe(exe, args, out output_, out string _, out string __, encoding, dryRun);
        }

        public static int runExe(
            string filename, string args,
            out string output_, out string stdOut_, out string stdErr_,
            Encoding encoding = null, bool dryRun = false)
        {
            if (dryRun)
            {
                output_ = "";
                stdOut_ = "";
                stdErr_ = "";
                return 0;
            }

            // if executable is 'cmd.exe', force use ascii
            if (filename == "cmd" ||
                filename == "cmd.exe")
            {
                encoding = RuntimeEncoding.instance().Default;
            }

            Process process = new();
            process.StartInfo.FileName = replaceEnvVariable(filename);
            process.StartInfo.Arguments = replaceEnvVariable(args);
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.StandardOutputEncoding = encoding ?? RuntimeEncoding.instance().Default;
            process.StartInfo.StandardErrorEncoding = encoding ?? RuntimeEncoding.instance().Default;
            process.Start();

            StringBuilder output = new();
            StringBuilder stdOut = new();
            StringBuilder stdErr = new();

            process.OutputDataReceived += delegate (object sender, DataReceivedEventArgs e) {

                if (e.Data == null) return;

                stdOut.AppendLine(e.Data);

                lock (output)
                {
                    output.AppendLine(e.Data);
                }
            };

            process.ErrorDataReceived += delegate (object sender, DataReceivedEventArgs e) {

                if (e.Data == null) return;

                lock (output)
                {
                    output.AppendLine(e.Data);
                }

                stdErr.AppendLine(e.Data);
            };

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            process.WaitForExit();
            int exitCode = process.ExitCode;
            process.Close();

            output_ = output.ToString();
            stdOut_ = stdOut.ToString();
            stdErr_ = stdErr.ToString();

            return exitCode;
        }

        public static int runShellCommand(string command, out string _output, Encoding encoding = null, bool dryRun = false)
        {
            string filename;
            string args;

            if (OsInfo.instance().OsType == "win32")
            {
                filename = "cmd";
                args = "/C \"" + command + "\"";
            }
            else
            {
                filename = "/bin/bash";
                args = "-c \"" + command + "\"";
            }

            return runExe(filename, args, out _output, encoding, dryRun);
        }

        static string[] parseSourceSplitterOutput(string log)
        {
            return parseSourceSplitterOutput(CRLFMatcher.Split(log));
        }

        static string[] parseSourceSplitterOutput(IEnumerable<string> lines)
        {
            List<string> res = new(64);

            bool headerMatched = false;

            foreach (var line_ in lines)
            {
                var line = line_.Trim();

                if (headerMatched)
                {
                    if (line.StartsWith("<---"))
                        break; // go end, exit
                    else if (!string.IsNullOrWhiteSpace(line))
                        res.Add(line);
                }
                else
                {
                    headerMatched = line.StartsWith("--->");
                }
            }

            return res.ToArray();
        }

        static string getCompileLogTag(string sourceType)
        {
            return sourceType switch {
                "c"   => "CC",
                "asm" => "AS",
                "cpp" => "CXX",
                _ => "CC",
            };
        }

        struct CompilerLogData
        {
            public string logTxt;
            public CmdGenerator.CmdInfo srcInfo;
        }

        static void compileByMulThread(int thrNum, CmdGenerator.CmdInfo[] cmds_, List<string> errLogs)
        {
            // print title
            info("start compiling (jobs: " + thrNum.ToString() + ") ...");
            if (cmds_.Length > 0) log("");

            Exception err = null;
            bool isCompileDone = false;

            BlockingCollection<CompilerLogData> ccLogQueue = new();
            BlockingCollection<CmdGenerator.CmdInfo> cmdQueue = new();

            // fill data
            foreach (var item in cmds_) cmdQueue.Add(item);

            // create logger thread
            Thread compilerLogger;
            {
                compilerLogger = new Thread(delegate () {

                    int curProgress = 0;
                    int tolProgress = cmds_.Length;

                    while (true)
                    {
                        if (isCompileDone && ccLogQueue.Count == 0)
                            break; // exit

                        if (ccLogQueue.TryTake(out CompilerLogData logData, 100))
                        {
                            string compilerTag = getCompileLogTag(logData.srcInfo.sourceType);
                            string humanRdPath = toHumanReadablePath(logData.srcInfo.sourcePath);

                            // log progress
                            string progressTag = genProgressTag(++curProgress, tolProgress);
                            string progressLog = ">> " + progressTag + " " + compilerTag + " '" + humanRdPath + "'";
                            Console.WriteLine(progressLog);

                            if (!string.IsNullOrWhiteSpace(logData.logTxt))
                            {
                                Console.Write(renderCompilerOutput(logData.logTxt));
                                storeCompileOutput(logData.logTxt);
                            }
                        }
                    }
                });

                compilerLogger.Start();
            }

            // compiler worker
            var workerFunc = () => {

                while (cmdQueue.Count > 0)
                {
                    if (err != null)
                        break; // sometask compile error, exit

                    if (!cmdQueue.TryTake(out CmdGenerator.CmdInfo ccArgs, 100))
                        continue;

                    int exitCode;
                    string cclog;

                    // do compile

                    if (string.IsNullOrEmpty(ccArgs.argsForSplitter))
                    {
                        exitCode = runExe(
                            ccArgs.exePath, ccArgs.commandLine,
                            out string output, ccArgs.outputEncoding, cliArgs.DryRun);

                        cclog = output.Trim();
                    }
                    else
                    {
                        string[] argsLi = {
                            "--cwd", projectRoot,
                            "--outdir", Utility.toRelativePath(projectRoot, outDir) ?? outDir,
                            "--compiler-args", "\\\"" + ccArgs.argsForSplitter + "\\\"",
                            "--compiler-dir", curEnvs["TOOL_DIR"] + Path.DirectorySeparatorChar + "bin",
                            Utility.toRelativePath(projectRoot, ccArgs.sourcePath) ?? outDir
                        };

                        string exeArgs = string.Join(" ",
                            argsLi.Select(str => str.Contains(' ') ? ("\"" + str + "\"") : str).ToArray());

                        exitCode = runExe(sdcc_asm_optimizer, exeArgs, out string __,
                            out string resultOut, out string ccOut, ccArgs.outputEncoding, cliArgs.DryRun);

                        cclog = ccOut.Trim();

                        // parse and set obj list
                        ccArgs.outputs = parseSourceSplitterOutput(resultOut);
                    }

                    // need ignore normal output ?
                    bool isLogEn = enableNormalOut || exitCode != CODE_DONE;

                    // post log data
                    ccLogQueue.Add(new CompilerLogData {
                        srcInfo = ccArgs,
                        logTxt = isLogEn ? cclog : null,
                    });

                    // compile failed.
                    if (exitCode > ERR_LEVEL)
                    {
                        lock (errLogs)
                        {
                            errLogs.Add(cclog);
                        }

                        string msg = "compilation failed at : \"" + ccArgs.sourcePath + "\", exit code: " + exitCode.ToString()
                                   + "\ncommand: \n  " + ccArgs.shellCommand;

                        err = new Exception(msg);
                        break;
                    }
                    // compile ok
                    else
                    {
                        if (!cliArgs.DryRun)
                        {
                            FileInfo cmdFile = new(ccArgs.outPath + ".cmd");
                            var t = File.WriteAllTextAsync(cmdFile.FullName, ccArgs.shellCommand, RuntimeEncoding.instance().UTF8);
                            ioAsyncTask.Add(t);
                        }
                    }
                }
            };

            // alloc work threads and start compile
            Task[] tasks = new Task[thrNum];
            {
                for (int i = 0; i < thrNum; i++)
                {
                    tasks[i] = Task.Run(workerFunc);
                }
            }
            Task.WaitAll(tasks);

            isCompileDone = true;   // notify logger the builder is end
            compilerLogger.Join();  // wait logger end

            if (err != null)
            {
                throw err;
            }
        }

        static string getBlanks(int num)
        {
            if (num <= 0) return "";

            char[] buf = new char[num];

            for (int i = 0; i < num; i++)
            {
                buf[i] = ' ';
            }

            return new string(buf);
        }

        static int runTasks(string label, string fieldName)
        {
            var vars = new Dictionary<string, string>(64);
            {
                foreach (var kv in curEnvs) vars.TryAdd(kv.Key, kv.Value);
                foreach (var kv in cliVars) vars.TryAdd(kv.Key, kv.Value);
            }

            JObject options = (JObject)paramsObj[CmdGenerator.optionKey];

            if (options.ContainsKey(fieldName))
            {
                try
                {
                    JArray taskList = (JArray)options[fieldName];

                    if (cliArgs.OutputMakefile)
                    {
                        if (fieldName == "beforeBuildTasks")
                            makefileOutput
                                .AppendLine("prebuild:")
                                .AppendLine("\t@echo $(COLOR_INF)\"prebuild ...\"$(COLOR_END)");
                        else
                            makefileOutput
                                .AppendLine("postbuild: bin")
                                .AppendLine("\t@echo $(COLOR_INF)\"postbuild ...\"$(COLOR_END)");
                    }

                    if (taskList.Count == 0)
                    {
                        if (fieldName == "beforeBuildTasks")
                            makefileOutput.AppendLine("\t@echo nothing to prebuild.").AppendLine();
                        else
                            makefileOutput.AppendLine("\t@echo nothing to postbuild.").AppendLine();
                        return CODE_DONE;
                    }

                    // check available task numbers
                    int availableCount = 0;
                    foreach (JObject cmd in taskList)
                    {
                        if (cmd.ContainsKey("disable")
                            && cmd["disable"].Type == JTokenType.Boolean
                            && cmd["disable"].Value<bool>())
                        {
                            // task is disabled, ignore it !
                            continue;
                        }

                        availableCount++;
                    }

                    if (availableCount == 0)
                    {
                        return CODE_DONE;
                    }

                    infoWithLable("", false);
                    info(label.ToLower() + " ...");

                    // get max length
                    int maxLen = -1;
                    foreach (JObject cmd in taskList)
                    {
                        if (cmd.ContainsKey("name"))
                        {
                            string name = cmd["name"].Value<string>();
                            maxLen = name.Length > maxLen ? name.Length : maxLen;
                        }
                    }

                    foreach (JObject cmd in taskList)
                    {
                        if (cmd.ContainsKey("disable")
                            && cmd["disable"].Type == JTokenType.Boolean
                            && cmd["disable"].Value<bool>())
                            continue; // task is disabled, ignore it !

                        if (!cmd.ContainsKey("command"))
                            continue; // no command, ignored

                        string command = cmd["command"].Value<string>().Trim();

                        if (string.IsNullOrEmpty(command))
                            continue; // empty command, ignored

                        string taskName = command;

                        if (cmd.ContainsKey("name"))
                        {
                            var n = cmd["name"].Value<string>().Trim();

                            if (!string.IsNullOrEmpty(n))
                            {
                                taskName = n;
                            }
                        }

                        // print task name
                        log("\r\n>> " + taskName + getBlanks(maxLen - taskName.Length) + "\t\t", false);

                        // makefile target command
                        string makefileTargetCmd = Regex
                            .Replace(command, @"%(\w+)%", "$($1)")
                            .Replace(toolchainRoot, "$(COMPILER_DIR)");

                        // replace env path
                        bool useBashInCmd = OsInfo.instance().OsType == "win32" &&
                            Regex.IsMatch(command, "^(?:.+\\b)?bash(?:\\.exe)?(?:\\s|\")", RegexOptions.IgnoreCase);
                        for (int i = 0; i < 5; i++)
                        {
                            if (!command.Contains("${"))
                                break;

                            foreach (var kv in vars)
                            {
                                var value = kv.Value;

                                // '\' -> '/' in var for win32 bash
                                if (useBashInCmd)
                                {
                                    value = Utility.toUnixPath(value);
                                }

                                command = Regex.Replace(command, "\\$\\{" + kv.Key + "\\}", value, RegexOptions.IgnoreCase);

                                if (cliArgs.OutputMakefile)
                                {
                                    var replaceValue = value;
                                    if (OsInfo.instance().OsType == "win32")
                                    {
                                        var path = replaceValue;
                                        if (Utility.isAbsolutePath(path))
                                            path = Utility.toRelativePath(projectRoot, path) ?? path;
                                        if (Regex.IsMatch(kv.Key, @"path|dir\b|directory|folder|root\b", RegexOptions.IgnoreCase))
                                            path = Utility.toUnixPath(path);
                                        // replace var: ${ToolchainRoot}
                                        if (kv.Key == "ToolchainRoot")
                                            path = "$(COMPILER_DIR)";
                                        else if (Utility.toUnixPath(path).StartsWith(Utility.toUnixPath(toolchainRoot)))
                                            path = Utility.toUnixPath(path)
                                                .Replace(Utility.toUnixPath(toolchainRoot), "$(COMPILER_DIR)");
                                        replaceValue = path;
                                    }
                                    makefileTargetCmd = Regex.Replace(
                                        makefileTargetCmd, "\\$\\{" + kv.Key + "\\}", replaceValue, RegexOptions.IgnoreCase);
                                }
                            }
                        }

                        if (cliArgs.OutputMakefile)
                        {
                            makefileOutput.AppendLine("\t" + makefileTargetCmd);
                        }

                        // run command
                        if (runShellCommand(command, out string cmdStdout, null, cliArgs.DryRun) == CODE_DONE)
                        {
                            success("[done]");
                            if (!string.IsNullOrEmpty(cmdStdout.Trim()))
                                log("\r\n" + cmdStdout, false);
                        }
                        else
                        {
                            error("[failed]");
                            log("\r\n" + command);
                            if (!string.IsNullOrEmpty(cmdStdout.Trim()))
                                error("\r\n" + cmdStdout, false);

                            if (cmd.ContainsKey("stopBuildAfterFailed")
                                && cmd["stopBuildAfterFailed"].Type == JTokenType.Boolean
                                && cmd["stopBuildAfterFailed"].Value<bool>())
                                return CODE_ERR;

                            if (cmd.ContainsKey("abortAfterFailed")
                                && cmd["abortAfterFailed"].Type == JTokenType.Boolean
                                && cmd["abortAfterFailed"].Value<bool>())
                                break;
                        }
                    }

                    if (cliArgs.OutputMakefile)
                    {
                        makefileOutput.AppendLine();
                    }

                    log(""); // empty line
                }
                catch (Exception e)
                {
                    error("failed on '" + label + "', msg: " + e.Message);
                }
            }

            return CODE_DONE;
        }

        static bool checkMode(BuilderMode mode)
        {
            return modeList.Contains(mode);
        }

        class CheckDiffRes
        {
            public int cCount;
            public int cppCount;
            public int asmCount;
            public Dictionary<string, CmdGenerator.CmdInfo> totalCmds;

            public CheckDiffRes()
            {
                cCount = cppCount = asmCount = 0;
                totalCmds = new(512);
            }
        }

        static Dictionary<string, DateTime> _srcRefsMtCache = new(512);
        static DateTime getSrcRefLastModifyTime(string fpath)
        {
            if (_srcRefsMtCache.ContainsKey(fpath))
                return _srcRefsMtCache[fpath];

            if (!File.Exists(fpath))
                throw new FileNotFoundException($"no such file '{fpath}'");

            var lastWrTime = File.GetLastWriteTime(fpath);
            _srcRefsMtCache.Add(fpath, lastWrTime);

            return lastWrTime;
        }

        static CheckDiffRes checkDiff(string modelID, Dictionary<string, CmdGenerator.CmdInfo> commands)
        {
            CheckDiffRes res = new CheckDiffRes();
            List<string> diffLogs = new List<string>(256);

            Func<CmdGenerator.CmdInfo, bool> AddToChangeList = (cmd) => {

                switch (cmd.sourceType)
                {
                    case "c":
                        res.cCount++;
                        break;
                    case "cpp":
                        res.cppCount++;
                        break;
                    case "asm":
                        res.asmCount++;
                        break;
                    default:
                        break;
                }

                res.totalCmds.Add(cmd.sourcePath, cmd);

                return true;
            };

            try
            {
                foreach (var cmd in commands.Values)
                {
                    if (!File.Exists(cmd.outPath)) // not compiled
                    {
                        AddToChangeList(cmd);
                        diffLogs.Add($"'{cmd.sourcePath}': object (.o) file not exist");
                        continue;
                    }

                    DateTime objLastWriteTime = File.GetLastWriteTime(cmd.outPath);
                    DateTime srcLastWriteTime = File.GetLastWriteTime(cmd.sourcePath);

                    // source always in build
                    if (alwaysInBuildSources.Contains(cmd.sourcePath))
                    {
                        AddToChangeList(cmd);
                        diffLogs.Add($"'{cmd.sourcePath}': source file is always in build.");
                    }
                    // src file is newer than obj file
                    else if (DateTime.Compare(srcLastWriteTime, objLastWriteTime) > 0)
                    {
                        AddToChangeList(cmd);
                        diffLogs.Add($"'{cmd.sourcePath}': source file has been changed.");
                    }

                    // source args is changed
                    else if (cmd.sourceArgsChanged)
                    {
                        AddToChangeList(cmd);
                        diffLogs.Add($"'{cmd.sourcePath}': compiler options has been changed.");
                    }

                    // reference is changed ?
                    else
                    {
                        string refFilePath = Path.GetDirectoryName(cmd.outPath)
                            + Path.DirectorySeparatorChar + Path.GetFileNameWithoutExtension(cmd.outPath) + ".d";

                        if (!File.Exists(refFilePath))
                        {
                            AddToChangeList(cmd);
                            diffLogs.Add($"'{cmd.sourcePath}': dependence (.d) file not exist");
                            continue; // not found ref file
                        }

                        var refList = parseRefFile(refFilePath, modelID)
                            .Where(p => p != cmd.outPath && p != cmd.sourcePath);

                        foreach (var refpath in refList)
                        {
                            try
                            {
                                var lastModifyTime = getSrcRefLastModifyTime(refpath);

                                if (DateTime.Compare(lastModifyTime, objLastWriteTime) > 0)
                                {
                                    AddToChangeList(cmd);
                                    diffLogs.Add($"'{cmd.sourcePath}': dependence '{refpath}' has been changed.");
                                    break; // out of date, need recompile, exit
                                }
                            }
                            catch (FileNotFoundException e)
                            {
                                AddToChangeList(cmd);
                                diffLogs.Add($"'{cmd.sourcePath}': check dependence failed, msg: {e.Message}");
                                break; // out of date, need recompile, exit
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                log("");
                warn(e.Message);
                log("");
                warnWithLable("Check difference failed !, will rollback to rebuild mode.");
                log("");

                // fill all cmds
                res = new CheckDiffRes();
                foreach (var cmd in commands.Values)
                    AddToChangeList(cmd);
                appendLogs($"[warn] incremental build: check difference failed, rollback to rebuild mode.",
                    e.Message);
            }

            if (diffLogs.Count > 0)
            {
                appendLogs($"[info] incremental build: {diffLogs.Count} source files changed",
                    "These source files will be recompiled", diffLogs.ToArray());
            }

            return res;
        }

        static string toAbsolutePath(string _repath)
        {
            string repath = _repath.Trim();

            if (Utility.isAbsolutePath(repath))
            {
                return repath;
            }

            return Utility.formatPath(projectRoot + Path.DirectorySeparatorChar + repath);
        }

        static string toHumanReadablePath(string absPath)
        {
            return showRelativePathOnLog ?
                Utility.toRelativePath(projectRoot, absPath, true) ?? absPath :
                Path.GetFileName(absPath);
        }

        static Regex md_whitespaceMatcher = new Regex(@"(?<![\\:]) ", RegexOptions.Compiled);
        static char[] md_trimEndChars = { '\\', ' ', '\t', '\v', '\r', '\n' };

        // example input
        //  build/Debug/.obj/__/__/fwlib/STM32F10x_DSP_Lib/src/iir_stm32.o: \
        //   ../../fwlib/STM32F10x_DSP_Lib/src/iir_stm32.c \
        //   ../../fwlib/STM32F10x_DSP_Lib/inc/stm32_dsp.h \
        //   ../../fwlib/CM3/system_stm32f2xx.h ../../fwlib/CM3/stm32f2xx_conf.h\  \
        //   ../../fwlib/STM32F2xx_StdPeriph_Driver/inc/stm32f2xx_wwdg.h \
        //   ../../fwlib/STM32F2xx_StdPeriph_Driver/inc/misc.h
        static string[] gnu_parseRefLines(string[] lines)
        {
            HashSet<string> resultList = new HashSet<string>();
            int resultCnt = 0;

            for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                string line = lines[lineIndex].Trim().TrimEnd(md_trimEndChars); // remove char '\' end of line

                if (lineIndex == 0) // first line is makefile dep format: '<obj>: <deps>'
                {
                    int sepIndex = line.IndexOf(": ");
                    if (sepIndex > 0) line = line.Substring(sepIndex + 1).Trim();
                    else continue; /* line is invalid, skip */
                }

                string[] subLines = md_whitespaceMatcher.Split(line);

                foreach (string subLine in subLines)
                {
                    if (string.IsNullOrWhiteSpace(subLine)) continue;

                    resultCnt++;

                    if (resultCnt == 1) continue; /* skip first ref, it's src self */

                    var _path = subLine
                        .Replace("\\ ", " ").Replace("\\:", ":")
                        .TrimEnd(md_trimEndChars);
                    resultList.Add(toAbsolutePath(_path));
                }
            }

            string[] resList = new string[resultList.Count];
            resultList.CopyTo(resList);

            return resList;
        }

        static string[] ac5_parseRefLines(string[] lines, int startIndex = 1)
        {
            HashSet<string> resultList = new HashSet<string>();

            for (int i = startIndex; i < lines.Length; i++)
            {
                int sepIndex = lines[i].IndexOf(": ");
                if (sepIndex > 0)
                {
                    string line = lines[i].Substring(sepIndex + 1)
                        .Replace("\\ ", " ").Replace("\\:", ":")
                        .TrimEnd(md_trimEndChars);
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    resultList.Add(toAbsolutePath(line));
                }
            }

            string[] resList = new string[resultList.Count];
            resultList.CopyTo(resList);

            return resList;
        }

        static string[] parseRefFile(string fpath, string modeID)
        {
            string[] lines = File.ReadAllLines(fpath, RuntimeEncoding.instance().Default);

            switch (modeID)
            {
                case "AC5":
                    return ac5_parseRefLines(lines);
                case "IAR_STM8":
                case "IAR_ARM":
                    return ac5_parseRefLines(lines);
                case "SDCC":
                case "AC6":
                case "GCC":
                    return gnu_parseRefLines(lines);
                default:
                    return gnu_parseRefLines(lines);
            }
        }

        /// <summary>
        /// 做一些预处理，比如将路径转换到当前 OS 要求的格式
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        static void prepareModel(JObject model)
        {
            var globals = (JObject)model["global"];
            var groups  = (JObject)model["groups"];

            // 将路径转换到当前OS的格式
            foreach (var kv in groups)
            {
                var grp = (JObject)kv.Value;

                if (grp.ContainsKey("$path"))
                {
                    if (OsInfo.instance().OsType == "win32")
                        grp["$path"] = grp["$path"].Value<string>().Replace('/', '\\') + ".exe";
                }

                if (grp.ContainsKey("$outputBin"))
                {
                    foreach (JObject item in (JArray)grp["$outputBin"])
                    {
                        if (OsInfo.instance().OsType == "win32")
                            item["toolPath"] = item["toolPath"].Value<string>().Replace('/', '\\') + ".exe";

                        // 根据OS环境选择 commmand
                        if (OsInfo.instance().OsType == "win32" && item.ContainsKey("command.win32"))
                            item["command"] = item["command.win32"].Value<string>();
                        else if (OsInfo.instance().OsType != "win32" && item.ContainsKey("command.unix"))
                            item["command"] = item["command.unix"].Value<string>();
                    }
                }
                
                if (grp.ContainsKey("$extraCommand"))
                {
                    foreach (JObject item in (JArray)grp["$extraCommand"])
                    {
                        if (OsInfo.instance().OsType == "win32")
                            item["toolPath"] = item["toolPath"].Value<string>().Replace('/', '\\') + ".exe";

                        // 根据OS环境选择 commmand
                        if (OsInfo.instance().OsType == "win32" && item.ContainsKey("command.win32"))
                            item["command"] = item["command.win32"].Value<string>();
                        else if (OsInfo.instance().OsType != "win32" && item.ContainsKey("command.unix"))
                            item["command"] = item["command.unix"].Value<string>();
                    }
                }
            }

            // 将所有 global 的配置插入到相应的 group 中去
            foreach (var ele in globals)
            {
                if (!((JObject)ele.Value).ContainsKey("group"))
                    throw new Exception("not found 'group' in global option '" + ele.Key + "'");

                foreach (var category in (JArray)ele.Value["group"])
                {
                    if (groups.ContainsKey(category.Value<string>()))
                    {
                        if (((JObject)ele.Value).ContainsKey("location")
                            && ele.Value["location"].Value<string>() == "first")
                        {
                            ((JObject)groups[category.Value<string>()]).AddFirst(new JProperty(ele.Key, ele.Value));
                        }
                        else
                        {
                            ((JObject)groups[category.Value<string>()]).Add(new JProperty(ele.Key, ele.Value));
                        }
                    }
                }
            }
        }

        static void prepareParams(JObject _params)
        {
            if (compilerModel.ContainsKey("defines"))
            {
                foreach (var define in ((JArray)compilerModel["defines"]).Values<string>())
                {
                    ((JArray)_params["defines"]).Add(define);
                }
            }
        }

        static void prepareSourceFiles(string rootDir, IEnumerable<string> sourceList, 
            JObject srcParamsObj, HashSet<string> alwaysInBuild)
        {
            foreach (string repath in sourceList)
            {
                string sourcePath = Utility.isAbsolutePath(repath)
                    ? (repath)
                    : (rootDir + Path.DirectorySeparatorChar + repath);

                FileInfo file = new(sourcePath);

                if (file.Exists)
                {
                    if (libFileFilter.IsMatch(file.Name))
                    {
                        libList.Add(file.FullName);
                    }
                    else
                    {
                        srcList.Add(file.FullName);
                    }
                }

                // add independent command for file
                if (srcParamsObj != null && srcParamsObj.ContainsKey(repath))
                {
                    if (srcParams.ContainsKey(file.FullName))
                    {
                        srcParams.Remove(file.FullName);
                    }

                    srcParams.Add(file.FullName, srcParamsObj[repath].Value<string>());
                }

                if (alwaysInBuild.Contains(repath))
                {
                    alwaysInBuildSources.Add(file.FullName);
                }
            }
        }

        static string quotePath(string path)
        {
            if (path.Contains(' ') && !path.StartsWith('"'))
                return $"\"{path}\"";
            return path;
        }

        static string aliasMakefileCompiler(string compilerFullPath)
        {
            compilerFullPath = compilerFullPath.Trim('"');

            foreach (var item in makefileCompilers)
            {
                if (item.Value == compilerFullPath)
                    return $"$({item.Key})";
            }

            var res = Regex
                .Replace(compilerFullPath, @"\.exe$", "$(EXE)", RegexOptions.IgnoreCase)
                .Replace(toolchainRoot, "$(COMPILER_DIR)")
                .Replace(Utility.toUnixPath(toolchainRoot), "$(COMPILER_DIR)");

            return quotePath(res);
        }

        //////////////////////////////////////////////////
        ///             logger function
        //////////////////////////////////////////////////

        static void lockLogs()
        {
            if (cliArgs.OnlyDumpCompilerDB || cliArgs.OnlyPrintArgs)
            {
                if (logStream == null)
                {
                    string logPath = Path.GetTempPath() + "unify_builder.log";
                    logStream = new FileStream(logPath, FileMode.Append, FileAccess.Write);
                }

                if (compilerLogStream == null)
                {
                    string logPath = Path.GetTempPath() + "compiler.log";
                    compilerLogStream = new FileStream(logPath, FileMode.Create, FileAccess.Write);
                }
            }
            else
            {
                if (logStream == null)
                {
                    string logPath = dumpPath + Path.DirectorySeparatorChar + "unify_builder.log";
                    logStream = new FileStream(logPath, FileMode.Append, FileAccess.Write, FileShare.None);
                }

                if (compilerLogStream == null)
                {
                    string logPath = dumpPath + Path.DirectorySeparatorChar + "compiler.log";
                    compilerLogStream = new FileStream(logPath, FileMode.Create, FileAccess.Write, FileShare.None);
                }
            }
        }

        static void unlockLogs()
        {
            try
            {
                // compiler.log
                compilerLogStream.Flush();
                compilerLogStream.Close();

                // unify_builder.log must be at last to flush
                logStream.Flush();
                logStream.Close();
            }
            catch (Exception)
            {
                // nothing todo
            }
        }

        static void dumpCompilerLog()
        {
            try
            {
                if (compilerLogStream != null)
                {
                    // cc log
                    {
                        var txt = ">>> cc" + OsInfo.instance().CRLF + OsInfo.instance().CRLF;
                        txt += string.Join(OsInfo.instance().CRLF, compiler_log_cpp);
                        compilerLogStream.Write(RuntimeEncoding.instance().Default.GetBytes(txt));
                    }

                    compilerLogStream.Write(RuntimeEncoding.instance().Default.GetBytes(
                        OsInfo.instance().CRLF + OsInfo.instance().CRLF));

                    // link log
                    {
                        var txt = ">>> ld" + OsInfo.instance().CRLF + OsInfo.instance().CRLF;
                        txt += string.Join(OsInfo.instance().CRLF, compiler_log_lnk);
                        compilerLogStream.Write(RuntimeEncoding.instance().Default.GetBytes(txt));
                    }
                }
            }
            catch (Exception _err)
            {
                error("log dump failed !, " + _err.Message);
            }
        }

        static void appendLogs(string lable, string msg, string[] logs = null)
        {
            try
            {
                if (logStream != null)
                {
                    string txt = "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "]\t";

                    txt += lable + Environment.NewLine + msg + Environment.NewLine;

                    if (logs != null && logs.Length > 0)
                    {
                        txt += "---" + Environment.NewLine;
                        txt += String.Join(Environment.NewLine, logs);
                        txt += (Environment.NewLine + Environment.NewLine);
                    }
                    else
                    {
                        txt += Environment.NewLine;
                    }

                    logStream.Write(RuntimeEncoding.instance().Default.GetBytes(txt));
                }
            }
            catch (Exception _err)
            {
                error("log dump failed !, " + _err.Message);
            }
        }

        static void appendErrLogs(Exception err, string[] errLogs)
        {
            appendLogs(err.Message, err.StackTrace, errLogs);
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

        static void printColor(string txt, ConsoleColor color, bool newLine = true)
        {
            Console.ForegroundColor = color;
            if (newLine)
                Console.WriteLine(txt);
            else
                Console.Write(txt);
            Console.ResetColor();
        }

        static void infoWithLable(string txt, bool newLine = true, string label = "INFO")
        {
            Console.Write("[");
            Console.ForegroundColor = ConsoleColor.Blue;
            //Console.BackgroundColor = ConsoleColor.Black;
            Console.Write(" " + label + " ");
            Console.ResetColor();
            Console.Write("]");
            Console.Write(" " + (newLine ? (txt + "\r\n") : txt));
        }

        static void warnWithLable(string txt, bool newLine = true, string label = "WARNING")
        {
            Console.ForegroundColor = ConsoleColor.Black;
            Console.BackgroundColor = ConsoleColor.Yellow;
            Console.Write(" " + label + " ");
            Console.ResetColor();
            Console.Write(" " + (newLine ? (txt + "\r\n") : txt));
        }

        static void errorWithLable(string txt, bool newLine = true, string label = "ERROR")
        {
            Console.ForegroundColor = ConsoleColor.Black;
            Console.BackgroundColor = ConsoleColor.Red;
            Console.Write(" " + label + " ");
            Console.ResetColor();
            Console.Write(" " + (newLine ? (txt + "\r\n") : txt));
        }

        static void doneWithLable(string txt, bool newLine = true, string label = "DONE")
        {
            Console.Write("[");
            Console.ForegroundColor = ConsoleColor.Green;
            //Console.BackgroundColor = ConsoleColor.Black;
            Console.Write(" " + label + " ");
            Console.ResetColor();
            Console.Write("]");
            Console.Write(" " + (newLine ? (txt + "\r\n") : txt));
        }
    }
}
