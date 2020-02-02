# FA pack
A regular expression engine, Pike Virtual Machine R/E compiler/assember, the Rolex lexer generator, plus an experimental lexer generator, Lexly. Unicode support

# FA

This codebase is my new Unicode capable finite automata and regular expression engine. It fills a gap in Microsoft's Regular Expression offering, which only deals with matching rather than lexing (I'll explain the difference), relies on a less efficient but more expressive regular expression engine, and can't stream.
This engine is designed primarily for lexing rather than matching, and it's designed to be efficient and to be streaming. In other words, it fills the holes in what Microsoft offers us in .NET.

I use deslang and csbrick as pre-build steps to build Rolex and Lexly, the included lexer generators. Because of this I've included the binaries in the solution folder. They are not harmful, and they are necessary for the project to build. The source code for each is available elsewhere on my GitHub profile. You can always delete them, but you'll have to delete the pre-build steps from Rolex and Lexly to get the mess to build again.

Finite automata is virtually always used at some phase during regular expression matching and/or compilation regardless of implementation. This is in part due to the dual nature of regular expressions and finite automata. One can exactly describe the other and vice versa. This makes finite automata useful for graphing and otherwise analyzing or interpreting a regular expression, from its grotty internals on outward, and in turn makes regular expressions useful for describing finite automata. They're a natural pair, like peanut butter and jelly. Consequently this library includes both.

One of the major difficulties in supporting Unicode is it's no longer practical for an input transition to be based on a single character value. This is because Unicode ranges are sometimes huge - 21-bits, or a cardinality of 0x110000 - and we must avoid expanding them to their individual characters at all costs or it will take a very long time to process. Especially challenging was the DFA subset/powerset construction which had to work with those ranges as well. In the end I borrowed a technique from the Fare library, which saved me $50 and change on a book I would have needed. Happy find.

This library is a streamlined and rewritten version of my previous offerings, designed specifically for Unicode inputs. Consequently all input values internally are kept as UTF-32, stored as ints.

 

The FA class is the key to the library and handles all of the operations from parsing regular expressions to lexing. Each FA represents a single state in a a finite state machine. They are connected via InputTransitions and EpsilonTransitions. Two things to always be aware of are the root state of the machine, and the extents of the machine as gathered by taking the closure. The closure of a state is simply the set of all states reachable from that state either directly or indirectly, including itself. The closure of x therefore, represents the entire machine for FA x. The astute reader may notice this means machines can be nested inside machines, and this is true. There is no concept of an overarching machine, just the root state of the machine by which it can determine the rest of the states. The root state is always "q0" if graphed. There are no parents, so we can only move along the graph in a single direction. The graphs can contain loops and thus be infinitely recursive. Because of this, when traversing the graph, one must be careful to not retraverse nodes already seen. This is handled by the FillClosure() and FillEpsilonClosure() methods. There shouldn't be a case where you actually have to concern yourself with that.
The library deals with lexing, not matching - at least not directly. Lexing is the process of breaking input up into lexemes or tokens with an associated symbol for each. This is usually passed along to a parser. A normal regular expression match can't do this. Lexing is basically matching a composite regular expression where each subexpression has a special tag - a "symbol" attached to it. There is no way to attach symbols to a regular match expression, and that doesn't make sense to do so.

 

You'll usually want to build a lexer, so basically, the steps to produce a lexer are this:

1. Build an array of FAs, usually parsed from regular expressions using Parse(). Make sure to set the accept symbols to a unique value for each expression you want to distinguish between.

2. Call ToLexer() with the array to get an FSM back you can use to lex.

Now you have a working NFA lexer but you'll probably want to convert it into something more efficient:

3. Build an array of ints to represent your symbol table. Each entry is the id of the FA in that ordinal position. This is optional but recommended otherwise you won't know what your ids are because they will be automatically generated in state order.

4. Call ToDfaStateTable() with the symbol table you created to get an efficient DFA table you can use to lex. Once you have that you can call the Lex() method to lex your input, or alternately create a Tokenizer in order to lex with an FA. For a more efficient tokenizer, use the Rolex tool to generate one.

 
One you have that you can lex either by calling one of the Lex() methods, or create a Tokenizer to do your lexing. The Tokenizer, like the non-static Lex() method, will accept an FA that can either be a DFA or an NFA but the static Lex() method that takes a DFA state table is the most efficient.

See the FADemo project

# Lex

So previously, I have used deterministic finite automata to implement non-backtracking regular expressions in my tokenizers. The algorithm is older, but very efficient. The only downside is the time it takes to generate the state table at least when dealing with large character ranges introduced by Unicode. This is an algorithm limitation and there's very little to be done about it, as I eventually found out.

What I needed was a way to use non-deterministic finite automata and forgo the transformation to the deterministic model altogether. That solves some of the issue, but it still doesn't allow for some of the more complicated regular expressions like word boundaries.

Then a few guys, namely, Rob Pike, Ken Thompson and Russ Cox gave me a fantastic idea that solves these problems efficiently, and in a very interesting way using a tiny virtual machine with a specialized instruction set to run a regular expression match. I've included articles on this in the further reading section. To me this approach is fascinating, as I just love bit twiddling like this. It also potentially lends itself to compilation in the native instruction set of a "real" target machine. I haven't implemented all of that here (yet!) but this is the baseline runtime engine. I should stress that my code draws from the concepts introduced by them and I wouldn't have done it this way without exposure to that code at that link - credit where it is due.

The optimizing compiler still doesn't optimize entirely. There's a significant opportunity to optimize the initial split which should dramatically improve the results, but I haven't implemented it yet, as it's non-trivial.

It's maybe not quite fast enough to be production ready but it's fun to play with so far

# Lexly

Lexly is a lexer generator based on Lex. The engine is Lex, with the same caveats on performance

# Rolex

Lexical analysis is often the first stage of parsing. Before we can analyze a structured document, we must break it into lexical elements called tokens or lexemes. Most parsers use this approach, taking tokens from the lexer and using them as their elemental input. Lexers can also be used outside of parsers, and are in tools like minifiers or even simple scanners that just need to look for patterns in a document, since lexers essentially work as compound regular expression matchers.

Rolex generates lexers to make this process painless and relatively intuitive, both in terms of defining them and using them. The code Rolex generates uses a simple but reliably fast DFA algorithm. All matching is done in linear time. There are no potentially quadratic time expressions you can feed it since it doesn't backtrack. The regular expressions are simple. There are no capturing groups because they are not needed. There are no anchors because they complicate matching, and aren't very useful in tokenizers. There are no lazy expressions, but there is a facility to define multicharacter ending conditions, which is 80% of what lazy expressions are used for.

The main advantage of using Rolex is speed. The generated tokenizers are very fast. The main disadvantage of using Rolex, aside from a somewhat limited regular expression syntax, is the time it can take to generate complicated lexers. Basically you pay for the performance of Rolex upfront, during the build.

The rest are demo projects
