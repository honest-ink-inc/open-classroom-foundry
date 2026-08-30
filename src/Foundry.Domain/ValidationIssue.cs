// SPDX-License-Identifier: GPL-3.0-or-later
namespace Foundry.Domain;

public enum ValidationSeverity
{
    Info = 0,
    Warning = 1,
    Blocking = 2,
}

/// <summary>
/// One finding from a validator. Blocking issues make an artifact unapprovable
/// (constitution: fail closed; uncertainty is marked, never plausibly completed).
/// </summary>
public sealed record ValidationIssue(
    ValidationSeverity Severity,
    string Code,
    string Message,
    bool RequiresAcknowledgement = false)
{
    public static ValidationIssue Info(string code, string message) => new(ValidationSeverity.Info, code, message);

    public static ValidationIssue Warning(
        string code,
        string message,
        bool requiresAcknowledgement = false)
        => new(ValidationSeverity.Warning, code, message, requiresAcknowledgement);

    public static ValidationIssue Blocking(string code, string message) => new(ValidationSeverity.Blocking, code, message);
}
