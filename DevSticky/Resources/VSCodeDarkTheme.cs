using System.Windows.Media;
using ICSharpCode.AvalonEdit.Highlighting;
using WpfColor = System.Windows.Media.Color;

namespace DevSticky.Resources;

/// <summary>
/// VSCode Dark+ inspired syntax highlighting colors for all supported languages:
/// C#, Java, JavaScript, TypeScript, JSON, XML, SQL, Python, Bash
/// </summary>
public static class VSCodeDarkTheme
{
    // VSCode Dark+ color palette
    public static readonly WpfColor Comment = WpfColor.FromRgb(0x6A, 0x99, 0x55);      // Green comments
    public static readonly WpfColor String = WpfColor.FromRgb(0xCE, 0x91, 0x78);       // Orange strings
    public static readonly WpfColor Keyword = WpfColor.FromRgb(0x56, 0x9C, 0xD6);      // Blue keywords
    public static readonly WpfColor ControlKeyword = WpfColor.FromRgb(0xC5, 0x86, 0xC0); // Purple control flow
    public static readonly WpfColor Type = WpfColor.FromRgb(0x4E, 0xC9, 0xB0);         // Teal types
    public static readonly WpfColor Number = WpfColor.FromRgb(0xB5, 0xCE, 0xA8);       // Light green numbers
    public static readonly WpfColor Function = WpfColor.FromRgb(0xDC, 0xDC, 0xAA);     // Yellow functions
    public static readonly WpfColor Variable = WpfColor.FromRgb(0x9C, 0xDC, 0xFE);     // Light blue variables
    public static readonly WpfColor Operator = WpfColor.FromRgb(0xD4, 0xD4, 0xD4);     // Light gray operators
    public static readonly WpfColor Punctuation = WpfColor.FromRgb(0x80, 0x80, 0x80);  // Gray punctuation
    public static readonly WpfColor Preprocessor = WpfColor.FromRgb(0xC5, 0x86, 0xC0); // Purple preprocessor
    public static readonly WpfColor XmlTag = WpfColor.FromRgb(0x56, 0x9C, 0xD6);       // Blue XML/HTML tags
    public static readonly WpfColor XmlAttribute = WpfColor.FromRgb(0x9C, 0xDC, 0xFE); // Light blue attributes
    public static readonly WpfColor XmlValue = WpfColor.FromRgb(0xCE, 0x91, 0x78);     // Orange attribute values
    public static readonly WpfColor JsonKey = WpfColor.FromRgb(0x9C, 0xDC, 0xFE);      // Light blue JSON keys
    public static readonly WpfColor Regex = WpfColor.FromRgb(0xD1, 0x6D, 0x6D);        // Red regex
    public static readonly WpfColor Escape = WpfColor.FromRgb(0xD7, 0xBA, 0x7D);       // Yellow escape chars
    public static readonly WpfColor Constant = WpfColor.FromRgb(0x4F, 0xC1, 0xFF);     // Bright blue constants
    public static readonly WpfColor SqlKeyword = WpfColor.FromRgb(0x56, 0x9C, 0xD6);   // Blue SQL keywords
    public static readonly WpfColor SqlFunction = WpfColor.FromRgb(0xDC, 0xDC, 0xAA);  // Yellow SQL functions
    public static readonly WpfColor Default = WpfColor.FromRgb(0xD4, 0xD4, 0xD4);      // Default text

    public static void ApplyTheme(IHighlightingDefinition definition)
    {
        if (definition == null) return;

        foreach (var color in definition.NamedHighlightingColors)
        {
            var name = color.Name.ToLowerInvariant();
            ApplyColorByName(color, name);
        }
    }

    private static void ApplyColorByName(HighlightingColor color, string name)
    {
        // ========== COMMENTS (all languages) ==========
        if (name.Contains("comment") || name.Contains("documentation") || name.Contains("doc") ||
            name.Contains("todo") || name.Contains("javadoc") || name.Contains("xmldoc"))
        {
            color.Foreground = new SimpleHighlightingBrush(Comment);
            color.FontStyle = System.Windows.FontStyles.Italic;
            return;
        }

        // ========== STRINGS (all languages) ==========
        if (name.Contains("string") || name.Contains("char") || 
            (name.Contains("literal") && !name.Contains("number")) ||
            name.Contains("cdata") || name.Contains("text") ||
            name.Contains("heredoc") || name.Contains("template") ||
            name.Contains("interpolat") || name.Contains("fstring") ||
            name.Contains("rawstring") || name.Contains("verbatim"))
        {
            color.Foreground = new SimpleHighlightingBrush(String);
            return;
        }

        // ========== NUMBERS (all languages) ==========
        if (name.Contains("number") || name.Contains("digit") || name.Contains("integer") ||
            name.Contains("float") || name.Contains("decimal") || name.Contains("hex") ||
            name.Contains("numeric") || name.Contains("octal") || name.Contains("binary") ||
            name.Contains("exponent") || name.Contains("scientific"))
        {
            color.Foreground = new SimpleHighlightingBrush(Number);
            return;
        }

        // ========== ESCAPE SEQUENCES (all languages) ==========
        if (name.Contains("escape") || name.Contains("special") || name.Contains("backslash"))
        {
            color.Foreground = new SimpleHighlightingBrush(Escape);
            return;
        }

        // ========== REGEX (JavaScript, Python, etc.) ==========
        if (name.Contains("regex") || name.Contains("regexp") || name.Contains("pattern"))
        {
            color.Foreground = new SimpleHighlightingBrush(Regex);
            return;
        }

        // ========== C#/Java SPECIFIC ==========
        // Value types (int, bool, string, void, etc.)
        if (name.Contains("valuetype") || name.Contains("referencetype") || 
            name.Contains("typekeyword") || name == "valuetypekeywords" || 
            name == "referencetypekeywords" || name.Contains("primitivetype"))
        {
            color.Foreground = new SimpleHighlightingBrush(Type);
            return;
        }

        // Visibility modifiers (public, private, protected, internal)
        if (name.Contains("visibility") || name.Contains("accessmodifier"))
        {
            color.Foreground = new SimpleHighlightingBrush(Keyword);
            return;
        }

        // Other modifiers (static, final, readonly, virtual, override, abstract, sealed, async)
        if ((name.Contains("modifier") && !name.Contains("parameter")) || 
            name.Contains("static") || name.Contains("final") || name.Contains("abstract"))
        {
            color.Foreground = new SimpleHighlightingBrush(Keyword);
            return;
        }

        // Parameter modifiers (ref, out, in, params)
        if (name.Contains("parametermodifier") || name.Contains("parammodifier"))
        {
            color.Foreground = new SimpleHighlightingBrush(Keyword);
            return;
        }

        // Control flow keywords (if, else, for, while, return, etc.)
        if (name.Contains("goto") || name.Contains("exception") || name.Contains("jump") ||
            name.Contains("checked") || name.Contains("unsafe") || name.Contains("control") ||
            name.Contains("loop") || name.Contains("conditional") || name.Contains("branch"))
        {
            color.Foreground = new SimpleHighlightingBrush(ControlKeyword);
            return;
        }

        // Namespace/Import keywords
        if (name.Contains("namespace") || name.Contains("using") || name.Contains("import") ||
            name.Contains("package") || name.Contains("module") || name.Contains("from"))
        {
            color.Foreground = new SimpleHighlightingBrush(Keyword);
            return;
        }

        // this, base, self, super
        if (name.Contains("thisorbase") || name.Contains("thisreference") || 
            name.Contains("basereference") || name.Contains("self") || name.Contains("super"))
        {
            color.Foreground = new SimpleHighlightingBrush(Keyword);
            return;
        }

        // null, None, nil, undefined, default
        if (name.Contains("nullorvalue") || name.Contains("null") || name.Contains("none") ||
            name.Contains("nil") || name.Contains("undefined"))
        {
            color.Foreground = new SimpleHighlightingBrush(Constant);
            return;
        }

        // true, false, boolean constants
        if (name.Contains("truefalse") || name.Contains("boolean") || name.Contains("bool"))
        {
            color.Foreground = new SimpleHighlightingBrush(Constant);
            return;
        }

        // get, set, add, remove (C#)
        if (name.Contains("getset") || name.Contains("addremove") || name.Contains("accessor"))
        {
            color.Foreground = new SimpleHighlightingBrush(Keyword);
            return;
        }

        // Context keywords (var, dynamic, where, etc.)
        if (name.Contains("context") || name.Contains("semantic") || name.Contains("var"))
        {
            color.Foreground = new SimpleHighlightingBrush(Keyword);
            return;
        }

        // Operator keywords (new, typeof, sizeof, is, as, instanceof)
        if (name.Contains("operatorkeyword") || (name.Contains("operator") && name.Contains("keyword")) ||
            name.Contains("instanceof") || name.Contains("typeof") || name.Contains("sizeof"))
        {
            color.Foreground = new SimpleHighlightingBrush(Keyword);
            return;
        }

        // ========== JAVASCRIPT/TYPESCRIPT SPECIFIC ==========
        // JS keywords (let, const, var, function, class, extends, etc.)
        if (name.Contains("jskeyword") || name.Contains("tskeyword") || 
            name.Contains("ecmakeyword") || name.Contains("es6"))
        {
            color.Foreground = new SimpleHighlightingBrush(Keyword);
            return;
        }

        // Arrow functions, async/await
        if (name.Contains("arrow") || name.Contains("async") || name.Contains("await") ||
            name.Contains("promise") || name.Contains("generator") || name.Contains("yield"))
        {
            color.Foreground = new SimpleHighlightingBrush(ControlKeyword);
            return;
        }

        // ========== PYTHON SPECIFIC ==========
        // Python keywords (def, class, if, elif, else, for, while, try, except, etc.)
        if (name.Contains("pythonkeyword") || name.Contains("pykeyword"))
        {
            color.Foreground = new SimpleHighlightingBrush(Keyword);
            return;
        }

        // Python decorators (@decorator)
        if (name.Contains("decorator") || name.Contains("annotation"))
        {
            color.Foreground = new SimpleHighlightingBrush(Function);
            return;
        }

        // Python builtins (print, len, range, etc.)
        if (name.Contains("builtin") || name.Contains("builtinfunction") || 
            name.Contains("builtintype") || name.Contains("magic"))
        {
            color.Foreground = new SimpleHighlightingBrush(Function);
            return;
        }

        // ========== SQL SPECIFIC ==========
        // SQL keywords (SELECT, FROM, WHERE, JOIN, etc.)
        if (name.Contains("sqlkeyword") || name.Contains("tsqlkeyword") ||
            name.Contains("select") || name.Contains("from") || name.Contains("where") ||
            name.Contains("join") || name.Contains("insert") || name.Contains("update") ||
            name.Contains("delete") || name.Contains("create") || name.Contains("alter") ||
            name.Contains("drop") || name.Contains("table") || name.Contains("index") ||
            name.Contains("view") || name.Contains("procedure") || name.Contains("trigger"))
        {
            color.Foreground = new SimpleHighlightingBrush(SqlKeyword);
            return;
        }

        // SQL functions (COUNT, SUM, AVG, MAX, MIN, etc.)
        if (name.Contains("sqlfunction") || name.Contains("aggregate") ||
            name.Contains("count") || name.Contains("sum") || name.Contains("avg"))
        {
            color.Foreground = new SimpleHighlightingBrush(SqlFunction);
            return;
        }

        // SQL data types
        if (name.Contains("sqldatatype") || name.Contains("sqltype"))
        {
            color.Foreground = new SimpleHighlightingBrush(Type);
            return;
        }

        // ========== BASH/SHELL SPECIFIC ==========
        // Shell keywords (if, then, else, fi, for, do, done, case, esac, etc.)
        if (name.Contains("shellkeyword") || name.Contains("bashkeyword") ||
            name.Contains("shebang") || name.Contains("hashbang"))
        {
            color.Foreground = new SimpleHighlightingBrush(Keyword);
            return;
        }

        // Shell variables ($VAR, ${VAR})
        if (name.Contains("shellvariable") || name.Contains("bashvariable") ||
            name.Contains("envvar") || name.Contains("environment"))
        {
            color.Foreground = new SimpleHighlightingBrush(Variable);
            return;
        }

        // Shell commands
        if (name.Contains("command") || name.Contains("builtin"))
        {
            color.Foreground = new SimpleHighlightingBrush(Function);
            return;
        }

        // ========== XML/HTML SPECIFIC ==========
        // XML/HTML tags
        if (name.Contains("xmltag") || name.Contains("tag") || name.Contains("element") ||
            name.Contains("htmltag") || name.Contains("doctype") || name.Contains("entity") ||
            (name.Contains("name") && (name.Contains("xml") || name.Contains("html"))) ||
            name.Contains("xmldelimiter") || name.Contains("htmldelimiter"))
        {
            color.Foreground = new SimpleHighlightingBrush(XmlTag);
            return;
        }

        // XML/HTML attributes
        if (name.Contains("attribute") && !name.Contains("value"))
        {
            color.Foreground = new SimpleHighlightingBrush(XmlAttribute);
            return;
        }

        // XML/HTML attribute values
        if (name.Contains("attributevalue") || 
            (name.Contains("value") && !name.Contains("type") && !name.Contains("null") && !name.Contains("keyword")))
        {
            color.Foreground = new SimpleHighlightingBrush(XmlValue);
            return;
        }

        // XML processing instructions
        if (name.Contains("processing") || name.Contains("xmlpi") || name.Contains("prolog"))
        {
            color.Foreground = new SimpleHighlightingBrush(Preprocessor);
            return;
        }

        // ========== JSON SPECIFIC ==========
        // JSON keys/property names
        if (name.Contains("key") || name.Contains("propertyname") || name.Contains("objectkey") ||
            name.Contains("jsonkey") || name.Contains("fieldname"))
        {
            color.Foreground = new SimpleHighlightingBrush(JsonKey);
            return;
        }

        // JSON values (handled by string/number/boolean above)

        // ========== GENERIC FALLBACKS ==========
        // Generic keywords
        if (name.Contains("keyword") && !name.Contains("type"))
        {
            color.Foreground = new SimpleHighlightingBrush(Keyword);
            return;
        }

        // Types (class, struct, interface, enum, delegate)
        if (name.Contains("type") || name.Contains("class") || name.Contains("struct") ||
            name.Contains("interface") || name.Contains("enum") || name.Contains("delegate") ||
            name.Contains("generic") || name.Contains("void") || name.Contains("object") || 
            name.Contains("array"))
        {
            color.Foreground = new SimpleHighlightingBrush(Type);
            return;
        }

        // Functions/Methods
        if (name.Contains("method") || name.Contains("function") || name.Contains("call") ||
            name.Contains("invoke") || name.Contains("def") || name.Contains("lambda") ||
            name.Contains("func") || name.Contains("subroutine") || name.Contains("proc"))
        {
            color.Foreground = new SimpleHighlightingBrush(Function);
            return;
        }

        // Variables, parameters, properties
        if (name.Contains("variable") || name.Contains("parameter") || name.Contains("param") ||
            name.Contains("property") || name.Contains("field") || name.Contains("member") ||
            name.Contains("local") || name.Contains("argument") || name.Contains("identifier"))
        {
            color.Foreground = new SimpleHighlightingBrush(Variable);
            return;
        }

        // Operators
        if (name.Contains("operator") || name.Contains("symbol") || name.Contains("assign") ||
            name.Contains("comparison") || name.Contains("arithmetic") || name.Contains("logical") ||
            name.Contains("bitwise") || name.Contains("ternary"))
        {
            color.Foreground = new SimpleHighlightingBrush(Operator);
            return;
        }

        // Punctuation
        if (name.Contains("punctuation") || name.Contains("bracket") || name.Contains("delimiter") ||
            name.Contains("brace") || name.Contains("paren") || name.Contains("semicolon") ||
            name.Contains("comma") || name.Contains("colon") || name.Contains("dot"))
        {
            color.Foreground = new SimpleHighlightingBrush(Punctuation);
            return;
        }

        // Preprocessor directives
        if (name.Contains("preprocessor") || name.Contains("directive") || name.Contains("region") ||
            name.Contains("pragma") || name.Contains("define") || name.Contains("include") ||
            name.Contains("ifdef") || name.Contains("endif") || name.Contains("macro"))
        {
            color.Foreground = new SimpleHighlightingBrush(Preprocessor);
            return;
        }

        // Default - soften bright colors for dark theme
        if (color.Foreground != null)
        {
            var brush = color.Foreground.GetBrush(null);
            if (brush is SolidColorBrush scb)
            {
                var c = scb.Color;
                var luminance = 0.299 * c.R + 0.587 * c.G + 0.114 * c.B;
                if (luminance > 200)
                {
                    color.Foreground = new SimpleHighlightingBrush(Default);
                }
            }
        }
    }
}
