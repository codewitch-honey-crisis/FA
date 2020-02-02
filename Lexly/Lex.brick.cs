using LC;
using System;
using System.Collections.Generic;
using F;
using System.Text;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
namespace L{static class Assembler{internal static List<Inst>Parse(LexContext l){var result=new List<Inst>();while(-1!=l.Current&&'}'!=l.Current){result.Add(Inst.Parse(l));
}return result;}internal static List<int[]>Emit(IList<Inst>instructions){var ic=instructions.Count;var result=new List<int[]>(ic);var lmap=new Dictionary<string,
int>();var pc=0;var regm=new Dictionary<Inst,int[][]>();for(var i=0;i<ic;++i){var inst=instructions[i];if(inst.Opcode==Inst.Label){if(lmap.ContainsKey(inst.Name))
throw new InvalidProgramException("Duplicate label "+inst.Name+" found at line "+inst.Line.ToString());lmap.Add(inst.Name,pc);}else if(inst.Opcode==Inst.Regex)
{var reg=new List<int[]>();Compiler.EmitPart(inst.Expr,reg);regm.Add(inst,reg.ToArray());pc+=reg.Count;}else++pc;}pc=0;for(var i=0;i<ic;++i){int dst;var
 inst=instructions[i];int[]code=null;switch(inst.Opcode){case Inst.Regex:Compiler.Fixup(regm[inst],pc);result.AddRange(regm[inst]);break;case Inst.Label:
break;case Inst.Switch:var sw=new List<int>();if(0==inst.Cases.Length){if(0==inst.Labels.Length)break;sw.Add(Inst.Jmp);}else sw.Add(inst.Opcode);for(var
 k=0;k<inst.Cases.Length;k++){var c=inst.Cases[k];sw.AddRange(c.Key);sw.Add(-1);var lbl=c.Value;if(!lmap.TryGetValue(lbl,out dst))throw new InvalidProgramException("Switch references undefined label "
+inst.Name+" at line "+inst.Line.ToString());sw.Add(dst);}if(0<inst.Cases.Length&&(null!=inst.Labels&&0<inst.Labels.Length))sw.Add(-2);if(null!=inst.Labels)
{for(var j=0;j<inst.Labels.Length;j++){var lbl=inst.Labels[j];if(!lmap.TryGetValue(lbl,out dst))throw new InvalidProgramException("Switch references undefined label "
+inst.Name+" at line "+inst.Line.ToString());sw.Add(dst);}}code=sw.ToArray();break;case Inst.Any:code=new int[1];code[0]=inst.Opcode;break;case Inst.Char:
case Inst.UCode:case Inst.NUCode:case Inst.Save:case Inst.Match:code=new int[2];code[0]=inst.Opcode;code[1]=inst.Value;break;case Inst.Set:case Inst.NSet:
var set=new List<int>(inst.Ranges.Length+1);set.Add(inst.Opcode);Compiler.SortRanges(inst.Ranges);set.AddRange(inst.Ranges);code=set.ToArray();break; case
 Inst.Jmp:var jmp=new List<int>(inst.Labels.Length+1);jmp.Add(inst.Opcode);for(var j=0;j<inst.Labels.Length;j++){var lbl=inst.Labels[j];if(!lmap.TryGetValue(lbl,
out dst))throw new InvalidProgramException("Jmp references undefined label "+inst.Name+" at line "+inst.Line.ToString());jmp.Add(dst);}code=jmp.ToArray();
break;}if(null!=code){result.Add(code);}pc=result.Count;}return result;}}class Inst{
#region Opcodes
internal const int Regex=-2; internal const int Label=-1; internal const int Match=1; internal const int Jmp=2; internal const int Switch=3; internal const
 int Any=4; internal const int Char=5; internal const int Set=6; internal const int NSet=7; internal const int UCode=8; internal const int NUCode=9; internal
 const int Save=10;
#endregion
public int Opcode; public int[]Ranges;public string[]Labels;public KeyValuePair<int[],string>[]Cases;public int Value;public string Name;public int Line;
public Ast Expr;internal static Inst Parse(LexContext input){Inst result=new Inst();_SkipCommentsAndWhiteSpace(input);var l=input.Line;var c=input.Column;
var p=input.Position;result.Line=l;var id=_ParseIdentifier(input);switch(id){case"regex":_SkipWhiteSpace(input);result.Opcode=Regex;input.Expecting('(');
input.Advance();result.Expr=Ast.Parse(input);input.Expecting(')');input.Advance();_SkipToNextInstruction(input);break;case"match":_SkipWhiteSpace(input);
var ll=input.CaptureBuffer.Length;var neg=false;if('-'==input.Current){neg=true;input.Advance();}if(!input.TryReadDigits())throw new ExpectingException("Illegal operand in match instruction. Expecting integer",
input.Line,input.Column,input.Position,input.FileOrUrl,"integer");var i=int.Parse(input.GetCapture(ll));if(neg)i=-i;result.Opcode=Match;result.Value=i;
_SkipToNextInstruction(input);break; case"jmp":_SkipWhiteSpace(input);result.Opcode=Jmp;result.Labels=_ParseLabels(input);_SkipToNextInstruction(input);
break;case"switch":_SkipWhiteSpace(input);result.Opcode=Switch;_ParseCases(result,input);_SkipToNextInstruction(input);break;case"any":result.Opcode=Any;
_SkipToNextInstruction(input);break;case"char":_SkipWhiteSpace(input);result.Opcode=Char;result.Value=_ParseChar(input);_SkipToNextInstruction(input);
break;case"set":_SkipWhiteSpace(input);result.Opcode=Set;result.Ranges=_ParseRanges(input);_SkipToNextInstruction(input);break;case"nset":_SkipWhiteSpace(input);
result.Opcode=NSet;result.Ranges=_ParseRanges(input);_SkipToNextInstruction(input);break;case"ucode":_SkipWhiteSpace(input);ll=input.CaptureBuffer.Length;
if(!input.TryReadDigits())throw new ExpectingException("Illegal operand in ucode instruction. Expecting integer",input.Line,input.Column,input.Position,
input.FileOrUrl,"integer");i=int.Parse(input.GetCapture(ll));result.Opcode=UCode;result.Value=i;_SkipToNextInstruction(input);break;case"nucode":_SkipWhiteSpace(input);
ll=input.CaptureBuffer.Length;if(!input.TryReadDigits())throw new ExpectingException("Illegal operand in nucode instruction. Expecting integer",input.Line,
input.Column,input.Position,input.FileOrUrl,"integer");i=int.Parse(input.GetCapture(ll));result.Opcode=NUCode;result.Value=i;_SkipToNextInstruction(input);
break;case"save":_SkipWhiteSpace(input);ll=input.CaptureBuffer.Length;if(!input.TryReadDigits())throw new ExpectingException("Illegal operand in save instruction. Expecting integer",
input.Line,input.Column,input.Position,input.FileOrUrl,"integer");i=int.Parse(input.GetCapture(ll));result.Opcode=Save;result.Value=i;_SkipToNextInstruction(input);
break;default:if(':'!=input.Current)throw new ExpectingException("Expecting instruction or label",l,c,p,input.FileOrUrl,"match","jmp","jmp","any","char",
"set","nset","ucode","nucode","save","label");input.Advance();result.Opcode=Label;result.Name=id;break;}_SkipCommentsAndWhiteSpace(input);return result;
}static void _SkipWhiteSpace(LexContext l){l.EnsureStarted();while(-1!=l.Current&&'\n'!=l.Current&&char.IsWhiteSpace((char)l.Current))l.Advance();}static
 void _SkipToNextInstruction(LexContext l){l.EnsureStarted();while(-1!=l.Current){if(';'==l.Current){_SkipCommentsAndWhiteSpace(l);return;}else if('\n'
==l.Current){_SkipCommentsAndWhiteSpace(l);return;}else if(char.IsWhiteSpace((char)l.Current))l.Advance();else throw new ExpectingException("Unexpected token in input",
l.Line,l.Column,l.Position,l.FileOrUrl,"newline","comment");}}static void _SkipCommentsAndWhiteSpace(LexContext l){l.TrySkipWhiteSpace();while(';'==l.Current)
{l.TrySkipUntil('\n',true);l.TrySkipWhiteSpace();}}static string _ParseIdentifier(LexContext l){l.EnsureStarted();var ll=l.CaptureBuffer.Length;if(-1!=l.Current
&&'_'==l.Current||char.IsLetter((char)l.Current)){l.Capture();l.Advance();while(-1!=l.Current&&'_'==l.Current||char.IsLetterOrDigit((char)l.Current)){
l.Capture();l.Advance();}return l.GetCapture(ll);}throw new ExpectingException("Expecting identifier",l.Line,l.Column,l.Position,"identifier");}static
 KeyValuePair<int,int>_ParseRange(LexContext l){l.EnsureStarted();var first=_ParseChar(l);if('.'!=l.Current){_SkipWhiteSpace(l);l.Expecting(',','\n',';',':',
-1);_SkipWhiteSpace(l);return new KeyValuePair<int,int>(first,first);}l.Advance();l.Expecting('.');l.Advance();var last=_ParseChar(l);_SkipWhiteSpace(l);
l.Expecting(',',';',':','\n',-1);_SkipWhiteSpace(l);return new KeyValuePair<int,int>(first,last);}static int[]_ParseRanges(LexContext l){_SkipWhiteSpace(l);
var result=new List<int>();while(-1!=l.Current&&'\n'!=l.Current&&':'!=l.Current){_SkipWhiteSpace(l);var kvp=_ParseRange(l);result.Add(kvp.Key);result.Add(kvp.Value);
if(','==l.Current)l.Advance();}result.Sort();return result.ToArray();}static void _ParseCases(Inst result,LexContext l){var cases=new List<KeyValuePair<int[],
string>>();while(-1!=l.Current&&'\n'!=l.Current&&';'!=l.Current){_SkipWhiteSpace(l);var line=l.Line;var column=l.Column;var position=l.Position;string
 s;if("case"!=(s=_ParseIdentifier(l))&&"default"!=s)throw new ExpectingException("Expecting case or default",line,column,position,l.FileOrUrl,"case","default");
_SkipWhiteSpace(l);if("case"==s){var ranges=_ParseRanges(l);_SkipWhiteSpace(l);l.Expecting(':');l.Advance();l.Expecting();var dst=_ParseIdentifier(l);
cases.Add(new KeyValuePair<int[],string>(ranges,dst));if(','==l.Current){l.Advance();}}else{_SkipWhiteSpace(l);l.Expecting(':');l.Advance();l.Expecting();
result.Labels=_ParseLabels(l);break;}_SkipWhiteSpace(l);}result.Cases=cases.ToArray();_SkipWhiteSpace(l);}static string[]_ParseLabels(LexContext l){_SkipWhiteSpace(l);
var result=new List<string>();while(-1!=l.Current&&';'!=l.Current&&'\n'!=l.Current){_SkipWhiteSpace(l);var name=_ParseIdentifier(l);_SkipWhiteSpace(l);
result.Add(name);if(','==l.Current){l.Advance();_SkipWhiteSpace(l);}}return result.ToArray();}static int _ParseChar(LexContext l){var line=l.Line;var column
=l.Column;var position=l.Position;l.EnsureStarted();l.Expecting('\"');l.Advance();var ll=l.CaptureBuffer.Length;if(!l.TryReadUntil('\"','\\',false))throw
 new ExpectingException("Unterminated character literal",line,column,position,l.FileOrUrl,"\"");var s=l.GetCapture(ll);int result;if('\\'==s[0]){var e
=s.GetEnumerator();e.MoveNext();result=char.ConvertToUtf32(_ParseEscapeChar(e,l),0);l.Expecting('\"');l.Advance();return result;}result=char.ConvertToUtf32(s,
0);l.Expecting('\"');l.Advance();return result;}static string _ParseEscapeChar(IEnumerator<char>e,LexContext pc){if(e.MoveNext()){switch(e.Current){case
'r':e.MoveNext();return"\r";case'n':e.MoveNext();return"\n";case't':e.MoveNext();return"\t";case'a':e.MoveNext();return"\a";case'b':e.MoveNext();return
"\b";case'f':e.MoveNext();return"\f";case'v':e.MoveNext();return"\v";case'0':e.MoveNext();return"\0";case'\\':e.MoveNext();return"\\";case'\'':e.MoveNext();
return"\'";case'\"':e.MoveNext();return"\"";case'u':var acc=0L;if(!e.MoveNext())break;if(!_IsHexChar(e.Current))break;acc<<=4;acc|=_FromHexChar(e.Current);
if(!e.MoveNext())break;if(!_IsHexChar(e.Current))break;acc<<=4;acc|=_FromHexChar(e.Current);if(!e.MoveNext())break;if(!_IsHexChar(e.Current))break;acc
<<=4;acc|=_FromHexChar(e.Current);if(!e.MoveNext())break;if(!_IsHexChar(e.Current))break;acc<<=4;acc|=_FromHexChar(e.Current);e.MoveNext();return unchecked((char)acc).ToString();
case'x':acc=0;if(!e.MoveNext())break;if(!_IsHexChar(e.Current))break;acc<<=4;acc|=_FromHexChar(e.Current);if(e.MoveNext()&&_IsHexChar(e.Current)){acc<<=
4;acc|=_FromHexChar(e.Current);if(e.MoveNext()&&_IsHexChar(e.Current)){acc<<=4;acc|=_FromHexChar(e.Current);if(e.MoveNext()&&_IsHexChar(e.Current)){acc
<<=4;acc|=_FromHexChar(e.Current);e.MoveNext();}}}return unchecked((char)acc).ToString();case'U':acc=0;if(!e.MoveNext())break;if(!_IsHexChar(e.Current))
break;acc<<=4;acc|=_FromHexChar(e.Current);if(!e.MoveNext())break;if(!_IsHexChar(e.Current))break;acc<<=4;acc|=_FromHexChar(e.Current);if(!e.MoveNext())
break;if(!_IsHexChar(e.Current))break;acc<<=4;acc|=_FromHexChar(e.Current);if(!e.MoveNext())break;if(!_IsHexChar(e.Current))break;acc<<=4;acc|=_FromHexChar(e.Current);
if(!e.MoveNext())break;if(!_IsHexChar(e.Current))break;acc<<=4;acc|=_FromHexChar(e.Current);if(!e.MoveNext())break;if(!_IsHexChar(e.Current))break;acc
<<=4;acc|=_FromHexChar(e.Current);if(!e.MoveNext())break;if(!_IsHexChar(e.Current))break;acc<<=4;acc|=_FromHexChar(e.Current);if(!e.MoveNext())break;if
(!_IsHexChar(e.Current))break;acc<<=4;acc|=_FromHexChar(e.Current);e.MoveNext();return char.ConvertFromUtf32(unchecked((int)acc));default:throw new NotSupportedException(string.Format("Unsupported escape sequence \\{0}",
e.Current));}}throw new ExpectingException("Unterminated escape sequence",pc.Line,pc.Column,pc.Position,pc.FileOrUrl);}static bool _IsHexChar(char hex)
{return(':'>hex&&'/'<hex)||('G'>hex&&'@'<hex)||('g'>hex&&'`'<hex);}static byte _FromHexChar(char hex){if(':'>hex&&'/'<hex)return(byte)(hex-'0');if('G'
>hex&&'@'<hex)return(byte)(hex-'7'); if('g'>hex&&'`'<hex)return(byte)(hex-'W'); throw new ArgumentException("The value was not hex.","hex");}}}namespace
 L{sealed class Ast{
#region Kinds
public const int None=0;public const int Lit=1;public const int Set=2;public const int NSet=3;public const int Cls=4;public const int Cat=5;public const
 int Opt=6;public const int Alt=7;public const int Star=8;public const int Plus=9;public const int Rep=10;public const int UCode=11;public const int NUCode
=12;internal const int Dot=13;
#endregion Kinds
public int Kind=None;public bool IsLazy=false;public Ast[]Exprs=null;public int Value='\0';public int[]Ranges;public int Min=0;public int Max=0;static
 FA[]_ToFAs(Ast[]asts,int match=0){var result=new FA[asts.Length];for(var i=0;i<result.Length;i++)result[i]=asts[i].ToFA(match);return result;}public FA
 ToFA(int match=0){Ast ast=this;if(ast.IsLazy)throw new NotSupportedException("The AST node cannot be lazy");switch(ast.Kind){case Ast.Alt:return FA.Or(_ToFAs(ast.Exprs,
match),match);case Ast.Cat:if(1==ast.Exprs.Length)return ast.Exprs[0].ToFA(match);return FA.Concat(_ToFAs(ast.Exprs,match),match);case Ast.Dot:return FA.Set(new
 int[]{0,0xd7ff,0xe000,0x10ffff},match);case Ast.Lit:return FA.Literal(new int[]{ast.Value},match);case Ast.NSet:var pairs=RangeUtility.ToPairs(ast.Ranges);
RangeUtility.NormalizeRangeList(pairs);var pairl=new List<KeyValuePair<int,int>>(RangeUtility.NotRanges(pairs));return FA.Set(RangeUtility.FromPairs(pairl),
match);case Ast.NUCode:pairs=RangeUtility.ToPairs(CharacterClasses.UnicodeCategories[ast.Value]);RangeUtility.NormalizeRangeList(pairs);pairl=new List<KeyValuePair<int,
int>>(RangeUtility.NotRanges(pairs));return FA.Set(RangeUtility.FromPairs(pairl),match);case Ast.Opt:return FA.Optional(ast.Exprs[0].ToFA(),match);case
 Ast.Plus:return FA.Repeat(ast.Exprs[0].ToFA(),1,0,match);case Ast.Rep:return FA.Repeat(ast.Exprs[0].ToFA(),ast.Min,ast.Max,match);case Ast.Set:return
 FA.Set(ast.Ranges,match);case Ast.Star:return FA.Repeat(ast.Exprs[0].ToFA(),0,0,match);case Ast.UCode:return FA.Set(CharacterClasses.UnicodeCategories[ast.Value],
match);default:throw new NotImplementedException();}}internal static Ast Parse(LexContext pc){Ast result=null,next=null;int ich;pc.EnsureStarted();while
(true){switch(pc.Current){case-1:return result;case'.':var dot=new Ast();dot.Kind=Ast.Dot;if(null==result)result=dot;else{if(Ast.Cat==result.Kind){var
 exprs=new Ast[result.Exprs.Length+1];Array.Copy(result.Exprs,0,exprs,0,result.Exprs.Length);exprs[exprs.Length-1]=dot;result.Exprs=exprs;}else{var cat
=new Ast();cat.Kind=Ast.Cat;cat.Exprs=new Ast[]{result,dot};result=cat;}}pc.Advance();result=_ParseModifier(result,pc);break;case'\\':pc.Advance();pc.Expecting();
var isNot=false;switch(pc.Current){case'P':isNot=true;goto case'p';case'p':pc.Advance();pc.Expecting('{');var uc=new StringBuilder();int uli=pc.Line;int
 uco=pc.Column;long upo=pc.Position;while(-1!=pc.Advance()&&'}'!=pc.Current)uc.Append((char)pc.Current);pc.Expecting('}');pc.Advance();int uci=0;switch(uc.ToString())
{case"Pe":uci=21;break;case"Pc":uci=19;break;case"Cc":uci=14;break;case"Sc":uci=26;break;case"Pd":uci=19;break;case"Nd":uci=8;break;case"Me":uci=7;break;
case"Pf":uci=23;break;case"Cf":uci=15;break;case"Pi":uci=22;break;case"Nl":uci=9;break;case"Zl":uci=12;break;case"Ll":uci=1;break;case"Sm":uci=25;break;
case"Lm":uci=3;break;case"Sk":uci=27;break;case"Mn":uci=5;break;case"Ps":uci=20;break;case"Lo":uci=4;break;case"Cn":uci=29;break;case"No":uci=10;break;
case"Po":uci=24;break;case"So":uci=28;break;case"Zp":uci=13;break;case"Co":uci=17;break;case"Zs":uci=11;break;case"Mc":uci=6;break;case"Cs":uci=16;break;
case"Lt":uci=2;break;case"Lu":uci=0;break;}next=new Ast();next.Value=uci;next.Kind=isNot?Ast.NUCode:Ast.UCode;break;case'd':next=new Ast();next.Kind=Ast.Set;
next.Ranges=new int[]{'0','9'};pc.Advance();break;case'D':next=new Ast();next.Kind=Ast.NSet;next.Ranges=new int[]{'0','9'};pc.Advance();break;case's':
next=new Ast();next.Kind=Ast.Set;next.Ranges=new int[]{'\t','\t',' ',' ','\r','\r','\n','\n','\f','\f'};pc.Advance();break;case'S':next=new Ast();next.Kind
=Ast.NSet;next.Ranges=new int[]{'\t','\t',' ',' ','\r','\r','\n','\n','\f','\f'};pc.Advance();break;case'w':next=new Ast();next.Kind=Ast.Set;next.Ranges
=new int[]{'_','_','0','9','A','Z','a','z',};pc.Advance();break;case'W':next=new Ast();next.Kind=Ast.NSet;next.Ranges=new int[]{'_','_','0','9','A','Z',
'a','z',};pc.Advance();break;default:if(-1!=(ich=_ParseEscapePart(pc))){next=new Ast();next.Kind=Ast.Lit;next.Value=ich;}else{pc.Expecting(); return null;
}break;}next=_ParseModifier(next,pc);if(null!=result){if(Ast.Cat==result.Kind){var exprs=new Ast[result.Exprs.Length+1];Array.Copy(result.Exprs,0,exprs,
0,result.Exprs.Length);exprs[exprs.Length-1]=next;result.Exprs=exprs;}else{var cat=new Ast();cat.Kind=Ast.Cat;cat.Exprs=new Ast[]{result,next};result=
cat;}}else result=next;break;case')':return result;case'(':pc.Advance();pc.Expecting();next=Parse(pc);pc.Expecting(')');pc.Advance();next=_ParseModifier(next,
pc);if(null==result)result=next;else{if(Ast.Cat==result.Kind){var exprs=new Ast[result.Exprs.Length+1];Array.Copy(result.Exprs,0,exprs,0,result.Exprs.Length);
exprs[exprs.Length-1]=next;result.Exprs=exprs;}else{var cat=new Ast();cat.Kind=Ast.Cat;cat.Exprs=new Ast[]{result,next};result=cat;}}break;case'|':if(-1
!=pc.Advance()){next=Parse(pc);if(null!=result&&Ast.Lit==result.Kind&&Ast.Lit==next.Kind){var set=new Ast();set.Kind=Set;set.Ranges=new int[]{result.Value,
result.Value,next.Value,next.Value};result=set;}else if(null!=result&&Ast.Lit==result.Kind&&Ast.Set==next.Kind){var set=new Ast();set.Kind=Ast.Set;set.Ranges
=new int[next.Ranges.Length+2];set.Ranges[0]=result.Value;set.Ranges[1]=result.Value;Array.Copy(next.Ranges,0,set.Ranges,2,next.Ranges.Length);result=
set;}else if(null!=result&&Ast.Alt==result.Kind){var exprs=new Ast[result.Exprs.Length+1];Array.Copy(result.Exprs,0,exprs,0,result.Exprs.Length);exprs[exprs.Length
-1]=next;result.Exprs=exprs;}else{var alt=new Ast();alt.Kind=Ast.Alt;if(null==next||next.Kind!=Alt){alt.Exprs=new Ast[]{result,next};result=alt;}else{
var exprs=new Ast[1+next.Exprs.Length];Array.Copy(next.Exprs,0,exprs,1,next.Exprs.Length);exprs[0]=result;alt.Exprs=exprs;result=alt;}}}else{var opt=new
 Ast();opt.Kind=Ast.Opt;opt.Exprs=new Ast[]{result};result=opt;}break;case'[':var seti=_ParseSet(pc);next=new Ast();next.Kind=(seti.Key)?NSet:Set;next.Ranges
=seti.Value;next=_ParseModifier(next,pc);if(null==result)result=next;else{if(Ast.Cat==result.Kind){var exprs=new Ast[result.Exprs.Length+1];Array.Copy(result.Exprs,
0,exprs,0,result.Exprs.Length);exprs[exprs.Length-1]=next;result.Exprs=exprs;}else{var cat=new Ast();cat.Kind=Ast.Cat;cat.Exprs=new Ast[]{result,next};
result=cat;}}break;default:ich=pc.Current;if(char.IsHighSurrogate((char)ich)){if(-1==pc.Advance())throw new ExpectingException("Expecting low surrogate in Unicode stream",
pc.Line,pc.Column,pc.Position,pc.FileOrUrl,"low-surrogate");ich=char.ConvertToUtf32((char)ich,(char)pc.Current);}next=new Ast();next.Kind=Ast.Lit;next.Value
=ich;pc.Advance();next=_ParseModifier(next,pc);if(null==result)result=next;else{if(Ast.Cat==result.Kind){var exprs=new Ast[result.Exprs.Length+1];Array.Copy(result.Exprs,
0,exprs,0,result.Exprs.Length);exprs[exprs.Length-1]=next;result.Exprs=exprs;}else{var cat=new Ast();cat.Kind=Ast.Cat;cat.Exprs=new Ast[]{result,next};
result=cat;}}break;}}}static KeyValuePair<bool,int[]>_ParseSet(LexContext pc){var result=new List<int>();pc.EnsureStarted();pc.Expecting('[');pc.Advance();
pc.Expecting();var isNot=false;if('^'==pc.Current){isNot=true;pc.Advance();pc.Expecting();}var firstRead=true;int firstChar='\0';var readFirstChar=false;
var wantRange=false;while(-1!=pc.Current&&(firstRead||']'!=pc.Current)){if(!wantRange){ if('['==pc.Current){pc.Advance();pc.Expecting();if(':'!=pc.Current)
{firstChar='[';readFirstChar=true;}else{pc.Advance();pc.Expecting();var ll=pc.CaptureBuffer.Length;if(!pc.TryReadUntil(':',false))throw new ExpectingException("Expecting character class",
pc.Line,pc.Column,pc.Position,pc.FileOrUrl);pc.Expecting(':');pc.Advance();pc.Expecting(']');pc.Advance();var cls=pc.GetCapture(ll);result.AddRange(Lex.GetCharacterClass(cls));
readFirstChar=false;wantRange=false;firstRead=false;continue;}}if(!readFirstChar){if(char.IsHighSurrogate((char)pc.Current)){var chh=(char)pc.Current;
pc.Advance();pc.Expecting();firstChar=char.ConvertToUtf32(chh,(char)pc.Current);pc.Advance();pc.Expecting();}else if('\\'==pc.Current){pc.Advance();firstChar
=_ParseRangeEscapePart(pc);}else{firstChar=pc.Current;pc.Advance();pc.Expecting();}readFirstChar=true;}else{if('-'==pc.Current){pc.Advance();pc.Expecting();
wantRange=true;}else{result.Add(firstChar);result.Add(firstChar);readFirstChar=false;}}firstRead=false;}else{if('\\'!=pc.Current){var ch=0;if(char.IsHighSurrogate((char)pc.Current))
{var chh=(char)pc.Current;pc.Advance();pc.Expecting();ch=char.ConvertToUtf32(chh,(char)pc.Current);}else ch=(char)pc.Current;pc.Advance();pc.Expecting();
result.Add(firstChar);result.Add(ch);}else{result.Add(firstChar);pc.Advance();result.Add(_ParseRangeEscapePart(pc));}wantRange=false;readFirstChar=false;
}}if(readFirstChar){result.Add(firstChar);result.Add(firstChar);if(wantRange){result.Add('-');result.Add('-');}}pc.Expecting(']');pc.Advance();return new
 KeyValuePair<bool,int[]>(isNot,result.ToArray());}static int[]_ParseRanges(LexContext pc){pc.EnsureStarted();var result=new List<int>();int[]next=null;
bool readDash=false;while(-1!=pc.Current&&']'!=pc.Current){switch(pc.Current){case'[': if(null!=next){result.Add(next[0]);result.Add(next[1]);if(readDash)
{result.Add('-');result.Add('-');}}pc.Advance();pc.Expecting(':');pc.Advance();var l=pc.CaptureBuffer.Length;var lin=pc.Line;var col=pc.Column;var pos
=pc.Position;pc.TryReadUntil(':',false);var n=pc.GetCapture(l);pc.Advance();pc.Expecting(']');pc.Advance();int[]rngs;if(!CharacterClasses.Known.TryGetValue(n,
out rngs)){var sa=new string[CharacterClasses.Known.Count];CharacterClasses.Known.Keys.CopyTo(sa,0);throw new ExpectingException("Invalid character class "
+n,lin,col,pos,pc.FileOrUrl,sa);}result.AddRange(rngs);readDash=false;next=null;break;case'\\':pc.Advance();pc.Expecting();switch(pc.Current){case'h':
_ParseCharClassEscape(pc,"space",result,ref next,ref readDash);break;case'd':_ParseCharClassEscape(pc,"digit",result,ref next,ref readDash);break;case
'D':_ParseCharClassEscape(pc,"^digit",result,ref next,ref readDash);break;case'l':_ParseCharClassEscape(pc,"lower",result,ref next,ref readDash);break;
case's':_ParseCharClassEscape(pc,"space",result,ref next,ref readDash);break;case'S':_ParseCharClassEscape(pc,"^space",result,ref next,ref readDash);break;
case'u':_ParseCharClassEscape(pc,"upper",result,ref next,ref readDash);break;case'w':_ParseCharClassEscape(pc,"word",result,ref next,ref readDash);break;
case'W':_ParseCharClassEscape(pc,"^word",result,ref next,ref readDash);break;default:var ch=(char)_ParseRangeEscapePart(pc);if(null==next)next=new int[]
{ch,ch};else if(readDash){result.Add(next[0]);result.Add(ch);next=null;readDash=false;}else{result.AddRange(next);next=new int[]{ch,ch};}break;}break;
case'-':pc.Advance();if(null==next){next=new int[]{'-','-'};readDash=false;}else{if(readDash)result.AddRange(next);readDash=true;}break;default:if(null
==next){next=new int[]{pc.Current,pc.Current};}else{if(readDash){result.Add(next[0]);result.Add((char)pc.Current);next=null;readDash=false;}else{result.AddRange(next);
next=new int[]{pc.Current,pc.Current};}}pc.Advance();break;}}if(null!=next){result.AddRange(next);if(readDash){result.Add('-');result.Add('-');}}return
 result.ToArray();}static void _ParseCharClassEscape(LexContext pc,string cls,List<int>result,ref int[]next,ref bool readDash){if(null!=next){result.AddRange(next);
if(readDash){result.Add('-');result.Add('-');}result.Add('-');result.Add('-');}pc.Advance();int[]rngs;if(!CharacterClasses.Known.TryGetValue(cls,out rngs))
{var sa=new string[CharacterClasses.Known.Count];CharacterClasses.Known.Keys.CopyTo(sa,0);throw new ExpectingException("Invalid character class "+cls,
pc.Line,pc.Column,pc.Position,pc.FileOrUrl,sa);}result.AddRange(rngs);next=null;readDash=false;}static Ast _ParseModifier(Ast expr,LexContext pc){var line
=pc.Line;var column=pc.Column;var position=pc.Position;switch(pc.Current){case'*':var rep=new Ast();rep.Kind=Ast.Star;rep.Exprs=new Ast[]{expr};expr=rep;
pc.Advance();if('?'==pc.Current){rep.IsLazy=true;pc.Advance();}break;case'+':rep=new Ast();rep.Kind=Ast.Plus;rep.Exprs=new Ast[]{expr};expr=rep;pc.Advance();
if('?'==pc.Current){rep.IsLazy=true;pc.Advance();}break;case'?':var opt=new Ast();opt.Kind=Ast.Opt;opt.Exprs=new Ast[]{expr};expr=opt;pc.Advance();if('?'
==pc.Current){opt.IsLazy=true;pc.Advance();}break;case'{':pc.Advance();pc.TrySkipWhiteSpace();pc.Expecting('0','1','2','3','4','5','6','7','8','9',',',
'}');var min=-1;var max=-1;if(','!=pc.Current&&'}'!=pc.Current){var l=pc.CaptureBuffer.Length;pc.TryReadDigits();min=int.Parse(pc.GetCapture(l));pc.TrySkipWhiteSpace();
}if(','==pc.Current){pc.Advance();pc.TrySkipWhiteSpace();pc.Expecting('0','1','2','3','4','5','6','7','8','9','}');if('}'!=pc.Current){var l=pc.CaptureBuffer.Length;
pc.TryReadDigits();max=int.Parse(pc.GetCapture(l));pc.TrySkipWhiteSpace();}}else{max=min;}pc.Expecting('}');pc.Advance();rep=new Ast();rep.Exprs=new Ast[]
{expr};rep.Kind=Ast.Rep;rep.Min=min;rep.Max=max;expr=rep;if('?'==pc.Current){rep.IsLazy=true;pc.Advance();}break;}return expr;}static byte _FromHexChar(char
 hex){if(':'>hex&&'/'<hex)return(byte)(hex-'0');if('G'>hex&&'@'<hex)return(byte)(hex-'7'); if('g'>hex&&'`'<hex)return(byte)(hex-'W'); throw new ArgumentException("The value was not hex.",
"hex");}static bool _IsHexChar(char hex){if(':'>hex&&'/'<hex)return true;if('G'>hex&&'@'<hex)return true;if('g'>hex&&'`'<hex)return true;return false;
} static int _ParseEscapePart(LexContext pc){if(-1==pc.Current)return-1;switch(pc.Current){case'f':pc.Advance();return'\f';case'v':pc.Advance();return
'\v';case't':pc.Advance();return'\t';case'n':pc.Advance();return'\n';case'r':pc.Advance();return'\r';case'x':if(-1==pc.Advance()||!_IsHexChar((char)pc.Current))
return'x';byte b=_FromHexChar((char)pc.Current);if(-1==pc.Advance()||!_IsHexChar((char)pc.Current))return unchecked((char)b);b<<=4;b|=_FromHexChar((char)pc.Current);
if(-1==pc.Advance()||!_IsHexChar((char)pc.Current))return unchecked((char)b);b<<=4;b|=_FromHexChar((char)pc.Current);if(-1==pc.Advance()||!_IsHexChar((char)pc.Current))
return unchecked((char)b);b<<=4;b|=_FromHexChar((char)pc.Current);return unchecked((char)b);case'u':if(-1==pc.Advance())return'u';ushort u=_FromHexChar((char)pc.Current);
u<<=4;if(-1==pc.Advance())return unchecked((char)u);u|=_FromHexChar((char)pc.Current);u<<=4;if(-1==pc.Advance())return unchecked((char)u);u|=_FromHexChar((char)pc.Current);
u<<=4;if(-1==pc.Advance())return unchecked((char)u);u|=_FromHexChar((char)pc.Current);return unchecked((char)u);default:int i=pc.Current;pc.Advance();
if(char.IsHighSurrogate((char)i)){i=char.ConvertToUtf32((char)i,(char)pc.Current);pc.Advance();}return(char)i;}}static int _ParseRangeEscapePart(LexContext
 pc){if(-1==pc.Current)return-1;switch(pc.Current){case'f':pc.Advance();return'\f';case'v':pc.Advance();return'\v';case't':pc.Advance();return'\t';case
'n':pc.Advance();return'\n';case'r':pc.Advance();return'\r';case'x':if(-1==pc.Advance()||!_IsHexChar((char)pc.Current))return'x';byte b=_FromHexChar((char)pc.Current);
if(-1==pc.Advance()||!_IsHexChar((char)pc.Current))return unchecked((char)b);b<<=4;b|=_FromHexChar((char)pc.Current);if(-1==pc.Advance()||!_IsHexChar((char)pc.Current))
return unchecked((char)b);b<<=4;b|=_FromHexChar((char)pc.Current);if(-1==pc.Advance()||!_IsHexChar((char)pc.Current))return unchecked((char)b);b<<=4;b
|=_FromHexChar((char)pc.Current);return unchecked((char)b);case'u':if(-1==pc.Advance())return'u';ushort u=_FromHexChar((char)pc.Current);u<<=4;if(-1==
pc.Advance())return unchecked((char)u);u|=_FromHexChar((char)pc.Current);u<<=4;if(-1==pc.Advance())return unchecked((char)u);u|=_FromHexChar((char)pc.Current);
u<<=4;if(-1==pc.Advance())return unchecked((char)u);u|=_FromHexChar((char)pc.Current);return unchecked((char)u);default:int i=pc.Current;pc.Advance();
if(char.IsHighSurrogate((char)i)){i=char.ConvertToUtf32((char)i,(char)pc.Current);pc.Advance();}return(char)i;}}}}namespace L{static class Compiler{
#region Opcodes
internal const int Match=1; internal const int Jmp=2; internal const int Switch=3; internal const int Any=4; internal const int Char=5; internal const
 int Set=6; internal const int NSet=7; internal const int UCode=8; internal const int NUCode=9; internal const int Save=10;
#endregion
internal static List<int[]>Emit(Ast ast,int symbolId=-1){var prog=new List<int[]>();EmitPart(ast,prog);if(-1!=symbolId){var match=new int[2];match[0]=
Match;match[1]=symbolId;prog.Add(match);}return prog;}internal static void EmitPart(string literal,IList<int[]>prog){for(var i=0;i<literal.Length;++i)
{int ch=literal[i];if(char.IsHighSurrogate(literal[i])){if(i==literal.Length-1)throw new ArgumentException("The literal contains an incomplete unicode surrogate.",
nameof(literal));ch=char.ConvertToUtf32(literal,i);++i;}var lit=new int[2];lit[0]=Char;lit[1]=ch;prog.Add(lit);}}internal static void EmitPart(Ast ast,
IList<int[]>prog){int[]inst,jmp;switch(ast.Kind){case Ast.Lit: inst=new int[2];inst[0]=Char;inst[1]=ast.Value;prog.Add(inst);break;case Ast.Cat: for(var
 i=0;i<ast.Exprs.Length;i++)if(null!=ast.Exprs[i])EmitPart(ast.Exprs[i],prog);break;case Ast.Dot: inst=new int[1];inst[0]=Any;prog.Add(inst);break;case
 Ast.Alt: var exprs=new List<Ast>(ast.Exprs.Length);var firstNull=-1;for(var i=0;i<ast.Exprs.Length;i++){var e=ast.Exprs[i];if(null==e){if(0>firstNull)
{firstNull=i;exprs.Add(null);}continue;}exprs.Add(e);}ast.Exprs=exprs.ToArray();var jjmp=new int[ast.Exprs.Length+1];jjmp[0]=Jmp;prog.Add(jjmp);var jmpfixes
=new List<int>(ast.Exprs.Length-1);for(var i=0;i<ast.Exprs.Length;++i){var e=ast.Exprs[i];if(null!=e){jjmp[i+1]=prog.Count;EmitPart(e,prog);if(i==ast.Exprs.Length
-1)continue;if(i==ast.Exprs.Length-2&&null==ast.Exprs[i+1])continue;var j=new int[2];j[0]=Jmp;jmpfixes.Add(prog.Count);prog.Add(j);}}for(int ic=jmpfixes.Count,
i=0;i<ic;++i){var j=prog[jmpfixes[i]];j[1]=prog.Count;}if(-1<firstNull){jjmp[firstNull+1]=prog.Count;}break;case Ast.NSet:case Ast.Set: inst=new int[ast.Ranges.Length
+1];inst[0]=(ast.Kind==Ast.Set)?Set:NSet;SortRanges(ast.Ranges);Array.Copy(ast.Ranges,0,inst,1,ast.Ranges.Length);prog.Add(inst);break;case Ast.NUCode:
case Ast.UCode: inst=new int[2];inst[0]=(ast.Kind==Ast.UCode)?UCode:NUCode;inst[1]=ast.Value;prog.Add(inst);break;case Ast.Opt:inst=new int[3]; inst[0]
=Jmp;prog.Add(inst);inst[1]=prog.Count; for(var i=0;i<ast.Exprs.Length;i++)if(null!=ast.Exprs[i])EmitPart(ast.Exprs[i],prog);inst[2]=prog.Count;if(ast.IsLazy)
{ var t=inst[1];inst[1]=inst[2];inst[2]=t;}break; case Ast.Star:ast.Min=0;ast.Max=0;goto case Ast.Rep;case Ast.Plus:ast.Min=1;ast.Max=0;goto case Ast.Rep;
case Ast.Rep: if(ast.Min>0&&ast.Max>0&&ast.Min>ast.Max)throw new ArgumentOutOfRangeException("Max");int idx;Ast opt;Ast rep;switch(ast.Min){case-1:case
 0:switch(ast.Max){ case-1:case 0:idx=prog.Count;inst=new int[3];inst[0]=Jmp;prog.Add(inst);inst[1]=prog.Count;for(var i=0;i<ast.Exprs.Length;i++)if(null
!=ast.Exprs[i])EmitPart(ast.Exprs[i],prog);jmp=new int[2];jmp[0]=Jmp;jmp[1]=idx;prog.Add(jmp);inst[2]=prog.Count;if(ast.IsLazy){ var t=inst[1];inst[1]
=inst[2];inst[2]=t;}return; case 1:opt=new Ast();opt.Kind=Ast.Opt;opt.Exprs=ast.Exprs;opt.IsLazy=ast.IsLazy;EmitPart(opt,prog);return;default: opt=new
 Ast();opt.Kind=Ast.Opt;opt.Exprs=ast.Exprs;opt.IsLazy=ast.IsLazy;EmitPart(opt,prog);for(var i=1;i<ast.Max;++i){EmitPart(opt,prog);}return;}case 1:switch
(ast.Max){ case-1:case 0:idx=prog.Count;for(var i=0;i<ast.Exprs.Length;i++)if(null!=ast.Exprs[i])EmitPart(ast.Exprs[i],prog);inst=new int[3];inst[0]=Jmp;
prog.Add(inst);inst[1]=idx;inst[2]=prog.Count;if(ast.IsLazy){ var t=inst[1];inst[1]=inst[2];inst[2]=t;}return;case 1: for(var i=0;i<ast.Exprs.Length;i++)
if(null!=ast.Exprs[i])EmitPart(ast.Exprs[i],prog);return;default: rep=new Ast();rep.Min=0;rep.Max=ast.Max-1;rep.IsLazy=ast.IsLazy;rep.Exprs=ast.Exprs;
for(var i=0;i<ast.Exprs.Length;i++)if(null!=ast.Exprs[i])EmitPart(ast.Exprs[i],prog);EmitPart(rep,prog);return;}default: switch(ast.Max){ case-1:case 0:
for(var j=0;j<ast.Min;++j){for(var i=0;i<ast.Exprs.Length;i++)if(null!=ast.Exprs[i])EmitPart(ast.Exprs[i],prog);}rep=new Ast();rep.Kind=Ast.Star;rep.Exprs
=ast.Exprs;rep.IsLazy=ast.IsLazy;EmitPart(rep,prog);return;case 1: throw new NotImplementedException();default: for(var j=0;j<ast.Min;++j){for(var i=0;
i<ast.Exprs.Length;i++)if(null!=ast.Exprs[i])EmitPart(ast.Exprs[i],prog);}if(ast.Min==ast.Max)return;opt=new Ast();opt.Kind=Ast.Opt;opt.Exprs=ast.Exprs;
opt.IsLazy=ast.IsLazy;rep=new Ast();rep.Kind=Ast.Rep;rep.Min=rep.Max=ast.Max-ast.Min;EmitPart(rep,prog);return;}} throw new NotImplementedException();
}}internal static void EmitPart(FA fa,IList<int[]>prog){ fa=fa.ToDfa();fa.TrimDuplicates();fa=fa.ToGnfa();fa.TrimNeutrals();if(fa.IsNeutral){foreach(var
 efa in fa.EpsilonTransitions){fa=efa;}}var rendered=new Dictionary<FA,int>();var swFixups=new Dictionary<FA,int>();var jmpFixups=new Dictionary<FA,int>();
var l=new List<FA>();fa.FillClosure(l); var fas=fa.FirstAcceptingState;var afai=l.IndexOf(fas);l.RemoveAt(afai);l.Add(fas);for(int ic=l.Count,i=0;i<ic;++i)
{var cfa=l[i];rendered.Add(cfa,prog.Count);if(!cfa.IsFinal){int swfixup=prog.Count;prog.Add(null);swFixups.Add(cfa,swfixup);}}for(int ic=l.Count,i=0;i<ic;++i)
{var cfa=l[i];if(!cfa.IsFinal){var sw=new List<int>();sw.Add(Switch);int[]simple=null;if(1==cfa.InputTransitions.Count&&0==cfa.EpsilonTransitions.Count)
{foreach(var trns in cfa.InputTransitions){if(l.IndexOf(trns.Value)==i+1){simple=new int[]{trns.Key.Key,trns.Key.Value};break;}}}if(null!=simple){if(2
<simple.Length||simple[0]!=simple[1]){sw[0]=Set;sw.AddRange(simple);}else{sw[0]=Char;sw.Add(simple[0]);}}else{var rngGrps=cfa.FillInputTransitionRangesGroupedByState();
foreach(var grp in rngGrps){var dst=rendered[grp.Key];sw.AddRange(grp.Value);sw.Add(-1);sw.Add(dst);}}prog[swFixups[cfa]]=sw.ToArray();}var jfi=-1;if(jmpFixups.TryGetValue(cfa,
out jfi)){var jmp=new int[2];jmp[0]=Jmp;jmp[1]=prog.Count;prog[jfi]=jmp;}}}static void _EmitPart(FA fa,IDictionary<FA,int>rendered,IList<int[]>prog){if
(fa.IsFinal)return;int swfixup=prog.Count;var sw=new List<int>();sw.Add(Switch);prog.Add(null);foreach(var trns in fa.InputTransitions){var dst=-1;if(!rendered.TryGetValue(trns.Value,out
 dst)){dst=prog.Count;rendered.Add(trns.Value,dst);_EmitPart(trns.Value,rendered,prog);}sw.Add(trns.Key.Key);sw.Add(trns.Key.Value);sw.Add(-1);sw.Add(dst);
}if(0<fa.InputTransitions.Count&&0<fa.EpsilonTransitions.Count)sw.Add(-2);else if(0==fa.InputTransitions.Count)sw[0]=Jmp;foreach(var efa in fa.EpsilonTransitions)
{var dst=-1;if(!rendered.TryGetValue(efa,out dst)){dst=prog.Count;rendered.Add(efa,dst);_EmitPart(efa,rendered,prog);}sw.Add(dst);}prog[swfixup]=sw.ToArray();
}static string _FmtLbl(int i){return string.Format("L{0,4:000#}",i);}public static string ToString(IEnumerable<int[]>prog){var sb=new StringBuilder();
var i=0;foreach(var inst in prog){sb.Append(_FmtLbl(i));sb.Append(": ");sb.AppendLine(ToString(inst));++i;}return sb.ToString();}static string _ToStr(int
 ch){return string.Concat('\"',_EscChar(ch),'\"');}static string _EscChar(int ch){switch(ch){case'.':case'/': case'(':case')':case'[':case']':case'<':
 case'>':case'|':case';': case'\'': case'\"':case'{':case'}':case'?':case'*':case'+':case'$':case'^':case'\\':return"\\"+char.ConvertFromUtf32(ch);case
'\t':return"\\t";case'\n':return"\\n";case'\r':return"\\r";case'\0':return"\\0";case'\f':return"\\f";case'\v':return"\\v";case'\b':return"\\b";default:
var s=char.ConvertFromUtf32(ch);if(!char.IsLetterOrDigit(s,0)&&!char.IsSeparator(s,0)&&!char.IsPunctuation(s,0)&&!char.IsSymbol(s,0)){if(1==s.Length)return
 string.Concat(@"\u",unchecked((ushort)ch).ToString("x4"));else return string.Concat(@"\U"+ch.ToString("x8"));}else return s;}}static int _AppendRanges(StringBuilder
 sb,int[]inst,int index){var i=index;for(i=index;i<inst.Length-1;i++){if(-1==inst[i])return i;if(index!=i)sb.Append(", ");if(inst[i]==inst[i+1])sb.Append(_ToStr(inst[i]));
else{sb.Append(_ToStr(inst[i]));sb.Append("..");sb.Append(_ToStr(inst[i+1]));}++i;}return i;}public static string ToString(int[]inst){switch(inst[0]){
case Jmp:var sb=new StringBuilder();sb.Append("jmp ");sb.Append(_FmtLbl(inst[1]));for(var i=2;i<inst.Length;i++)sb.Append(", "+_FmtLbl(inst[i]));return
 sb.ToString();case Switch:sb=new StringBuilder();sb.Append("switch ");var j=1;for(;j<inst.Length;){if(-2==inst[j])break;if(j!=1)sb.Append(", ");sb.Append("case ");
j=_AppendRanges(sb,inst,j);++j;sb.Append(":");sb.Append(_FmtLbl(inst[j]));++j;}if(j<inst.Length&&-2==inst[j]){sb.Append(", default:");var delim="";for(++j;j<inst.Length;j++)
{sb.Append(delim);sb.Append(_FmtLbl(inst[j]));delim=", ";}}return sb.ToString();case Char:if(2==inst.Length) return"char "+_ToStr(inst[1]);else return
"char";case UCode:case NUCode:return(UCode==inst[0]?"ucode ":"nucode ")+inst[1];case Set:case NSet:sb=new StringBuilder();if(Set==inst[0])sb.Append("set ");
else sb.Append("nset ");for(var i=1;i<inst.Length-1;i++){if(1!=i)sb.Append(", ");if(inst[i]==inst[i+1])sb.Append(_ToStr(inst[i]));else{sb.Append(_ToStr(inst[i]));
sb.Append("..");sb.Append(_ToStr(inst[i+1]));}++i;}return sb.ToString();case Any:return"any";case Match:return"match "+inst[1].ToString();case Save:return
"save "+inst[1].ToString();default:throw new InvalidProgramException("The instruction is not valid");}}internal static int[][]EmitLexer(bool optimize,params
 Ast[]expressions){var parts=new KeyValuePair<int,int[][]>[expressions.Length];for(var i=0;i<expressions.Length;++i){var l=new List<int[]>();FA fa=null;
if(optimize){try{fa=expressions[i].ToFA(i);} catch(NotSupportedException){}} if(null!=fa){EmitPart(fa,l);}else{EmitPart(expressions[i],l);}parts[i]=new
 KeyValuePair<int,int[][]>(i,l.ToArray());}var result=EmitLexer(parts);if(optimize){result=_RemoveDeadCode(result);}return result;}static int[][]_RemoveDeadCode(int[][]
prog){var done=false;while(!done){done=true;var toRemove=-1;for(var i=0;i<prog.Length;++i){var pc=prog[i]; if(Jmp==pc[0]&&i+1==pc[1]&&2==pc.Length){toRemove
=i;break;}}if(-1!=toRemove){done=false;var newProg=new List<int[]>(prog.Length-1);for(var i=0;i<toRemove;++i){var inst=prog[i];switch(inst[0]){case Switch:
var inDef=false;for(var j=0;j<inst.Length;j++){if(inDef){if(inst[j]>toRemove)--inst[j];}else{if(-1==inst[j]){++j;if(inst[j]>toRemove)--inst[j];}else if
(-2==inst[j])inDef=true;}}break;case Jmp:for(var j=1;j<inst.Length;j++)if(inst[j]>toRemove)--inst[j];break;}newProg.Add(prog[i]);}var progNext=new List<int[]>(prog.Length
-toRemove-1);for(var i=toRemove+1;i<prog.Length;i++){progNext.Add(prog[i]);}var pna=progNext.ToArray();Fixup(pna,-1);newProg.AddRange(pna);prog=newProg.ToArray();
}}return prog;}internal static int[][]EmitLexer(IEnumerable<KeyValuePair<int,int[][]>>parts){var l=new List<KeyValuePair<int,int[][]>>(parts);var prog
=new List<int[]>();int[]match,save; save=new int[2];save[0]=Save;save[1]=0;prog.Add(save); var jmp=new int[l.Count+2];jmp[0]=Compiler.Jmp;prog.Add(jmp);
 for(int ic=l.Count,i=0;i<ic;++i){jmp[i+1]=prog.Count; Fixup(l[i].Value,prog.Count);prog.AddRange(l[i].Value); save=new int[2];save[0]=Save;save[1]=1;
prog.Add(save); match=new int[2];match[0]=Match;match[1]=l[i].Key;prog.Add(match);} jmp[jmp.Length-1]=prog.Count; var any=new int[1];any[0]=Any;prog.Add(any);
 save=new int[2];save[0]=Save;save[1]=1;prog.Add(save); match=new int[2];match[0]=Match;match[1]=-1;prog.Add(match);return prog.ToArray();}internal static
 void SortRanges(int[]ranges){var result=new List<KeyValuePair<int,int>>(ranges.Length/2);for(var i=0;i<ranges.Length-1;++i){var ch=ranges[i];++i;result.Add(new
 KeyValuePair<int,int>(ch,ranges[i]));}result.Sort((x,y)=>{return x.Key.CompareTo(y.Key);});for(int ic=result.Count,i=0;i<ic;++i){var j=i*2;var kvp=result[i];
ranges[j]=kvp.Key;ranges[j+1]=kvp.Value;}}static int[]_GetFirsts(int[][]part,int index){if(part.Length<=index)return new int[0];int idx;List<int>resl;
int[]result;var pc=part[index];switch(pc[0]){case Char:return new int[]{pc[1],pc[1]};case Set:result=new int[pc.Length-1];Array.Copy(pc,1,result,0,result.Length);
return result;case NSet:result=new int[pc.Length-1];Array.Copy(pc,1,result,0,result.Length);return RangeUtility.FromPairs(new List<KeyValuePair<int,int>>(RangeUtility.NotRanges(RangeUtility.ToPairs(result))));
case Any:return new int[]{0,0x10ffff};case UCode:result=CharacterClasses.UnicodeCategories[pc[1]];return result;case NUCode:result=CharacterClasses.UnicodeCategories[pc[1]];
Array.Copy(pc,1,result,0,result.Length);return RangeUtility.FromPairs(new List<KeyValuePair<int,int>>(RangeUtility.NotRanges(RangeUtility.ToPairs(result))));
case Switch:resl=new List<int>();idx=1;while(pc.Length>idx&&-2!=pc[idx]){if(-1==pc[idx]){idx+=2;continue;}resl.Add(pc[idx]);}if(pc.Length>idx&&-2==pc[idx])
{++idx;while(pc.Length>idx){resl.AddRange(_GetFirsts(part,pc[idx]));++idx;}}return resl.ToArray();case Jmp:resl=new List<int>();idx=1;while(pc.Length>
idx){resl.AddRange(_GetFirsts(part,pc[idx]));++idx;}return resl.ToArray();case Match:return new int[0];case Save:return _GetFirsts(part,index+1);} throw
 new NotImplementedException();}internal static void Fixup(int[][]program,int offset){for(var i=0;i<program.Length;i++){var inst=program[i];var op=inst[0];
switch(op){case Switch:var inDef=false;for(var j=0;j<inst.Length;j++){if(inDef){inst[j]+=offset;}else{if(-1==inst[j]){++j;inst[j]+=offset;}else if(-2==
inst[j])inDef=true;}}break;case Jmp:for(var j=1;j<inst.Length;j++)inst[j]+=offset;break;}}}}}namespace L{/// <summary>
/// Provides services for assembling and disassembling lexers, and for compiling regular expressions into lexers
/// </summary>
#if LLIB
public
#endif
static class Lex{public static void RenderOptimizedExecutionGraph(string expression,string filename){RenderOptimizedExecutionGraph(LexContext.Create(expression),
filename);}public static void RenderOptimizedExecutionGraph(LexContext expression,string filename){var ast=Ast.Parse(expression);var fa=ast.ToFA();fa.TrimNeutrals();
fa.RenderToFile(filename);}public static int[][]FinalizePart(int[][]part,int match=0){var result=new List<int[]>(part.Length+3);var inst=new int[2];inst[0]
=Compiler.Save;inst[1]=0;result.Add(inst);Compiler.Fixup(part,result.Count);result.AddRange(part);inst=new int[2];inst[0]=Compiler.Save;inst[1]=1;result.Add(inst);
inst=new int[2];inst[0]=Compiler.Match;inst[1]=match;result.Add(inst);return result.ToArray();}public static int[]GetCharacterClass(string name){if(null
==name)throw new ArgumentNullException(nameof(name));if(0==name.Length)throw new ArgumentException("The character class name must not be empty.",nameof(name));
int[]result;if(!CharacterClasses.Known.TryGetValue(name,out result))throw new ArgumentException("The character class "+name+" was not found",nameof(name));
return result;}/// <summary>
/// Assembles the assembly code into a program
/// </summary>
/// <param name="asmCode">The code to assemble</param>
/// <returns>A program</returns>
public static int[][]Assemble(LexContext asmCode){return Assembler.Emit(Assembler.Parse(asmCode)).ToArray();}/// <summary>
/// Assembles the assembly code into a program
/// </summary>
/// <param name="asmCode">The code to assemble</param>
/// <returns>A program</returns>
public static int[][]Assemble(string asmCode){var lc=LexContext.Create(asmCode);return Assembler.Emit(Assembler.Parse(lc)).ToArray();}/// <summary>
/// Assembles the assembly code from the <see cref="TextReader"/>
/// </summary>
/// <param name="asmCodeReader">A reader that will read the assembly code</param>
/// <returns>A program</returns>
public static int[][]AssembleFrom(TextReader asmCodeReader){var lc=LexContext.CreateFrom(asmCodeReader);return Assembler.Emit(Assembler.Parse(lc)).ToArray();
}/// <summary>
/// Assembles the assembly code from the specified file
/// </summary>
/// <param name="asmFile">A file containing the assembly code</param>
/// <returns>A program</returns>
public static int[][]AssembleFrom(string asmFile){using(var lc=LexContext.CreateFrom(asmFile))return Assembler.Emit(Assembler.Parse(lc)).ToArray();}/// <summary>
/// Assembles the assembly code from the specified url
/// </summary>
/// <param name="asmUrl">An URL that points to the assembly code</param>
/// <returns>A program</returns>
public static int[][]AssembleFromUrl(string asmUrl){using(var lc=LexContext.CreateFromUrl(asmUrl))return Assembler.Emit(Assembler.Parse(lc)).ToArray();
}/// <summary>
/// Compiles a single regular expression into a program segment
/// </summary>
/// <param name="input">The expression to compile</param>
/// <param name="optimize">Indicates whether or not to optimize the code</param>
/// <returns>A part of a program</returns>
public static int[][]CompileRegexPart(LexContext input,bool optimize=true){var ast=Ast.Parse(input);var prog=new List<int[]>();FA fa=null;if(optimize)
{try{fa=ast.ToFA();} catch(NotSupportedException){}if(null!=fa){Compiler.EmitPart(fa,prog);return prog.ToArray();}}Compiler.EmitPart(ast,prog);return prog.ToArray();
}/// <summary>
/// Compiles a single regular expression into a program segment
/// </summary>
/// <param name="expression">The expression to compile</param>
/// <param name="optimize">Indicates whether or not to optimize the output</param>
/// <returns>A part of a program</returns>
public static int[][]CompileRegexPart(string expression,bool optimize=true){return CompileRegexPart(LexContext.Create(expression),optimize);}/// <summary>
/// Compiles a single literal expression into a program segment
/// </summary>
/// <param name="input">The expression to compile</param>
/// <returns>A part of a program</returns>
public static int[][]CompileLiteralPart(LexContext input){var ll=input.CaptureBuffer.Length;while(-1!=input.Current)input.Capture();return CompileLiteralPart(input.GetCapture(ll));
}/// <summary>
/// Compiles a single literal expression into a program segment
/// </summary>
/// <param name="expression">The expression to compile</param>
/// <returns>A part of a program</returns>
public static int[][]CompileLiteralPart(string expression){var prog=new List<int[]>();Compiler.EmitPart(expression,prog);return prog.ToArray();}/// <summary>
/// Compiles a series of regular expressions into a program
/// </summary>
/// <param name="expressions">The expressions</param>
/// <param name="optimize">True to generate optimized code, false to use the standard generator</param>
/// <returns>A program</returns>
public static int[][]CompileLexerRegex(bool optimize,params string[]expressions){var asts=new Ast[expressions.Length];for(var i=0;i<expressions.Length;++i)
asts[i]=Ast.Parse(LexContext.Create(expressions[i]));return Compiler.EmitLexer(optimize,asts);}/// <summary>
/// Links a series of partial programs together into single lexer program
/// </summary>
/// <param name="parts">The parts</param>
/// <returns>A program</returns>
public static int[][]LinkLexerParts(bool optimize,IEnumerable<KeyValuePair<int,int[][]>>parts){return Compiler.EmitLexer(parts);}/// <summary>
/// Disassembles the specified program
/// </summary>
/// <param name="program">The program</param>
/// <returns>A string containing the assembly code for the program</returns>
public static string Disassemble(int[][]program){return Compiler.ToString(program);}/// <summary>
/// Indicates whether or not the program matches the entire input specified
/// </summary>
/// <param name="prog">The program</param>
/// <param name="input">The input to check</param>
/// <returns>True if the input was matched, otherwise false</returns>
public static bool IsMatch(int[][]prog,LexContext input){return-1!=Run(prog,input)&&input.Current==LexContext.EndOfInput;}/// <summary>
/// Indicates whether or not the program matches the entire input specified
/// </summary>
/// <param name="prog">The program</param>
/// <param name="input">The input to check</param>
/// <returns>True if the input was matched, otherwise false</returns>
public static bool IsMatch(int[][]prog,string input){return IsMatch(prog,LexContext.Create(input));}/// <summary>
/// Runs the specified program over the specified input
/// </summary>
/// <param name="prog">The program to run</param>
/// <param name="input">The input to match</param>
/// <returns>The id of the match, or -1 for an error. <see cref="LexContext.CaptureBuffer"/> contains the captured value.</returns>
public static int Run(int[][]prog,LexContext input){input.EnsureStarted();int i,match=-1;_Fiber[]currentFibers,nextFibers,tmp;int currentFiberCount=0,
nextFiberCount=0;int[]pc; int sp=0; var sb=new StringBuilder(64);int[]saved,matched;saved=new int[2];currentFibers=new _Fiber[prog.Length];nextFibers=
new _Fiber[prog.Length];_EnqueueFiber(ref currentFiberCount,ref currentFibers,new _Fiber(prog,0,saved),0);matched=null;var cur=-1;if(LexContext.EndOfInput
!=input.Current){var ch1=unchecked((char)input.Current);if(char.IsHighSurrogate(ch1)){if(-1==input.Advance())throw new ExpectingException("Expecting low surrogate in unicode stream. The input source is corrupt or not valid Unicode",
input.Line,input.Column,input.Position,input.FileOrUrl);var ch2=unchecked((char)input.Current);cur=char.ConvertToUtf32(ch1,ch2);}else cur=ch1;}while(0<currentFiberCount)
{bool passed=false;for(i=0;i<currentFiberCount;++i){var t=currentFibers[i];pc=t.Program[t.Index];saved=t.Saved;switch(pc[0]){case Compiler.Switch:var idx
=1;while(idx<pc.Length&&-2<pc[idx]){if(_InRanges(pc,ref idx,cur)){while(-1!=pc[idx])++idx;++idx;passed=true;_EnqueueFiber(ref nextFiberCount,ref nextFibers,
new _Fiber(t,pc[idx],saved),sp+1);idx=pc.Length;break;}else{while(-1!=pc[idx])++idx;++idx;}++idx;}if(idx<pc.Length&&-2==pc[idx]){++idx;while(idx<pc.Length)
{_EnqueueFiber(ref currentFiberCount,ref currentFibers,new _Fiber(t,pc[idx],saved),sp);++idx;}}break;case Compiler.Char:if(cur!=pc[1]){break;}goto case
 Compiler.Any;case Compiler.Set:idx=1;if(!_InRanges(pc,ref idx,cur)){break;}goto case Compiler.Any;case Compiler.NSet:idx=1;if(_InRanges(pc,ref idx,cur))
{break;}goto case Compiler.Any;case Compiler.UCode:var str=char.ConvertFromUtf32(cur);if(unchecked((int)char.GetUnicodeCategory(str,0)!=pc[1])){break;
}goto case Compiler.Any;case Compiler.NUCode:str=char.ConvertFromUtf32(cur);if(unchecked((int)char.GetUnicodeCategory(str,0))==pc[1]){break;}goto case
 Compiler.Any;case Compiler.Any:if(LexContext.EndOfInput==input.Current){break;}passed=true;_EnqueueFiber(ref nextFiberCount,ref nextFibers,new _Fiber(t,
t.Index+1,saved),sp+1);break;case Compiler.Match:matched=saved;match=pc[1]; i=currentFiberCount;break;}}if(passed){sb.Append(char.ConvertFromUtf32(cur));
input.Advance();if(LexContext.EndOfInput!=input.Current){var ch1=unchecked((char)input.Current);if(char.IsHighSurrogate(ch1)){input.Advance();if(-1==input.Advance())
throw new ExpectingException("Expecting low surrogate in unicode stream. The input source is corrupt or not valid Unicode",input.Line,input.Column,input.Position,
input.FileOrUrl);++sp;var ch2=unchecked((char)input.Current);cur=char.ConvertToUtf32(ch1,ch2);}else cur=ch1;}else cur=-1;++sp;}tmp=currentFibers;currentFibers
=nextFibers;nextFibers=tmp;currentFiberCount=nextFiberCount;nextFiberCount=0;}if(null!=matched){var start=matched[0]; var len=matched[1];input.CaptureBuffer.Append(sb.ToString(start,
len-start));return match;};return-1;}/// <summary>
/// Runs the specified program over the specified input, logging the run to <paramref name="log"/>
/// </summary>
/// <param name="prog">The program to run</param>
/// <param name="input">The input to match</param>
/// <param name="log">The log to output to</param>
/// <returns>The id of the match, or -1 for an error. <see cref="LexContext.CaptureBuffer"/> contains the captured value.</returns>
public static LexStatistics RunWithLoggingAndStatistics(int[][]prog,LexContext input,TextWriter log,out int result){ input.EnsureStarted();int i,match
=-1;int passes=0;int maxFiberCount=0;_Fiber[]currentFibers,nextFibers,tmp;int currentFiberCount=0,nextFiberCount=0;int[]pc; int sp=0; var sb=new StringBuilder(64);
int[]saved,matched;saved=new int[2];currentFibers=new _Fiber[prog.Length];nextFibers=new _Fiber[prog.Length];_EnqueueFiber(ref currentFiberCount,ref currentFibers,
new _Fiber(prog,0,saved),0);if(currentFiberCount>maxFiberCount)maxFiberCount=currentFiberCount;matched=null;var cur=-1;if(LexContext.EndOfInput!=input.Current)
{var ch1=unchecked((char)input.Current);if(char.IsHighSurrogate(ch1)){if(-1==input.Advance())throw new ExpectingException("Expecting low surrogate in unicode stream. The input source is corrupt or not valid Unicode",
input.Line,input.Column,input.Position,input.FileOrUrl);var ch2=unchecked((char)input.Current);cur=char.ConvertToUtf32(ch1,ch2);}else cur=ch1;}else cur
=-1;while(0<currentFiberCount){bool passed=false;for(i=0;i<currentFiberCount;++i){var lpassed=false;var shouldLog=false;var t=currentFibers[i];pc=t.Program[t.Index];
saved=t.Saved;switch(pc[0]){case Compiler.Switch:var idx=1;shouldLog=true;while(idx<pc.Length&&-2<pc[idx]){if(_InRanges(pc,ref idx,cur)){while(-1!=pc[idx])
++idx;++idx;lpassed=true;passed=true;_EnqueueFiber(ref nextFiberCount,ref nextFibers,new _Fiber(t,pc[idx],saved),sp+1);idx=pc.Length;break;}else{while
(-1!=pc[idx])++idx;++idx;}++idx;}if(idx<pc.Length&&-2==pc[idx]){++idx;while(pc.Length>idx){_EnqueueFiber(ref currentFiberCount,ref currentFibers,new _Fiber(t,
pc[idx],saved),sp);if(currentFiberCount>maxFiberCount)maxFiberCount=currentFiberCount;++idx;}}break;case Compiler.Char:shouldLog=true;if(cur!=pc[1]){break;
}goto case Compiler.Any;case Compiler.Set:shouldLog=true;idx=1;if(!_InRanges(pc,ref idx,cur)){break;}goto case Compiler.Any;case Compiler.NSet:shouldLog
=true;idx=1;if(_InRanges(pc,ref idx,cur)){break;}goto case Compiler.Any;case Compiler.UCode:shouldLog=true;var str=char.ConvertFromUtf32(cur);if(unchecked((int)char.GetUnicodeCategory(str,
0)!=pc[1])){break;}goto case Compiler.Any;case Compiler.NUCode:shouldLog=true;str=char.ConvertFromUtf32(cur);if(unchecked((int)char.GetUnicodeCategory(str,
0))==pc[1]){break;}goto case Compiler.Any;case Compiler.Any:shouldLog=true;if(LexContext.EndOfInput==input.Current){break;}passed=true;lpassed=true;_EnqueueFiber(ref
 nextFiberCount,ref nextFibers,new _Fiber(t,t.Index+1,saved),sp+1);break;case Compiler.Match:matched=saved;match=pc[1]; i=currentFiberCount;break;}if(shouldLog)
{++passes;_LogInstruction(input,pc,cur,sp,lpassed,log);}}if(passed){sb.Append(char.ConvertFromUtf32(cur));input.Advance();if(LexContext.EndOfInput!=input.Current)
{var ch1=unchecked((char)input.Current);if(char.IsHighSurrogate(ch1)){input.Advance();if(-1==input.Advance())throw new ExpectingException("Expecting low surrogate in unicode stream. The input source is corrupt or not valid Unicode",
input.Line,input.Column,input.Position,input.FileOrUrl);++sp;var ch2=unchecked((char)input.Current);cur=char.ConvertToUtf32(ch1,ch2);}else cur=ch1;}else
 cur=-1;++sp;}tmp=currentFibers;currentFibers=nextFibers;nextFibers=tmp;currentFiberCount=nextFiberCount;nextFiberCount=0;}if(null!=matched){var start
=matched[0]; var len=matched[1];input.CaptureBuffer.Append(sb.ToString(start,len-start));result=match;return new LexStatistics(maxFiberCount,passes/(sp+1f));
};result=-1; return new LexStatistics(maxFiberCount,passes/(sp+1f));}static void _LogInstruction(LexContext input,int[]pc,int cur,int sp,bool passed,TextWriter
 log){log.WriteLine("["+sp+"] "+(cur!=-1?char.ConvertFromUtf32(cur):"<EOI>")+": "+Compiler.ToString(pc)+" "+(passed?"passed":(pc[0]==Compiler.Switch&&
-1<Array.IndexOf(pc,-2)?"defaulted":"failed")));}static bool _InRanges(int[]pc,ref int index,int ch){var found=false; for(var j=index;j<pc.Length;++j)
{if(0>pc[j]){index=j;return false;} var first=pc[j];++j;var last=pc[j]; if(ch<=last){if(first<=ch)found=true;index=j;return found;}}index=pc.Length;return
 found;}static void _EnqueueFiber(ref int lcount,ref _Fiber[]l,_Fiber t,int sp){ if(l.Length<=lcount){var newarr=new _Fiber[l.Length*2];Array.Copy(l,0,
newarr,0,l.Length);l=newarr;}l[lcount]=t;++lcount;var pc=t.Program[t.Index];switch(pc[0]){case Compiler.Jmp:for(var j=1;j<pc.Length;j++)_EnqueueFiber(ref
 lcount,ref l,new _Fiber(t.Program,pc[j],t.Saved),sp);break;case Compiler.Save:var slot=pc[1];var max=slot>t.Saved.Length?slot:t.Saved.Length;var saved
=new int[max];for(var i=0;i<t.Saved.Length;++i)saved[i]=t.Saved[i];saved[slot]=sp;_EnqueueFiber(ref lcount,ref l,new _Fiber(t,t.Index+1,saved),sp);break;
}}private struct _Fiber{public readonly int[][]Program;public readonly int Index;public int[]Saved;public _Fiber(int[][]program,int index,int[]saved){
Program=program;Index=index;Saved=saved;}public _Fiber(_Fiber fiber,int index,int[]saved){Program=fiber.Program;Index=index;Saved=saved;}}}}namespace L
{public struct LexStatistics{public readonly int MaxFiberCount;public readonly float AverageCharacterPasses;public LexStatistics(int maxFiberCount,float
 averageCharacterPasses){MaxFiberCount=maxFiberCount;AverageCharacterPasses=averageCharacterPasses;}}}namespace L{static class RangeUtility{public static
 int[]Merge(int[]x,int[]y){var pairs=new List<KeyValuePair<int,int>>((x.Length+y.Length)/2);pairs.AddRange(ToPairs(x));pairs.AddRange(ToPairs(y));NormalizeRangeList(pairs);
return FromPairs(pairs);}public static bool Intersects(int[]x,int[]y){if(null==x||null==y)return false;if(x==y)return true;for(var i=0;i<x.Length;i+=2)
{for(var j=0;j<y.Length;j+=2){if(Intersects(x[i],x[i+1],y[j],y[j+1]))return true;if(x[i]>y[j+1])return false;}}return false;}public static bool Intersects(int
 xf,int xl,int yf,int yl){return(xf>=yf&&xf<=yl)||(xl>=yf&&xl<=yl);}public static KeyValuePair<int,int>[]ToPairs(int[]packedRanges){var result=new KeyValuePair<int,
int>[packedRanges.Length/2];for(var i=0;i<result.Length;++i){var j=i*2;result[i]=new KeyValuePair<int,int>(packedRanges[j],packedRanges[j+1]);}return result;
}public static int[]FromPairs(IList<KeyValuePair<int,int>>pairs){var result=new int[pairs.Count*2];for(int ic=pairs.Count,i=0;i<ic;++i){var pair=pairs[i];
var j=i*2;result[j]=pair.Key;result[j+1]=pair.Value;}return result;}public static void NormalizeRangeArray(int[]packedRanges){var pairs=ToPairs(packedRanges);
NormalizeRangeList(pairs);for(var i=0;i<pairs.Length;++i){var j=i*2;packedRanges[j]=pairs[i].Key;packedRanges[j+1]=pairs[i].Value;}}public static void
 NormalizeRangeList(IList<KeyValuePair<int,int>>pairs){_Sort(pairs,0,pairs.Count-1);var or=default(KeyValuePair<int,int>);for(int i=1;i<pairs.Count;++i)
{if(pairs[i-1].Value>=pairs[i].Key){var nr=new KeyValuePair<int,int>(pairs[i-1].Key,pairs[i].Value);pairs[i-1]=or=nr;pairs.RemoveAt(i);--i;}}}public static
 IEnumerable<KeyValuePair<int,int>>NotRanges(IEnumerable<KeyValuePair<int,int>>ranges){ var last=0x10ffff;using(var e=ranges.GetEnumerator()){if(!e.MoveNext())
{yield return new KeyValuePair<int,int>(0x0,0x10ffff);yield break;}if(e.Current.Key>0){yield return new KeyValuePair<int,int>(0,unchecked(e.Current.Key-
1));last=e.Current.Value;if(0x10ffff<=last)yield break;}while(e.MoveNext()){if(0x10ffff<=last)yield break;if(unchecked(last+1)<e.Current.Key)yield return
 new KeyValuePair<int,int>(unchecked(last+1),unchecked((e.Current.Key-1)));last=e.Current.Value;}if(0x10ffff>last)yield return new KeyValuePair<int,int>(unchecked((last
+1)),0x10ffff);}}public static int[]GetRanges(IEnumerable<int>sortedChars){var result=new List<int>();int first;int last;using(var e=sortedChars.GetEnumerator())
{bool moved=e.MoveNext();while(moved){first=last=e.Current;while((moved=e.MoveNext())&&(e.Current==last||e.Current==last+1)){last=e.Current;}result.Add(first);
result.Add(last);}}return result.ToArray();}static void _Sort(IList<KeyValuePair<int,int>>arr,int left,int right){if(left<right){int pivot=_Partition(arr,
left,right);if(1<pivot){_Sort(arr,left,pivot-1);}if(pivot+1<right){_Sort(arr,pivot+1,right);}}}static int _ComparePairTo(KeyValuePair<int,int>x,KeyValuePair<int,
int>y){var c=x.Key.CompareTo(y.Key);if(c!=0)return c;return x.Value.CompareTo(y.Value);}static int _Partition(IList<KeyValuePair<int,int>>arr,int left,
int right){KeyValuePair<int,int>pivot=arr[left];while(true){while(0<_ComparePairTo(arr[left],pivot)){++left;}while(0>_ComparePairTo(arr[right],pivot))
{--right;}if(left<right){if(0==_ComparePairTo(arr[left],arr[right]))return right;var swap=arr[left];arr[left]=arr[right];arr[right]=swap;}else{return right;
}}}}}