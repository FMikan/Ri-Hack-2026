using System.Text.Json;
using App.Domain.Content;
using App.Domain.Progress;

namespace App.Domain.Matches;

// A human answer that also feeds the learning loop (mastery + answers log). Timeouts while disconnected do not.
public record RecordedAnswer(MatchPlayer Player, MatchQuestion Question, MatchAnswer Answer, bool CountsForLearning);

// The match state machine (docs §4.3). Pure: time comes in as `now`, randomness from the match seed.
// Ranked starts with the veto (coin → bans → pick); every other kind starts in the lobby.
public static class MatchEngine
{
    public static Match Create(
        MatchKind kind, Guid topicId, bool timed, (int Min, int Max) band, int seed,
        IEnumerable<MatchPlayer> players, IReadOnlyList<DeckSlot> deck, IReadOnlyDictionary<Guid, Question> questions, DateTimeOffset now)
    {
        var match = new Match
        {
            Kind = kind, Timed = timed, RngSeed = seed, CreatedAt = now,
            Phase = MatchPhase.Lobby, PhaseDeadline = now.AddSeconds(GameRules.LobbySeconds),
        };
        Seat(match, players, now);
        AttachDeck(match, band, deck, questions, topicId);
        // Untimed practice has no countdown to wait for.
        if (!timed) StartQuestion(match, 0, now);
        return match;
    }

    // Ranked: the coin flip animation first; the topic and the deck come out of the veto.
    public static Match CreateRanked(IEnumerable<MatchPlayer> players, VetoState veto, int seed, DateTimeOffset now)
    {
        var match = new Match
        {
            Kind = MatchKind.Ranked, RngSeed = seed, CreatedAt = now, Veto = veto,
            Phase = MatchPhase.VetoCoin, PhaseDeadline = now.AddSeconds(GameRules.VetoAnimationSeconds),
        };
        Seat(match, players, now);
        return match;
    }

    // The frozen deck of a match (idea §8, rule 6). Ranked gets it once the veto has chosen the topic.
    public static void AttachDeck(Match match, (int Min, int Max) band, IReadOnlyList<DeckSlot> deck, IReadOnlyDictionary<Guid, Question> questions, Guid? topicId = null)
    {
        match.TopicId = topicId ?? match.TopicId;
        (match.BandMin, match.BandMax) = band;
        match.Questions.AddRange(deck.Select((slot, index) => new MatchQuestion
        {
            Index = index,
            QuestionId = slot.Question.Id,
            Question = questions[slot.Question.Id],
            ConceptId = slot.Question.ConceptId,
            Slot = slot.Slot,
            Type = slot.Question.Type,
            Difficulty = slot.Question.Difficulty,
            BasePoints = GameRules.Types[slot.Question.Type].BasePoints,
            TimeLimitMs = GameRules.Types[slot.Question.Type].TimeMs,
        }));
    }

    public static void Ban(Match match, int seat, Guid topicId, DateTimeOffset now)
    {
        if (match.Phase != MatchPhase.VetoBan || match.PhaseDeadline < now)
            throw new AppException(ErrorCodes.MatchWrongPhase, "Banning is closed.");
        Veto.Ban(match.Veto!, seat, topicId);
        match.Players.First(p => p.Seat == seat).LastSeenAt = now;
        AfterBan(match, now);
    }

    private static void Seat(Match match, IEnumerable<MatchPlayer> players, DateTimeOffset now)
    {
        match.Players.AddRange(players);
        foreach (var human in match.Players.Where(p => p.Kind == SeatKind.Human)) human.LastSeenAt ??= now;
    }

    // Two topics left: one is drawn now and shown for the 3 s pick animation; otherwise the other player bans.
    private static void AfterBan(Match match, DateTimeOffset at)
    {
        var veto = match.Veto!;
        if (veto.FinalTwo is null)
        {
            match.PhaseDeadline = at.AddSeconds(GameRules.VetoBanSeconds);
            return;
        }
        match.TopicId = Veto.Pick(veto, VetoRandom(match));
        match.Phase = MatchPhase.VetoPick;
        match.PhaseDeadline = at.AddSeconds(GameRules.VetoAnimationSeconds);
    }

    // Seeded by the match and the veto step, so a replay of the same bans gives the same auto-bans and pick.
    private static Random VetoRandom(Match match) => new(match.RngSeed + 97 * (match.Veto!.Bans.Count + 1));

    // Runs every transition whose deadline has passed, each at its own deadline, so a late tick catches up exactly.
    public static List<RecordedAnswer> Advance(Match match, DateTimeOffset now)
    {
        var recorded = new List<RecordedAnswer>();
        while (match.Phase != MatchPhase.Done && match.PhaseDeadline is { } deadline && deadline <= now)
        {
            switch (match.Phase)
            {
                case MatchPhase.VetoCoin:
                    match.Phase = MatchPhase.VetoBan;
                    if (match.Veto!.FinalTwo is null) match.PhaseDeadline = deadline.AddSeconds(GameRules.VetoBanSeconds);
                    else AfterBan(match, deadline);
                    break;
                case MatchPhase.VetoBan:
                    Veto.AutoBan(match.Veto!, VetoRandom(match));
                    AfterBan(match, deadline);
                    break;
                case MatchPhase.VetoPick:
                    // The deck needs the database: the caller attaches it (AttachDeck) and advances again.
                    if (match.Questions.Count == 0) return recorded;
                    match.Phase = MatchPhase.Lobby;
                    match.PhaseDeadline = deadline.AddSeconds(GameRules.LobbySeconds);
                    break;
                case MatchPhase.Lobby:
                    StartQuestion(match, 0, deadline);
                    break;
                case MatchPhase.Question:
                    CloseQuestion(match, deadline, recorded);
                    break;
                case MatchPhase.Interstitial:
                    NextQuestionOrFinish(match, deadline);
                    break;
                default:
                    throw new InvalidOperationException($"Phase {match.Phase} has no deadline.");
            }
        }
        return recorded;
    }

    public static RecordedAnswer SubmitAnswer(Match match, int seat, int index, JsonElement payload, int clientElapsedMs, DateTimeOffset now)
    {
        if (match.Phase != MatchPhase.Question || match.PhaseDeadline < now)
            throw new AppException(ErrorCodes.MatchWrongPhase, "This question is no longer open.");
        if (index != match.CurrentIndex)
            throw new AppException(ErrorCodes.MatchWrongIndex, "This is not the current question.");
        if (match.AnswerOf(seat, index) is not null)
            throw new AppException(ErrorCodes.MatchAlreadyAnswered, "You already answered this question.");

        var player = match.Players.First(p => p.Seat == seat);
        var question = match.Current;
        // Client-measured time is trusted within the tolerance; otherwise the server's own measurement wins (idea §17).
        var shownAt = player.DeliveredIndex == index && player.DeliveredAt is { } delivered ? delivered : match.QuestionStartedAt!.Value;
        var serverMs = (int)(now - shownAt).TotalMilliseconds;
        var elapsed = Math.Abs(serverMs - clientElapsedMs) <= GameRules.ClientTimeToleranceMs ? clientElapsedMs : serverMs;
        elapsed = Math.Clamp(elapsed, 0, match.Timed ? question.TimeLimitMs : int.MaxValue);

        var answer = Grade(match, question, seat, payload.ValueKind == JsonValueKind.Undefined ? null : payload.GetRawText(), payload, elapsed, now);
        match.Answers.Add(answer);
        player.LastSeenAt = now;

        var others = match.Players.Where(p => p.Seat != seat).Select(p => match.AnswerOf(p.Seat, index)).ToList();
        if (others.All(a => a is not null && a.AnsweredAt <= now))
            CloseQuestion(match, now, []);
        else if (match.Timed && others.All(a => a is not null))
            // Only bots or ghosts are left and their answers are already written: open the interstitial when the last one arrives.
            match.PhaseDeadline = others.Max(a => a!.AnsweredAt);

        return new RecordedAnswer(player, question, answer, CountsForLearning: true);
    }

    // Untimed practice: the player moves on when ready.
    public static void Next(Match match, DateTimeOffset now)
    {
        if (match.Phase != MatchPhase.Interstitial || match.Timed)
            throw new AppException(ErrorCodes.MatchWrongPhase, "Next is only for untimed practice between questions.");
        NextQuestionOrFinish(match, now);
    }

    public static void Resign(Match match, int seat, DateTimeOffset now)
    {
        if (match.Phase == MatchPhase.Done)
            throw new AppException(ErrorCodes.MatchWrongPhase, "The match is already over.");
        match.Players.First(p => p.Seat == seat).ResignedAt = now;
        Finish(match, now, resignedSeat: seat);
    }

    private static void StartQuestion(Match match, int index, DateTimeOffset at)
    {
        match.CurrentIndex = index;
        match.QuestionStartedAt = at;
        match.StartedAt ??= at;
        match.Phase = MatchPhase.Question;
        var question = match.Current;
        match.PhaseDeadline = match.Timed ? at.AddMilliseconds(question.TimeLimitMs + GameRules.ClientTimeToleranceMs) : null;

        // A ghost replays its recording: the same answer, points and label, arriving at the recorded elapsed time
        // (a recorded timeout arrives at this question's deadline).
        foreach (var ghost in match.Players.Where(p => p.Kind == SeatKind.Ghost))
        {
            var recorded = match.Recording!.First(a => a.Index == index);
            var elapsed = recorded.TimedOut ? question.TimeLimitMs : Math.Min(recorded.ElapsedMs, question.TimeLimitMs);
            match.Answers.Add(new MatchAnswer
            {
                Seat = ghost.Seat, Index = index, Payload = recorded.Payload, IsCorrect = recorded.IsCorrect, Fraction = recorded.Fraction,
                Points = recorded.Points, ElapsedMs = elapsed, TimedOut = recorded.TimedOut, Label = recorded.Label,
                AnsweredAt = recorded.TimedOut ? match.PhaseDeadline!.Value : at.AddMilliseconds(elapsed),
            });
        }

        foreach (var bot in match.Players.Where(p => p.Kind == SeatKind.Bot))
        {
            var rng = new Random(match.RngSeed + 31 * index + bot.Seat);
            var (payload, elapsed) = BotPlayer.Answer(question.Type, question.Question!.Content, question.Difficulty, bot.BotPersona!.Value, question.TimeLimitMs, rng);
            var json = payload.ToJsonString();
            using var document = JsonDocument.Parse(json);
            match.Answers.Add(Grade(match, question, bot.Seat, json, document.RootElement, elapsed, at.AddMilliseconds(elapsed)));
        }
    }

    private static void CloseQuestion(Match match, DateTimeOffset at, List<RecordedAnswer> recorded)
    {
        var question = match.Current;
        foreach (var player in match.Players.Where(p => match.AnswerOf(p.Seat, match.CurrentIndex) is null))
        {
            var timeout = new MatchAnswer
            {
                Seat = player.Seat, Index = match.CurrentIndex, IsCorrect = false, Fraction = 0, Points = 0,
                ElapsedMs = question.TimeLimitMs, TimedOut = true, AnsweredAt = at, Label = AnswerLabel.Miss,
            };
            match.Answers.Add(timeout);
            if (player.Kind == SeatKind.Human)
                recorded.Add(new RecordedAnswer(player, question, timeout, CountsForLearning: player.IsConnected(at)));
        }
        match.Phase = MatchPhase.Interstitial;
        match.PhaseDeadline = match.Timed ? at.AddSeconds(GameRules.InterstitialSeconds) : null;
    }

    private static void NextQuestionOrFinish(Match match, DateTimeOffset at)
    {
        if (match.CurrentIndex + 1 < match.Questions.Count) StartQuestion(match, match.CurrentIndex + 1, at);
        else Finish(match, at);
    }

    private static void Finish(Match match, DateTimeOffset at, int? resignedSeat = null)
    {
        match.Phase = MatchPhase.Done;
        match.PhaseDeadline = null;
        match.FinishedAt = at;
        foreach (var player in match.Players)
        {
            var answers = match.Answers.Where(a => a.Seat == player.Seat && a.AnsweredAt <= at).ToList();
            player.Score = answers.Sum(a => a.Points);
            player.CorrectCount = answers.Count(a => a.IsCorrect);
            player.TimeOnCorrectMs = answers.Where(a => a.IsCorrect).Sum(a => a.ElapsedMs);
        }

        if (match.Players.Count == 1)
        {
            match.ResultReason = resignedSeat is null ? MatchResultReason.Score : MatchResultReason.Resign;
            return;
        }
        if (resignedSeat is { } resigned)
        {
            match.WinnerSeat = 1 - resigned;
            match.ResultReason = MatchResultReason.Resign;
            return;
        }

        // Idea §17: a human who is gone at the end loses; if every human is gone the match is void.
        var humans = match.Players.Where(p => p.Kind == SeatKind.Human).ToList();
        var gone = humans.Where(p => !p.IsConnected(at)).ToList();
        if (gone.Count > 0 && gone.Count == humans.Count && humans.Count > 1)
        {
            match.ResultReason = MatchResultReason.Aborted;
            return;
        }
        if (gone.Count == 1)
        {
            match.WinnerSeat = 1 - gone[0].Seat;
            match.ResultReason = MatchResultReason.Disconnect;
            return;
        }
        (match.WinnerSeat, match.ResultReason) = Tiebreak.Of(match.Players[0], match.Players[1]);
    }

    private static MatchAnswer Grade(Match match, MatchQuestion question, int seat, string? payloadJson, JsonElement payload, int elapsed, DateTimeOffset answeredAt)
    {
        var fraction = Grader.Grade(question.Type, QuestionJson.Answer(question.Question!.Content), payload);
        return new MatchAnswer
        {
            Seat = seat,
            Index = question.Index,
            Payload = payloadJson,
            IsCorrect = fraction >= 1,
            Fraction = fraction,
            Points = Scoring.Points(question.BasePoints, fraction, elapsed, question.TimeLimitMs, match.Timed),
            ElapsedMs = elapsed,
            AnsweredAt = answeredAt,
            Label = Labels.Of(question.Difficulty, fraction, elapsed, question.TimeLimitMs, timedOut: false, match.Timed),
        };
    }
}

public static class Tiebreak
{
    // Points, then more correct answers, then less time spent on correct answers; otherwise a draw (idea §6).
    public static (int? WinnerSeat, MatchResultReason Reason) Of(MatchPlayer a, MatchPlayer b)
    {
        if (a.Score != b.Score) return (a.Score > b.Score ? a.Seat : b.Seat, MatchResultReason.Score);
        if (a.CorrectCount != b.CorrectCount) return (a.CorrectCount > b.CorrectCount ? a.Seat : b.Seat, MatchResultReason.TiebreakCorrect);
        if (a.TimeOnCorrectMs != b.TimeOnCorrectMs) return (a.TimeOnCorrectMs < b.TimeOnCorrectMs ? a.Seat : b.Seat, MatchResultReason.TiebreakTime);
        return (null, MatchResultReason.Draw);
    }
}

public record ResultCards(int? BlunderIndex, int? BrilliantIndex, Guid? PracticeConceptId, double PracticeConceptDelta);

public static class ResultCardRules
{
    // Blunder: the costliest miss. Brilliant: the best answer. Practice: the concept whose mastery dropped the most (idea §10).
    public static ResultCards Of(Match match, int seat, IReadOnlyDictionary<Guid, double> masteryDeltaByConcept)
    {
        var mine = match.Answers.Where(a => a.Seat == seat).Join(match.Questions, a => a.Index, q => q.Index, (a, q) => (Answer: a, Question: q)).ToList();
        var blunder = mine
            .Where(x => x.Answer.Fraction < 1)
            .OrderByDescending(x => x.Answer.Label == AnswerLabel.Blunder)
            .ThenByDescending(x => x.Question.BasePoints * (1 - x.Answer.Fraction))
            .ThenBy(x => x.Question.Index)
            .Select(x => (int?)x.Question.Index)
            .FirstOrDefault();
        var brilliant = mine
            .Where(x => x.Answer.Points > 0)
            .OrderByDescending(x => x.Answer.Label == AnswerLabel.Brilliant)
            .ThenByDescending(x => x.Answer.Points)
            .ThenBy(x => x.Question.Index)
            .Select(x => (int?)x.Question.Index)
            .FirstOrDefault();
        var drop = masteryDeltaByConcept.Where(d => d.Value < 0).OrderBy(d => d.Value).Select(d => (KeyValuePair<Guid, double>?)d).FirstOrDefault();
        return new ResultCards(blunder, brilliant, drop?.Key, drop?.Value ?? 0);
    }
}
