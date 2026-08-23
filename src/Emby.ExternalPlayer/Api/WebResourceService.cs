using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using MediaBrowser.Controller.Net;
using MediaBrowser.Model.Services;

namespace Emby.ExternalPlayer.Api;

public sealed class WebResourceService : IService, IRequiresRequest
{
    private static readonly IDictionary<string, string> ResponseHeaders =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Cache-Control"] = "no-store, max-age=0",
            ["X-Content-Type-Options"] = "nosniff",
        };

    private readonly IHttpResultFactory resultFactory;

    public WebResourceService(IHttpResultFactory resultFactory)
    {
        this.resultFactory = resultFactory;
    }

    public IRequest Request { get; set; } = null!;

    public object Get(GetExternalPlayerWebModule request)
    {
        return GetResource("external-player.js", "application/javascript; charset=utf-8");
    }

    public object Get(GetExternalPlayerLanguageModule request)
    {
        return GetResource("external-player-language.js", "application/javascript; charset=utf-8");
    }

    public object Get(GetExternalPlayerStylesheet request)
    {
        return GetResource("external-player.css", "text/css; charset=utf-8");
    }

    private object GetResource(string suffix, string contentType)
    {
        var assembly = typeof(WebResourceService).GetTypeInfo().Assembly;
        var resourceName = Array.Find(
            assembly.GetManifestResourceNames(),
            name => name.EndsWith(suffix, StringComparison.Ordinal));

        if (resourceName is null)
        {
            throw new FileNotFoundException("Embedded Web resource was not found.", suffix);
        }

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new FileNotFoundException("Embedded Web resource was not found.", suffix);
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return resultFactory.GetResult(Request, memory.ToArray(), contentType, ResponseHeaders);
    }
}
