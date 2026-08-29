// SPDX-License-Identifier: GPL-3.0-or-later
using Foundry.Contracts;
using Foundry.Domain;

namespace Foundry.Infrastructure.Simulated;

/// <summary>
/// The OCR stand-in for CI and offline development, like the synthetic provider:
/// deterministic scripted tokens with confidences. The real local OCR engine is
/// implementation-plan §20 decision 4 (feasibility spike) and replaces this behind
/// the same seam.
/// </summary>
public sealed class FakeOcrService(params OcrToken[] script) : IOcrService
{
    public Task<OcrResult> RecognizeAsync(SourceEnvelope source, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(new OcrResult([.. script]));
    }
}
