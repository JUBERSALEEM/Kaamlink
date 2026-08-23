using System.Collections.Concurrent;
using KaamLink.Application.Abstractions;

namespace KaamLink.IntegrationTests;

/// <summary>Test double for IOtpSender: captures codes per phone so tests can complete the verify step without an SMS provider.</summary>
public sealed class CapturingOtpSender : IOtpSender
{
    private readonly ConcurrentDictionary<string, string> _codesByPhone = new();

    public Task SendAsync(string phoneNumber, string code, CancellationToken ct = default)
    {
        _codesByPhone[phoneNumber] = code;
        return Task.CompletedTask;
    }

    public string GetCodeFor(string phoneNumber) =>
        _codesByPhone.TryGetValue(phoneNumber, out var code)
            ? code
            : throw new InvalidOperationException($"No OTP was captured for {phoneNumber}.");

    public void Clear() => _codesByPhone.Clear();
}
