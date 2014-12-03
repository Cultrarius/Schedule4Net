Schedule4Net
============

This is a general scheduling framework able to create schedules using arbitrary constraints written in C#.

### What is Schedule4Net?
It is a framework that allows a user to create a schedule from a given list of items and constraints. This does not sound spectacular, but it is an NP-hard problem. Schedule4Net tries to approximate a good solution by using a heuristic algorithm. Anyone interested can have a look at it here: http://cds.cern.ch/record/1463647

The cool thing about it is that a user can throw almost any constraint logic at it and it still manages to create a schedule out of it. Don't believe me? Just give it a try!

**What it is not**: this framework does not automatically execute any jobs or run tasks in a regular interval. The purpose of this framework is to solve a hard scheduling problem. If you just want to execute a process every x minutes then you could take a look at a library like [Quartz.NET]

### Download and install

The easiest way to install the lib is to use the NuGet package manager: [Schedule4Net on NuGet]

Of course, you can also clone the source from github and compile the lib yourself.

[Quartz.NET]:http://quartznet.sourceforge.net/
[Schedule4Net on Nuget]:http://nuget.org/packages/Schedule4Net
