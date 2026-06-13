# WebSearchServerTasks

Async C# konzolni web server za pretraživanje reči u tekstualnim fajlovima — Task/async/await, cache stampede zaštita, LRU keš, FileSystemWatcher.

## Pokretanje

```bash
git clone https://github.com/lunjae/web-search-server-tasks.git
cd web-search-server-tasks/WebSearchServerTasks
dotnet run
```

Server se pokreće na `http://localhost:5050/`.  
Pritisnite `Q` za zaustavljanje.

## Primer poziva

```
http://localhost:5050/sistemsko&programiranje&elfak&projekat
```

## Arhitektura sistema

```
Browser GET ──► HttpServer (HttpListener)
                    │
                    ▼
            AsyncRequestQueue  ← SemaphoreSlim
                    │
                    ▼
              TaskWorker (max 10 paralelnih Task-ova)
                    │
              ┌─────┴──────┐
              ▼            ▼
          SearchCache   FileSearcher
          (HIT)──────── (MISS → SearchAsync)
              │            │
              └─────┬──────┘
                    ▼
             .ContinueWith(ResponseBuilder.BuildHtml)
                    │
                    ▼
             .ContinueWith(SendResponse + Logger)
                    │
                    ▼
             HTML → Browser

TextFileWatcher ──► novi .txt fajl ──► cache.Clear()
CancellationToken ──► uredno gašenje svih Task-ova
```

## Struktura projekta

```
WebSearchServerTasks/
├── Server/
│   ├── HttpServer.cs        — prima HTTP zahteve, async loop
│   ├── RequestQueue.cs      — thread-safe red, SemaphoreSlim
│   └── TaskWorker.cs        — obrada zahteva, ContinueWith lanac
├── Cache/
│   └── SearchCache.cs       — LRU keš, cache stampede zaštita
├── Search/
│   └── FileSearcher.cs      — async pretraga .txt fajlova
├── Response/
│   └── ResponseBuilder.cs   — generisanje HTML odgovora
├── Watcher/
│   └── TextFileWatcher.cs   — FileSystemWatcher, auto cache clear
├── Logging/
│   └── Logger.cs            — thread-safe Singleton logger
├── TextFiles/               — .txt fajlovi za pretraživanje
└── Program.cs               — entry point, CancellationToken, Q dugme
```

## Analiza mehanizama sinhronizacije

### 1. RequestQueue — SemaphoreSlim

`RequestQueue` koristi `SemaphoreSlim` umesto klasičnog `Monitor.Wait`/`Pulse` iz prvog projekta.

```csharp
await _semaphore.WaitAsync(cancellationToken); // async čekanje — ne blokira nit
```

**Kritična sekcija:** dodavanje i uzimanje zahteva iz `Queue<HttpListenerContext>`.

**Zašto SemaphoreSlim umesto lock?** `lock` blokira nit dok čeka. `SemaphoreSlim.WaitAsync()` oslobađa nit dok čeka — nit može da radi nešto drugo. Ovo je ključna prednost async pristupa pod većim opterećenjem.

---

### 2. TaskWorker — Kontrolisan broj paralelnih Task-ova

```csharp
private readonly SemaphoreSlim _concurrencySemaphore = new SemaphoreSlim(10, 10);

await _concurrencySemaphore.WaitAsync(ct); // čeka slobodno mesto
try { ... }
finally { _concurrencySemaphore.Release(); } // uvek oslobađa
```

**Kritična sekcija:** istovremena obrada zahteva. Maksimalno 10 Task-ova sme da obrađuje zahteve istovremeno.

**`finally` blok je obavezan** — garantuje da se `Release()` uvek pozove, čak i ako Task baci izuzetak. Bez toga, SemaphoreSlim bi se postepeno punio i server bi prestao da obrađuje zahteve.

---

### 3. ContinueWith — Lanac kontinuacija

```csharp
await Task.FromResult(results)
    .ContinueWith(t => _responseBuilder.BuildHtml(t.Result, keywords),
        ct, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.Default)
    .ContinueWith(t => {
        SendResponse(context, t.Result);
        _logger.Info($"Zahtev završen: {cacheKey}");
    }, ct, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.Default);
```

**Zašto ContinueWith?** Pretraga, pravljenje HTML-a i slanje odgovora su prirodno sekvencijalni koraci. `ContinueWith` eksplicitno izražava taj redosled i garantuje da se svaki korak izvršava tek kada prethodni završi. `TaskContinuationOptions.OnlyOnRanToCompletion` osigurava da se kontinuacija ne izvršava ako prethodni Task baci izuzetak.

---

### 4. SearchCache — LRU keš sa TaskCompletionSource

**Strategija:** ograničenje veličine (size-limited LRU — Least Recently Used).

**Strukture podataka:**
- `Dictionary<string, CacheEntry>` — O(1) pristup po ključu
- `LinkedList<string>` — praćenje redosleda korišćenja
- `Dictionary<string, TaskCompletionSource<...>>` — stampede zaštita

**LRU eviction:** kada keš dostigne 50 unosa, briše se unos na kraju `LinkedList` — najduže nekorišćeni. Svaki korišćeni unos se pomera na početak liste.

**Cache stampede zaštita sa TaskCompletionSource:**

```csharp
// Prva nit pravi TCS
var tcs = new TaskCompletionSource<rezultat>();
_inProgress[key] = tcs;

// Ostale niti async čekaju
var rezultat = await _inProgress[key].Task;

// Prva nit postavlja rezultat — sve čekajuće se nastavljaju
tcs.SetResult(rezultat);
```

**Kritična sekcija:** sve operacije nad `_cache` i `_inProgress` Dictionary-jem zaštićene su `lock`-om.

**Zašto TaskCompletionSource umesto Monitor.Wait?** `Monitor.Wait` blokira nit. `TaskCompletionSource` omogućava async čekanje — nit se ne blokira, samo Task čeka na rezultat. Ovo je async ekvivalent `Monitor.Wait`/`Pulse` iz prvog projekta.

---

### 5. Logger — Singleton sa lock-om

```csharp
private static readonly Logger _instance = new Logger(); // thread-safe inicijalizacija
private readonly object _lock = new object();

lock (_lock)
{
    Console.WriteLine(entry);
    File.AppendAllText(_logFilePath, entry + Environment.NewLine);
}
```

**Zašto klasičan lock umesto async?** Pisanje u konzolu i fajl je toliko kratko da async overhead ne bi imao nikakvu korist. Ovo je primer gde klasična sinhronizacija ima više smisla od async pristupa.

**Singleton garantuje** da sve niti i Task-ovi koriste isti `_lock` — bez toga svaki Logger bi imao svoj lock i zaštita ne bi funkcionisala.

---

### 6. TextFileWatcher — FileSystemWatcher

```csharp
_watcher.Created += OnFileChanged;
_watcher.Changed += OnFileChanged;
_watcher.Deleted += OnFileChanged;
```

Kada se detektuje promena u `TextFiles/` folderu, keš se briše:
```csharp
_cache.Clear();
```

`FileSearcher` automatski pronalazi novi fajl jer svaki put poziva `Directory.GetFiles()` iznova — ne čuva listu fajlova u memoriji.

---

## Identifikacija kritičnih sekcija

| Kritična sekcija | Klasa | Mehanizam |
|---|---|---|
| Dodavanje/uzimanje zahteva iz Queue | RequestQueue | SemaphoreSlim |
| Broj paralelnih Task-ova | TaskWorker | SemaphoreSlim (max 10) |
| LRU operacije u kešu | SearchCache | lock |
| Cache stampede | SearchCache | TaskCompletionSource |
| Pisanje u log fajl | Logger | lock |
| Brisanje keša pri promeni fajla | TextFileWatcher | lock (unutar SearchCache) |

---

## Ponašanje sistema pod opterećenjem

### Mali broj zahteva (1-10 istovremeno)
Svi zahtevi se odmah obrađuju. SemaphoreSlim u TaskWorker-u ima dovoljno kapaciteta (max 10), keš se brzo puni i sledeći isti zahtevi dobijaju rezultat trenutno iz keša.

### Srednje opterećenje (10-50 istovremeno)
SemaphoreSlim ograničava na 10 paralelnih obrada. Ostali zahtevi čekaju async u `RequestQueue` bez blokiranja niti. Cache stampede zaštita postaje relevantna — isti zahtev se obrađuje jednom.

### Veliko opterećenje (50+ istovremeno)
`RequestQueue` prima do 100 zahteva u redu. Zahtevi se obrađuju redom kako se oslobađaju Task-ovi. Keš postaje ključan — jednom keširani rezultati se vraćaju bez pretrage fajlova. LRU eviction osigurava da keš ne raste neograničeno.

### Cache stampede scenario
50 zahteva za `sistemsko&elfak` stigne istovremeno, keš je prazan:
- Nit 1: kreira `TaskCompletionSource`, kreće u pretragu
- Niti 2-50: vide TCS u `_inProgress`, await na `tcs.Task`
- Nit 1 završi: `tcs.SetResult(rezultat)` — sve niti 2-50 se instantno nastavljaju
- Rezultat: pretraga urađena **jednom**, svih 50 zahteva dobija odgovor

---

## Testiranje

### Browser
```
http://localhost:5050/sistemsko&programiranje
http://localhost:5050/elfak&projekat&nit
```

### Postman
GET zahtev na `http://localhost:5050/sistemsko&programiranje`

### Stress test
```bash
python stress_test.py
```
Šalje 50 paralelnih zahteva i meri vreme odgovora. U logovima treba da se vidi tačno jedan `CACHE MISS` i ostali `CACHE HIT` za iste ključne reči.
