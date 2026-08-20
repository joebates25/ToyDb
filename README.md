This is a little toy database made because it seems cool to implement an actual storage engine, not just a text file that is "technically a database ;)". I'm trying to avoid looking at any code, and just rely on lectures (CMU ftw), textbooks and blogs. This uses c# and not something like Rust, C or C++ because I value my sanity and wellbeing and want to actually enjoy this.

It uses a basic paging scheme with slotted pages and everything is stored in one file. No indexes right now but that's coming soon. 

It currently has few real useful capabilities but will get there...eventually. Unless I get bored and move onto something else. 

Planned for Iteration First: 
- 4 types (int, long, bool, string)
- Basic inserts
- Basic selects with basic filtering 
- Basic deletes with filtering 
- Full table scan for all selects. Index? lol 
- No handling of free space gaps
- Uses a page buffer with very basic eviction policy: None. Everything get's written at the end. Run out of space? Oh Well. 
- Strict schema limitations: If you can't fit your while schema def on 1 page, why are you even here?
- Vacuum to clean up free space

Future plans:
- Making database actually relational 
- hash table indexes
- Basic benchmarking of speed and space efficiency so that I can see just how crappy this is with the most basic, stupid implementations and improve from there
- Actual eviction policies so that this writes to disk more than just once
- Sorting, 
- Stuff like limit, max/min, count
  
Future future plans:
- B+ tree indexes
- Concurrency and transaction management
- partial ACID compliance
- Write ahead logging 
- Network protocols
- Actual sql parsing...maybe
