using System.IO;
using System.Text;
using System.Threading.Tasks;
using Luaon.Json;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Net.Http.Headers;
using Newtonsoft.Json;

namespace AssettoServer.Network.Http;

public class LuaOutputFormatter : TextOutputFormatter
{
    public LuaOutputFormatter()
    {
        SupportedMediaTypes.Add(MediaTypeHeaderValue.Parse("text/x-lua"));
        SupportedEncodings.Add(Encoding.UTF8);
    }
    
    public override async Task WriteResponseBodyAsync(OutputFormatterWriteContext context, Encoding selectedEncoding)
    {
        var serializer = new JsonSerializer();

        // Serialize into an in-memory buffer first: JsonSerializer.Serialize writes synchronously,
        // and Kestrel disallows synchronous IO on the response body (AllowSynchronousIO defaults to false).
        var buffer = new StringWriter();
        using (var jlw = new JsonLuaWriter(buffer) { Formatting = Formatting.None })
        {
            serializer.Serialize(jlw, context.Object);
        }

        await using var sw = context.WriterFactory(context.HttpContext.Response.Body, selectedEncoding);
        await sw.WriteAsync(buffer.ToString());
        await sw.FlushAsync();
    }
}
