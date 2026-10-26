# StackOff

**Rangirani 1v1 kviz iz IT znanja u kojem te svaki poraz nauči zašto si izgubio.**

Projekt je nastao na hackathonu **Ri-Hack 4.0** (Rijeka, 23. do 25. listopada 2026.) u timu od troje.
Aplikacija je javno dostupna na **[stackoff.tech](https://stackoff.tech)**, a postoji i kao **Android
aplikacija**.

## Tema hackathona

Ovogodišnja tema bila je *Poticanje ljudi na učenje u doba umjetne inteligencije*. Kad ti AI za par sekundi
napiše kod, lako ga je zalijepiti bez razumijevanja: async kod koji radi dok ne prestane, upit koji je brz na
100 redaka i pada na milijun, CORS greška koja ide ravno u chat. Htjeli smo napraviti nešto što developere
tjera da sami razumiju te stvari, a da im se pritom isplati vraćati svaki dan.

## Ideja

Uzeli smo ono što ljude drži uz šah na chess.com (rating, protivnik tvoje razine, revanš) i primijenili to na
IT znanje. Igraš duel protiv stvarnog igrača ili bota, a nakon partije ne dobiješ samo rezultat nego i analizu
pitanja na kojima si pogriješio te kratku poduku baš za te koncepte. Sljedeći duel namjerno ponovno provjerava
ono što si zabrljao.

Osnovni krug igre:

```text
Duel → Rezultat → Analiza → Poduka → Mastery po konceptu → prilagođeni deck → Duel
```

Rating i znanje vodimo odvojeno. **Elo** kaže koliko si dobar igrač, a **mastery po konceptu** pokazuje što
stvarno znaš. Ta dva broja se nikad ne miješaju.

## Što aplikacija radi

- **Rangirani duel 1v1**: matchmaking po Elo ratingu, zatim veto faza u kojoj igrači naizmjence izbacuju
  teme dok ne ostanu dvije, a tema partije se izvlači nasumično. Slijede pitanja na vrijeme.
- **Duel protiv bota i trening**: igraš bez rizika za rating.
- **Revanš i "ghost" partije**: ponovi duel protiv istog protivnika, a ako u redu nema nikoga, igraš protiv
  snimljene partije stvarnog igrača.
- **Analiza i poduka nakon partije**: objašnjenje svakog krivog odgovora, kartice s gradivom i kontrolna
  pitanja.
- **AI Profesor**: chat s tutorom koji zna kontekst tvoje partije i pitanja, a odgovore streama uživo.
- **Dnevni izazov, prijatelji i izazovi prijateljima, ljestvice.**
- **Ekonomija**: coinsi, XP, trgovina s kozmetikom i pretplata *Senior developer* (plaćanje je simulirano).
- **Prijava** e-mailom ili Google računom, reset lozinke, "zapamti me".
- **Dva jezika**: engleski (zadani) i hrvatski.
- **Web i Android**: ista aplikacija radi u pregledniku i kao Android app, s istim računom i napretkom.

### Pitanja

U bazi je **1072 pitanja** iz 12 tema: JavaScript, frontend, backend, SQL, mreže, sigurnost, algoritmi,
system design, testiranje, Git, Linux i DevOps. Svako pitanje ima koncept, tip i težinu (D1 do D4), a na tome
počiva sve adaptivno u igri.

Osam tipova pitanja: točno/netočno, odabir, cloze u kodu, "koji ne pripada", "što ispisuje kod", povezivanje
parova, redoslijed koraka i "nađi bug" (klikneš na liniju s greškom, pa odabereš ispravak).

### Gdje je AI, a gdje nije

AI (Claude) koristimo tamo gdje stvarno pomaže u učenju: piše objašnjenja krivih odgovora, kartice za poduku
i kontrolne kvizove, a pokreće i AI Profesora. Objašnjenja se spremaju, pa ista pitanja ne troše nove pozive.
AI ne utječe na ocjenjivanje odgovora u duelu, Elo, matchmaking ni sastavljanje decka. Sve se ocjenjuje
deterministički iz ključa, pa je rangirana igra fer i ponovljiva.

## Stack

| Sloj | Tehnologije |
|------|-------------|
| Frontend | React 19, TypeScript, Vite, React Router, Tailwind CSS v4, shadcn/ui (Radix), lowlight za isticanje sintakse koda |
| Backend | ASP.NET Core minimal API na .NET 10, EF Core 10 + Npgsql, JWT autentikacija + Google prijava, OpenAPI + Scalar |
| AI | Anthropic Claude preko službenog .NET SDK-a, streaming odgovora (Server-Sent Events) |
| Android | Capacitor 8: isti React kod zapakiran u nativnu Android aplikaciju, API pozivi idu preko nativnog HTTP-a |
| Baza | PostgreSQL 18 |
| Testovi | xUnit, WebApplicationFactory, Testcontainers (oko 350 testova) |
| Infrastruktura | Docker Compose, GitHub Actions (CI/CD), VPS iza Caddyja s automatskim HTTPS-om |

Backend je podijeljen u slojeve `Api → Application → Domain`, a `Infrastructure` drži bazu, migracije i
integracije. Svaki push na `main` prolazi testove, gradi Docker slike i automatski se deploya na server.

## Tim

| Član | Uloga |
|------|-------|
| Filip Mikan ([@FMikan](https://github.com/FMikan)) | backend, logika igre, AI Profesor, infrastruktura i deploy |
| Matej Medved ([@matejmedo0](https://github.com/matejmedo0)) | razrada ideje igre, UI/UX i frontend |
| [@Sebo2806](https://github.com/Sebo2806) | frontend: prikaz pitanja, isticanje sintakse, lokalizacija |

Tijekom hackathona napravili smo preko 100 commitova.

## Pokretanje lokalno

Potrebni su .NET 10 SDK, Node.js 22+ i Docker.

```bash
cp .env.example .env
docker compose up -d postgres

cd backend
dotnet tool restore
dotnet ef database update --project src/App.Infrastructure --startup-project src/App.Api
dotnet run --project src/App.Api          # http://localhost:5080

cd frontend
npm install
npm run dev                               # http://localhost:5173
```

### Android

Uz to treba Android Studio (Android SDK i JDK 21). Aplikacija se spaja na produkcijski API na stackoff.tech.

```bash
cd frontend
npm run build:android                     # web build + kopiranje u Android projekt
npx cap open android                      # otvori u Android Studiju, pa Run
```

APK se može složiti i iz terminala s `cd frontend/android && ./gradlew assembleDebug`, a nastaje u
`frontend/android/app/build/outputs/apk/debug/`.

Detalji o konfiguraciji, migracijama i strukturi koda su u [docs/development.md](docs/development.md), a
deploy na server u [deploy/README.md](deploy/README.md).

## Dokumentacija

- [Razrada ideje igre](docs/Ri-Hack%204.0%20—%20IDEJA%20IGRE%20v2.md)
- [Tehnički dokument](docs/tehnicki-dokument.md): baza, pravila igre, API ugovor
- [Biznis model](docs/biznis-model.md) i [pretplata Senior developer](docs/premium-pretplata.md)

## Što dalje

- Objava Android aplikacije na Google Play i iOS verzija (Capacitor to već podržava)
- B2B: *StackOff for Teams* za onboarding juniora i *StackOff Hiring* za predselekciju kandidata
- Nove teme: mobile, game dev i "Kako radi AI"
