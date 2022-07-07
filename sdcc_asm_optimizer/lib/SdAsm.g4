//
// ASxxxx syntax reference: https://shop-pdp.net/ashtml/asxs01.htm#Symbols
//

grammar SdAsm;

asmFile
    : CRLF* (codeLine CRLF+)* EOF
    | codeLine EOF
    | EOF
    ;

codeLine
    : directive
    | segment
    | bootAddr
    | memoryAlloc
    | ifStatement
    | label
    | statement
    ;

directive
    : '.' ('16bit' | '24bit' | '32bit')
    | '.' 'module' moduleName
    | '.' 'optsdcc' sdccOpts+
    | '.' 'local' Identifier (',' Identifier)*
    ;

moduleName
    : Identifier
    ;

sdccOpts
    : '-'+ Identifier
    ;

segment
    : '.' SegmentType segmentName ('(' segmentOpts (',' segmentOpts)* ')')?
    ;

segmentName
    : Identifier
    ;

segmentOpts
    : Identifier (Assign Identifier)?
    ;

bootAddr
    : '.' ORG (Number | expressions)
    ;

memoryAlloc
    : '.' DataType memoryData (',' memoryData)*
    ;

memoryData
    : Number '$'? 
    | StringLiteral
    | '#'? '(' expr ')'
    ;

ifStatement
    : '.if' '!'? Identifier
    | '.ifdef' Identifier
    | '.else'
    | '.endif'
    ;

label
    : (normalLabel | inlineLabel) ':'+ codeLine?
    ;

statement
    : absAddrAllocExpr 
    | instruction expressions?
    ;

absAddrAllocExpr
    : Identifier assignmentOperator Number
    ;

instruction
    : Identifier
    ;

expressions
    : expr (',' expr)*
    ;

expr
    : expr operator expr
    | ('+' | '-' | '~' | unaryOperator) operand
    | operand '.' Number
    | operand
    | ('#' unaryOperator? | unaryOperator | Number)? '(' expressions+ ')'
    ;

operand
    : Number
    | '#'? '@'? (normalLabel | inlineLabel)
    | '.'
    ;

operator
    : arithmeticOperator
    | unaryOperator
    | bitOperator
    | compareOperator
    | logicalOperator
    | assignmentOperator
    ;

arithmeticOperator
    : '+' | '-' | '*' | '/' | '%'
    ;

unaryOperator
    : '<' | '>' | '\'' | '"' | '\\'
    // repeat ops: '+' | '-' | '~' 
    ;

assignmentOperator
    : '=' | '=:' | '=='
    ;

bitOperator
    : '<<' | '>>' | '&' | '|' | '~' | '^'
    ;

compareOperator
    : '!=' | '>' | '>=' | '<' | '<=' 
    ;

logicalOperator
    : '&&' | '||' | '!'
    ;

normalLabel
    : '.'? Identifier
    ;

inlineLabel
    : Number '$'
    ;

/////////////// lexer rules //////////////////

// symbols

Dot: '.';

LeftParen: '(';
RightParen: ')';

LeftBracket: '[';
RightBracket: ']';

// operators

Less: '<';
LessEqual: '<=';
Greater: '>';
GreaterEqual: '>=';
Equal: '==';
NotEqual: '!=';

Minus: '-';
Plus: '+';
Star: '*';
Div: '/';
Mod: '%';

AndAnd: '&&';
OrOr: '||';
Not: '!';

LeftShift: '<<';
RightShift: '>>';
And: '&';
Or: '|';
Caret: '^';
Tilde: '~';

Assign: '=';
PlusAssign: '+=';
MinusAssign: '-=';
StarAssign: '*=';
DivAssign: '/=';
ModAssign: '%=';
LeftShiftAssign: '<<=';
RightShiftAssign: '>>=';
AndAssign: '&=';
OrAssign: '|=';
XorAssign: '^=';
TildeAssign: '~=';

// asm instructions

ORG: 'org';

// txt sym

Question: '?';

SingleQuote: '\'';

Quote: '"';

Colon: ':';

Comma: ',';

Pound: '#';

AT: '@';

CRLF: '\r'? '\n';

// identifiers

SegmentType: 'globl' | 'area' | 'bank';

DataType: 'ascii' | 'byte' | 'db' | 'ds' | 'dw' | 'dd';

Number: '#'? '-'? ([0-9]+ | ('0' [xXhHbBoOqQdD] | '$%' | '$&' | '$#' | '$$') HexadecimalDigit+);

Identifier: [a-zA-Z_$] [0-9a-zA-Z_$]*;

// string

StringLiteral
    :   EncodingPrefix? '"' SCharSequence? '"'
    ;

fragment
CCharSequence
    :   CChar+
    ;

fragment
CChar
    :   ~['\\\r\n]
    |   EscapeSequence
    ;

fragment
EscapeSequence
    :   SimpleEscapeSequence
    |   OctalEscapeSequence
    |   HexadecimalEscapeSequence
    |   UniversalCharacterName
    ;

fragment
SimpleEscapeSequence
    :   '\\' ['"?abfnrtv\\]
    ;

fragment
OctalEscapeSequence
    :   '\\' OctalDigit OctalDigit? OctalDigit?
    ;

fragment
HexadecimalEscapeSequence
    :   '\\x' HexadecimalDigit+
    ;

fragment
UniversalCharacterName
    :   '\\u' HexQuad
    |   '\\U' HexQuad HexQuad
    ;

fragment
EncodingPrefix
    :   'u8'
    |   'u'
    |   'U'
    |   'L'
    ;

fragment
SCharSequence
    :   SChar+
    ;

fragment
SChar
    :   ~["\\\r\n]
    |   EscapeSequence
    |   '\\\n'   // Added line
    |   '\\\r\n' // Added line
    ;

// number define

fragment
HexQuad
    :   HexadecimalDigit HexadecimalDigit HexadecimalDigit HexadecimalDigit
    ;

fragment
HexadecimalDigit
    :   [0-9a-fA-F]
    ;

fragment
OctalDigit
    :   [0-7]
    ;

// ignored content

WS: [ \t]+ -> skip;

COMMENT: ';' ~[\r\n]* -> skip;
