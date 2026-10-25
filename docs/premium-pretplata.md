# Premium pretplata: Junior developer i Senior developer

Ideja implementacije pretplate u StackOffu: što koji plan sadrži, kako je to izvedeno u kodu, kako se plaćanje
simulira za hackathon i što se mijenja kad se spoji pravi naplatni sustav. Poslovni dio (cijene, faze,
B2B) je u `docs/biznis-model.md`; API ugovor je i u `docs/tehnicki-dokument.md` §6.13.

Grana: `feature/subscription-premium`.

## 1. Planovi

| | **Junior developer** | **Senior developer** |
| --- | --- | --- |
| Cijena | besplatno, zauvijek | 4,99 € / mjesec ili 39,99 € / godina (3,33 € / mjesec, ušteda 33 %) |
| Rangirano, duel, analiza, poduka, mapa znanja, dnevni ciklus, rang-liste, prijatelji | sve | sve |
| Rang bodovi (Elo) | standardno | **×1,2 na svaki dobitak ratinga**; poraz košta isto |
| XP za level | standardno | **×1,2 na sav XP** (i level-up kaskada ide s uvećanim XP-om) |
| AI Profesor | **2 pitanja, 1 poduka i 1 kviz dnevno** | bez dnevnog ograničenja |
| Dnevni novčići | — | **+25 novčića** svaki dan kad se prijavi (jedan klik na početnoj) |
| Paket dobrodošlice | — | **po jedna animirana legendarna kozmetika iz svake sekcije trgovine**, zauvijek |
| Senior značka | — | u izborniku, uz rezultat duela, na stranici plana |

Načelo iz ideje igre (§15) ostaje: premium prodaje brzinu, dubinu i status, **nikad pristup učenju**. Rangirano,
analiza partije, osnovna poduka s kontrolnim pitanjima, mapa znanja i rang-liste su jednaki za sve. Jedino
"gejtano" učenje je AI Profesor, i to kvotom, ne zabranom: Junior ga i dalje koristi svaki dan.

### Zašto baš ovo

- **×1,2 rating i XP**: Senior brže vidi napredak (tier, level), a to je ono što motivira dnevni povratak. Množi se
  samo dobitak: gubitak košta isto, pa pretplata ne štiti od pada i ne kvari matchmaking (par se i dalje traži po
  ratingu). Vidi §8 za rizik "pay-to-win" dojma i opciju da množitelj ide samo na SP.
- **AI Profesor kvota**: svaki poziv Profesora je plaćeni poziv modela. Kvota 2/1/1 dnevno drži trošak Junior
  korisnika ispod ~0,2 € mjesečno, a Senior je jedini plan gdje neograničena upotreba ima pokriće.
- **Paket dobrodošlice**: legendarne stavke koštaju 6 000–8 500 novčića (tjedni igranja). Jedan paket po slotu
  daje odmah vidljiv status (animirani okvir, ime, banner, kartica, ploča), a trgovina ostaje coin sink za sve ostalo.
- **25 novčića dnevno**: razlog da se Senior prijavi svaki dan (streak, dnevni zadaci), a dovoljno malo da ne
  obezvrijedi novčiće iz igre (pobjeda 15, poduka 50).

## 2. Model podataka

Pretplata su četiri stupca na `users` (nema zasebne tablice: jedan korisnik ima najviše jednu pretplatu, a
povijest plaćanja u produkciji vodi naplatni sustav, ne mi):

```sql
alter table users add column
  premium_until      timestamptz,          -- Senior developer dok je u budućnosti; null = nikad plaćeno
  premium_interval   text check (premium_interval is null or premium_interval in ('monthly','yearly')),
  premium_auto_renew boolean not null default false,
  premium_card_last4 text;                  -- jedino što se od kartice ikad čuva
```

- `Plan` nije stupac nego izvedenica: `IsSenior(now) => premium_until > now`. Istek ne treba posao ni događaj:
  sutra je korisnik Junior sam od sebe, a `premium_until` u prošlosti čuva "bio je Senior do".
- Ledger (`reward_events`) dobiva tip `premium_daily` s ključem `premium_daily:{dan}:{userId}`: dnevni novčići se ne
  mogu isplatiti dvaput, kao ni bilo koja druga nagrada.
- Paket dobrodošlice su obični redovi u `user_items` (`on conflict do nothing`): ponovni pretplatnik ga ne dobiva
  dvaput, a stavke ostaju i nakon isteka.

Migracija: `20261025045907_Premium`.

## 3. Gdje su pravila u kodu

| Pravilo | Mjesto |
| --- | --- |
| Svi brojevi (množitelji, kvote, cijene, ključevi paketa, "0002" odbijanje) | `App.Domain/Users/Premium.cs` |
| `IsSenior(now)`, `PlanAt(now)` | `App.Domain/Users/User.cs` |
| ×1,2 na dobitak ratinga | `RankedResult.Apply` → `Boost` u `App.Domain/Matches/Elo.cs` (vrijeme = `match.FinishedAt`) |
| ×1,2 na XP | `Rewarder.GrantAsync` u `App.Application/Economy/Rewarder.cs`: jedino mjesto koje piše ledger, pa pokriva partije, poduke, dnevni izazov i sve buduće izvore. Vraća stvarno isplaćeni `Grant`, pa ekran rezultata prikazuje uvećani XP |
| Kvota Profesora | `ProfessorService.UsageAsync` / `EnsureQuotaAsync`: broji današnja pitanja korisnika (`professor_messages` role `user`), poduke i kvizove (`created_at >= početak dana` u `Europe/Zagreb`). Provjera ide prije poziva modela; neuspio odgovor se ne sprema pa ne troši kvotu; već napisana poduka/kviz za isti odgovor vraća postojeće bez kvote |
| Pretplata, otkaz, dnevni novčići, paket | `App.Application/Subscription/SubscriptionService.cs` |
| Endpointi | `App.Api/Endpoints/SubscriptionEndpoints.cs` |
| `plan`, `premiumUntil` u `MeDto` | `MeService.ToDtoAsync` |

Greške (Problem Details, `type`): `urn:app:professor/quota` (409), `urn:app:payment/declined` (402),
`urn:app:subscription/not-active` (409), `urn:app:subscription/senior-only` (409).

## 4. API

```ts
GET    /api/subscription               → SubscriptionDto
POST   /api/subscription               { interval: 'monthly'|'yearly'; cardHolder; cardNumber; cardExpiry 'MM/YY'; cardCvc } → SubscriptionDto  (400; 402 payment/declined)
DELETE /api/subscription               → SubscriptionDto   // isključi obnovu; Senior do premiumUntil (409 subscription/not-active)
POST   /api/subscription/daily-coins   → MeDto             // 25 novčića, jednom dnevno (409 subscription/senior-only; drugi klik istog dana vraća isto stanje)

interface SubscriptionDto {
  plan: 'junior'|'senior'; premiumUntil?: Iso; interval?: 'monthly'|'yearly'; autoRenew: boolean; cardLast4?: string
  dailyCoins: number; dailyCoinsClaimed: boolean
  plans: { interval: 'monthly'|'yearly'; priceCents: number; days: number }[]        // cijene dolaze s API-ja, frontend ih ne zna
  professor: { questionsUsed: number; questionsLimit?: number; lessonsUsed: number; lessonsLimit?: number; quizzesUsed: number; quizzesLimit?: number }  // limit undefined = bez limita
}
```

`MeDto` dobiva `plan` i `premiumUntil`, pa svaki ekran zna je li igrač Senior bez dodatnog poziva.
Plaćanje dok je korisnik još Senior **dodaje** vrijeme na postojeći istek (mjesečni + godišnji = 395 dana), što
pokriva i "produlji" i promjenu intervala bez posebnih endpointa.

## 5. Plaćanje: simulacija sada, Stripe poslije

**Sada (hackathon).** `POST /api/subscription` prima podatke kartice, validira oblik (DataAnnotations: 13–19
znamenki, `MM/YY`, 3–4 znamenke CVC), odbija broj koji završava na `0002` (konvencija Stripeovih testnih kartica)
i inače "naplaćuje" uspješno. Čuva se samo `last4`. Frontend (`PayDialog`) ima gumb *Use this card* koji upiše
testnu karticu `4242 4242 4242 4242 · 12/28 · 123`, pa demo traje jedan klik. Stranica i dijalog jasno pišu da
se ništa ne naplaćuje.

**Produkcija.** Mijenja se samo "naplata", ostatak ostaje:

1. **Stripe Checkout (hosted)**: `POST /api/subscription` više ne prima karticu nego vraća `checkoutUrl`
   (Stripe Price ID po intervalu). Kartica nikad ne dira naš server, pa nema PCI opsega.
2. **Webhook `POST /api/stripe/webhook`** (potpis provjeren): `checkout.session.completed` i
   `invoice.paid` → `premium_until = current_period_end`, `premium_interval`, `premium_auto_renew = true`,
   paket dobrodošlice; `customer.subscription.deleted` / `invoice.payment_failed` nakon retryja → `auto_renew =
   false`. Logika je ista kao danas u `SubscribeAsync`, samo je okidač webhook, a ne klik.
3. **Otkaz**: `DELETE /api/subscription` zove `subscriptions.update(cancel_at_period_end = true)`; naš
   `premium_auto_renew = false` ostaje izvor istine za UI.
4. **Portal**: link na Stripe Customer Portal za promjenu kartice i račune (ne gradimo svoje).
5. **Android**: Google Play Billing za pretplate kupljene u aplikaciji (obvezno po pravilima trgovine), isti
   webhook-stil preko Real-time developer notifications; `premium_until` je zajednički nazivnik oba kanala.
6. Dodati `stripe_customer_id` i `stripe_subscription_id` na `users` (dva stupca), ništa drugo u shemi.

Što simulacija namjerno ne radi: automatska obnova po isteku (u produkciji je to Stripeov posao; ovdje
`auto_renew` samo govori UI-ju što će se dogoditi), povrat novca, računi.

## 6. Frontend

- `/senior` (`features/subscription/SubscriptionPage.tsx`): dva plana jedan uz drugi, prekidač mjesečno /
  godišnje (ušteda u %), redovi pogodnosti s točnim pravilima, *Pay with card*. Senior vidi karticu stanja
  (obnavlja se / Senior do, kartica •••• 4242, *Produlji*, *Otkaži obnovu*).
- `PayDialog.tsx`: forma kartice s *Use this card*, uspjeh u istom dijalogu (do kada, što je dobio, link na
  ormarić). Greška 402 ima svoju poruku.
- `SeniorDailyCard.tsx` na početnoj: *Pokupi 25* → `POST /api/subscription/daily-coins`, saldo u gornjoj traci se
  odmah osvježi; nakon toga *Gotovo* do sutra. Renderira se samo kad je `me.plan === 'senior'`.
- AI Profesor: Junior ispod naslova vidi *Danas: 1/2 pitanja · 0/1 poduke · 0/1 kviza* s linkom *Postani Senior*;
  kvota kao greška ima prevedenu poruku (`urn:app:professor/quota`).
- Značka *Senior* u izborniku (uz korisničko ime) i redak *Senior bonus ×1,2 uračunat* na ekranu rezultata kad je
  bilo dobitka. Stavka *Senior* u navigaciji.
- Cijene i datumi se formatiraju po jeziku (`Intl`), cijene dolaze s API-ja u centima.

## 7. Testovi

`App.Domain.Tests/PremiumTests.cs`: dobitak ratinga ×1,2 a gubitak ne; istekla pretplata ne množi; zaokruživanje;
plan iz `premium_until`.

`App.Api.Tests/SubscriptionTests.cs`: plaćanje → Senior 30 dana, paket u ormariću, `MeDto.plan`; odbijena kartica
402 bez promjene; 400 za loš interval/broj; ponovno plaćanje dodaje vrijeme, otkaz drži Senior do isteka pa Junior;
dnevni novčići samo za Seniora, jednom dnevno, drugi dan opet; XP ×1,2 u ledgeru i kešu (novčići ne); Junior 2/1/1
dnevno, Senior bez limita, sutra opet. Dva postojeća testa Profesora koja pitaju više od dvaput sad igraju kao
Senior (`SeniorAsync`).

## 8. Rubovi i odluke

- **Dojam pay-to-win.** Množitelj na Elo može izgledati kao kupnja ranga. Zašto je prihvatljiv: množi se samo
  dobitak, matchmaking i dalje traži par po ratingu (Senior s napuhanim ratingom igra jače protivnike i gubi
  jednako), a značka to čini vidljivim. Ako žiri ili igrači to ne prihvate, promjena je jedan redak:
  množitelj preseliti sa `RankedResult.Boost` na SP (`Rewards.ForMatch`), gdje je i sezonski 2× za učenje, pa
  Senior brže skuplja sezonske bodove, a Elo ostaje čist.
- **Dan** je `Europe/Zagreb` kao i sve ostalo dnevno (streak, zadaci, capovi): kvota i novčići se resetiraju u
  ponoć po lokalnom vremenu.
- **Istek bez obnove** (simulacija): korisnik tiho postane Junior; kozmetika ostaje, novčići ostaju, kvota
  Profesora se opet primjenjuje od tog trenutka.
- **Paket kad već posjeduje stavku** (kupio je novčićima): ništa se ne događa; nema povrata novčića.
- **Promjena intervala**: nije poseban slučaj, nova uplata se dodaje na postojeći istek i zapisuje novi
  interval.
- **Rate limit Profesora** (10 poruka/min) ostaje za sve; kvota je iznad njega.

## 9. Roadmap premiuma (nije u kodu)

Iz ideje igre §15 i onoga što postojeći sustavi omogućuju gotovo besplatno:

| Pogodnost | Zašto ima smisla uz ono što već postoji |
| --- | --- |
| Mjesečni kozmetički drop | `season_drop_tier` mehanizam već dijeli stavke po pravilu; dodati `premium_drop_month` |
| *Objasni svojim riječima* s AI ocjenom u poduci | Profesor već ima chat s kontekstom; rubrika točno/nedostaje/krivo |
| Napredna statistika i izvoz profila znanja (PDF/link) | mapa znanja, mastery i povijest su u bazi; ovo je i most prema B2B (biznis-model §3) |
| Prioritet u redu čekanja / ghost duel bez čekanja 30 s | `GhostOfferAfterSeconds` po planu |
| Treći slot za zamrzavanje streaka | `MaxStreakFreezes` po planu (check constraint postaje 0–3) |
| Rani pristup novim temama | `topics.is_active` + `premium_from` datum |
| Obiteljski / Team plan | isti `premium_until`, plaća ga organizacija (biznis-model §3) |
