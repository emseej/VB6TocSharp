using System.Text.RegularExpressions;

namespace Vb6ModernConverter.Core;

public static class CompatibilityCatalog
{
    public static readonly Dictionary<string, string> ConstantMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["vbKeyTab"] = "Keys.Tab",
        ["vbKeyDelete"] = "Keys.Delete",
        ["vbYes"] = "DialogResult.Yes",
        ["vbNo"] = "DialogResult.No",
        ["vbNullString"] = "string.Empty",
        ["vbCrLf"] = "Environment.NewLine",
        ["False"] = "false",
        ["True"] = "true",
        ["Nothing"] = "null"
    };

    public static readonly Dictionary<string, string> MethodMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["AddItem"] = "Add",
        ["RemoveItem"] = "Remove",
        ["Clear"] = "Clear",
        ["SetFocus"] = "Focus",
        ["GetText"] = "Text",
        ["SetText"] = "Text",
        ["GetFromClipboard"] = "GetText",
        ["PutInClipboard"] = "SetText",
        ["UndoAction"] = "Undo",
        ["RedoAction"] = "Redo",
        ["Repaint"] = "Refresh",
        ["ZOrder"] = "BringToFront",
        ["Dropdown"] = "DroppedDown",
        ["Copy"] = "Copy",
        ["Cut"] = "Cut",
        ["Paste"] = "Paste",
        ["Scroll"] = "ScrollToControl",
        ["StartDrag"] = "DoDragDrop",
        ["RemoveAll"] = "Clear",
        ["Exists"] = "ContainsKey"
    };

    public static readonly Dictionary<string, string> PropertyMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Caption"] = "Text",
        ["BackColor"] = "BackColor",
        ["ForeColor"] = "ForeColor",
        ["Visible"] = "Visible",
        ["Enabled"] = "Enabled",
        ["Height"] = "Height",
        ["Width"] = "Width",
        ["Left"] = "Left",
        ["Top"] = "Top",
        ["Tag"] = "Tag",
        ["ListIndex"] = "SelectedIndex",
        ["ListCount"] = "Items.Count",
        ["Value"] = "Value",
        ["Text"] = "Text",
        ["Count"] = "Count",
        ["CanUndo"] = "CanUndo",
        ["CanRedo"] = "CanRedo"
    };

    public static readonly Dictionary<string, string> FunctionMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Left"] = "Microsoft.VisualBasic.Strings.Left",
        ["Right"] = "Microsoft.VisualBasic.Strings.Right",
        ["Mid"] = "Microsoft.VisualBasic.Strings.Mid",
        ["Len"] = "Microsoft.VisualBasic.Strings.Len",
        ["Trim"] = "Microsoft.VisualBasic.Strings.Trim",
        ["LTrim"] = "Microsoft.VisualBasic.Strings.LTrim",
        ["RTrim"] = "Microsoft.VisualBasic.Strings.RTrim",
        ["InStr"] = "Microsoft.VisualBasic.Strings.InStr",
        ["Replace"] = "Microsoft.VisualBasic.Strings.Replace",
        ["MsgBox"] = "Microsoft.VisualBasic.Interaction.MsgBox",
        ["InputBox"] = "Microsoft.VisualBasic.Interaction.InputBox",
        ["CreateObject"] = "Microsoft.VisualBasic.Interaction.CreateObject",
        ["GetObject"] = "Microsoft.VisualBasic.Interaction.GetObject",
        ["DateDiff"] = "Microsoft.VisualBasic.DateAndTime.DateDiff",
        ["DateAdd"] = "Microsoft.VisualBasic.DateAndTime.DateAdd",
        ["DatePart"] = "Microsoft.VisualBasic.DateAndTime.DatePart",
        ["IIf"] = "Microsoft.VisualBasic.Interaction.IIf",
        ["UBound"] = "Microsoft.VisualBasic.Information.UBound",
        ["LBound"] = "Microsoft.VisualBasic.Information.LBound",
        ["IsNumeric"] = "Microsoft.VisualBasic.Information.IsNumeric",
        ["IsDate"] = "Microsoft.VisualBasic.Information.IsDate",
        ["IsArray"] = "Microsoft.VisualBasic.Information.IsArray"
    };

    public static readonly Dictionary<string, string> StatementHints = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ReDim"] = "// converted: dynamic resize array (verify bounds/options)",
        ["Erase"] = "// converted: array reset/clear",
        ["RaiseEvent"] = "// converted: event invocation (verify delegate signature)",
        ["With "] = "// converted: with-block flattened member access",
        ["Open "] = "// converted: file open (verify FileMode/FileAccess)",
        ["Close "] = "// converted: file close",
        ["Print #"] = "// converted: stream write",
        ["Write #"] = "// converted: stream write csv-style",
        ["Input #"] = "// converted: stream read",
        ["Line Input #"] = "// converted: stream readline",
        ["Randomize"] = "Random.Shared.Next();",
        ["Beep"] = "Console.Beep();"
    };

    public static string Apply(string input)
    {
        var output = input;

        foreach (var (vb, net) in ConstantMap)
            output = Regex.Replace(output, $@"\b{Regex.Escape(vb)}\b", net);

        foreach (var (vb, net) in MethodMap)
            output = Regex.Replace(output, $@"\.{Regex.Escape(vb)}\b", $".{net}");

        foreach (var (vb, net) in PropertyMap)
            output = Regex.Replace(output, $@"\.{Regex.Escape(vb)}\b", $".{net}");

        foreach (var (vb, net) in FunctionMap)
            output = Regex.Replace(output, $@"\b{Regex.Escape(vb)}\s*\(", $"{net}(");

        output = Regex.Replace(output, @"\bXor\b", "^", RegexOptions.IgnoreCase);
        output = Regex.Replace(output, @"\bMod\b", "%", RegexOptions.IgnoreCase);
        output = Regex.Replace(output, @"\bAddressOf\b", "&", RegexOptions.IgnoreCase);

        return output;
    }

    public static string? GetStatementHint(string line)
    {
        foreach (var (key, hint) in StatementHints)
        {
            if (line.StartsWith(key, StringComparison.OrdinalIgnoreCase)) return hint;
        }

        return null;
    }
}
