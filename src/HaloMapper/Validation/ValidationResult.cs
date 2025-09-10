using System;
using System.Collections.Generic;
using System.Linq;

namespace HaloMapper.Validation
{
    public class ValidationResult
    {
        public bool IsValid => !Errors.Any();
        public List<ValidationError> Errors { get; } = new();
        public List<ValidationWarning> Warnings { get; } = new();

        public void AddError(string message, Type? sourceType = null, Type? destinationType = null, string? memberName = null)
        {
            Errors.Add(new ValidationError(message, sourceType, destinationType, memberName));
        }

        public void AddWarning(string message, Type? sourceType = null, Type? destinationType = null, string? memberName = null)
        {
            Warnings.Add(new ValidationWarning(message, sourceType, destinationType, memberName));
        }

        public override string ToString()
        {
            var lines = new List<string>();
            
            if (Errors.Any())
            {
                lines.Add("ERRORS:");
                lines.AddRange(Errors.Select(e => $"  - {e}"));
            }
            
            if (Warnings.Any())
            {
                if (lines.Any()) lines.Add("");
                lines.Add("WARNINGS:");
                lines.AddRange(Warnings.Select(w => $"  - {w}"));
            }

            return string.Join(Environment.NewLine, lines);
        }
    }

    public record ValidationError(string Message, Type? SourceType = null, Type? DestinationType = null, string? MemberName = null)
    {
        public override string ToString()
        {
            var context = "";
            if (SourceType != null && DestinationType != null)
            {
                context = $" [{SourceType.Name} -> {DestinationType.Name}";
                if (!string.IsNullOrEmpty(MemberName))
                    context += $".{MemberName}";
                context += "]";
            }
            return $"{Message}{context}";
        }
    }

    public record ValidationWarning(string Message, Type? SourceType = null, Type? DestinationType = null, string? MemberName = null)
    {
        public override string ToString()
        {
            var context = "";
            if (SourceType != null && DestinationType != null)
            {
                context = $" [{SourceType.Name} -> {DestinationType.Name}";
                if (!string.IsNullOrEmpty(MemberName))
                    context += $".{MemberName}";
                context += "]";
            }
            return $"{Message}{context}";
        }
    }
}