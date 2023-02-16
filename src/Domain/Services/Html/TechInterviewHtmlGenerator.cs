﻿using Markdig;

namespace Domain.Services.Html;

public class TechInterviewHtmlGenerator : ITechInterviewHtmlGenerator
{
    public string FromMarkdown(
        string source)
    {
        var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
        return Markdown.ToHtml(source, pipeline);
    }
}