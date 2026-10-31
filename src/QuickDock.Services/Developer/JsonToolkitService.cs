using System;
using System.IO;
using System.Text;
using System.Text.Json;
using QuickDock.Core.Interfaces;
using QuickDock.Core.Models;

namespace QuickDock.Services.Developer;

public sealed class JsonToolkitService : IJsonToolkitService
{
    private static readonly JsonSerializerOptions IndentedOptions = new() { WriteIndented = true };
    private static readonly JsonSerializerOptions MinifiedOptions = new() { WriteIndented = false };

    public JsonFormatResult Format(string jsonInput, bool indented = true)
    {
        if (string.IsNullOrWhiteSpace(jsonInput))
        {
            return new JsonFormatResult
            {
                Success = false,
                ErrorMessage = "Input cannot be empty."
            };
        }

        try
        {
            using var doc = JsonDocument.Parse(jsonInput, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
            using var stream = new MemoryStream();
            using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = indented });
            doc.WriteTo(writer);
            writer.Flush();

            string formatted = Encoding.UTF8.GetString(stream.ToArray());
            return new JsonFormatResult
            {
                Success = true,
                FormattedText = formatted
            };
        }
        catch (JsonException ex)
        {
            return new JsonFormatResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                ErrorLine = ex.LineNumber,
                ErrorColumn = ex.BytePositionInLine
            };
        }
        catch (Exception ex)
        {
            return new JsonFormatResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public JsonFormatResult Minify(string jsonInput) => Format(jsonInput, indented: false);

    public JsonFormatResult Validate(string jsonInput)
    {
        if (string.IsNullOrWhiteSpace(jsonInput))
        {
            return new JsonFormatResult
            {
                Success = false,
                ErrorMessage = "Input cannot be empty."
            };
        }

        try
        {
            using var doc = JsonDocument.Parse(jsonInput);
            return new JsonFormatResult
            {
                Success = true,
                FormattedText = "Valid JSON"
            };
        }
        catch (JsonException ex)
        {
            return new JsonFormatResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                ErrorLine = ex.LineNumber,
                ErrorColumn = ex.BytePositionInLine
            };
        }
        catch (Exception ex)
        {
            return new JsonFormatResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }
}
