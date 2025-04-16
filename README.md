# NetNation

This program has been developed for reading data from ".csv" file and generate two SQL INSERT 
statements. The full task description can be found in "./Task/Usage Translator.docx" file.

How to use
==========

0) Use Windows
1) Create XXX folder
2) Put Sample_Report.csv to XXX (can be found in the ./Task)
3) Put typemap.json to XXX (can be found in the ./Task)
4) Copy NetNationExercise.exe, NetNationExercise.pdb, appsettings.json from ./Executables to XXX
5) Edit appsettings.json, add or remove PartnerIDs you want to exclude from processing
6) Run console command "NetNationExercise Sample_Report.csv typemap.json"
7) If something was wrong the program shows an error message
8) If everything is ok the program will generate 4 files:
	- insert-chargeable.sql;
	- insert-domains.sql;
	- log-errors.txt;
	- log-success.txt;

You can type NetNationExercise and hit Enter to see a use propmpt.

Generated output files can be found in ./Generated files folder.

Unit testing
============
This program can be automatically unit tested, for example, with MSTests. 
The corresponding project NetNationExercise.Tests and a few tests are included.


Additional Criteria for Evaluation question in Usage Translator.docx
======================================================================
To protect INSERT SQL queries from exploitation, you can put them in a stored 
procedure. You give the user permission to execute this SP and do not give access to 
the tables.


