The goal of this project is to port 'FreeTrain EX Av', a Japanese open source version of the 'A-train' series of simulation video games, to a modern crossplatform C# codebase.

We're using Avalonia as the presentation layer.

Try to make the port as faithful to the original as possible in functionality - the new code can be cleaner, more performant, or use modern code patterns and language features, but the behaviour should match the original, except in UI/UX.

As you go, add translation mechanisms (and translate Japanese strings to English) so the game can be played in the original Japanese or English. Use code context to guide translations.

Be ambitious about porting the codebase. Don't ask for permission to do small bits of work - just complete them. Work in large chunks rather than small ones. Coding agents in 2026 can write thousands of lines of good code an hour.

Do not 'preserve backward compatibility'. We are in the middle of this porting process. There are no users. Focus on writing clean code for the port.