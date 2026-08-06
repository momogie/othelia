using System.Text.RegularExpressions;

namespace Othelia.Api.Observability;

public static class IngestionFilter
{
    public static IReadOnlyList<SpanRecord> Apply(
        IReadOnlyList<SpanRecord> spans,
        IReadOnlyList<IngestionFilterRule> rules)
    {
        if (spans.Count == 0)
            return spans;

        var compiled = rules
            .Where(r => r.Enabled && !string.IsNullOrWhiteSpace(r.Value))
            .Select(BuildRule)
            .ToList();

        if (compiled.Count == 0)
            return spans;

        var result = new List<SpanRecord>(spans.Count);
        foreach (var span in spans)
        {
            var keep = true;
            foreach (var rule in compiled)
            {
                if (!rule.Matches(span))
                    continue;

                keep = rule.Keeps;
                break;
            }

            if (keep)
                result.Add(span);
        }

        return result;
    }

    private static CompiledRule BuildRule(IngestionFilterRule rule)
    {
        var op = string.IsNullOrWhiteSpace(rule.Operator) ? "Equals" : rule.Operator;
        Regex? regex = op.Equals("Regex", StringComparison.OrdinalIgnoreCase)
            ? new Regex(rule.Value, RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100))
            : null;

        return new CompiledRule(rule.Field, op, rule.Value, rule.Action, regex);
    }

    private sealed class CompiledRule
    {
        private readonly string _field;
        private readonly string _operator;
        private readonly string _value;
        private readonly bool _keep;
        private readonly Regex? _regex;

        public CompiledRule(string field, string op, string value, string action, Regex? regex)
        {
            _field = field;
            _operator = op;
            _value = value;
            _keep = action.Equals("Keep", StringComparison.OrdinalIgnoreCase);
            _regex = regex;
        }

        public bool Matches(SpanRecord span) => GetFieldValue(span) is { } actual && Compare(actual);
        public bool Keeps => _keep;

        private string? GetFieldValue(SpanRecord span) => _field switch
        {
            "ServiceName" => span.ServiceName,
            "SpanName" => span.Name,
            "HttpPath" => span.HttpPath,
            "StatusCode" => span.StatusCode,
            "Kind" => span.Kind,
            _ => null,
        };

        private bool Compare(string actual) => _operator.ToLowerInvariant() switch
        {
            "equals" => string.Equals(actual, _value, StringComparison.OrdinalIgnoreCase),
            "notequals" => !string.Equals(actual, _value, StringComparison.OrdinalIgnoreCase),
            "contains" => actual.Contains(_value, StringComparison.OrdinalIgnoreCase),
            "regex" => _regex?.IsMatch(actual) ?? false,
            _ => false,
        };
    }
}
