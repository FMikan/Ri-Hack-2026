# StackOff: biznis model i plan monetizacije

Kako StackOff postaje održiv proizvod, a ne samo hackathon demo. Dokument prati tri faze: prvo baza korisnika i
dokaz da platforma uči, zatim B2C pretplata (Senior developer), zatim B2B alat za regrutaciju i onboarding
juniora. Tehnička izvedba pretplate je u `docs/premium-pretplata.md`.

## 1. Sažetak za žiri

StackOff je rangirani 1v1 kviz iz IT znanja koji svaki poraz pretvara u kratku poduku, a svaki odgovor u mjerljivu
mapu znanja po konceptima. To daje dvije stvari koje se mogu prodati: **igraču** bržu progresiju i neograničenog AI
mentora (pretplata *Senior developer*), a **poslodavcu** provjerljiv profil znanja juniora i alat da ga uvede u tim
(*StackOff for Teams* i *StackOff Hiring*). Prihod ne dolazi od gejtanja učenja: rangirano, analiza i poduka ostaju
besplatni za sve, jer je baza aktivnih igrača ono što B2B kupcima vrijedi.

## 2. Tko su korisnici i tko plaća

| Segment | Što želi | Plaća? |
| --- | --- | --- |
| Studenti i samouki (18–28), bootcamp polaznici | spremiti se za intervju, vidjeti gdje su slabi, natjecanje | besplatno; dio prelazi na Senior |
| Juniori prvih 1–2 godine posla | brzo zakrpati rupe u znanju uz stvarni stack | Senior, ili ih plaća firma (Teams) |
| Firme koje zapošljavaju juniore (agencije, outsourcing, startupi, HR) | brz i fer predizbor kandidata, manje intervjua, dokaz napretka nakon zapošljavanja | **Hiring** i **Teams** |
| Fakulteti, bootcampovi, studentske udruge | motivacija i praćenje grupe, natjecanja | Teams (edu cijena), sponzorirane sezone |

Hrvatska i regija su prvo tržište (hrvatski jezik je već u aplikaciji, dan igre je `Europe/Zagreb`), a engleski
je zadani jezik, pa ništa ne blokira širenje.

## 3. Faze

### Faza 1 (mjeseci 0–6): baza korisnika i dokaz učenja

Cilj: pokazati da StackOff **stvarno uči**, mjerljivo, i skupiti aktivnu bazu. Sve je besplatno; nema
naplate dok nema retencije.

- **Kanali**: studentske udruge i fakulteti (FER, FOI, FIPU, TVZ, Algebra), Discord zajednice, kratki videi
  "jedan duel, jedna lekcija" (TikTok/YouTube Shorts), hackathoni i meetupovi s "StackOff turnirom", LinkedIn
  objave rezultata sezone, referral (pozovi prijatelja = sezonska kozmetika).
- **Petlja rasta koja je već u proizvodu**: izazov prijatelja linkom, javni profil, rang-liste, tjedne i sezonske
  titule, dnevni izazov isti za sve (dijeli se rezultat).
- **Dokaz učenja (metrike koje pokazujemo)**: zakrpani koncepti po korisniku, rast masteryja po temi kroz 30 dana,
  udio poraza koji završe položenom podukom, točnost na ponovljenim pitanjima (red ponavljanja). Sve se već
  bilježi (`concept_mastery`, `answers`, `lessons`).
- **Ciljevi**: 5 000 registriranih, 1 500 tjedno aktivnih, D7 retencija 30 %, D30 15 %.
- **Sadržaj**: pool pitanja raste offline batchom (AI generira, drugi prolaz validira, 10 % ručni pregled), nove
  teme po trendu (LLM/AI inženjering, Rust, Kubernetes, sigurnost), hrvatski pool.

### Faza 2 (mjeseci 6–12): Senior developer (B2C)

Uključuje se pretplata iz `docs/premium-pretplata.md`: ×1,2 na dobitak ratinga i XP, AI Profesor bez dnevnog
limita, 25 novčića dnevno, paket animirane kozmetike, značka. Junior ostaje potpun proizvod s kvotom Profesora
(2 pitanja, 1 poduka, 1 kviz dnevno), što ujedno drži trošak AI-ja besplatnih korisnika pod kontrolom.

- Cilj konverzije 3–5 % aktivnih korisnika (benchmark freemium edu aplikacija 2–6 %).
- Okidači u proizvodu: kvota Profesora (najjači), ekran rezultata (*Senior bonus*), ormarić (legendarne stavke),
  streak (dnevni novčići).
- Godišnji plan s 33 % popusta za niži churn i gotovinu unaprijed.

### Faza 3 (mjesec 12+): B2B, alat za regrutaciju i onboarding juniora

Ovdje je najveći prihod po kupcu i najmanje konkurencije u regiji. Sve se gradi na podacima koji već postoje.

**StackOff Hiring** (regrutacija)
- Firma objavi *assessment link*: kandidat odigra kalibraciju + 3 duela protiv bota / ghost duela na temama firme
  (npr. backend, SQL, Docker). Bez čekanja na protivnika: bot i ghost dueli već postoje.
- Firma dobije **profil znanja**: mastery po konceptu, točnost po tipu pitanja, brzina, najčešći tip greške,
  usporedbu s medijanom kandidata. Mapa znanja i statistika po tipu već se računaju.
- Postojeći igrači mogu podijeliti svoj *verificirani profil* (sezonski rang, zakrpani koncepti) umjesto CV
  retka "poznajem SQL". Javni profil s kontrolom vidljivosti već postoji.
- Protiv varanja: isti deck, neovisni timeri, deterministički ključ (ideja §17); za assessment još i jednokratni
  link, vremenski prozor i uspoređivanje s poviješću igranja.

**StackOff for Teams** (onboarding i rast juniora)
- Tim lead vidi mapu znanja tima po temi, dodjeljuje teme (3–5 aktivnih po igraču već je mehanika), vidi
  tjedni napredak i tko je zapeo. Dnevni zadaci i red ponavljanja rade kao plan učenja bez da ih itko piše.
- Privatne teme za stack firme (pool pitanja iz njihove dokumentacije, offline batch + validacija) i interni
  leaderboard tima (`friends` rang-lista postoji, timska je ista stvar s drugim filtrom).
- AI Profesor kao mentor za cijeli tim (Senior status za sva sjedala).
- Mentorski izvještaj mjesečno (PDF): zakrpani koncepti, mastery delta, preporuke.

**Sponzorirane sezone i izazovi** (employer branding)
- Firma sponzorira temu ili sezonu ("Infobip Docker sezona"): nagrade (kozmetika, titula), njihov logo u
  dnevnom izazovu, opcija da najbolji dobiju poziv na razgovor. Sezone, titule i sezonski dropovi već postoje.

## 4. Prihodovni tokovi i cijene

| Tok | Cijena | Obrazloženje |
| --- | --- | --- |
| **Senior developer** (B2C) | 4,99 € / mj, 39,99 € / god | Ispod Duolingo Super (≈ 7–13 €), Brilliant (≈ 10 €) i LeetCode Premium (≈ 35 $): niša je uža, kupovna moć regije niža, a glavni trošak (AI) je mjerljiv. Godišnji 3,33 € / mj za zaključavanje na 12 mjeseci |
| **StackOff for Teams** | 6 € / sjedalo / mj (min. 5 sjedala), edu 2 € / sjedalo | Firmi je jeftinije od jednog sata mentora mjesečno po junioru; uključuje Senior status, timsku mapu znanja, privatne teme, izvještaj |
| **StackOff Hiring** | 149 € / mj do 50 kandidata, 399 € / mj do 250, enterprise po dogovoru | Jedan uštedjeni tehnički intervju (≈ 2 sata seniora) plaća mjesec. Alternativa: 9 € po kandidatu bez pretplate |
| **Sponzorirana sezona / izazov** | 1 500–5 000 € po sezoni (3 mjeseca) | Employer branding prema 1 000+ aktivnih juniora; cijena raste s bazom |
| Oglasi | **ne** | Kvare duel i poduku; baza korisnika vrijedi više kao B2B publika nego kao oglasni prostor |

Novčići se **ne prodaju za novac**: ekonomija ostaje zatvorena (ideja §15), pa trgovina ne postaje pay-to-win
i ne otvara pitanje kockanja za maloljetne korisnike.

## 5. Troškovi po korisniku

| Stavka | Procjena | Napomena |
| --- | --- | --- |
| AI Profesor, Junior | ≤ 0,20 € / mj | kvota 2 pitanja + 1 poduka + 1 kviz dnevno, ≈ 0,003–0,01 € po pozivu (Haiku za chat, Sonnet za poduku/kviz); većina dana se ne iskoristi |
| AI Profesor, Senior | 0,50–2 € / mj | realno 5–10 poruka dnevno kod aktivnih; rate limit 10/min ostaje; marža na 4,99 € ostaje > 50 % i u lošem slučaju |
| Objašnjenja i kartice poduke | ≈ 0 | keširaju se po pitanju odnosno po (koncept, band); plaća se jednom po sadržaju, ne po korisniku |
| Infrastruktura | 20–40 € / mj do ≈ 10 000 korisnika | jedan VPS, PostgreSQL, Caddy (već u `deploy/`); horizontalno tek kad zatreba SignalR |
| Naplata | 1,5 % + 0,25 € (Stripe, EU kartice); 15 % Google Play za pretplate u aplikaciji | Android tek nakon weba (Capacitor) |
| Sadržaj | 200–500 € / mj | AI batch generiranje + ručni pregled 10 %; raste s brojem tema |

## 6. Projekcija 12 mjeseci nakon uvođenja naplate

Tri scenarija, mjesec 12 nakon početka faze 2 (faza 1 je već dala bazu):

| | Pesimistično | Osnovno | Optimistično |
| --- | --- | --- | --- |
| Mjesečno aktivnih | 3 000 | 8 000 | 20 000 |
| Senior konverzija | 2 % → 60 | 4 % → 320 | 5 % → 1 000 |
| Senior MRR (blend 4,2 €) | 250 € | 1 350 € | 4 200 € |
| Teams sjedala | 20 | 150 | 600 |
| Teams MRR | 120 € | 900 € | 3 600 € |
| Hiring kupci | 1 | 4 | 12 |
| Hiring MRR | 150 € | 800 € | 2 800 € |
| Sponzorirane sezone / god | 0 | 2 | 6 |
| **MRR ukupno** | **≈ 520 €** | **≈ 3 300 €** | **≈ 11 600 €** |

Osnovni scenarij pokriva infrastrukturu, sadržaj i AI s velikom marginom; B2B tokovi su već u 12. mjesecu veći od
B2C, što je i razlog da se faza 1 radi besplatno: Hiring kupac plaća za bazu, ne za feature.

## 7. Zašto baš ovaj model uz ono što već imamo

| Postojeća funkcionalnost | Što omogućuje u modelu |
| --- | --- |
| Mapa znanja po konceptu, mastery, red ponavljanja | profil znanja kandidata (Hiring), timska mapa (Teams), mjesečni izvještaj |
| Bot i ghost dueli, kalibracija | assessment bez čekanja na protivnika i bez organiziranja termina |
| Isti deck, neovisni timeri, deterministički ključ, prijave pitanja | fer i provjerljiv rezultat, bitno kad rezultat utječe na posao |
| Ledger (coins/XP/SP), dnevni zadaci, streak | sve Senior pogodnosti su jedan redak u ledgeru ili jedan množitelj, bez nove ekonomije |
| AI Profesor s kvotom | jasan razlog za pretplatu i kontroliran trošak besplatnih korisnika |
| Sezone, titule, dropovi, rang-liste | sponzorirane sezone i employer branding bez novog koda |
| Javni profil s vidljivošću, prijatelji, izazov linkom | verificirani profil za CV, viralnost bez oglasa |
| Trgovina s rijetkostima | paket dobrodošlice i mjesečni drop kao status, ne kao moć |
| EN/HR, mobile-first, Capacitor plan | regija prvo, Android kao sljedeći kanal distribucije |

## 8. Rizici i kako ih držimo pod kontrolom

| Rizik | Mitigacija |
| --- | --- |
| Dojam pay-to-win zbog ×1,2 na rating | množi se samo dobitak, matchmaking ostaje po ratingu, značka je vidljiva; rezervna opcija je množitelj samo na sezonske bodove (jedan redak koda, `premium-pretplata.md` §8) |
| Trošak AI-ja raste s uspjehom | kvota za Junior, rate limit za sve, keš objašnjenja i kartica, jeftiniji model za chat; prag po korisniku s upozorenjem |
| Kvaliteta pitanja (krivi ključ) | drugi validacijski prolaz, 10 % ručni pregled, prijave igrača (3 prijave = karantena), auto-kalibracija težine; sve već u sustavu |
| Varanje u assessmentu | jednokratni linkovi, vremenski prozor, usporedba s poviješću igranja, deterministički ključ; za finalni krug firma i dalje radi intervju, StackOff skraćuje predizbor |
| Churn pretplate | godišnji plan, dnevni novčići i streak kao razlog povratka, mjesečni drop (roadmap) |
| Mali broj B2B kupaca u regiji | kreće se s 3–5 pilot firmi iz mreže hackathona/udruga uz besplatni pilot 2 mjeseca; engleski od prvog dana za širenje |
| Pravila trgovina aplikacija | pretplate u Android aplikaciji preko Play Billinga; web ostaje primarni kanal s nižom provizijom |

## 9. Što mjerimo

- Rast i retencija: registracije, WAU/MAU, D1/D7/D30, prosječno duela po tjednu.
- Učenje: zakrpani koncepti po korisniku, mastery delta 30 dana, udio poraza s položenom podukom.
- Monetizacija: konverzija Junior → Senior, churn, ARPU, udio godišnjih; Teams sjedala i aktivacija (odigrano
  u prvih 7 dana); Hiring: kandidata po kupcu, vrijeme do odluke.
- Trošak: AI trošak po aktivnom korisniku i po Senioru, trošak sadržaja po pitanju.

## 10. Sljedeći koraci nakon hackathona

1. Spojiti Stripe Checkout i webhook (plan u `premium-pretplata.md` §5), Google Play Billing uz Android.
2. Izvoz profila znanja (PDF/link) kao prvi B2B artefakt, jer koristi i Senior korisniku.
3. Pilot s 3 firme: assessment link + profil kandidata, bez naplate, uz mjerenje "koliko intervjua je uštedio".
4. Timska mapa znanja i privatne teme (Teams), sponzorirana sezona s jednim partnerom.
