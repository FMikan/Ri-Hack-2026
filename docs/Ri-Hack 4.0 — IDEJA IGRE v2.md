# Ri-Hack 4.0 — detaljna razrada ideje igre

Oct 24, 2026 · @Matej

## 1. Vizija i pozicioniranje

Rangirana 1v1 igra IT znanja u kojoj natjecanje tjera na učenje, a svaki poraz završava u poduci. Rečenica za žiri: *chess.com za IT znanje — gdje te svaki poraz nauči zašto si izgubio.*

Pet stvari koje igru definiraju:

1. **Natjecanje je poticaj, ne ograničenje.** Matchmaking, rating, leaderboardi, streak i sezone daju razlog za povratak svaki dan.
2. **Znanje je jedini put prema gore.** Bez znanja rating ne raste; bez ratinga nema jačih protivnika i boljih titula.
3. **Svaka partija ostavlja trag.** Analiza imenuje praznine, poduka ih krpa, a sljedeći deck ih namjerno provjerava.
4. **Dvije odvojene mjere.** Rating (Elo) kaže tko si kao igrač; mastery po konceptu kaže što znaš. Nikad se ne miješaju u jedan broj.
5. **Fokus na ono što ljudi prepuštaju AI-ju bez razumijevanja.** Osam tema iz developmenta, uključujući temu koja uči kako AI radi i kada griješi.

Jezgreni loop: **Duel → Rezultat → Analiza → Poduka → Mastery i ponavljanje → adaptivni deck → Duel.** Sve ostalo (bot, trening, dnevni izazov, ekonomija) služi da se taj loop vrti češće.

## 2. Korisnik, profil i odabir tema

Korisnik se registrira, bira 3 do 5 tema i odmah može igrati; sve ostalo na profilu raste iz partija i poduka.

**Registracija i onboarding**

1. E-mail ili OAuth, korisničko ime, avatar.
2. Odabir tema: minimalno 3, maksimalno 5. Bez 3 odabrane teme gumb *Igraj* je zaključan.
3. Bez kalibracije u onboardingu: mastery kreće kao *nepoznato* i deck builder prvih 5 rangiranih partija koristi samo jezgru i ponavljanje. Kalibracija po temi se nudi kasnije s mape znanja (§11), da prva partija dođe što prije.
4. Dashboard.

**Što profil sadrži**

| Element | Što je | Tko ga mijenja |
| --- | --- | --- |
| Rating | Elo, start 1000, prikazan s `?` prvih 10 partija | rangirane 1v1 partije |
| Rank tier | vizualni razred izveden iz Elo-a | automatski |
| Sezonski bodovi (SP) | brojač tekuće sezone | pobjede i zakrpani koncepti |
| Mastery po temi | 0–100, prosjek koncepata teme | svaki odgovor (duel, bot, trening, poduka) |
| Mastery po konceptu | 0–100 + stanje (nepoznat / slab / u radu / savladan / graduiran) | svaki odgovor |
| Mapa znanja | vizualna mreža koncepata po temi | izvedeno iz masteryja |
| Streak | uzastopni dani aktivnosti | partija, poduka ili dnevni izazov |
| Coins, XP, level | ekonomija | izvori iz §15 |
| Titula i kozmetika | jedna opremljena titula, okvir, banner, tema ploče | korisnik bira iz zarađenog |
| Achievementi | trajni zapisi (sezonske titule, 100 % teme, streak milestonei) | automatski |
| Povijest partija | zadnjih 50 s linkom na analizu | automatski |
| Statistika | win rate, prosječno vrijeme po tipu, najčešći tip blundera, omjer točnosti po tipu pitanja | izvedeno |

**Teme na profilu**

- Aktivne teme stoje kao chipovi na dashboardu; promjena je moguća bilo kad, osim dok je igrač u redu čekanja (ulazak u red zaključava chipove).
- Deaktivirana tema zadržava mastery i mapu znanja; samo ne ulazi u matchmaking.
- Profil je javan po defaultu (rating, tier, titula, mapa znanja na razini tema); mastery po konceptu vidi samo vlasnik.

**Postavke:** vidljivost profila (javno / prijatelji), obavijesti (dnevni izazov, streak u opasnosti u 20:00, zahtjev za revanš), jezik sučelja (engleski zadano, hrvatski kao opcija).

## 3. Teme, taksonomija koncepata i pool pitanja

Osam tema, svaka razbijena na 15–25 koncepata; svako pitanje nosi koncept, tip i težinu, i to je podatak na kojem počiva sve adaptivno u igri.

**Osam tema**

| Tema | Što ljudi prepuštaju AI-ju bez razumijevanja | Seed koncepti |
| --- | --- | --- |
| JavaScript i runtime | async kod koji radi dok ne radi | event loop, Promise i async/await, closures, `this`, `==` vs `===`, mutacija vs kopija, hoisting |
| Frontend | komponente koje se renderiraju 40 puta | DOM i reflow, state i re-render, flex/grid, CSS specifičnost, pristupačnost, memo/lazy |
| Backend, API i arhitektura | endpointi bez validacije, slojevi bez smisla | HTTP semantika i status kodovi, REST dizajn, validacija, error handling, auth tok, middleware, coupling |
| Baze i SQL | upit radi na 100 redaka, umire na milijun | JOIN tipovi, indeksi, N+1, transakcije i ACID, normalizacija, NULL semantika |
| Mreže i web protokoli | CORS greška ide ravno u ChatGPT | TCP vs UDP, DNS, HTTP metode/cache/cookies, CORS, TLS, WebSocket vs HTTP |
| Cybersecurity | AI generira ranjiv kod, nitko ga ne čita | SQLi, XSS, CSRF, hashing vs enkripcija, secrets u kodu, JWT zamke, input validacija |
| Algoritmi i strukture podataka | radi, ali O(n²) | složenost, hash mapa vs lista, rekurzija, sortiranje, BFS/DFS, stog i red |
| Kako radi AI | koriste ga svaki dan, ne znaju što radi | tokeni i kontekst, halucinacije, temperature, embeddings i RAG, prompt vs fine-tune, kada mu ne vjerovati |

Roadmap teme: Git i DevOps, OS/Linux, mobile, game dev.

**Taksonomija**

Tema → koncept → pitanje. Svako pitanje nosi:

| Polje | Vrijednost |
| --- | --- |
| Tip | jedan od 8 tipova (§7) |
| Težina | D1–D4 |
| Koncept | 1 primarni, do 1 sekundarni |
| Sadržaj | tekst, kod (max 25 linija, označen jezik), opcije, točan odgovor |
| Objašnjenje | zašto je točno točno, zašto je najčešći distraktor privlačan, jedna rečenica *zapamti*; keširano trajno |
| Status | aktivno / prijavljeno / karantena / povučeno |
| Statistika | postotak točnih, prosječno vrijeme, broj prikaza |

**Težina i rating**

| Težina | Band ratinga | Karakter pitanja |
| --- | --- | --- |
| D1 | < 1100 | definicija, prepoznavanje, očita greška |
| D2 | 1100–1400 | primjena jednog koncepta, semantička greška |
| D3 | 1400–1700 | dva koncepta u interakciji, greška vidljiva tek iz konteksta |
| D4 | 1700+ | rubni slučajevi, distraktori koji su skoro točni |

Deck vuče iz banda prosjeka ratinga oba igrača, ±1 band za raznolikost.

**Pool i njegovo održavanje**

- Pool se generira offline u batchu po konceptu, tipu i težini; drugi prolaz provjerava točnost i jednoznačnost; uzorak se ručno pregleda prije aktivacije.
- Gumb *Prijavi pitanje* u analizi: nakon 3 prijave pitanje ide u karantenu i ne izvlači se dok ga netko ne pregleda. Rezultati već odigranih partija se ne mijenjaju.
- Auto-kalibracija težine: nakon 50 odgovora, ako D2 pitanje rješava više od 85 % igrača, spušta se u D1; ako ga rješava manje od 30 %, diže se u D3.
- Isto pitanje ne ide istom igraču unutar 30 dana (povijest viđenih pitanja).
- Cilj seed-a: 20 koncepata × 4 pitanja = 80 pitanja po temi; za hackathon 3 teme duboko, 5 plitko.

**Jezik sadržaja:** cijela aplikacija i pool pitanja su na engleskom; prebacivanje sučelja na hrvatski postoji od prvog dana, a hrvatski pool pitanja je zaseban generirani batch (roadmap). Kod, termini i nazivi API-ja ostaju engleski u oba jezika.

## 4. Matchmaking

Klik na *Igraj* stavlja igrača u red; par se traži po ratingu uz uvjet barem jedne zajedničke teme, a nakon 20 sekundi bez para nudi se bot.

**Pravila uparivanja**

1. U red ulazi zapis (igrač, rating, aktivne teme, vrijeme ulaska).
2. Početni raspon ±100 Elo; svakih 10 s raspon se širi za 50, do najviše ±400.
3. Kandidat je valjan ako je u rasponu i dijeli barem jednu aktivnu temu.
4. Među valjanim kandidatima prednost ima najmanja razlika ratinga, zatim najviše zajedničkih tema, zatim dulje čekanje.
5. Isti protivnik se ne uparuje tri rangirane partije zaredom, osim kroz revanš.
6. Pronađen par: oba igrača vide *Protivnik pronađen* i imaju 10 s za *Prihvati*. Tko ne prihvati, ispada iz reda; drugi ostaje s očuvanim vremenom čekanja.
7. Nakon 20 s bez para: ponuda *Igraj protiv bota* (nerangirano) ili *Nastavi čekati*; nakon 30 s ponuda ghost duela (rangirano, pravila dolje), koja zamjenjuje bota dok je aktivna. Red se ne napušta dok igrač sam ne odluči.

**Što igrač vidi dok čeka:** proteklo vrijeme, trenutni raspon ratinga, svoje aktivne teme, gumb *Odustani*. Promjena tema nije moguća u redu; odustajanje otključava chipove.

**Rangirane partije su 1v1 iz ovog reda, žive ili ghost.** Bot, trening i izazov prijatelja ne mijenjaju Elo.

**Ghost dueli (asinkroni)**

Svaka rangirana partija se snima: deck, odgovori i vremena oba igrača. Ghost duel je ista partija odigrana kasnije protiv te snimke, pa rangirana igra ne ovisi o tome je li netko online u istom trenutku.

- Kandidat je snimka igrača u rasponu ±100 Elo (širi se kao u redu), na temi koju živi igrač ima aktivnu, čija pitanja živi igrač nije vidio u zadnjih 30 dana. Prednost ima najsvježija snimka.
- Veto se preskače; tema je tema snimke. Lobby prikazuje ghost ikonu uz protivnikovo ime i datum snimke.
- Tijek je isti kao u živoj partiji; međuekran prikazuje ghostov snimljeni odgovor i vrijeme za to pitanje.
- Elo i SP se mijenjaju samo živom igraču, računano prema ghostovom ratingu iz trenutka snimke. Ghostu se ništa ne mijenja i ne dobiva obavijest.
- Ghost duel se broji kao rangirana partija za streak, dnevne zadatke, coine i tjedni leaderboard.
- Ista snimka se jednom igraču nudi samo jednom; snimke starije od 30 dana se ne nude.
- Revanš nakon ghost duela ne postoji; gumb vodi na novu partiju.

## 5. Veto faza

Unija tema oba igrača se banovima naizmjence svodi na dvije, a tema partije se iz te dvije bira nasumično; coin flip samo određuje tko banira prvi.

**Tijek**

1. Pool = unija aktivnih tema oba igrača, ograničena na 6 tema: zajedničke teme ulaze uvijek, ostatak se nasumično dopunjava iz unije (3 do 6 tema u poolu). Svaka tema prikazuje igračev vlastiti mastery kao traku; teme koje je protivnik odabrao nose ikonu. Protivnikov mastery se ne prikazuje.
2. Coin flip, animacija 3 s. Dobitnik banira prvi.
3. Ban naizmjence, 8 s po potezu. Istek vremena = automatski ban nasumične teme iz poola.
4. Banovi traju dok u poolu ne ostanu 2 teme.
5. Nasumični odabir jedne od dvije preostale, animacija 3 s. To je tema partije.

**Broj banova po veličini poola**

| Veličina poola | Banovi | Tko banira |
| --- | --- | --- |
| 3 | 1 | dobitnik coina |
| 4 | 2 | A, B |
| 5 | 3 | A, B, A |
| 6 | 4 | A, B, A, B |

Nasumični korak na kraju uklanja prednost zadnjeg bana: nitko ne bira temu, svatko samo uklanja ono što mu ne odgovara.

**Trajanje:** 10 do 30 s: najviše 4 bana po 8 s plus dvije animacije. Deck se gradi u trenutku kad je tema poznata; pitanja dolaze iz poola, pa je priprema trenutna i faza ne mora skrivati latenciju.

## 6. Duel — tijek partije

Partija je 10 identičnih pitanja za oba igrača, svako sa svojim timerom koji kreće od prikaza; cijela partija s veto fazom traje 4 do 5 minuta.

**Tijek**

1. **Lobby (5 s):** protivnikovo ime, tier, rating, tema partije, odbrojavanje 3-2-1.
2. **Pitanje:** prikazuje se oboma istodobno; timer svakog igrača kreće u trenutku prikaza na njegovom ekranu. Odgovor se zaključava na klik (odabir, T/N, uljez, cloze, output), na *Potvrdi* (povezivanje, redoslijed) ili u dva koraka (nađi bug: klik linije, pa odabir fixa).
3. **Čekanje:** tko odgovori prvi vidi *Odgovoreno* i čeka; protivnikov odgovor se ne vidi.
4. **Međuekran (2 s):** točan odgovor, je li svatko pogodio, bodovi oba igrača za to pitanje, trenutni zbroj. Pokreće se kad oba odgovore ili istekne vrijeme sporijem.
5. Koraci 2–4 za svih 10 pitanja; redoslijed i sadržaj identični oboma.
6. **Kraj:** zbroj bodova, pobjednik, promjena Elo-a i SP-a, prelazak na ekran rezultata (§10).

**Trajanje**

| Dio | Vrijeme |
| --- | --- |
| Veto faza | 10–30 s |
| Lobby | 5 s |
| 10 pitanja (timeri iz §7) | 190–230 s ako oba igrača iskoriste puno vrijeme, u praksi 120–180 s |
| Međuekrani | 20 s |
| Ukupno | 3–5 min |

**Pravila tijekom partije**

- Istek timera = 0 bodova za to pitanje, oznaka *miss* u analizi.
- Nema slobodnog chata. Set od 6 emota (gg, wow, oops, 🔥, 🤔, 👏) je roadmap, ne hackathon.
- Igrač može otvoriti *Odustani*; potvrda u 3 s. Odustajanje = poraz s punim Elo gubitkom.
- Prekid veze: 20 s za ponovno spajanje. Pitanja u međuvremenu teku i nose 0 bodova. Ako se igrač ne vrati do kraja partije, računa se kao odustajanje.
- Ako se oba igrača odspoje, partija se poništava bez promjene Elo-a.
- Neriješeno po bodovima: pobjeđuje igrač s više točnih odgovora, zatim manji zbroj vremena na točnim odgovorima; isto i to → remi (0,5 boda u Elo-u).
- Pitanje koje netko prijavi tijekom ili nakon partije ne mijenja rezultat; samo ide u karantenu.

## 7. Tipovi pitanja

Osam tipova, svi deterministički ocjenjivani; tri od njih (cloze, uljez, output) su mehanički odabir s drugačijim prikazom, dva (povezivanje, redoslijed) dijele drag-and-drop.

| Tip | Mehanika | Vrijeme | Bazni bodovi | Djelomični bodovi | Ograničenja u decku |
| --- | --- | --- | --- | --- | --- |
| Točno/netočno | jedna tvrdnja, dva gumba | 8 s | 50 | ne | uvijek 1. pitanje (D1); još najviše 1 |
| Odabir | pitanje + 4 opcije | 15 s | 100 | ne | najviše 3 |
| Cloze u kodu | kod s jednim `____`, 4 tokena | 18 s | 120 | ne | najviše 2 |
| Koji ne pripada | 4 stavke, jedna uljez | 12 s | 100 | ne | najviše 2 |
| Što ispisuje kod | snippet + 4 moguća izlaza | 25 s | 150 | ne | najmanje 1, najviše 2 |
| Povezivanje | 4 para, drag-and-drop, *Potvrdi* | 30 s | 150 | 25 % po točnom paru | povezivanje + redoslijed: najmanje 1, najviše 2 |
| Redoslijed | 4–5 koraka, drag-and-drop, *Potvrdi* | 25 s | 150 | ne (sve ili ništa) | vidi gore |
| Nađi bug | klik na liniju, zatim 3 fixa | 40 s | 200 | linija 40 %, fix 60 %; kriva linija = 0 | najmanje 1, najviše 2 |

**Primjeri dodatnih tipova**

- Cloze: `const data = ____ fetch(url);` s opcijama `await`, `.then`, `async`, `yield`.
- Koji ne pripada: `GET`, `PUT`, `DELETE`, `POST` — uljez je `POST` (nije idempotentan).
- Redoslijed: koraci TCP handshakea, JWT auth tok od logina do zaštićenog zahtjeva, React lifecycle od mounta do unmounta.
- Što ispisuje kod: petlja s `var` i `setTimeout`, `[1, 2, 10].sort()`, `0.1 + 0.2 === 0.3`.

**Skaliranje tipa Nađi bug po težini**

| Težina | Duljina koda | Vrsta buga | Fixevi |
| --- | --- | --- | --- |
| D1 | 5–8 linija | očit: off-by-one, kriva usporedba, zaboravljen `return` | 2 od 3 jasno kriva |
| D2 | 8–12 linija | semantički: zaboravljen `await`, mutacija state-a, kriv JOIN | 1 distraktor popravlja simptom, ne uzrok |
| D3 | 12–18 linija | vidljiv tek iz konteksta: closure u petlji, N+1, race condition, nedostaje `WHERE` | 2 distraktora skoro točna |
| D4 | 15–25 linija | ovisi o dvije linije (definicija + upotreba), SQLi kroz konkatenaciju, kriva usporedba hashova | fixevi se razlikuju u jednoj riječi |

**Prikaz koda:** monospace, numerirane linije, označen jezik, bez horizontalnog skrola na mobilnom (max 60 znakova po liniji). Vrijeme čitanja je dio timera; nema zasebne faze čitanja.

**Ostali tipovi po težini:** D1 provjerava definiciju, D2 primjenu, D3 interakciju dva koncepta, D4 rubni slučaj s distraktorima koji su točni u sličnoj situaciji.

**Nađi bug u temi Kako radi AI:** kod koji zove LLM API ili RAG pipeline — šalje cijelu povijest svaki put, parsira JSON bez skidanja code fenceova, nema `max_tokens`, system prompt u user poruci, embeddingi bez normalizacije. Time ograničenje *najmanje 1 nađi bug* vrijedi i za tu temu.

## 8. Sastavljanje decka

Deck builder bira 10 pitanja iz poola tako da 3 ciljaju slabe koncepte, 4 pokrivaju temu nasumično, 2 ponavljaju zakrpano, a prvo zagrijava; oba igrača dobivaju isti deck u istom redoslijedu.

**Ulazi:** tema partije, band težine (prosjek ratinga oba igrača ±1), mastery profili oba igrača po konceptu, redovi ponavljanja oba igrača, povijest viđenih pitanja oba igrača.

**Slotovi**

| Slot | Broj | Izvor koncepta | Pravilo |
| --- | --- | --- | --- |
| Zagrijavanje | 1 | nasumični koncept teme | tip T/N, težina D1, uvijek prvo |
| Ciljano | 3 | koncepti s najnižim masteryjem | prvo koncepti slabi kod oba igrača; zatim koncepti gdje je razlika masteryja ≤ 20 i oba su ispod 70; ako takvih nema, slot postaje jezgra; ako jedan igrač nema profila (prvih 5 partija), ciljani slotovi postaju jezgra; protiv bota sva 3 idu iz igračevih slabih koncepata |
| Jezgra | 4 | nasumični koncepti teme | težina po bandu; savladani i graduirani koncepti ovdje normalno izlaze |
| Ponavljanje | 2 | koncepti dospjeli za review kod bilo kojeg igrača | ako nema dospjelih, slot postaje jezgra |

**Pravila**

1. Nijedan koncept se ne pojavljuje dva puta u istoj partiji.
2. Ograničenja tipova iz §7 (najmanje 1 nađi bug, najmanje 1 output, najmanje 1 drag-and-drop, maksimumi po tipu).
3. Pitanje koje je bilo koji od igrača vidio u zadnjih 30 dana se preskače.
4. Ako koncept nema pitanje traženog tipa ili težine, uzima se susjedna težina, zatim drugi tip. Ako ni to ne uspije, slot postaje jezgra.
5. Slotovi 2–10 se promiješaju; redoslijed je nasumičan, ne po težini.
6. Deck se zamrzava i sprema uz partiju; analiza i ghost dueli ga čitaju odatle.

**Simetrija ciljanja:** ciljano pitanje nikad ne gađa koncept na kojem je jedan igrač slab, a drugi jak, jer bi to bio swing bodova protiv slabijeg. Ciljaju se samo koncepti koje oba igrača znaju otprilike jednako (ne)dobro; pitanje tada provjerava znanje, a ne kažnjava jednog igrača.

**Rotacija ciljanih koncepata:** koncept ciljan u zadnje 2 partije igrača se ne cilja ponovno dok nije zakrpan; na njegovo mjesto dolazi sljedeći po masteryju. Bez toga ista tri koncepta dolaze svaku partiju dok se ne potroše sva pitanja na njima.

**Ažuriranje masteryja po odgovoru**

```latex
m_{t+1} = m_t + \alpha \cdot (r - m_t) \cdot w
```

`r` = 100 za točan odgovor, 0 za netočan (povezivanje i nađi bug proporcionalno djelomičnim bodovima). `w` = težina × pogodivost × kontekst: težina D1 0,7 · D2 0,9 · D3 1,1 · D4 1,3; pogodivost samo za točne odgovore T/N 0,5 · 4 opcije 0,75 · drag-and-drop i nađi bug 1 (netočan odgovor nosi punu težinu jer je jači dokaz); blunder množi s 1,2; trening *bez vremena* množi s 0,5 jer se tamo može guglati. `α` = max(0,15, 1/(n+1)), gdje je `n` broj dosadašnjih odgovora na konceptu: prvi odgovori postavljaju procjenu, kasniji je fino pomiču.

**Primjer:** igrač A (1350) protiv B (1420), tema Baze i SQL, band D2–D3. Mastery: N+1 — A 30, B 35; indeksi — A 35, B 50; transakcije — A 80, B 30. Deck: 1 T/N o NULL-u; ciljano: N+1 (obojici slab), indeksi (razlika 15, oba ispod 70), treći slot postaje jezgra jer su transakcije A-u jake, a B-u slabe; jezgra: JOIN, normalizacija, agregacije, podupiti, GROUP BY i HAVING (iz praznog ciljanog slota); ponavljanje: koncept koji je A zakrpao prije 3 dana, drugi slot postaje jezgra (pogledi) jer B nema dospjelih. Promiješano u slučajan redoslijed.

## 9. Bodovanje, Elo i sezonski bodovi

Točnost dominira, brzina modulira: točan odgovor vrijedi 50 do 100 % baznih bodova ovisno o preostalom vremenu, netočan 0; zbroj odlučuje pobjednika, Elo se računa iz pobjede ili poraza.

**Bodovi po pitanju**

```latex
bodovi = bazni \cdot \left(0{,}5 + 0{,}5 \cdot \frac{preostalo}{ukupno}\right)
```

- Vrijeme se mjeri od prikaza pitanja na igračevom ekranu do zaključavanja odgovora, neovisno o protivniku.
- Netočan odgovor i istek vremena = 0.
- Djelomični bodovi (povezivanje, nađi bug) se množe istim vremenskim faktorom.
- Maksimum partije ovisi o decku (1 150 do 1 400), ali je jednak za oba igrača.

**Elo**

```latex
E_A = \frac{1}{1 + 10^{(R_B - R_A)/400}}, \qquad R_A' = R_A + K \cdot (S_A - E_A)
```

- `S` = 1 pobjeda, 0,5 remi, 0 poraz; `K` = 32 prvih 30 rangiranih partija, zatim 16.
- Start 1000; prvih 10 partija rating nosi oznaku `?` i ne ulazi u leaderboarde.
- Odustajanje i nepovratak nakon prekida veze = poraz s punim gubitkom.
- Rating se nikad ne resetira; sezone imaju vlastiti brojač. U ghost duelu Elo se mijenja samo živom igraču, računano prema ghostovom ratingu iz trenutka snimke.

**Sezonski bodovi (SP)**

| Izvor | SP |
| --- | --- |
| Pobjeda u rangiranoj ili ghost partiji | +10, najviše 100 SP dnevno iz pobjeda (sezonske titule ne smiju mjeriti samo volumen) |
| Pobjeda protiv protivnika s 100+ višim ratingom | +5 dodatno |
| Zakrpan koncept u poduci | +2 (najviše 10 SP dnevno iz učenja) |
| Poraz | 0 |

SP se resetira na kraju sezone (3 mjeseca) nakon što se dodijele titule (§14). Isti koncept donosi SP i coine najviše jednom u 7 dana, da se poduke ne farmaju.

## 10. Nakon partije — rezultat, analiza, poduka

Svaka partija završava na kratkom ekranu rezultata; detaljna analiza i poduka su jedan klik dalje, nikad nametnute, ali nagrađene.

**Ekran rezultata (brief, uvijek)**

- Rezultat u bodovima, pobjednik, Elo prije → poslije, SP, coins i XP zarađeni, streak status.
- Tri kartice: *Blunder* (najskuplje krivo pitanje), *Brilliant* (najbolji odgovor), *1 koncept za vježbu* (koncept s najvećim padom masteryja u ovoj partiji).
- Gumbi: **Detaljna analiza** · **Poduka (3 min, +50)** · **Revanš** · **Nova partija**.
- Napredni igrač koji zna gdje je pogriješio ide ravno na novu partiju; ništa ga ne zadržava.

**Detaljna analiza (na klik)**

- Timeline 10 pitanja: tip, koncept, tvoj odgovor, protivnikov odgovor, točan odgovor, vrijeme oba igrača, bodovi oba igrača, oznaka.
- AI objašnjenje po pitanju, isto za oba igrača, keširano uz pitanje (§16).
- Delta mape znanja: koji su koncepti pali, koji porasli, za koliko.
- Gumb *Prijavi pitanje* uz svako pitanje.

**Oznake po pitanju**

| Oznaka | Uvjet |
| --- | --- |
| Brilliant | D3 ili D4, točno, u manje od 40 % vremena |
| Dobro | točno |
| Miss | netočno ili istek, odgovor nakon više od 60 % vremena |
| Blunder | netočno u manje od 40 % vremena, ili netočno na D1 |

**Poduka (free, opcionalna, nagrađena)**

1. Ulaz za AI: koncepti promašeni u ovoj partiji + do 2 kronične praznine po masteryju + zadnja 3 kriva odgovora igrača na te koncepte.
2. Izlaz: 3 do 5 kartica, svaka = pojam, zašto je bitno, primjer u kodu, česta zabluda. Kartice se čitaju jedna po jedna, swipe ili klik.
3. Kontrolna pitanja: 3 iz poola, deterministički ocijenjena. Jedno je varijanta pitanja koje je igrač promašio (isti koncept, ista težina, drugi tip); dva su nova iz istih koncepata.
4. Prolaz = varijanta promašenog pitanja točna + barem 1 od 2 ostala; ako varijanta padne, koncept nije zakrpan bez obzira na ostala dva, jer je upravo to praznina koju poduka krpa. Koncepti iz poduke dobivaju status *zakrpan* i ulaze u red ponavljanja (§11). Nagrada: 50 coina, 2 SP po konceptu, XP.
5. Pad = kartice ostaju dostupne, kontrola se može ponoviti nakon 10 minuta bez nagrade za drugi pokušaj istog dana.
6. Trajanje 2 do 4 minute. Poduka je dostupna i kasnije iz povijesti partija.

**Premium unutar poduke**

| Mogućnost | Što radi |
| --- | --- |
| Pitaj tutora | chat s kontekstom partije i trenutne kartice; igrač pita za dio koji ne razumije |
| Još pitanja na ovaj koncept | live generiranje dodatnih pitanja za vježbu, s objašnjenjem |
| Dublji uvid | dulja lekcija (8–10 kartica) s više primjera i protuprimjera |
| Objasni svojim riječima | igrač piše objašnjenje koncepta, AI daje feedback što je točno, što nedostaje, što je krivo |
| Napredna statistika | toplinska mapa koncepata kroz vrijeme, usporedba s prosjekom svog banda |

Free uvijek ima: analizu, poduku s kontrolnim pitanjima, neograničen broj poduka dnevno.

**Revanš:** ako je protivnik još online, zahtjev traje 20 s; prihvat preskače matchmaking i ide ravno u novu veto fazu. Nakon poraza gumb glasi *Poduka pa revanš*: poduka od 2 minute, zatim zahtjev za revanš istom protivniku.

## 11. Učenje i napredak

Svaki koncept ima mastery i stanje; zakrpani koncepti se vraćaju po rasporedu ponavljanja dok ne graduiraju, a mapa znanja pokazuje sve to na jednom ekranu.

**Stanja koncepta**

| Stanje | Uvjet | Što se događa |
| --- | --- | --- |
| Nepoznat | nema odgovora | ne ulazi u ciljane slotove; prvi susret kroz jezgru ili kalibraciju |
| Slab | mastery < 40 | kandidat za ciljane slotove i poduku |
| U radu | 40–70 | ulazi u ciljane slotove ako nema slabijih |
| Savladan | ≥ 70 | izlazi samo kroz jezgru i ponavljanje |
| Graduiran | ≥ 85 i 3 uzastopna uspješna ponavljanja | ispada iz reda ponavljanja; jezgra ga i dalje može izvući |

*Zakrpan* je događaj, ne stanje: koncept je prošao kontrolu u poduci i ušao u red ponavljanja. Pad na bilo kojem pitanju tog koncepta spušta mastery po formuli iz §8 i resetira interval.

**Raspored ponavljanja**

| Korak | Nakon | Gdje se ponavlja |
| --- | --- | --- |
| 1 | 1 dan | slot ponavljanja u rangiranoj partiji, trening ili bot |
| 2 | 3 dana | isto |
| 3 | 7 dana | isto |
| 4 | 14 dana | isto |
| 5 | 30 dana | isto; nakon uspjeha koncept graduira ako je mastery ≥ 85 |

- Uspjeh = točan odgovor na dospjelom pitanju; prelazi se na sljedeći korak. Pad = povratak na korak 1.
- D1 koncepti imaju upola kraće intervale, pa kako rating raste, osnove brzo graduiraju i prestaju se vraćati. Dogovorena progresija: s vremenom se ne ponavljaju baš osnove, nego ono što je igraču stvarno teško.
- Dospjeli koncepti su vidljivi na dashboardu kao *Za ponoviti: 3*, s linkom na trening.

**Mapa znanja**

- Po temi: mreža čvorova, jedan po konceptu, boja po stanju (siva nepoznat, crvena slab, žuta u radu, zelena savladan, zlatna graduiran). Gumb Kalibriraj temu (5 pitanja D1–D2, bez nagrade, samo za koncepte u stanju nepoznat) postavlja početni mastery; nudi se nakon prve partije, ne u onboardingu. Klik na čvor otvara zadnja pitanja na tom konceptu i gumb *Vježbaj ovo*.
- Po profilu: radar s 8 tema (mastery po temi), postotak dovršenosti teme (udio graduiranih koncepata), graf ratinga kroz vrijeme.
- 100 % graduiranih koncepata u temi = achievement i trajna titula (*Majstor SQL-a*). Mapa je kolekcija: grind se mjeri u znanju.

**Što AI pamti o igraču:** samo mastery profil i zadnjih 20 krivih odgovora po temi. Svaka poduka dobiva taj kontekst, pa se kartice fokusiraju na ono što igraču kronično ne ide, a kontrolna pitanja provjeravaju je li iz analize nešto ostalo.

**Tjedni izvještaj (obavijest, nedjelja):** partija odigrano, koncepti zakrpani, koncepti graduirani, najčešći tip blundera, rating promjena.

## 12. Bot, trening, dnevni izazov, izazov prijatelja

Sve nerangirane aktivnosti ažuriraju mastery i održavaju streak, ali ne diraju Elo; bot ne zna ništa, samo simulira točnost i vrijeme.

**Bot**

| Persona | Prikazani rating | Točnost | Vrijeme odgovora | Karakter |
| --- | --- | --- | --- | --- |
| Vibe Coder Vinko | 1000 | 50 % | 3–6 s | brz i često kriv; publici i žiriju jasna poruka |
| Junior Jura | 1200 | 60 % | 8–12 s | spor, oprezan |
| Mid Marko | 1500 | 75 % | 6–9 s | solidan |
| Senior Sanja | 1800 | 88 % | 4–7 s | brza i točna |

- Bot ima ključ odgovora. Za svako pitanje izvlači točan odgovor s vjerojatnošću *točnost − 8 % × (težina − 2)*, inače nasumični distraktor; vrijeme iz raspona.
- Deck gradi isti builder; bot nema mastery profil, pa sva 3 ciljana slota idu iz igračevih slabih koncepata.
- Bez Elo-a, bez coina, bez SP-a. Ažurira mastery, broji se za streak i dnevne zadatke.
- Igrač bira personu; preporučena je ona najbliža njegovom ratingu. Imena persona se lokaliziraju s jezikom (EN: Vibe Coder Vince, Junior Jake, Mid Mike, Senior Sarah).

**Trening po temi**

- 10 pitanja iz igračevih najslabijih koncepata odabrane teme, bez protivnika.
- Dva načina: *Bez vremena* (nema timera, objašnjenje odmah nakon svakog pitanja, mastery se pomiče upola) i *Pod vremenom* (timeri kao u duelu, objašnjenja na kraju).
- Dospjeli koncepti iz reda ponavljanja imaju prednost u decku.
- Ažurira mastery, broji se za streak i dnevne zadatke; nema coina osim kroz dnevni zadatak.

**Dnevni izazov**

- Jedno pitanje tipa Nađi bug, težina D2–D3, isto za sve igrače tog dana, unaprijed generirano i validirano.
- Jedan pokušaj, 60 s. Leaderboard dana: prvo točnost, zatim vrijeme.
- Nagrada: 30 coina za točan odgovor, 5 za pokušaj. Broji se za streak.
- Objašnjenje se otključava nakon pokušaja; pitanje ostaje u arhivi izazova.

**Izazov prijatelja**

- Link ili kod sobe; onaj tko ga otvori ulazi u lobby, pa veto faza kao u rangiranoj partiji.
- Nerangirano: bez Elo-a i SP-a, coini kao za poraz (5) bez obzira na ishod. Ažurira mastery i streak.
- Rangirani dueli među prijateljima su roadmap, zbog boostinga.
- Prijatelji: lista, online status, leaderboard prijatelja, gumb *Izazovi* uz svakog online prijatelja.

## 13. Grind i zadržavanje

Tri ciklusa drže igrača: dnevni (streak, zadaci, izazov), tjedni (leaderboard, XP cilj) i sezonski (SP, titule); učenje u svakom od njih vrijedi barem koliko i pobjeda.

**Dnevni ciklus**

- **Streak:** dan se broji za bilo koju dovršenu aktivnost: rangiranu ili ghost partiju, bot duel, trening, položenu poduku ili dnevni izazov. Streak drži naviku, kvalitetu aktivnosti tjeraju dnevni zadaci.
- **Zamrzavanje streaka:** kupuje se za 100 coina, najviše 2 u zalihi; automatski se troši na dan bez aktivnosti.
- **Obavijest u 20:00** ako streak nije osiguran.
- **Dnevni zadaci:** tri zadatka, rotiraju iz liste, resetiraju se u ponoć.

| Zadatak | Nagrada |
| --- | --- |
| Odigraj 2 rangirane partije | 20 coina |
| Zakrpaj 1 koncept | 20 coina |
| Riješi dnevni izazov | 20 coina |
| Odigraj trening na najslabijoj temi | 20 coina |
| Osvoji 3 oznake Brilliant | 20 coina |
| Ponovi sve dospjele koncepte | 20 coina |
| Sva tri zadatka dovršena | +30 coina bonus |

**Tjedni ciklus**

- Tjedni leaderboard (ponedjeljak 00:00 reset) rangira po bodovima osvojenim u rangiranim i ghost partijama tog tjedna.
- Tjedni XP cilj (500 XP) = 100 coina. Top 10 tjednog leaderboarda = značka koja se broji (*3× tjedni Top 10*).

**Sezonski ciklus**

- Sezona traje 3 mjeseca. SP se skuplja po §9.
- Kraj sezone: snapshot sezonskog leaderboarda, dodjela titula (§14), kozmetički drop po tieru, reset SP-a. Elo ostaje.
- Prva dva tjedna sezone: dvostruki SP za zakrpane koncepte, da sezona krene učenjem.

**Ponašanje nakon poraza**

- Gumb *Poduka pa revanš* na ekranu rezultata.
- Nakon 3 uzastopna poraza dashboard predlaže trening na konceptima koji su padali, s ikonom na gumbu *Trening*. Elo se ne ublažava.

**Povratak nakon pauze:** igrač koji se vrati nakon 7+ dana dobiva *Ponovi 3 koncepta* kao prvi predloženi korak umjesto rangirane partije, jer su mu dospjeli reviewi nagomilani.

## 14. Leaderboardi, rank tierovi, titule i achievementi

Pet leaderboarda, šest tierova izvedenih iz Elo-a i tri vrste titula; sezonske i tjedne nagrade ostaju trajno u achievementima, a globalni Elo leaderboard se nikad ne resetira.

**Leaderboardi**

| Leaderboard | Rangira po | Reset | Napomena |
| --- | --- | --- | --- |
| Globalni | Elo | nikad | samo igrači s 10+ rangiranih partija |
| Sezonski | SP | svaka 3 mjeseca | izvor sezonskih titula |
| Tjedni | bodovi iz rangiranih i ghost partija u tjednu | ponedjeljak | izvor tjednih značaka |
| Po temi | broj graduiranih koncepata, tiebreak mastery teme | nikad | jedan po temi; pokazuje tko zna, ne tko pobjeđuje; graduiranje traži 30+ dana ponavljanja pa se ne farma treningom |
| Prijatelji | Elo | nikad | samo lista prijatelja |

Svaki leaderboard prikazuje top 100 i igračevu vlastitu poziciju s 2 igrača iznad i ispod. Za hackathon se pune mock podacima.

**Rank tierovi**

| Tier | Elo |
| --- | --- |
| Bronca | < 1100 |
| Srebro | 1100–1299 |
| Zlato | 1300–1499 |
| Platina | 1500–1699 |
| Dijamant | 1700–1899 |
| Master | 1900+ |
| Grandmaster | globalni top 100, neovisno o Elo-u |

Tier se prikazuje kao značka uz ime u lobbyju, leaderboardima i profilu. Pad tiera ima 25 bodova histereze (iz Zlata se pada tek ispod 1275).

**Titule**

| Vrsta | Kako se dobiva | Trajnost |
| --- | --- | --- |
| Sezonska | snapshot sezonskog leaderboarda: *S1 Top 5*, *S1 Top 50*, *S1 Top 100*, *S1 Dijamant* (najviši dosegnuti tier u sezoni) | trajno u achievementima |
| Znanje | 100 % graduiranih koncepata u temi: *Majstor SQL-a*, *Majstor mreža* | trajno; gubi se ako graduirani koncept padne ispod 70 |
| Tjedna | top 10 tjednog leaderboarda, broji se (*3× tjedni Top 10*) | trajno kao brojač |

Igrač oprema jednu titulu koja se prikazuje uz ime. Titule su dio kozmetike, pa ulaze u istu ekonomiju kao okviri i teme ploče.

**Achievementi (primjeri):** prva pobjeda, 10 Brilliant oznaka, streak 7/30/100 dana, 50 zakrpanih koncepata, pobjeda protiv 200+ jačeg protivnika, sve teme iznad 50 mastery, dnevni izazov 30 dana zaredom.

## 15. Ekonomija — coins, XP, kozmetika, premium

Jedna valuta (coins) koju učenje donosi više nego pobjeda, XP koji vodi level, kozmetika kao jedini sink i premium koji prodaje dubinu poduke, nikad pristup učenju.

**Izvori coina**

| Izvor | Coins |
| --- | --- |
| Položena poduka | 50 |
| Dnevni izazov (točan / pokušaj) | 30 / 5 |
| Dnevni zadatak | 20, bonus 30 za sva tri |
| Streak 7 / 30 / 100 dana | 100 / 500 / 2 000 |
| Pobjeda / poraz / remi u rangiranoj | 15 / 5 / 10 |
| Izazov prijatelja | 5 |
| Tjedni XP cilj | 100 |
| Level up | 50 × level |

Igrač koji svaki dan odigra 2 partije i položi 1 poduku zaradi oko 150 coina dnevno. Isti koncept donosi coine za poduku najviše jednom u 7 dana.

**Sinkovi (kozmetika)**

| Stavka | Cijena | Napomena |
| --- | --- | --- |
| Okvir avatara | 300–800 | po rijetkosti |
| Banner profila | 500 |  |
| Tema ploče (duel ekran) | 800 |  |
| Boja imena | 400 |  |
| Pozadina kartica pitanja | 300 |  |
| Skin bot persone | 600 | roadmap |
| Zamrzavanje streaka | 100 | max 2 u zalihi |

Hackathon: shop s 3–4 stavke, dovoljno da se vidi loop zaradi → potroši.

**XP i level**

- XP za svaki odgovor (5), partiju (50), poduku (100), dnevni izazov (30). Level `n` traži `100 × n` XP.
- Level otključava kozmetičke slotove i bot persone (Senior Sanja od levela 10). Roadmap.

**Premium (mjesečna pretplata)**

| Uključeno | Nije uključeno (nikad) |
| --- | --- |
| Pitaj tutora u poduci | pristup rangiranom |
| Još pitanja na koncept (live generiranje) | analiza partije |
| Dublji uvid (duže lekcije) | osnovna poduka i kontrolna pitanja |
| Objasni svojim riječima s AI feedbackom | broj poduka dnevno |
| Napredna statistika | Elo, SP, pozicija na leaderboardu |
| Mjesečni kozmetički drop i premium značka |  |

Razlog: žiri boduje učenje; gejtanje analize ili poduke bi ga koštalo. Premium prodaje dubinu i interakciju, ne pristup.

## 16. AI u sustavu — gdje se koristi, gdje ne

AI generira sadržaj (pitanja offline, objašnjenja i poduke live) i razgovara s igračem u premiumu; nikad ne ocjenjuje u rangiranoj partiji i nikad ne odlučuje o ratingu, matchmakingu ili decku.

| Upotreba | Kada | Live ili offline | Keširanje | Fallback |
| --- | --- | --- | --- | --- |
| Generiranje poola pitanja | prije objave, po konceptu/tipu/težini | offline, batch | trajno u poolu | ručna dopuna |
| Validacija pitanja | drugi prolaz nad svakim generiranim pitanjem: točnost, jednoznačnost, jedan točan odgovor | offline | — | pitanje ne ulazi u pool |
| Objašnjenje pitanja | prvi put kad netko otvori analizu s tim pitanjem | live, zatim keš | trajno uz pitanje | statički *zapamti* iz poola |
| Kartice poduke | po zahtjevu igrača | live po konceptu i bandu, keš | po (koncept, band) | generičke kartice koncepta |
| Sastav poduke | po zahtjevu igrača | live: izbor koncepata iz profila + zadnji krivi odgovori | — | samo koncepti iz ove partije |
| Kontrolna pitanja | nakon kartica | iz poola, deterministički | — | — |
| Dnevni izazov | unaprijed, 7 dana naprijed | offline + validacija | — | rezervni izazov iz poola |
| Pitaj tutora (premium) | u poduci | live, chat s kontekstom partije i kartice | — | poruka o nedostupnosti |
| Još pitanja na koncept (premium) | u poduci | live + validacijski prolaz prije prikaza | ne ulaze u rangirani pool | — |
| Objasni svojim riječima (premium) | u poduci | live ocjena slobodnog teksta po rubrici (točno / nedostaje / krivo) | — | — |

**Gdje se AI ne koristi:** ocjenjivanje odgovora u duelu, treningu i kontrolnim pitanjima (sve deterministički iz ključa), matchmaking, Elo, deck builder, mastery formula.

**Pravila kvalitete**

- Svako generirano pitanje prolazi validaciju drugim prolazom; uzorak od 10 % ide na ručni pregled prije aktivacije.
- Objašnjenje ima fiksnu strukturu: zašto je točno točno, zašto najčešći distraktor privlači, jedna rečenica *zapamti*. Do 120 riječi.
- Live poziv koji ne vrati odgovor u 8 s pada na fallback iz tablice; igrač nikad ne čeka na prazan ekran.
- Premium generirana pitanja i tutorovi odgovori se ne spremaju u pool; pool raste samo kroz offline batch.

**Trošak po partiji:** objašnjenja su keširana, pa partija na postojećem poolu ne zove AI; prva analiza novog pitanja i svaka poduka su jedini live pozivi za free korisnika.

## 17. Fairness, varanje i rubni slučajevi

Isti deck, neovisni timeri i deterministički ključ čine partiju fer; varanje se ne može ukinuti, ali se čini sporim i neisplativim.

**Fairness**

- Oba igrača: ista pitanja, isti redoslijed, isti ključ, iste vremenske granice.
- Timer kreće na prikazu kod svakog igrača zasebno; odgovor nosi vrijeme mjereno kod igrača. Server prihvaća vrijeme u toleranciji od 1,5 s u odnosu na trenutak kad je pitanje poslano; veće odstupanje se zamjenjuje serverskim vremenom.
- Protivnikov odgovor nije vidljiv do međuekrana, pa nitko ne može kopirati.

**Varanje**

- Timeri od 8 do 40 s i kod koji se mora pročitati čine vanjsko traženje odgovora sporim; sporo točan odgovor vrijedi 50 % baznih bodova.
- Rangirani mod nagrađuje brzinu upravo zato što je brzina dokaz vlastitog znanja.
- Uzorak *stalno ispod 1 s na D3–D4* zastavicom ide na pregled; nema automatskih kazni.
- U prezentaciji se to kaže otvoreno: igra ne može spriječiti da netko pita AI, ali ga čini gubitnikom na vrijeme.

**Integritet ratinga**

- Novi računi: `?` prvih 10 partija, ne ulaze u leaderboarde.
- Isti protivnik najviše 3 rangirane partije zaredom.
- Izazov prijatelja nerangiran.
- Razlika ratinga veća od 400 se ne uparuje.

**Rubni slučajevi**

| Situacija | Ponašanje |
| --- | --- |
| Prekid veze jednog igrača | 20 s za povratak; pitanja teku s 0 bodova; nepovratak do kraja = odustajanje |
| Prekid veze oba igrača | partija poništena, bez promjene Elo-a i SP-a |
| Odustajanje | poraz, puni Elo gubitak, protivnik dobiva pobjedu |
| Oba igrača isteknu na pitanju | 0 bodova oboma, partija ide dalje |
| Neriješeno po bodovima | više točnih odgovora, zatim manji zbroj vremena na točnim; i to isto → remi |
| Pitanje prijavljeno tijekom partije | ništa se ne mijenja do kraja; pitanje ide u karantenu nakon 3 prijave |
| Pitanje povučeno nakon partije | rezultat i Elo ostaju; mastery promjena na tom pitanju se poništava |
| Protivnik ne prihvati pronađeni par | ispada iz reda; drugi ostaje s očuvanim vremenom |
| Revanš odbijen ili istekao | igrač se vraća na ekran rezultata s gumbom *Nova partija* |
| Promjena tema u redu | nije moguća; red se mora napustiti |
| Nedovoljno pitanja u poolu za deck | susjedna težina, pa drugi tip, pa jezgra; ako ni to ne uspije, tema se ne nudi u matchmakingu dok se pool ne dopuni |
| Poduka pokrenuta dva puta za istu partiju | druga je besplatna za ponavljanje, bez nagrade |

## 18. Ekrani i korisnički tok

Trinaest ekrana, jedan primarni gumb na dashboardu i glavni tok od pet koraka koji završava na mapi znanja, ne na ratingu.

**Ekrani**

| Ekran | Sadržaj | Primarna akcija |
| --- | --- | --- |
| Onboarding | registracija, odabir 3–5 tema, odabir jezika (EN zadano, HR opcija) | *Kreni* |
| Dashboard | gumb Igraj; sekundarno Bot, Trening, Dnevni izazov, Prijatelji; chipovi tema; desno radar, streak, graf ratinga, *Za ponoviti: n*, dnevni zadaci | *Igraj* |
| Red čekanja | vrijeme, raspon ratinga, teme, nakon 20 s ponuda bota, nakon 30 s ghosta | *Odustani* |
| Prihvat para | protivnik (ime, tier, rating), 10 s | *Prihvati* |
| Veto | pool tema s vlastitim masteryjem, coin flip, banovi s timerom, random odabir | ban |
| Lobby | protivnik, tema, odbrojavanje | — |
| Duel | pitanje, timer, rezultat uživo, međuekrani | odgovor |
| Rezultat | bodovi, Elo, SP, coins, tri kartice, četiri gumba | *Detaljna analiza* / *Poduka* / *Revanš* / *Nova partija* |
| Analiza | timeline 10 pitanja, objašnjenja, delta mape znanja, prijava pitanja | *Pokreni poduku* |
| Poduka | kartice, kontrolna pitanja, rezultat, premium gumbi | *Dalje* / *Potvrdi* |
| Profil i mapa znanja | radar, mreže koncepata po temi, titule, achievementi, statistika, povijest partija | *Vježbaj ovo* |
| Leaderboardi | tabovi: globalni, sezonski, tjedni, po temi, prijatelji | — |
| Trening / Dnevni izazov / Shop / Postavke | po §12, §15, §2 | — |

**Glavni tok**

1. Dashboard → *Igraj* → red čekanja → prihvat para.
2. Veto → lobby → duel (10 pitanja).
3. Rezultat → *Detaljna analiza* (opcionalno) → *Poduka* (opcionalno) → kontrolna pitanja → *zakrpano*.
4. Povratak na dashboard: radar i *Za ponoviti* su se promijenili, dnevni zadatak je možda dovršen, streak je osiguran.
5. *Revanš* ili *Nova partija* vraća na korak 1 ili 2.

**Sporedni tokovi:** Dashboard → Bot → duel protiv persone → rezultat bez Elo-a; Dashboard → Trening → 10 pitanja → mastery; Dashboard → Dnevni izazov → 1 pitanje → leaderboard dana; Prijatelji → *Izazovi* → link → lobby.

**Načela sučelja**

- Jedan primarni gumb po ekranu; sve ostalo sekundarno ili jedan klik dublje.
- Mapa znanja i rating su uvijek vidljivi na dashboardu, ali rating nikad nije veći od mape.
- Ništa se ne nameće: analiza i poduka su ponuda s nagradom, ne prepreka do sljedeće partije.
- Mobilni prikaz: kod do 60 znakova po liniji, drag-and-drop s velikim ciljevima, timeri vidljivi bez skrola.

**Jezik aplikacije i rječnik naziva**

Aplikacija je na engleskom; hrvatski je dostupan kao opcija u postavkama od prvog dana. Dokument koristi hrvatske nazive, a tablica daje engleske UI nazive koji se koriste u implementaciji.

| U dokumentu | UI (EN) |
| --- | --- |
| Igraj | Play |
| Prihvati | Accept |
| Odustani (red) / Odustani (partija) | Leave queue / Resign |
| Detaljna analiza | Match review |
| Poduka | Lesson |
| Poduka pa revanš | Lesson, then rematch |
| Revanš / Nova partija | Rematch / New match |
| Vježbaj ovo | Practice this |
| Kalibriraj temu | Calibrate topic |
| Za ponoviti | Due for review |
| Prijavi pitanje | Report question |
| Pitaj tutora / Još pitanja / Dublji uvid / Objasni svojim riječima | Ask the tutor / More questions / Deep dive / Explain it yourself |
| Trening / Dnevni izazov / Izazovi | Practice / Daily challenge / Challenge |
| Mapa znanja | Knowledge map |
| Nepoznat / Slab / U radu / Savladan / Graduiran / Zakrpan | Unknown / Weak / In progress / Solid / Graduated / Patched |
| Točno/netočno / Odabir / Cloze u kodu / Koji ne pripada / Što ispisuje kod / Povezivanje / Redoslijed / Nađi bug | True or false / Multiple choice / Fill the blank / Odd one out / What does it print / Match pairs / Put in order / Find the bug |
| Brilliant / Dobro / Miss / Blunder | Brilliant / Good / Miss / Blunder |
| Bronca / Srebro / Zlato / Platina / Dijamant / Master / Grandmaster | Bronze / Silver / Gold / Platinum / Diamond / Master / Grandmaster |
| Majstor SQL-a | SQL Master |
| Ghost duel | Ghost match |
