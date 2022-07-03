grammar SdAsm;

asmFile
    : CRLF* (codeLine CRLF+)* EOF
    | codeLine EOF
    | EOF
    ;

codeLine
    : segment 
    | bootAddr
    | memoryAlloc
    | ifStatement
    | label 
    | statement
    ;

segment
    : '.' SegmentName segmentSpec
    ;

segmentSpec
    : '-'* Identifier ('-'* Identifier | '(' (segmentSpec ','?)+ ')')*
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
    | '.else'
    | '.endif'
    ;

label
    : (normalLabel | inlineLabel) ':'+ codeLine?
    ;

statement
    : assignmentExpr 
    | instruction expressions?
    ;

instruction
    : Identifier
    ;

assignmentExpr
    : operand assignmentOperator expr
    ;

expressions
    : expr (',' expr)*
    ;

expr
    : operand? operator operand
    | bitOperationExpr
    | operand
    | ('#' | Number)? '(' expressions+ ')'
    ;

bitOperationExpr
    : operand '.' Number ',' operand
    ;

operand
    : Number
    | '#'? '@'? (normalLabel | inlineLabel)
    ;

operator
    : arithmeticOperator
    | bitOperator
    | compareOperator
    | logicalOperator
    | assignmentOperator
    ;

arithmeticOperator
    : '+' | '-' 
    ;

assignmentOperator
    : '=' 
    // ignore this for asm: | '+=' | '-=' | '*=' | '/=' | '%=' | '<<=' | '>>=' | '&=' | '|=' | '~=' | '^=' 
    ;

bitOperator
    : '<<' | '>>' | '&' | '|' | '~' | '^'
    ;

compareOperator
    : '==' | '!=' | '>' | '>=' | '<' | '<=' 
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

SegmentName: 'module' | 'optsdcc' | 'globl' | 'area';

DataType: 'ascii' | 'byte' | 'db' | 'ds' | 'dw' | 'dd';

Number: '#'? '-'? ([0-9]+ | '0x' HexadecimalDigit+);

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
