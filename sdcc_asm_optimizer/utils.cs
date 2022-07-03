
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
