Create Markdown content for a knowledge base article.
Use a short summary followed by useful bullets or headings from the uploaded files.
Return only Markdown.

# Article
<~article_title noescape>

# Uploaded Files
<~documents repeat inline>
## <~filename noescape>
<~sections if="sections_text" inline>Sections: <~sections_text noescape>
</~sections><~text noescape>
</~documents>
