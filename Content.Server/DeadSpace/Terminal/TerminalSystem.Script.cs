using System.Text;
using System.Text.RegularExpressions;

namespace Content.Server.DeadSpace.Terminal;

public sealed partial class TerminalSystem
{
    private sealed class RunInstruction
    {
        public string Kind;
        public string Argument;
        public string Variable;
        public int Line;
        public List<RunInstruction> Body = new();
        public List<RunInstruction> ElseBody = new();

        public RunInstruction(string kind, string arg, string var, int line)
        {
            Kind = kind;
            Argument = arg;
            Variable = var;
            Line = line;
        }
    }

    private const int MaxRunNesting = 8;
    private const int MaxRunIterations = 100;
    private const int MaxRunSteps = 1000;
    private const int MaxRunOutputLength = 16_384;

    private static readonly Regex RunVariableRegex = new(@"%([A-Za-z_][A-Za-z0-9_]*)%", RegexOptions.Compiled);
    private static readonly Regex RunVariableNameRegex = new(@"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);
    private static readonly Regex RunConditionRegex = new(@"^(.+?)\s*(==|!=)\s*(.+)$", RegexOptions.Compiled);
    private static readonly Regex RunForRegex = new(@"^for\s+([A-Za-z_][A-Za-z0-9_]*)\s+in\s*\((.*)\)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private string CmdRun(TerminalContext ctx, List<string> args)
    {
        if (args.Count != 1)
            return Fail(ctx, Loc.GetString("terminal-run-usage"));

        if (!args[0].EndsWith(".run", StringComparison.OrdinalIgnoreCase))
            return Fail(ctx, Loc.GetString("terminal-run-extension"));

        var path = ResolvePath(ctx, args[0]);

        if (!TryGetNode(ctx.Machine, path, out var node) || node.IsDirectory)
            return Fail(ctx, Loc.GetString("terminal-run-not-found", ("path", args[0])));

        if (!CanTraverse(ctx, path) || !CanRead(ctx, node))
            return Fail(ctx, $"run: {args[0]}: Permission denied");

        return ExecuteScript(ctx, path, node.Content);
    }

    private string ExecuteScript(TerminalContext ctx, string path, string content)
    {
        if (ctx.Depth >= MaxRunNesting)
            return Fail(ctx, Loc.GetString("terminal-run-limit"));

        var lines = content.Replace("\r\n", "\n").Split('\n');
        var index = 0;

        if (lines.Length > 0 && lines[0].StartsWith("#!", StringComparison.Ordinal))
            index = 1;

        if (!TryParseRunBlock(lines, ref index, 0, out var instructions, out var terminator, out var error) || terminator != null)
            return Fail(ctx, Loc.GetString("terminal-run-syntax", ("line", index + 1), ("reason", error ?? "Unexpected block terminator")));

        var interactive = ctx.Interactive;
        ctx.Interactive = false;
        ctx.Depth++;

        var output = new StringBuilder();
        var vars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["0"] = path,
            ["USER"] = ctx.User,
            ["HOME"] = GetHome(ctx.Machine, ctx.User),
            ["HOSTNAME"] = ctx.Comp.Hostname,
        };

        ExecuteRunBlock(ctx, instructions, vars, output);

        ctx.Depth--;
        ctx.Interactive = interactive;
        return output.ToString();
    }

    private static string ExpandRunVariables(string text, Dictionary<string, string> vars)
    {
        return RunVariableRegex.Replace(text, match => vars.TryGetValue(match.Groups[1].Value, out var value) ? value : string.Empty);
    }

    private static string UnquoteRunValue(string value)
    {
        return value.Length >= 2 && value[0] == '"' && value[^1] == '"' ? value[1..^1] : value;
    }

    private static List<string> SplitRunValues(string text)
    {
        var values = new List<string>();
        var value = new StringBuilder();
        var inQuotes = false;

        foreach (var character in text)
        {
            if (character == '"')
            {
                inQuotes = !inQuotes;
                value.Append(character);
            }
            else if (character == ',' && !inQuotes)
            {
                values.Add(UnquoteRunValue(value.ToString().Trim()));
                value.Clear();
            }
            else
            {
                value.Append(character);
            }
        }

        if (value.Length > 0)
            values.Add(UnquoteRunValue(value.ToString().Trim()));

        return values;
    }

    private static void RestoreRunVariable(Dictionary<string, string> vars, string name, bool hadPreviousValue, string? previousValue)
    {
        if (hadPreviousValue)
            vars[name] = previousValue!;
        else
            vars.Remove(name);
    }

    private static bool IsRunComment(string line)
    {
        return line.Equals("rem", StringComparison.OrdinalIgnoreCase)
               || line.StartsWith("rem ", StringComparison.OrdinalIgnoreCase)
               || line.StartsWith("::", StringComparison.Ordinal)
               || line.StartsWith('#');
    }

    private string ExecuteRunCommand(TerminalContext ctx, RunInstruction instruction, Dictionary<string, string> variables)
    {
        var line = ExpandRunVariables(instruction.Argument, variables);

        if (line.StartsWith("set ", StringComparison.OrdinalIgnoreCase))
        {
            var assignment = line[4..].Trim();
            var equals = assignment.IndexOf('=');

            if (equals <= 0)
                return Line(Loc.GetString("terminal-run-syntax", ("line", instruction.Line), ("reason", "Expected set NAME=value")));

            var name = assignment[..equals].Trim();
            if (!RunVariableNameRegex.IsMatch(name))
                return Line(Loc.GetString("terminal-run-syntax", ("line", instruction.Line), ("reason", "Invalid variable name")));

            variables[name] = UnquoteRunValue(assignment[(equals + 1)..].Trim());
            return string.Empty;
        }

        if (IsRunComment(line))
            return string.Empty;

        var result = ExecuteLine(ctx, line);
        variables["ERRORLEVEL"] = ctx.Failed ? "1" : "0";
        return result;
    }

    private bool ExecuteRunBlock(TerminalContext ctx, List<RunInstruction> instructions, Dictionary<string, string> vars, StringBuilder output)
    {
        foreach (var instruction in instructions)
        {
            if (++ctx.Steps > MaxRunSteps)
            {
                output.Append(Line(Loc.GetString("terminal-run-limit")));
                return false;
            }

            if (instruction.Kind == "if")
            {
                var condition = ExpandRunVariables(instruction.Argument, vars);
                var match = RunConditionRegex.Match(condition);

                if (!match.Success)
                {
                    output.Append(Line(Loc.GetString("terminal-run-syntax", ("line", instruction.Line), ("reason", "Expected a comparison using == or !="))));
                    continue;
                }

                var left = UnquoteRunValue(match.Groups[1].Value.Trim());
                var right = UnquoteRunValue(match.Groups[3].Value.Trim());
                var equal = left.Equals(right, StringComparison.OrdinalIgnoreCase);
                var conditionIsTrue = match.Groups[2].Value == "==" ? equal : !equal;

                if (!ExecuteRunBlock(ctx, conditionIsTrue ? instruction.Body : instruction.ElseBody, vars, output))
                    return false;

                continue;
            }

            if (instruction.Kind == "for")
            {
                var values = SplitRunValues(ExpandRunVariables(instruction.Argument, vars));

                if (values.Count > MaxRunIterations)
                {
                    output.Append(Line(Loc.GetString("terminal-run-limit")));
                    return false;
                }

                var hadPreviousValue = vars.TryGetValue(instruction.Variable, out var previousValue);

                foreach (var value in values)
                {
                    vars[instruction.Variable] = value;

                    if (!ExecuteRunBlock(ctx, instruction.Body, vars, output))
                    {
                        RestoreRunVariable(vars, instruction.Variable, hadPreviousValue, previousValue);
                        return false;
                    }
                }

                RestoreRunVariable(vars, instruction.Variable, hadPreviousValue, previousValue);
                continue;
            }

            var result = ExecuteRunCommand(ctx, instruction, vars);

            if (output.Length + result.Length > MaxRunOutputLength)
            {
                output.Append(Line(Loc.GetString("terminal-run-limit")));
                return false;
            }

            output.Append(result);

            if (ctx.Steps > MaxRunSteps)
                return false;
        }

        return true;
    }

    private bool TryParseRunBlock(string[] lines, ref int index, int depth, out List<RunInstruction> instructions, out string? terminator, out string? error)
    {
        instructions = new List<RunInstruction>();
        terminator = null;
        error = null;

        if (depth > MaxRunNesting)
        {
            error = "Maximum block nesting exceeded";
            return false;
        }

        while (index < lines.Length)
        {
            var lineNumber = index + 1;
            var line = lines[index].Trim();

            if (line.Length == 0 || IsRunComment(line))
            {
                index++;
                continue;
            }

            if (line.Equals("else", StringComparison.OrdinalIgnoreCase) || line.Equals("endif", StringComparison.OrdinalIgnoreCase) || line.Equals("endfor", StringComparison.OrdinalIgnoreCase))
            {
                terminator = line.ToLowerInvariant();
                return true;
            }

            if (line.StartsWith("if ", StringComparison.OrdinalIgnoreCase))
            {
                var instruction = new RunInstruction("if", line[3..].Trim(), string.Empty, lineNumber);
                index++;

                if (!TryParseRunBlock(lines, ref index, depth + 1, out instruction.Body, out var bodyTerminator, out error))
                    return false;

                if (bodyTerminator == "else")
                {
                    index++;
                    if (!TryParseRunBlock(lines, ref index, depth + 1, out instruction.ElseBody, out bodyTerminator, out error))
                        return false;
                }

                if (bodyTerminator != "endif")
                {
                    error = "Expected endif";
                    return false;
                }

                index++;
                instructions.Add(instruction);
                continue;
            }

            if (line.StartsWith("for ", StringComparison.OrdinalIgnoreCase))
            {
                var match = RunForRegex.Match(line);
                if (!match.Success)
                {
                    error = "Expected: for NAME in (value1,value2)";
                    return false;
                }

                var instruction = new RunInstruction("for", match.Groups[2].Value, match.Groups[1].Value, lineNumber);
                index++;

                if (!TryParseRunBlock(lines, ref index, depth + 1, out instruction.Body, out var bodyTerminator, out error))
                    return false;

                if (bodyTerminator != "endfor")
                {
                    error = "Expected endfor";
                    return false;
                }

                index++;
                instructions.Add(instruction);
                continue;
            }

            instructions.Add(new RunInstruction("command", line, string.Empty, lineNumber));
            index++;
        }

        return true;
    }
}
