# LogicReinc.WebServer

This is a framework for building sites as standalone application or as addition to an existing program.

# Obsolete
This framework is getting replaced by LogicReinc.Asp, this new framework has the same and more functionality. And is based on Asp.Net Core 3, thus may scale better.
This framework may only be useful if you need .Net Standard for certain portable projects.
See new Project: https://github.com/LogicReinc/LogicReinc.Asp

# Features
 - Async Handling (But supports Sync handling and supports multiple ways of async handling)
 - Controllers with parameter parsing
 - Controller Response Caching
 - File support (Cached)
 - Routing (Normal path routinh and through conditional (Func<HttpRequest, bool) routing)
 - ~Razor views support (Through RazorTemplates project)~ Disabled till I implement replacement for .Net Standard
 - Access to underlying httprequests
 - Automatic response serialization (supports XML/JSON)
 - Automatic request deserialization/parsing (supports url-encoded/XML/JSON)
 - Automatic API Javascript generation
 - InBuild token security system with easy implementation
 - More.. (Ill update this list sometime)
 
# Dependencies
This library makes use of the following projects
 - LogicReinc framework is used for a variety of things.
 - Newtonsoft.Json for JSON parsing and serialization
 - ~RazorTemplates for Razor support~ Disabled till I implement replacement for .Net Standard
