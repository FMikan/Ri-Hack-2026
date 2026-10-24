# Ri-Hack 4.0 — Tehnički dokument: baza, backend, API ugovor, TDD

Verzija 2 · 24. 10. 2026 · izvedeno iz `docs/Ri-Hack 4.0 — IDEJA IGRE v2.md` (dalje: *ideja*, reference `§n` pokazuju na njezina poglavlja). v2 ugrađuje tri neovisna reviewa (pokrivenost ideje, baza/konkurentnost/matematika, API ugovor/TDD/mobile).

Ovaj dokument je ugovor između backenda i frontenda. Backend se gradi test-first po popisu testova iz §9; frontend radi protiv DTO-a iz §6 i ne čeka backend. Sve brojke iz ideje (timeri, bodovi, K-faktor, intervali) žive na jednom mjestu u kodu (`GameRules`, §4.1) i mijenjaju se bez diranja logike.

## 0. Ključne odluke

| Odluka | Izbor | Zašto |
| --- | --- | --- |
| Realtime u duelu | REST komande + `GET /api/matches/{id}` polling svake 1 s; server tick svake 1 s pokreće istek vremena. SignalR kasnije samo kao "state se promijenio, refetchaj" | Sva logika je testabilna običnim HTTP testovima; timer po igraču kreće na prikazu (§6 ideje) pa 1 s latencija ne utječe na fer. Radi identično u Capacitor Android aplikaciji |
| Autentikacija | E-mail + lozinka, JWT access token (15 min) u `Authorization: Bearer`, rotirajući refresh token (30 dana) u bazi s detekcijom ponovne upotrebe, reset lozinke e-mailom. OAuth je roadmap | Bez cookieja: isti kod radi u browseru i u Android WebViewu. `PasswordHasher<User>` i JWT su u ASP.NET Core paketima, jedan NuGet (`Microsoft.AspNetCore.Authentication.JwtBearer`) |
| Izvor istine za statistiku | Append-only tablica `answers` (svaki odgovor svakog korisnika s punim kontekstom) + `reward_events` ledger (svaki coin/XP/SP s jedinstvenim `dedupe_key`) | Nova statistika = novi upit, ne nova shema. Nagrade se nikad ne dodjeljuju dvaput |
| Deck, bodovanje, Elo, mastery, matchmaking | Čiste funkcije u `App.Domain` bez ovisnosti o bazi; use case samo učita ulaze i spremi izlaze | Unit testovi u milisekundama; sve brojke iz ideje provjerene testom |
| Sadržaj pitanja | `jsonb` stupac `content` s poljem `answer` (ključ); API skida `answer` dok nije vrijeme za otkrivanje; konkretni C# record bira se po stupcu `questions.type` | 8 tipova pitanja bez 8 tablica; novi tip = novi record + grader + renderer |
| ID-evi | `uuid` v7 (`Guid.CreateVersion7()`), kao postojeći `Note` | Neizbrojivi iz API-ja, vremenski sortirani |
| Imena u bazi | `snake_case` preko `EFCore.NamingConventions` (`UseSnakeCaseNamingConvention()`) | Baza koju se ručno gleda u psql-u; jedan paket, jedna linija |
| Enumi | `text` + `CHECK`; jedan zajednički EF konverter enum ↔ `snake_case` string i `JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower)` u HTTP JSON-u | Ista vrijednost (`true_false`) u bazi, u API-ju i u CHECK-u |
| Vrijeme | `TimeProvider` (stdlib) injektiran svugdje; svaki `created_at` upisuje kod iz `TimeProvider`-a (ne `default now()`); `timestamptz` u bazi; "dan" i "tjedan" računa `Clock` helper u zoni `Europe/Zagreb` | Testovi pomiču vrijeme bez čekanja i bez raskoraka sa satom baze |
| Slučajnost | `matches.rng_seed` + `Random(seed)` | Deck i auto-ban reproducibilni u testovima i u ghost duelu |
| Pozadinski posao | Jedan `BackgroundService` (`GameTickService`, `PeriodicTimer`, try/catch po iteraciji i po partiji) zove use caseove `AdvanceMatches` (1 s), `RunMatchmaking` (2 s), `Housekeeping` (60 s) | Nema Hangfire/Quartz; use case se iz testa zove direktno s lažnim vremenom; jedna loša partija ne ruši API |
| Konkurentnost | Svaka mutacija partije/ponude počinje s `SELECT ... FOR UPDATE` nad redom `matches`/`match_proposals` (tick: `SKIP LOCKED`); brojači na `users` idu kroz `ExecuteUpdateAsync`; nema `xmin` | Dva istodobna odgovora ili prihvata se serijaliziraju; `xmin` ne vidi inserte u `match_answers` pa ne rješava taj race |
| Jedna aktivna partija po korisniku | `users.current_match_id`, postavlja se atomarno (`UPDATE ... WHERE current_match_id IS NULL`), briše u `FinishMatch`; ulazak u partiju napušta red | Dvoklik ne stvara dvije partije; igrač u bot duelu se ne uparuje |
| AI | `Anthropic` NuGet, model `claude-opus-5-5`, structured output; svaki poziv iza sučelja s fallback implementacijom i timeoutom `Ai:TimeoutMs` (8000) | AI nikad ne blokira igru (§16 ideje); testovi koriste fake s timeoutom 50 ms |
| Testna baza | Pravi PostgreSQL kroz `Testcontainers.PostgreSql` u `App.Api.Tests`; `TRUNCATE` svih tablica prije svakog testa; paralelizacija testova isključena u tom projektu | `jsonb`, `FOR UPDATE` i indeksi se ne mogu simulirati in-memory providerom; CI runner ima Docker |

## 1. Opseg: što ide u hackathon, što je roadmap

| Faza | Sadržaj | Rezultat koji se može pokazati | Stanje backenda |
| --- | --- | --- | --- |
| **F0 Auth i profil** | register, login, refresh (rotacija + opoziv obitelji), logout, `/api/me`, odabir 3–5 tema, teme i koncepti, dashboard | Korisnik se registrira i vidi dashboard s temama | gotovo, u `development` |
| **F1 Sadržaj i bot duel** | seed pitanja, deck builder, grading svih 8 tipova, bodovanje, mastery, bot persone, ekran rezultata, povijest partija | Cijeli duel protiv bota od početka do kraja; jezgreni loop bez drugog čovjeka | gotovo, u `development` |
| **F0b Auth ostatak** | forgot/reset lozinke, rate limit, forwarded headers | Zaboravljena lozinka radi; login je zaštićen od pogađanja | gotovo, u `development` |
| **F2 Analiza, poduka, mapa znanja** | match review s oznakama, AI objašnjenje s kešom i fallbackom, poduka s kontrolnim pitanjima, red ponavljanja, mapa znanja, kalibracija, trening (po temi i po konceptu) | *Duel → analiza → poduka → zakrpano* radi; žiri vidi učenje | gotovo, PR `feature/f2-review-lessons` (nagrade poduke se knjiže u F4) |
| **F3 Rangirano 1v1** | red čekanja, prihvat para, veto, živi duel, Elo, tierovi, SP, odustajanje, prekid veze, revanš, ghost duel | Dva laptopa igraju jedno protiv drugog | nije počelo |
| **F4 Ekonomija i dnevni ciklus** | ledger, coins/XP/level, streak, dnevni zadaci, dnevni izazov, shop, oprema | Loop zaradi → potroši | nije počelo |
| **F5 Društveno i rang-liste** | leaderboardi (5), Grandmaster, sezone, titule, achievementi, prijatelji, izazov prijatelja, javni profil, statistika, auto-kalibracija težine, povlačenje pitanja | Sve iz §14 ideje | nije počelo |
| Roadmap | OAuth, verifikacija e-maila, push obavijesti, emoti, premium (tutor, još pitanja, dublji uvid, objasni svojim riječima, napredna statistika), hrvatski pool pitanja, offline batch generator, SignalR, Android | — | — |

Stupac "Stanje backenda" se ažurira u PR-u svake faze; što frontend može spojiti po fazama je u §6.11.

F0b dolazi nakon F1 namjerno: prvi igrivi demo (bot duel) je najvažniji rizik. Refresh je ušao u F0 jer ga frontend treba od prvog ekrana. Frontend može ići paralelno od prvog dana: svi DTO-i su u §6, a redoslijed endpointa prati faze.

## 2. Arhitektura

### 2.1 Slojevi (postojeći, nepromijenjeni)

```text
App.Api            Endpoints/<Feature>Endpoints.cs, auth, rate limit, forwarded headers, GameTickService registracija, Program.cs
App.Application    use case klase po featureu, DTO-i (DataAnnotations), IAppDbContext, sučelja za vanjski svijet
App.Domain         entiteti + čista pravila igre (GameRules, Scoring, Elo, Grader, Mastery, DeckBuilder, Veto, Matchmaking, BotPlayer, Clock, ...)
App.Infrastructure AppDbContext + Configurations/, migracije, EmailSender, ClaudeClient, Seeder
```

Ovisnosti ostaju `Api → Application, Infrastructure`; `Infrastructure → Application, Domain`; `Application → Domain`. `PasswordHasher<User>` (Microsoft.Extensions.Identity.Core, shared framework) se koristi iz Application kroz `IPasswordHasher` sučelje, Domain ga ne vidi.

### 2.2 Mape po featureu

```text
App.Domain/
  GameRules.cs                     sve konstante iz ideje (§4.1)
  Clock.cs                         Today(now), WeekStart(now), DayStart(day) u GameRules.DayTimeZone
  Users/        User, RefreshToken, PasswordResetToken, Tier, Level
  Content/      Topic, Concept, Question, QuestionContent (8 recorda), QuestionType, Difficulty, QuestionReport, Grader
  Progress/     ConceptMastery, Answer, AnswerSource, Mastery (formula), ReviewSchedule, ConceptState, AnswerLabel, Labels
  Matches/      Match, MatchPlayer, MatchQuestion, MatchAnswer, MatchKind, MatchPhase, Scoring, Elo, DeckBuilder, Veto, BotPlayer, BotPersona, Tiebreak, ResultCards, MatchEngine
  Matchmaking/  QueueEntry, MatchProposal, MatchmakingRules, GhostOffer
  Lessons/      Lesson, LessonCard, LessonRules
  Economy/      RewardEvent, RewardType, Rewards, ShopItem, UserItem
  Daily/        DailyChallenge, DailyChallengeAttempt, DailyTasks, UserDailyTask, ActivityDay, Streak
  Social/       Friendship, FriendChallenge
  Seasons/      Season, UserSeason, Title, UserTitle, AchievementRules, UserAchievement, WeeklyResult
App.Application/                   jedna servisna klasa po featureu (AuthService, MeService, ContentService, ...) s metodom po use caseu, kao starterov NoteService
  IAppDbContext.cs, AppException.cs (ErrorCodes)
  Auth/         AuthService (register, login, refresh, logout; F0b: forgot/reset), ITokenService + IPasswordHasher, IEmailSender, AuthDtos
  Me/           MeService (get, update, topics, dashboard; kasnije knowledge map, stats, matches), MeDtos
  Content/      ContentService (topics, concepts, question, explanation, report; kasnije RetireQuestion, RecalibrateDifficulty), IExplanationGenerator, QuestionView
  Matches/      StartBotMatch, StartPracticeMatch, StartCalibration, GetMatchState, BanTopic, SubmitAnswer, NextQuestion, ResignMatch, AdvanceMatches, FinishMatch, GetMatchReview, RequestRematch, AcceptRematch, MatchStateMapper, MatchLock
  Matchmaking/  JoinQueue, GetQueueState, LeaveQueue, AcceptProposal, RunMatchmaking, AcceptGhostOffer, DeclineGhostOffer
  Lessons/      LessonService (start, get, check), ILessonGenerator, LessonDtos
  Economy/      Rewarder (jedina klasa koja piše u reward_events), GetShop, PurchaseItem, SetEquipment
  Daily/        GetDailyChallenge, AttemptDailyChallenge, GetDailyTasks, RecordActivity (streak)
  Social/       friends + friend challenges use caseovi
  Leaderboards/ GetLeaderboard
  Seasons/      CloseSeason, CloseWeek, RefreshGrandmasters
App.Infrastructure/
  AppDbContext.cs, Configurations/*.cs (jedna klasa po entitetu), SnakeCaseEnumConverter.cs
  EmailSender.cs, ClaudeAi.cs (oba AI sučelja)
  Seed/Seeder.cs + Seed/topics.json + Seed/questions/<tema>.json (embedded resources, nema problema s putanjama u Dockeru)
App.Api/
  Auth.cs (JWT izdavanje i validacija, PasswordHasher adapter, ClaimsPrincipal.UserId()), AppExceptionHandler.cs (AppException → Problem Details),
  GameTickService.cs (BackgroundService; Infrastructure nema Hosting paket)
App.Api/Endpoints/
  AuthEndpoints, MeEndpoints, ContentEndpoints, MatchEndpoints, QueueEndpoints, LessonEndpoints, EconomyEndpoints, DailyEndpoints, SocialEndpoints, LeaderboardEndpoints, HealthEndpoints
```

`Note`, `NoteService`, `NoteEndpoints`, `NotesExample.tsx` i njihova migracija se brišu u F0 (README to i traži); dva postojeća Notes testa u `ApiTests.cs` postaju isti testovi nad `POST /api/auth/register`.

### 2.3 Tok jednog zahtjeva u duelu

```text
POST /api/matches/{id}/answers
  → endpoint (auth, validacija DTO-a)
  → SubmitAnswer use case:
      transakcija; MatchLock.Take(id)  = SELECT 1 FROM matches WHERE id = @id FOR UPDATE
      učita Match + players + questions + answers (jedan upit)
      MatchEngine.SubmitAnswer(match, seat, index, payload, clientElapsedMs, now)   [čisto, Domain]
         grade → fraction, points, label; zapiše MatchAnswer;
         oba odgovorila → faza interstitial odmah; protivnik bot/ghost s answered_at u budućnosti → phase_deadline = answered_at
      Mastery.Apply(conceptMastery, answer) + ReviewSchedule  [čisto, Domain]  + Answer log red
      SaveChanges → commit → MatchStateMapper.ForViewer(match, seat) → 200 MatchStateDto
```

Isti lock koriste `BanTopic`, `NextQuestion`, `Resign`, `AcceptRematch`, `FinishMatch` i tick (`AdvanceMatches` s `FOR UPDATE SKIP LOCKED`, svaka partija u vlastitoj transakciji). `AcceptProposal` i `LeaveQueue` zaključavaju red `match_proposals`. Dvostruki odgovor dodatno odbija PK `(match_id, seat, index)`. `GET /api/matches/{id}` ne zaključava: čita stanje i jednim `ExecuteUpdateAsync` upiše `match_players.last_seen_at` te, ako vraća novi `current_index` tom sjedalu prvi put, `delivered_index/delivered_at`.

### 2.4 Vrijeme i fer igra

- Server pamti `question_started_at` (pitanje postalo aktivno) i po sjedalu `delivered_at` (prvi `GET` koji je tom igraču vratio to pitanje = "trenutak kad je pitanje poslano", §17 ideje).
- Klijent šalje `clientElapsedMs` (mjereno `performance.now()` od prvog rendera pitanja). Server računa `serverElapsed = now - delivered_at` (ili `- question_started_at` ako `delivered_at` nema). Ako je `|serverElapsed - clientElapsed| <= 1500 ms` prihvaća klijentovo, inače uzima serversko.
- Istek: `phase_deadline = question_started_at + timeLimit + 1500 ms`. Nakon reloada klijent kreće od `serverTime - deliveredAt` i vizualno odbrojavanje reže na `phaseDeadline - serverTime - 500 ms`.
- Istek bez odgovora: red u `match_answers` s `timed_out = true`, 0 bodova, label `miss`; za čovjeka koji je **spojen** i red u `answers` (`payload = null`, `r = 0`, `w = DifficultyWeight`, bez pogodivosti i blundera), mastery pada, pitanje se broji kao viđeno. Za čovjeka koji je **odspojen** (`last_seen_at` stariji od 20 s) samo `match_answers`: mastery i "viđeno" se ne diraju jer pitanje nije vidio.
- Bot i ghost odgovori se zapisuju na početku pitanja s `answered_at = question_started_at + elapsed` u budućnosti; `elapsed` je ograničen na `timeLimit - 500 ms`. "Broje se" tek kad `now >= answered_at`; protivnikov odgovor se nikad ne vraća prije međuekrana. Kad čovjek odgovori prije, `phase_deadline` postaje `answered_at` pa idući tick (≤ 1 s) otvara međuekran.
- Trening bez vremena (`timed = false`): `phase_deadline` je `null`, bodovi `= base · fraction`, oznake bez vremenske komponente; sljedeće pitanje ide na `POST /api/matches/{id}/next`.
- "Dan" (streak, dnevni zadaci, dnevni capovi SP-a, dnevni izazov) i "tjedan" (ponedjeljak 00:00) računa `Clock` u `Europe/Zagreb`.

### 2.5 Mobilna spremnost (Capacitor, kasnije)

- Token u `Authorization` headeru, nikad cookie; refresh token klijent čuva iza malog async `tokenStore` sučelja (web: `localStorage`; Android: Capacitor Preferences). Access token samo u memoriji.
- `Cors:AllowedOrigins` dobiva `https://localhost`, `capacitor://localhost` i `http://localhost` kad dođe Android; Android build koristi apsolutni `VITE_API_BASE_URL` (produkcijski web ide preko Caddyja).
- Polling i REST rade identično u WebViewu; SignalR (ako dođe) ima `@microsoft/signalr` koji radi u Capacitoru. Polling hook (§6.10) staje kad je `document.hidden`, a odmah refetcha na `visibilitychange`/Capacitor `appStateChange`; vrijeme se mjeri iz `performance.now()` zabilježenog na renderu, ne iz brojanja intervala (WebView u pozadini usporava timere).
- Linkovi koji rade i na webu i u aplikaciji: `https://<domena>/challenge/{code}` i `https://<domena>/reset-password?token=...` kao Android App Links (jedan `assetlinks.json`); nema `app://` sheme.
- Push obavijesti (streak u 20:00, revanš, tjedni izvještaj): tablica `device_tokens` je u roadmapu, FCM preko backenda.
- Nijedan endpoint ne ovisi o browser-only značajci.

## 3. Baza podataka

Ciljni oblik (EF migracije generiraju stvarni DDL). Sve tablice imaju `id uuid primary key` ako nije drugačije rečeno; `created_at timestamptz not null` upisuje kod iz `TimeProvider`-a (bez `default now()`, da testovi s lažnim satom budu dosljedni). Enumi su `text` s `CHECK`. FK stupci su indeksirani (PostgreSQL to ne radi sam). Razlomci su `double precision`.

### 3.1 Korisnici i autentikacija

```sql
create table users (
  id                    uuid primary key,
  email                 text not null unique,                 -- spremljen lowercased
  username              text not null check (length(username) between 3 and 20),
  username_normalized   text not null unique,                 -- lower(username), za login i URL-ove
  password_hash         text not null,                        -- PasswordHasher<User> (PBKDF2)
  avatar                text not null default 'default',      -- ključ iz liste na frontendu
  locale                text not null default 'en' check (locale in ('en','hr')),
  profile_visibility    text not null default 'public' check (profile_visibility in ('public','friends')),
  notifications         jsonb not null default '{"dailyChallenge":true,"streakReminder":true,"rematch":true}',
  rating                integer not null default 1000,
  ranked_matches        integer not null default 0,           -- Elo K=32 do 30, '?' do 10, profil za ciljanje od 5 (§9, §8 ideje)
  tier                  text not null default 'bronze' check (tier in ('bronze','silver','gold','platinum','diamond','master')),  -- s histerezom
  is_grandmaster        boolean not null default false,       -- globalni top 100, osvježava Housekeeping; DTO prikazuje 'grandmaster'
  coins                 integer not null default 0 check (coins >= 0),   -- keš ledgera, mijenja se samo ExecuteUpdate-om
  xp                    integer not null default 0,                     -- keš ledgera
  streak_count          integer not null default 0,
  streak_last_active_on date,
  streak_freezes        integer not null default 0 check (streak_freezes between 0 and 2),
  current_match_id      uuid references matches(id) on delete set null, -- jedna aktivna partija po korisniku
  equipped_title_id     uuid references titles(id) on delete set null,
  equipment             jsonb not null default '{}',          -- {"avatarFrame":itemId,"banner":...,"boardTheme":...,"nameColor":...,"cardBackground":...}
  last_seen_at          timestamptz,                           -- online status, ažurira se najviše jednom u 30 s
  created_at            timestamptz not null
);

create table refresh_tokens (
  id          uuid primary key,
  user_id     uuid not null references users(id) on delete cascade,
  token_hash  text not null unique,          -- SHA-256 od tokena; token sam se nikad ne sprema
  family_id   uuid not null,                 -- jedna prijava = jedna obitelj; ponovna upotreba opozvanog tokena opoziva cijelu obitelj
  expires_at  timestamptz not null,
  revoked_at  timestamptz,
  replaced_by uuid,                          -- rotacija: stari token pokazuje na novi
  created_at  timestamptz not null
);
create index on refresh_tokens (user_id);
create index on refresh_tokens (family_id);

create table password_reset_tokens (
  id          uuid primary key,
  user_id     uuid not null references users(id) on delete cascade,
  token_hash  text not null unique,
  expires_at  timestamptz not null,          -- 1 h
  used_at     timestamptz,
  created_at  timestamptz not null
);
create index on password_reset_tokens (user_id);

create table user_topics (                   -- aktivne teme (3–5, provjera u use caseu)
  user_id   uuid not null references users(id) on delete cascade,
  topic_id  uuid not null references topics(id) on delete cascade,
  primary key (user_id, topic_id)
);
create index on user_topics (topic_id);
```

### 3.2 Sadržaj: teme, koncepti, pitanja

```sql
create table topics (
  id          uuid primary key,
  slug        text not null unique,          -- 'sql', 'javascript', 'how-ai-works' ...
  name        text not null,
  description text not null,
  sort_order  integer not null,
  is_active   boolean not null default true  -- false = ne nudi se u matchmakingu (npr. premali pool)
);

create table concepts (
  id          uuid primary key,
  topic_id    uuid not null references topics(id) on delete cascade,
  slug        text not null unique,          -- 'sql.n-plus-one'
  name        text not null,
  description text not null,                 -- 1–2 rečenice; ulaz za fallback kartice poduke
  sort_order  integer not null
);
create index on concepts (topic_id);

create table questions (
  id                   uuid primary key,
  seed_key             text unique,          -- 'sql.n-plus-one.mc.001' za idempotentan seed
  topic_id             uuid not null references topics(id),
  concept_id           uuid not null references concepts(id),      -- primarni
  secondary_concept_id uuid references concepts(id),
  type                 text not null check (type in ('true_false','multiple_choice','cloze','odd_one_out','output','match_pairs','order','find_bug')),
  difficulty           smallint not null check (difficulty between 1 and 4),
  language             text,                                        -- jezik koda ('js','sql','py','cs','http') ako ima koda
  content              jsonb not null,                              -- §3.2.1, uključuje "answer"
  remember             text not null,                               -- jedna rečenica "zapamti" (statički fallback objašnjenja)
  explanation          jsonb,                                       -- {"whyCorrect","whyDistractor","remember"} keš AI objašnjenja
  status               text not null default 'active' check (status in ('active','reported','quarantine','retired')),
  created_at           timestamptz not null
);
create index on questions (topic_id) where status = 'active';      -- deck builder učita sva aktivna pitanja teme jednim upitom
create index on questions (concept_id);
create index on questions (secondary_concept_id);

create table question_reports (
  id          uuid primary key,
  question_id uuid not null references questions(id) on delete cascade,
  user_id     uuid not null references users(id) on delete cascade,
  reason      text not null check (length(reason) <= 500),
  created_at  timestamptz not null,
  unique (question_id, user_id)
);
```

Statistika pitanja (postotak točnih, prosječno vrijeme, broj prikaza) se ne sprema na pitanju: izvodi se iz `answers` (indeks `(question_id)`), pa dva istodobna odgovora ne gube inkrement.

#### 3.2.1 `questions.content` po tipu

Zajednička polja: `type`, `prompt` (tekst pitanja, smije biti prazan kod cloze/output), opcionalno `code` (max 25 linija, 60 znakova po liniji; validira seed test). Polje `answer` je ključ i API ga skida iz `QuestionPublicDto`. TypeScript unija je u §6.1.

| Tip | Javni dio | `answer` | Payload odgovora | Fraction |
| --- | --- | --- | --- | --- |
| `true_false` | `{ statement }` | `{ value: bool }` | `{ value: bool }` | 1 ili 0 |
| `multiple_choice` | `{ prompt, code?, options: [{id,text}] ×4 }` | `{ optionId }` | `{ optionId }` | 1 ili 0 |
| `cloze` | `{ code (sadrži "____"), options ×4 }` | `{ optionId }` | `{ optionId }` | 1 ili 0 |
| `odd_one_out` | `{ prompt, items: [{id,text}] ×4 }` | `{ itemId }` | `{ itemId }` | 1 ili 0 |
| `output` | `{ code, options ×4 }` | `{ optionId }` | `{ optionId }` | 1 ili 0 |
| `match_pairs` | `{ prompt, left: [{id,text}] ×4, right: [{id,text}] ×4 }` | `{ pairs: [{leftId,rightId}] ×4 }` | `{ pairs: [{leftId,rightId}] }` | točni parovi / 4 |
| `order` | `{ prompt, steps: [{id,text}] ×4–5 }` (promiješani u seedu) | `{ orderedIds: [] }` | `{ orderedIds: [] }` | 1 ili 0 |
| `find_bug` | `{ prompt, code, fixes: [{id,text}] ×3 }` | `{ line: int, fixId, acceptLines?: int[] }` | `{ line: int, fixId }` | kriva linija 0; točna linija 0.4, uz točan fix 1. `acceptLines` su druge linije iste razlomljene naredbe koje se također priznaju; `line` je kanonska linija za analizu |

Opcije se ne miješaju u runtimeu: seed ih sprema već promiješane, oba igrača vide isti redoslijed. ID-evi opcija su kratki stringovi (`"a"`, `"b"`, ...); kod `match_pairs` ID-evi desne strane ne smiju slijediti redoslijed lijeve (seed test), inače ključ curi.

### 3.3 Napredak: mastery, red ponavljanja, log odgovora

```sql
create table concept_mastery (
  user_id                      uuid not null references users(id) on delete cascade,
  concept_id                   uuid not null references concepts(id) on delete cascade,
  topic_id                     uuid not null references topics(id),      -- denormalizirano za leaderboard po temi
  mastery                      double precision not null default 50 check (mastery between 0 and 100),
  answer_count                 integer not null default 0,               -- n u formuli α; 0 = 'unknown', ne ulazi u ciljanje ni u prosjek teme
  is_graduated                 boolean not null default false,
  review_step                  smallint check (review_step between 1 and 5),   -- null = nije u redu ponavljanja
  review_due_at                timestamptz,
  consecutive_review_successes smallint not null default 0,
  patched_at                   timestamptz,                               -- zadnji nagrađeni prolaz poduke (SP/coins max 1× u 7 dana)
  last_answered_at             timestamptz,
  primary key (user_id, concept_id)
);
create index on concept_mastery (user_id, review_due_at) where review_due_at is not null;   -- "Za ponoviti: n"
create index on concept_mastery (topic_id, user_id) where is_graduated;                     -- leaderboard po temi
create index on concept_mastery (concept_id);

-- Append-only. Izvor istine za svu statistiku i za "viđeno u zadnjih 30 dana".
create table answers (
  id              uuid primary key,
  user_id         uuid not null references users(id) on delete cascade,
  question_id     uuid not null references questions(id),
  concept_id      uuid not null references concepts(id),
  topic_id        uuid not null references topics(id),
  question_type   text not null,
  difficulty      smallint not null,
  source          text not null check (source in ('ranked','ghost','bot','friend','practice','calibration','lesson_check','daily_challenge')),
  match_id        uuid references matches(id) on delete set null,
  lesson_id       uuid references lessons(id) on delete set null,
  payload         jsonb,                     -- null = istek vremena
  is_correct      boolean not null,          -- fraction == 1
  fraction        double precision not null, -- 0..1 (djelomični bodovi)
  points          integer not null,
  elapsed_ms      integer not null,
  time_limit_ms   integer,                   -- null = bez vremena (trening)
  label           text check (label in ('brilliant','good','miss','blunder')),
  mastery_before  double precision not null,
  mastery_after   double precision not null,
  review_outcome  text check (review_outcome in ('success','fail')),   -- success = točno na dospjelom konceptu; fail = krivo na konceptu u redu ponavljanja
  reverted_at     timestamptz,               -- RetireQuestion poništio mastery promjenu; statistika preskače
  created_at      timestamptz not null
);
create index on answers (user_id, created_at desc);                 -- seen in last 30 days, aktivnost dana
create index on answers (user_id, concept_id, created_at desc);    -- zadnjih N krivih po konceptu, mapa znanja
create index on answers (question_id);                             -- statistika pitanja, auto-kalibracija, RetireQuestion
create index on answers (match_id);
create index on answers (lesson_id);
```

Što iz `answers` izlazi bez nove sheme: točnost po tipu, prosječno vrijeme po tipu, najčešći tip blundera, toplinska mapa koncepata kroz vrijeme, usporedba s bandom, "zadnjih 20 krivih po temi" za AI, "viđeno u 30 dana", broj Brilliant oznaka za achievemente, postotak točnih po pitanju za auto-kalibraciju. Win rate dolazi iz `match_players`.

### 3.4 Partije

```sql
create table matches (
  id                   uuid primary key,
  kind                 text not null check (kind in ('ranked','ghost','bot','friend','practice','calibration')),
  phase                text not null check (phase in ('veto_coin','veto_ban','veto_pick','lobby','question','interstitial','done')),
  phase_deadline       timestamptz,                               -- tick djeluje kad prođe; null u treningu bez vremena
  topic_id             uuid references topics(id),                -- null dok traje veto
  timed                boolean not null default true,             -- false samo trening "bez vremena"
  band_min             smallint, band_max smallint,               -- D band iz prosjeka ratinga (§4.2)
  current_index        smallint not null default 0,
  question_started_at  timestamptz,
  veto                 jsonb,                                     -- {"pool":[topicId..],"pickedBy":{topicId:[seat..]},"coinWinnerSeat":0,"turnSeat":0,"bans":[{"topicId","seat","auto"}],"finalTwo":[..],"chosenTopicId"}
  rng_seed             integer not null,
  season_id            uuid references seasons(id),
  rematch_of_match_id  uuid references matches(id),
  rematch_requested_by_seat smallint,
  rematch_requested_at timestamptz,
  rematch_status       text check (rematch_status in ('pending','accepted','declined','expired')),
  rematch_match_id     uuid references matches(id),
  winner_seat          smallint,                                  -- null = remi ili prekinuto
  result_reason        text check (result_reason in ('score','tiebreak_correct','tiebreak_time','draw','resign','disconnect','aborted')),
  created_at           timestamptz not null,
  started_at           timestamptz,
  finished_at          timestamptz
);
create index on matches (phase_deadline) where phase <> 'done';                      -- tick
create index on matches (finished_at) where kind in ('ranked','ghost');              -- tjedni leaderboard, ghost kandidati
create index on matches (topic_id);
create index on matches (season_id);

create table match_players (
  match_id              uuid not null references matches(id) on delete cascade,
  seat                  smallint not null check (seat in (0,1)),
  kind                  text not null check (kind in ('human','bot','ghost')),
  user_id               uuid references users(id),                -- human: igrač; ghost: snimljeni igrač (prikaz); bot: null
  bot_persona           text,                                     -- 'vibe_coder','junior','mid','senior'
  ghost_source_match_id uuid references matches(id),
  display_rating        integer not null,                         -- rating u trenutku partije (ghost: iz snimke)
  rating_before         integer, rating_after integer,            -- samo human u ranked/ghost
  score                 integer not null default 0,
  correct_count         smallint not null default 0,
  time_on_correct_ms    integer not null default 0,               -- tiebreak
  accepted_at           timestamptz,
  last_seen_at          timestamptz,                              -- postavlja se na start/prihvat; GET state ga osvježava; 20 s bez = disconnected
  disconnected_at       timestamptz,                              -- AdvanceMatches ga postavlja/briše
  delivered_index       smallint,                                 -- zadnje pitanje koje je GET isporučio ovom sjedalu
  delivered_at          timestamptz,
  resigned_at           timestamptz,
  sp_earned             integer not null default 0,
  coins_earned          integer not null default 0,
  xp_earned             integer not null default 0,
  primary key (match_id, seat)
);
create index on match_players (user_id, match_id);                -- povijest partija, "isti protivnik", ghost kandidati
create index on match_players (ghost_source_match_id);

create table match_questions (                                    -- zamrznuti deck (§8 ideje, pravilo 6)
  match_id       uuid not null references matches(id) on delete cascade,
  index          smallint not null check (index between 0 and 9),
  question_id    uuid not null references questions(id),
  concept_id     uuid not null references concepts(id),
  slot           text not null check (slot in ('warmup','targeted','core','review')),
  type           text not null,
  difficulty     smallint not null,
  base_points    smallint not null,
  time_limit_ms  integer not null,                                -- nominalni limit i u treningu bez vremena
  primary key (match_id, index)
);
create index on match_questions (question_id);

create table match_answers (                                      -- istina partije za sva sjedala (human, bot, ghost)
  match_id     uuid not null references matches(id) on delete cascade,
  seat         smallint not null,
  index        smallint not null,
  payload      jsonb,                                             -- null = istek
  is_correct   boolean not null,
  fraction     double precision not null,
  points       integer not null,
  elapsed_ms   integer not null,
  timed_out    boolean not null default false,
  answered_at  timestamptz not null,                              -- bot/ghost: u budućnosti dok ne "stigne"
  label        text check (label in ('brilliant','good','miss','blunder')),
  primary key (match_id, seat, index)
);
```

DTO `status` (`veto|lobby|in_progress|finished|aborted`) se izvodi iz `phase` + `result_reason` u mapperu; nema dupliranog stupca. Ghost duel: novi `match` s `kind='ghost'`; sjedalo 1 ima `kind='ghost'`, `user_id` snimljenog igrača, `ghost_source_match_id`, `display_rating` iz snimke; `match_questions` se kopiraju iz izvorne partije, a `match_answers` ghosta se kopiraju s `answered_at = question_started_at + elapsed_ms` kad svako pitanje krene. `answers` (log učenja) se piše samo za živog igrača.

### 3.5 Matchmaking

```sql
create table queue_entries (
  user_id      uuid primary key references users(id) on delete cascade,
  rating       integer not null,
  topic_ids    uuid[] not null,
  entered_at   timestamptz not null,
  status       text not null default 'waiting' check (status in ('waiting','proposed')),
  proposal_id  uuid references match_proposals(id) on delete set null,
  ghost_offer_declined boolean not null default false     -- nakon odbijanja: nudi se bot, ghost se više ne nudi u ovom čekanju
);

create table match_proposals (
  id          uuid primary key,
  user_a      uuid not null references users(id),
  user_b      uuid not null references users(id),
  created_at  timestamptz not null,
  expires_at  timestamptz not null,       -- +10 s
  a_accepted_at timestamptz, b_accepted_at timestamptz,
  status      text not null default 'pending' check (status in ('pending','matched','expired','declined')),
  match_id    uuid references matches(id)
);

create table ghost_offers (                 -- ista snimka se istom igraču nudi samo jednom
  user_id          uuid not null references users(id) on delete cascade,
  source_match_id  uuid not null references matches(id) on delete cascade,
  offered_at       timestamptz not null,
  primary key (user_id, source_match_id)
);
```

Napuštanje reda dok je `proposed`: pod `FOR UPDATE` nad ponudom, ponuda ide u `declined`, drugi igrač se vraća u `waiting` sa sačuvanim `entered_at`. Svaki `POST /api/matches/*` i prihvat ghosta brišu `queue_entries` red korisnika; `RunMatchmaking` preskače korisnike s `current_match_id`.

### 3.6 Poduka

```sql
create table lessons (
  id             uuid primary key,
  user_id        uuid not null references users(id) on delete cascade,
  match_id       uuid references matches(id) on delete set null,
  status         text not null check (status in ('generated','passed','failed')),
  concept_ids    uuid[] not null,          -- promašeni u partiji + do 2 kronične praznine
  cards          jsonb not null,           -- [{"conceptId","title","why","code","language","misconception"}] ×3–5
  cards_source   text not null check (cards_source in ('ai','cache','fallback')),
  check_question_ids uuid[] not null,      -- 3; [0] = varijanta promašenog pitanja; nakon pada nova tri (ključevi starih su prikazani)
  check_results  jsonb,                    -- [{"questionId","payload","isCorrect"}] zadnjeg pokušaja
  attempt        smallint not null default 1,
  last_attempt_at timestamptz,
  first_for_match boolean not null default true,   -- druga poduka za istu partiju je bez nagrade
  rewarded       boolean not null default false,
  retry_after    timestamptz,              -- pad: +10 min
  created_at     timestamptz not null,
  finished_at    timestamptz
);
create index on lessons (user_id, created_at desc);
create index on lessons (match_id);

create table concept_lesson_cards (        -- keš po (koncept, band)
  concept_id  uuid not null references concepts(id) on delete cascade,
  band        smallint not null,
  cards       jsonb not null,
  created_at  timestamptz not null,
  primary key (concept_id, band)
);
```

Pravila sastavljanja (`StartLesson`, `LessonRules`):

- Ulaz: moji odgovori u partiji s `fraction < 1`; ako ih nema → 409 `urn:app:lesson/nothing-missed` i `MatchResultDto.lessonAvailable = false`.
- Varijanta: izvor je moj promašaj s najvećim `base_points · (1 - fraction)` (isto pitanje koje je Blunder kartica); traži se pitanje istog koncepta, iste težine, drugog tipa, neviđeno 30 dana → fallback susjedna težina → fallback isti tip, drugo pitanje. Ostala 2 kontrolna: nova pitanja iz `concept_ids`, neviđena 30 dana.
- Koncepti: promašeni, najskuplji prvi (koncept varijante uvijek prvi), najviše 3; plus kronične praznine. Jedna kartica po konceptu, dakle 1–5 kartica.
- Kronične praznine: do 2 koncepta teme partije sa stanjem *weak* i `answer_count > 0`, najniži mastery, koji nisu promašeni u ovoj partiji (ni oni iznad limita od 3).
- Kartice: keš `concept_lesson_cards` po (koncept, band igrača), inače AI (paralelno, zajednički limit `Ai:TimeoutMs`), inače kartica iz `concepts.description` (`misconception` prazan). Kešira se samo AI kartica.
- Prolaz: varijanta točna + barem 1 od 2 ostala. Zakrpani = svi `concept_ids` (doslovno §10 ideje) → `ReviewSchedule.Patch`.
- Nagrada (`rewardEligible`): `lessons.rewarded = false` i nema ranijeg pokušaja te poduke na današnji dan (`last_attempt_at` u `Clock.Today`); druga poduka za istu partiju i pokušaj isti dan nakon pada su bez nagrade, pokušaj idući dan je s nagradom. Coins 50 + XP 100 samo ako je ≥ 1 zakrpani koncept nagradno podoban (`patched_at` null ili stariji od 7 dana); SP 2 po podobnom konceptu pod dnevnim capom 10 (×2 prva dva tjedna sezone). F2 računa `rewardEligible` i postavlja `rewarded`; iznose knjiži F4 kroz ledger (do tada `rewards` su 0, a `patched_at` se ne dira).
- Kontrolna pitanja su bez vremena (mastery težina ×0.5), pišu `answers` sa `source = lesson_check` i `lesson_id`. Pad: `retry_after = now + 10 min`, `attempt + 1`, nova tri kontrolna pitanja.

### 3.7 Ekonomija

```sql
create table reward_events (               -- ledger; jedini način da se promijene coins/xp/sp
  id           uuid primary key,
  user_id      uuid not null references users(id) on delete cascade,
  type         text not null,              -- 'match_win','match_loss','match_draw','rating_upset','lesson_passed','concept_patched','daily_challenge','daily_task','daily_task_bonus','streak_milestone','weekly_xp_goal','level_up','friend_match','answer_xp','match_xp','shop_purchase','streak_freeze','season_reset','season_drop'
  coins        integer not null default 0, -- može biti negativan (kupnja)
  xp           integer not null default 0,
  sp           integer not null default 0,
  season_id    uuid references seasons(id),
  reference_id uuid,                       -- match/lesson/concept/item id
  dedupe_key   text not null unique,       -- 'match_win:{matchId}:{userId}', 'concept_patched:{conceptId}:{userId}:{lessonId}', 'daily_task:{day}:{userId}:{key}', 'level_up:{userId}:{n}'
  created_at   timestamptz not null
);
create index on reward_events (user_id, created_at desc);
create index on reward_events (user_id, type, created_at desc);   -- dnevni capovi (100 SP iz pobjeda, 10 iz učenja), tjedni XP cilj
create index on reward_events (season_id, user_id) where sp <> 0;

create table shop_items (
  id         uuid primary key,
  key        text not null unique,         -- 'frame.bronze-ring'
  kind       text not null check (kind in ('avatar_frame','banner','board_theme','name_color','card_background','streak_freeze')),
  name       text not null,
  price      integer not null check (price > 0),
  rarity     text not null default 'common',
  asset_key  text not null,                -- frontend mapira na sliku/CSS
  season_drop_tier text,                   -- ako nije null, CloseSeason ga daje igračima s tim najvišim tierom sezone
  is_active  boolean not null default true
);

create table user_items (
  user_id     uuid not null references users(id) on delete cascade,
  item_id     uuid not null references shop_items(id),
  acquired_at timestamptz not null,
  primary key (user_id, item_id)
);
```

`users.coins` i `users.xp` su keš: `Rewarder` u istoj transakciji upiše red u ledger i uveća keš s `ExecuteUpdateAsync` (`coins = coins + @d`); kupnja ide `UPDATE users SET coins = coins - @price WHERE id = @u AND coins >= @price`, 0 redova = 409. Test `Ledger_sum_equals_cached_balance` čuva invarijantu. SP tekuće sezone = `user_seasons.sp` (keš) = `sum(sp) where season_id = current`.

### 3.8 Dnevni ciklus

```sql
create table activity_days (               -- streak i "aktivni dani" (dan po Clock.Today)
  user_id uuid not null references users(id) on delete cascade,
  day     date not null,
  primary key (user_id, day)
);

create table daily_challenges (
  day          date primary key,
  question_id  uuid not null references questions(id)     -- find_bug, D2–D3, osigurano 7 dana unaprijed iz poola
);

create table daily_challenge_attempts (
  user_id      uuid not null references users(id) on delete cascade,
  day          date not null references daily_challenges(day),
  started_at   timestamptz not null,       -- prvi GET tog dana otvara pokušaj; 60 s + 1,5 s tolerancije
  answered_at  timestamptz,
  payload      jsonb,
  is_correct   boolean,
  elapsed_ms   integer,
  primary key (user_id, day)
);
create index on daily_challenge_attempts (day, is_correct desc, elapsed_ms);   -- leaderboard dana

create table user_daily_tasks (
  user_id      uuid not null references users(id) on delete cascade,
  day          date not null,
  task_key     text not null,              -- 'play_2_ranked','patch_1_concept','daily_challenge','practice_weakest','3_brilliant','review_all_due'
  target       integer not null,           -- review_all_due: broj dospjelih pri generiranju; ostali: konstanta
  topic_id     uuid references topics(id), -- practice_weakest: najslabija tema u trenutku generiranja
  completed_at timestamptz,
  primary key (user_id, day, task_key)
);
```

Tri zadatka dana biraju se deterministički iz liste (`Random(hash(day, userId))`) i upisuju pri prvom `GET /api/me/daily-tasks` tog dana; `review_all_due` se ne nudi kad je dospjelih 0, `practice_weakest` pamti temu (min mastery teme među aktivnim temama s ≥ 1 odgovorenim konceptom). Napredak se računa iz `answers`/`matches`/`lessons` tog dana (ranked = `kind in ('ranked','ghost')`; Brilliant iz bilo kojeg izvora s vremenom), a završetak se upisuje jednom i nagrađuje kroz ledger.

### 3.9 Društveno

```sql
create table friendships (                 -- jedan red po paru; bez smjera u ključu
  user_low      uuid not null references users(id) on delete cascade,
  user_high     uuid not null references users(id) on delete cascade,
  requested_by  uuid not null references users(id),
  status        text not null check (status in ('pending','accepted')),
  created_at    timestamptz not null,
  primary key (user_low, user_high),
  check (user_low < user_high)
);
create index on friendships (user_high, status);

create table friend_challenges (
  id              uuid primary key,
  code            text not null unique,     -- 6 znakova A–Z2–9
  host_user_id    uuid not null references users(id) on delete cascade,
  target_user_id  uuid references users(id), -- null = otvoreni link/kod
  guest_user_id   uuid references users(id),
  match_id        uuid references matches(id),
  expires_at      timestamptz not null,     -- 10 min
  created_at      timestamptz not null
);
create index on friend_challenges (target_user_id) where match_id is null;
```

### 3.10 Sezone, titule, achievementi, tjedni

```sql
create table seasons (
  id         uuid primary key,
  number     integer not null unique,
  starts_at  timestamptz not null,
  ends_at    timestamptz not null,
  closed_at  timestamptz
);

create table user_seasons (                 -- keš SP + najviši tier u sezoni (FinishMatch ga diže)
  user_id    uuid not null references users(id) on delete cascade,
  season_id  uuid not null references seasons(id) on delete cascade,
  sp         integer not null default 0,
  best_tier  text not null,
  final_rank integer,                       -- snapshot na kraju sezone
  primary key (user_id, season_id)
);
create index on user_seasons (season_id, sp desc);

create table titles (
  id    uuid primary key,
  key   text not null unique,               -- 's1.top5', 'master.sql', 'weekly.top10'
  kind  text not null check (kind in ('season','knowledge','weekly')),
  name  text not null
);

create table user_titles (
  user_id   uuid not null references users(id) on delete cascade,
  title_id  uuid not null references titles(id) on delete cascade,
  earned_at timestamptz not null,
  count     integer not null default 1,     -- tjedna titula se broji ("3× tjedni Top 10")
  active    boolean not null default true,  -- znanje: gubi se ako graduirani koncept padne ispod 70
  primary key (user_id, title_id)
);

create table user_achievements (
  user_id   uuid not null references users(id) on delete cascade,
  key       text not null,                  -- pravila u kodu (AchievementRules)
  earned_at timestamptz not null,
  primary key (user_id, key)
);

create table weekly_results (               -- snapshot ponedjeljkom 00:00
  week_start date not null,
  user_id    uuid not null references users(id) on delete cascade,
  rank       integer not null,
  points     integer not null,
  primary key (week_start, user_id)
);
```

### 3.11 Što se namjerno ne sprema

| Nije tablica | Jer |
| --- | --- |
| Povijest ratinga | `match_players.rating_before/after` + `matches.finished_at` |
| Viđena pitanja | `answers (user_id, created_at)` → `question_id` u zadnjih 30 dana |
| Statistika pitanja (prikazi, točnost, vrijeme) | `answers (question_id)` |
| Globalni/tjedni/prijateljski leaderboard | upiti nad `users`, `match_players`, `friendships`; top 100 je jeftin s indeksima |
| Dnevni zadaci kao konfiguracija, bot persone, achievement pravila, tierovi | konstante u `App.Domain` |
| Stanje koncepta (`unknown/weak/...`) | izvedeno iz `mastery`, `answer_count`, `is_graduated` (`ConceptState.Of(...)`) |
| Online status | `users.last_seen_at` < 2 min |
| DTO `status` partije | `phase` + `result_reason` |

### 3.12 EF Core mapiranje

- Tablica povijesti migracija je `__ef_migrations_history`: snake_case konvencija preimenuje i njezine stupce, pa stara `"__EFMigrationsHistory"` (`"MigrationId"`) iz startera nije čitljiva. Migracija `InitialSchema` briše staru tablicu i `"Notes"`; testna baza prije migracije dobiva staru starter shemu da svaki run provjeri nadogradnju.
- `OnModelCreating` primjenjuje `ApplyConfigurationsFromAssembly`; jedna `IEntityTypeConfiguration<T>` klasa po entitetu u `Infrastructure/Configurations/`. `UseSnakeCaseNamingConvention()`; stringovi u `HasFilter("...")` se pišu ručno u snake_case (konvencija ih ne prepisuje).
- `jsonb` stupci su `string` svojstva s `.HasColumnType("jsonb")`; (de)serijalizacija kroz `System.Text.Json` u Domainu. Konkretni record se bira `switch`-em po `questions.type` (`QuestionContent.Parse(type, json)`), ne polimorfnim diskriminatorom (jsonb ne čuva redoslijed ključeva pa `"type"` ne bi bio prvi).
- Enumi: `SnakeCaseEnumConverter<T>` (`JsonNamingPolicy.SnakeCaseLower.ConvertName` ↔ `Enum.Parse` bez podvlaka) primijenjen u `ConfigureConventions`, + `t.HasCheckConstraint(...)` s istim vrijednostima. HTTP JSON: `JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower)`.
- `uuid[]`: Npgsql mapira `Guid[]` / `List<Guid>` nativno.
- Nema `xmin`/row versiona: serijalizaciju daje `MatchLock` (`db.Database.ExecuteSqlAsync($"SELECT 1 FROM matches WHERE id = {id} FOR UPDATE")` unutar `BeginTransactionAsync`), tick koristi `FromSql(... FOR UPDATE SKIP LOCKED)`.
- Brojači (`users.coins/xp`, `last_seen_at`, `match_players.last_seen_at/delivered_*`) idu `ExecuteUpdateAsync`-om, nikad kroz praćeni entitet.
- `created_at` i sva vremena postavlja kod iz `TimeProvider`-a; migracije ne dodaju `default now()`.
- Složeni ključevi: `HasKey(x => new { x.MatchId, x.Seat })`.
- `IAppDbContext` dobiva `DbSet` po tablici i `Task<T> InTransactionAsync(Func<Task<T>>)`; use caseovi rade direktno nad njim (bez repozitorija).
- Migracija F0 briše `Notes`; nakon svake promjene entiteta `dotnet ef migrations add`.

## 4. Pravila igre kao čiste funkcije (App.Domain)

Svaka funkcija ovdje ima test u §9. Nijedna ne dira bazu, sat ili slučajnost bez da ih dobije kao parametar.

### 4.1 `GameRules` (sve konstante iz ideje)

```csharp
public static class GameRules
{
    public const string DayTimeZone = "Europe/Zagreb";
    public const int StartRating = 1000, ProvisionalMatches = 10, ProfileAfterRankedMatches = 5, HighKMatches = 30, KHigh = 32, KLow = 16;
    public const int MaxRatingGap = 400, QueueInitialRange = 100, QueueRangeStep = 50, QueueStepSeconds = 10, GhostOfferAfterSeconds = 30, GhostRecordingMaxDays = 30;
    public const int ProposalAcceptSeconds = 10, VetoBanSeconds = 8, VetoAnimationSeconds = 3, LobbySeconds = 5, InterstitialSeconds = 2, RematchSeconds = 20, FriendChallengeMinutes = 10;
    public const int ReconnectGraceSeconds = 20, ClientTimeToleranceMs = 1500, BotMinSlackMs = 500, QuestionsPerMatch = 10, CalibrationQuestions = 5, VetoPoolMax = 6;
    public const int MinTopics = 3, MaxTopics = 5, SeenQuestionDays = 30, SameOpponentMaxStreak = 3, TargetedRotationMatches = 2;
    public const int TierHysteresis = 25, GrandmasterTop = 100;
    public const double MasteryStart = 50, MasteryAlphaMin = 0.15, BlunderWeight = 1.2, UntimedWeight = 0.5;
    public const double GraduationMastery = 85, SolidMastery = 70, WeakMastery = 40, TargetedMaxMastery = 70, TargetedMaxGap = 20;
    public const int GraduationStep = 5;                                            // graduira na uspjehu koraka 5 uz mastery >= 85 (~55 dana)
    public static readonly TimeSpan[] ReviewIntervals = [Days(1), Days(3), Days(7), Days(14), Days(30)];
    public const int CoinsLessonPassed = 50, CoinsWin = 15, CoinsLoss = 5, CoinsDraw = 10, CoinsFriendMatch = 5, CoinsDailyTask = 20, CoinsDailyBonus = 30, CoinsDailyChallenge = 30, CoinsDailyChallengeAttempt = 5, CoinsWeeklyXpGoal = 100, CoinsStreakFreeze = 100, MaxStreakFreezes = 2;
    public const int SpWin = 10, SpUpset = 5, SpUpsetGap = 100, SpPatched = 2, SpDailyWinCap = 100, SpDailyLearnCap = 10, PatchRewardCooldownDays = 7, SeasonLearningBoostDays = 14, SeasonMonths = 3;
    public const int XpAnswer = 5, XpMatch = 50, XpLesson = 100, XpDailyChallenge = 30, WeeklyXpGoal = 500;
    public const int DailyChallengeSeconds = 60, LessonRetryMinutes = 10;
    public const int CalibrationMinAnswers = 50; public const double CalibrationEasyRatio = 0.85, CalibrationHardRatio = 0.30;
    public static readonly (int days, int coins)[] StreakMilestones = [(7, 100), (30, 500), (100, 2000)];
    public static readonly IReadOnlyDictionary<QuestionType, QuestionTypeRule> Types = new Dictionary<QuestionType, QuestionTypeRule>
    {
        [TrueFalse]      = new(TimeMs: 8_000,  BasePoints: 50,  Guessability: 0.5,  MaxPerDeck: 2, MinPerDeck: 0, Partial: false),
        [MultipleChoice] = new(15_000, 100, 0.75, MaxPerDeck: 3, MinPerDeck: 0, false),
        [Cloze]          = new(18_000, 120, 0.75, 2, 0, false),
        [OddOneOut]      = new(12_000, 100, 0.75, 2, 0, false),
        [Output]         = new(25_000, 150, 0.75, 2, 1, false),
        [MatchPairs]     = new(30_000, 150, 1.0,  2, 0, true),
        [Order]          = new(25_000, 150, 1.0,  2, 0, false),
        [FindBug]        = new(40_000, 200, 1.0,  2, 1, true),
    };
    public static readonly TypeGroupRule[] TypeGroups = [ new(Types: [MatchPairs, Order], Min: 1, Max: 2) ];   // drag-and-drop zajedno
    public static readonly double[] DifficultyWeight = [0, 0.7, 0.9, 1.1, 1.3];   // index = D
}
```

### 4.2 Funkcije

| Funkcija | Potpis | Pravilo (ideja) |
| --- | --- | --- |
| `Clock.Today / WeekStart` | `(DateTimeOffset now) → DateOnly` | `DayTimeZone`; tjedan počinje ponedjeljkom |
| `Grader.Grade` | `(QuestionContent q, JsonElement payload) → double fraction` | §7; neispravan payload = 0, ne iznimka; `isCorrect = fraction == 1` |
| `Scoring.Points` | `(int basePoints, double fraction, int elapsedMs, int limitMs, bool timed) → int` | `base · fraction · (0.5 + 0.5 · remaining/total)`, zaokruženo; bez vremena faktor 1; istek ili fraction 0 → 0 (§9) |
| `Labels.Of` | `(difficulty, double fraction, elapsedMs, limitMs, timedOut, timed) → AnswerLabel` | fraction 1: Brilliant ako D3/D4 i < 40 % vremena, inače Good; fraction ≥ 0.5: Good; fraction u (0, 0.5): Miss; fraction 0: Blunder ako < 40 % vremena ili D1, inače Miss; istek: Miss; bez vremena: samo Good/Miss (§10) |
| `Elo.Update` | `(int ra, int rb, double sa, int rankedMatchesA) → int newRa` | `E = 1/(1+10^((rb-ra)/400))`, `K = 32` do 30 partija, `16` poslije; `S` 1/0.5/0 |
| `Tier.Of` | `(int rating, Tier current) → Tier` | pragovi §14 (bronze..master); pad tek ispod `prag - 25`; Grandmaster nije ovdje nego `users.is_grandmaster` (Housekeeping, top 100 s ≥ 10 partija) |
| `Mastery.Next` | `(double m, int n, double r, double w) → double` | `m + α(r-m)w`, `α = max(0.15, 1/(n+1))`, clamp 0–100 (§8); `r = 100 · fraction` |
| `Mastery.Weight` | `(difficulty, type, double fraction, label, timed) → double` | težina D × pogodivost (samo kad fraction > 0) × 1.2 ako Blunder × 0.5 bez vremena |
| `Mastery.Apply` | `(ConceptMastery, answer, now) → ConceptMastery` | Next + Weight; `answer_count++`; ako `is_graduated` i mastery < 70 → `is_graduated = false` + ponovno u red (step 1) + znanje-titula `active = false`; ako je koncept u redu ponavljanja i fraction < 1 → `ReviewSchedule.Fail`; ako je `review_due_at <= now` i fraction = 1 → `ReviewSchedule.Success` |
| `ConceptState.Of` | `(ConceptMastery) → Unknown/Weak/InProgress/Solid/Graduated` | n=0 unknown; <40 weak; <70 in progress; ≥70 solid; graduated flag (§11) |
| `ReviewSchedule.Success` | `(ConceptMastery, bool questionWasD1, now)` | `consecutive++`; na koraku 5 uz mastery ≥ 85 → graduated, step/due null; inače step = min(step+1, 5), due = now + interval[step-1] (D1 pitanje: pola) |
| `ReviewSchedule.Fail` | `(ConceptMastery, now)` | step 1, due now+1d, consecutive 0 (bilo koji izvor, bilo koji slot) |
| `ReviewSchedule.Patch` | `(ConceptMastery, now)` | step 1, due now+1d, consecutive 0 |
| `Veto.BuildPool` | `(topicsA, topicsB, Random) → List<Guid>` | zajedničke uvijek, dopuna nasumično iz unije do 6 (§5) |
| `Veto.BansFor` | `(int poolSize) → int` | 3→1, 4→2, 5→3, 6→4 |
| `Veto.Ban / AutoBan / Pick` | nad `VetoState` | naizmjence, istek = nasumični ban, 2 preostale → nasumični odabir |
| `DeckBuilder.Band` | `(ratingA, ratingB) → (min, max)` | `D = D(avg)`; par susjednih bandova na strani bližeg praga: avg bliže gornjem pragu → `(D, D+1)`, inače `(D-1, D)`; D1 → (1,2), D4 → (3,4). Primjer ideje 1350+1420 → avg 1385 → D2–D3 |
| `DeckBuilder.Build` | `(pool, players, targetingAllowed, band, now, rng, degrade) → DeckSlot[]` | §8 slotovi 1+3+4+2; ciljanje samo koncepata s `answer_count > 0` kod svih ljudskih igrača; `targetingAllowed = false` u rangiranoj partiji dok netko nema profil (`ranked_matches < 5`), u bot partiji uvijek true (cilja se iz igračevih slabih koncepata); grupa 1 = oba < 40, grupa 2 = oba < 70 i razlika ≤ 20, sortirano po prosjeku; rotacija (ciljan u zadnje 2 partije → preskoči dok nije zakrpan); ograničenja tipova i `TypeGroups`; susjedna težina → drugi tip → jezgra; bez ponavljanja koncepta; 30-dnevna isključenja; shuffle 2–10 |
| `DeckBuilder.BuildPractice` | `(topic questions, myMastery, due, seen, conceptIds?, Random) → Deck` | 10 pitanja bez warmupa: tražene `conceptIds` prvo, pa dospjeli, pa najniži mastery; tipovi po `Types`/`TypeGroups`; degradacija kad nema dovoljno: dopusti ponavljanje koncepta → sve težine → zanemari 30 dana (nikad prazno) |
| `DeckBuilder.Degrade` | isto za bot/kalibraciju | bot i trening nikad ne padaju zbog tankog poola; ranked tema s premalo pitanja dobiva `topics.is_active = false` |
| `Tiebreak.Winner` | `(MatchPlayer a, MatchPlayer b) → (seat?, reason)` | bodovi → više točnih → manji zbroj vremena na točnim → remi |
| `ResultCards.Of` | `(deck, myAnswers) → (blunder?, brilliant?, practiceConcept?)` | blunder = fraction < 1 s max `base·(1-fraction)` (prednost label blunder); brilliant = max `points` (prednost label brilliant); koncept za vježbu = min Σ(`mastery_after - mastery_before`); null kad nema |
| `BotPlayer.Answer` | `(QuestionContent q, BotPersona p, int limitMs, Random) → (payload, elapsedMs)` | točan s `p.Accuracy - 0.08·(D-2)`, inače krivi: distraktor; match_pairs = deranžman (0 točnih), order = permutacija ≠ ključ, find_bug = kriva linija; `elapsed = min(sample(p.Range), limitMs - 500)` (§12) |
| `MatchmakingRules.Range` | `(TimeSpan waited) → int` | 100 + 50·⌊s/10⌋, max 400 |
| `MatchmakingRules.Pick` | `(QueueEntry me, IEnumerable<QueueEntry> others, Guid[] myLast2RankedOpponents) → QueueEntry?` | u rasponu, ≥ 1 zajednička tema, ne ako su oba zadnja ranked protivnika = kandidat (treća zaredom se ne uparuje; revanš zaobilazi; ghost se ne broji); sort: razlika ratinga, zajedničke teme, dulje čekanje |
| `GhostOffer.Find` | `(QueueEntry me, candidates, seenQuestionIds) → candidate?` | sjedalo čovjeka u `ranked`/`ghost` partiji s `result_reason in (score, tiebreak_*, draw)`, `user_id ≠ me`, tema u mojim aktivnim, `|display_rating - me.rating| ≤ Range(waited)`, ni jedno pitanje decka u mojih 30 dana, nije u `ghost_offers`, `finished_at ≥ now - 30 d`; najsvježija |
| `Level.Of` | `(int xp) → int` | novi korisnik level 1; level n (n ≥ 2) traži 100·n XP od prethodnog, kumulativno `50·n·(n+1) - 100`; `XpToNext = 50·(n+1)(n+2) - 100 - xp`; level-up nagrada `50·n` za dosegnuti n |
| `Streak.Apply` | `(Streak s, DateOnly today) → Streak` | isti dan: ništa; jučer: +1; rupa ≤ freezes: troši freezes, +1; inače 1 |
| `DailyTasks.ForDay` | `(DateOnly day, Guid userId, int dueCount, Guid? weakestTopic) → Task[3]` | deterministički izbor; bez `review_all_due` kad `dueCount = 0` |
| `AchievementRules.Evaluate` | `(UserSnapshot) → string[] newKeys` | lista pravila nad brojčanim snapshotom |

### 4.3 `MatchEngine` (agregat partije)

Operira nad `Match` s učitanim `Players`, `Questions`, `Answers`. Metode su deterministične uz `now` i `Random(match.RngSeed)`:

```text
Start(kind, players, topic?, deck?, now)
   ranked/friend: phase veto_coin, deadline now + 3 s (coin winner već odlučen, klijent animira)
   bot/practice/calibration/ghost: phase lobby, deadline now + 5 s (practice untimed: lobby 0 s)
   svim human sjedalima last_seen_at = now
Advance(now)                                      (tick) veto_coin istek → veto_ban (deadline +8 s)
                                                  veto_ban istek → AutoBan → sljedeći turn (+8 s) ili veto_pick (chosenTopicId odmah, deadline +3 s)
                                                  veto_pick istek → topic + deck → lobby (+5 s)
                                                  lobby istek → pitanje 0 (question_started_at = now, deadline + limit + 1,5 s; bot/ghost odgovor unaprijed)
                                                  question: oba odgovorila (answered_at <= now) ili istek → timeouti po §2.4 → interstitial (+2 s)
                                                  interstitial istek (timed) → sljedeće pitanje ili Finish
                                                  u in_progress: human s last_seen_at < now - 20 s → disconnected_at; vraćeni → null
Ban(seat, topicId, now)                           faza veto_ban, moj turn, tema u poolu i nije banirana; 2 preostale → veto_pick
SubmitAnswer(seat, index, payload, clientMs, now) faza question, index == current, još nije odgovoreno; tolerancija (§2.4); grade; points; label;
                                                  oba → interstitial odmah; protivnik bot/ghost s answered_at > now → phase_deadline = answered_at
Next(seat, now)                                   samo untimed practice u interstitial → sljedeće pitanje ili Finish
Resign(seat, now)                                 finished, result resign, protivnik pobjeđuje
Finish(now)                                       §17: human s disconnected_at ≠ null na kraju = resign; oba = aborted; tiebreak; winner; phase done
```

`FinishMatch` use case nakon `Engine.Finish`, u istoj transakciji pod lockom i samo ako `finished_at` još nije postavljen:

| Korak | ranked | ghost (živi igrač) | bot | friend | practice | calibration |
| --- | --- | --- | --- | --- | --- | --- |
| Elo, `ranked_matches++`, tier histereza, `user_seasons.best_tier` | da | da (prema ghostovom `display_rating`) | ne | ne | ne | ne |
| Coins 15/5/10 (win/loss/draw) | da | da | ne | 5 oboma | ne | ne |
| SP 10 pobjeda (cap 100/dan) + 5 upset (protivnik ≥ 100 viši) | da | da | ne | ne | ne | ne |
| XP: 5 po odgovoru + 50 po partiji | da | da | da | da | da | ne |
| Tjedni leaderboard (bodovi) | da | da | ne | ne | ne | ne |
| Aktivni dan (streak), dnevni zadaci, achievementi | da | da | da | da | da | ne |
| `current_match_id = null` oboma, rematch dostupan | da | ne | ne | da | — | — |

`dedupe_key` čini ponovni poziv bezopasnim.

## 5. Vanjske ovisnosti i sučelja

| Sučelje (Application) | Implementacije (Infrastructure) | Napomena |
| --- | --- | --- |
| `ITokenService` | `JwtTokenService` (u `App.Api/Auth.cs`, uz JwtBearer paket) | HS256, ključ iz `Auth:JwtSigningKey` (env `Auth__JwtSigningKey`, ≥ 32 bajta, u `.env`), issuer/audience iz konfiguracije; `JwtBearerOptions.TimeProvider` = isti `TimeProvider` (testovi pomiču sat) |
| `IPasswordHasher` | `PasswordHasherAdapter` nad `PasswordHasher<User>` (u `App.Api/Auth.cs`) | PBKDF2; nepoznati e-mail se provjerava protiv lažnog hasha da odgovor traje jednako |
| `IEmailSender` | `EmailSender`: SMTP (`System.Net.Mail`, `Email:Smtp:Host/Port/Username/Password/From/EnableSsl` iz env) kad je `Email:Smtp:Host` postavljen, inače link ide u log (lokalni razvoj) | reset lozinke; link je `{Frontend:BaseUrl}/reset-password?token=...`; SMTP slanje je fire-and-forget (`Task.Run` + log greške) da odgovor ne otkriva postoji li e-mail; testovi koriste `FakeEmailSender` |
| `IExplanationGenerator`, `ILessonGenerator` | `ClaudeAi` (jedna klasa za oba); bez `ANTHROPIC_API_KEY` baca `NotSupportedException`. Fallback radi pozivatelj kroz `AiOptions.TryAsync`: objašnjenje → `remember` (`source: 'static'`), kartica → `concepts.description` | `Ai:TimeoutMs` (8000) ograničava svaki poziv; objašnjenje se kešira u `questions.explanation`, kartica u `concept_lesson_cards`; nikad se ne zove iz `GET /api/matches/{id}` ni iz `SubmitAnswer` |
| `TimeProvider` | `TimeProvider.System`; testovi `FakeTimeProvider` (`Microsoft.Extensions.TimeProvider.Testing`) inicijaliziran na `DateTimeOffset.UtcNow` | |

AI pozivi (`Anthropic` NuGet): `client.Beta.Messages.Create` s `Model = "claude-opus-5-5"`, effort `low` (kratak odgovor unutar 8 s), `OutputConfig.Format` JSON shema za `{whyCorrect, whyDistractor, remember}` odnosno `{title, why, code, language, misconception}` (jedna kartica po pozivu), `MaxTokens` 2048, server-side fallback (`fallbacks: "default"`, beta `server-side-fallback-2026-07-01`) za odbijene zahtjeve, `CancellationToken` s `Ai:TimeoutMs`. Ključ `ANTHROPIC_API_KEY` iz `.env`; bez ključa igra radi na fallbacku.

Novi paketi: `Microsoft.AspNetCore.Authentication.JwtBearer`, `EFCore.NamingConventions`, `Anthropic`, `Microsoft.Extensions.ApiDescription.Server` (Api: `dotnet build` piše `openapi/v1.json` koji se commita); `Testcontainers.PostgreSql`, `Microsoft.Extensions.TimeProvider.Testing` (testovi). Frontend: `openapi-typescript` (dev) za generiranje tipova iz committanog `openapi/v1.json`.

## 6. API ugovor

Konvencije: JSON camelCase; vremena ISO-8601 UTC; greške RFC 9457 Problem Details (`400` validacija s `errors`, `401` bez/istekao token (JwtBearer + `UseStatusCodePages` daju tijelo), `403` tuđi resurs, `404`, `409` pravilo igre s `type: "urn:app:<code>"` i `detail` za prikaz, `429` rate limit s `Retry-After` i Problem Details tijelom (`RateLimiterOptions.OnRejected`)). Autentikacija: `Authorization: Bearer <accessToken>` na svemu osim `/api/auth/*`, `/health*`, `GET /api/topics`. Svaki autentificirani zahtjev osvježava `users.last_seen_at` (throttle 30 s).

Kodovi za `409` (`ErrorCodes` u Application; na frontendu `type AppErrorCode = 'urn:app:topics/count' | ...` i `ApiError.code` getter):

```text
urn:app:topics/count            (nije 3–5)          urn:app:topics/locked-in-queue
urn:app:queue/already-in        urn:app:queue/not-in                 urn:app:proposal/expired          urn:app:queue/no-ghost-offer
urn:app:match/not-participant   urn:app:match/wrong-phase            urn:app:match/not-your-turn       urn:app:match/already-in-match
urn:app:match/already-answered  urn:app:match/wrong-index            urn:app:match/topic-not-in-pool   urn:app:match/not-finished
urn:app:lesson/already-passed   urn:app:lesson/retry-too-soon        urn:app:lesson/nothing-missed     urn:app:match/nothing-to-calibrate
urn:app:question/not-answered (403)  urn:app:question/not-found (404)  urn:app:lesson/not-found (404)
urn:app:shop/insufficient-coins urn:app:shop/already-owned           urn:app:shop/freeze-limit         urn:app:shop/not-owned
urn:app:auth/invalid-credentials urn:app:auth/email-taken            urn:app:auth/username-taken       urn:app:auth/reset-token-invalid
urn:app:friends/already         urn:app:challenge/expired            urn:app:daily/already-attempted   urn:app:daily/too-late
```

Za `409 match/already-answered|wrong-phase|wrong-index` klijent ne prikazuje grešku: refetcha stanje i nastavlja (zakašnjeli klik ili dupli zahtjev).

Tipovi su zapisani kao TypeScript jer idu ravno u `frontend/src/types/api.ts`; C# recordi u `App.Application` imaju ista imena i polja.

### 6.1 Zajednički tipovi

```ts
type Uuid = string; type Iso = string
type QuestionType = 'true_false'|'multiple_choice'|'cloze'|'odd_one_out'|'output'|'match_pairs'|'order'|'find_bug'
type Difficulty = 1|2|3|4
type Tier = 'bronze'|'silver'|'gold'|'platinum'|'diamond'|'master'|'grandmaster'
type ConceptState = 'unknown'|'weak'|'in_progress'|'solid'|'graduated'
type AnswerLabel = 'brilliant'|'good'|'miss'|'blunder'
type MatchKind = 'ranked'|'ghost'|'bot'|'friend'|'practice'|'calibration'
type BotPersona = 'vibe_coder'|'junior'|'mid'|'senior'          // imena lokalizira frontend
type EquipmentSlot = 'avatarFrame'|'banner'|'boardTheme'|'nameColor'|'cardBackground'

interface TopicDto { id: Uuid; slug: string; name: string; description: string; conceptCount: number }
interface ConceptDto { id: Uuid; slug: string; name: string; topicId: Uuid }
interface UserRefDto { id: Uuid; username: string; avatar: string; tier: Tier; rating: number; isProvisional: boolean; title?: string; equipment: Partial<Record<EquipmentSlot, string>> /* assetKey */ }

interface Option { id: string; text: string }
// Javni dio pitanja, bez ključa. Diskriminirana unija: ovo se u api.ts piše ručno (generator iz jsonb-a daje unknown).
type QuestionContent =
  | { type: 'true_false'; statement: string }
  | { type: 'multiple_choice'; prompt: string; code?: string; options: Option[] }
  | { type: 'cloze'; prompt?: string; code: string; options: Option[] }
  | { type: 'odd_one_out'; prompt: string; items: Option[] }
  | { type: 'output'; prompt?: string; code: string; options: Option[] }
  | { type: 'match_pairs'; prompt: string; left: Option[]; right: Option[] }
  | { type: 'order'; prompt: string; steps: Option[] }
  | { type: 'find_bug'; prompt: string; code: string; fixes: Option[] }
interface QuestionPublicDto { id: Uuid; type: QuestionType; difficulty: Difficulty; language?: string; timeLimitMs: number; basePoints: number; content: QuestionContent }
type AnswerPayload = { value: boolean } | { optionId: string } | { itemId: string } | { pairs: { leftId: string; rightId: string }[] } | { orderedIds: string[] } | { line: number; fixId: string }
interface AnswerKeyDto { answer: AnswerPayload; remember: string }
interface ExplanationDto { whyCorrect: string; whyDistractor: string; remember: string; source: 'ai'|'static' }
```

### 6.2 Auth (F0 register/login, F0b ostalo)

```ts
POST /api/auth/register   { email; username; password }  → 201 AuthResponse      // password 8–128 znakova; rate limit 5/min po IP-u
POST /api/auth/login      { email; password }            → 200 AuthResponse      // 401 invalid-credentials; rate limit 10/min po IP-u
POST /api/auth/refresh    { refreshToken }               → 200 AuthResponse      // rotira; stari se opoziva; opozvani token ponovno → 401 i opoziv cijele obitelji
POST /api/auth/logout     { refreshToken }               → 204
POST /api/auth/forgot-password { email }                 → 202 uvijek (ne otkriva postoji li e-mail); šalje link `{Frontend:BaseUrl}/reset-password?token=...`; rate limit 10/min po IP-u
POST /api/auth/reset-password  { token; newPassword }    → 204; 409 reset-token-invalid (nepoznat, iskorišten ili stariji od 1 h); opoziva sve refresh tokene i sve starije linkove korisnika; nakon 204 frontend vodi na login
// 429 (rate limit): Problem Details { status: 429, title, detail } + header Retry-After (sekunde); frontend prikaže detail i ne ponavlja zahtjev sam

interface AuthResponse { accessToken: string; accessTokenExpiresAt: Iso; refreshToken: string; user: MeDto }
```

### 6.3 Ja i profil (F0, dashboard raste kroz faze)

```ts
GET   /api/me                      → MeDto
PATCH /api/me                      { username?; avatar?; locale?; profileVisibility?; notifications? } → MeDto
PUT   /api/me/topics               { topicIds: Uuid[] }  → 204   // 3–5; 409 locked-in-queue
GET   /api/me/dashboard            → DashboardDto
GET   /api/me/matches?limit=50     → MatchSummaryDto[]        (F1; samo završene partije, najviše 50)
GET   /api/me/knowledge-map        → KnowledgeMapDto           (F2)
GET   /api/me/stats                → StatsDto                  (F5)
GET   /api/users/{username}        → PublicProfileDto          (F5; 403 ako je 'friends' i niste prijatelji)

interface MeDto extends UserRefDto {
  email: string; locale: 'en'|'hr'; profileVisibility: 'public'|'friends'; notifications: Record<string, boolean>
  rankedMatches: number; coins: number; xp: number; level: number; xpToNextLevel: number; seasonPoints: number
  streak: { count: number; lastActiveOn?: string; freezes: number; securedToday: boolean }
  activeTopicIds: Uuid[]; equippedTitleId?: Uuid; activeMatchId?: Uuid; inQueue: boolean; createdAt: Iso
}
interface DashboardDto {
  me: MeDto
  topics: { topic: TopicDto; active: boolean; mastery?: number; completion: number }[]   // mastery = prosjek odgovorenih koncepata; undefined dok nema nijednog
  ratingHistory: { at: Iso; rating: number }[]                                           // zadnjih 30 partija
  dueReviews: number
  dailyTasks: DailyTaskDto[]                                                             // F4, dotad []
  dailyChallengeAttempted: boolean
  suggestion?: { kind: 'review_due'|'practice_after_losses'|'calibrate'|'play'; topicId?: Uuid; conceptIds?: Uuid[] }
  inbox: { rematch?: { matchId: Uuid; from: UserRefDto; expiresAt: Iso }; friendChallenge?: { code: string; host: UserRefDto; expiresAt: Iso } }
  queueLocked: boolean
}
interface MatchSummaryDto { id: Uuid; kind: MatchKind; topic?: TopicDto; opponent?: { kind: 'human'|'bot'|'ghost'; persona?: BotPersona; user?: UserRefDto; displayRating: number }; myScore: number; opponentScore: number; outcome: 'win'|'loss'|'draw'|'aborted'|'completed' /* completed = trening */; ratingDelta?: number; finishedAt?: Iso; lessonId?: Uuid }
```

### 6.4 Sadržaj (F0/F2)

```ts
GET  /api/topics                           → TopicDto[]            (javno)
GET  /api/topics/{slug}/concepts           → ConceptDto[]
GET  /api/bots                             → { persona: BotPersona; displayRating: number; recommended: boolean }[]
GET  /api/questions/{id}                   → QuestionPublicDto & { key?: AnswerKeyDto }   // key samo ako sam ga odgovorio; 404 question/not-found
POST /api/questions/{id}/report            { reason } → 204        // reason 1–500; jedna po igraču; 3. igrač → quarantine (završene partije ostaju iste); 403 question/not-answered
GET  /api/questions/{id}/explanation       → ExplanationDto         // samo ako je korisnik to pitanje odgovorio (inače 403 question/not-answered); prvi poziv generira i kešira (≤ 8 s); klijent ga zove zasebno (nikad u pollingu); source 'static' = AI nedostupan, whyCorrect i whyDistractor su prazni
```

### 6.5 Partija (F1 bot/practice, F3 ranked)

```ts
POST /api/matches/bot          { persona: BotPersona; topicId: Uuid }                         → 201 MatchStateDto   (409 match/already-in-match)
POST /api/matches/practice     { topicId: Uuid; timed: boolean; conceptIds?: Uuid[] }        → 201 MatchStateDto   ("Practice this", "Due for review", prijedlog nakon poraza)
POST /api/matches/calibration  { topicId: Uuid }                                             → 201 MatchStateDto   (F2; do 5 pitanja D1–D2 nad 'unknown' konceptima, s vremenom, bez nagrade; 409 match/nothing-to-calibrate)
GET  /api/matches/{id}                                                                        → MatchStateDto  (poll 1 s; samo sudionik)
POST /api/matches/{id}/veto/ban   { topicId }                                                 → MatchStateDto
POST /api/matches/{id}/answers    { questionIndex; payload; clientElapsedMs }                 → MatchStateDto  (409 already-answered / wrong-index / wrong-phase)
POST /api/matches/{id}/next                                                                   → MatchStateDto  (samo trening bez vremena u interstitial)
POST /api/matches/{id}/resign                                                                 → MatchStateDto  (frontend traži potvrdu u 3 s)
GET  /api/matches/{id}/review                                                                 → MatchReviewDto (F2; 409 match/not-finished)
POST /api/matches/{id}/rematch                                                                → RematchDto     (F3; 20 s)
POST /api/matches/{id}/rematch/accept                                                         → MatchStateDto
GET  /api/matches/{id}/rematch                                                                → RematchDto     (Result ekran polla 2 s dok je rematchAvailable)

interface MatchStateDto {
  id: Uuid; kind: MatchKind; status: 'veto'|'lobby'|'in_progress'|'finished'|'aborted'
  phase: 'veto_coin'|'veto_ban'|'veto_pick'|'lobby'|'question'|'interstitial'|'done'
  serverTime: Iso; phaseDeadline?: Iso           // klijent računa odbrojavanja iz (deadline - serverTime), ne iz svog sata; undefined u treningu bez vremena
  topic?: TopicDto; timed: boolean; mySeat: 0|1
  players: PlayerDto[]                           // uvijek 2 (practice/calibration: 1)
  veto?: VetoDto                                 // prisutan u veto_coin|veto_ban|veto_pick
  question?: QuestionTurnDto                     // prisutan u question I interstitial (reload tijekom međuekrana ima što renderirati)
  interstitial?: InterstitialDto                 // samo u interstitial
  result?: MatchResultDto                        // samo kad je status finished|aborted
}
interface PlayerDto { seat: 0|1; kind: 'human'|'bot'|'ghost'; user?: UserRefDto; persona?: BotPersona; ghostRecordedAt?: Iso; displayRating: number; score: number; connected: boolean; hasAnswered: boolean }
// Lobby/duel prikazuju displayRating + user.tier (ne user.rating), jer ghost ima rating iz snimke.
interface VetoDto {
  pool: { topic: TopicDto; myMastery?: number; pickedByOpponent: boolean; bannedBySeat?: 0|1; auto?: boolean }[]
  coinWinnerSeat: 0|1; turnSeat?: 0|1; bansRemaining: number; finalTwo?: Uuid[]; chosenTopicId?: Uuid   // chosenTopicId je poznat od početka veto_pick: klijent animira prema njemu
}
interface QuestionTurnDto { index: number; total: number; question: QuestionPublicDto; startedAt: Iso; deliveredAt?: Iso; myAnswer?: { payload?: AnswerPayload; elapsedMs: number } }
interface InterstitialDto { index: number; key: AnswerKeyDto; mine: AnswerResultDto; opponent?: AnswerResultDto }
interface AnswerResultDto { payload?: AnswerPayload; isCorrect: boolean; fraction: number; points: number; elapsedMs: number; timedOut: boolean; label: AnswerLabel }
interface MatchResultDto {
  winnerSeat?: 0|1; reason: 'score'|'tiebreak_correct'|'tiebreak_time'|'draw'|'resign'|'disconnect'|'aborted'
  mine: { score: number; correct: number; ratingBefore?: number; ratingAfter?: number; sp: number; coins: number; xp: number; streakSecured: boolean }
  opponent?: { score: number; correct: number }
  cards: { blunder?: ReviewQuestionDto; brilliant?: ReviewQuestionDto; practiceConcept?: { concept: ConceptDto; masteryDelta: number } }
  lessonAvailable: boolean; lessonRewardEligible: boolean; lessonId?: Uuid; rematchAvailable: boolean; reviewAvailable: boolean
}
interface MatchReviewDto {
  match: MatchSummaryDto
  questions: ReviewQuestionDto[]
  masteryDelta: { concept: ConceptDto; before: number; after: number }[]
}
interface ReviewQuestionDto { index: number; question: QuestionPublicDto; key: AnswerKeyDto; concept: ConceptDto; slot: 'warmup'|'targeted'|'core'|'review'; mine: AnswerResultDto; opponent?: AnswerResultDto; explanation?: ExplanationDto; reported: boolean }
interface RematchDto { status: 'none'|'pending'|'accepted'|'declined'|'expired'; requestedBySeat?: 0|1; expiresAt?: Iso; matchId?: Uuid }
```

Tok na frontendu: `useMatch(id)` polla `GET /api/matches/{id}` svake 1 s dok `status` nije `finished|aborted`; svaka komanda vraća isti DTO pa se state odmah zamijeni. Timer pitanja klijent mjeri `performance.now()`-om od prvog rendera (nakon reloada kreće od `serverTime - deliveredAt`) i šalje `clientElapsedMs`; vizualno odbrojavanje koristi `timeLimitMs`, rezano na `phaseDeadline - serverTime - 500 ms`. `rematchAvailable` = ranked/friend, protivnik čovjek, viđen u zadnje 2 min. Trening bez vremena: nakon međuekrana klijent zove `GET /api/questions/{id}/explanation` pa `POST .../next`.

### 6.6 Red čekanja (F3)

```ts
POST   /api/queue                               → QueueStateDto   (409 already-in, topics/count, match/already-in-match)
GET    /api/queue                               → QueueStateDto   (poll 1 s)
DELETE /api/queue                               → 204             (dok je proposed: ponuda se odbija, drugi igrač ostaje u redu)
POST   /api/queue/proposals/{id}/accept         → QueueStateDto
POST   /api/queue/ghost                         → 201 MatchStateDto  (409 queue/no-ghost-offer)
POST   /api/queue/ghost/decline                 → QueueStateDto      (ostaje u redu, nudi se bot)

interface QueueStateDto {
  status: 'idle'|'waiting'|'proposed'|'matched'
  enteredAt?: Iso; serverTime: Iso; rangeNow?: number; topicIds: Uuid[]
  proposal?: { id: Uuid; opponent: UserRefDto; expiresAt: Iso; iAccepted: boolean }
  ghostOffer?: { opponent: UserRefDto; recordedAt: Iso; topic: TopicDto }    // nakon 30 s
  botOffer: boolean                                                          // nakon 30 s bez ghosta ili nakon odbijanja; POST /api/matches/bot napušta red
  matchId?: Uuid                                                             // status matched → navigiraj
}
```

### 6.7 Poduka i napredak (F2)

```ts
POST /api/lessons                 { matchId: Uuid }                          → 201 LessonDto   (409 lesson/nothing-missed, match/not-finished; 403 tuđa partija; druga za istu partiju: bez nagrade)
GET  /api/lessons/{id}                                                       → LessonDto
POST /api/lessons/{id}/check      { answers: { questionId: Uuid; payload: AnswerPayload }[] }  → LessonResultDto  (409 retry-too-soon / already-passed)
GET  /api/me/progress/due                                                    → { concepts: (ConceptDto & { reviewDueAt: Iso; reviewStep: number; topic: TopicDto })[] }

interface LessonDto { id: Uuid; matchId?: Uuid; status: 'generated'|'passed'|'failed'; concepts: ConceptDto[]; cards: LessonCardDto[]; check: { question: QuestionPublicDto; isVariantOfMissed: boolean }[]; attempt: number; rewardEligible: boolean; retryAfter?: Iso; lastResults?: LessonResultDto['results'] }
interface LessonCardDto { conceptId: Uuid; title: string; why: string; code?: string; language?: string; misconception: string }
interface LessonResultDto { passed: boolean; results: { questionId: Uuid; isCorrect: boolean; key: AnswerKeyDto; isVariantOfMissed: boolean }[]; patchedConcepts: ConceptDto[]; rewards: { coins: number; sp: number; xp: number }; retryAfter?: Iso }
interface KnowledgeMapDto {
  topics: { topic: TopicDto; active: boolean; mastery?: number; completion: number; calibrationAvailable: boolean
            concepts: { concept: ConceptDto; mastery: number; state: ConceptState; reviewDueAt?: Iso; reviewStep?: number
                        lastAnswers: { questionId: Uuid; type: QuestionType; prompt: string; isCorrect: boolean; at: Iso }[] }[] }[]
}
```

### 6.8 Ekonomija i dnevni ciklus (F4)

```ts
GET  /api/shop                              → { items: (ShopItemDto & { owned: boolean })[] }
POST /api/shop/purchase   { itemId }        → MeDto   (409 insufficient-coins / already-owned / freeze-limit)
PUT  /api/me/equipment    { avatarFrame?; banner?; boardTheme?; nameColor?; cardBackground?; titleId? }  → MeDto  (null = skini; 409 shop/not-owned)
GET  /api/me/rewards?limit=50               → RewardEventDto[]
GET  /api/daily-challenge                   → DailyChallengeDto     // prvi poziv dana otvara pokušaj (startedAt)
GET  /api/daily-challenge/{day}             → DailyChallengeDto     // arhiva; key/explanation samo ako sam pokušao
POST /api/daily-challenge/attempt { payload; clientElapsedMs }  → DailyChallengeDto   (409 already-attempted / daily/too-late nakon 60 s + 1,5 s: pokušaj se zapisuje kao istek, 5 coina)
GET  /api/me/daily-tasks                    → { tasks: DailyTaskDto[]; bonusAwarded: boolean }

interface ShopItemDto { id: Uuid; key: string; kind: string; name: string; price: number; rarity: string; assetKey: string }
interface RewardEventDto { type: string; coins: number; xp: number; sp: number; at: Iso; referenceId?: Uuid }
interface DailyChallengeDto { day: string; question: QuestionPublicDto /* timeLimitMs = 60000 */; startedAt?: Iso; serverTime: Iso; attempted: boolean; myAttempt?: AnswerResultDto & { rank?: number }; key?: AnswerKeyDto; explanation?: ExplanationDto; leaderboard: { rank: number; user: UserRefDto; isCorrect: boolean; elapsedMs: number }[] }
interface DailyTaskDto { key: string; progress: number; target: number; completed: boolean; reward: number; topic?: TopicDto }
```

### 6.9 Rang-liste, sezone, društveno (F5)

```ts
GET /api/leaderboards/{board}?topic={slug}   board: 'global'|'season'|'weekly'|'topic'|'friends'   → LeaderboardDto
GET /api/seasons/current                     → { id; number; startsAt; endsAt; doubleLearningSp: boolean }
GET /api/me/achievements                     → { key: string; name: string; earnedAt?: Iso; progress?: number; target?: number }[]
GET /api/me/titles                           → { id: Uuid; key: string; name: string; kind: string; count: number; active: boolean }[]
GET    /api/friends                          → FriendDto[]
POST   /api/friends/requests  { username }   → 201 FriendDto     (409 friends/already)
POST   /api/friends/requests/{userId}/accept → FriendDto
DELETE /api/friends/{userId}                 → 204
POST   /api/friend-challenges { friendUserId?: Uuid }  → 201 { code: string; expiresAt: Iso }   // s friendUserId: pojavi se u njegovom DashboardDto.inbox
GET    /api/friend-challenges/{code}         → { code; host: UserRefDto; matchId?: Uuid; expiresAt: Iso }
POST   /api/friend-challenges/{code}/join    → 201 MatchStateDto  (veto; 409 challenge/expired)

interface LeaderboardDto { board: string; entries: LeaderboardEntryDto[]; me?: { entry: LeaderboardEntryDto; around: LeaderboardEntryDto[] } ; resetsAt?: Iso }
interface LeaderboardEntryDto { rank: number; user: UserRefDto; value: number }    // value = Elo | SP | tjedni bodovi | graduirani koncepti
interface FriendDto { user: UserRefDto; status: 'pending_in'|'pending_out'|'accepted'; online: boolean }
interface StatsDto { matches: number; wins: number; losses: number; draws: number; winRate: number; accuracyByType: Record<QuestionType, number>; avgTimeMsByType: Record<QuestionType, number>; mostCommonBlunderType?: QuestionType; brilliantCount: number; conceptsPatched: number; conceptsGraduated: number; ratingHistory: { at: Iso; rating: number }[] }
interface PublicProfileDto { user: UserRefDto; topics: { topic: TopicDto; mastery?: number; completion: number }[]; titles: string[]; achievements: string[]; stats: Pick<StatsDto, 'matches'|'winRate'|'conceptsGraduated'> }
```

### 6.10 Što frontend radi dok backend nije tu, i kako klijent radi

- `src/types/api.ts`: ručno pisani tipovi svih DTO-a iz §6. Backend ga ažurira u istom PR-u u kojem mijenja DTO, pa je on izvor istine za frontend. Za provjeru uživo: `http://localhost:5080/scalar` i `http://localhost:5080/openapi/v1.json` dok API radi lokalno (Development).
- `src/lib/api-client.ts`: access token samo u memoriji (`setAccessToken`), refresh token iza `tokenStore` (async `get/set/clear`; web `localStorage`, Android Capacitor Preferences). Na 401: jedan dijeljeni in-flight `refresh()` promise za sve pozivatelje, zatim jedan retry originalnog zahtjeva; `/api/auth/*` se nikad ne retrya; pad refresha → `tokenStore.clear()` + `onUnauthenticated()` (router na `/login` s return URL-om). `request<T>(path, init, { auth: false })` za javne rute. `ApiError.code` vraća `problem.type` kao `AppErrorCode`.
- `src/hooks/use-poll.ts`: `usePoll(load, intervalMs, until)` zadržava zadnje podatke, nikad ne vraća `loading` nakon prvog uspjeha (postojeći `useAsync` bi treperio svake sekunde), staje kad `until(data)` vrati true, pauzira na `document.hidden`, refetcha odmah na `visibilitychange`/`appStateChange`.
- `src/lib/fixtures/` drži po jedan primjer svakog DTO-a (kopirano iz ovog dokumenta) za razvoj ekrana prije endpointa; nema mock servera.
- Konstante na klijentu: lista avatara, mapiranje `assetKey` → slika/CSS, lokalizirana imena bot persona, pragovi tierova za prikaz (iz `UserRefDto.tier`, ne računa se na klijentu).
- Ekrani prate faze: F0 onboarding + dashboard, F1 duel + rezultat, F2 analiza + poduka + mapa + trening, F3 red + veto, F4 shop + zadaci + dnevni izazov, F5 rang-liste + prijatelji + profil.

### 6.11 Stanje po fazama: što frontend može spojiti sada

Što backend već poslužuje (u `development`) i što ekrani trebaju znati, a ne piše u tipovima. Svaka faza dodaje svoj odlomak.

**F0: auth, onboarding, dashboard**
- `register`, `login`, `refresh`, `logout`, `GET/PATCH /api/me`, `PUT /api/me/topics`, `GET /api/me/dashboard`, `GET /api/topics`, `GET /api/topics/{slug}/concepts`.
- Validacija je `400` s `errors` po polju; ključevi su imena svojstava DTO-a u PascalCase (`Username`, `Password`), pa ih spajaj s poljima forme bez obzira na velika slova; `409` s `type` `urn:app:auth/email-taken` ili `urn:app:auth/username-taken` ide uz polje forme.
- `GET /api/topics` vraća samo aktivne teme, one koje imaju dovoljno pitanja (zasad `javascript`, `sql`, `how-ai-works`). Onboarding traži 3–5 (`409 urn:app:topics/count`).
- Token flow je opisan u §6.10 i još nije u `api-client.ts`; to je prvi posao frontenda.

**F1: bot duel, trening, povijest**
- `GET /api/bots` (persona s `recommended`), `POST /api/matches/bot`, `POST /api/matches/practice`, `GET /api/matches/{id}`, `POST .../answers`, `POST .../next`, `POST .../resign`, `GET /api/me/matches`.
- Bot partija: `lobby` 5 s → 10 × (`question` → `interstitial` 2 s) → `done`. Poll svake 1 s; odbrojavanje iz `phaseDeadline - serverTime`, ne iz sata klijenta.
- Ključ odgovora (`interstitial.key`) postoji tek u međuekranu; `question.question.content` ga nikad nema.
- Kad odgovoriš prije bota, faza se zatvara čim bot "odgovori"; kad su svi odgovorili, međuekran dolazi odmah.
- `find_bug`: linije koda broje se od 1 (`split('\n')`); ključ može imati `acceptLines` (druge linije iste naredbe) za isticanje.
- `409 urn:app:match/already-in-match`: `me.activeMatchId` vodi natrag u partiju (i nakon reloada).
- Trening bez vremena nema `phaseDeadline`; nakon međuekrana klijent zove `POST .../next`. Objašnjenje (`/api/questions/{id}/explanation`) dolazi tek u F2.
- `result.cards` (blunder, brilliant, koncept za vježbu) je popunjen; rating, coins, XP i SP su 0 do F3/F4, a `rematchAvailable` je `false` do F3.

**F0b: reset lozinke i rate limit**
- `/forgot-password` uvijek vraća 202: ekran uvijek kaže "ako račun postoji, poslali smo link", bez obzira na e-mail.
- Link iz e-maila je `{Frontend:BaseUrl}/reset-password?token=...`; ruta `/reset-password` čita `token` iz querya i šalje `POST /api/auth/reset-password`. `204` → login (sve sesije su odjavljene); `409 urn:app:auth/reset-token-invalid` → ponudi novi link. Lokalno nema maila: link je u logu API-ja.
- `429` (login, register, forgot-password, prebrzo slanje odgovora): prikaži `detail`, header `Retry-After` je u sekundama; ne ponavljaj zahtjev automatski.

**F2: analiza, objašnjenja, poduka, red ponavljanja, mapa znanja, kalibracija**
- Rezultat: `reviewAvailable` je `true`; `lessonAvailable` kad je išta promašeno; `lessonId` je zadnja poduka za tu partiju (ako postoji, otvori nju s `GET /api/lessons/{id}` umjesto nove); `lessonRewardEligible` za gumb "Poduka (+50)".
- Analiza: `GET /api/matches/{id}/review`. `explanation` je u pitanju samo ako ga je netko već otvorio; inače klikom zovi `GET /api/questions/{id}/explanation` (prvi put može trajati do 8 s, prikaži loader). `source: 'static'` znači da AI nije dostupan: prikaži samo `remember`. "Prijavi pitanje" je `POST /api/questions/{id}/report` (204, i kad je već prijavljeno).
- Poduka: `POST /api/lessons { matchId }` (201). Kartice redom (jedna po konceptu), zatim 3 kontrolna pitanja bez vremena, `check[0]` je varijanta promašenog. `POST /api/lessons/{id}/check { answers }` vraća rezultat s ključevima. Pad: `retryAfter` (10 min), nova pitanja se vide tek nakon `GET /api/lessons/{id}`; `409 urn:app:lesson/retry-too-soon` prije toga. `rewards` je `0` do F4.
- Trening bez vremena: u međuekranu zovi objašnjenje pa `POST .../next` (igrač je odgovorio, pa endpoint radi).
- Red ponavljanja: `dashboard.dueReviews` i `suggestion` (`review_due` s `topicId` i `conceptIds`, spremno za `POST /api/matches/practice`). Popis: `GET /api/me/progress/due`.
- Mapa znanja: `GET /api/me/knowledge-map`, boja po `state`; `completion` je postotak (0–100) graduiranih; klik na čvor: `lastAnswers` + `GET /api/questions/{questionId}` (s ključem jer je odgovoreno) + "Vježbaj ovo" (`practice` s `conceptIds: [id]`). Gumb "Kalibriraj" kad je `calibrationAvailable`: `POST /api/matches/calibration { topicId }` je obična partija za jednog igrača (do 5 pitanja, s vremenom).
- Dashboard `topics[].mastery` i `completion` sada imaju prave vrijednosti.

## 7. Seed sadržaja

- `backend/src/App.Infrastructure/Seed/topics.json` (teme s konceptima: slug, name, description; redoslijed u datoteci je `sort_order`) i `Seed/questions/<tema>.json` (niz pitanja: `key`, `concept` (slug), `type`, `difficulty`, `language`, `content` s `answer`, `remember`). Kasnije `shop-items.json`, `titles.json`. Datoteke su embedded resources u Infrastructure assemblyju.
- `Seeder` se pokreće na startu kad je `Database:SeedOnStartup=true` (Development i prod) i radi upsert po `slug`/`key` (`questions.seed_key`); nikad ne briše i ne vraća u igru pitanje koje je u karanteni ili povučeno. Na kraju postavi `topics.is_active` = tema gradi potpun ranked deck za novog igrača (D1–D2, ništa viđeno); `GET /api/topics` vraća samo aktivne teme.
- Novo pitanje se dodaje u JSON i provjerava s `dotnet test` (`SeedTests`); format i primjer svakog tipa su u postojećim datotekama.
- Hackathon pitanja se pišu kroz Claude Code sesije ravno u JSON (po konceptu, tipu, težini), a `SeedTests` su drugi prolaz validacije: točno jedan točan odgovor, svi ID-evi opcija jedinstveni, kod ≤ 25 linija i ≤ 60 znakova, `find_bug.line` unutar koda, koncept i tema postoje, `order` ima 4–5 koraka, `match_pairs` 4+4 i desna strana nije u redoslijedu ključa, svaki koncept ima ≥ 1 `find_bug` i ≥ 1 `output` u temi, i **svaka aktivna tema gradi ranked deck za band D1–D2 s praznom poviješću** (ograničenja decka §7/§8 ideje ispunjiva).
- Offline batch generator s AI validacijom (§16 ideje) je roadmap: `tools/QuestionGen` konzolni projekt koji piše iste JSON datoteke.

## 8. Pozadinski procesi (`GameTickService`)

```text
svake 1 s   AdvanceMatches(now)   matches gdje phase_deadline <= now (indeks), FOR UPDATE SKIP LOCKED, svaka partija u vlastitoj transakciji i try/catch;
                                  disconnect oznake za human sjedala u in_progress
svake 2 s   RunMatchmaking(now)   parovi iz queue_entries (FOR UPDATE SKIP LOCKED, preskače current_match_id), ghost ponude nakon 30 s, istekli proposals, istekli rematch zahtjevi
svake 60 s  Housekeeping(now)     istekli friend_challenges i refresh tokeni; RefreshGrandmasters (top 100 po ratingu s >= 10 partija);
                                  ponedjeljak 00:00 → CloseWeek; kraj sezone → CloseSeason (snapshot, titule, season_drop, SP reset);
                                  novi dan → daily_challenges za 7 dana unaprijed, RecalibrateDifficulty (>= 50 odgovora: > 85 % točnih → D-1, < 30 % → D+1, po answers)
```

`PeriodicTimer` petlja, novi DI scope po iteraciji, iznimka se logira i ne ruši host. `Game:TickEnabled=false` u testovima; testovi zovu use caseove direktno s `FakeTimeProvider`. Jedna instanca API-ja (VPS), pa nema distribuirane brave. `// ponytail: single-instance tick; add a leader lock (pg_advisory_lock) if a second API instance ever appears.`

## 9. TDD plan

### 9.1 Testni projekti

| Projekt | Što | Infrastruktura |
| --- | --- | --- |
| `tests/App.Domain.Tests` (novi, dodaje se u `App.slnx`) | čiste funkcije iz §4; `MatchEngine` nad in-memory agregatom | xUnit, bez baze, paralelno, < 1 s |
| `tests/App.Api.Tests` (postojeći, proširen) | HTTP ugovor + use caseovi + baza; jedan test = jedan scenarij kroz API | `WebApplicationFactory<Program>` + `Testcontainers.PostgreSql` (collection fixture, jedan kontejner po runu, `TRUNCATE` svih tablica prije svakog testa, seed minimalnog sadržaja), `[assembly: CollectionBehavior(DisableTestParallelization = true)]`, `FakeTimeProvider` (start = `UtcNow`, isti instance u `JwtBearerOptions.TimeProvider`), `FakeEmailSender`, fake AI generatori, `Game:TickEnabled=false`, `RateLimit:Enabled=false` (jedan test ga uključuje vlastitom factoryjem), `Ai:TimeoutMs=50` |

Pomoćnici u Api testovima: `TestApp.RegisterAsync("ana") → AuthedClient` (klijent s tokenom; nakon `Clock.Advance` iznad 15 min sam ponovno logira), `Seed.Topic("sql", concepts: 10, questionsPerConcept: 8)`, `Clock.Advance(seconds)`, `Tick()` (zove `AdvanceMatches` + `RunMatchmaking`), `PlayThrough(match, answers)`.

Pravilo rada: za svaki redak dolje prvo crveni test, pa najmanji kod, pa refaktor. Redoslijed unutar faze je redoslijed implementacije. Imena prate postojeći stil (`Liveness_is_healthy_without_a_database`).

### 9.2 Domain testovi (pišu se prije use caseova faze koja ih treba)

```text
Clock
  Today_uses_zagreb_zone                                 23:30 UTC zimi → sutrašnji datum
  Week_starts_monday
Scoring
  Full_time_remaining_gives_base_points                  (100, 0 ms/15 000) → 100
  Half_time_remaining_gives_75_percent                   → 75
  No_time_remaining_gives_half_base                      → 50
  Wrong_answer_gives_zero
  Partial_fraction_scales_points                         match_pairs 2/4 točna u 0 ms → 75
  Timed_out_gives_zero
  Untimed_gives_base_times_fraction
Grader (po tipu)
  TrueFalse_matches_value / MultipleChoice_matches_option / Cloze / OddOneOut / Output
  MatchPairs_fraction_is_correct_pairs_over_four         3/4 → 0.75
  Order_is_all_or_nothing
  FindBug_wrong_line_is_zero_even_with_right_fix
  FindBug_right_line_wrong_fix_is_40_percent
  FindBug_right_line_right_fix_is_exactly_1
  Malformed_payload_is_zero_not_exception
Labels
  Brilliant_requires_D3_or_D4_correct_under_40_percent
  Correct_D2_fast_is_good
  Partial_answer_is_good_not_blunder                     3/4 parova u 10 % vremena → good
  Partial_under_half_is_miss
  Wrong_fast_is_blunder / Wrong_D1_any_time_is_blunder / Wrong_slow_is_miss / Timeout_is_miss
  Untimed_has_only_good_and_miss
Elo
  Equal_ratings_win_gives_plus_16_with_K32
  K_drops_to_16_after_30_ranked_matches
  Draw_between_equal_ratings_changes_nothing
  Upset_win_against_200_higher_gives_24
Tier
  Rating_1300_is_gold / Gold_drops_to_silver_only_below_1275 / Rating_1299_from_silver_stays_silver
Mastery
  First_answer_uses_alpha_1                              m=50, n=0, correct D1 mc (w=0.7·0.75) → 76.25
  Alpha_floor_is_015_after_six_answers
  Wrong_answer_uses_full_difficulty_weight               bez pogodivosti
  Partial_answer_mastery_uses_fraction                   r = 100·fraction, pogodivost se primjenjuje
  Blunder_multiplies_weight_by_12
  Untimed_practice_halves_weight
  Timeout_counts_as_wrong_without_blunder
  Mastery_is_clamped_0_100
  Graduated_concept_dropping_below_70_loses_graduation_and_reenters_review
ConceptState
  No_answers_is_unknown / Below_40_is_weak / 40_to_70_is_in_progress / 70_plus_is_solid / Graduated_flag_wins
ReviewSchedule
  Patch_sets_step_1_due_tomorrow
  Success_moves_to_next_step_and_interval               1d→3d→7d→14d→30d
  D1_question_halves_interval
  Fail_resets_to_step_1_and_streak_0
  Wrong_answer_on_any_question_resets_review_to_step_1
  Correct_answer_before_due_changes_nothing
  Step_5_success_with_85_plus_graduates
  Step_5_success_below_85_stays_at_step_5
Veto
  Pool_always_includes_shared_topics
  Pool_is_capped_at_6
  Bans_for_pool_size_3_4_5_6_are_1_2_3_4
  Ban_out_of_turn_is_rejected / Ban_of_banned_topic_is_rejected
  Auto_ban_picks_random_unbanned_with_seed
  Two_remaining_topics_pick_randomly_with_seed
DeckBuilder
  Band_pairs_neighbor_on_nearer_threshold_side           1350+1420 → D2–D3; 1000+1000 → D1–D2; 1150+1150 → D1–D2; 1800+1900 → D3–D4
  Deck_has_10_questions_first_is_true_false_D1
  Targeted_slots_prefer_concepts_weak_for_both
  Targeted_slot_skips_concept_weak_for_one_strong_for_other   (A 80, B 30 → jezgra)
  Unknown_concepts_are_never_targeted
  Targeted_slots_become_core_when_a_player_has_under_5_ranked_matches
  Bot_match_targets_only_human_weak_concepts
  Review_slot_uses_due_concept_of_either_player
  Review_slot_becomes_core_when_nothing_due
  No_concept_appears_twice
  Questions_seen_in_last_30_days_are_skipped
  Deck_respects_type_min_and_max                        ≥1 find_bug, ≥1 output, max po tipu
  Deck_has_1_to_2_drag_and_drop_total
  Missing_difficulty_falls_back_to_neighbor_then_other_type_then_core
  Concept_targeted_in_last_two_matches_is_rotated_out
  Positions_2_to_10_are_shuffled_by_seed                isti seed → isti deck
  Practice_deck_prefers_requested_then_due_then_weakest
  Practice_deck_degrades_instead_of_failing_on_thin_pool
Tiebreak
  Higher_score_wins / Equal_score_more_correct_wins / Equal_correct_less_time_on_correct_wins / Everything_equal_is_draw
ResultCards
  Blunder_is_costliest_miss / Brilliant_is_best_answer / Practice_concept_is_biggest_drop / Cards_are_null_when_nothing_qualifies
BotPlayer
  Accuracy_drops_8_percent_per_difficulty_above_2
  Wrong_answer_is_a_distractor_not_the_key
  Wrong_answer_on_partial_types_scores_zero
  Elapsed_never_exceeds_time_limit_minus_slack          Junior na T/N (8 s) odgovara u ≤ 7,5 s
Matchmaking
  Range_starts_100_grows_50_every_10s_caps_400
  Candidate_needs_shared_topic
  Closest_rating_wins_then_most_shared_topics_then_longest_wait
  Third_consecutive_match_against_same_opponent_is_skipped
  Gap_over_400_never_matches
GhostOffer
  Candidate_requires_active_topic_unseen_questions_and_normal_finish
  Own_recordings_and_offered_recordings_are_skipped
  Most_recent_recording_wins
MatchEngine
  Ranked_match_starts_in_veto_coin_with_3s_deadline
  First_ban_deadline_is_8s_after_coin_phase
  Bot_match_starts_in_lobby_with_deck
  Lobby_deadline_starts_question_0
  Answer_before_opponent_marks_waiting_and_hides_opponent
  Both_answered_moves_to_interstitial_immediately
  Human_answer_sets_deadline_to_bot_answered_at
  Timeout_writes_zero_answer_and_moves_on
  Client_elapsed_outside_tolerance_is_replaced_by_server_elapsed
  Second_answer_for_same_index_is_rejected
  Answer_in_interstitial_is_rejected
  Untimed_practice_waits_for_next
  Tenth_interstitial_finishes_match
  Resign_finishes_with_opponent_as_winner
  Disconnected_player_at_finish_counts_as_resign
  Both_disconnected_aborts_without_rating_change
  Ghost_answers_count_only_after_their_recorded_time
Level / Streak / DailyTasks / AchievementRules
  New_user_is_level_1_and_level_2_needs_200_xp         level n kumulativno 50·n·(n+1) − 100
  Xp_to_next_level_is_reported
  Streak_increments_on_consecutive_day / Streak_uses_freeze_for_one_missed_day / Streak_resets_after_gap_without_freeze
  Three_tasks_per_day_deterministic_per_user
  Review_all_due_task_not_offered_when_nothing_due
  First_win_achievement_fires_once
```

### 9.3 API testovi po fazama

```text
F0 Auth i profil (minimum)
  Register_returns_tokens_and_me
  Register_with_taken_email_is_409 / Register_with_taken_username_case_insensitive_is_409
  Register_with_short_password_is_400_validation / Register_with_blank_body_is_400 / Malformed_json_is_400_not_500
  Login_with_wrong_password_is_401_problem
  Protected_endpoint_without_token_is_401 / Expired_access_token_is_401_problem
  Me_returns_defaults                                                   rating 1000, provisional, bronze, 0 coins, level 1
  Set_topics_requires_3_to_5
  Topics_are_public_and_sorted
  Patch_me_updates_locale_and_visibility
  Dashboard_returns_topics_with_active_flags_and_due_count
F1 Sadržaj i bot duel
  Seed_is_idempotent                                                     dva runa, isti broj redova
  SeedTests: Every_question_has_exactly_one_correct_answer, Code_fits_25_lines_60_chars, Every_concept_has_find_bug_and_output,
             MatchPairs_right_ids_do_not_reveal_pairing, Every_active_topic_builds_a_deck_for_D1_D2_with_empty_history
  Start_bot_match_returns_lobby_with_10_questions_without_answer_keys
  Start_bot_match_twice_is_409_already_in_match
  Match_state_hides_answer_key_until_interstitial
  Submit_answer_returns_state_with_my_answer_and_opponent_hidden
  Tick_after_bot_time_reveals_interstitial_with_both_results            čovjek u 3 s, bot u 5 s → međuekran na ticku nakon 5 s, ne nakon limita
  Interstitial_includes_question_and_key
  Tick_after_interstitial_advances_index
  Answer_after_deadline_is_409_wrong_phase
  Full_bot_match_finishes_with_result_cards_and_no_rating_change
  Each_answer_writes_answers_row_and_updates_concept_mastery
  Bot_match_grants_xp_but_not_coins_or_sp
  Bot_match_counts_for_activity_day
  Match_history_lists_finished_match
  Non_participant_gets_403
  Practice_untimed_has_no_deadline_and_advances_on_next
  Practice_untimed_halves_mastery_weight
  Practice_with_concept_ids_targets_them
F0b Auth ostatak
  Refresh_rotates_token_and_revokes_old / Reused_revoked_refresh_token_is_401_and_revokes_family
  Logout_revokes_refresh_token
  Login_is_rate_limited                                                  vlastita factory s RateLimit:Enabled=true
  Forgot_password_always_202_and_sends_email_only_for_existing_user     (FakeEmailSender)
  Reset_password_with_valid_token_changes_password_and_revokes_sessions
  Reset_token_is_single_use_and_expires_after_1h                       uz to: reset troši i starije linkove
  Reset_with_short_password_is_400_validation
F2 Analiza, poduka, mapa
  Review_lists_10_questions_with_labels_and_mastery_delta
  Review_before_finish_is_409
  Explanation_is_generated_once_and_cached                              fake generator broji pozive
  Explanation_falls_back_to_remember_when_generator_times_out           Ai:TimeoutMs=50
  Report_question_three_times_quarantines_it_and_keeps_match_result
  Start_lesson_builds_cards_from_missed_and_chronic_concepts
  Lesson_with_no_missed_question_is_409
  Lesson_check_has_variant_of_missed_question_first
  Variant_falls_back_to_neighbor_difficulty
  Lesson_pass_requires_variant_plus_one_more
  Lesson_pass_patches_concepts_and_sets_review_due_tomorrow
  Lesson_fail_allows_retry_after_10_minutes_without_reward_same_day     retry dobiva nova pitanja
  Retry_on_a_later_day_is_reward_eligible
  Second_lesson_for_same_match_is_not_reward_eligible_and_reuses_cached_cards
  Lesson_cards_fall_back_to_the_concept_description_when_the_generator_times_out
  Unanswered_question_shows_no_key_and_no_explanation
  Due_concepts_appear_in_dashboard_and_due_endpoint
  Due_concept_answered_correctly_moves_review_step
  Timeout_updates_mastery_as_wrong
  Knowledge_map_shows_states_completion_and_last_answers
  Topic_mastery_ignores_unknown_concepts
  Calibration_uses_5_D1_D2_questions_on_unknown_concepts_without_reward
F3 Rangirano
  Join_queue_requires_topics / Join_twice_is_409 / Join_while_in_match_is_409
  Set_topics_is_locked_while_in_queue
  Two_compatible_players_get_proposal_within_one_tick
  Incompatible_topics_never_match
  Range_widens_with_time_and_matches_after_30s
  Proposal_expires_after_10s_decliner_leaves_queue_other_keeps_wait_time
  Leaving_queue_while_proposed_declines_and_returns_other_to_waiting
  Both_accept_creates_ranked_match_in_veto_coin
  Simultaneous_accepts_create_exactly_one_match
  Veto_pool_shows_my_mastery_and_opponent_picks_only
  Ban_out_of_turn_is_409 / Ban_timeout_auto_bans_and_passes_turn
  Veto_ends_with_random_topic_deck_and_lobby
  Live_match_full_playthrough_updates_elo_sp_coins_xp_tier
  Simultaneous_answers_from_both_players_reach_interstitial
  Provisional_rating_hidden_until_10_matches
  Resign_gives_full_loss
  Disconnect_20s_marks_disconnected_questions_timeout_without_mastery_and_end_counts_as_resign
  Both_disconnected_aborts
  Tie_resolves_by_correct_then_time_then_draw
  Rematch_request_expires_in_20s / Rematch_accept_skips_queue_and_starts_veto / Rematch_appears_in_opponent_inbox
  Ghost_offer_after_30s_from_recent_recording_in_range
  Ghost_match_replays_recorded_answers_and_changes_only_live_player_rating
  Same_recording_is_offered_once / Recordings_older_than_30_days_not_offered / Declined_ghost_offers_bot
  Sp_win_cap_100_per_day / Upset_bonus_5_sp
  Weekly_points_only_from_ranked_and_ghost
F4 Ekonomija
  Lesson_pass_rewards_50_coins_100_xp_and_2_sp_per_eligible_concept
  Same_concept_rewards_at_most_once_in_7_days                           dan 6 → ne, dan 8 → da
  Ledger_sum_equals_cached_balance                       invarijanta nakon niza nagrada
  Dedupe_key_prevents_double_reward                      FinishMatch dvaput → jedan red
  Daily_tasks_are_three_and_progress_from_activity
  Completing_all_three_gives_bonus_30
  Streak_milestone_7_days_gives_100_coins_once
  Buy_streak_freeze_max_2 / Freeze_consumed_on_missed_day
  Purchase_deducts_coins_and_adds_item / Insufficient_coins_is_409 / Concurrent_purchases_cannot_overspend
  Equip_requires_ownership
  Daily_challenge_first_get_starts_attempt / Daily_challenge_rejects_answer_after_60s / Daily_challenge_leaderboard_orders_correct_then_time
  Daily_challenge_rewards_30_correct_5_attempt / Daily_challenge_archive_shows_key_only_after_attempt
  Level_up_rewards_50_times_level
  Weekly_xp_goal_500_gives_100_coins_once_per_week
F5 Društveno i rang-liste
  Global_board_excludes_provisional_players
  Top_100_global_are_grandmaster
  Season_board_orders_by_sp / Weekly_board_resets_monday / Topic_board_counts_graduated / Friends_board_only_friends
  Board_includes_me_with_two_above_and_below
  Patched_sp_doubles_in_first_two_weeks_of_season
  Close_season_snapshots_ranks_awards_titles_and_tier_cosmetic_resets_sp_keeps_elo
  Close_week_awards_top10_badge_count
  Knowledge_title_for_100_percent_graduated_and_lost_below_70
  Friend_request_accept_remove / Friend_request_in_both_directions_is_one_row / Online_status_from_last_seen
  Friend_challenge_code_joins_veto_unranked_5_coins_each / Targeted_friend_challenge_appears_in_inbox
  Public_profile_hides_concept_mastery / Friends_only_profile_is_403_for_strangers
  Stats_accuracy_by_type_and_most_common_blunder
  Retiring_question_reverts_its_mastery_deltas_and_keeps_match_result
  Question_moves_down_a_band_when_too_easy
```

### 9.4 Frontend provjere

`npm run build && npm run lint` ostaju jedina obavezna provjera. Dodatno (po želji tima): jedan Playwright smoke test "registracija → odabir tema → bot duel do rezultata" kad F1 bude gotov; `playwright-cli` skill je već instaliran.

## 10. Proširivost: kako se dodaje...

| Što | Koraci |
| --- | --- |
| Nova statistika | upit nad `answers`/`match_players`/`reward_events` u `GetStats`; polje u `StatsDto`; nema migracije |
| Novi tip pitanja | record u `QuestionContent` (+ grana u `Parse`), grana u `Grader` i `BotPlayer`, redak u `GameRules.Types`, seed validacija, član TS unije i renderer na frontendu; `questions.type` CHECK dobiva vrijednost u migraciji |
| Nova tema / koncepti | JSON u `backend/seed/`, re-seed; ništa u kodu |
| Novi izvor nagrade | `RewardType` + jedan `Rewarder.Grant(...)` poziv s `dedupe_key`; redak u tablici §4.3 ako ovisi o vrsti partije |
| Novi achievement / titula / dnevni zadatak | pravilo u `AchievementRules` / `titles.json` / `DailyTasks` lista |
| Novi leaderboard | `case` u `GetLeaderboard` s upitom |
| Nova bot persona | redak u `BotPersonas` |
| Premium (tutor, još pitanja, objasni svojim riječima) | `users.is_premium` + novi endpointi pod `/api/lessons/{id}/premium/*` s postojećim `ILessonGenerator` obrascem; generirana pitanja se ne spremaju u `questions` |
| Hrvatski pool | `questions.locale` stupac (default `'en'`) + filter u deck builderu |
| SignalR | `MatchHub` koji šalje `MatchChanged(matchId)`; klijent na event zove postojeći `GET`; polling ostaje fallback |
| Push obavijesti | `device_tokens(user_id, token, platform)`, FCM slanje iz `Housekeeping` |
| Druga API instanca | `pg_advisory_lock` oko ticka, inače ništa |

## 11. Sigurnost i rubovi

- Lozinke: `PasswordHasher<User>` (PBKDF2), 8–128 znakova; nikad u logovima.
- Proxy: API stoji iza host Caddyja i Caddyja u web kontejneru. Host Caddy odbacuje `X-Forwarded-For` koji pošalje klijent i postavlja pravi IP; web Caddy (`trusted_proxies static private_ranges` u `frontend/Caddyfile`) ga čuva i dodaje svoj hop. API (`UseForwardedHeaders`, prvi u pipelineu, `ForwardLimit = null`, `KnownIPNetworks` = privatni rasponi + loopback) ide zdesna dok ne naiđe na prvu javnu adresu, i to je IP igrača. Bez toga rate limit po IP-u vidi jedan IP za sve.
- Rate limit (`AddRateLimiter`, stdlib): `/api/auth/login` i `/api/auth/forgot-password` 10/min, `/api/auth/register` 5/min po (proslijeđenom) IP-u; `/api/matches/*/answers` 5/s po korisniku; `RejectionStatusCode = 429` + Problem Details + `Retry-After`.
- Refresh tokeni: rotacija s `family_id`; predočenje već opozvanog tokena opoziva cijelu obitelj (ukradeni token postaje beskoristan).
- Ključ odgovora nikad ne napušta server prije međuekrana; `MatchStateMapper` je jedino mjesto koje gradi `QuestionPublicDto` i ima test `Match_state_hides_answer_key_until_interstitial`.
- Vrijeme odgovora: tolerancija 1,5 s prema `delivered_at`, inače serversko; uzorak "stalno < 1 s na D3–D4" je zastavica u `GetStats` (roadmap pregled), bez automatskih kazni.
- Svi upisi u partiju idu pod row lock i idempotentni su ili odbijeni s 409; `dedupe_key` na ledgeru; `current_match_id` sprječava dvije partije.
- Korisnik vidi samo svoje partije, poduke, nagrade; javni profil poštuje `profile_visibility`.
- Reset lozinke: token 32 nasumična bajta, SHA-256 u bazi, 1 h, jednokratan; odgovor 202 bez obzira na e-mail; slanje asinkrono da vrijeme odgovora ne otkriva korisnika.
- `.env` dobiva `Auth__JwtSigningKey`, `Email__Smtp__*`, `ANTHROPIC_API_KEY` (opcionalno; bez njega AI fallback), `Frontend__BaseUrl` (za link u e-mailu); `.env.example` prazne placeholdere.

## 12. Otvorena pitanja za Mateja (ideja ne precizira ili je u sukobu sama sa sobom; dokument pretpostavlja prvo)

1. **Početni mastery** nepoznatog koncepta: 50, uz pravilo da koncept s 0 odgovora ne ulazi u ciljane slotove ni u prosjek teme.
2. **Graduiranje**: tablica stanja (§11) kaže "≥ 85 i 3 uzastopna uspješna ponavljanja", raspored kaže "nakon koraka 5", §14 traži "30+ dana". Pretpostavka: uspjeh na koraku 5 uz mastery ≥ 85 (nakon ~55 dana ponavljanja; pad resetira na korak 1, pa je "3 uzastopna" implicirano).
3. **"D1 koncepti imaju upola kraće intervale"**: koncepti nemaju težinu, pitanja imaju. Pretpostavka: interval se prepolovi kad je dospjelo pitanje bilo D1.
4. **Band težine**: §3 kaže "band prosjeka ±1", primjer u §8 (1350 + 1420 → D2–D3) tome ne odgovara (bilo bi D1–D3). Pretpostavka: dva susjedna banda na strani bližeg praga (odgovara primjeru i "±1 za raznolikost").
5. **Isti protivnik**: §4 kaže "ne uparuje tri zaredom", §17 "najviše tri zaredom". Pretpostavka: treća zaredom se ne uparuje iz reda; revanš je iznimka; ghost se ne broji.
6. **Tema bot duela**: ideja ne kaže tko bira. Pretpostavka: igrač bira temu i personu.
7. **Kontrolna pitanja poduke**: bez timera, šalju se sva tri odjednom, poštuju 30-dnevno pravilo viđenosti. Ako želimo timer kao u duelu, prelaze na match engine.
8. **Poduka nakon pada**: "bez nagrade za drugi pokušaj istog dana" čitamo doslovno: pokušaj idući dan je nagrađen.
9. **XP u nerangiranim partijama**: §12 isključuje Elo/coins/SP, ne XP. Pretpostavka: XP se daje u bot, friend i trening partijama, ne u kalibraciji.
10. **Dan i tjedan**: `Europe/Zagreb` za streak, dnevne zadatke, capove i izazov. Alternativa UTC.
11. **Ghost protivnik u decku**: snimka fiksira deck, pa ciljani slotovi živog igrača ne postoje u ghost duelu. Prihvatljivo? Alternativa ne bi bila "ista partija".
12. **Grandmaster**: top 100 po Elo-u među igračima s ≥ 10 partija, osvježava se svake minute, prikazuje se umjesto tiera.
13. **Streak 20:00 obavijest i tjedni izvještaj**: bez push kanala do Androida; na webu samo badge na dashboardu.
