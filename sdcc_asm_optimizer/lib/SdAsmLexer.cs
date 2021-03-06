//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     ANTLR Version: 4.10.1
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

// Generated from SdAsm.g4 by ANTLR 4.10.1

// Unreachable code detected
#pragma warning disable 0162
// The variable '...' is assigned but its value is never used
#pragma warning disable 0219
// Missing XML comment for publicly visible type or member '...'
#pragma warning disable 1591
// Ambiguous reference in cref attribute
#pragma warning disable 419

using System;
using System.IO;
using System.Text;
using Antlr4.Runtime;
using Antlr4.Runtime.Atn;
using Antlr4.Runtime.Misc;
using DFA = Antlr4.Runtime.Dfa.DFA;

[System.CodeDom.Compiler.GeneratedCode("ANTLR", "4.10.1")]
[System.CLSCompliant(false)]
public partial class SdAsmLexer : Lexer {
	protected static DFA[] decisionToDFA;
	protected static PredictionContextCache sharedContextCache = new PredictionContextCache();
	public const int
		T__0=1, T__1=2, T__2=3, T__3=4, T__4=5, T__5=6, T__6=7, T__7=8, T__8=9, 
		T__9=10, T__10=11, T__11=12, T__12=13, Dot=14, LeftParen=15, RightParen=16, 
		LeftBracket=17, RightBracket=18, Less=19, LessEqual=20, Greater=21, GreaterEqual=22, 
		Equal=23, NotEqual=24, Minus=25, Plus=26, Star=27, Div=28, Mod=29, AndAnd=30, 
		OrOr=31, Not=32, LeftShift=33, RightShift=34, And=35, Or=36, Caret=37, 
		Tilde=38, Assign=39, PlusAssign=40, MinusAssign=41, StarAssign=42, DivAssign=43, 
		ModAssign=44, LeftShiftAssign=45, RightShiftAssign=46, AndAssign=47, OrAssign=48, 
		XorAssign=49, TildeAssign=50, ORG=51, Question=52, SingleQuote=53, Quote=54, 
		Colon=55, Comma=56, Pound=57, AT=58, CRLF=59, SegmentType=60, DataType=61, 
		Number=62, Identifier=63, StringLiteral=64, WS=65, COMMENT=66;
	public static string[] channelNames = {
		"DEFAULT_TOKEN_CHANNEL", "HIDDEN"
	};

	public static string[] modeNames = {
		"DEFAULT_MODE"
	};

	public static readonly string[] ruleNames = {
		"T__0", "T__1", "T__2", "T__3", "T__4", "T__5", "T__6", "T__7", "T__8", 
		"T__9", "T__10", "T__11", "T__12", "Dot", "LeftParen", "RightParen", "LeftBracket", 
		"RightBracket", "Less", "LessEqual", "Greater", "GreaterEqual", "Equal", 
		"NotEqual", "Minus", "Plus", "Star", "Div", "Mod", "AndAnd", "OrOr", "Not", 
		"LeftShift", "RightShift", "And", "Or", "Caret", "Tilde", "Assign", "PlusAssign", 
		"MinusAssign", "StarAssign", "DivAssign", "ModAssign", "LeftShiftAssign", 
		"RightShiftAssign", "AndAssign", "OrAssign", "XorAssign", "TildeAssign", 
		"ORG", "Question", "SingleQuote", "Quote", "Colon", "Comma", "Pound", 
		"AT", "CRLF", "SegmentType", "DataType", "Number", "Identifier", "StringLiteral", 
		"CCharSequence", "CChar", "EscapeSequence", "SimpleEscapeSequence", "OctalEscapeSequence", 
		"HexadecimalEscapeSequence", "UniversalCharacterName", "EncodingPrefix", 
		"SCharSequence", "SChar", "HexQuad", "HexadecimalDigit", "OctalDigit", 
		"WS", "COMMENT"
	};


	public SdAsmLexer(ICharStream input)
	: this(input, Console.Out, Console.Error) { }

	public SdAsmLexer(ICharStream input, TextWriter output, TextWriter errorOutput)
	: base(input, output, errorOutput)
	{
		Interpreter = new LexerATNSimulator(this, _ATN, decisionToDFA, sharedContextCache);
	}

	private static readonly string[] _LiteralNames = {
		null, "'16bit'", "'24bit'", "'32bit'", "'module'", "'optsdcc'", "'local'", 
		"'$'", "'.if'", "'.ifdef'", "'.else'", "'.endif'", "'\\'", "'=:'", "'.'", 
		"'('", "')'", "'['", "']'", "'<'", "'<='", "'>'", "'>='", "'=='", "'!='", 
		"'-'", "'+'", "'*'", "'/'", "'%'", "'&&'", "'||'", "'!'", "'<<'", "'>>'", 
		"'&'", "'|'", "'^'", "'~'", "'='", "'+='", "'-='", "'*='", "'/='", "'%='", 
		"'<<='", "'>>='", "'&='", "'|='", "'^='", "'~='", "'org'", "'?'", "'''", 
		"'\"'", "':'", "','", "'#'", "'@'"
	};
	private static readonly string[] _SymbolicNames = {
		null, null, null, null, null, null, null, null, null, null, null, null, 
		null, null, "Dot", "LeftParen", "RightParen", "LeftBracket", "RightBracket", 
		"Less", "LessEqual", "Greater", "GreaterEqual", "Equal", "NotEqual", "Minus", 
		"Plus", "Star", "Div", "Mod", "AndAnd", "OrOr", "Not", "LeftShift", "RightShift", 
		"And", "Or", "Caret", "Tilde", "Assign", "PlusAssign", "MinusAssign", 
		"StarAssign", "DivAssign", "ModAssign", "LeftShiftAssign", "RightShiftAssign", 
		"AndAssign", "OrAssign", "XorAssign", "TildeAssign", "ORG", "Question", 
		"SingleQuote", "Quote", "Colon", "Comma", "Pound", "AT", "CRLF", "SegmentType", 
		"DataType", "Number", "Identifier", "StringLiteral", "WS", "COMMENT"
	};
	public static readonly IVocabulary DefaultVocabulary = new Vocabulary(_LiteralNames, _SymbolicNames);

	[NotNull]
	public override IVocabulary Vocabulary
	{
		get
		{
			return DefaultVocabulary;
		}
	}

	public override string GrammarFileName { get { return "SdAsm.g4"; } }

	public override string[] RuleNames { get { return ruleNames; } }

	public override string[] ChannelNames { get { return channelNames; } }

	public override string[] ModeNames { get { return modeNames; } }

	public override int[] SerializedAtn { get { return _serializedATN; } }

	static SdAsmLexer() {
		decisionToDFA = new DFA[_ATN.NumberOfDecisions];
		for (int i = 0; i < _ATN.NumberOfDecisions; i++) {
			decisionToDFA[i] = new DFA(_ATN.GetDecisionState(i), i);
		}
	}
	private static int[] _serializedATN = {
		4,0,66,517,6,-1,2,0,7,0,2,1,7,1,2,2,7,2,2,3,7,3,2,4,7,4,2,5,7,5,2,6,7,
		6,2,7,7,7,2,8,7,8,2,9,7,9,2,10,7,10,2,11,7,11,2,12,7,12,2,13,7,13,2,14,
		7,14,2,15,7,15,2,16,7,16,2,17,7,17,2,18,7,18,2,19,7,19,2,20,7,20,2,21,
		7,21,2,22,7,22,2,23,7,23,2,24,7,24,2,25,7,25,2,26,7,26,2,27,7,27,2,28,
		7,28,2,29,7,29,2,30,7,30,2,31,7,31,2,32,7,32,2,33,7,33,2,34,7,34,2,35,
		7,35,2,36,7,36,2,37,7,37,2,38,7,38,2,39,7,39,2,40,7,40,2,41,7,41,2,42,
		7,42,2,43,7,43,2,44,7,44,2,45,7,45,2,46,7,46,2,47,7,47,2,48,7,48,2,49,
		7,49,2,50,7,50,2,51,7,51,2,52,7,52,2,53,7,53,2,54,7,54,2,55,7,55,2,56,
		7,56,2,57,7,57,2,58,7,58,2,59,7,59,2,60,7,60,2,61,7,61,2,62,7,62,2,63,
		7,63,2,64,7,64,2,65,7,65,2,66,7,66,2,67,7,67,2,68,7,68,2,69,7,69,2,70,
		7,70,2,71,7,71,2,72,7,72,2,73,7,73,2,74,7,74,2,75,7,75,2,76,7,76,2,77,
		7,77,2,78,7,78,1,0,1,0,1,0,1,0,1,0,1,0,1,1,1,1,1,1,1,1,1,1,1,1,1,2,1,2,
		1,2,1,2,1,2,1,2,1,3,1,3,1,3,1,3,1,3,1,3,1,3,1,4,1,4,1,4,1,4,1,4,1,4,1,
		4,1,4,1,5,1,5,1,5,1,5,1,5,1,5,1,6,1,6,1,7,1,7,1,7,1,7,1,8,1,8,1,8,1,8,
		1,8,1,8,1,8,1,9,1,9,1,9,1,9,1,9,1,9,1,10,1,10,1,10,1,10,1,10,1,10,1,10,
		1,11,1,11,1,12,1,12,1,12,1,13,1,13,1,14,1,14,1,15,1,15,1,16,1,16,1,17,
		1,17,1,18,1,18,1,19,1,19,1,19,1,20,1,20,1,21,1,21,1,21,1,22,1,22,1,22,
		1,23,1,23,1,23,1,24,1,24,1,25,1,25,1,26,1,26,1,27,1,27,1,28,1,28,1,29,
		1,29,1,29,1,30,1,30,1,30,1,31,1,31,1,32,1,32,1,32,1,33,1,33,1,33,1,34,
		1,34,1,35,1,35,1,36,1,36,1,37,1,37,1,38,1,38,1,39,1,39,1,39,1,40,1,40,
		1,40,1,41,1,41,1,41,1,42,1,42,1,42,1,43,1,43,1,43,1,44,1,44,1,44,1,44,
		1,45,1,45,1,45,1,45,1,46,1,46,1,46,1,47,1,47,1,47,1,48,1,48,1,48,1,49,
		1,49,1,49,1,50,1,50,1,50,1,50,1,51,1,51,1,52,1,52,1,53,1,53,1,54,1,54,
		1,55,1,55,1,56,1,56,1,57,1,57,1,58,3,58,344,8,58,1,58,1,58,1,59,1,59,1,
		59,1,59,1,59,1,59,1,59,1,59,1,59,1,59,1,59,1,59,1,59,3,59,361,8,59,1,60,
		1,60,1,60,1,60,1,60,1,60,1,60,1,60,1,60,1,60,1,60,1,60,1,60,1,60,1,60,
		1,60,1,60,3,60,380,8,60,1,61,3,61,383,8,61,1,61,3,61,386,8,61,1,61,4,61,
		389,8,61,11,61,12,61,390,1,61,1,61,1,61,1,61,1,61,1,61,1,61,1,61,1,61,
		1,61,3,61,403,8,61,1,61,4,61,406,8,61,11,61,12,61,407,3,61,410,8,61,1,
		62,1,62,5,62,414,8,62,10,62,12,62,417,9,62,1,63,3,63,420,8,63,1,63,1,63,
		3,63,424,8,63,1,63,1,63,1,64,4,64,429,8,64,11,64,12,64,430,1,65,1,65,3,
		65,435,8,65,1,66,1,66,1,66,1,66,3,66,441,8,66,1,67,1,67,1,67,1,68,1,68,
		1,68,3,68,449,8,68,1,68,3,68,452,8,68,1,69,1,69,1,69,1,69,4,69,458,8,69,
		11,69,12,69,459,1,70,1,70,1,70,1,70,1,70,1,70,1,70,1,70,1,70,1,70,3,70,
		472,8,70,1,71,1,71,1,71,3,71,477,8,71,1,72,4,72,480,8,72,11,72,12,72,481,
		1,73,1,73,1,73,1,73,1,73,1,73,1,73,3,73,491,8,73,1,74,1,74,1,74,1,74,1,
		74,1,75,1,75,1,76,1,76,1,77,4,77,503,8,77,11,77,12,77,504,1,77,1,77,1,
		78,1,78,5,78,511,8,78,10,78,12,78,514,9,78,1,78,1,78,0,0,79,1,1,3,2,5,
		3,7,4,9,5,11,6,13,7,15,8,17,9,19,10,21,11,23,12,25,13,27,14,29,15,31,16,
		33,17,35,18,37,19,39,20,41,21,43,22,45,23,47,24,49,25,51,26,53,27,55,28,
		57,29,59,30,61,31,63,32,65,33,67,34,69,35,71,36,73,37,75,38,77,39,79,40,
		81,41,83,42,85,43,87,44,89,45,91,46,93,47,95,48,97,49,99,50,101,51,103,
		52,105,53,107,54,109,55,111,56,113,57,115,58,117,59,119,60,121,61,123,
		62,125,63,127,64,129,0,131,0,133,0,135,0,137,0,139,0,141,0,143,0,145,0,
		147,0,149,0,151,0,153,0,155,65,157,66,1,0,12,1,0,48,57,12,0,66,66,68,68,
		72,72,79,79,81,81,88,88,98,98,100,100,104,104,111,111,113,113,120,120,
		4,0,36,36,65,90,95,95,97,122,5,0,36,36,48,57,65,90,95,95,97,122,4,0,10,
		10,13,13,39,39,92,92,10,0,34,34,39,39,63,63,92,92,97,98,102,102,110,110,
		114,114,116,116,118,118,3,0,76,76,85,85,117,117,4,0,10,10,13,13,34,34,
		92,92,3,0,48,57,65,70,97,102,1,0,48,55,2,0,9,9,32,32,2,0,10,10,13,13,539,
		0,1,1,0,0,0,0,3,1,0,0,0,0,5,1,0,0,0,0,7,1,0,0,0,0,9,1,0,0,0,0,11,1,0,0,
		0,0,13,1,0,0,0,0,15,1,0,0,0,0,17,1,0,0,0,0,19,1,0,0,0,0,21,1,0,0,0,0,23,
		1,0,0,0,0,25,1,0,0,0,0,27,1,0,0,0,0,29,1,0,0,0,0,31,1,0,0,0,0,33,1,0,0,
		0,0,35,1,0,0,0,0,37,1,0,0,0,0,39,1,0,0,0,0,41,1,0,0,0,0,43,1,0,0,0,0,45,
		1,0,0,0,0,47,1,0,0,0,0,49,1,0,0,0,0,51,1,0,0,0,0,53,1,0,0,0,0,55,1,0,0,
		0,0,57,1,0,0,0,0,59,1,0,0,0,0,61,1,0,0,0,0,63,1,0,0,0,0,65,1,0,0,0,0,67,
		1,0,0,0,0,69,1,0,0,0,0,71,1,0,0,0,0,73,1,0,0,0,0,75,1,0,0,0,0,77,1,0,0,
		0,0,79,1,0,0,0,0,81,1,0,0,0,0,83,1,0,0,0,0,85,1,0,0,0,0,87,1,0,0,0,0,89,
		1,0,0,0,0,91,1,0,0,0,0,93,1,0,0,0,0,95,1,0,0,0,0,97,1,0,0,0,0,99,1,0,0,
		0,0,101,1,0,0,0,0,103,1,0,0,0,0,105,1,0,0,0,0,107,1,0,0,0,0,109,1,0,0,
		0,0,111,1,0,0,0,0,113,1,0,0,0,0,115,1,0,0,0,0,117,1,0,0,0,0,119,1,0,0,
		0,0,121,1,0,0,0,0,123,1,0,0,0,0,125,1,0,0,0,0,127,1,0,0,0,0,155,1,0,0,
		0,0,157,1,0,0,0,1,159,1,0,0,0,3,165,1,0,0,0,5,171,1,0,0,0,7,177,1,0,0,
		0,9,184,1,0,0,0,11,192,1,0,0,0,13,198,1,0,0,0,15,200,1,0,0,0,17,204,1,
		0,0,0,19,211,1,0,0,0,21,217,1,0,0,0,23,224,1,0,0,0,25,226,1,0,0,0,27,229,
		1,0,0,0,29,231,1,0,0,0,31,233,1,0,0,0,33,235,1,0,0,0,35,237,1,0,0,0,37,
		239,1,0,0,0,39,241,1,0,0,0,41,244,1,0,0,0,43,246,1,0,0,0,45,249,1,0,0,
		0,47,252,1,0,0,0,49,255,1,0,0,0,51,257,1,0,0,0,53,259,1,0,0,0,55,261,1,
		0,0,0,57,263,1,0,0,0,59,265,1,0,0,0,61,268,1,0,0,0,63,271,1,0,0,0,65,273,
		1,0,0,0,67,276,1,0,0,0,69,279,1,0,0,0,71,281,1,0,0,0,73,283,1,0,0,0,75,
		285,1,0,0,0,77,287,1,0,0,0,79,289,1,0,0,0,81,292,1,0,0,0,83,295,1,0,0,
		0,85,298,1,0,0,0,87,301,1,0,0,0,89,304,1,0,0,0,91,308,1,0,0,0,93,312,1,
		0,0,0,95,315,1,0,0,0,97,318,1,0,0,0,99,321,1,0,0,0,101,324,1,0,0,0,103,
		328,1,0,0,0,105,330,1,0,0,0,107,332,1,0,0,0,109,334,1,0,0,0,111,336,1,
		0,0,0,113,338,1,0,0,0,115,340,1,0,0,0,117,343,1,0,0,0,119,360,1,0,0,0,
		121,379,1,0,0,0,123,382,1,0,0,0,125,411,1,0,0,0,127,419,1,0,0,0,129,428,
		1,0,0,0,131,434,1,0,0,0,133,440,1,0,0,0,135,442,1,0,0,0,137,445,1,0,0,
		0,139,453,1,0,0,0,141,471,1,0,0,0,143,476,1,0,0,0,145,479,1,0,0,0,147,
		490,1,0,0,0,149,492,1,0,0,0,151,497,1,0,0,0,153,499,1,0,0,0,155,502,1,
		0,0,0,157,508,1,0,0,0,159,160,5,49,0,0,160,161,5,54,0,0,161,162,5,98,0,
		0,162,163,5,105,0,0,163,164,5,116,0,0,164,2,1,0,0,0,165,166,5,50,0,0,166,
		167,5,52,0,0,167,168,5,98,0,0,168,169,5,105,0,0,169,170,5,116,0,0,170,
		4,1,0,0,0,171,172,5,51,0,0,172,173,5,50,0,0,173,174,5,98,0,0,174,175,5,
		105,0,0,175,176,5,116,0,0,176,6,1,0,0,0,177,178,5,109,0,0,178,179,5,111,
		0,0,179,180,5,100,0,0,180,181,5,117,0,0,181,182,5,108,0,0,182,183,5,101,
		0,0,183,8,1,0,0,0,184,185,5,111,0,0,185,186,5,112,0,0,186,187,5,116,0,
		0,187,188,5,115,0,0,188,189,5,100,0,0,189,190,5,99,0,0,190,191,5,99,0,
		0,191,10,1,0,0,0,192,193,5,108,0,0,193,194,5,111,0,0,194,195,5,99,0,0,
		195,196,5,97,0,0,196,197,5,108,0,0,197,12,1,0,0,0,198,199,5,36,0,0,199,
		14,1,0,0,0,200,201,5,46,0,0,201,202,5,105,0,0,202,203,5,102,0,0,203,16,
		1,0,0,0,204,205,5,46,0,0,205,206,5,105,0,0,206,207,5,102,0,0,207,208,5,
		100,0,0,208,209,5,101,0,0,209,210,5,102,0,0,210,18,1,0,0,0,211,212,5,46,
		0,0,212,213,5,101,0,0,213,214,5,108,0,0,214,215,5,115,0,0,215,216,5,101,
		0,0,216,20,1,0,0,0,217,218,5,46,0,0,218,219,5,101,0,0,219,220,5,110,0,
		0,220,221,5,100,0,0,221,222,5,105,0,0,222,223,5,102,0,0,223,22,1,0,0,0,
		224,225,5,92,0,0,225,24,1,0,0,0,226,227,5,61,0,0,227,228,5,58,0,0,228,
		26,1,0,0,0,229,230,5,46,0,0,230,28,1,0,0,0,231,232,5,40,0,0,232,30,1,0,
		0,0,233,234,5,41,0,0,234,32,1,0,0,0,235,236,5,91,0,0,236,34,1,0,0,0,237,
		238,5,93,0,0,238,36,1,0,0,0,239,240,5,60,0,0,240,38,1,0,0,0,241,242,5,
		60,0,0,242,243,5,61,0,0,243,40,1,0,0,0,244,245,5,62,0,0,245,42,1,0,0,0,
		246,247,5,62,0,0,247,248,5,61,0,0,248,44,1,0,0,0,249,250,5,61,0,0,250,
		251,5,61,0,0,251,46,1,0,0,0,252,253,5,33,0,0,253,254,5,61,0,0,254,48,1,
		0,0,0,255,256,5,45,0,0,256,50,1,0,0,0,257,258,5,43,0,0,258,52,1,0,0,0,
		259,260,5,42,0,0,260,54,1,0,0,0,261,262,5,47,0,0,262,56,1,0,0,0,263,264,
		5,37,0,0,264,58,1,0,0,0,265,266,5,38,0,0,266,267,5,38,0,0,267,60,1,0,0,
		0,268,269,5,124,0,0,269,270,5,124,0,0,270,62,1,0,0,0,271,272,5,33,0,0,
		272,64,1,0,0,0,273,274,5,60,0,0,274,275,5,60,0,0,275,66,1,0,0,0,276,277,
		5,62,0,0,277,278,5,62,0,0,278,68,1,0,0,0,279,280,5,38,0,0,280,70,1,0,0,
		0,281,282,5,124,0,0,282,72,1,0,0,0,283,284,5,94,0,0,284,74,1,0,0,0,285,
		286,5,126,0,0,286,76,1,0,0,0,287,288,5,61,0,0,288,78,1,0,0,0,289,290,5,
		43,0,0,290,291,5,61,0,0,291,80,1,0,0,0,292,293,5,45,0,0,293,294,5,61,0,
		0,294,82,1,0,0,0,295,296,5,42,0,0,296,297,5,61,0,0,297,84,1,0,0,0,298,
		299,5,47,0,0,299,300,5,61,0,0,300,86,1,0,0,0,301,302,5,37,0,0,302,303,
		5,61,0,0,303,88,1,0,0,0,304,305,5,60,0,0,305,306,5,60,0,0,306,307,5,61,
		0,0,307,90,1,0,0,0,308,309,5,62,0,0,309,310,5,62,0,0,310,311,5,61,0,0,
		311,92,1,0,0,0,312,313,5,38,0,0,313,314,5,61,0,0,314,94,1,0,0,0,315,316,
		5,124,0,0,316,317,5,61,0,0,317,96,1,0,0,0,318,319,5,94,0,0,319,320,5,61,
		0,0,320,98,1,0,0,0,321,322,5,126,0,0,322,323,5,61,0,0,323,100,1,0,0,0,
		324,325,5,111,0,0,325,326,5,114,0,0,326,327,5,103,0,0,327,102,1,0,0,0,
		328,329,5,63,0,0,329,104,1,0,0,0,330,331,5,39,0,0,331,106,1,0,0,0,332,
		333,5,34,0,0,333,108,1,0,0,0,334,335,5,58,0,0,335,110,1,0,0,0,336,337,
		5,44,0,0,337,112,1,0,0,0,338,339,5,35,0,0,339,114,1,0,0,0,340,341,5,64,
		0,0,341,116,1,0,0,0,342,344,5,13,0,0,343,342,1,0,0,0,343,344,1,0,0,0,344,
		345,1,0,0,0,345,346,5,10,0,0,346,118,1,0,0,0,347,348,5,103,0,0,348,349,
		5,108,0,0,349,350,5,111,0,0,350,351,5,98,0,0,351,361,5,108,0,0,352,353,
		5,97,0,0,353,354,5,114,0,0,354,355,5,101,0,0,355,361,5,97,0,0,356,357,
		5,98,0,0,357,358,5,97,0,0,358,359,5,110,0,0,359,361,5,107,0,0,360,347,
		1,0,0,0,360,352,1,0,0,0,360,356,1,0,0,0,361,120,1,0,0,0,362,363,5,97,0,
		0,363,364,5,115,0,0,364,365,5,99,0,0,365,366,5,105,0,0,366,380,5,105,0,
		0,367,368,5,98,0,0,368,369,5,121,0,0,369,370,5,116,0,0,370,380,5,101,0,
		0,371,372,5,100,0,0,372,380,5,98,0,0,373,374,5,100,0,0,374,380,5,115,0,
		0,375,376,5,100,0,0,376,380,5,119,0,0,377,378,5,100,0,0,378,380,5,100,
		0,0,379,362,1,0,0,0,379,367,1,0,0,0,379,371,1,0,0,0,379,373,1,0,0,0,379,
		375,1,0,0,0,379,377,1,0,0,0,380,122,1,0,0,0,381,383,5,35,0,0,382,381,1,
		0,0,0,382,383,1,0,0,0,383,385,1,0,0,0,384,386,5,45,0,0,385,384,1,0,0,0,
		385,386,1,0,0,0,386,409,1,0,0,0,387,389,7,0,0,0,388,387,1,0,0,0,389,390,
		1,0,0,0,390,388,1,0,0,0,390,391,1,0,0,0,391,410,1,0,0,0,392,393,5,48,0,
		0,393,403,7,1,0,0,394,395,5,36,0,0,395,403,5,37,0,0,396,397,5,36,0,0,397,
		403,5,38,0,0,398,399,5,36,0,0,399,403,5,35,0,0,400,401,5,36,0,0,401,403,
		5,36,0,0,402,392,1,0,0,0,402,394,1,0,0,0,402,396,1,0,0,0,402,398,1,0,0,
		0,402,400,1,0,0,0,403,405,1,0,0,0,404,406,3,151,75,0,405,404,1,0,0,0,406,
		407,1,0,0,0,407,405,1,0,0,0,407,408,1,0,0,0,408,410,1,0,0,0,409,388,1,
		0,0,0,409,402,1,0,0,0,410,124,1,0,0,0,411,415,7,2,0,0,412,414,7,3,0,0,
		413,412,1,0,0,0,414,417,1,0,0,0,415,413,1,0,0,0,415,416,1,0,0,0,416,126,
		1,0,0,0,417,415,1,0,0,0,418,420,3,143,71,0,419,418,1,0,0,0,419,420,1,0,
		0,0,420,421,1,0,0,0,421,423,5,34,0,0,422,424,3,145,72,0,423,422,1,0,0,
		0,423,424,1,0,0,0,424,425,1,0,0,0,425,426,5,34,0,0,426,128,1,0,0,0,427,
		429,3,131,65,0,428,427,1,0,0,0,429,430,1,0,0,0,430,428,1,0,0,0,430,431,
		1,0,0,0,431,130,1,0,0,0,432,435,8,4,0,0,433,435,3,133,66,0,434,432,1,0,
		0,0,434,433,1,0,0,0,435,132,1,0,0,0,436,441,3,135,67,0,437,441,3,137,68,
		0,438,441,3,139,69,0,439,441,3,141,70,0,440,436,1,0,0,0,440,437,1,0,0,
		0,440,438,1,0,0,0,440,439,1,0,0,0,441,134,1,0,0,0,442,443,5,92,0,0,443,
		444,7,5,0,0,444,136,1,0,0,0,445,446,5,92,0,0,446,448,3,153,76,0,447,449,
		3,153,76,0,448,447,1,0,0,0,448,449,1,0,0,0,449,451,1,0,0,0,450,452,3,153,
		76,0,451,450,1,0,0,0,451,452,1,0,0,0,452,138,1,0,0,0,453,454,5,92,0,0,
		454,455,5,120,0,0,455,457,1,0,0,0,456,458,3,151,75,0,457,456,1,0,0,0,458,
		459,1,0,0,0,459,457,1,0,0,0,459,460,1,0,0,0,460,140,1,0,0,0,461,462,5,
		92,0,0,462,463,5,117,0,0,463,464,1,0,0,0,464,472,3,149,74,0,465,466,5,
		92,0,0,466,467,5,85,0,0,467,468,1,0,0,0,468,469,3,149,74,0,469,470,3,149,
		74,0,470,472,1,0,0,0,471,461,1,0,0,0,471,465,1,0,0,0,472,142,1,0,0,0,473,
		474,5,117,0,0,474,477,5,56,0,0,475,477,7,6,0,0,476,473,1,0,0,0,476,475,
		1,0,0,0,477,144,1,0,0,0,478,480,3,147,73,0,479,478,1,0,0,0,480,481,1,0,
		0,0,481,479,1,0,0,0,481,482,1,0,0,0,482,146,1,0,0,0,483,491,8,7,0,0,484,
		491,3,133,66,0,485,486,5,92,0,0,486,491,5,10,0,0,487,488,5,92,0,0,488,
		489,5,13,0,0,489,491,5,10,0,0,490,483,1,0,0,0,490,484,1,0,0,0,490,485,
		1,0,0,0,490,487,1,0,0,0,491,148,1,0,0,0,492,493,3,151,75,0,493,494,3,151,
		75,0,494,495,3,151,75,0,495,496,3,151,75,0,496,150,1,0,0,0,497,498,7,8,
		0,0,498,152,1,0,0,0,499,500,7,9,0,0,500,154,1,0,0,0,501,503,7,10,0,0,502,
		501,1,0,0,0,503,504,1,0,0,0,504,502,1,0,0,0,504,505,1,0,0,0,505,506,1,
		0,0,0,506,507,6,77,0,0,507,156,1,0,0,0,508,512,5,59,0,0,509,511,8,11,0,
		0,510,509,1,0,0,0,511,514,1,0,0,0,512,510,1,0,0,0,512,513,1,0,0,0,513,
		515,1,0,0,0,514,512,1,0,0,0,515,516,6,78,0,0,516,158,1,0,0,0,25,0,343,
		360,379,382,385,390,402,407,409,415,419,423,430,434,440,448,451,459,471,
		476,481,490,504,512,1,6,0,0
	};

	public static readonly ATN _ATN =
		new ATNDeserializer().Deserialize(_serializedATN);


}
