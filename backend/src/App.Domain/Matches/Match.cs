using App.Domain.Content;
using App.Domain.Progress;
using App.Domain.Users;

namespace App.Domain.Matches;

public enum MatchKind { Ranked, Ghost, Bot, Friend, Practice, Calibration }

public enum MatchPhase { VetoCoin, VetoBan, VetoPick, Lobby, Question, Interstitial, Done }

public enum MatchResultReason { Score, TiebreakCorrect, TiebreakTime, Draw, Resign, Disconnect, Aborted }

public enum SeatKind { Human, Bot, Ghost }

public enum MatchSlot { Warmup, Targeted, Core, Review }

// One match of any kind (docs §3.4). The deck is frozen in Questions; every seat's answers are in Answers.
public class Match
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required MatchKind Kind { get; init; }
    public MatchPhase Phase { get; set; } = MatchPhase.Lobby;
    public DateTimeOffset? PhaseDeadline { get; set; } // the tick acts when it passes; null while untimed practice waits for "next"
    public Guid? TopicId { get; set; }
    public bool Timed { get; init; } = true;
    public int BandMin { get; set; }
    public int BandMax { get; set; }
    public int CurrentIndex { get; set; }
    public DateTimeOffset? QuestionStartedAt { get; set; }
    public required int RngSeed { get; init; }
    public int? WinnerSeat { get; set; }
    public MatchResultReason? ResultReason { get; set; }
    public required DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public List<MatchPlayer> Players { get; } = [];
    public List<MatchQuestion> Questions { get; } = [];
    public List<MatchAnswer> Answers { get; } = [];

    public AnswerSource Source => Kind switch
    {
        MatchKind.Ranked => AnswerSource.Ranked,
        MatchKind.Ghost => AnswerSource.Ghost,
        MatchKind.Bot => AnswerSource.Bot,
        MatchKind.Friend => AnswerSource.Friend,
        MatchKind.Practice => AnswerSource.Practice,
        _ => AnswerSource.Calibration,
    };

    public MatchQuestion Current => Questions.First(q => q.Index == CurrentIndex);

    public MatchAnswer? AnswerOf(int seat, int index) => Answers.FirstOrDefault(a => a.Seat == seat && a.Index == index);

    // An answer is visible to the other seat only once its question has reached the interstitial.
    public bool IsRevealed(int index) => index < CurrentIndex || Phase is MatchPhase.Interstitial or MatchPhase.Done;
}

public class MatchPlayer
{
    public Guid MatchId { get; init; }
    public required int Seat { get; init; }
    public required SeatKind Kind { get; init; }
    public Guid? UserId { get; init; }
    public User? User { get; init; }
    public BotPersona? BotPersona { get; init; }
    public required int DisplayRating { get; init; }
    public int Score { get; set; } // final totals, written when the match finishes
    public int CorrectCount { get; set; }
    public int TimeOnCorrectMs { get; set; }
    public DateTimeOffset? LastSeenAt { get; set; }
    public int? DeliveredIndex { get; set; } // last question a GET returned to this seat, and when
    public DateTimeOffset? DeliveredAt { get; set; }
    public DateTimeOffset? ResignedAt { get; set; }
    public int SpEarned { get; set; }
    public int CoinsEarned { get; set; }
    public int XpEarned { get; set; }

    public bool IsConnected(DateTimeOffset now) =>
        Kind != SeatKind.Human || LastSeenAt >= now.AddSeconds(-GameRules.ReconnectGraceSeconds);
}

public class MatchQuestion
{
    public Guid MatchId { get; init; }
    public required int Index { get; init; }
    public required Guid QuestionId { get; init; }
    public Question? Question { get; init; }
    public required Guid ConceptId { get; init; }
    public required MatchSlot Slot { get; init; }
    public required QuestionType Type { get; init; }
    public required int Difficulty { get; init; }
    public required int BasePoints { get; init; }
    public required int TimeLimitMs { get; init; }
}

public class MatchAnswer
{
    public Guid MatchId { get; init; }
    public required int Seat { get; init; }
    public required int Index { get; init; }
    public string? Payload { get; init; } // jsonb; null = timed out
    public required bool IsCorrect { get; init; }
    public required double Fraction { get; init; }
    public required int Points { get; init; }
    public required int ElapsedMs { get; init; }
    public bool TimedOut { get; init; }
    public required DateTimeOffset AnsweredAt { get; init; } // bot and ghost answers are written ahead and arrive at this time
    public required AnswerLabel Label { get; init; }
}
