import asyncio
import aiohttp
import time
import os

# Konfiguracija
SERVER_URL = "http://localhost:5050"
KEYWORDS = "sistemsko&programiranje&elfak&projekat"
NUM_REQUESTS = 200
LOG_PATH = r"D:\Fakultet\III godina\III_godina-projekti\Sistemsko\web-search-server-tasks\WebSearchServerTasks\bin\Debug\net10.0\server.log"

async def send_request(session, request_id):
    url = f"{SERVER_URL}/{KEYWORDS}"
    start = time.time()
    try:
        async with session.get(url) as response:
            await response.text()
            elapsed = time.time() - start
            print(f"[Zahtev {request_id:02d}] Status: {response.status} | Vreme: {elapsed:.3f}s")
            return elapsed
    except Exception as e:
        elapsed = time.time() - start
        print(f"[Zahtev {request_id:02d}] GREŠKA: {e} | Vreme: {elapsed:.3f}s")
        return None

def read_new_log_lines(log_size_before):
    """Čita samo nove linije dodane tokom testa"""
    try:
        with open(LOG_PATH, "r", encoding="utf-8") as f:
            f.seek(log_size_before)
            new_content = f.read()

        lines = new_content.strip().split("\n")

        miss = sum(1 for l in lines if "[CACHE]: MISS" in l)
        hit  = sum(1 for l in lines if "[CACHE]: HIT"  in l)
        wait = sum(1 for l in lines if "[CACHE]: WAIT" in l)
        set_ = sum(1 for l in lines if "[CACHE]: SET"  in l)

        print(f"\n📊 Cache statistika (samo ovaj test):")
        print(f"   MISS : {miss}  ← pretraga pokrenuta")
        print(f"   WAIT : {wait}  ← čekale na rezultat")
        print(f"   SET  : {set_}  ← rezultat upisan u keš")
        print(f"   HIT  : {hit}  ← iz keša, bez pretrage")
        print(f"   Ukupno cache događaja: {miss + hit + wait + set_}")

        if miss == 1:
            print("\n✅ Cache stampede zaštita radi ispravno — tačno 1 MISS!")
        elif miss == 0:
            print("\n✅ Sve iz keša — HIT only! (pokreni test sa praznim kešom za MISS)")
        else:
            print(f"\n⚠️  Cache stampede problem — {miss} MISS umesto 1!")

    except Exception as e:
        print(f"⚠️  Greška pri čitanju loga: {e}")

async def run_stress_test():
    # Zapamti veličinu loga PRE testa
    log_size_before = 0
    try:
        log_size_before = os.path.getsize(LOG_PATH)
    except FileNotFoundError:
        pass

    print(f"Stress test — {NUM_REQUESTS} paralelnih zahteva na {SERVER_URL}/{KEYWORDS}")
    print("-" * 60)

    start_total = time.time()

    async with aiohttp.ClientSession() as session:
        tasks = [send_request(session, i + 1) for i in range(NUM_REQUESTS)]
        results = await asyncio.gather(*tasks)

    total_time = time.time() - start_total

    successful = [r for r in results if r is not None]
    failed = NUM_REQUESTS - len(successful)

    print("-" * 60)
    print(f"Ukupno zahteva:     {NUM_REQUESTS}")
    print(f"Uspešnih:           {len(successful)}")
    print(f"Neuspešnih:         {failed}")
    if successful:
        print(f"Najbrži odgovor:    {min(successful):.3f}s")
        print(f"Najsporiji odgovor: {max(successful):.3f}s")
        print(f"Prosečno vreme:     {sum(successful)/len(successful):.3f}s")
    print(f"Ukupno vreme:       {total_time:.3f}s")
    print("-" * 60)

    # Čita samo nove linije iz loga
    read_new_log_lines(log_size_before)

if __name__ == "__main__":
    try:
        import aiohttp
    except ImportError:
        print("Instaliraj aiohttp: pip install aiohttp")
        exit(1)

    asyncio.run(run_stress_test())