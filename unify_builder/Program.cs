﻿using ConsoleTableExt;
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
using CommandLine;
using CommandLine.Text;

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
            public string commandLine;      // [required] executable file cli args
            public string sourcePath;       // [required] for compiler, value is '.c' path; for linker, value is output '.map' path
            public string outPath;          // [required] output file full path
            public Encoding outputEncoding; // [required] cli encoding, UTF8/GBK/...

            public string compilerId;       // [optional] [used in compile process] compiler id (lower case), like: 'gcc', 'sdcc'
            public string compilerType;     // [optional] [used in compile process] compiler type, like: 'c', 'asm', 'linker'

            public string title;            // [optional] a title for this command
            public string shellCommand;     // [optional] shell command which will be invoke compiler, used to gen 'compile_commands.json'
            public string[] outputs;        // [optional] if output more than one files, use this field
            public string argsForSplitter;  // [optional] compiler args for 'source_splitter' tool
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
            public string suffix = "";
            public string sep = " ";
            public bool noQuotes = false;
        };

        class InvokeFormat
        {
            public bool useFile = false;
            public string body = null;
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
        private readonly string binDir; // compiler root folder, like: 'c:\compiler_root', '/bin/gcc_root', '%COMPILER_ROOT%'
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

            var GetRealToolName = delegate (Dictionary<string, JObject> userParamsObj, string modName) {
                if (!userParamsObj[modName].ContainsKey("$use")) return modName;
                var name = userParamsObj[modName]["$use"].Value<string>();
                if (string.IsNullOrWhiteSpace(name) || name.StartsWith(modName + "-") || name == modName) return name;
                return modName + '-' + name;
            };

            // init compiler models
            string cCompilerName = ((JObject)cModel["groups"]).ContainsKey("c/cpp") ? "c/cpp" : "c";
            string cppCompilerName = ((JObject)cModel["groups"]).ContainsKey("c/cpp") ? "c/cpp" : "cpp";
            string linkerName = GetRealToolName(paramObj, "linker");

            if (!((JObject)cModel["groups"]).ContainsKey(cCompilerName))
                throw new Exception("Not found c compiler model");

            if (!((JObject)cModel["groups"]).ContainsKey(cppCompilerName))
                throw new Exception("Not found cpp compiler model");

            if (!((JObject)cModel["groups"]).ContainsKey(linkerName))
                throw new Exception("Invalid '$use' option!，please check compile option 'linker.$use'");

            models.Add("c", (JObject)cModel["groups"][cCompilerName]);
            models.Add("cpp", (JObject)cModel["groups"][cppCompilerName]);
            models.Add("linker", (JObject)cModel["groups"][linkerName]);

            // init asm compiler models and params
            asmCompilerName = GetRealToolName(paramObj, "asm");

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
                    throw new Exception("Invalid '$use' option!，please check compile option 'asm-compiler.$use'");

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
                        if (this.getCompilerId() == "KEIL_C51") eCode = Program.CODE_DONE;

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
                        compilerAttr_sdcc_module_split = paramObj["global"]["$one-module-per-function"].Value<bool>();
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

                // invoke mode
                InvokeFormat invokeFormat = modelParams["$invoke"].ToObject<InvokeFormat>();
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

        public CmdInfo genLinkCommand(List<string> objList, bool cliTestMode = false)
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

            string outFileName = getOutName();

            string compilerId = getCompilerId();

            //--

            // For SDCC, bundled *.rel files as a *.lib file
            // ref: https://sourceforge.net/p/sdcc/discussion/1865/thread/e395ff7a42/#a03e
            // cmd: sdar -rcv ${out} ${in}
            if (!cliTestMode && compilerId == "SDCC" && checkEntryOrderForSdcc)
            {
                List<string> finalObjsLi = new(128); // must be absolute path
                List<string> bundledList = new(128); // must be relative path

                // make entry src file at the first of cli args
                {
                    string mainName = linkerParams.ContainsKey("$mainFileName")
                        ? linkerParams["$mainFileName"].Value<string>() : "main";

                    int index = objList.FindIndex((string fName) => {
                        return Path.GetFileNameWithoutExtension(fName).Equals(mainName);
                    });

                    if (index != -1)
                    {
                        finalObjsLi.Add(objList[index]);
                        objList.RemoveAt(index);
                    }
                    else
                    {
                        throw new Exception("Not found '"
                            + mainName + ".rel' object file in output list, the '"
                            + mainName + ".rel' object file must be the first object file !");
                    }
                }

                // split objs
                foreach (string objPath in objList)
                {
                    if (objPath.EndsWith(".lib") || objPath.EndsWith(".a"))
                        finalObjsLi.Add(objPath);
                    else
                        bundledList.Add(toRelativePathForCompilerArgs(objPath));
                }

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
                int exitCode = Program.runExe(getToolFullPathByModel("linker-lib"), cliStr, out string log);
                if (exitCode != Program.CODE_DONE)
                    throw new Exception("bundled lib file failed, exit code: " + exitCode + ", msg: " + log);

                // append to linker obj list
                finalObjsLi.Add(bundledFullOutPath);

                // set real obj list
                objList = finalObjsLi;
            }

            //--

            string outName = outDir + Path.DirectorySeparatorChar + outFileName;
            string outPath = outName + outSuffix;
            string mapPath = outName + mapSuffix;
            string stableCommand = string.Join(" ", baseOpts["linker"].Concat(userOpts["linker"]));
            string cmdLine = compilerAttr_commandPrefix;

            if (cmdLocation == "start")
            {
                cmdLine += stableCommand;

                if (linkerModel.ContainsKey("$linkMap"))
                {
                    cmdLine += sep + getCommandValue((JObject)linkerModel["$linkMap"], "")
                        .Replace("${mapPath}", toRelativePathForCompilerArgs(mapPath));
                }
            }

            for (int i = 0; i < objList.Count; i++)
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

            // repleace eide cmd vars
            string reOutDir = toRelativePathForCompilerArgs(outDir, false, false);
            cmdLine = cmdLine
                .Replace("${OutName}", reOutDir + compilerAttr_directorySeparator + formatPathForCompilerArgs(outFileName))
                .Replace("${OutDir}", reOutDir);

            // replace system env
            cmdLine = Program.replaceEnvVariable(cmdLine);

            //--

            string commandLine = null;

            if (iFormat.useFile && !cliTestMode)
            {
                FileInfo paramFile = new FileInfo(outName + ".lnp");
                File.WriteAllText(paramFile.FullName, cmdLine, encodings["linker"]);
                commandLine = iFormat.body.Replace("${value}", "\"" + paramFile.FullName + "\"");
            }
            else
            {
                commandLine = cmdLine;
            }

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

            // gen .map.view for eide
            try
            {
                string[] cont = new string[] {
                    "tool: " + toolId,
                    "fileName: " + Path.GetFileName(mapPath)
                };

                File.WriteAllLines(outName + ".map.view", cont, RuntimeEncoding.instance().UTF8);
            }
            catch (Exception)
            {
                // do nothing
            }

            return new CmdInfo {
                compilerId = compilerId.ToLower(),
                compilerType = "linker",
                exePath = getToolFullPathById("linker"),
                commandLine = commandLine,
                sourcePath = mapPath,
                outPath = outPath,
                outputEncoding = encodings["linker"]
            };
        }

        public CmdInfo[] genOutputCommand(string linkerOutputFile)
        {
            JObject linkerModel = models["linker"];
            List<CmdInfo> commandsList = new();

            // not need output hex/bin
            if (!linkerModel.ContainsKey("$outputBin"))
                return commandsList.ToArray();

            string outFileName = outDir + Path.DirectorySeparatorChar + getOutName();

            foreach (JObject outputModel in (JArray)linkerModel["$outputBin"])
            {
                string outFilePath = outFileName;

                if (outputModel.ContainsKey("outputSuffix"))
                {
                    outFilePath += outputModel["outputSuffix"].Value<string>();
                }

                string command = outputModel["command"].Value<string>()
                    .Replace("${linkerOutput}", toRelativePathForCompilerArgs(linkerOutputFile))
                    .Replace("${output}", toRelativePathForCompilerArgs(outFilePath));

                // replace system env
                command = Program.replaceEnvVariable(command);

                commandsList.Add(new CmdInfo {
                    title = outputModel["name"].Value<string>(),
                    exePath = toAbsToolPath(outputModel["toolPath"].Value<string>()),
                    commandLine = command,
                    sourcePath = linkerOutputFile,
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
                command = Program.replaceEnvVariable(command);

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

        private string toAbsToolPath(string rawToolPath)
        {
            return binDir +
                rawToolPath.Replace("${toolPrefix}", toolPrefix);
        }

        public string getToolFullPathById(string id)
        {
            return binDir + getActivedRawToolPath(id);
        }

        public string getActivedRawToolPath(string name)
        {
            return models[name]["$path"].Value<string>();
        }

        public string getToolFullPathByModel(string name)
        {
            return toAbsToolPath(model["groups"][name]["$path"].Value<string>());
        }

        public string getModelName()
        {
            return model.ContainsKey("name") ? model["name"].Value<string>() : "null";
        }

        private JObject getToolchainVersionMatcher()
        {
            return model.ContainsKey("version") ? (JObject)model["version"] : null;
        }

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

        //------------

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

        private readonly Regex compilerOpts_overrideExprMatcher = new(@"\$<override:(.+)>", RegexOptions.Compiled);
        private readonly Regex compilerOpts_replaceExprMatcher = new(@"\$<replace:(?<old>.+?)/(?<new>.+?)>", RegexOptions.Compiled);

        private CmdInfo fromModel(string modelName, string langName, string fpath, bool onlyCmd = false)
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

            string outFileName = null;

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
                        Directory.CreateDirectory(outDir + Path.DirectorySeparatorChar + fDir);
                        outFileName = fDir + Path.DirectorySeparatorChar + srcName;
                    }
                    else // no parent dir
                    {
                        outFileName = srcName;
                    }
                }

                // if we can't calcu repath, gen complete path to out folder
                else
                {
                    string fmtSrcDir = Utility.toUnixPath(srcDir).Trim('/');
                    // convert 'c:\xxx\a.c' -> '<build_out_dir>/c/xxx/a.??'
                    Regex drvReplacer = new Regex(@"^(?<drv>[a-z]):/", RegexOptions.IgnoreCase);
                    string fDir = Utility.toLocalPath(drvReplacer.Replace(fmtSrcDir, "${drv}/"));
                    Directory.CreateDirectory(outDir + Path.DirectorySeparatorChar + fDir);
                    outFileName = fDir + Path.DirectorySeparatorChar + srcName;
                }
            }

            // generate to output root directly
            else
            {
                outFileName = srcName;
            }

            string outName = getUniqueName(outDir + Path.DirectorySeparatorChar + outFileName);
            string outPath = outName + outputSuffix;
            string refPath = outName + ".d"; // --depend ${refPath} 
            string listPath = outName + ".lst";
            string langOption = null;

            List<string> commands = new(256);
            List<string> compiler_cmds = new(baseOpts[modelName]);

            if (langName != null && cModel.ContainsKey("$" + langName))
            {
                langOption = cParams.ContainsKey(langName) ? cParams[langName].Value<string>() : "default";
                commands.Add(getCommandValue((JObject)cModel["$" + langName], langOption));
            }

            if (cModel.ContainsKey("$listPath"))
            {
                commands.Add(getCommandValue((JObject)cModel["$listPath"], "")
                    .Replace("${listPath}", toRelativePathForCompilerArgs(listPath, isQuote)));
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
                    var mRes = compilerOpts_overrideExprMatcher.Match(srcOpts);

                    if (mRes.Success && mRes.Groups.Count > 1)
                    {
                        srcOpts = mRes.Groups[1].Value.Trim();
                    }
                    else
                    {
                        compiler_cmds.AddRange(userOpts[modelName]);
                    }
                }

                // replace expr:
                //      replace some matched options to other
                {
                    var mRes = compilerOpts_replaceExprMatcher.Match(srcOpts);
                    if (mRes.Success && mRes.Groups.Count > 2)
                    {
                        var oldVal = mRes.Groups["old"].Value.Trim();
                        var newVal = mRes.Groups["new"].Value.Trim();

                        // del expr-self
                        srcOpts = srcOpts.Remove(mRes.Groups[0].Index, mRes.Groups[0].Length);

                        if (!string.IsNullOrEmpty(oldVal))
                        {
                            for (int i = 0; i < compiler_cmds.Count; i++)
                            {
                                compiler_cmds[i] = compiler_cmds[i].Replace(oldVal, newVal);
                            }
                        }
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

            // replace variables
            for (int i = 0; i < compiler_cmds.Count; i++)
            {
                compiler_cmds[i] = compiler_cmds[i]
                    .Replace("${outName}", outName);
            }

            // join commands
            if (onlyCmd == false) /* generate full command */
            {
                string outputFormat = cModel["$output"].Value<string>();

                if (outputFormat.Contains("${in}"))
                {
                    commands.AddRange(compiler_cmds);

                    if (!isSplitterEn)
                    {
                        outputFormat = outputFormat
                            .Replace("${out}", toRelativePathForCompilerArgs(outPath, isQuote))
                            .Replace("${in}", toRelativePathForCompilerArgs(fpath, isQuote));
                    }

                    commands.Add(outputFormat
                        .Replace("${refPath}", toRelativePathForCompilerArgs(refPath, isQuote)));
                }
                else /* compate KEIL_C51 */
                {
                    commands.Insert(0, toRelativePathForCompilerArgs(fpath));
                    commands.AddRange(compiler_cmds);

                    if (!isSplitterEn)
                    {
                        outputFormat = outputFormat
                            .Replace("${out}", toRelativePathForCompilerArgs(outPath, isQuote));
                    }

                    commands.Add(outputFormat
                        .Replace("${refPath}", toRelativePathForCompilerArgs(refPath, isQuote)));
                }
            }
            else /* only retain compiler flags */
            {
                commands.AddRange(compiler_cmds);
            }

            // delete whitespace
            commands.RemoveAll(delegate (string _command) { return string.IsNullOrEmpty(_command); });

            // repleace eide cmd vars and system envs
            {
                string reOutDir = toRelativePathForCompilerArgs(outDir, false, false);
                string reSrcDir = toRelativePathForCompilerArgs(srcDir, false, false);

                for (int i = 0; i < commands.Count; i++)
                {
                    commands[i] = commands[i]
                        .Replace("${OutName}", reOutDir + compilerAttr_directorySeparator + formatPathForCompilerArgs(outFileName))
                        .Replace("${OutDir}", reOutDir)
                        .Replace("${FileName}", reSrcDir + compilerAttr_directorySeparator + srcName)
                        .Replace("${FileDir}", reSrcDir);

                    commands[i] = Program.replaceEnvVariable(commands[i]);
                }
            }

            string commandLines = compilerAttr_commandPrefix + string.Join(" ", commands.ToArray());
            string compilerArgs = commandLines;
            string exeFullPath = getToolFullPathById(modelName);

            // save raw compiler args
            FileInfo paramFile = new(outName + paramsSuffix);
            if (onlyCmd == false) File.WriteAllText(paramFile.FullName, commandLines, encodings[modelName]);

            // if compiler support read args from file
            // we need to regen compiler args
            if (iFormat.useFile && onlyCmd == false)
            {
                commandLines = iFormat.body.Replace("${value}", "\"" + paramFile.FullName + "\"");
            }

            var buildArgs = new CmdInfo {
                compilerId = getCompilerId().ToLower(),
                compilerType = modelName,
                exePath = exeFullPath,
                commandLine = commandLines,
                sourcePath = fpath,
                outPath = outPath,
                outputEncoding = encodings[modelName]
            };

            // create cli args for 'compile_commands.json'
            {
                buildArgs.shellCommand = "\"" + Program.replaceEnvVariable(exeFullPath) + "\" " + compilerArgs
                    .Replace("${out}", toRelativePathForCompilerArgs(Path.ChangeExtension(outPath, ".obj"), isQuote))
                    .Replace("${in}", toRelativePathForCompilerArgs(fpath, isQuote));
            }

            if (isSplitterEn)
            {
                buildArgs.argsForSplitter = compilerArgs;
            }

            return buildArgs;
        }

        private string formatPathForCompilerArgs(string path)
        {
            return Utility.toLocalPath(path, compilerAttr_directorySeparator);
        }

        private string toRelativePathForCompilerArgs(string path, bool quote = true, bool addDotPrefix = true)
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
                    if (string.IsNullOrWhiteSpace(macro)) continue;

                    string macroStr;

                    if (modelName == "asm")
                    {
                        value = "1";
                        macroStr = defFormat.body
                            .Replace("${key}", macro)
                            .Replace("${value}", value);
                    }
                    else // delete macro fmt str suffix
                    {
                        macroStr = Regex
                            .Replace(defFormat.body, @"(?<macro_key>^[^\$]*\$\{key\}).*$", "${macro_key}")
                            .Replace("${key}", macro);
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
        static readonly Regex libFileFilter = new Regex(@"\.(?:lib|a|o|obj)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
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

        static readonly HashSet<string> cList = new(512);
        static readonly HashSet<string> cppList = new(512);
        static readonly HashSet<string> asmList = new(512);
        static readonly HashSet<string> libList = new(512);

        // compiler params for single source file, Map<absPath, params>
        static readonly Dictionary<string, string> srcParams = new(512);
        static Int64 paramsMtime = 0; // source file params modify time

        static readonly string appBaseDir = AppDomain.CurrentDomain.SetupInformation.ApplicationBase.TrimEnd(Path.DirectorySeparatorChar);
        static readonly Dictionary<string, string> curEnvs = new(64);

        // Used to determine whether the received
        // return code is an error code
        static int ERR_LEVEL = CODE_DONE;

        static int ram_max_size = -1;
        static int rom_max_size = -1;

        static string dumpPath;
        static string binDir;
        static int reqThreadsNum;
        static JObject compilerModel;
        static JObject paramsObj;
        static string outDir;
        static string projectRoot;
        static string builderDir;
        static string paramsFilePath;
        static string refJsonName;

        static FileStream logStream = null;

        static bool enableNormalOut = true;
        static bool showRelativePathOnLog = false;
        static bool colorRendererEnabled = true;

        static HashSet<BuilderMode> modeList = new HashSet<BuilderMode>();

        enum BuilderMode
        {
            NORMAL = 0,
            FAST,
            DEBUG,
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

            // init all params
            try
            {
                /* start commands runner ? */
                if (!string.IsNullOrEmpty(cliArgs.RunCommandsJsonPath))
                {
                    try
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
                                ignoreFailed = item.ContainsKey("ignoreFailed") ? item["ignoreFailed"].Value<bool>() : false,
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
                    catch (Exception err)
                    {
                        errorWithLable("runner aborted, msg: " + err.Message);
                        return CODE_ERR;
                    }
                }

                /* load params */
                try
                {
                    paramsFilePath = cliArgs.ParamsFilePath;

                    // load params file
                    string paramsJson = File.ReadAllText(paramsFilePath, RuntimeEncoding.instance().UTF8);
                    if (String.IsNullOrWhiteSpace(paramsJson)) throw new ArgumentException("file '" + paramsFilePath + "' is empty !");
                    paramsObj = JObject.Parse(paramsJson);

                    // load core params
                    binDir = paramsObj["toolchainLocation"].Value<string>();
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
                builderDir = Path.GetDirectoryName(appBaseDir);

                // get real path
                dumpPath = Utility.isAbsolutePath(dumpPath) ? dumpPath : (projectRoot + Path.DirectorySeparatorChar + dumpPath);
                outDir = Utility.isAbsolutePath(outDir) ? outDir : (projectRoot + Path.DirectorySeparatorChar + outDir);

                // prepare source
                paramsMtime = paramsObj.ContainsKey("sourceParamsMtime") ? paramsObj["sourceParamsMtime"].Value<Int64>() : 0;
                JObject srcParamsObj = paramsObj.ContainsKey("sourceParams") ? (JObject)paramsObj["sourceParams"] : null;
                addToSourceList(projectRoot, paramsObj["sourceList"].Values<string>(), srcParamsObj);

                // other params
                modeList.Add(BuilderMode.NORMAL);
                reqThreadsNum = paramsObj.ContainsKey("threadNum") ? paramsObj["threadNum"].Value<int>() : 0;
                ram_max_size = paramsObj.ContainsKey("ram") ? paramsObj["ram"].Value<int>() : -1;
                rom_max_size = paramsObj.ContainsKey("rom") ? paramsObj["rom"].Value<int>() : -1;
                showRelativePathOnLog = paramsObj.ContainsKey("showRepathOnLog") ? paramsObj["showRepathOnLog"].Value<bool>() : false;
                refJsonName = paramsObj.ContainsKey("sourceMapName") ? paramsObj["sourceMapName"].Value<string>() : "ref.json";

                // init other params
                ERR_LEVEL = compilerModel.ContainsKey("ERR_LEVEL") ? compilerModel["ERR_LEVEL"].Value<int>() : ERR_LEVEL;
                prepareModel();
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
                            var mod = (BuilderMode)Enum.Parse(typeof(BuilderMode), modeStr.ToUpper());
                            if (cliArgs.ForceRebuild && mod == BuilderMode.FAST) continue;
                            modeList.Add(mod);
                        }
                        catch (Exception)
                        {
                            warn("\r\nInvalid mode option '" + modeStr + "', ignore it !");
                        }
                    }
                }
            }
            catch (Exception err)
            {
                errorWithLable("Init build failed !\r\n" + err.ToString());
                return CODE_ERR;
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

            // task envs
            Dictionary<string, string> taskEnvs = new(32); // format: <key: 'ENV_NAME', val: 'ENV_VALxxxx'>

            // compiler errlog list
            List<string> errLogs = new(512);

            // compiler prefix
            string COMPILER_CMD_PREFIX = "";

            // record build start time
            DateTime time = DateTime.Now;

            try
            {
                Directory.CreateDirectory(outDir);

                //
                setEnvValue("EIDE_CUR_OS_TYPE", OsInfo.instance().OsType);

                // add appBase folder to system env
                setEnvValue("PATH", appBaseDir);

                // add builder root folder to system env
                setEnvValue("PATH", builderDir +
                    Path.DirectorySeparatorChar + "msys" +
                    Path.DirectorySeparatorChar + "bin");

                // add env from bulder.params
                if (paramsObj.ContainsKey("env"))
                {
                    JObject envs = (JObject)paramsObj["env"];

                    foreach (JProperty field in envs.Properties())
                    {
                        string envName = field.Name.ToString().Trim();
                        string envValue = field.Value.ToString().Trim();

                        if (!Regex.IsMatch(envName, @"^[\w\$]+$"))
                        {
                            warn(string.Format("ignore incorrect env name: '{0}'", envName));
                            continue;
                        }

                        // set to shell env
                        setEnvValue(envName, envValue);

                        // set to task env
                        taskEnvs.Add("${" + envName + "}", envValue);

                        // set cmd prefix
                        if (envName == "COMPILER_CMD_PREFIX" && !string.IsNullOrWhiteSpace(envValue))
                        {
                            COMPILER_CMD_PREFIX = envValue + " ";
                        }
                    }
                }

                // create command generator
                CmdGenerator cmdGen = new(compilerModel, paramsObj, new CmdGenerator.GeneratorOption {
                    bindirEnvName = "%TOOL_DIR%",
                    bindirAbsPath = binDir,
                    outpath = outDir,
                    cwd = projectRoot,
                    testMode = checkMode(BuilderMode.DEBUG),
                    compiler_prefix = COMPILER_CMD_PREFIX,
                    srcParams = srcParams,
                    outDirTree = true
                });

                // ingnore keil c51 normal output
                enableNormalOut = cmdGen.getCompilerId() != "KEIL_C51";

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
                                ccOutputRender.Add(new Regex(@"((?:\swarning\s|^warning\s)(?:[A-Z][0-9]+)(?::\s.+)?)",
                                    RegexOptions.IgnoreCase | RegexOptions.Compiled), WARN_RENDER);
                                ccOutputRender.Add(new Regex(@"((?:\serror\s|^error\s)(?:[A-Z][0-9]+)(?::\s.+)?)",
                                    RegexOptions.IgnoreCase | RegexOptions.Compiled), ERRO_RENDER);

                                /* linker */
                                lkOutputRender.Add(new Regex(@"((?:\swarning\s|^warning\s)(?:[A-Z][0-9]+)(?::\s.+)?)",
                                    RegexOptions.IgnoreCase | RegexOptions.Compiled), WARN_RENDER);
                                lkOutputRender.Add(new Regex(@"((?:\serror\s|^error\s)(?:[A-Z][0-9]+)(?::\s.+)?)",
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
                            {
                                /* compiler */
                                ccOutputRender.Add(new Regex(@"(\swarning\[\w+\]:\s|^warning\[\w+\]:\s)",
                                    RegexOptions.IgnoreCase | RegexOptions.Compiled), WARN_RENDER);
                                ccOutputRender.Add(new Regex(@"(\serror\[\w+\]:\s|^error\[\w+\]:\s)",
                                    RegexOptions.IgnoreCase | RegexOptions.Compiled), ERRO_RENDER);
                                // cc hint msg
                                ccOutputRender.Add(new Regex(@"^([\^~\s]*\^[\^~\s]*)$",
                                    RegexOptions.IgnoreCase | RegexOptions.Compiled), HINT_RENDER);
                                ccOutputRender.Add(new Regex(@"^([~\s]*~[~\s]*)$",
                                    RegexOptions.IgnoreCase | RegexOptions.Compiled), NOTE_RENDER);
                                ccOutputRender.Add(new Regex(@"^(\s*\|.+)",
                                    RegexOptions.IgnoreCase | RegexOptions.Compiled), HINT_RENDER);

                                /* linker */
                                lkOutputRender.Add(new Regex(@"(\swarning\[\w+\]:\s|^warning\[\w+\]:\s)",
                                    RegexOptions.IgnoreCase | RegexOptions.Compiled), WARN_RENDER);
                                lkOutputRender.Add(new Regex(@"(\serror\[\w+\]:\s|^error\[\w+\]:\s)",
                                    RegexOptions.IgnoreCase | RegexOptions.Compiled), ERRO_RENDER);
                            }
                            break;
                        /* other modern compilers */
                        default:
                            {
                                /* common */
                                {
                                    ccOutputRender.Add(new Regex(@"(\swarning:\s|^warning:\s)",
                                        RegexOptions.IgnoreCase | RegexOptions.Compiled), WARN_RENDER);
                                    ccOutputRender.Add(new Regex(@"(\serror:\s|^error:\s)",
                                        RegexOptions.IgnoreCase | RegexOptions.Compiled), ERRO_RENDER);
                                    ccOutputRender.Add(new Regex(@"(\snote:\s|^note:\s)",
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
                                    lkOutputRender.Add(new Regex(@"(\swarning:\s|^warning:\s)",
                                        RegexOptions.IgnoreCase | RegexOptions.Compiled), WARN_RENDER);
                                    lkOutputRender.Add(new Regex(@"(\serror:\s|^error:\s)",
                                        RegexOptions.IgnoreCase | RegexOptions.Compiled), ERRO_RENDER);
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

                // export compiler bin folder to PATH
                string ccFolder = Path.GetDirectoryName(binDir + Path.DirectorySeparatorChar + cmdGen.getActivedRawToolPath("c"));
                setEnvValue("PATH", ccFolder);

                // export compiler info to env
                setEnvValue("EIDE_CUR_COMPILER_ID", cmdGen.getCompilerId().ToLower());
                setEnvValue("EIDE_CUR_COMPILER_NAME", cmdGen.compilerName);
                setEnvValue("EIDE_CUR_COMPILER_NAME_FULL", cmdGen.compilerFullName);
                setEnvValue("EIDE_CUR_COMPILER_VERSION", cmdGen.compilerVersion);

                // preset env vars for tasks
                {
                    taskEnvs.Add("${TargetName}", cmdGen.getOutName());
                    taskEnvs.Add("${ConfigName}", cmdGen.getBuildConfigName());
                    taskEnvs.Add("${ProjectRoot}", projectRoot);
                    taskEnvs.Add("${BuilderFolder}", builderDir);
                    taskEnvs.Add("${OutDir}", outDir);

                    taskEnvs.Add("${ToolchainRoot}", binDir);
                    taskEnvs.Add("${CompilerPrefix}", cmdGen.getToolPrefix());
                    taskEnvs.Add("${CompilerFolder}", ccFolder);
                    taskEnvs.Add("${CompilerId}", cmdGen.getCompilerId().ToLower());
                    taskEnvs.Add("${CompilerName}", cmdGen.compilerName);
                    taskEnvs.Add("${CompilerFullName}", cmdGen.compilerFullName);
                    taskEnvs.Add("${CompilerVersion}", cmdGen.compilerVersion);

                    taskEnvs.Add("${re:ProjectRoot}", ".");
                    taskEnvs.Add("${re:BuilderFolder}", Utility.toRelativePath(projectRoot, builderDir) ?? builderDir);
                    taskEnvs.Add("${re:OutDir}", Utility.toRelativePath(projectRoot, outDir) ?? outDir);
                    taskEnvs.Add("${re:ToolchainRoot}", Utility.toRelativePath(projectRoot, binDir) ?? binDir);
                    taskEnvs.Add("${re:CompilerFolder}", Utility.toRelativePath(projectRoot, ccFolder) ?? ccFolder);
                }

                if (checkMode(BuilderMode.DEBUG))
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

                    cmdInf = cmdGen.genLinkCommand(new List<string> { "${obj1}", "${obj2}" }, true);
                    warn("\r\nLinker command line (" + Path.GetFileNameWithoutExtension(cmdInf.exePath) + "): \r\n");
                    log(cmdInf.commandLine);

                    warn("\r\nOuput file command line: \r\n");
                    CmdGenerator.CmdInfo[] cmdInfoList = cmdGen.genOutputCommand(cmdInf.outPath);
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

                int cCount = 0, asmCount = 0, cppCount = 0;

                // Check toolchain root folder
                if (!Directory.Exists(binDir))
                {
                    throw new Exception("Not found toolchain directory !, [path] : \"" + binDir + "\"");
                }

                // set toolchain root env
                try
                {
                    setEnvValue("TOOL_DIR", binDir);
                }
                catch (Exception e)
                {
                    throw new Exception("Set Environment Failed !, [path] : \"" + binDir + "\"", e);
                }

                // check compiler 
                {
                    if (cList.Count > 0)
                    {
                        string absPath = replaceEnvVariable(cmdGen.getToolFullPathById("c"));

                        if (!File.Exists(absPath))
                        {
                            throw new Exception("Not found 'C Compiler' !, [path]: \"" + absPath + "\"");
                        }
                    }

                    if (cppList.Count > 0)
                    {
                        string absPath = replaceEnvVariable(cmdGen.getToolFullPathById("cpp"));

                        if (!File.Exists(absPath))
                        {
                            throw new Exception("Not found 'C++ Compiler' !, [path]: \"" + absPath + "\"");
                        }
                    }

                    if (asmList.Count > 0)
                    {
                        string absPath = replaceEnvVariable(cmdGen.getToolFullPathById("asm"));

                        if (!File.Exists(absPath))
                        {
                            throw new Exception("Not found 'Assembler' !, [path]: \"" + absPath + "\"");
                        }
                    }

                    {
                        string absPath = replaceEnvVariable(cmdGen.getToolFullPathById("linker"));

                        if (!File.Exists(absPath))
                        {
                            throw new Exception("Not found 'Linker' !, [path]: \"" + absPath + "\"");
                        }
                    }
                }

                // switch to project root directory
                switchWorkDir(projectRoot);

                // run tasks before build
                if (runTasks("RUN TASKS BEFORE BUILD", "beforeBuildTasks", taskEnvs) != CODE_DONE)
                {
                    throw new Exception("Run Tasks Failed !, Stop Build !");
                }

                // reset work directory
                resetWorkDir();

                // prepare build
                infoWithLable("", false);
                info("start building at " + time.ToString("yyyy-MM-dd HH:mm:ss") + "\r\n");

                // print toolchain name and version
                infoWithLable(cmdGen.compilerFullName + "\r\n", true, "TOOL");

                // some compiler database informations
                Dictionary<string, string> sourceRefs = new();
                List<CompileCommandsDataBaseItem> compilerArgsDataBase = new(256);
                Dictionary<string, List<string>> linkerObjs = new(256);

                var PushLinkerObjs = delegate (CmdGenerator.CmdInfo ccArgs) {

                    // it's a normal obj
                    if (string.IsNullOrEmpty(ccArgs.argsForSplitter))
                    {
                        linkerObjs.Add(ccArgs.sourcePath, new() { ccArgs.outPath });
                    }

                    // it's a splitted obj, parse from file
                    else if (File.Exists(ccArgs.outPath))
                    {
                        var objLi = parseSourceSplitterOutput(File.ReadLines(ccArgs.outPath))
                            .Select(path => {
                                return Utility.isAbsolutePath(path) ? path : (projectRoot + Path.DirectorySeparatorChar + path);
                            }).ToList();

                        linkerObjs.Add(ccArgs.sourcePath, objLi);
                    }
                };

                foreach (var cFile in cList)
                {
                    CmdGenerator.CmdInfo cmdInf = cmdGen.fromCFile(cFile);
                    commands.Add(cmdInf.sourcePath, cmdInf);
                    PushLinkerObjs(cmdInf);
                    cCount++;

                    sourceRefs.Add(cmdInf.sourcePath, cmdInf.outPath);
                    compilerArgsDataBase.Add(new CompileCommandsDataBaseItem {
                        file = cmdInf.sourcePath,
                        directory = projectRoot,
                        command = cmdInf.shellCommand
                    });
                }

                foreach (var asmFile in asmList)
                {
                    CmdGenerator.CmdInfo cmdInf = cmdGen.fromAsmFile(asmFile);
                    commands.Add(cmdInf.sourcePath, cmdInf);
                    PushLinkerObjs(cmdInf);
                    asmCount++;

                    sourceRefs.Add(cmdInf.sourcePath, cmdInf.outPath);
                    compilerArgsDataBase.Add(new CompileCommandsDataBaseItem {
                        file = cmdInf.sourcePath,
                        directory = projectRoot,
                        command = cmdInf.shellCommand
                    });
                }

                foreach (var cppFile in cppList)
                {
                    CmdGenerator.CmdInfo cmdInf = cmdGen.fromCppFile(cppFile);
                    commands.Add(cmdInf.sourcePath, cmdInf);
                    PushLinkerObjs(cmdInf);
                    cppCount++;

                    sourceRefs.Add(cmdInf.sourcePath, cmdInf.outPath);
                    compilerArgsDataBase.Add(new CompileCommandsDataBaseItem {
                        file = cmdInf.sourcePath,
                        directory = projectRoot,
                        command = cmdInf.shellCommand
                    });
                }

                // check source file count
                if (cCount + cppCount + asmCount == 0)
                {
                    throw new Exception("Not found any source files !, please add some source files !");
                }

                // save compiler database informations
                try
                {
                    string refFilePath = outDir + Path.DirectorySeparatorChar + refJsonName;
                    File.WriteAllText(refFilePath, JsonConvert.SerializeObject(sourceRefs), RuntimeEncoding.instance().UTF8);

                    string compilerDbPath = outDir + Path.DirectorySeparatorChar + "compile_commands.json";
                    CompileCommandsDataBaseItem[] iLi = compilerArgsDataBase.ToArray();
                    File.WriteAllText(compilerDbPath, JsonConvert.SerializeObject(iLi), RuntimeEncoding.instance().UTF8);
                }
                catch (Exception)
                {
                    // do nothings
                }

                /* use incremental mode */
                if (checkMode(BuilderMode.FAST))
                {
                    CheckDiffRes res = checkDiff(cmdGen.getCompilerId(), commands);
                    cCount = res.cCount;
                    asmCount = res.asmCount;
                    cppCount = res.cppCount;
                    commands = res.totalCmds;
                    infoWithLable("file statistics (incremental compilation mode)\r\n");
                }

                /* rebuild mode */
                else
                {
                    infoWithLable("file statistics (rebuild mode)\r\n");
                }

                int totalFilesCount = (cCount + cppCount + asmCount + libList.Count);

                string tString = ConsoleTableBuilder
                    .From(new List<List<object>> { new List<object> { cCount, cppCount, asmCount, libList.Count, totalFilesCount } })
                    .WithFormat(ConsoleTableBuilderFormat.Alternative)
                    .WithColumn(new List<string> { "C Files", "Cpp Files", "Asm Files", "Lib Files", "Totals" })
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
                    info("start compilation ...");
                    if (commands.Count > 0) log("");

                    int total = commands.Count;
                    int curCnt = 0;

                    foreach (var cmdInfo in commands.Values)
                    {
                        curCnt++;

                        string compilerTag = cmdInfo.compilerType == "asm" ? "AS" : "CC";
                        string progressTag = genProgressTag(curCnt, total);

                        log(">> " + progressTag + " " + compilerTag + " '" + toHumanReadablePath(cmdInfo.sourcePath) + "'");

                        int exitCode;
                        string ccLog;

                        if (string.IsNullOrEmpty(cmdInfo.argsForSplitter)) // normal compile
                        {
                            exitCode = runExe(cmdInfo.exePath, cmdInfo.commandLine, out string ccOut, cmdInfo.outputEncoding);
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
                                out string resOut, out string ccOut, cmdInfo.outputEncoding);
                            ccLog = ccOut.Trim();

                            // parse and set obj list
                            cmdInfo.outputs = parseSourceSplitterOutput(resOut);
                        }

                        // ignore normal output
                        if (enableNormalOut || exitCode != CODE_DONE)
                        {
                            printCompileOutput(ccLog);
                        }

                        if (exitCode > ERR_LEVEL)
                        {
                            errLogs.Add(ccLog);
                            throw new Exception("compilation failed at : \"" + cmdInfo.sourcePath + "\", exit code: " + exitCode.ToString());
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

                // dump old params file after compilation done 
                // because link operation will always execute, but compilaion not
                try
                {
                    string oldParmasPath = paramsFilePath + ".old";
                    File.WriteAllText(oldParmasPath, File.ReadAllText(paramsFilePath));
                }
                catch (Exception)
                {
                    // do nothing
                }

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

                var allObjs = linkerObjs.SelectMany(kv => kv.Value, (kv, item) => item).ToList();
                CmdGenerator.CmdInfo linkInfo = cmdGen.genLinkCommand(allObjs);

                int linkerExitCode = runExe(linkInfo.exePath, linkInfo.commandLine, out string linkerOut, linkInfo.outputEncoding);

                if (!string.IsNullOrEmpty(linkerOut.Trim()))
                {
                    log(""); // newline
                    printCompileOutput(linkerOut, true);
                }

                if (linkerExitCode > ERR_LEVEL)
                {
                    errLogs.Add(linkerOut);
                    throw new Exception("link failed !, exit code: " + linkerExitCode.ToString());
                }

                // execute extra command
                foreach (CmdGenerator.LinkerExCmdInfo extraLinkerCmd in cmdGen.genLinkerExtraCommand(linkInfo.outPath))
                {
                    if (runExe(extraLinkerCmd.exePath, extraLinkerCmd.commandLine,
                        out string cmdOutput, extraLinkerCmd.outputEncoding) == CODE_DONE)
                    {
                        log("\r\n>> " + extraLinkerCmd.title);

                        /* skip empty string */
                        if (string.IsNullOrEmpty(cmdOutput))
                            continue;

                        log("\r\n" + cmdOutput, false);
                    }
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
                        {
                            string ccID = cmdGen.getCompilerId().ToLower();

                            switch (ccID)
                            {
                                case "sdcc":
                                    parseMapFileForSdcc(mapFileFullPath,
                                        out ram_size, out rom_size, out mapLog);
                                    break;
                                case "iar_stm8":
                                    parseMapFileForIarStm8(mapFileFullPath,
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
                        if ((ram_size >= 0 || rom_size >= 0) &&
                            (ram_max_size > 0 && rom_max_size > 0))
                        {
                            log("");

                            if (ram_size >= 0) // print ram usage
                            {
                                if (ram_size > 1024)
                                {
                                    float size_kb = ram_size / 1024.0f;
                                    float max_kb = ram_max_size / 1024.0f;
                                    string suffix = size_kb.ToString("f1") + "KB/" + max_kb.ToString("f1") + "KB";
                                    printProgress("RAM: ", (float)ram_size / ram_max_size, suffix);
                                }
                                else
                                {
                                    string suffix = ram_size.ToString() + "B/" + ram_max_size.ToString() + "B";
                                    printProgress("RAM: ", (float)ram_size / ram_max_size, suffix);
                                }
                            }

                            if (rom_size >= 0) // print rom usage
                            {
                                if (rom_size > 1024)
                                {
                                    float size_kb = rom_size / 1024.0f;
                                    float max_kb = rom_max_size / 1024.0f;
                                    string suffix = size_kb.ToString("f1") + "KB/" + max_kb.ToString("f1") + "KB";
                                    printProgress("ROM: ", (float)rom_size / rom_max_size, suffix);
                                }
                                else
                                {
                                    string suffix = rom_size.ToString() + "B/" + rom_max_size.ToString() + "B";
                                    printProgress("ROM: ", (float)rom_size / rom_max_size, suffix);
                                }
                            }
                        }
                    }
                    catch (Exception err)
                    {
                        warn("\r\ncan't read information from '.map' file !, " + err.Message);
                    }
                }

                // execute output command
                CmdGenerator.CmdInfo[] commandList = (cmdGen.isDisableOutputTask()) ?
                    null :
                    cmdGen.genOutputCommand(linkInfo.outPath);

                if (commandList != null &&
                    commandList.Length > 0)
                {
                    log("");
                    infoWithLable("", false);
                    info("start outputting file ...");

                    foreach (CmdGenerator.CmdInfo outputCmdInfo in commandList)
                    {
                        log("\r\n>> " + outputCmdInfo.title, false);

                        string exeLog = "";

                        try
                        {
                            string exeAbsPath = replaceEnvVariable(outputCmdInfo.exePath);

                            if (!File.Exists(exeAbsPath))
                            {
                                throw new Exception("not found " + Path.GetFileName(exeAbsPath)
                                    + " !, [path] : \"" + exeAbsPath + "\"");
                            }

                            // must use 'cmd', because SDCC has '>' command
                            int eCode = runShellCommand("\"" + outputCmdInfo.exePath + "\" " + outputCmdInfo.commandLine, out string _exe_log);
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

                            if (!string.IsNullOrEmpty(exeLog.Trim()))
                            {
                                log("\r\n" + exeLog, false);
                            }

                            error("\r\n" + err.Message);
                        }
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

                // close and unlock log file
                unlockLogs();

                return CODE_ERR;
            }

            try
            {
                switchWorkDir(projectRoot);
                runTasks("RUN TASKS AFTER BUILD", "afterBuildTasks", taskEnvs);
                resetWorkDir();
            }
            catch (Exception err)
            {
                errorWithLable(err.Message + "\r\n");
            }

            // close and unlock log file
            unlockLogs();

            return CODE_DONE;
        }

        /*static string[] parseCommandLineArgs(string cliStr)
        {
            var argsLi = new List<string>(32);

            bool inQuote = false;
            string curArg = string.Empty;

            foreach (var char_ in cliStr)
            {
                if (char_ == '"' && (curArg.Length == 0 || curArg[^1] != '\\')) // is a "..." start or end
                {
                    if (inQuote)
                    {
                        inQuote = false;
                        if (!string.IsNullOrWhiteSpace(curArg)) argsLi.Add(curArg);
                        curArg = string.Empty;
                    }
                    else
                    {
                        inQuote = true;
                    }

                    continue; // skip '"'
                }

                if (inQuote) // in "..." region
                {
                    curArg += char_;
                }
                else // out "..." region
                {
                    if (char.IsWhiteSpace(char_))
                    {
                        if (!string.IsNullOrWhiteSpace(curArg)) argsLi.Add(curArg);
                        curArg = string.Empty;
                    }
                    else
                    {
                        curArg += char_;
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(curArg))
            {
                argsLi.Add(curArg);
            }

            return argsLi.ToArray();
        }*/

        static void parseMapFileForIarStm8(string mapFileFullPath,
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
                + ((progress * 100).ToString("f1") + "%").PadRight(8 + 3)
                + (" " + suffix);

            if (progress >= 1.0f)
            {
                error(progTxt);
            }
            else if (progress >= 0.95f)
            {
                warn(progTxt);
            }
            else
            {
                info(progTxt);
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

        static void setEnvValue(string key, string value)
        {
            // del old
            if (curEnvs.ContainsKey(key)) curEnvs.Remove(key);

            // append for 'PATH' var
            if (key.ToLower() == "path")
            {
                string val = Environment.GetEnvironmentVariable(key);

                if (val != null) // found path, append it
                {
                    val = val + Path.PathSeparator + value;
                    Environment.SetEnvironmentVariable(key, val);
                }
                else // not found, set it
                {
                    Environment.SetEnvironmentVariable(key, value);
                }

                return;
            }

            // set env
            Environment.SetEnvironmentVariable(key, value);
            curEnvs.Add(key, value);
        }

        public static string replaceEnvVariable(string str)
        {
            foreach (var keyValue in curEnvs)
            {
                str = str
                    .Replace("%" + keyValue.Key + "%", keyValue.Value)
                    .Replace("${" + keyValue.Key + "}", keyValue.Value)
                    .Replace("$(" + keyValue.Key + ")", keyValue.Value);
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
                    printCompileOutput(Regex.Replace(logTxt, @"(?<enter>\n)", "${enter}   "));

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

        public static int runExe(string filename, string args, out string output_,
            Encoding encoding = null)
        {
            return runExe(filename, args, out output_, out string _, out string __, encoding);
        }

        public static int runExe(string filename, string args, out string output_,
            out string stdOut_, out string stdErr_, Encoding encoding = null)
        {
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

        public static int runShellCommand(string command, out string _output, Encoding encoding = null)
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

            return runExe(filename, args, out _output, encoding);
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

        struct TaskData
        {
            public ManualResetEvent _event;
            public int index;
            public int end;
        }

        struct CompilerLogData
        {
            public string logTxt;
            public CmdGenerator.CmdInfo srcInfo;
        }

        static void compileByMulThread(int thrNum, CmdGenerator.CmdInfo[] cmds, List<string> errLogs)
        {
            Exception err = null;
            bool isBuildEnd = false;

            BlockingCollection<CompilerLogData> ccLogQueue = new BlockingCollection<CompilerLogData>();
            Thread compilerLogger;

            // print title
            info("start compilation (jobs: " + thrNum.ToString() + ") ...");
            if (cmds.Length > 0) log("");

            // create logger thread
            {
                compilerLogger = new Thread(delegate () {

                    int curProgress = 0;
                    int tolProgress = cmds.Length;

                    while (true)
                    {
                        if (isBuildEnd && ccLogQueue.Count == 0)
                            break; // exit

                        if (ccLogQueue.TryTake(out CompilerLogData logData, 100))
                        {
                            string compilerTag = logData.srcInfo.compilerType == "asm" ? "AS" : "CC";
                            string humanRdPath = toHumanReadablePath(logData.srcInfo.sourcePath);

                            // log progress
                            string progressTag = genProgressTag(++curProgress, tolProgress);
                            string progressLog = ">> " + progressTag + " " + compilerTag + " '" + humanRdPath + "'";
                            Console.WriteLine(progressLog);

                            if (!string.IsNullOrWhiteSpace(logData.logTxt))
                            {
                                string rStr = renderCompilerOutput(logData.logTxt);
                                Console.Write(rStr);
                            }
                        }
                    }
                });

                compilerLogger.Start();
            }

            // worker's function
            ParameterizedThreadStart workerFunc = delegate (object _dat) {

                TaskData dat = (TaskData)_dat;

                for (int index = dat.index; index < dat.end; index++)
                {
                    var ccArgs = cmds[index];

                    if (err != null) break; // compile error, exit

                    int exitCode;
                    string cclog;

                    // do compile

                    if (string.IsNullOrEmpty(ccArgs.argsForSplitter))
                    {
                        exitCode = runExe(
                            ccArgs.exePath, ccArgs.commandLine,
                            out string output, ccArgs.outputEncoding);

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
                            out string resultOut, out string ccOut, ccArgs.outputEncoding);

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

                    if (exitCode > ERR_LEVEL)
                    {
                        lock (errLogs)
                        {
                            errLogs.Add(cclog);
                        }

                        err = new Exception("compilation failed at : \"" + ccArgs.sourcePath + "\", exit code: " + exitCode.ToString());
                        break;
                    }
                }

                dat._event.Set();
            };

            // alloc some work threads and start compile
            ManualResetEvent[] tEvents = new ManualResetEvent[thrNum];
            {
                Thread[] tasks = new Thread[thrNum];
                int part = cmds.Length / thrNum; // amount of src files for per workers

                for (int i = 0; i < thrNum; i++)
                {
                    tEvents[i] = new ManualResetEvent(false);
                    tasks[i] = new Thread(workerFunc);

                    TaskData param = new TaskData {
                        _event = tEvents[i],
                        index = i * part
                    };

                    param.end = (i == thrNum - 1) ? (cmds.Length) : (param.index + part);

                    tasks[i].Start(param);
                }
            }

            WaitHandle.WaitAll(tEvents); // wait work thread go end

            isBuildEnd = true; // notify build is end

            compilerLogger.Join(); // wait logger end

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

        static int runTasks(string label, string fieldName, Dictionary<string, string> envKvMap)
        {
            JObject options = (JObject)paramsObj[CmdGenerator.optionKey];

            if (options.ContainsKey(fieldName))
            {
                try
                {
                    JArray taskList = (JArray)options[fieldName];

                    if (taskList.Count == 0)
                    {
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
                        {
                            // task is disabled, ignore it !
                            continue;
                        }

                        if (!cmd.ContainsKey("name"))
                        {
                            throw new Exception("task name can't be null !");
                        }

                        // print task name
                        string tName = cmd["name"].Value<string>();
                        log("\r\n>> " + tName + getBlanks(maxLen - tName.Length) + "\t\t", false);

                        if (!cmd.ContainsKey("command"))
                        {
                            throw new Exception("task command line can't be null !");
                        }

                        string command = cmd["command"].Value<string>().Trim();

                        // replace env path
                        foreach (var kv in envKvMap)
                        {
                            var pattern = kv.Key
                                .Replace("$", "\\$")
                                .Replace("{", "\\{").Replace("}", "\\}");
                            command = Regex.Replace(command, pattern, kv.Value, RegexOptions.IgnoreCase);
                        }

                        // run command
                        if (runShellCommand(command, out string cmdStdout) == CODE_DONE)
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

                    log(""); // empty line
                }
                catch (Exception e)
                {
                    warn("run task failed ! " + e.Message);
                    warnWithLable("can not parse task information, aborted !\r\n");
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

        static Dictionary<string, bool> diffCache = new(512);

        static readonly DateTime utcBaseTime = TimeZoneInfo.ConvertTimeFromUtc(new System.DateTime(1970, 1, 1), TimeZoneInfo.Local);
        static CheckDiffRes checkDiff(string modelID, Dictionary<string, CmdGenerator.CmdInfo> commands)
        {
            CheckDiffRes res = new CheckDiffRes();

            Func<CmdGenerator.CmdInfo, bool> AddToChangeList = (cmd) => {

                if (cFileFilter.IsMatch(cmd.sourcePath))
                {
                    res.cCount++;
                }
                else if (cppFileFilter.IsMatch(cmd.sourcePath))
                {
                    res.cppCount++;
                }
                else if (asmFileFilter.IsMatch(cmd.sourcePath))
                {
                    res.asmCount++;
                }

                res.totalCmds.Add(cmd.sourcePath, cmd);

                return true;
            };

            try
            {
                DateTime optLastWriteTime = utcBaseTime.AddMilliseconds(paramsMtime);

                foreach (var cmd in commands.Values)
                {
                    if (File.Exists(cmd.outPath))
                    {
                        DateTime objLastWriteTime = File.GetLastWriteTime(cmd.outPath);
                        DateTime srcLastWriteTime = File.GetLastWriteTime(cmd.sourcePath);

                        // src file is newer than obj file
                        if (DateTime.Compare(srcLastWriteTime, objLastWriteTime) > 0)
                        {
                            AddToChangeList(cmd);
                        }

                        // file options is newer than obj file
                        else if (srcParams.ContainsKey(cmd.sourcePath) &&
                            DateTime.Compare(optLastWriteTime, objLastWriteTime) > 0)
                        {
                            AddToChangeList(cmd);
                        }

                        // check referance is changed
                        else
                        {
                            string refFilePath = Path.GetDirectoryName(cmd.outPath)
                                + Path.DirectorySeparatorChar + Path.GetFileNameWithoutExtension(cmd.outPath) + ".d";

                            if (File.Exists(refFilePath))
                            {
                                string[] refList = parseRefFile(refFilePath, modelID);

                                if (refList != null)
                                {
                                    foreach (var refPath in refList)
                                    {
                                        if (diffCache.ContainsKey(refPath))
                                        {
                                            if (diffCache[refPath])
                                            {
                                                AddToChangeList(cmd);
                                                break; // file need recompile, exit
                                            }
                                        }
                                        else // not in cache
                                        {
                                            if (File.Exists(refPath))
                                            {
                                                DateTime lastWrTime = File.GetLastWriteTime(refPath);
                                                bool isOutOfDate = DateTime.Compare(lastWrTime, objLastWriteTime) > 0;
                                                diffCache.Add(refPath, isOutOfDate); // add to cache

                                                if (isOutOfDate)
                                                {
                                                    AddToChangeList(cmd);
                                                    break; // out of date, need recompile, exit
                                                }
                                            }
                                            else // not found ref, ref file need update
                                            {
                                                AddToChangeList(cmd);
                                                break; // need recompile, exit
                                            }
                                        }
                                    }
                                }
                                else // not found parser or parse error
                                {
                                    AddToChangeList(cmd);
                                }
                            }
                            else // not found ref file
                            {
                                AddToChangeList(cmd);
                            }
                        }
                    }
                    else
                    {
                        AddToChangeList(cmd);
                    }
                }
            }
            catch (Exception e)
            {
                log("");
                warn(e.Message);
                log("");
                warnWithLable("Check difference failed !, Use normal build !");
                log("");
            }

            return res;
        }

        static string toAbsolutePath(string _repath)
        {
            string repath = Utility.toLocalPath(_repath);

            if (repath.Length > 1 && char.IsLetter(repath[0]) && repath[1] == ':')
            {
                return repath;
            }

            return projectRoot + Path.DirectorySeparatorChar + repath;
        }

        static string toHumanReadablePath(string absPath)
        {
            return showRelativePathOnLog ?
                Utility.toRelativePath(projectRoot, absPath, true) ?? absPath :
                Path.GetFileName(absPath);
        }

        static Regex whitespaceMatcher = new Regex(@"(?<![\\:]) ", RegexOptions.Compiled);

        static string[] gnu_parseRefLines(string[] lines)
        {
            HashSet<string> resultList = new HashSet<string>();
            int resultCnt = 0;

            for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                string line = lines[lineIndex].TrimEnd('\\').Trim(); // remove char '\' end of line

                if (lineIndex == 0) // first line is makefile dep format: '<obj>: <deps>'
                {
                    int sepIndex = line.IndexOf(": ");
                    if (sepIndex > 0) line = line.Substring(sepIndex + 1).Trim();
                    else continue; /* line is invalid, skip */
                }

                string[] subLines = whitespaceMatcher.Split(line);

                foreach (string subLine in subLines)
                {
                    if (string.IsNullOrWhiteSpace(subLine)) continue;

                    resultCnt++;

                    if (resultCnt == 1) continue; /* skip first ref, it's src self */

                    resultList.Add(toAbsolutePath(subLine
                        .Replace("\\ ", " ")
                        .Replace("\\:", ":")));
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
                        .Trim();
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
                    return ac5_parseRefLines(lines, 2);
                case "SDCC":
                case "AC6":
                case "GCC":
                    return gnu_parseRefLines(lines);
                default:
                    return gnu_parseRefLines(lines);
            }
        }

        static void prepareModel()
        {
            var globals = (JObject)compilerModel["global"];
            var groups = (JObject)compilerModel["groups"];

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
                    ((JArray)paramsObj["defines"]).Add(define);
                }
            }
        }

        static void addToSourceList(string rootDir, IEnumerable<string> sourceList, JObject srcParamsObj)
        {
            foreach (string repath in sourceList)
            {
                string sourcePath = Utility.isAbsolutePath(repath)
                    ? repath : (rootDir + Path.DirectorySeparatorChar + repath);

                FileInfo file = new FileInfo(sourcePath);

                if (file.Exists)
                {
                    if (cFileFilter.IsMatch(file.Name))
                    {
                        cList.Add(file.FullName);
                    }
                    else if (cppFileFilter.IsMatch(file.Name))
                    {
                        cppList.Add(file.FullName);
                    }
                    else if (asmFileFilter.IsMatch(file.Name))
                    {
                        asmList.Add(file.FullName);
                    }
                    else if (libFileFilter.IsMatch(file.Name))
                    {
                        libList.Add(file.FullName);
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
            }
        }

        static void toAbsolutePaths(string rootDir, JArray jArr)
        {
            string[] incList = jArr.ToObject<string[]>();

            jArr.RemoveAll();

            foreach (string _path in incList)
            {
                if (Utility.isAbsolutePath(_path))
                {
                    jArr.Add(_path);
                }
                else
                {
                    jArr.Add(rootDir + Path.DirectorySeparatorChar + _path);
                }
            }
        }

        //////////////////////////////////////////////////
        ///             logger function
        //////////////////////////////////////////////////

        static void lockLogs()
        {
            if (logStream == null)
            {
                string logPath = dumpPath + Path.DirectorySeparatorChar + "unify_builder.log";
                logStream = new FileStream(logPath, FileMode.Append, FileAccess.Write, FileShare.None);
            }
        }

        static void unlockLogs()
        {
            try
            {
                logStream.Flush();
                logStream.Close();
            }
            catch (Exception)
            {
                // nothing todo
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

                    byte[] buf = RuntimeEncoding.instance().Default.GetBytes(txt);
                    logStream.Write(buf, 0, buf.Length);
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
