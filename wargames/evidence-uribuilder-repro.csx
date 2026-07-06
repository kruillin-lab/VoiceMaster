using System;
var baseUrl = "http://127.0.0.1:7851";
var uriBuilder = new UriBuilder(baseUrl) { Path = "/api/reload?tts_method=" + "xtts" };
Console.WriteLine(uriBuilder.Uri);

var uriBuilder2 = new UriBuilder("http://host:7851/api") { Path = "/api/voices" };
Console.WriteLine(uriBuilder2.Uri);

var uriBuilder3 = new UriBuilder(baseUrl) { Path = "/api/reload?tts_method=xtts" };
Console.WriteLine(uriBuilder3.Uri);
