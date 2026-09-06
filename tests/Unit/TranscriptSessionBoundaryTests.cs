// SPDX-License-Identifier: GPL-3.0-or-later
using Foundry.Application;
using Foundry.Contracts;

namespace Foundry.Tests.Unit;

public sealed class TranscriptSessionBoundaryTests
{
    [Fact]
    public void A_token_view_cannot_replace_an_uncertainty_with_an_unreviewed_resolution()
    {
        var session = UncertainSession();
        var view = Assert.IsType<IList<TranscriptToken>>(session.Tokens, exactMatch: false);
        var replacement = view[0] with
        {
            State = TranscriptTokenState.Resolved,
            ResolvedText = "Unreviewed synthetic replacement",
        };

        Assert.Throws<NotSupportedException>(() => view[0] = replacement);
        Assert.Equal(1, session.UnresolvedCount);
        Assert.Throws<InvalidOperationException>(session.VerifiedLines);
    }

    [Fact]
    public void A_token_view_cannot_remove_uncertainty_to_make_an_empty_session_complete()
    {
        var session = UncertainSession();
        var view = Assert.IsType<IList<TranscriptToken>>(session.Tokens, exactMatch: false);

        Assert.Throws<NotSupportedException>(view.Clear);
        Assert.Single(session.Tokens);
        Assert.False(session.IsComplete);
        Assert.Throws<InvalidOperationException>(session.VerifiedWords);
    }

    [Fact]
    public void A_retained_token_view_observes_only_the_sessions_explicit_resolution()
    {
        var session = UncertainSession();
        var view = session.Tokens;

        session.Resolve(0, "Verified synthetic word");

        Assert.Equal(TranscriptTokenState.Resolved, view[0].State);
        Assert.Equal("Verified synthetic word", view[0].ResolvedText);
        Assert.Equal(["Verified synthetic word"], session.VerifiedLines());
        Assert.True(session.IsComplete);
    }

    private static TranscriptSession UncertainSession()
        => new(new OcrResult([new OcrToken("Synthetic", 0.4)]));
}
